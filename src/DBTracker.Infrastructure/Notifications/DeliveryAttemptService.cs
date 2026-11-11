using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Stores and lists send attempts for the /setup delivery history. Uses its own short-lived DbContext so recording never races a caller mid-save.</summary>
public class DeliveryAttemptService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public const int DefaultHistorySize = 50;
    public const string EmailChannel = "Email";
    public const string WebhookChannel = "Webhook";

    public async Task<DeliveryAttempt> RecordAsync(
        DeliveryKind kind,
        string scopeDescription,
        IReadOnlyCollection<EmailSendResult> results,
        string triggeredBy,
        int heldForQuietHours = 0,
        DateTime? attemptedAtUtc = null,
        CancellationToken ct = default,
        string channel = EmailChannel)
    {
        var summary = DeliveryOutcomeSummarizer.Summarize(results.Select(r => new DeliveryRecipientResult(r.Outcome, r.Detail)).ToList());

        var attempt = new DeliveryAttempt
        {
            AttemptedAtUtc = attemptedAtUtc ?? DateTime.UtcNow,
            Kind = kind,
            Channel = channel,
            ScopeDescription = Truncate(scopeDescription, 500),
            RecipientCount = summary.RecipientCount,
            AcceptedCount = summary.AcceptedCount,
            FailedCount = summary.FailedCount,
            HeldForQuietHoursCount = heldForQuietHours,
            Outcome = summary.Outcome,
            Error = summary.Error,
            TriggeredBy = Truncate(string.IsNullOrWhiteSpace(triggeredBy) ? "unknown" : triggeredBy, 320)
        };

        using var db = await factory.CreateDbContextAsync(ct);
        db.DeliveryAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);
        return attempt;
    }

    public async Task<List<DeliveryAttempt>> ListRecentAsync(int take = DefaultHistorySize, CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);
        return await db.DeliveryAttempts.AsNoTracking()
            .OrderByDescending(a => a.AttemptedAtUtc).ThenByDescending(a => a.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
