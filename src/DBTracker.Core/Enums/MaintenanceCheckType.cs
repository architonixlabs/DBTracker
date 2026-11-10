namespace DBTracker.Core.Enums;

/// <summary>
/// What kind of assurance evidence a MaintenanceEvidenceRecord (D18) captures. Deliberately named
/// generically rather than after any one vendor's tool (e.g. "IntegrityCheck" rather than
/// "DbccCheckdb") since this covers every supported engine, not just SQL Server.
/// </summary>
public enum MaintenanceCheckType
{
    /// <summary>A database consistency/integrity check (e.g. SQL Server DBCC CHECKDB, PostgreSQL amcheck, a MongoDB validate).</summary>
    IntegrityCheck,
    ReplicationDrill,
    FailoverDrill,

    /// <summary>A required recurring maintenance job not otherwise covered above (index/statistics maintenance, log/vacuum housekeeping, etc.).</summary>
    MaintenanceJob
}
