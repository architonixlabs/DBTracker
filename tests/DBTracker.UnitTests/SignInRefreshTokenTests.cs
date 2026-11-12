using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class SignInRefreshTokenTests
{
    [Fact]
    public void RoundTrips()
    {
        Assert.Equal(("user-1", "STAMP"), SignInRefreshToken.Parse(SignInRefreshToken.Format("user-1", "STAMP")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("only-one-part")]
    [InlineData("a\nb\nc")]
    [InlineData("\nstamp")]
    [InlineData("user\n")]
    public void Malformed_IsRejected(string? payload) => Assert.Null(SignInRefreshToken.Parse(payload));

    [Fact]
    public void LineBreaks_AreRefused() =>
        Assert.Throws<ArgumentException>(() => SignInRefreshToken.Format("user\n2", "stamp"));
}
