namespace DBTracker.Core.Enums;

/// <summary>Kind of assurance evidence, named generically (IntegrityCheck, not DbccCheckdb) to cover every engine.</summary>
public enum MaintenanceCheckType
{
    /// <summary>A database consistency/integrity check (e.g. SQL Server DBCC CHECKDB, PostgreSQL amcheck, a MongoDB validate).</summary>
    IntegrityCheck,
    ReplicationDrill,
    FailoverDrill,

    /// <summary>A required recurring maintenance job not otherwise covered above (index/statistics maintenance, log/vacuum housekeeping, etc.).</summary>
    MaintenanceJob
}
