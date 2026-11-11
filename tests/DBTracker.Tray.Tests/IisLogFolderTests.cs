using DBTracker.Tray.Core;

namespace DBTracker.Tray.Tests;

public class IisLogFolderTests
{
    private const string Config = """
        <configuration><system.applicationHost><sites>
          <site name="Default Web Site" id="1" />
          <site name="DBTracker" id="3"><application path="/" /></site>
          <site name="Custom" id="4"><logFile directory="D:\IISLogs" /></site>
          <siteDefaults><logFile directory="%SystemDrive%\inetpub\logs\LogFiles" /></siteDefaults>
        </sites></system.applicationHost></configuration>
        """;

    [Fact]
    public void UsesSiteDefaultsAndSiteId()
    {
        Assert.Equal(@"%SystemDrive%\inetpub\logs\LogFiles\W3SVC3", IisLogFolder.Find(Config, "DBTracker"));
    }

    [Fact]
    public void SiteSpecificDirectoryWins()
    {
        Assert.Equal(@"D:\IISLogs\W3SVC4", IisLogFolder.Find(Config, "custom"));
    }

    [Fact]
    public void FallsBackToIisDefaultWithoutSiteDefaults()
    {
        var xml = """<configuration><system.applicationHost><sites><site name="DBTracker" id="2" /></sites></system.applicationHost></configuration>""";
        Assert.Equal(IisLogFolder.IisDefault + @"\W3SVC2", IisLogFolder.Find(xml, "DBTracker"));
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("")]
    public void UnknownSite_IsNull(string site)
    {
        Assert.Null(IisLogFolder.Find(Config, site));
    }

    [Fact]
    public void BadXml_IsNull()
    {
        Assert.Null(IisLogFolder.Find("<configuration>", "DBTracker"));
    }
}
