using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Pure classification result for one source-to-target pairing (D08).</summary>
public record PathSupportClassificationResult(PathSupportStatus Status, PathSupportRule? MatchedRule, string Reasoning);

/// <summary>
/// Classifies a source-to-target migration pairing as Supported/Blocked/Unknown by matching it
/// against a curated set of reviewed PathSupportRule rows. This is the one place D08's central rule
/// lives: the absence of a matching rule is always Unknown, never Supported — there is no fallback
/// that treats "nobody has said no" as "yes". Engine, AssessmentType and Method must all match
/// exactly (a Relocation rule never answers a VersionUpgrade question, and vice versa); family,
/// edition, platform and topology fields on a rule are optional constraints (blank = "no constraint
/// on this dimension"), and build ranges are compared with VersionBuildComparer so "8.0.36" correctly
/// falls inside "8.0.0"-"8.0.40" instead of ordering lexicographically. Multiple matching rules that
/// disagree on status are reported as Unknown rather than picking one arbitrarily.
/// </summary>
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
                $"{matches.Count} reviewed rules match this pairing with conflicting outcomes ({string.Join(", ", distinctStatuses)}) — needs review rather than an automatic pick.");
        }

        var best = matches.OrderByDescending(Specificity).First();
        return new PathSupportClassificationResult(best.Status, best,
            $"Matched a reviewed rule (reviewed by {(string.IsNullOrWhiteSpace(best.ReviewedBy) ? "unknown reviewer" : best.ReviewedBy)} on {best.ReviewedAtUtc:yyyy-MM-dd}).");
    }

    private static bool FieldMatches(string ruleValue, string actual) =>
        string.IsNullOrWhiteSpace(ruleValue) || string.Equals(ruleValue.Trim(), (actual ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool BuildInRange(string actual, string min, string max) =>
        string.IsNullOrWhiteSpace(actual) || VersionBuildComparer.IsInRange(actual, min, max);

    private static int Specificity(PathSupportRule r) =>
        new[] { r.SourceEdition, r.SourcePlatform, r.SourceTopology, r.TargetEdition, r.TargetPlatform, r.TargetTopology, r.SourceBuildMin + r.SourceBuildMax, r.TargetBuildMin + r.TargetBuildMax }
            .Count(s => !string.IsNullOrWhiteSpace(s));
}
