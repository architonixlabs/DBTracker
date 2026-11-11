using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A curated Supported or Blocked rule for a source-to-target pairing. No match means Unknown; empty ranges mean no constraint.</summary>
public class PathSupportRule
{
    public int Id { get; set; }

    public DatabaseEngine Engine { get; set; }
    public MigrationAssessmentType AssessmentType { get; set; }
    public MigrationMethod Method { get; set; }

    public string SourceFamily { get; set; } = string.Empty;
    public string SourceBuildMin { get; set; } = string.Empty;
    public string SourceBuildMax { get; set; } = string.Empty;
    public string SourceEdition { get; set; } = string.Empty;
    public string SourcePlatform { get; set; } = string.Empty;
    public string SourceTopology { get; set; } = string.Empty;

    public string TargetFamily { get; set; } = string.Empty;
    public string TargetBuildMin { get; set; } = string.Empty;
    public string TargetBuildMax { get; set; } = string.Empty;
    public string TargetEdition { get; set; } = string.Empty;
    public string TargetPlatform { get; set; } = string.Empty;
    public string TargetTopology { get; set; } = string.Empty;

    public PathSupportStatus Status { get; set; } = PathSupportStatus.Blocked;

    public string RequiredIntermediateVersions { get; set; } = string.Empty;
    public string DowngradeConstraints { get; set; } = string.Empty;

    public string VendorGuidanceUrl { get; set; } = string.Empty;
    public string VendorGuidanceNotes { get; set; } = string.Empty;

    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime ReviewedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
