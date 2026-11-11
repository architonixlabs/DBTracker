using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Decides whether current health warrants a new alert. Alerts fire on a change into an alertable state, so a still-failed instance doesn't re-notify every sweep.</summary>
public static class AlertEvaluator
{
    private static bool IsAlertable(BackupHealth health) => health is BackupHealth.Overdue or BackupHealth.Failed or BackupHealth.NeverBackedUp;

    /// <summary><paramref name="suppressNeverBackedUp"/> is true during the onboarding grace period. It only affects NeverBackedUp; Overdue/Failed always notify on a state change.</summary>
    public static bool ShouldNotify(BackupHealth? lastNotifiedHealth, BackupHealth currentHealth, bool suppressNeverBackedUp = false)
    {
        if (currentHealth == BackupHealth.NeverBackedUp && suppressNeverBackedUp)
        {
            return false;
        }

        return IsAlertable(currentHealth) && lastNotifiedHealth != currentHealth;
    }

    /// <summary>What to store as "last notified" after this check — null once health recovers, so a future relapse re-alerts.</summary>
    public static BackupHealth? NextNotifiedState(BackupHealth currentHealth) =>
        IsAlertable(currentHealth) ? currentHealth : null;

    /// <summary>True while a maintenance window is active. Callers must also skip updating LastNotifiedHealth, so a still-bad health re-alerts when the window ends.</summary>
    public static bool IsSuppressedByMaintenance(DateTime? maintenanceUntilUtc, DateTime nowUtc) =>
        maintenanceUntilUtc is not null && nowUtc < maintenanceUntilUtc.Value;

    /// <summary>True while a new instance is inside its onboarding grace window, so it isn't flagged NeverBackedUp before its first scheduled backup. graceHours &lt;= 0 disables the grace period.</summary>
    public static bool IsWithinOnboardingGrace(DateTime createdAtUtc, DateTime nowUtc, int graceHours) =>
        graceHours > 0 && nowUtc < createdAtUtc.AddHours(graceHours);
}
