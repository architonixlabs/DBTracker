using System.Text;

namespace DBTracker.Core.Services;

/// <summary>CSV export of the lifecycle workspace. Leading '#' lines carry the filter, the denominator, unknown counts and freshness, so a
/// filtered file can't be mistaken for the whole estate; every row carries its own identity observation time.</summary>
public static class LifecycleWorkspaceCsvWriter
{
    public static readonly string[] Columns =
    [
        "Instance", "Engine", "Family", "Branch", "SupportPhase", "DaysToNextMilestone", "Coverage", "Criticality", "Owner",
        "ClusterNodes", "Deployments", "HostedDatabases", "CampaignState", "Blocked", "Late", "LateReason", "ExpiringExceptions",
        "IdentityObservedAtUtc", "IdentityUnknownOrStale"
    ];

    public static string Build(LifecycleWorkspaceSummary summary, IReadOnlyList<LifecycleWorkspaceRow> matchingRows, string filterDescription, DateTime generatedAtUtc)
    {
        var sb = new StringBuilder();
        sb.Append("# DBTracker lifecycle workspace export, generated ").Append(generatedAtUtc.ToString("yyyy-MM-dd HH:mm")).Append(" UTC\n");
        sb.Append("# Filter: ").Append(string.IsNullOrWhiteSpace(filterDescription) ? "none" : filterDescription.Replace('\n', ' ')).Append('\n');
        sb.Append("# Rows in this file: ").Append(summary.MatchingCount)
          .Append(" of ").Append(summary.Denominator).Append(" non-archived instances (the denominator)\n");
        sb.Append("# Unknown or stale identity: ").Append(summary.UnknownIdentityCount).Append(" of ").Append(summary.Denominator)
          .Append(". Unknown support phase: ").Append(summary.UnknownSupportPhaseCount).Append(" of ").Append(summary.Denominator).Append('\n');
        sb.Append("# Oldest identity observation: ").Append(summary.OldestObservedAtUtc?.ToString("yyyy-MM-dd") ?? "none recorded")
          .Append(". An empty IdentityObservedAtUtc means the version was never observed.\n");
        sb.Append("# In these rows: ").Append(summary.MatchingDeploymentCount).Append(" deployment(s), ")
          .Append(summary.MatchingHostedDatabaseCount).Append(" hosted database(s), ")
          .Append(summary.MatchingLateCount).Append(" late migration(s). Cluster nodes are counted inside their instance, not as rows.\n");

        sb.Append(string.Join(",", Columns)).Append('\n');
        foreach (var r in matchingRows)
        {
            sb.Append(string.Join(",",
                Csv(r.InstanceName), Csv(r.Engine.ToString()), Csv(r.IdentityFamily ?? ""), Csv(r.Branch ?? ""), Csv(r.SupportPhase.ToString()),
                r.DaysToNextMilestone?.ToString() ?? "", Csv(r.Coverage.ToString()), Csv(r.Criticality.ToString()), Csv(r.OwnerTeamName ?? ""),
                r.ClusterNodeCount.ToString(), r.DeploymentCount.ToString(), r.HostedDatabaseCount.ToString(),
                Csv(r.LatestCampaignState?.ToString() ?? ""), r.IsBlocked ? "Yes" : "No", r.IsLate ? "Yes" : "No", Csv(r.LateReason ?? ""),
                r.ExpiringExceptionCount.ToString(), r.IdentityObservedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "",
                r.IsIdentityUnknownOrStale ? "Yes" : "No")).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Quotes a cell when needed, and neutralises a leading formula character so a spreadsheet won't evaluate it.</summary>
    public static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
