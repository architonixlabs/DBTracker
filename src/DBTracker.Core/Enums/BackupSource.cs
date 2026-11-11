namespace DBTracker.Core.Enums;

public enum BackupSource
{
    Manual,
    Discovered,

    /// <summary>Loaded from a history CSV at onboarding. A claim copied from another system: labelled as imported, never counted as verified evidence.</summary>
    Imported
}
