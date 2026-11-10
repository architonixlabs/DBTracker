using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>
/// Diffs two snapshots of a DatabaseInstance's editable fields into change-log entries — audit
/// evidence of who changed what, not just that the record was touched. Pure (no I/O).
/// </summary>
public static class InstanceChangeLogBuilder
{
    public static IReadOnlyList<InstanceChangeLog> Diff(DatabaseInstance before, DatabaseInstance after, string changedBy, DateTime nowUtc)
    {
        var fields = new (string Name, string Old, string New)[]
        {
            ("Name", before.Name, after.Name),
            ("Engine", before.Engine.ToString(), after.Engine.ToString()),
            ("Version", before.Version, after.Version),
            ("Application", before.Application, after.Application),
            ("Company", before.Company, after.Company),
            ("Department", before.Department, after.Department),
            ("ContactName", before.ContactName, after.ContactName),
            ("ContactEmail", before.ContactEmail, after.ContactEmail),
            ("ContactPhone", before.ContactPhone, after.ContactPhone),
            ("OwnerTeamId", before.OwnerTeamId?.ToString() ?? "", after.OwnerTeamId?.ToString() ?? ""),
            ("BackupContactName", before.BackupContactName, after.BackupContactName),
            ("BackupContactEmail", before.BackupContactEmail, after.BackupContactEmail),
            ("BackupContactPhone", before.BackupContactPhone, after.BackupContactPhone),
            ("Region", before.Region, after.Region),
            ("Cluster", before.Cluster, after.Cluster),
            ("ReplicaSet", before.ReplicaSet, after.ReplicaSet),
            ("Role", before.Role.ToString(), after.Role.ToString()),
            ("Environment", before.Environment.ToString(), after.Environment.ToString()),
            ("DatabaseIp", before.DatabaseIp, after.DatabaseIp),
            ("ServerIp", before.ServerIp, after.ServerIp),
            ("ExpectedBackupIntervalHours", before.ExpectedBackupIntervalHours.ToString(), after.ExpectedBackupIntervalHours.ToString()),
            ("BackupFileLocation", before.BackupFileLocation, after.BackupFileLocation),
            ("BackupFileNamePatterns", before.BackupFileNamePatterns, after.BackupFileNamePatterns),
            ("RetentionPolicyDays", before.RetentionPolicyDays?.ToString() ?? "", after.RetentionPolicyDays?.ToString() ?? ""),
            ("Tags", before.Tags, after.Tags),
            ("RunbookUrl", before.RunbookUrl, after.RunbookUrl),
            ("Notes", before.Notes, after.Notes),
        };

        return fields
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
