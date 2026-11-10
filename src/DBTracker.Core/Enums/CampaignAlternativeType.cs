namespace DBTracker.Core.Enums;

/// <summary>D10 — a campaign can record and compare alternatives rather than assuming upgrade is the only option.</summary>
public enum CampaignAlternativeType
{
    Upgrade,
    Patch,
    Replace,
    Retire,
    /// <summary>A documented support-extension or exception (e.g. paid ESU, a formal risk exception) instead of changing the database at all.</summary>
    SupportExtensionOrException
}
