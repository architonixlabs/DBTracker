using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class OwnershipStatusClassifierTests
{
    [Fact]
    public void NoTeamNoContact_IsMissing()
    {
        var status = OwnershipStatusClassifier.Classify(null, "", "", null);
        Assert.Equal(OwnershipStatus.Missing, status);
    }

    [Fact]
    public void HasContactEmail_ButNeverConfirmed_IsUnconfirmed()
    {
        var status = OwnershipStatusClassifier.Classify(null, "dev@acme.test", "", null);
        Assert.Equal(OwnershipStatus.Unconfirmed, status);
    }

    [Fact]
    public void HasTeamOnly_ButNeverConfirmed_IsUnconfirmed()
    {
        var status = OwnershipStatusClassifier.Classify(5, "", "", null);
        Assert.Equal(OwnershipStatus.Unconfirmed, status);
    }

    [Fact]
    public void HasBackupContactOnly_CountsAsOwnerInfo()
    {
        var status = OwnershipStatusClassifier.Classify(null, "", "backup@acme.test", null);
        Assert.Equal(OwnershipStatus.Unconfirmed, status);
    }

    [Fact]
    public void ConfirmedTimestampSet_IsConfirmed()
    {
        var status = OwnershipStatusClassifier.Classify(5, "dev@acme.test", "", DateTime.UtcNow);
        Assert.Equal(OwnershipStatus.Confirmed, status);
    }

    [Fact]
    public void ConfirmedTimestamp_WithoutAnyOwnerInfo_StillMissing()
    {
        // A stale confirmation timestamp from before ownership was cleared shouldn't claim confirmed.
        var status = OwnershipStatusClassifier.Classify(null, "", "", DateTime.UtcNow);
        Assert.Equal(OwnershipStatus.Missing, status);
    }
}
