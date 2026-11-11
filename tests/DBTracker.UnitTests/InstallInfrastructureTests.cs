using DBTracker.Infrastructure.Install;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DBTracker.UnitTests;

public sealed class InstallConfigWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dbt-config-" + Guid.NewGuid().ToString("N"));
    private readonly IDataProtector _protector = new EphemeralDataProtectionProvider().CreateProtector(InstallConfigWriter.ProtectorPurpose);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static InstallConfiguration Sample(bool sqlLogin, InstallEmailSettings email) => new(
        sqlLogin
            ? "Data Source=sql01;Initial Catalog=DBTracker;User ID=dbt;Password=fake-test-value-1;Encrypt=True"
            : "Data Source=sql01;Initial Catalog=DBTracker;Integrated Security=True;Encrypt=True",
        sqlLogin,
        email,
        "dba@example.com, leads@example.com",
        new DateTime(2026, 11, 11, 9, 30, 0, DateTimeKind.Utc),
        "admin@example.com");

    private IConfigurationRoot Load(string path) =>
        new ConfigurationBuilder().AddProtectedJsonFile(path, _protector).Build();

    [Fact]
    public void SecretsAreEncryptedOnDisk_AndDecryptedWhenLoaded()
    {
        var path = Path.Combine(_dir, "dbtracker.config.json");
        var email = new InstallEmailSettings
        {
            Provider = EmailProviders.Smtp, SmtpHost = "smtp.example.com", SmtpPort = 2525, SmtpUsername = "mailer",
            SmtpPassword = "Mail-P@ss-123", SmtpFromAddress = "dbtracker@example.com",
        };

        InstallConfigWriter.Write(path, Sample(sqlLogin: true, email), _protector);

        var raw = File.ReadAllText(path);
        Assert.DoesNotContain("fake-test-value-1", raw);
        Assert.DoesNotContain("Mail-P@ss-123", raw);
        Assert.Contains(ProtectedValue.Prefix, raw);

        var config = Load(path);
        Assert.Contains("Password=fake-test-value-1", config.GetConnectionString("DBTracker"));
        Assert.Equal("Mail-P@ss-123", config["Email:Smtp:Password"]);
        Assert.Equal("2525", config["Email:Smtp:Port"]);
        Assert.Equal("Smtp", config["Email:Provider"]);
        Assert.Equal("admin@example.com", config["Install:CompletedBy"]);
        Assert.Equal("2026-11-11T09:30:00.0000000Z", config["Install:CompletedAtUtc"]);
        Assert.Equal("dba@example.com, leads@example.com", config["Reports:DistributionList"]);
    }

    [Fact]
    public void WindowsAuthConnectionString_StaysReadable_AndArxMailKeyIsEncrypted()
    {
        var path = Path.Combine(_dir, "dbtracker.config.json");
        var email = new InstallEmailSettings { Provider = EmailProviders.ArxMail, ArxMailFromAddress = "dbt@example.com", ArxMailApiKey = "arx-key-42" };

        InstallConfigWriter.Write(path, Sample(sqlLogin: false, email), _protector);

        var raw = File.ReadAllText(path);
        Assert.Contains("Integrated Security=True", raw);
        Assert.DoesNotContain("arx-key-42", raw);
        Assert.DoesNotContain("\"Smtp\"", raw);
        Assert.Equal("arx-key-42", Load(path)["Email:ArxMail:ApiKey"]);
    }

    [Fact]
    public void NoEmailProvider_WritesOnlyTheProvider()
    {
        var path = Path.Combine(_dir, "dbtracker.config.json");
        InstallConfigWriter.Write(path, Sample(false, new InstallEmailSettings()) with { ReportsDistributionList = null }, _protector);

        var config = Load(path);
        Assert.Equal("None", config["Email:Provider"]);
        Assert.Null(config["Email:Smtp:Host"]);
        Assert.Null(config["Email:ArxMail:Endpoint"]);
        Assert.Null(config["Reports:DistributionList"]);
    }

    [Fact]
    public void LoadingWithDifferentKeys_FailsWithAnActionableMessage()
    {
        var path = Path.Combine(_dir, "dbtracker.config.json");
        InstallConfigWriter.Write(path, Sample(true, new InstallEmailSettings()), _protector);

        var otherKeys = new EphemeralDataProtectionProvider().CreateProtector(InstallConfigWriter.ProtectorPurpose);
        var ex = Assert.Throws<InvalidDataException>(() => new ConfigurationBuilder().AddProtectedJsonFile(path, otherKeys).Build());
        var inner = Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("delete the config file", inner.Message);
    }

    [Fact]
    public void MissingFile_IsOptional()
    {
        var config = Load(Path.Combine(_dir, "absent.json"));
        Assert.Null(config.GetConnectionString("DBTracker"));
    }
}

public sealed class SetupKeyStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "dbt-key-" + Guid.NewGuid().ToString("N"), "setup-key.txt");

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EnsureKey_CreatesTheFile_AndReusesItOnTheNextStart()
    {
        var store = new SetupKeyStore(_path);
        var first = store.EnsureKey();

        Assert.True(File.Exists(_path));
        Assert.Equal(first, new SetupKeyStore(_path).EnsureKey());
        Assert.True(store.Verify(first.ToLowerInvariant().Replace("-", " ")));
        Assert.False(store.Verify("WRONG-KEY"));
    }

    [Fact]
    public void Delete_RemovesTheKey_SoNothingVerifiesAfterwards()
    {
        var store = new SetupKeyStore(_path);
        var key = store.EnsureKey();
        store.Delete();

        Assert.False(File.Exists(_path));
        Assert.False(store.Verify(key));
    }
}

public class InstallDatabaseSettingsTests
{
    [Fact]
    public void WindowsAuthentication_UsesIntegratedSecurity_AndNoPassword()
    {
        var settings = new InstallDatabaseSettings(" sql01\\SQLEXPRESS ", " DBTracker ", true, "ignored", "ignored", true, false);
        var builder = new SqlConnectionStringBuilder(InstallDatabaseService.BuildConnectionString(settings));

        Assert.Equal("sql01\\SQLEXPRESS", builder.DataSource);
        Assert.Equal("DBTracker", builder.InitialCatalog);
        Assert.True(builder.IntegratedSecurity);
        Assert.Equal("", builder.Password);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, builder.Encrypt);
        Assert.False(builder.TrustServerCertificate);
        Assert.False(settings.HasSecret);
    }

    [Fact]
    public void SqlLogin_CarriesTheCredentials_AndCanTargetMaster()
    {
        var settings = new InstallDatabaseSettings("sql01", "DBTracker", false, "dbt", "p@ss;word", false, true);
        var builder = new SqlConnectionStringBuilder(InstallDatabaseService.BuildConnectionString(settings, "master"));

        Assert.Equal("master", builder.InitialCatalog);
        Assert.False(builder.IntegratedSecurity);
        Assert.Equal("dbt", builder.UserID);
        Assert.Equal("p@ss;word", builder.Password);
        Assert.Equal(SqlConnectionEncryptOption.Optional, builder.Encrypt);
        Assert.True(builder.TrustServerCertificate);
        Assert.True(settings.HasSecret);
    }
}

public class InstallStateTests
{
    private static IConfiguration Config(string? connectionString) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DBTracker"] = connectionString }).Build();

    [Fact]
    public void PendingConnectionString_WinsUntilInstalled_ThenConfigTakesOver()
    {
        var state = new InstallState(Config("from-config"), installFeatureEnabled: true, initiallyInstalled: false);
        Assert.Equal("from-config", state.ConnectionString);

        state.UsePendingConnectionString("from-wizard");
        Assert.Equal("from-wizard", state.ConnectionString);

        state.MarkInstalled();
        Assert.True(state.IsInstalled);
        Assert.Equal("from-config", state.ConnectionString);
    }

    [Fact]
    public async Task WaitUntilInstalled_ReleasesBackgroundWorkersOnlyWhenSetupFinishes()
    {
        var state = new InstallState(Config(null), true, false);
        var wait = state.WaitUntilInstalledAsync(CancellationToken.None);

        await Task.Delay(50);
        Assert.False(wait.IsCompleted);

        state.MarkInstalled();
        await wait.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Factory_StampsEachNewContextWithTheCurrentConnectionString()
    {
        var state = new InstallState(Config(null), true, false);
        var options = new DbContextOptionsBuilder<DBTracker.Infrastructure.DBTrackerDbContext>().UseSqlServer().Options;
        var factory = new InstallAwareDbContextFactory(options, state);

        using (var before = factory.CreateDbContext())
        {
            Assert.True(string.IsNullOrEmpty(before.Database.GetConnectionString()));
        }

        state.UsePendingConnectionString("Data Source=sql01;Initial Catalog=DBTracker;Integrated Security=True");
        using var after = factory.CreateDbContext();
        Assert.Contains("sql01", after.Database.GetConnectionString());
    }

    [Fact]
    public void AlreadyInstalled_RejectsAPendingConnectionString()
    {
        var state = new InstallState(Config("x"), false, true);
        Assert.True(state.WaitUntilInstalledAsync(CancellationToken.None).IsCompleted);
        Assert.Throws<InvalidOperationException>(() => state.UsePendingConnectionString("y"));
    }
}
