using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class OwnerNoticeRecipientResolverTests
{
    [Fact]
    public void OwnerEmailAndTeamContacts_AreUsed_NotTheFallback()
    {
        var result = OwnerNoticeRecipientResolver.Resolve("owner@example.com", "Platform", ["lead@example.com", null], [], "contact@example.com", "the instance contact");

        Assert.Equal(["owner@example.com", "lead@example.com"], result.Emails);
        Assert.False(result.UsedFallback);
        Assert.Contains("recorded owner", result.RoutedTo);
        Assert.Contains("team Platform", result.RoutedTo);
    }

    [Fact]
    public void NoOwnerAddress_FallsBackToInstanceContact_AndSaysSo()
    {
        var result = OwnerNoticeRecipientResolver.Resolve("", null, [], [], "contact@example.com", "the instance contact");

        Assert.Equal(["contact@example.com"], result.Emails);
        Assert.True(result.UsedFallback);
        Assert.Contains("no owner email", result.RoutedTo);
    }

    [Fact]
    public void NobodyToEmail_IsReportedPlainly()
    {
        var result = OwnerNoticeRecipientResolver.Resolve(null, null, [], [], null, "the instance contact");

        Assert.Empty(result.Emails);
        Assert.StartsWith("nobody by email", result.RoutedTo);
    }

    [Fact]
    public void DuplicatesAndInvalidAddresses_AreDropped()
    {
        var result = OwnerNoticeRecipientResolver.Resolve("Owner@Example.com", "Ops", ["owner@example.com", "not-an-address"], ["sub@example.com"], null, "x");

        Assert.Equal(["Owner@Example.com", "sub@example.com"], result.Emails);
    }
}
