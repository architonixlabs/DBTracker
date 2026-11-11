using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Rules for readiness exceptions. Expiry is computed from ExpiresAtUtc, so IsActive re-blocks the moment an exception lapses without waiting for a sweep; CanApprove enforces separation of duties when required.</summary>
public static class ReadinessExceptionEvaluator
{
    public static ReadinessExceptionEffectiveStatus EffectiveStatus(ReadinessException exception, DateTime nowUtc)
    {
        if (exception.Status == ReadinessExceptionStatus.Revoked)
        {
            return ReadinessExceptionEffectiveStatus.Revoked;
        }
        if (exception.Status == ReadinessExceptionStatus.Rejected)
        {
            return ReadinessExceptionEffectiveStatus.Rejected;
        }
        if (exception.Status == ReadinessExceptionStatus.PendingApproval)
        {
            return ReadinessExceptionEffectiveStatus.PendingApproval;
        }

        return nowUtc >= exception.ExpiresAtUtc ? ReadinessExceptionEffectiveStatus.Expired : ReadinessExceptionEffectiveStatus.Active;
    }

    public static bool IsActive(ReadinessException exception, DateTime nowUtc) =>
        EffectiveStatus(exception, nowUtc) == ReadinessExceptionEffectiveStatus.Active;

    /// <summary>True when any exception in the set is currently Active for the target. Every gate bypass check goes through here.</summary>
    public static bool HasActiveExceptionFor(IEnumerable<ReadinessException> exceptions, string targetType, int targetId, DateTime nowUtc) =>
        exceptions.Any(e =>
            string.Equals(e.TargetType, targetType, StringComparison.OrdinalIgnoreCase) &&
            e.TargetId == targetId &&
            IsActive(e, nowUtc));

    public static bool CanApprove(ReadinessException exception, string approverUserId) =>
        !exception.RequiresSeparateApprover ||
        !string.Equals(exception.PreparedByUserId, approverUserId, StringComparison.OrdinalIgnoreCase);

    /// <summary>The longest single exception or renewal allowed, so a waiver can't quietly become permanent.</summary>
    public const int MaxDurationDays = 365;

    /// <summary>Checks an expiry chosen for a new exception or a renewal: it must be in the future and within MaxDurationDays.</summary>
    public static string? ValidateExpiry(DateTime expiresAtUtc, DateTime nowUtc)
    {
        if (expiresAtUtc <= nowUtc)
        {
            return "Choose an expiry date in the future.";
        }
        if (expiresAtUtc > nowUtc.AddDays(MaxDurationDays))
        {
            return $"An exception can last at most {MaxDurationDays} days. Renew it again later if it is still needed.";
        }
        return null;
    }

    /// <summary>The newest record in a renewal chain, i.e. the one no other exception renews.</summary>
    public static bool IsLatestInChain(ReadinessException exception, IEnumerable<ReadinessException> all) =>
        !all.Any(e => e.RenewalOfExceptionId == exception.Id);
}

/// <summary>The kinds of record an exception can waive a gate on. Each name matches what the gates look up.</summary>
public static class ReadinessExceptionTargets
{
    public const string DatabaseInstance = "DatabaseInstance";
    public const string MigrationCampaign = "MigrationCampaign";
    public const string ChangeWindow = "ChangeWindow";
    public const string RetirementChecklist = "RetirementChecklist";

    public static IReadOnlyList<string> All { get; } = [DatabaseInstance, MigrationCampaign, ChangeWindow, RetirementChecklist];

    public static string Describe(string targetType) => targetType switch
    {
        DatabaseInstance => "Instance",
        MigrationCampaign => "Migration campaign",
        ChangeWindow => "Change window",
        RetirementChecklist => "Retirement checklist",
        _ => targetType
    };
}
