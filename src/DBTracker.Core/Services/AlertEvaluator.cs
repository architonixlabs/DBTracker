using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// Decides whether an instance's current health warrants a fresh alert. Alerts fire on a state
/// change into an alertable health, not on every periodic health check — otherwise a still-failed
/// instance would re-notify every sweep forever.
/// </summary>
public static class AlertEvaluator
{
    // NeverBackedUp became alertable under U08 (a grace period keeps a freshly-onboarded instance
    // from paging anyone the moment it's created) — see ShouldNotify's suppressNeverBackedUp param
    // and IsWithinOnboardingGrace.
    private static bool IsAlertable(BackupHealth health) => health is BackupHealth.Overdue or BackupHealth.Failed or BackupHealth.NeverBackedUp;

    /// <summary>
    /// <paramref name="suppressNeverBackedUp"/> should be true while the instance is still within
    /// its onboarding grace period (see <see cref="IsWithinOnboardingGrace"/>) — it only affects the
    /// NeverBackedUp case; Overdue/Failed always notify on a state change regardless.
    /// </summary>
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

    /// <summary>
    /// True while a planned maintenance window is active — alerts (email + webhook) are suppressed
    /// for the duration, but the caller must also skip updating LastNotifiedHealth while suppressed,
    /// so a still-bad health correctly re-alerts once the window ends rather than being remembered
    /// as already-notified.
    /// </summary>
    public static bool IsSuppressedByMaintenance(DateTime? maintenanceUntilUtc, DateTime nowUtc) =>
        maintenanceUntilUtc is not null && nowUtc < maintenanceUntilUtc.Value;

    /// <summary>
    /// True while a newly-created instance is still inside its onboarding grace window (U08) —
    /// a database added five minutes ago that hasn't been backed up yet isn't a NeverBackedUp
    /// alert, it just hasn't had its first backup scheduled yet. graceHours &lt;= 0 disables the
    /// grace period entirely (every instance is immediately alertable).
    /// </summary>
    public static bool IsWithinOnboardingGrace(DateTime createdAtUtc, DateTime nowUtc, int graceHours) =>
        graceHours > 0 && nowUtc < createdAtUtc.AddHours(graceHours);
}
