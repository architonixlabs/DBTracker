using System.Data;
using DBTracker.Core.Services.Install;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DBTracker.Infrastructure.Install;

public sealed record InstallDatabaseSettings(
    string Server,
    string Database,
    bool UseWindowsAuthentication,
    string SqlUser,
    string SqlPassword,
    bool Encrypt,
    bool TrustServerCertificate)
{
    public bool HasSecret => !UseWindowsAuthentication && !string.IsNullOrEmpty(SqlPassword);
}

public enum DatabaseCheckStatus
{
    InvalidName,
    ServerUnreachable,
    LoginFailed,
    CertificateNotTrusted,
    NoAccessToDatabase,
    DatabaseMissing,
    DatabaseEmpty,
    DatabaseHasDBTracker,
    DatabaseHasOtherTables
}

public sealed record DatabaseCheckResult(DatabaseCheckStatus Status, string Message)
{
    public bool CanContinue => Status is DatabaseCheckStatus.DatabaseEmpty or DatabaseCheckStatus.DatabaseHasDBTracker;
}

/// <summary>The SQL Server work the wizard does. Never puts user input into SQL text; passwords never reach messages.</summary>
public sealed class InstallDatabaseService
{
    public static string BuildConnectionString(InstallDatabaseSettings settings, string? databaseOverride = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Server.Trim(),
            InitialCatalog = databaseOverride ?? settings.Database.Trim(),
            Encrypt = settings.Encrypt ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = settings.TrustServerCertificate,
            ApplicationName = "DBTracker",
            ConnectTimeout = 15,
        };

        if (settings.UseWindowsAuthentication)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = settings.SqlUser.Trim();
            builder.Password = settings.SqlPassword;
        }

        return builder.ConnectionString;
    }

    public async Task<DatabaseCheckResult> CheckAsync(InstallDatabaseSettings settings, CancellationToken ct = default)
    {
        var nameError = DatabaseNameValidator.Validate(settings.Database.Trim());
        if (nameError is not null)
        {
            return new DatabaseCheckResult(DatabaseCheckStatus.InvalidName, nameError);
        }

        if (string.IsNullOrWhiteSpace(settings.Server))
        {
            return new DatabaseCheckResult(DatabaseCheckStatus.ServerUnreachable, "Enter the SQL Server name, for example localhost or sqlhost\\SQLEXPRESS.");
        }

        var database = settings.Database.Trim();
        try
        {
            await using (var master = new SqlConnection(BuildConnectionString(settings, "master")))
            {
                await master.OpenAsync(ct);
                await using var exists = new SqlCommand("SELECT DB_ID(@name)", master);
                exists.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = database });
                if (await exists.ExecuteScalarAsync(ct) is null or DBNull)
                {
                    return new DatabaseCheckResult(DatabaseCheckStatus.DatabaseMissing,
                        $"Connected to {settings.Server.Trim()}. The database {database} doesn't exist yet.");
                }
            }
        }
        catch (SqlException ex)
        {
            return Describe(ex, settings);
        }

        try
        {
            await using var conn = new SqlConnection(BuildConnectionString(settings));
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(
                "SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0), " +
                "CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL AND OBJECT_ID(N'dbo.DatabaseInstances') IS NOT NULL THEN 1 ELSE 0 END", conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            var tableCount = reader.GetInt32(0);
            var isDbTracker = reader.GetInt32(1) == 1;

            if (tableCount == 0)
            {
                return new DatabaseCheckResult(DatabaseCheckStatus.DatabaseEmpty, $"Connected. {database} is empty and ready for DBTracker's tables.");
            }

            return isDbTracker
                ? new DatabaseCheckResult(DatabaseCheckStatus.DatabaseHasDBTracker,
                    $"Connected. {database} already holds DBTracker data. Setup will keep it and only add tables from newer versions.")
                : new DatabaseCheckResult(DatabaseCheckStatus.DatabaseHasOtherTables,
                    $"{database} already contains tables from another application. Choose an empty database or a new name.");
        }
        catch (SqlException ex) when (ex.Number == 4060)
        {
            return new DatabaseCheckResult(DatabaseCheckStatus.NoAccessToDatabase,
                $"The login can reach the server but can't open {database}. Give it db_owner on that database, then test again.");
        }
        catch (SqlException ex)
        {
            return Describe(ex, settings);
        }
    }

    /// <summary>Returns null on success, otherwise a message to show.</summary>
    public async Task<string?> CreateDatabaseAsync(InstallDatabaseSettings settings, CancellationToken ct = default)
    {
        var database = settings.Database.Trim();
        var nameError = DatabaseNameValidator.Validate(database);
        if (nameError is not null)
        {
            return nameError;
        }

        try
        {
            await using var master = new SqlConnection(BuildConnectionString(settings, "master"));
            await master.OpenAsync(ct);
            await using var cmd = new SqlCommand("DECLARE @sql nvarchar(300) = N'CREATE DATABASE ' + QUOTENAME(@name); EXEC (@sql);", master);
            cmd.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = database });
            await cmd.ExecuteNonQueryAsync(ct);
            return null;
        }
        catch (SqlException ex) when (ex.Number == 1801)
        {
            return null;
        }
        catch (SqlException ex) when (ex.Number == 262)
        {
            return $"This login isn't allowed to create databases. Ask a DBA to create {database} and give the login db_owner on it, then test the connection again.";
        }
        catch (SqlException ex)
        {
            return Describe(ex, settings).Message;
        }
    }

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(string connectionString, CancellationToken ct = default)
    {
        await using var db = CreateContext(connectionString);
        return (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
    }

    public async Task ApplyMigrationAsync(string connectionString, string migration, CancellationToken ct = default)
    {
        await using var db = CreateContext(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(migration, ct);
    }

    /// <summary>For deployments configured before the wizard existed: does this database already have an Admin?</summary>
    public static async Task<InstallProbeResult> ProbeForAdminAsync(string connectionString, CancellationToken ct = default)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(
                "IF OBJECT_ID(N'dbo.AspNetUserRoles') IS NULL OR OBJECT_ID(N'dbo.AspNetRoles') IS NULL SELECT 0 " +
                "ELSE SELECT COUNT(*) FROM dbo.AspNetUserRoles ur JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId WHERE r.NormalizedName = N'ADMIN'", conn);
            var admins = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            return admins > 0 ? InstallProbeResult.AdminExists : InstallProbeResult.NoAdminOrDatabase;
        }
        catch (SqlException ex) when (ex.Number == 4060)
        {
            return InstallProbeResult.NoAdminOrDatabase;
        }
        catch (Exception)
        {
            return InstallProbeResult.Unreachable;
        }
    }

    private static DBTrackerDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<DBTrackerDbContext>().UseSqlServer(connectionString).Options);

    private static DatabaseCheckResult Describe(SqlException ex, InstallDatabaseSettings settings)
    {
        var server = settings.Server.Trim();
        if (ex.Number == 18456)
        {
            return new DatabaseCheckResult(DatabaseCheckStatus.LoginFailed, settings.UseWindowsAuthentication
                ? $"SQL Server rejected the Windows login {BackupShareChecker.CurrentIdentityName()}. Give that account a login on {server}, then test again."
                : "SQL Server rejected the user name or password.");
        }

        if (ex.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseCheckResult(DatabaseCheckStatus.CertificateNotTrusted,
                $"{server} uses a certificate this machine doesn't trust. Tick Trust server certificate if it's self-signed, or install a trusted certificate on the server.");
        }

        return new DatabaseCheckResult(DatabaseCheckStatus.ServerUnreachable,
            $"Couldn't reach SQL Server at {server} (SQL error {ex.Number}). Check the server name, that SQL Server is running and that it accepts TCP connections.");
    }
}
