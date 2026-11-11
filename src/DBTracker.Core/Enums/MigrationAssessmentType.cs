namespace DBTracker.Core.Enums;

/// <summary>Version upgrade, same-version relocation and cross-engine conversion each need their own path rules and evidence.</summary>
public enum MigrationAssessmentType
{
    VersionUpgrade,
    Relocation,
    CrossEngineConversion
}
