using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One readiness check. It stays Unknown until a reviewer records a result, and returns to Unknown when invalidated by a target change or expired evidence; the original result is kept.</summary>
public class ReadinessCheckItem
{
    public int Id { get; set; }

    public int ReadinessChecklistId { get; set; }
    public ReadinessChecklist? ReadinessChecklist { get; set; }

    public ReadinessCheckArea Area { get; set; }
    public string Label { get; set; } = string.Empty;

    public ReadinessCheckStatus Status { get; set; } = ReadinessCheckStatus.Unknown;
    public string Findings { get; set; } = string.Empty;

    public string Reviewer { get; set; } = string.Empty;
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>Reference to an attached/linked engine-native assessment output or other evidence for the source side.</summary>
    public string SourceReference { get; set; } = string.Empty;
    public string TargetReference { get; set; } = string.Empty;

    public string RemediationOwner { get; set; } = string.Empty;
    public string RemediationNotes { get; set; } = string.Empty;

    public DateTime? EvidenceExpiresAtUtc { get; set; }

    /// <summary>Set explicitly when the checklist's target changed after this item was recorded — see ReadinessChecklist/ReadinessChecklistService.</summary>
    public bool IsInvalidated { get; set; }
    public string InvalidationReason { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
