using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>Pairs one advisory with one instance. The classifier only suggests Potential/NotApplicable/Unknown; Confirmed needs a reviewer, and IsReviewed stops re-evaluation overwriting a human classification.</summary>
public class AdvisoryInstanceAssessment
{
    public int Id { get; set; }

    public int AdvisoryRecordId { get; set; }
    public AdvisoryRecord? AdvisoryRecord { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public AdvisoryClassification Classification { get; set; } = AdvisoryClassification.Unknown;
    public string ClassificationReasoning { get; set; } = string.Empty;

    public bool DistributionProvidesBackport { get; set; }
    public string ManagedServiceResponsibility { get; set; } = string.Empty;

    public bool IsReviewed { get; set; }
    public string ReviewerEvidence { get; set; } = string.Empty;
    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime? ReviewedAtUtc { get; set; }

    public string Owner { get; set; } = string.Empty;
    public DateTime? RemediationDeadlineUtc { get; set; }
    public string TargetPatch { get; set; } = string.Empty;
    public AdvisoryVerificationStatus VerificationStatus { get; set; } = AdvisoryVerificationStatus.Unverified;
    public string VerificationNotes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
