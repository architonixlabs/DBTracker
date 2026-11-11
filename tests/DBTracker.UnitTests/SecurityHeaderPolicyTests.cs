using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class SecurityHeaderPolicyTests
{
    [Fact]
    public void Csp_RunsOnlyNoncedScripts_AndBlocksPluginsFramingAndForeignForms()
    {
        var csp = SecurityHeaderPolicy.ContentSecurityPolicy("abc123");

        Assert.Contains("script-src 'nonce-abc123' 'strict-dynamic' 'report-sample'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("form-action 'self'", csp);
        Assert.Contains("report-to csp", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.DoesNotContain("script-src 'unsafe-inline'", csp);
    }

    [Fact]
    public void Csp_KeepsBlazorsBaseTag() =>
        Assert.Contains("base-uri 'self'", SecurityHeaderPolicy.ContentSecurityPolicy("n"));

    [Fact]
    public void TrustedTypes_IsReportOnly_NotEnforced()
    {
        Assert.DoesNotContain("trusted-types", SecurityHeaderPolicy.ContentSecurityPolicy("n"));
        Assert.Contains("require-trusted-types-for 'script'", SecurityHeaderPolicy.ContentSecurityPolicyReportOnly());
    }

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    [InlineData("Cross-Origin-Opener-Policy", "same-origin-allow-popups")]
    [InlineData("Cross-Origin-Resource-Policy", "same-origin")]
    [InlineData("Reporting-Endpoints", "csp=\"/csp-reports\"")]
    public void CompanionHeaders_AreSent(string name, string value) =>
        Assert.Contains(SecurityHeaderPolicy.Headers("n"), h => h.Key == name && h.Value == value);

    [Fact]
    public void PermissionsPolicy_TurnsOffTheCamera() =>
        Assert.Contains("camera=()", SecurityHeaderPolicy.Headers("n").Single(h => h.Key == "Permissions-Policy").Value);

    [Theory]
    [InlineData("https://host/Account/ResetPassword?userId=1&code=secret", "https://host/Account/ResetPassword")]
    [InlineData("https://host/page#frag", "https://host/page")]
    [InlineData("inline", "inline")]
    [InlineData(null, "")]
    public void ReportUrls_LoseTheirQueryStrings(string? url, string expected) =>
        Assert.Equal(expected, SecurityHeaderPolicy.StripQuery(url));
}
