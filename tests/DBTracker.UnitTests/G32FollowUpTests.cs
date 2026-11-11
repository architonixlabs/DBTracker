using System.Text.Json;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G32: inbox query filter, restore corrections, runway notices without an identity, hosted databases at retirement,
/// backdated entry times, report-send feedback and evidence audit snapshots.</summary>
public class G32FollowUpTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc);

    // ---- Notification inbox filter matches the in-memory rule ----

    private static readonly Notification[] SampleNotifications =
    [
        new() { Id = 1, DatabaseInstanceId = 10, Company = "Contoso", OwnerTeamId = 1 },
        new() { Id = 2, DatabaseInstanceId = 11, Company = "contoso", OwnerTeamId = null },
        new() { Id = 3, DatabaseInstanceId = 12, Company = "Fabrikam", OwnerTeamId = 2 },
        new() { Id = 4, DatabaseInstanceId = null, Company = "", OwnerTeamId = null },
        new() { Id = 5, DatabaseInstanceId = 13, Company = " ", OwnerTeamId = 3 }
    ];

    public static TheoryData<string> SubscriptionSets => new()
    {
        "none", "all-inapp", "all-email", "instance-10", "company-contoso", "team-2", "mixed", "email-only-instance", "blank-company"
    };

    private static List<NotificationSubscription> Subscriptions(string set) => set switch
    {
        "none" => [],
        "all-inapp" => [new() { Scope = SubscriptionScope.AllInstances, Channel = NotificationChannel.InApp }],
        "all-email" => [new() { Scope = SubscriptionScope.AllInstances, Channel = NotificationChannel.Email }],
        "instance-10" => [new() { Scope = SubscriptionScope.Instance, DatabaseInstanceId = 10, Channel = NotificationChannel.Both }],
        "company-contoso" => [new() { Scope = SubscriptionScope.Company, Company = "CONTOSO", Channel = NotificationChannel.InApp }],
        "team-2" => [new() { Scope = SubscriptionScope.Team, TeamId = 2, Channel = NotificationChannel.InApp }],
        "mixed" =>
        [
            new() { Scope = SubscriptionScope.Instance, DatabaseInstanceId = 12, Channel = NotificationChannel.Email },
            new() { Scope = SubscriptionScope.Team, TeamId = 3, Channel = NotificationChannel.Both },
            new() { Scope = SubscriptionScope.Company, Company = "Fabrikam", Channel = NotificationChannel.InApp }
        ],
        "email-only-instance" => [new() { Scope = SubscriptionScope.Instance, DatabaseInstanceId = 10, Channel = NotificationChannel.Email }],
        "blank-company" => [new() { Scope = SubscriptionScope.Company, Company = " ", Channel = NotificationChannel.InApp }],
        _ => throw new ArgumentOutOfRangeException(nameof(set))
    };

    [Theory]
    [MemberData(nameof(SubscriptionSets))]
    public void InboxFilter_SelectsExactlyWhatShowsInInboxSelects(string set)
    {
        var subscriptions = Subscriptions(set);
        var filter = NotificationInboxFilter.For(subscriptions).Compile();

        var expected = SampleNotifications
            .Where(n => NotificationSubscriptionMatcher.ShowsInInbox(subscriptions, n.DatabaseInstanceId ?? 0, n.Company, n.OwnerTeamId))
            .Select(n => n.Id);
        var actual = SampleNotifications.Where(filter).Select(n => n.Id);

        Assert.Equal(expected, actual);
    }

    // ---- Restore corrections decide restore-tested ----

    private static RestoreEvent Restore(int id, RestoreOutcome outcome, DateTime at, BackupSource source = BackupSource.Manual, params RestoreEventCorrection[] corrections)
    {
        var restore = new RestoreEvent { Id = id, Outcome = outcome, TimestampUtc = at, Source = source };
        foreach (var c in corrections)
        {
            c.RestoreEventId = id;
            restore.Corrections.Add(c);
        }
        return restore;
    }

    private static RestoreEventCorrection Correction(int id, RestoreOutcome? to, DateTime at) =>
        new() { Id = id, NewOutcome = to, CorrectedAtUtc = at, Reason = "test" };

    [Fact]
    public void SuccessCorrectedToFailed_IsNotRestoreTested()
    {
        var restore = Restore(1, RestoreOutcome.Success, Now.AddDays(-3), corrections: Correction(1, RestoreOutcome.Failed, Now.AddDays(-1)));

        Assert.False(RestoreEvidence.CountsAsVerified(restore));
        Assert.False(RestoreEvidence.AnyVerified([restore]));
        Assert.Null(RestoreEvidence.LastVerifiedUtc([restore]));
    }

    [Fact]
    public void FailedCorrectedToSuccess_IsRestoreTested()
    {
        var restore = Restore(1, RestoreOutcome.Failed, Now.AddDays(-3), corrections: Correction(1, RestoreOutcome.Success, Now.AddDays(-1)));

        Assert.True(RestoreEvidence.CountsAsVerified(restore));
        Assert.Equal(Now.AddDays(-3), RestoreEvidence.LastVerifiedUtc([restore]));
    }

    [Fact]
    public void NewestOutcomeCorrectionWins_AndNotesOnlyCorrectionsAreIgnored()
    {
        var restore = Restore(1, RestoreOutcome.Success, Now.AddDays(-5), corrections:
        [
            Correction(1, RestoreOutcome.Failed, Now.AddDays(-4)),
            Correction(2, RestoreOutcome.Success, Now.AddDays(-2)),
            Correction(3, null, Now.AddDays(-1))
        ]);

        Assert.Equal(RestoreOutcome.Success, RestoreEvidence.EffectiveOutcome(restore));
        Assert.True(RestoreEvidence.CountsAsVerified(restore));
    }

    [Fact]
    public void AsOfView_IgnoresCorrectionsMadeLater()
    {
        var restore = Restore(1, RestoreOutcome.Success, Now.AddDays(-10), corrections: Correction(1, RestoreOutcome.Failed, Now.AddDays(-1)));

        Assert.Equal(Now.AddDays(-10), RestoreEvidence.LastVerifiedUtc([restore], Now.AddDays(-5)));
        Assert.Null(RestoreEvidence.LastVerifiedUtc([restore], Now));
    }

    [Fact]
    public void ImportedRestore_NeverCounts_EvenWhenCorrectedToSuccess()
    {
        var restore = Restore(1, RestoreOutcome.Failed, Now.AddDays(-3), BackupSource.Imported, Correction(1, RestoreOutcome.Success, Now.AddDays(-1)));

        Assert.False(RestoreEvidence.CountsAsVerified(restore));
        Assert.False(RestoreEvidence.IsVerified.Compile()(restore));
    }

    [Fact]
    public void QueryExpression_AgreesWithInMemoryRule()
    {
        var isVerified = RestoreEvidence.IsVerified.Compile();
        var restores = new[]
        {
            Restore(1, RestoreOutcome.Success, Now),
            Restore(2, RestoreOutcome.Failed, Now),
            Restore(3, RestoreOutcome.Success, Now, corrections: Correction(1, RestoreOutcome.Failed, Now)),
            Restore(4, RestoreOutcome.Failed, Now, corrections: [Correction(2, RestoreOutcome.Success, Now.AddHours(-2)), Correction(3, RestoreOutcome.Failed, Now.AddHours(-1))]),
            Restore(5, RestoreOutcome.Failed, Now, corrections: [Correction(4, RestoreOutcome.Success, Now), Correction(5, null, Now.AddHours(1))]),
            Restore(6, RestoreOutcome.Success, Now, BackupSource.Imported)
        };

        Assert.Equal(restores.Select(RestoreEvidence.CountsAsVerified), restores.Select(isVerified));
    }

    [Fact]
    public void CorrectedRestore_NoLongerUpgradesBackupToRestoreTested()
    {
        var backup = new BackupRecord { Id = 1, Status = BackupStatus.Success, Source = BackupSource.Manual, TimestampUtc = Now.AddDays(-2) };
        var restore = Restore(1, RestoreOutcome.Success, Now.AddDays(-1), corrections: Correction(1, RestoreOutcome.Failed, Now));

        Assert.Equal(EvidenceLevel.ReportedSuccess, BackupEvidenceClassifier.Classify(backup, [restore]));
        Assert.Equal(EvidenceLevel.RestoreTested, BackupEvidenceClassifier.Classify(backup, [Restore(2, RestoreOutcome.Success, Now.AddDays(-1))]));
    }

    [Fact]
    public void RestoreTestingReport_HonoursCorrections()
    {
        var input = new RestoreTestingInput("SQL-01", "Contoso", "Finance", InstanceRole.Active, EnvironmentTier.Production,
            [Restore(1, RestoreOutcome.Success, Now.AddDays(-5), corrections: Correction(1, RestoreOutcome.Failed, Now.AddDays(-1)))]);

        var row = Assert.Single(RestoreTestingReportBuilder.Build([input], 90, Now));

        Assert.False(row.RestoreTested);
        Assert.Null(row.LastSuccessfulRestoreUtc);
    }

    // ---- Support-runway notice for an instance with no version identity ----

    private static readonly int[] Thresholds = [365, 180, 90, 30];

    [Fact]
    public void NoIdentity_FirstSweep_SendsOneUnknownNotice()
    {
        var runway = SupportRunwayCalculator.Evaluate(null, null, [], Now);
        var decision = SupportRunwayNoticeEvaluator.Decide(runway, Thresholds, SupportRunwayNoticeEvaluator.LastStateFor(null, unknownNoticeSentWithoutIdentity: false));

        Assert.True(runway.IsUnknown);
        Assert.True(decision.Notify);
        Assert.Equal(RunwayNoticeKinds.Unknown, decision.NewState.Kind);
        Assert.Contains("lifecycle page", runway.UnknownReason);
    }

    [Fact]
    public void NoIdentity_AfterTheNoticeWasRecorded_SendsNothingMore()
    {
        var runway = SupportRunwayCalculator.Evaluate(null, null, [], Now);
        var decision = SupportRunwayNoticeEvaluator.Decide(runway, Thresholds, SupportRunwayNoticeEvaluator.LastStateFor(null, unknownNoticeSentWithoutIdentity: true));

        Assert.False(decision.Notify);
    }

    [Fact]
    public void NewUnresolvedIdentity_AfterNoIdentityNotice_DoesNotRepeatTheUnknownNotice()
    {
        var identity = new VersionIdentity { ResolutionStatus = VersionResolutionStatus.Unresolved, UnresolvedReason = "Unparsed" };
        var runway = SupportRunwayCalculator.Evaluate(identity, null, [], Now);
        var decision = SupportRunwayNoticeEvaluator.Decide(runway, Thresholds, SupportRunwayNoticeEvaluator.LastStateFor(identity, unknownNoticeSentWithoutIdentity: true));

        Assert.True(runway.IsUnknown);
        Assert.False(decision.Notify);
    }

    [Fact]
    public void IdentityWithItsOwnState_IgnoresTheNoIdentityRecord()
    {
        var identity = new VersionIdentity { LastRunwayNoticeKind = RunwayNoticeKinds.Deadline, LastRunwayNoticeThresholdDays = 90, LastRunwayNoticeMilestoneUtc = Now.AddDays(60) };

        var state = SupportRunwayNoticeEvaluator.LastStateFor(identity, unknownNoticeSentWithoutIdentity: true);

        Assert.Equal(new RunwayNoticeState(90, Now.AddDays(60), RunwayNoticeKinds.Deadline), state);
    }

    // ---- Retirement gate counts hosted databases ----

    private static RetirementChecklistEvaluation EvaluateRetirement(params InstanceDependency[] dependencies) =>
        RetirementChecklistGateEvaluator.Evaluate(new RetirementChecklist(), dependencies, ChainCoverageStatus.Complete, true, true, null, null, Now);

    [Fact]
    public void UnresolvedHostedDatabase_BlocksRetirement_WhicheverDirectionItWasRecorded()
    {
        var gate = Assert.Single(EvaluateRetirement(
                new InstanceDependency { Direction = DependencyDirection.Upstream, Kind = DependencyKind.HostedDatabase, Name = "PayrollDb", IsResolved = false }).Gates,
            g => g.GateKey == "ConsumersResolved");

        Assert.Equal(RetirementGateStatus.Blocked, gate.Status);
        Assert.Contains("hosted database PayrollDb", gate.Detail);
    }

    [Fact]
    public void ResolvedHostedDatabase_CountsAsAResolvedConsumer()
    {
        var gate = Assert.Single(EvaluateRetirement(
                new InstanceDependency { Direction = DependencyDirection.Upstream, Kind = DependencyKind.HostedDatabase, Name = "PayrollDb", IsResolved = true },
                new InstanceDependency { Direction = DependencyDirection.Downstream, Kind = DependencyKind.Consumer, Name = "BillingApp", IsResolved = true }).Gates,
            g => g.GateKey == "ConsumersResolved");

        Assert.Equal(RetirementGateStatus.Passed, gate.Status);
        Assert.Contains("including 1 hosted database(s)", gate.Detail);
    }

    // ---- Backdated entry times ----

    [Fact]
    public void EntryForm_UntouchedDefault_MeansNow()
    {
        var result = EventTimeZones.FromEntryForm(new DateTime(2026, 11, 12, 10, 0, 0), EventTimeZones.Find("America/New_York")!, untouchedDefault: true, Now);

        Assert.True(result.Ok);
        Assert.Equal(Now, result.Utc);
        Assert.Equal("America/New_York", result.ZoneId);
    }

    [Fact]
    public void EntryForm_BackdatedTime_ConvertsFromTheChosenZone()
    {
        var result = EventTimeZones.FromEntryForm(new DateTime(2026, 11, 10, 22, 30, 0), EventTimeZones.Find("America/New_York")!, untouchedDefault: false, Now);

        Assert.True(result.Ok);
        Assert.Equal(new DateTime(2026, 11, 11, 3, 30, 0, DateTimeKind.Utc), result.Utc);
    }

    [Fact]
    public void EntryForm_FutureTime_IsRejected()
    {
        var result = EventTimeZones.FromEntryForm(new DateTime(2026, 11, 12, 16, 0, 0), TimeZoneInfo.Utc, untouchedDefault: false, Now);

        Assert.False(result.Ok);
        Assert.Null(result.Utc);
        Assert.Equal("Event time can't be in the future.", result.Error);
    }

    [Fact]
    public void EntryForm_TimeSkippedByDaylightSaving_IsRejected()
    {
        var result = EventTimeZones.FromEntryForm(new DateTime(2026, 3, 29, 1, 30, 0), EventTimeZones.Find("Europe/London")!, untouchedDefault: false, Now);

        Assert.False(result.Ok);
        Assert.Contains("daylight saving", result.Error);
    }

    // ---- Report-send feedback ----

    [Theory]
    [InlineData(0, false, 0, 0)]
    [InlineData(2, true, 0, 0)]
    [InlineData(2, false, 0, 2)]
    [InlineData(3, false, 1, 2)]
    public void NonAdmin_IsNeverPointedToTheAdminOnlySetupPage(int recipients, bool disabled, int accepted, int failed)
    {
        var message = ReportSendFeedback.Describe(recipients, disabled, accepted, failed, viewerIsAdmin: false, "Nov 12, 2026 10:00 AM (UTC-05:00)");

        Assert.False(message.Succeeded);
        Assert.DoesNotContain("Setup", message.Text);
        Assert.DoesNotContain(":DistributionList", message.Text);
        Assert.DoesNotContain("Email:Provider", message.Text);
        Assert.Contains("administrator", message.Text);
        Assert.DoesNotContain("—", message.Text);
    }

    [Fact]
    public void NonAdmin_FailedSend_NamesTheSendTimeForTheAdministrator()
    {
        var message = ReportSendFeedback.Describe(2, false, 0, 2, viewerIsAdmin: false, "Nov 12, 2026 10:00 AM (UTC-05:00)");

        Assert.Contains("Nov 12, 2026 10:00 AM (UTC-05:00)", message.Text);
        Assert.Contains("failed to send to all 2", message.Text);
    }

    [Fact]
    public void Admin_FailedSend_PointsToDeliveryHistory()
    {
        var message = ReportSendFeedback.Describe(3, false, 1, 2, viewerIsAdmin: true, "any");

        Assert.Contains("Setup and diagnostics, Delivery history", message.Text);
        Assert.Contains("1 accepted, 2 failed", message.Text);
    }

    [Fact]
    public void AllAccepted_Succeeds()
    {
        Assert.Equal(new ReportSendMessage(true, "Report accepted for 1 recipient."), ReportSendFeedback.Describe(1, false, 1, 0, false, ""));
        Assert.True(ReportSendFeedback.Describe(4, false, 4, 0, false, "").Succeeded);
    }

    // ---- Evidence audit snapshots ----

    [Fact]
    public void BackupSnapshot_KeepsTheEnteredValues()
    {
        var record = new BackupRecord
        {
            TimestampUtc = new DateTime(2026, 11, 11, 3, 30, 0), OccurredTimeZone = "America/New_York", Type = BackupType.TransactionLog,
            Status = BackupStatus.Failed, SizeMb = 12.5, Notes = "Disk \"full\"", RecordedByUserName = "priya"
        };

        using var json = JsonDocument.Parse(EvidenceAuditSnapshot.Backup(record));
        var root = json.RootElement;

        Assert.Equal("2026-11-11T03:30:00Z", root.GetProperty("OccurredAtUtc").GetString());
        Assert.Equal("America/New_York", root.GetProperty("OccurredTimeZone").GetString());
        Assert.Equal("TransactionLog", root.GetProperty("Type").GetString());
        Assert.Equal("Failed", root.GetProperty("Status").GetString());
        Assert.Equal("Disk \"full\"", root.GetProperty("Notes").GetString());
        Assert.Equal("priya", root.GetProperty("RecordedByUserName").GetString());
    }

    [Fact]
    public void RestoreSnapshot_KeepsOutcomeLinkAndEvidenceReference()
    {
        var restore = new RestoreEvent
        {
            TimestampUtc = Now, Outcome = RestoreOutcome.Failed, Reason = RestoreReason.Test, LinkedBackupRecordId = 42,
            EvidenceReference = "CHG-1001", PerformedBy = "Priya"
        };

        using var json = JsonDocument.Parse(EvidenceAuditSnapshot.Restore(restore));

        Assert.Equal("Failed", json.RootElement.GetProperty("Outcome").GetString());
        Assert.Equal(42, json.RootElement.GetProperty("LinkedBackupRecordId").GetInt32());
        Assert.Equal("CHG-1001", json.RootElement.GetProperty("EvidenceReference").GetString());
    }

    [Fact]
    public void CorrectionSides_IncludeNotes()
    {
        using var json = JsonDocument.Parse(EvidenceAuditSnapshot.RestoreCorrectionSide(RestoreOutcome.Failed, "Wrong \"file\""));

        Assert.Equal("Failed", json.RootElement.GetProperty("Outcome").GetString());
        Assert.Equal("Wrong \"file\"", json.RootElement.GetProperty("Notes").GetString());
    }

    [Fact]
    public void MaintenanceSnapshot_KeepsRunTimeAndEnteredZone()
    {
        var record = new MaintenanceEvidenceRecord
        {
            CheckType = MaintenanceCheckType.IntegrityCheck, RanAtUtc = new DateTime(2026, 11, 11, 3, 30, 0), Outcome = MaintenanceOutcome.Passed
        };

        using var json = JsonDocument.Parse(EvidenceAuditSnapshot.Maintenance(record, "Europe/London"));

        Assert.Equal("2026-11-11T03:30:00Z", json.RootElement.GetProperty("RanAtUtc").GetString());
        Assert.Equal("Europe/London", json.RootElement.GetProperty("EnteredTimeZone").GetString());
    }
}
