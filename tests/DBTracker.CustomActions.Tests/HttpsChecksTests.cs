using DBTracker.CustomActions;

namespace DBTracker.CustomActions.Tests;

public class HttpsChecksTests
{
    private const string Thumb = "0123456789ABCDEF0123456789ABCDEF01234567";
    private static readonly DateTime Now = new(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);

    private static CertificateInfo Good() => new()
    {
        Thumbprint = Thumb,
        HasPrivateKey = true,
        NotBefore = Now.AddDays(-30),
        NotAfter = Now.AddDays(300),
        DnsName = "tracker.example",
    };

    [Theory]
    [InlineData("01 23 45 67 89 ab cd ef 01 23 45 67 89 ab cd ef 01 23 45 67")]
    [InlineData("‎0123456789abcdef0123456789abcdef01234567")]
    [InlineData("01:23:45:67:89:AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67")]
    [InlineData("  0123456789ABCDEF0123456789ABCDEF01234567\r\n")]
    public void Normalize_StripsSeparatorsAndInvisibleMarks(string value)
    {
        var normalized = HttpsChecks.NormalizeThumbprint(value);
        Assert.Equal(Thumb, normalized);
        Assert.Null(HttpsChecks.ValidateThumbprintFormat(normalized));
    }

    [Theory]
    [InlineData("0123456789ABCDEF")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456G")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF012345678")]
    [InlineData("")]
    public void Format_RejectsAnythingButFortyHexDigits(string value)
    {
        Assert.NotNull(HttpsChecks.ValidateThumbprintFormat(HttpsChecks.NormalizeThumbprint(value)));
    }

    [Fact]
    public void Choose_GivenWins_PreviousFillsIn_NoneTurnsOff()
    {
        Assert.Equal(Thumb, HttpsChecks.ChooseThumbprint(Thumb.ToLowerInvariant(), "FFFF", out var fromPrevious));
        Assert.False(fromPrevious);

        Assert.Equal(Thumb, HttpsChecks.ChooseThumbprint("", Thumb, out fromPrevious));
        Assert.True(fromPrevious);

        Assert.Equal("", HttpsChecks.ChooseThumbprint(" None ", Thumb, out fromPrevious));
        Assert.False(fromPrevious);

        Assert.Equal("", HttpsChecks.ChooseThumbprint(null, null, out fromPrevious));
        Assert.False(fromPrevious);
    }

    [Fact]
    public void Certificate_GoodOneIsAccepted()
    {
        Assert.Null(HttpsChecks.CheckCertificate(Thumb, Good(), Now, fromPreviousInstall: false));
    }

    [Fact]
    public void Certificate_MissingOrWithoutKey_FailsWithAClearMessage()
    {
        Assert.Contains("LocalMachine\\My", HttpsChecks.CheckCertificate(Thumb, null, Now, false));

        var noKey = Good();
        noKey.HasPrivateKey = false;
        Assert.Contains("no private key", HttpsChecks.CheckCertificate(Thumb, noKey, Now, false));
    }

    [Fact]
    public void Certificate_ExpiredNotYetValidOrWrongUsage_Fails()
    {
        var expired = Good();
        expired.NotAfter = Now.AddDays(-1);
        Assert.Contains("expired", HttpsChecks.CheckCertificate(Thumb, expired, Now, false));

        var early = Good();
        early.NotBefore = Now.AddDays(1);
        Assert.Contains("not valid until", HttpsChecks.CheckCertificate(Thumb, early, Now, false));

        var clientOnly = Good();
        clientOnly.AllowsServerAuthentication = false;
        Assert.Contains("Server Authentication", HttpsChecks.CheckCertificate(Thumb, clientOnly, Now, false));
    }

    [Fact]
    public void Certificate_FromThePreviousInstall_SaysHowToReplaceOrTurnOff()
    {
        var message = HttpsChecks.CheckCertificate(Thumb, null, Now, fromPreviousInstall: true);
        Assert.Contains("CERT_THUMBPRINT=none", message);
        Assert.Contains("CertThumbprint=", message);
    }

    [Theory]
    [InlineData(null, null, 443)]
    [InlineData("", "8443", 8443)]
    [InlineData("9443", "8443", 9443)]
    public void HttpsPort_GivenThenPreviousThen443(string given, string previous, int expected)
    {
        Assert.Null(HttpsChecks.ValidateHttpsPort(given, previous, 8150, out var port));
        Assert.Equal(expected, port);
    }

    [Theory]
    [InlineData("8150")]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("70000")]
    public void HttpsPort_InvalidOrSameAsHttp_IsRejected(string given)
    {
        Assert.NotNull(HttpsChecks.ValidateHttpsPort(given, null, 8150, out _));
    }

    [Theory]
    [InlineData("Tracker.Example.", "tracker.example")]
    [InlineData("*.example.com", "localhost")]
    [InlineData(null, "localhost")]
    [InlineData("bad host", "localhost")]
    public void BrowserHost_UsesPlainCertificateNamesOnly(string dnsName, string expected)
    {
        Assert.Equal(expected, HttpsChecks.BrowserHost(dnsName));
    }

    [Theory]
    [InlineData("https", "443", "tracker.example", "8150", "https://tracker.example")]
    [InlineData("https", "8443", "tracker.example", "8150", "https://tracker.example:8443")]
    [InlineData("https", "8443", null, "8150", "https://localhost:8443")]
    [InlineData("http", "8443", "tracker.example", "8150", "http://localhost:8150")]
    [InlineData(null, null, null, "8150", "http://localhost:8150")]
    [InlineData("https", "", "tracker.example", "8150", "http://localhost:8150")]
    public void BrowserBaseUrl_FollowsWhatWasRecorded(string scheme, string httpsPort, string host, string httpPort, string expected)
    {
        Assert.Equal(expected, HttpsChecks.BrowserBaseUrl(scheme, httpsPort, host, httpPort));
    }

    [Fact]
    public void ServerAuthentication_NoListMeansAnyUse()
    {
        Assert.True(HttpsChecks.AllowsServerAuthentication(null));
        Assert.True(HttpsChecks.AllowsServerAuthentication([]));
        Assert.True(HttpsChecks.AllowsServerAuthentication(["1.3.6.1.5.5.7.3.2", "1.3.6.1.5.5.7.3.1"]));
        Assert.False(HttpsChecks.AllowsServerAuthentication(["1.3.6.1.5.5.7.3.2"]));
    }

    [Fact]
    public void Commands_BindOnEveryAddressWithDbTrackersAppId()
    {
        Assert.Equal("http add sslcert ipport=0.0.0.0:8443 certhash=" + Thumb + " appid={B9B7166A-8502-4965-BA3E-0EA237AF389F} certstorename=MY",
            HttpsChecks.NetshAddSslCertArguments(8443, Thumb));
        Assert.Equal("http delete sslcert ipport=0.0.0.0:8443", HttpsChecks.NetshDeleteSslCertArguments(8443));
        Assert.Equal("set site \"/site.name:Ops Tracker\" /+bindings.[protocol='https',bindingInformation='*:8443:']",
            HttpsChecks.AppCmdBindingArguments("Ops Tracker", 8443, add: true));
        Assert.Equal("set site \"/site.name:DBTracker\" /-bindings.[protocol='https',bindingInformation='*:443:']",
            HttpsChecks.AppCmdBindingArguments("DBTracker", 443, add: false));
    }
}
