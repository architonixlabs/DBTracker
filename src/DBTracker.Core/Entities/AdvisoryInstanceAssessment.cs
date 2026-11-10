using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// The applicability pairing between one advisory and one instance (D05). AdvisoryApplicabilityClassifier
/// supplies an initial Potential/NotApplicable/Unknown suggestion from version-range matching alone,
/// but it can never set Confirmed — only a reviewer, with evidence, can. IsReviewed guards automatic
/// re-evaluation (EvaluateAdvisoriesForInstanceAsync) from overwriting a human's own classification.
/// </summary>
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
