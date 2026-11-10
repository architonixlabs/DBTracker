namespace DBTracker.Core.Enums;

/// <summary>
/// How a security advisory applies to one instance (D05). Version matching alone can only ever
/// produce Potential or NotApplicable/Unknown automatically — Confirmed requires a reviewer to set
/// it explicitly, with evidence, because a build-range match does not by itself confirm exposure
/// (distribution backports, managed-service patching, and config-dependent advisories can all mean
/// a version match isn't actually exploitable, or that an older-looking build is already fixed).
/// </summary>
public enum AdvisoryClassification
{
    Potential,
    Confirmed,
    NotApplicable,
    Unknown
}
