using DBTracker.Tray.Core;

namespace DBTracker.Tray.Tests;

public class HttpsSettingsTests
{
    [Fact]
    public void NothingRecorded_StaysOnHttp()
    {
        var s = TraySettings.From("8200", null, null, null, null, null, null);
        Assert.False(s.UsesHttps);
        Assert.Equal(8200, s.ActivePort);
        Assert.Equal("http://localhost:8200/healthz", s.HealthUrl);
        Assert.Equal("http://localhost:8200/install", s.OpenUrl(setupRequired: true));
    }

    [Fact]
    public void RecordedHttps_PollsLocalhostAndOpensTheCertificateName()
    {
        var s = TraySettings.From("8150", null, null, null, "https", "8443", "DBTracker.Corp.Example");
        Assert.True(s.UsesHttps);
        Assert.Equal(8443, s.ActivePort);
        Assert.Equal("https://localhost:8443/healthz", s.HealthUrl);
        Assert.Equal("https://localhost:8443/api/tray/summary", s.SummaryUrl);
        Assert.Equal("https://dbtracker.corp.example:8443/", s.OpenUrl(setupRequired: false));
        Assert.Equal("https://dbtracker.corp.example:8443/account/tokens", s.TokensUrl);
        Assert.Equal("https://dbtracker.corp.example:8443/backups", s.DashboardUrl(new TraySummary { DashboardUrl = "/backups" }));
    }

    [Fact]
    public void Port443_IsLeftOutOfBrowserLinks()
    {
        var s = TraySettings.From(null, null, null, null, "HTTPS", "443", "tracker.example");
        Assert.Equal("https://tracker.example/install", s.OpenUrl(setupRequired: true));
        Assert.Equal("https://localhost:443/healthz", s.HealthUrl);
    }

    [Theory]
    [InlineData("*.corp.example")]
    [InlineData("evil.example/path")]
    [InlineData("")]
    [InlineData(null)]
    public void OddOrMissingCertificateName_FallsBackToLocalhost(string? host)
    {
        var s = TraySettings.From(null, null, null, null, "https", "8443", host);
        Assert.Equal("https://localhost:8443/", s.OpenUrl(setupRequired: false));
    }

    [Theory]
    [InlineData("https", "")]
    [InlineData("https", "0")]
    [InlineData("https", "70000")]
    [InlineData("http", "8443")]
    [InlineData("ftp", "8443")]
    public void HttpsNeedsTheSchemeAndAValidPort(string scheme, string httpsPort)
    {
        var s = TraySettings.From("8150", null, null, null, scheme, httpsPort, "tracker.example");
        Assert.False(s.UsesHttps);
        Assert.Equal("http://localhost:8150/healthz", s.HealthUrl);
    }

    [Fact]
    public void Tls_AcceptsACertificateThatPassesWindowsChecks()
    {
        Assert.True(TrayTls.Accept(true, null, _ => false));
    }

    [Fact]
    public void Tls_AcceptsThisMachinesOwnCertificateDespiteTheLocalhostNameMismatch()
    {
        string? looked = null;
        Assert.True(TrayTls.Accept(false, " ab12 ", t => { looked = t; return true; }));
        Assert.Equal("AB12", looked);
    }

    [Fact]
    public void Tls_RejectsAnUnknownCertificateWithErrors()
    {
        Assert.False(TrayTls.Accept(false, "AB12", _ => false));
        Assert.False(TrayTls.Accept(false, null, _ => true));
        Assert.False(TrayTls.Accept(false, "AB12", null!));
    }
}
