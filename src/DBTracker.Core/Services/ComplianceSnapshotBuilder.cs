using System.Text.Json;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Frozen per-instance row inside a stored ComplianceSnapshot (U10) — plain data, never recomputed after generation.</summary>
public record ComplianceSnapshotRow(
    string InstanceName, string Company, string Department, InstanceRole Role, EnvironmentTier Environment,
    DateTime? LastBackupUtc, BackupHealth Health, bool RestoreTested);

/// <summary>
/// Builds and (de)serializes the frozen row set a ComplianceSnapshot stores. Pure (no I/O) —
/// serialization correctness (round-tripping, and staying stable even if the source instances are
/// later edited) is unit-testable without a database.
/// </summary>
public static class ComplianceSnapshotBuilder
{
    public static IReadOnlyList<ComplianceSnapshotRow> BuildRows(IEnumerable<ComplianceReportRow> rows) =>
        rows.Select(r => new ComplianceSnapshotRow(
                r.Instance.Name, r.Instance.Company, r.Instance.Department, r.Instance.Role, r.Instance.Environment,
                r.LastBackupUtc, r.Health, r.RestoreTested))
            .OrderBy(r => r.InstanceName)
            .ToList();

    public static string Serialize(IReadOnlyList<ComplianceSnapshotRow> rows) => JsonSerializer.Serialize(rows);

    public static IReadOnlyList<ComplianceSnapshotRow> Deserialize(string rowsJson) =>
        string.IsNullOrWhiteSpace(rowsJson)
            ? []
            : JsonSerializer.Deserialize<List<ComplianceSnapshotRow>>(rowsJson) ?? [];
}
