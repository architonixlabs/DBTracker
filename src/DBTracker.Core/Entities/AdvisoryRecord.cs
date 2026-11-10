using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// One imported/referenced security advisory (D05) — vendor ID, affected build range, fixed build
/// and provenance. This app never scans for or discovers advisories itself; it only records
/// imported/reviewed results, deliberately not duplicating a dedicated vulnerability-management
/// platform's own UI (DefectDojo/Dependency-Track per org standards already cover that).
/// </summary>
public class AdvisoryRecord
{
    public int Id { get; set; }

    public DatabaseEngine Engine { get; set; }
    public string Family { get; set; } = string.Empty;

    public string VendorAdvisoryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AdvisorySeverity Severity { get; set; } = AdvisorySeverity.Unknown;

    /// <summary>Inclusive affected build range, compared with VersionBuildComparer. Empty min = "any version up to max".</summary>
    public string AffectedMinBuild { get; set; } = string.Empty;
    public string AffectedMaxBuild { get; set; } = string.Empty;
    public string AffectedConfiguration { get; set; } = string.Empty;

    public string FixedBuild { get; set; } = string.Empty;
    public string ProvenanceUrl { get; set; } = string.Empty;
    public DateTime? PublishedAtUtc { get; set; }

    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
    public string ImportedBy { get; set; } = string.Empty;

    public ICollection<AdvisoryInstanceAssessment> Assessments { get; set; } = new List<AdvisoryInstanceAssessment>();
}
