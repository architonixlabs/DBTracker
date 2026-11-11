using DBTracker.Tray.Core;

namespace DBTracker.Tray.Tests;

public class SettingsAndCommandTests
{
    [Fact]
    public void MissingRegistryValues_FallBackToDefaults()
    {
        var s = TraySettings.From(null, null, null, null);
        Assert.Equal(8150, s.Port);
        Assert.Equal(@"C:\inetpub\DBTracker", s.InstallFolder);
        Assert.Equal("DBTracker", s.SiteName);
        Assert.Equal("http://localhost:8150/healthz", s.HealthUrl);
    }

    [Fact]
    public void RecordedValues_AreUsed()
    {
        var s = TraySettings.From("8200", @"D:\Sites\DBTracker\", "Ops Tracker", "OpsPool");
        Assert.Equal(8200, s.Port);
        Assert.Equal(@"D:\Sites\DBTracker", s.InstallFolder);
        Assert.Equal(@"D:\Sites\DBTracker\App_Data", s.AppDataFolder(null));
        Assert.Equal(@"E:\Data", s.AppDataFolder(@"E:\Data"));
        Assert.Equal("http://localhost:8200/install", s.OpenUrl(setupRequired: true));
        Assert.Equal("http://localhost:8200/", s.OpenUrl(setupRequired: false));
    }

    [Theory]
    [InlineData("70000")]
    [InlineData("abc")]
    [InlineData("0")]
    public void InvalidPort_FallsBack(string port)
    {
        Assert.Equal(8150, TraySettings.From(port, null, null, null).Port);
    }

    [Theory]
    [InlineData("pool\" & calc")]
    [InlineData("a/b")]
    [InlineData("")]
    public void UnsafeNames_AreRejected(string name)
    {
        var s = TraySettings.From(null, null, name, name);
        Assert.Equal("DBTracker", s.SiteName);
        Assert.Equal("DBTracker", s.AppPoolName);
    }

    [Theory]
    [InlineData("//evil.example.com/x")]
    [InlineData("https://evil.example.com")]
    [InlineData(null)]
    public void DashboardUrl_StaysOnTheLocalSite(string? path)
    {
        var s = TraySettings.From(null, null, null, null);
        Assert.Equal("http://localhost:8150/", s.DashboardUrl(new TraySummary { DashboardUrl = path }));
    }

    [Fact]
    public void RestartAppPool_IsStopThenStart()
    {
        var args = AdminCommands.AppCmdArguments(AdminAction.RestartAppPool, TraySettings.From(null, null, null, "OpsPool"));
        Assert.Equal(new[] { "stop apppool /apppool.name:\"OpsPool\"", "start apppool /apppool.name:\"OpsPool\"" }, args);
    }

    [Fact]
    public void AdminArguments_RoundTrip()
    {
        foreach (AdminAction action in Enum.GetValues(typeof(AdminAction)))
        {
            Assert.True(AdminCommands.TryParse(AdminCommands.ToArgument(action), out var parsed));
            Assert.Equal(action, parsed);
        }
        Assert.False(AdminCommands.TryParse("format-c", out _));
    }

    [Theory]
    [InlineData("dbt_abcdefghijklmnopqrstuvwxyz012345", true)]
    [InlineData("  dbt_abcdefghijklmnopqrstuvwxyz012345\r\n", true)]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345", false)]
    [InlineData("dbt_short", false)]
    [InlineData("dbt_abcdefghij klmnopqrstuvwxyz", false)]
    public void TokenFormat_Check(string token, bool expected)
    {
        Assert.Equal(expected, TokenFormat.LooksValid(token));
    }

    [Fact]
    public void EventLogFilter_MatchesThisAppOnly()
    {
        var s = TraySettings.From(null, @"C:\inetpub\DBTracker", null, null);
        Assert.True(EventLogFilter.IsAboutThisApp("IIS AspNetCore Module V2",
            "Application '/LM/W3SVC/2/ROOT' with physical root 'C:\\inetpub\\DBTracker\\' failed to start.", s));
        Assert.False(EventLogFilter.IsAboutThisApp("IIS AspNetCore Module V2",
            "Application with physical root 'C:\\inetpub\\Other\\' failed to start.", s));
        Assert.False(EventLogFilter.IsAboutThisApp("MSSQL$SQLEXPRESS", "C:\\inetpub\\DBTracker", s));
    }

    [Fact]
    public void Tooltip_FitsTheWindowsLimit()
    {
        var state = new HealthState(StatusLight.Amber, new string('x', 200), false);
        Assert.True(TrayText.Tooltip(state).Length <= TrayText.MaxTooltipLength);
    }
}
