using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G13: catalogue matching, runway with contracts and review states, notice re-firing, patch position.</summary>
public class LifecycleCatalogueGapTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static VersionIdentity Identity(string family = "SQL Server", int major = 16, int? minor = 0, string build = "", string edition = "", string train = "") => new()
    {
        ResolutionStatus = VersionResolutionStatus.Resolved,
        Family = family,
        Major = major,
        Minor = minor,
        Build = build,
        Edition = edition,
        PatchTrain = train
    };

    private static ReleaseCatalogEntry Entry(string label, DatabaseEngine engine = DatabaseEngine.SqlServer, string family = "SQL Server", int major = 16, int? minor = 0,
        string edition = "", string train = "", ReleaseChannel channel = ReleaseChannel.GA, DateTime? mainstream = null) => new()
    {
        Engine = engine,
        Family = family,
        ReleaseLabel = label,
        Major = major,
        Minor = minor,
        Edition = edition,
        PatchTrain = train,
        Channel = channel,
        MainstreamSupportEndUtc = mainstream ?? Now.AddDays(100)
    };

    // ---- ReleaseCatalogMatcher ----

    [Fact]
    public void Matcher_EditionSpecificEntry_DoesNotMatchAnotherEdition()
    {
        var entries = new[] { Entry("Express", edition: "Express") };
        Assert.Null(ReleaseCatalogMatcher.Match(entries, DatabaseEngine.SqlServer, Identity(edition: "Enterprise")));
    }

    [Fact]
    public void Matcher_EditionSpecificEntry_DoesNotMatchAnIdentityWithNoEditionRecorded()
    {
        var entries = new[] { Entry("Express", edition: "Express") };
        Assert.Null(ReleaseCatalogMatcher.Match(entries, DatabaseEngine.SqlServer, Identity()));
    }

    [Fact]
    public void Matcher_PrefersTheEditionSpecificEntry_OverTheGenericOne()
    {
        var entries = new[] { Entry("Generic"), Entry("Enterprise", edition: "Enterprise") };
        Assert.Equal("Enterprise", ReleaseCatalogMatcher.Match(entries, DatabaseEngine.SqlServer, Identity(edition: "enterprise"))!.ReleaseLabel);
    }

    [Fact]
    public void Matcher_PatchTrainMustAgree()
    {
        var entries = new[] { Entry("Innovation", DatabaseEngine.MySql, "MySQL", 8, 4, train: "Innovation"), Entry("LTS", DatabaseEngine.MySql, "MySQL", 8, 4, train: "LTS") };
        Assert.Equal("LTS", ReleaseCatalogMatcher.Match(entries, DatabaseEngine.MySql, Identity("MySQL", 8, 4, train: "LTS"))!.ReleaseLabel);
    }

    [Fact]
    public void Matcher_NeverMatchesAPreview()
    {
        var entries = new[] { Entry("Preview", channel: ReleaseChannel.Preview) };
        Assert.Null(ReleaseCatalogMatcher.Match(entries, DatabaseEngine.SqlServer, Identity()));
    }

    [Fact]
    public void Matcher_MySqlMinorPicksTheBranch_So80DoesNotGet84Dates()
    {
        var entries = new[] { Entry("MySQL 8.4 LTS", DatabaseEngine.MySql, "MySQL", 8, 4) };
        Assert.Null(ReleaseCatalogMatcher.Match(entries, DatabaseEngine.MySql, Identity("MySQL", 8, 0, "36")));
    }

    [Fact]
    public void Matcher_PostgreSqlMinorIsAPatchLevel_SoTheMajorEntryApplies()
    {
        var entries = new[] { Entry("PostgreSQL 16.4", DatabaseEngine.PostgreSql, "PostgreSQL", 16, 4) };
        Assert.NotNull(ReleaseCatalogMatcher.Match(entries, DatabaseEngine.PostgreSql, Identity("PostgreSQL", 16, 2)));
    }

    [Fact]
    public void Matcher_PrefersAnEntryThatCarriesDates()
    {
        var undated = Entry("8.0.36 build", DatabaseEngine.MySql, "MySQL", 8, 0);
        undated.MainstreamSupportEndUtc = null;
        undated.Build = "36";
        var dated = Entry("MySQL 8.0", DatabaseEngine.MySql, "MySQL", 8, 0);
        Assert.Equal("MySQL 8.0", ReleaseCatalogMatcher.Match([undated, dated], DatabaseEngine.MySql, Identity("MySQL", 8, 0, "36"))!.ReleaseLabel);
    }

    // ---- SupportRunwayCalculator.Evaluate ----

    [Fact]
    public void Evaluate_IdentityNeedingReview_IsNeedsReview_NotUnknown()
    {
        var identity = Identity();
        identity.ResolutionStatus = VersionResolutionStatus.NeedsReview;
        identity.UnresolvedReason = "Reported as MySQL but looks like MariaDB.";

        var result = SupportRunwayCalculator.Evaluate(identity, Entry("x"), [], Now);

        Assert.True(result.NeedsReview);
        Assert.True(result.IsUnknown);
        Assert.Equal(SupportPhase.NeedsReview, result.Phase);
    }

    [Fact]
    public void Evaluate_UnresolvedIdentity_IsUnknown_NotNeedsReview()
    {
        var identity = Identity();
        identity.ResolutionStatus = VersionResolutionStatus.Unresolved;
        var result = SupportRunwayCalculator.Evaluate(identity, null, [], Now);
        Assert.Equal(SupportPhase.Unknown, result.Phase);
        Assert.False(result.NeedsReview);
    }

    [Fact]
    public void Evaluate_ConflictingDates_IsNeedsReview()
    {
        var entry = Entry("x");
        entry.DatesConflict = true;
        Assert.Equal(SupportPhase.NeedsReview, SupportRunwayCalculator.Evaluate(Identity(), entry, [], Now).Phase);
    }

    [Fact]
    public void Evaluate_ExpiredOverride_IsNeedsReview_AndItsDatesAreNotUsed()
    {
        var entry = Entry("x", mainstream: Now.AddDays(400));
        entry.IsManualOverride = true;
        entry.OverrideExpiresUtc = Now.AddDays(-1);

        var result = SupportRunwayCalculator.Evaluate(Identity(), entry, [], Now);

        Assert.True(result.NeedsReview);
        Assert.Null(result.DaysToNextMilestone);
        Assert.Contains("expired", result.UnknownReason);
    }

    [Fact]
    public void Evaluate_ActiveOverride_IsUsed()
    {
        var entry = Entry("x", mainstream: Now.AddDays(40));
        entry.IsManualOverride = true;
        entry.OverrideExpiresUtc = Now.AddDays(10);
        Assert.Equal(40, SupportRunwayCalculator.Evaluate(Identity(), entry, [], Now).DaysToNextMilestone);
    }

    [Fact]
    public void Evaluate_EsuDatesWithoutAnExtendedContract_AreNotCounted()
    {
        var entry = Entry("x", mainstream: Now.AddDays(-400));
        entry.ExtendedSupportEndUtc = Now.AddDays(-10);
        entry.EsuSupportEndUtc = Now.AddDays(700);

        var result = SupportRunwayCalculator.Evaluate(Identity(), entry, [], Now);

        Assert.Equal(SupportPhase.EndOfLife, result.Phase);
        Assert.True(result.DaysToNextMilestone < 0);
        Assert.Contains("no active Extended coverage contract", result.CoverageNote);
    }

    [Fact]
    public void Evaluate_EsuWithAnActiveExtendedContract_RunsToTheEarlierOfContractEndAndEsuEnd()
    {
        var entry = Entry("x", mainstream: Now.AddDays(-400));
        entry.ExtendedSupportEndUtc = Now.AddDays(-10);
        entry.EsuSupportEndUtc = Now.AddDays(700);
        var contract = new SupportContract { Vendor = "Microsoft", CoverageType = SupportCoverageType.Extended, CoverageStartUtc = Now.AddDays(-30), CoverageEndUtc = Now.AddDays(200) };

        var result = SupportRunwayCalculator.Evaluate(Identity(), entry, [contract], Now);

        Assert.Equal(SupportPhase.Esu, result.Phase);
        Assert.Equal(200, result.DaysToNextMilestone);
        Assert.Contains("Microsoft", result.CoverageNote);
    }

    [Fact]
    public void Evaluate_ExpiredOrStandardContract_DoesNotCoverEsu()
    {
        var entry = Entry("x", mainstream: Now.AddDays(-400));
        entry.EsuSupportEndUtc = Now.AddDays(700);
        var expired = new SupportContract { Vendor = "A", CoverageType = SupportCoverageType.Extended, CoverageEndUtc = Now.AddDays(-1) };
        var standard = new SupportContract { Vendor = "B", CoverageType = SupportCoverageType.Standard };

        Assert.Equal(SupportPhase.EndOfLife, SupportRunwayCalculator.Evaluate(Identity(), entry, [expired, standard], Now).Phase);
    }

    // ---- SupportRunwayNoticeEvaluator.Decide ----

    private static readonly int[] Thresholds = [365, 180, 90, 30];

    private static SupportRunwayResult Deadline(int days, DateTime milestone) =>
        new(SupportPhase.Mainstream, "Mainstream support ends", milestone, days, false, "");

    [Fact]
    public void Decide_SameMilestone_DoesNotRepeat()
    {
        var milestone = Now.AddDays(20);
        var last = new RunwayNoticeState(30, milestone, RunwayNoticeKinds.Deadline);
        Assert.False(SupportRunwayNoticeEvaluator.Decide(Deadline(20, milestone), Thresholds, last).Notify);
    }

    [Fact]
    public void Decide_RevisedMilestoneDate_FiresAgain()
    {
        var last = new RunwayNoticeState(30, Now.AddDays(20), RunwayNoticeKinds.Deadline);
        var revised = Now.AddDays(25);

        var decision = SupportRunwayNoticeEvaluator.Decide(Deadline(25, revised), Thresholds, last);

        Assert.True(decision.Notify);
        Assert.Equal(revised, decision.NewState.MilestoneUtc);
        Assert.Equal(30, decision.NewState.ThresholdDays);
    }

    [Fact]
    public void Decide_DateMovedFarOut_ClearsDedupe_SoTheLaterThresholdsFireAgain()
    {
        var last = new RunwayNoticeState(0, Now.AddDays(-5), RunwayNoticeKinds.Deadline);
        var decision = SupportRunwayNoticeEvaluator.Decide(Deadline(500, Now.AddDays(500)), Thresholds, last);

        Assert.False(decision.Notify);
        Assert.True(decision.StateChanged);
        Assert.Null(decision.NewState.ThresholdDays);
    }

    [Fact]
    public void Decide_Unknown_NotifiesOnce()
    {
        var unknown = SupportRunwayResult.Unknown("No matching release-catalogue entry.");
        var first = SupportRunwayNoticeEvaluator.Decide(unknown, Thresholds, RunwayNoticeState.None);
        Assert.True(first.Notify);

        var second = SupportRunwayNoticeEvaluator.Decide(unknown, Thresholds, first.NewState);
        Assert.False(second.Notify);
    }

    [Fact]
    public void Decide_UnknownThenNeedsReview_AreSeparateNotices()
    {
        var afterUnknown = new RunwayNoticeState(null, null, RunwayNoticeKinds.Unknown);
        Assert.True(SupportRunwayNoticeEvaluator.Decide(SupportRunwayResult.NeedsReviewResult("conflict"), Thresholds, afterUnknown).Notify);
    }

    [Fact]
    public void Decide_LegacyStateWithoutMilestone_KeepsItsDedupe()
    {
        var legacy = new RunwayNoticeState(30, null, null);
        Assert.False(SupportRunwayNoticeEvaluator.Decide(Deadline(20, Now.AddDays(20)), Thresholds, legacy).Notify);
    }

    // ---- PatchBaselineComparer ----

    [Fact]
    public void Baseline_ExpiredOverrideOnTarget_IsNotShownAsApproved()
    {
        var target = Entry("8.0.43", DatabaseEngine.MySql, "MySQL", 8, 0);
        target.Build = "43";
        target.IsManualOverride = true;
        target.OverrideExpiresUtc = Now.AddDays(-1);
        var baseline = new ApprovedPatchBaseline { IsActive = true, TargetReleaseCatalogEntry = target };

        var result = PatchBaselineComparer.Compare(Identity("MySQL", 8, 0, "36"), [target], baseline, Now);

        Assert.Null(result.ApprovedTarget);
        Assert.Null(result.LatestApplicableBranchPatch);
        Assert.Contains("expired manual override", result.Notes);
    }

    [Fact]
    public void Baseline_BuildLevelComparison_AndRequiredBy()
    {
        var target = Entry("8.0.43", DatabaseEngine.MySql, "MySQL", 8, 0);
        target.Build = "43";
        var baseline = new ApprovedPatchBaseline { IsActive = true, TargetReleaseCatalogEntry = target, RequiredByUtc = Now.AddDays(-2) };

        var result = PatchBaselineComparer.Compare(Identity("MySQL", 8, 0, "36"), [target], baseline, Now);

        Assert.Equal(BuildPosition.Behind, result.InstalledVsApproved);
        Assert.Equal(BuildPosition.Behind, result.InstalledVsLatestPatch);
        Assert.True(result.IsPastRequiredBy(Now));
    }

    [Fact]
    public void Baseline_OnTarget_IsNotPastRequiredBy()
    {
        var target = Entry("8.0.43", DatabaseEngine.MySql, "MySQL", 8, 0);
        target.Build = "43";
        var baseline = new ApprovedPatchBaseline { IsActive = true, TargetReleaseCatalogEntry = target, RequiredByUtc = Now.AddDays(-2) };

        var result = PatchBaselineComparer.Compare(Identity("MySQL", 8, 0, "43"), [target], baseline, Now);

        Assert.Equal(BuildPosition.Same, result.InstalledVsApproved);
        Assert.False(result.IsPastRequiredBy(Now));
    }

    [Fact]
    public void Baseline_BranchPatchRespectsThePatchTrain()
    {
        var cu = Entry("CU 20", minor: 0, train: "CU");
        cu.Build = "4205";
        var gdr = Entry("GDR", minor: 0, train: "GDR");
        gdr.Build = "1135";

        var result = PatchBaselineComparer.Compare(Identity(build: "1100", train: "GDR"), [cu, gdr], null, Now);

        Assert.Equal("GDR", result.LatestApplicableBranchPatch!.ReleaseLabel);
    }

    [Fact]
    public void ComparePosition_MissingBuild_IsNotComparable()
    {
        Assert.Equal(BuildPosition.NotComparable, PatchBaselineComparer.ComparePosition(Identity(build: ""), Entry("x")));
    }

    // ---- Advisory ranking and review ----

    private static AdvisoryInstanceAssessment Assessment(AdvisorySeverity severity, AdvisoryClassification classification, string id) => new()
    {
        Classification = classification,
        AdvisoryRecord = new AdvisoryRecord { Severity = severity, VendorAdvisoryId = id }
    };

    [Fact]
    public void Ranker_ConfirmedHighOnACriticalService_OutranksPotentialCriticalOnALowService()
    {
        var critical = new DatabaseInstance { Criticality = InstanceCriticality.Critical, Environment = EnvironmentTier.Production };
        var low = new DatabaseInstance { Criticality = InstanceCriticality.Low, Environment = EnvironmentTier.Development };

        var ranked = AdvisoryPriorityRanker.Rank([
            (Assessment(AdvisorySeverity.Critical, AdvisoryClassification.Potential, "A"), low),
            (Assessment(AdvisorySeverity.High, AdvisoryClassification.Confirmed, "B"), critical)]);

        Assert.Equal("B", ranked[0].Assessment.AdvisoryRecord!.VendorAdvisoryId);
    }

    [Fact]
    public void Ranker_NotApplicableSortsLast_AndUnknownSeverityIsNotTreatedAsLow()
    {
        var instance = new DatabaseInstance { Criticality = InstanceCriticality.Medium };
        var ranked = AdvisoryPriorityRanker.Rank([
            Assessment(AdvisorySeverity.Critical, AdvisoryClassification.NotApplicable, "NA"),
            Assessment(AdvisorySeverity.Low, AdvisoryClassification.Potential, "LOW"),
            Assessment(AdvisorySeverity.Unknown, AdvisoryClassification.Potential, "UNK")], instance);

        Assert.Equal(["UNK", "LOW", "NA"], ranked.Select(r => r.Assessment.AdvisoryRecord!.VendorAdvisoryId));
    }

    [Theory]
    [InlineData("", "Owner", true, AdvisoryVerificationStatus.Unverified, "Evidence is required")]
    [InlineData("seen", "", true, AdvisoryVerificationStatus.Unverified, "owner")]
    [InlineData("seen", "Owner", false, AdvisoryVerificationStatus.InProgress, "deadline")]
    public void ReviewValidator_ConfirmedNeedsEvidenceOwnerAndDeadline(string evidence, string owner, bool hasDeadline, AdvisoryVerificationStatus status, string expected)
    {
        var error = AdvisoryReviewValidator.Validate(AdvisoryClassification.Confirmed, evidence, owner, hasDeadline ? Now : null, status);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void ReviewValidator_VerifiedConfirmedNeedsNoDeadline()
    {
        Assert.Null(AdvisoryReviewValidator.Validate(AdvisoryClassification.Confirmed, "patched", "Priya", null, AdvisoryVerificationStatus.Verified));
    }

    [Fact]
    public void ReviewValidator_NotApplicableNeedsOnlyEvidence()
    {
        Assert.Null(AdvisoryReviewValidator.Validate(AdvisoryClassification.NotApplicable, "plugin not installed", "", null, AdvisoryVerificationStatus.Unverified));
    }

    // ---- Drift ----

    [Fact]
    public void Drift_ObservedEditionDifferent_IsDrift()
    {
        var declared = Identity("PostgreSQL", 16, 2);
        declared.RawVersionText = "16.2";
        declared.Edition = "Community";
        var observation = new VersionObservation { RawVersionText = "16.2", ObservedEdition = "EnterpriseDB", Outcome = ObservationOutcome.Success };

        var result = VersionDriftDetector.Detect(declared, observation, DatabaseEngine.PostgreSql);

        Assert.Equal(DriftStatus.Drift, result.Status);
        Assert.Contains("Edition", result.DifferingFields);
    }
}
