using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record TrayAtRiskInstance(int Id, string Name, string Health, string Environment, DateTime? LastSuccessUtc);

public record TraySummary(
    DateTime GeneratedAtUtc,
    string User,
    int Failed,
    int Overdue,
    int NeverBackedUp,
    IReadOnlyList<TrayAtRiskInstance> AtRisk,
    string DashboardUrl);

/// <summary>The read-only counts the tray app shows, ordered the way the dashboard ranks risk.</summary>
public static class TraySummaryBuilder
{
    public const int MaxAtRisk = 20;

    private static readonly Dictionary<BackupHealth, int> RiskRank = new()
    {
        [BackupHealth.Failed] = 0,
        [BackupHealth.NeverBackedUp] = 1,
        [BackupHealth.Overdue] = 2,
    };

    public static TraySummary Build(IEnumerable<InstanceListRow> rows, string user, DateTime nowUtc)
    {
        var atRisk = rows.Where(r => r.Health != BackupHealth.OnTrack).ToList();

        var top = atRisk
            .OrderBy(r => RiskRank[r.Health])
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxAtRisk)
            .Select(r => new TrayAtRiskInstance(
                r.Id,
                r.Name,
                r.Health.ToString(),
                r.Environment.ToString(),
                r.LastBackupUtc is { } last ? DateTime.SpecifyKind(last, DateTimeKind.Utc) : null))
            .ToList();

        return new TraySummary(
            DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc),
            user,
            atRisk.Count(r => r.Health == BackupHealth.Failed),
            atRisk.Count(r => r.Health == BackupHealth.Overdue),
            atRisk.Count(r => r.Health == BackupHealth.NeverBackedUp),
            top,
            "/");
    }
}
