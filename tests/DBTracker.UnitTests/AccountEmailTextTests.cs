using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class AccountEmailTextTests
{
    [Fact]
    public void InviteBody_CarriesTheLinkAndSignInEmail_AndSaysWhenItExpires()
    {
        const string link = "https://dbtracker.example/Account/ResetPassword?userId=u1&code=abc&invite=true";
        var body = AccountEmailText.InviteBody(link, "dev.patel@ediqra.com");

        Assert.Contains(link, body);
        Assert.Contains("dev.patel@ediqra.com", body);
        Assert.Contains("expires in 24 hours", body);
    }

    [Fact]
    public void InviteBody_NeverMentionsAPassword_OnlyHowToSetOne() =>
        Assert.DoesNotContain("password:", AccountEmailText.InviteBody("https://x/l", "a@b.c"), StringComparison.OrdinalIgnoreCase);
}
