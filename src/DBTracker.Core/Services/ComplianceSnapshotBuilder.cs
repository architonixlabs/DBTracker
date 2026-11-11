using System.Text.Json;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>A frozen per-instance row inside a stored ComplianceSnapshot, never recomputed.</summary>
public record ComplianceSnapshotRow(
    string InstanceName, string Company, string Department, InstanceRole Role, EnvironmentTier Environment,
    DateTime? LastBackupUtc, BackupHealth Health, bool RestoreTested);

/// <summary>Builds and (de)serializes a snapshot's frozen rows.</summary>
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
