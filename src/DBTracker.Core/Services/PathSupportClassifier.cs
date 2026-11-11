using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record PathSupportClassificationResult(PathSupportStatus Status, PathSupportRule? MatchedRule, string Reasoning);

/// <summary>Classifies a migration pairing as Supported, Blocked or Unknown against curated rules. No matching rule is always Unknown, and rules that disagree also give Unknown.</summary>
/// <remarks>Engine, AssessmentType and Method must match exactly; other rule fields are optional constraints, and builds compare numerically ("8.0.36" falls inside "8.0.0"-"8.0.40").</remarks>
public static class PathSupportClassifier
{
    public static PathSupportClassificationResult Classify(
        DatabaseEngine engine,
        MigrationAssessmentType assessmentType,
        MigrationMethod method,
        string sourceFamily, string sourceBuild, string sourceEdition, string sourcePlatform, string sourceTopology,
        string targetFamily, string targetBuild, string targetEdition, string targetPlatform, string targetTopology,
        IReadOnlyList<PathSupportRule> rules)
    {
        var matches = rules.Where(r =>
                r.Engine == engine &&
                r.AssessmentType == assessmentType &&
                r.Method == method &&
                FieldMatches(r.SourceFamily, sourceFamily) &&
                FieldMatches(r.TargetFamily, targetFamily) &&
                FieldMatches(r.SourceEdition, sourceEdition) &&
                FieldMatches(r.SourcePlatform, sourcePlatform) &&
                FieldMatches(r.SourceTopology, sourceTopology) &&
                FieldMatches(r.TargetEdition, targetEdition) &&
                FieldMatches(r.TargetPlatform, targetPlatform) &&
                FieldMatches(r.TargetTopology, targetTopology) &&
                BuildInRange(sourceBuild, r.SourceBuildMin, r.SourceBuildMax) &&
                BuildInRange(targetBuild, r.TargetBuildMin, r.TargetBuildMax))
            .ToList();

        if (matches.Count == 0)
        {
            return new PathSupportClassificationResult(PathSupportStatus.Unknown, null,
                "No reviewed rule covers this source-to-target pairing for this engine, method and assessment type. The absence of a rule is Unknown, never Supported.");
        }

        var distinctStatuses = matches.Select(m => m.Status).Distinct().ToList();
        if (distinctStatuses.Count > 1)
        {
            return new PathSupportClassificationResult(PathSupportStatus.Unknown, null,
                $"{matches.Count} reviewed rules match this pairing with conflicting outcomes ({string.Join(", ", distinctStatuses)}). This needs review rather than an automatic pick.");
        }

        var best = matches.OrderByDescending(Specificity).First();
        return new PathSupportClassificationResult(best.Status, best,
            $"Matched a reviewed rule (reviewed by {(string.IsNullOrWhiteSpace(best.ReviewedBy) ? "unknown reviewer" : best.ReviewedBy)} on {best.ReviewedAtUtc:yyyy-MM-dd}).");
    }

    private static bool FieldMatches(string ruleValue, string actual) =>
        string.IsNullOrWhiteSpace(ruleValue) || string.Equals(ruleValue.Trim(), (actual ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool BuildInRange(string actual, string min, string max) =>
        string.IsNullOrWhiteSpace(actual) || VersionBuildComparer.IsInRange(actual, min, max);

    /// <summary>Combines what the assessor entered with the matched rule's reviewed text. Neither is dropped: when both are
    /// present and differ, the rule's text follows in brackets.</summary>
    public static string MergeGuidance(string? entered, string? fromRule)
    {
        var e = (entered ?? "").Trim();
        var r = (fromRule ?? "").Trim();
        if (e.Length == 0)
        {
            return r;
        }
        if (r.Length == 0 || string.Equals(e, r, StringComparison.OrdinalIgnoreCase))
        {
            return e;
        }
        return $"{e} (matched rule: {r})";
    }

    private static int Specificity(PathSupportRule r) =>
        new[] { r.SourceEdition, r.SourcePlatform, r.SourceTopology, r.TargetEdition, r.TargetPlatform, r.TargetTopology, r.SourceBuildMin + r.SourceBuildMax, r.TargetBuildMin + r.TargetBuildMax }
            .Count(s => !string.IsNullOrWhiteSpace(s));
}
