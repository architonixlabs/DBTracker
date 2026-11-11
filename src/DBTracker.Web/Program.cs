using DBTracker.Web.Services;
using DBTracker.Infrastructure;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Install;
using DBTracker.Infrastructure.ChangeManagement;
using DBTracker.Infrastructure.Diagnostics;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Lifecycle;
using DBTracker.Infrastructure.MigrationPlanning;
using DBTracker.Infrastructure.Notifications;
using DBTracker.Infrastructure.Ownership;
using DBTracker.Infrastructure.Queries;
using DBTracker.Infrastructure.Scanning;
using DBTracker.Web.Api;
using DBTracker.Web.Components;
using DBTracker.Web.Components.Account;
using DBTracker.Web.Install;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Static Web Assets only auto-load in Development; unpublished runs elsewhere (Testing, a Production run from bin) would 500 on every
// framework asset. Published output has no runtime manifest, so this is a no-op there.
builder.WebHost.UseStaticWebAssets();

var installState = await builder.AddInstallSupportAsync();

// The factory sets each context's connection string from InstallState, so finishing the wizard repoints the app without a restart.
// The scoped context is created from the factory (registering AddDbContext too would conflict). NotificationService uses the factory
// directly because NotificationBell queries concurrently with each page's scoped context.
builder.Services.AddDbContextFactory<DBTrackerDbContext, InstallAwareDbContextFactory>(options => options.UseSqlServer());
builder.Services.AddScoped<DBTrackerDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<DBTrackerDbContext>>().CreateDbContext());

builder.Services.AddScoped<InstanceOverviewService>();
builder.Services.AddScoped<SavedViewService>();
builder.Services.AddScoped<DBTracker.Web.Services.ToastService>();
builder.Services.AddScoped<BackupFileScanner>();
builder.Services.AddHostedService<BackupScanHostedService>();

builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<NotificationSubscriptionService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Notifications.OwnerNoticeSender>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<NotificationPreferenceService>();
builder.Services.AddScoped<DeliveryAttemptService>();
builder.Services.AddHostedService<AlertCheckHostedService>();
builder.Services.AddScoped<ComplianceReportService>();
builder.Services.AddHostedService<ReportHostedService>();
builder.Services.AddScoped<DeviceLinkService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Scanning.ConnectivityChecker>();

builder.Services.AddScoped<AuditRecorder>();
builder.Services.AddScoped<InstanceArchiveService>();
builder.Services.AddScoped<InstanceBulkEditService>();
builder.Services.AddScoped<AccountLifecycleService>();
builder.Services.AddScoped<PersonalAccessTokenService>();
builder.Services.AddScoped<PasskeyService>();

builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<BackupPolicyService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Import.EvidenceHistoryImportService>();
builder.Services.AddScoped<DBTracker.Web.Services.ViewerTimeZone>();
builder.Services.AddScoped<ContactCorrectionService>();

builder.Services.AddScoped<VersionIdentityService>();
builder.Services.AddScoped<ReleaseCatalogService>();
builder.Services.AddScoped<SupportRunwayService>();
builder.Services.AddScoped<PatchBaselineService>();
builder.Services.AddScoped<AdvisoryService>();
builder.Services.AddScoped<VersionObservationService>();
builder.Services.AddHostedService<SupportRunwayCheckHostedService>();

builder.Services.AddScoped<DependencyService>();
builder.Services.AddScoped<PathAssessmentService>();
builder.Services.AddScoped<ReadinessChecklistService>();
builder.Services.AddScoped<CampaignCompletionGateService>();
builder.Services.AddScoped<CampaignService>();

builder.Services.AddScoped<RehearsalService>();
builder.Services.AddScoped<ChangeWindowService>();
builder.Services.AddScoped<ChangeValidationService>();
builder.Services.AddScoped<ReadinessExceptionService>();
builder.Services.AddHostedService<ReadinessExceptionExpiryCheckHostedService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Lifecycle.LifecycleWorkspaceService>();

builder.Services.AddScoped<DBTracker.Infrastructure.Recovery.RecoveryObjectiveService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Recovery.BackupCopyLocationService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Recovery.MaintenanceEvidenceService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Recovery.CapacityPlanningService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Recovery.CertificateDependencyService>();
builder.Services.AddHostedService<DBTracker.Infrastructure.Recovery.CertificateExpiryCheckHostedService>();

builder.Services.AddScoped<DBTracker.Infrastructure.Entitlement.SupportEntitlementService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Retirement.RetirementChecklistService>();

builder.Services.AddSingleton<WorkerHeartbeatStore>();
builder.Services.AddScoped<SetupDiagnosticsService>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Email:Smtp"));
builder.Services.Configure<ArxMailOptions>(builder.Configuration.GetSection("Email:ArxMail"));
builder.Services.AddHttpClient<ArxMailEmailSender>();
// Resolved per request so email settings saved by the install wizard take effect without a restart.
builder.Services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<IConfiguration>()["Email:Provider"] switch
{
    EmailProviders.Smtp => ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp),
    EmailProviders.ArxMail => sp.GetRequiredService<ArxMailEmailSender>(),
    _ => ActivatorUtilities.CreateInstance<NullEmailSender>(sp),
});

// Webhook alerts alongside email; NullWebhookSender unless a URL is configured.
builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection("Alerts:Webhook"));
builder.Services.AddHttpClient<HttpWebhookSender>();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Alerts:Webhook:Url"]))
{
    builder.Services.AddScoped<IWebhookSender>(sp => sp.GetRequiredService<HttpWebhookSender>());
}
else
{
    builder.Services.AddScoped<IWebhookSender, NullWebhookSender>();
}

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies(cookies =>
    {
        cookies.ApplicationCookie?.Configure(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });
    });

// Personal access tokens for /api/*; only endpoints that ask for this scheme's policy use it, so cookie sign-in is unaffected.
builder.Services.AddAuthentication()
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, AccessTokenAuthenticationHandler>(
        AccessTokenDefaults.Scheme, _ => { });

// No global FallbackPolicy: every page and endpoint declares its authorization explicitly. A fallback also caught /_blazor transport
// endpoints, redirecting them to login and breaking the circuit on anonymous pages (e.g. Forgot/Reset password).
builder.Services.AddAuthorization(options =>
    options.AddPolicy(AccessTokenDefaults.Policy, policy => policy
        .AddAuthenticationSchemes(AccessTokenDefaults.Scheme)
        .RequireAuthenticatedUser()));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireNonAlphanumeric = false;
        // G26: version 3 adds the AspNetUserPasskeys table used by Identity's passkey store.
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<DBTrackerDbContext>()
    .AddSignInManager<AppSignInManager>()
    .AddDefaultTokenProviders();

builder.Services.AddHttpContextAccessor();

// G26 passkeys. ServerDomain is the WebAuthn RP ID; left empty, Identity uses the request's host. Passkeys only work over HTTPS
// (or http://localhost), and changing the domain later strands every passkey registered under the old one. See DEPLOYMENT.md.
builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    var serverDomain = builder.Configuration["Passkeys:ServerDomain"];
    if (!string.IsNullOrWhiteSpace(serverDomain))
    {
        options.ServerDomain = serverDomain.Trim();
    }
    // Discoverable credentials, so a user can pick their passkey without typing an email first.
    options.ResidentKeyRequirement = "required";
    // A PIN, fingerprint or face check on every use, so a passkey stands in for the password rather than weakening it.
    options.UserVerificationRequirement = "required";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

await app.PrepareDatabaseAsync(installState);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// HSTS upgrades the SignalR websocket to wss://, which breaks interactivity when served over plain http (e.g. Testing E2E runs).
// Production only, where TLS is expected.
if (app.Environment.IsProduction())
{
    app.UseHsts();
}
// CSP with a per-request script nonce, plus the companion headers (SecurityHeaderPolicy). First, so error pages get them too.
app.UseSecurityHeaders();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// The tray polls these over loopback http and doesn't follow redirects; see HttpsRedirectExemption.
app.UseWhen(
    ctx => !DBTracker.Core.Services.HttpsRedirectExemption.Applies(ctx.Request.Path.Value,
        ctx.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip)),
    branch => branch.UseHttpsRedirection());

app.UseInstallGate(installState);

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapIdentityLogoutEndpoint();
app.MapDeviceLinkEndpoints();
app.MapPasskeyEndpoints();
app.MapTrayApi();
app.MapCspReports();

app.Run();

public partial class Program;
