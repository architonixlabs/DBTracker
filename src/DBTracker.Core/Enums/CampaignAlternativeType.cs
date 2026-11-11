namespace DBTracker.Core.Enums;

public enum CampaignAlternativeType
{
    Upgrade,
    Patch,
    Replace,
    Retire,
    /// <summary>A documented support-extension or exception (e.g. paid ESU, a formal risk exception) instead of changing the database at all.</summary>
    SupportExtensionOrException
}
