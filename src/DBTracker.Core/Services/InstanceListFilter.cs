using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// A flat, denormalized row for the Instances list/dashboard — carries only the fields those views
/// render, so the query that produces it (InstanceOverviewService) never has to pull each
/// instance's full BackupRecords/RestoreEvents graph just to show a grid (U06).
/// </summary>
public record InstanceListRow(
    int Id,
    string Name,
    DatabaseEngine Engine,
    string Version,
    string Application,
    string Company,
    string Department,
    string Tags,
    string ContactName,
    string ContactEmail,
    InstanceRole Role,
    EnvironmentTier Environment,
    string DatabaseIp,
    BackupHealth Health,
    DateTime? LastBackupUtc,
    DateTime? MaintenanceUntilUtc,
    string BackupFileLocation,
    DateTime? LastScanUtc,
    ScanStatus LastScanStatus,
    string LastScanError);

public enum InstanceSortKey
{
    Name,
    Risk,
    Engine,
    Company,
    Environment
}

/// <summary>Everything the Instances list needs to reproduce a view from a URL (U05) — every field round-trips through query-string parameters.</summary>
public record InstanceListQuery(
    string? Search = null,
    DatabaseEngine? Engine = null,
    BackupHealth? Health = null,
    InstanceRole? Role = null,
    EnvironmentTier? Environment = null,
    string? Company = null,
    string? Department = null,
    string? Owner = null,
    string? Tag = null,
    bool MyInstancesOnly = false,
    string? MyEmail = null,
    InstanceSortKey Sort = InstanceSortKey.Name,
    bool Descending = false,
    int Page = 1,
    int PageSize = 25);

public record InstanceListResult(IReadOnlyList<InstanceListRow> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

/// <summary>
/// Pure filter/sort/page over an already-materialized set of rows (U05/U06) — no EF, no I/O, so
/// the query logic that drives the Instances list, saved views and shareable URLs is fully
/// unit-testable independent of the database.
/// </summary>
public static class InstanceListFilter
{
    public const int MaxPageSize = 200;

    private static readonly Dictionary<BackupHealth, int> RiskRank = new()
    {
        [BackupHealth.Failed] = 0,
        [BackupHealth.NeverBackedUp] = 1,
        [BackupHealth.Overdue] = 2,
        [BackupHealth.OnTrack] = 3,
    };

    public static InstanceListResult Apply(IEnumerable<InstanceListRow> rows, InstanceListQuery query)
    {
        var items = rows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            items = items.Where(r =>
                Contains(r.Name, term) || Contains(r.Application, term) || Contains(r.Company, term) ||
                Contains(r.Department, term) || Contains(r.ContactName, term) || Contains(r.ContactEmail, term) ||
                Contains(r.DatabaseIp, term));
        }

        if (query.Engine is { } engine)
        {
            items = items.Where(r => r.Engine == engine);
        }

        if (query.Health is { } health)
        {
            items = items.Where(r => r.Health == health);
        }

        if (query.Role is { } role)
        {
            items = items.Where(r => r.Role == role);
        }

        if (query.Environment is { } env)
        {
            items = items.Where(r => r.Environment == env);
        }

        if (!string.IsNullOrWhiteSpace(query.Company))
        {
            items = items.Where(r => string.Equals(r.Company, query.Company, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Department))
        {
            items = items.Where(r => string.Equals(r.Department, query.Department, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Owner))
        {
            items = items.Where(r => string.Equals(r.ContactName, query.Owner, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            items = items.Where(r => SplitTags(r.Tags).Any(t => string.Equals(t, query.Tag, StringComparison.OrdinalIgnoreCase)));
        }

        if (query.MyInstancesOnly && !string.IsNullOrWhiteSpace(query.MyEmail))
        {
            items = items.Where(r => string.Equals(r.ContactEmail, query.MyEmail, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = OrderBy(items, query.Sort, query.Descending);
        var list = ordered.ToList();
        var totalCount = list.Count;

        var pageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, MaxPageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Clamp(query.Page <= 0 ? 1 : query.Page, 1, totalPages);

        var pageItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new InstanceListResult(pageItems, totalCount, page, pageSize);
    }

    public static IReadOnlyList<string> SplitTags(string tags) =>
        tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IOrderedEnumerable<InstanceListRow> OrderBy(IEnumerable<InstanceListRow> items, InstanceSortKey sort, bool descending)
    {
        IOrderedEnumerable<InstanceListRow> ordered = sort switch
        {
            InstanceSortKey.Risk => descending
                ? items.OrderByDescending(r => RiskRank[r.Health])
                : items.OrderBy(r => RiskRank[r.Health]),
            InstanceSortKey.Engine => descending
                ? items.OrderByDescending(r => r.Engine)
                : items.OrderBy(r => r.Engine),
            InstanceSortKey.Company => descending
                ? items.OrderByDescending(r => r.Company, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(r => r.Company, StringComparer.OrdinalIgnoreCase),
            InstanceSortKey.Environment => descending
                ? items.OrderByDescending(r => r.Environment)
                : items.OrderBy(r => r.Environment),
            _ => descending
                ? items.OrderByDescending(r => r.Name, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
        };

        // Secondary key so paging is stable even when many rows tie on the primary sort (e.g. every Failed instance).
        return ordered.ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Id);
    }

    private static bool Contains(string haystack, string term) =>
        haystack.Contains(term, StringComparison.OrdinalIgnoreCase);
}
