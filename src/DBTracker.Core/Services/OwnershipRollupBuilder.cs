using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record OwnershipRollupInput(string Company, string Department, BackupHealth Health, string ContactName);
public record OwnershipRollupRow(string Company, string Department, int InstanceCount, int OnTrack, int Overdue, int Failed, int NeverBackedUp, string PrimaryContacts);

/// <summary>
/// Health counts rolled up by company/department. Serves the IT Manager persona: "who owns this"
/// and where risk is concentrated, without digging through the raw instance grid. Pure (no I/O).
/// </summary>
public static class OwnershipRollupBuilder
{
    public static IReadOnlyList<OwnershipRollupRow> Build(IEnumerable<OwnershipRollupInput> inputs) =>
        inputs
            .GroupBy(i => (i.Company, i.Department))
            .Select(g => new OwnershipRollupRow(
                g.Key.Company,
                g.Key.Department,
                g.Count(),
                g.Count(i => i.Health == BackupHealth.OnTrack),
                g.Count(i => i.Health == BackupHealth.Overdue),
                g.Count(i => i.Health == BackupHealth.Failed),
                g.Count(i => i.Health == BackupHealth.NeverBackedUp),
                string.Join(", ", g.Select(i => i.ContactName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())))
            .OrderBy(r => r.Company).ThenBy(r => r.Department)
            .ToList();
}
