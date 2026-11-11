namespace DBTracker.Core.Enums;

/// <summary>Migration technique, recorded only where the engine supports it. No migration scripts are generated.</summary>
public enum MigrationMethod
{
    Patch,
    InPlaceUpgrade,
    SideBySide,
    DumpLoad,
    Replication
}
