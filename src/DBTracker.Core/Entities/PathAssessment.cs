using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// A recorded evaluation of one source-to-target pairing for one instance (D08), produced by
/// PathSupportClassifier against the curated PathSupportRule set. Relocation (same version, new
/// host/platform) and cross-engine conversion are recorded with their own AssessmentType rather than
/// being folded into a version upgrade. This never generates an executable migration script and never
/// assumes zero downtime — Method only records which supported technique was chosen/evaluated.
/// </summary>
public class PathAssessment
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public MigrationAssessmentType AssessmentType { get; set; }
    public MigrationMethod Method { get; set; }

    public string SourceProduct { get; set; } = string.Empty;
    public string SourceBuild { get; set; } = string.Empty;
    public string SourceEdition { get; set; } = string.Empty;
    public string SourcePlatform { get; set; } = string.Empty;
    public string SourceTopology { get; set; } = string.Empty;

    public string TargetProduct { get; set; } = string.Empty;
    public string TargetBuild { get; set; } = string.Empty;
    public string TargetEdition { get; set; } = string.Empty;
    public string TargetPlatform { get; set; } = string.Empty;
    public string TargetTopology { get; set; } = string.Empty;

    public int? MatchedRuleId { get; set; }
    public PathSupportRule? MatchedRule { get; set; }

    public PathSupportStatus Status { get; set; } = PathSupportStatus.Unknown;
    public string StatusReasoning { get; set; } = string.Empty;

    /// <summary>Reviewed vendor guidance link/notes attached to this specific assessment (in addition to whatever the matched rule itself carries).</summary>
    public string VendorGuidanceUrl { get; set; } = string.Empty;
    public string VendorGuidanceNotes { get; set; } = string.Empty;
    public string RequiredIntermediateVersions { get; set; } = string.Empty;
    public string DowngradeConstraints { get; set; } = string.Empty;

    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
    public string EvaluatedBy { get; set; } = string.Empty;
}
