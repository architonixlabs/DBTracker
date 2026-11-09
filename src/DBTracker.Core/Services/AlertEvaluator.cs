using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// Decides whether an instance's current health warrants a fresh alert. Alerts fire on a state
/// change into Overdue/Failed, not on every periodic health check — otherwise a still-failed
/// instance would re-notify every sweep forever.
/// </summary>
public static class AlertEvaluator
{
    private static bool IsAlertable(BackupHealth health) => health is BackupHealth.Overdue or BackupHealth.Failed;

    public static bool ShouldNotify(BackupHealth? lastNotifiedHealth, BackupHealth currentHealth) =>
        IsAlertable(currentHealth) && lastNotifiedHealth != currentHealth;

    /// <summary>What to store as "last notified" after this check — null once health recovers, so a future relapse re-alerts.</summary>
    public static BackupHealth? NextNotifiedState(BackupHealth currentHealth) =>
        IsAlertable(currentHealth) ? currentHealth : null;
}
