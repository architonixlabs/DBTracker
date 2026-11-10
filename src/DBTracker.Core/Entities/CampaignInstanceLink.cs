namespace DBTracker.Core.Entities;

/// <summary>One affected deployment grouped into a D10 MigrationCampaign.</summary>
public class CampaignInstanceLink
{
    public int Id { get; set; }

    public int MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
