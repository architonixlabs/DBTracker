using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Text of an alert or recovery message. Names the evidence behind the last success, so a discovered file isn't read as a confirmed backup.</summary>
public static class AlertMessageBuilder
{
    public static string DescribeHealth(BackupHealth health) => health switch
    {
        BackupHealth.Overdue => "overdue for backup",
        BackupHealth.Failed => "failing backups",
        BackupHealth.NeverBackedUp => "never backed up",
        BackupHealth.OnTrack => "on track",
        _ => health.ToString()
    };

    public static string BuildAlert(string instanceName, BackupHealth health, DateTime? lastSuccessUtc, EvidenceLevel? lastSuccessEvidence, int? escalationLevel = null)
    {
        var headline = escalationLevel is { } level
            ? $"{instanceName} is still {DescribeHealth(health)} (escalation #{level}, unacknowledged)."
            : $"{instanceName} is now {DescribeHealth(health)}.";

        return headline + " " + DescribeLastSuccess(lastSuccessUtc, lastSuccessEvidence);
    }

    public static string BuildRecovery(string instanceName, DateTime? lastSuccessUtc, EvidenceLevel? lastSuccessEvidence) =>
        $"{instanceName} has recovered: backup health is on track again. " + DescribeLastSuccess(lastSuccessUtc, lastSuccessEvidence);

    public static string DescribeLastSuccess(DateTime? lastSuccessUtc, EvidenceLevel? evidence) =>
        lastSuccessUtc is { } at
            ? $"Last successful backup {at:yyyy-MM-dd HH:mm} UTC, evidence: {(evidence is { } e ? BackupEvidenceClassifier.Describe(e) : "unknown")}."
            : "No successful backup on record.";
}
