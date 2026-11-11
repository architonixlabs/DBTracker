using DBTracker.Infrastructure.Notifications;
using DBTracker.Infrastructure.Scanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DBTracker.Infrastructure.Diagnostics;

public enum DiagnosticSeverity { Ok, Warning, Problem }
public record DiagnosticItem(string Area, string Label, DiagnosticSeverity Severity, string Detail, string? NextActionUrl = null, string? NextActionLabel = null);
public record PathCheck(string InstanceName, string Path, bool Reachable, string? Error);

/// <summary>Admin setup and diagnostics checks: import/create readiness, file-path reachability, scan/alert/report enablement, the distribution list, and worker heartbeats. Credentials are masked.</summary>
public class SetupDiagnosticsService(
    IDbContextFactory<DBTrackerDbContext> factory,
    IConfiguration configuration,
    WorkerHeartbeatStore heartbeats,
    IEmailSender emailSender)
{
    public async Task<List<DiagnosticItem>> BuildChecklistAsync()
    {
        var items = new List<DiagnosticItem>();

        using var db = await factory.CreateDbContextAsync();
        var instanceCount = await db.DatabaseInstances.CountAsync(i => !i.IsArchived);

        items.Add(instanceCount == 0
            ? new DiagnosticItem("Inventory", "Instances on file", DiagnosticSeverity.Warning, "No active instances yet.", "/instances/new", "Add an instance")
            : new DiagnosticItem("Inventory", "Instances on file", DiagnosticSeverity.Ok, $"{instanceCount} active instance(s) tracked."));

        var provider = configuration["Email:Provider"];
        if (string.IsNullOrWhiteSpace(provider) || provider == "None")
        {
            items.Add(new DiagnosticItem("Email", "Provider", DiagnosticSeverity.Warning,
                "Email:Provider is unset (None), so alerts and reports are logged, not sent.", "/setup", "Configure Email:Provider in appsettings"));
        }
        else if (provider == "Smtp")
        {
            var host = configuration["Email:Smtp:Host"];
            items.Add(string.IsNullOrWhiteSpace(host)
                ? new DiagnosticItem("Email", "Provider", DiagnosticSeverity.Problem, "Email:Provider is Smtp but Email:Smtp:Host is empty.")
                : new DiagnosticItem("Email", "Provider", DiagnosticSeverity.Ok, $"SMTP via {host}:{configuration.GetValue("Email:Smtp:Port", 587)} (password: {Mask(configuration["Email:Smtp:Password"])})."));
        }
        else if (provider == "ArxMail")
        {
            var endpoint = configuration["Email:ArxMail:Endpoint"];
            items.Add(new DiagnosticItem("Email", "Provider", DiagnosticSeverity.Ok, $"ArxMail gateway at {endpoint} (API key: {Mask(configuration["Email:ArxMail:ApiKey"])})."));
        }

        var scanMinutes = configuration.GetValue("BackupScan:IntervalMinutes", 15);
        items.Add(scanMinutes > 0
            ? new DiagnosticItem("Scanning", "Background scan", DiagnosticSeverity.Ok, $"Every {scanMinutes} minute(s).")
            : new DiagnosticItem("Scanning", "Background scan", DiagnosticSeverity.Warning, "Disabled (BackupScan:IntervalMinutes <= 0). Manual \"Scan now\" still works."));

        var alertMinutes = configuration.GetValue("Alerts:IntervalMinutes", 10);
        items.Add(alertMinutes > 0
            ? new DiagnosticItem("Alerting", "Alert sweep", DiagnosticSeverity.Ok, $"Every {alertMinutes} minute(s).")
            : new DiagnosticItem("Alerting", "Alert sweep", DiagnosticSeverity.Warning, "Disabled (Alerts:IntervalMinutes <= 0). No Overdue/Failed alerts will fire automatically."));

        var reportDays = configuration.GetValue("Reports:IntervalDays", 7);
        var distList = configuration["Reports:DistributionList"];
        if (reportDays > 0 && string.IsNullOrWhiteSpace(distList))
        {
            items.Add(new DiagnosticItem("Reporting", "Distribution list", DiagnosticSeverity.Problem, "Scheduled reports are enabled but Reports:DistributionList is empty, so nothing will be sent.", "/setup", "Configure Reports:DistributionList"));
        }
        else
        {
            items.Add(reportDays > 0
                ? new DiagnosticItem("Reporting", "Scheduled report", DiagnosticSeverity.Ok, $"Every {reportDays} day(s) to {distList}.")
                : new DiagnosticItem("Reporting", "Scheduled report", DiagnosticSeverity.Warning, "Disabled (Reports:IntervalDays <= 0). On-demand \"Send report\" still works."));
        }

        foreach (var worker in new[] { "Backup scan", "Alert check", "Scheduled report" })
        {
            var hb = heartbeats.Get(worker);
            if (hb is null)
            {
                items.Add(new DiagnosticItem("Workers", worker, DiagnosticSeverity.Warning, "No heartbeat recorded yet. It may not have run its first sweep."));
            }
            else if (!hb.Enabled)
            {
                items.Add(new DiagnosticItem("Workers", worker, DiagnosticSeverity.Warning, "Disabled by configuration."));
            }
            else if (hb.LastError is { Length: > 0 } error)
            {
                items.Add(new DiagnosticItem("Workers", worker, DiagnosticSeverity.Problem, $"Last run at {hb.LastRunUtc:u} failed: {error}"));
            }
            else
            {
                items.Add(new DiagnosticItem("Workers", worker, DiagnosticSeverity.Ok,
                    hb.LastRunUtc is { } last ? $"Last ran {last:u}; next due {hb.NextRunUtc:u}." : $"Starting; next due {hb.NextRunUtc:u}."));
            }
        }

        return items;
    }

    /// <summary>Checks each backup-file location under the service's own identity, which is the identity the scanner runs as.</summary>
    public async Task<List<PathCheck>> CheckFilePathsAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        var paths = await db.DatabaseInstances
            .Where(i => !i.IsArchived && i.BackupFileLocation != "")
            .Select(i => new { i.Name, i.BackupFileLocation })
            .ToListAsync();

        var results = new List<PathCheck>();
        foreach (var p in paths)
        {
            try
            {
                var reachable = Directory.Exists(p.BackupFileLocation);
                results.Add(new PathCheck(p.Name, p.BackupFileLocation, reachable, reachable ? null : "Path does not exist or is not reachable from the server process."));
            }
            catch (Exception ex)
            {
                results.Add(new PathCheck(p.Name, p.BackupFileLocation, false, ex.Message));
            }
        }

        return results;
    }

    /// <summary>Sends a real test email and reports the true Disabled/Accepted/Failed outcome.</summary>
    public Task<EmailSendResult> SendTestEmailAsync(string toAddress, CancellationToken ct = default) =>
        emailSender.SendAsync(toAddress, "DBTracker test email", "This is a test email sent from the DBTracker setup/diagnostics page to confirm email delivery is working.", ct);

    private static string Mask(string? secret) =>
        string.IsNullOrEmpty(secret) ? "(not set)" : secret.Length <= 4 ? "****" : $"****{secret[^4..]}";
}
