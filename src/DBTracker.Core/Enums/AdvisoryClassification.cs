namespace DBTracker.Core.Enums;

/// <summary>How a security advisory applies to one instance. Version matching alone can only suggest Potential, NotApplicable or Unknown; Confirmed needs a reviewer, since backports and managed patching can make a build-range match misleading.</summary>
public enum AdvisoryClassification
{
    Potential,
    Confirmed,
    NotApplicable,
    Unknown
}
