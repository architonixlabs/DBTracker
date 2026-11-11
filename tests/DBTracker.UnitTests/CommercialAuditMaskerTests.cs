using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class CommercialAuditMaskerTests
{
    private const string LegacyReason = "ExtendedSupport: 12,500.00 USD (SQL Server 2019 ESU)";

    [Fact]
    public void CostEstimateReason_IsHidden_FromRolesWithoutCommercialAccess() =>
        Assert.Equal(CommercialAuditMasker.HiddenText, CommercialAuditMasker.Reason("EditionCostEstimateRecorded", LegacyReason, canSeeCommercialEvidence: false));

    [Fact]
    public void CostEstimateReason_IsShown_ToCommercialRoles() =>
        Assert.Equal(LegacyReason, CommercialAuditMasker.Reason("EditionCostEstimateRecorded", LegacyReason, canSeeCommercialEvidence: true));

    [Fact]
    public void OtherActions_AreNeverMasked() =>
        Assert.Equal("Bulk edit batch 1a2b3c4d", CommercialAuditMasker.Reason("InstanceBulkEdited", "Bulk edit batch 1a2b3c4d", canSeeCommercialEvidence: false));
}
