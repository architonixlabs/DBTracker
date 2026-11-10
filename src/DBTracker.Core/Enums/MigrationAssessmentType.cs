namespace DBTracker.Core.Enums;

/// <summary>
/// D08 — a version upgrade, a same-version relocation (new host/platform) and a cross-engine
/// conversion are deliberately distinct assessment types, each needing its own path rule/evidence,
/// rather than one generic "change version" flow.
/// </summary>
public enum MigrationAssessmentType
{
    VersionUpgrade,
    Relocation,
    CrossEngineConversion
}
