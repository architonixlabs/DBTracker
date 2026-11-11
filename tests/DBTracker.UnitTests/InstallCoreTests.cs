using DBTracker.Core.Services.Install;
using Xunit;

namespace DBTracker.UnitTests;

public class InstallStateResolverTests
{
    [Fact]
    public void DevelopmentAndTesting_AreAlwaysInstalled()
    {
        Assert.True(InstallStateResolver.IsInstalled(false, false, false, InstallProbeResult.NotProbed));
    }

    [Fact]
    public void CompletedMarker_MeansInstalled_WithoutProbing()
    {
        Assert.True(InstallStateResolver.IsInstalled(true, true, false, InstallProbeResult.NotProbed));
    }

    [Fact]
    public void NoConnectionString_ShowsWizard()
    {
        Assert.False(InstallStateResolver.IsInstalled(true, false, false, InstallProbeResult.NotProbed));
    }

    [Theory]
    [InlineData(InstallProbeResult.AdminExists, true)]
    [InlineData(InstallProbeResult.Unreachable, true)]
    [InlineData(InstallProbeResult.NoAdminOrDatabase, false)]
    public void ExistingDeployment_DependsOnWhatTheProbeFound(InstallProbeResult probe, bool expected)
    {
        Assert.Equal(expected, InstallStateResolver.IsInstalled(true, false, true, probe));
    }
}

public class SetupKeyTests
{
    [Fact]
    public void Generate_ProducesGroupedKeyOfExpectedLength()
    {
        var key = SetupKey.Generate();

        Assert.Equal(SetupKey.Length, SetupKey.Normalize(key).Length);
        Assert.Matches("^([A-Z0-9]{4}-){5}[A-Z0-9]{4}$", key);
        Assert.DoesNotContain('O', key);
        Assert.DoesNotContain('0', key);
    }

    [Fact]
    public void Generate_IsNotRepeated()
    {
        Assert.NotEqual(SetupKey.Generate(), SetupKey.Generate());
    }

    [Theory]
    [InlineData("ABCD-EFGH-JKMN")]
    [InlineData("abcd efgh jkmn")]
    [InlineData("  ABCDEFGHJKMN \n")]
    public void Matches_IgnoresCaseSpacingAndDashes(string candidate)
    {
        Assert.True(SetupKey.Matches("ABCD-EFGH-JKMN", candidate));
    }

    [Theory]
    [InlineData("ABCD-EFGH-JKMP")]
    [InlineData("")]
    [InlineData(null)]
    public void Matches_RejectsWrongOrMissingKeys(string? candidate)
    {
        Assert.False(SetupKey.Matches("ABCD-EFGH-JKMN", candidate));
    }

    [Fact]
    public void Matches_NeverAcceptsAnEmptyExpectedKey()
    {
        Assert.False(SetupKey.Matches("", ""));
    }
}

public class AttemptLimiterTests
{
    [Fact]
    public void LocksOutAfterMaxFailures_ThenUnlocksWhenTheWindowPasses()
    {
        var now = new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);
        var limiter = new AttemptLimiter(5, TimeSpan.FromMinutes(1), () => now);

        for (var i = 0; i < 4; i++)
        {
            limiter.RecordFailure("10.0.0.1");
        }
        Assert.False(limiter.IsLockedOut("10.0.0.1", out _));

        limiter.RecordFailure("10.0.0.1");
        Assert.True(limiter.IsLockedOut("10.0.0.1", out var retryAfter));
        Assert.Equal(TimeSpan.FromMinutes(1), retryAfter);

        now = now.AddSeconds(61);
        Assert.False(limiter.IsLockedOut("10.0.0.1", out _));
    }

    [Fact]
    public void ClientsAreCountedSeparately()
    {
        var limiter = new AttemptLimiter(2, TimeSpan.FromMinutes(1));
        limiter.RecordFailure("a");
        limiter.RecordFailure("a");

        Assert.True(limiter.IsLockedOut("a", out _));
        Assert.False(limiter.IsLockedOut("b", out _));
    }

    [Fact]
    public void Reset_ClearsFailures()
    {
        var limiter = new AttemptLimiter(1, TimeSpan.FromMinutes(1));
        limiter.RecordFailure("a");
        limiter.Reset("a");

        Assert.False(limiter.IsLockedOut("a", out _));
    }
}

public class DatabaseNameValidatorTests
{
    [Theory]
    [InlineData("DBTracker")]
    [InlineData("DBTracker_Prod")]
    [InlineData("dbtracker-2026.v2")]
    [InlineData("_staging")]
    public void AcceptsOrdinaryNames(string name)
    {
        Assert.Null(DatabaseNameValidator.Validate(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("db]; DROP DATABASE master; --")]
    [InlineData("my database")]
    [InlineData("-leading-dash")]
    [InlineData("master")]
    [InlineData("TempDB")]
    public void RejectsEmptyUnsafeOrSystemNames(string? name)
    {
        Assert.NotNull(DatabaseNameValidator.Validate(name));
    }

    [Fact]
    public void RejectsNamesLongerThan128Characters()
    {
        Assert.NotNull(DatabaseNameValidator.Validate(new string('a', 129)));
        Assert.Null(DatabaseNameValidator.Validate(new string('a', 128)));
    }
}

public class InstallDataPathsTests
{
    [Fact]
    public void DefaultsToAppDataUnderTheContentRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "site");
        var paths = InstallDataPaths.Resolve(root, null);

        Assert.Equal(Path.Combine(root, "App_Data"), paths.DataDirectory);
        Assert.Equal(Path.Combine(root, "App_Data", "dbtracker.config.json"), paths.ConfigFile);
        Assert.Equal(Path.Combine(root, "App_Data", "setup-key.txt"), paths.SetupKeyFile);
        Assert.Equal(Path.Combine(root, "App_Data", "keys"), paths.KeysDirectory);
    }

    [Fact]
    public void EnvironmentOverrideWins()
    {
        var custom = Path.Combine(Path.GetTempPath(), "dbtracker-data");
        Assert.Equal(custom, InstallDataPaths.Resolve("C:\\ignored", custom).DataDirectory);
    }
}
