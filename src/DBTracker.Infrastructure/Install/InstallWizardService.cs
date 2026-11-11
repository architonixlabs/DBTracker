using DBTracker.Core.Services.Install;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Notifications;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DBTracker.Infrastructure.Install;

public sealed record InstallFinishRequest(
    InstallDatabaseSettings Database,
    bool LoadSampleData,
    string AdminEmail,
    string AdminDisplayName,
    string AdminPassword,
    InstallEmailSettings Email,
    string? ReportsDistributionList,
    string? BackupShareChecked);

public sealed record InstallFinishResult(bool Succeeded, string? Error, bool AdminProblem = false)
{
    public static readonly InstallFinishResult Ok = new(true, null);
}

/// <summary>Everything the wizard does that outlives a single step: test email, password rules, and finishing setup.</summary>
public sealed class InstallWizardService(
    InstallState state,
    SetupKeyStore keyStore,
    InstallDataPaths paths,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IDataProtectionProvider dataProtection,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    ILogger<InstallWizardService> logger)
{
    private readonly SemaphoreSlim _finishGate = new(1, 1);

    public async Task<EmailSendResult> SendTestEmailAsync(InstallEmailSettings email, string toAddress, CancellationToken ct = default)
    {
        IEmailSender sender = email.Provider switch
        {
            EmailProviders.ArxMail => new ArxMailEmailSender(
                httpClientFactory.CreateClient(nameof(InstallWizardService)),
                Options.Create(new ArxMailOptions { Endpoint = email.ArxMailEndpoint, FromAddress = email.ArxMailFromAddress, ApiKey = email.ArxMailApiKey }),
                loggerFactory.CreateLogger<ArxMailEmailSender>()),
            EmailProviders.Smtp => new SmtpEmailSender(
                Options.Create(new SmtpOptions
                {
                    Host = email.SmtpHost, Port = email.SmtpPort, EnableSsl = email.SmtpEnableSsl, Username = email.SmtpUsername,
                    Password = email.SmtpPassword, FromAddress = email.SmtpFromAddress, FromName = email.SmtpFromName,
                }),
                loggerFactory.CreateLogger<SmtpEmailSender>()),
            _ => new NullEmailSender(loggerFactory.CreateLogger<NullEmailSender>()),
        };

        return await sender.SendAsync(toAddress, "DBTracker test email",
            "This is a test message from the DBTracker setup wizard. If you can read it, email delivery works.", ct);
    }

    public async Task<IReadOnlyList<string>> ValidateAdminPasswordAsync(string email, string password)
    {
        using var scope = scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email };
        var errors = new List<string>();
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, password);
            errors.AddRange(result.Errors.Select(e => e.Description));
        }
        return errors;
    }

    public async Task<InstallFinishResult> FinishAsync(InstallFinishRequest request, CancellationToken ct = default)
    {
        await _finishGate.WaitAsync(ct);
        try
        {
            if (state.IsInstalled)
            {
                return new InstallFinishResult(false, "Setup has already been completed.");
            }

            var connectionString = InstallDatabaseService.BuildConnectionString(request.Database);
            state.UsePendingConnectionString(connectionString);

            ApplicationUser admin;
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DBTrackerDbContext>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                if (request.LoadSampleData)
                {
                    await SeedData.SeedAsync(db);
                    await IdentitySeedData.SeedAsync(roleManager, userManager);
                }
                else
                {
                    foreach (var role in Roles.All)
                    {
                        if (!await roleManager.RoleExistsAsync(role))
                        {
                            await roleManager.CreateAsync(new IdentityRole(role));
                        }
                    }
                }

                var adminResult = await EnsureAdminAsync(userManager, request);
                if (adminResult.User is null)
                {
                    return new InstallFinishResult(false, adminResult.Error, AdminProblem: true);
                }
                admin = adminResult.User;
            }

            var completedAt = DateTime.UtcNow;
            InstallConfigWriter.Write(paths.ConfigFile, new InstallConfiguration(
                connectionString,
                request.Database.HasSecret,
                request.Email,
                request.ReportsDistributionList,
                completedAt,
                admin.Email!), dataProtection.CreateProtector(InstallConfigWriter.ProtectorPurpose));

            if (configuration is IConfigurationRoot root)
            {
                root.Reload();
            }

            if (configuration.GetConnectionString("DBTracker") != connectionString)
            {
                return new InstallFinishResult(false,
                    $"The settings were saved to {paths.ConfigFile}, but the app couldn't read them back. Check that the app pool identity can read that folder.");
            }

            using (var scope = scopeFactory.CreateScope())
            {
                var audit = scope.ServiceProvider.GetRequiredService<AuditRecorder>();
                await audit.RecordUserActionAsync(admin.Id, admin.Email!, "Completed installation", "Installation", "install",
                    $"{request.Database.Server.Trim()} / {request.Database.Database.Trim()}",
                    $"Sample data: {(request.LoadSampleData ? "loaded" : "not loaded")}. Email: {request.Email.Provider}. " +
                    $"Backup share checked: {(string.IsNullOrWhiteSpace(request.BackupShareChecked) ? "no" : request.BackupShareChecked)}.",
                    ct: ct);
            }

            keyStore.Delete();
            state.MarkInstalled();
            logger.LogInformation("DBTracker setup completed by {Admin}. Settings saved to {ConfigFile}.", admin.Email, paths.ConfigFile);
            return InstallFinishResult.Ok;
        }
        finally
        {
            _finishGate.Release();
        }
    }

    private static async Task<(ApplicationUser? User, string? Error)> EnsureAdminAsync(UserManager<ApplicationUser> userManager, InstallFinishRequest request)
    {
        var email = request.AdminEmail.Trim();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // A retry after a failed finish finds the admin it already created; anything else is a clash.
            if (await userManager.IsInRoleAsync(existing, Roles.Admin) && await userManager.CheckPasswordAsync(existing, request.AdminPassword))
            {
                return (existing, null);
            }
            return (null, $"An account for {email} already exists in this database. Use a different email for the Administrator.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(request.AdminDisplayName) ? email : request.AdminDisplayName.Trim(),
            IsActive = true,
        };

        var created = await userManager.CreateAsync(user, request.AdminPassword);
        if (!created.Succeeded)
        {
            return (null, string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, Roles.Admin);
        return (user, null);
    }
}
