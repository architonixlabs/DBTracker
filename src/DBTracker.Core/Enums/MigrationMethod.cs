namespace DBTracker.Core.Enums;

/// <summary>D08 — only recorded where the engine/assessment actually supports it; this app never generates executable migration scripts for any of these.</summary>
public enum MigrationMethod
{
    Patch,
    InPlaceUpgrade,
    SideBySide,
    DumpLoad,
    Replication
}
