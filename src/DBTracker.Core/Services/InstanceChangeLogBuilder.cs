using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>Diffs two snapshots of an instance's editable fields into change-log entries.</summary>
public static class InstanceChangeLogBuilder
{
    /// <summary>Every editable field as (property name, display value). Names match DatabaseInstance properties, which the
    /// concurrent-edit merge relies on.</summary>
    public static IReadOnlyList<(string Name, string Value)> Fields(DatabaseInstance i) =>
    [
        ("Name", i.Name),
        ("Engine", i.Engine.ToString()),
        ("Version", i.Version),
        ("Application", i.Application),
        ("Company", i.Company),
        ("Department", i.Department),
        ("ContactName", i.ContactName),
        ("ContactEmail", i.ContactEmail),
        ("ContactPhone", i.ContactPhone),
        ("OwnerTeamId", i.OwnerTeamId?.ToString() ?? ""),
        ("BackupContactName", i.BackupContactName),
        ("BackupContactEmail", i.BackupContactEmail),
        ("BackupContactPhone", i.BackupContactPhone),
        ("Region", i.Region),
        ("Cluster", i.Cluster),
        ("ReplicaSet", i.ReplicaSet),
        ("Role", i.Role.ToString()),
        ("Environment", i.Environment.ToString()),
        ("Criticality", i.Criticality.ToString()),
        ("DatabaseIp", i.DatabaseIp),
        ("ServerIp", i.ServerIp),
        ("ConnectivityHostOverride", i.ConnectivityHostOverride),
        ("ConnectivityPortOverride", i.ConnectivityPortOverride?.ToString() ?? ""),
        ("ExpectedBackupIntervalHours", i.ExpectedBackupIntervalHours.ToString()),
        ("BackupFileLocation", i.BackupFileLocation),
        ("BackupFileNamePatterns", i.BackupFileNamePatterns),
        ("RetentionPolicyDays", i.RetentionPolicyDays?.ToString() ?? ""),
        ("RestoreTestIntervalDaysOverride", i.RestoreTestIntervalDaysOverride?.ToString() ?? ""),
        ("Tags", i.Tags),
        ("RunbookUrl", i.RunbookUrl),
        ("Notes", i.Notes),
    ];

    public static IReadOnlyList<InstanceChangeLog> Diff(DatabaseInstance before, DatabaseInstance after, string changedBy, DateTime nowUtc)
    {
        return Fields(before).Zip(Fields(after), (b, a) => (b.Name, Old: b.Value, New: a.Value))
            .Where(f => !string.Equals(f.Old, f.New, StringComparison.Ordinal))
            .Select(f => new InstanceChangeLog
            {
                DatabaseInstanceId = after.Id,
                ChangedAtUtc = nowUtc,
                ChangedBy = changedBy,
                FieldName = f.Name,
                OldValue = f.Old,
                NewValue = f.New
            })
            .ToList();
    }
}
