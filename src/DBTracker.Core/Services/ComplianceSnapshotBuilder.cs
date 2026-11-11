using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>A frozen per-instance row inside a stored ComplianceSnapshot, never recomputed. The fields after RestoreTested were added
/// later and are null in older snapshots, which the pages show as "not recorded" rather than filling in from today's data.</summary>
public record ComplianceSnapshotRow(
    string InstanceName, string Company, string Department, InstanceRole Role, EnvironmentTier Environment,
    DateTime? LastBackupUtc, BackupHealth Health, bool RestoreTested,
    int? InstanceId = null,
    EvidenceLevel? Evidence = null,
    RestoreTestRecency? RestoreRecency = null,
    DateTime? LastVerifiedRestoreUtc = null,
    string? PolicyName = null,
    int? PolicyVersion = null,
    int? ExpectedBackupIntervalHours = null,
    int? RestoreTestIntervalDays = null);

/// <summary>Everything one snapshot row is built from, gathered by the page at generation time.</summary>
public record ComplianceSnapshotInput(
    DatabaseInstance Instance,
    BackupHealth Health,
    DateTime? LastBackupUtc,
    IReadOnlyCollection<BackupRecord> Backups,
    IReadOnlyCollection<RestoreEvent> Restores,
    string? PolicyName);

/// <summary>Builds and (de)serializes a snapshot's frozen rows, and the policy-version and provenance labels stored with it.</summary>
public static class ComplianceSnapshotBuilder
{
    /// <summary>Version of the health and evidence rules (BackupHealthCalculator, BackupEvidenceClassifier, RestoreEvidence).
    /// Bump it whenever those rules change, so snapshots built under different rules can be told apart.</summary>
    public const int HealthRulesVersion = 2;

    public static IReadOnlyList<ComplianceSnapshotRow> BuildRows(IEnumerable<ComplianceReportRow> rows) =>
        rows.Select(r => new ComplianceSnapshotRow(
                r.Instance.Name, r.Instance.Company, r.Instance.Department, r.Instance.Role, r.Instance.Environment,
                r.LastBackupUtc, r.Health, r.RestoreTested, InstanceId: r.Instance.Id))
            .OrderBy(r => r.InstanceName)
            .ToList();

    /// <summary>Full rows: evidence level of the latest successful backup, restore-test recency and the policy in force, all as of <paramref name="asOfUtc"/>.</summary>
    public static IReadOnlyList<ComplianceSnapshotRow> BuildRows(IEnumerable<ComplianceSnapshotInput> inputs, DateTime asOfUtc, int defaultRestoreTestIntervalDays)
    {
        return inputs.Select(i =>
            {
                var restoresAsOf = i.Restores.Where(r => r.TimestampUtc <= asOfUtc).ToList();
                var latestSuccess = i.Backups
                    .Where(b => b.Status == BackupStatus.Success && b.TimestampUtc <= asOfUtc)
                    .OrderByDescending(b => b.TimestampUtc).ThenByDescending(b => b.Id)
                    .FirstOrDefault();
                var evidence = latestSuccess is null ? (EvidenceLevel?)null : BackupEvidenceClassifier.Classify(latestSuccess, restoresAsOf, asOfUtc);
                var lastVerified = RestoreEvidence.LastVerifiedUtc(restoresAsOf, asOfUtc);
                var intervalDays = i.Instance.RestoreTestIntervalDaysOverride ?? defaultRestoreTestIntervalDays;
                var recency = RestoreTestRecencyCalculator.Compute(lastVerified, intervalDays, asOfUtc);

                return new ComplianceSnapshotRow(
                    i.Instance.Name, i.Instance.Company, i.Instance.Department, i.Instance.Role, i.Instance.Environment,
                    i.LastBackupUtc, i.Health, RestoreTested: lastVerified is not null,
                    InstanceId: i.Instance.Id,
                    Evidence: evidence,
                    RestoreRecency: recency.Status,
                    LastVerifiedRestoreUtc: lastVerified,
                    PolicyName: i.PolicyName,
                    PolicyVersion: i.PolicyName is null ? null : i.Instance.AppliedPolicyVersion,
                    ExpectedBackupIntervalHours: i.Instance.ExpectedBackupIntervalHours,
                    RestoreTestIntervalDays: intervalDays);
            })
            .OrderBy(r => r.InstanceName)
            .ToList();
    }

    /// <summary>A short label (at most 50 characters) naming the rules version and a fingerprint of every row's policy, cadence and
    /// restore-test interval. Two snapshots with the same label were judged under the same settings.</summary>
    public static string PolicyVersionLabel(IReadOnlyList<ComplianceSnapshotRow> rows)
    {
        var canonical = string.Join("\n", rows
            .OrderBy(r => r.InstanceName, StringComparer.Ordinal)
            .Select(r => $"{r.InstanceName}|{r.PolicyName}|{r.PolicyVersion}|{r.ExpectedBackupIntervalHours}|{r.RestoreTestIntervalDays}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..8].ToLowerInvariant();
        var policies = rows.Where(r => r.PolicyName is not null).Select(r => r.PolicyName).Distinct().Count();
        return $"rules-v{HealthRulesVersion} {policies} polic{(policies == 1 ? "y" : "ies")} {hash}";
    }

    /// <summary>Where the rows' evidence came from, counted by evidence level (at most 500 characters).</summary>
    public static string DescribeProvenance(IReadOnlyList<ComplianceSnapshotRow> rows, string source)
    {
        int Count(EvidenceLevel level) => rows.Count(r => r.Evidence == level);
        var none = rows.Count(r => r.Evidence is null);
        var text = $"{source}; health rules v{HealthRulesVersion}. Latest successful backup per instance: " +
                   $"{Count(EvidenceLevel.RestoreTested)} restore-tested, {Count(EvidenceLevel.ReportedSuccess)} reported, " +
                   $"{Count(EvidenceLevel.Discovered)} discovered file, {Count(EvidenceLevel.Imported)} imported, {none} none. " +
                   "Restore tests count only successful restores logged in DBTracker, never imported history.";
        return text.Length <= 500 ? text : text[..500];
    }

    public static string Serialize(IReadOnlyList<ComplianceSnapshotRow> rows) => JsonSerializer.Serialize(rows);

    public static IReadOnlyList<ComplianceSnapshotRow> Deserialize(string rowsJson) =>
        string.IsNullOrWhiteSpace(rowsJson)
            ? []
            : JsonSerializer.Deserialize<List<ComplianceSnapshotRow>>(rowsJson) ?? [];
}
