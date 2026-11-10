using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class PathSupportClassifierTests
{
    private static PathSupportRule Rule(
        DatabaseEngine engine = DatabaseEngine.MySql,
        MigrationAssessmentType type = MigrationAssessmentType.VersionUpgrade,
        MigrationMethod method = MigrationMethod.InPlaceUpgrade,
        string sourceFamily = "MySQL", string sourceMin = "", string sourceMax = "",
        string targetFamily = "MySQL", string targetMin = "", string targetMax = "",
        PathSupportStatus status = PathSupportStatus.Supported) => new()
    {
        Engine = engine,
        AssessmentType = type,
        Method = method,
        SourceFamily = sourceFamily,
        SourceBuildMin = sourceMin,
        SourceBuildMax = sourceMax,
        TargetFamily = targetFamily,
        TargetBuildMin = targetMin,
        TargetBuildMax = targetMax,
        Status = status,
        ReviewedBy = "Angela Price",
        ReviewedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void NoRulesAtAll_IsUnknown_NeverSupported()
    {
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.VersionUpgrade, MigrationMethod.InPlaceUpgrade,
            "MySQL", "8.0.36", "", "", "",
            "MySQL", "8.4.0", "", "", "",
            []);

        Assert.Equal(PathSupportStatus.Unknown, result.Status);
        Assert.Null(result.MatchedRule);
        Assert.Contains("never Supported", result.Reasoning);
    }

    [Fact]
    public void ExactMatchingSupportedRule_ReturnsSupported()
    {
        var rule = Rule(sourceMin: "8.0.0", sourceMax: "8.0.40", targetMin: "8.4.0", targetMax: "8.4.99");
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.VersionUpgrade, MigrationMethod.InPlaceUpgrade,
            "MySQL", "8.0.36", "", "", "",
            "MySQL", "8.4.0", "", "", "",
            [rule]);

        Assert.Equal(PathSupportStatus.Supported, result.Status);
        Assert.Same(rule, result.MatchedRule);
    }

    [Fact]
    public void MatchingBlockedRule_ReturnsBlocked()
    {
        var rule = Rule(status: PathSupportStatus.Blocked);
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.VersionUpgrade, MigrationMethod.InPlaceUpgrade,
            "MySQL", "8.0.36", "", "", "",
            "MySQL", "8.4.0", "", "", "",
            [rule]);

        Assert.Equal(PathSupportStatus.Blocked, result.Status);
    }

    [Fact]
    public void SourceBuildOutsideRuleRange_DoesNotMatch_IsUnknown()
    {
        var rule = Rule(sourceMin: "8.0.0", sourceMax: "8.0.10");
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.VersionUpgrade, MigrationMethod.InPlaceUpgrade,
            "MySQL", "8.0.36", "", "", "",
            "MySQL", "8.4.0", "", "", "",
            [rule]);

        Assert.Equal(PathSupportStatus.Unknown, result.Status);
    }

    [Fact]
    public void DifferentEngine_NeverMatches()
    {
        var rule = Rule(engine: DatabaseEngine.PostgreSql, sourceFamily: "PostgreSQL", targetFamily: "PostgreSQL");
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.VersionUpgrade, MigrationMethod.InPlaceUpgrade,
            "MySQL", "8.0.36", "", "", "",
            "MySQL", "8.4.0", "", "", "",
            [rule]);

        Assert.Equal(PathSupportStatus.Unknown, result.Status);
    }

    [Fact]
    public void RelocationAndVersionUpgrade_AreDistinctAssessmentTypes()
    {
        // A rule reviewed only for a VersionUpgrade must not answer a Relocation question for the same products.
        var upgradeRule = Rule(type: MigrationAssessmentType.VersionUpgrade);
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.Relocation, MigrationMethod.InPlaceUpgrade,
            "MySQL", "8.0.36", "", "", "",
            "MySQL", "8.0.36", "", "", "",
            [upgradeRule]);

        Assert.Equal(PathSupportStatus.Unknown, result.Status);
    }

    [Fact]
    public void CrossEngineConversion_NeedsItsOwnRule_NotTheUpgradeRule()
    {
        var upgradeRule = Rule(type: MigrationAssessmentType.VersionUpgrade, targetFamily: "MySQL");
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.CrossEngineConversion, MigrationMethod.DumpLoad,
            "MySQL", "8.0.36", "", "", "",
            "PostgreSQL", "16.2", "", "", "",
            [upgradeRule]);

        Assert.Equal(PathSupportStatus.Unknown, result.Status);
    }

    [Fact]
    public void DifferentMethod_DoesNotMatch()
    {
        var rule = Rule(method: MigrationMethod.InPlaceUpgrade);
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.VersionUpgrade, MigrationMethod.SideBySide,
            "MySQL", "8.0.36", "", "", "",
            "MySQL", "8.4.0", "", "", "",
            [rule]);

        Assert.Equal(PathSupportStatus.Unknown, result.Status);
    }

    [Fact]
    public void ConflictingRules_SameKeyDifferentStatus_ReturnsUnknown()
    {
        var supported = Rule(status: PathSupportStatus.Supported);
        var blocked = Rule(status: PathSupportStatus.Blocked);
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.VersionUpgrade, MigrationMethod.InPlaceUpgrade,
            "MySQL", "8.0.36", "", "", "",
            "MySQL", "8.4.0", "", "", "",
            [supported, blocked]);

        Assert.Equal(PathSupportStatus.Unknown, result.Status);
        Assert.Contains("conflicting", result.Reasoning);
    }

    [Fact]
    public void EditionConstraint_MustMatchWhenSpecified()
    {
        var rule = Rule();
        rule.SourceEdition = "Enterprise";
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.VersionUpgrade, MigrationMethod.InPlaceUpgrade,
            "MySQL", "8.0.36", "Community", "", "",
            "MySQL", "8.4.0", "", "", "",
            [rule]);

        Assert.Equal(PathSupportStatus.Unknown, result.Status);
    }

    [Fact]
    public void BlankSourceBuild_DoesNotExcludeRuleMatch()
    {
        // No build precision supplied by the caller — the build-range filter is skipped for that
        // dimension rather than forcing a false "out of range" result.
        var rule = Rule(sourceMin: "8.0.0", sourceMax: "8.0.40");
        var result = PathSupportClassifier.Classify(
            DatabaseEngine.MySql, MigrationAssessmentType.VersionUpgrade, MigrationMethod.InPlaceUpgrade,
            "MySQL", "", "", "", "",
            "MySQL", "8.4.0", "", "", "",
            [rule]);

        Assert.Equal(PathSupportStatus.Supported, result.Status);
    }
}
