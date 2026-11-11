using DBTracker.Infrastructure;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.ChangeManagement;
using DBTracker.Infrastructure.Diagnostics;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Lifecycle;
using DBTracker.Infrastructure.MigrationPlanning;
using DBTracker.Infrastructure.Notifications;
using DBTracker.Infrastructure.Ownership;
using DBTracker.Infrastructure.Queries;
using DBTracker.Infrastructure.Scanning;
using DBTracker.Web.Components;
using DBTracker.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Static Web Assets only auto-load in Development; unpublished runs in other environments (e.g. Testing) would 500 on every framework asset.
if (!builder.Environment.IsProduction())
{
    builder.WebHost.UseStaticWebAssets();
}

var connectionString = builder.Configuration.GetConnectionString("DBTracker")
    ?? throw new InvalidOperationException("Connection string 'DBTracker' not configured.");

// One registration path for the options: the scoped context is created from the factory, since AddDbContext too would conflict.
// NotificationService uses the factory directly because NotificationBell queries concurrently with each page's scoped context.
builder.Services.AddDbContextFactory<DBTrackerDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<DBTrackerDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<DBTrackerDbContext>>().CreateDbContext());

builder.Services.AddScoped<InstanceOverviewService>();
builder.Services.AddScoped<SavedViewService>();
builder.Services.AddScoped<DBTracker.Web.Services.ToastService>();
builder.Services.AddScoped<BackupFileScanner>();
builder.Services.AddHostedService<BackupScanHostedService>();

builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<NotificationSubscriptionService>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddHostedService<AlertCheckHostedService>();
builder.Services.AddScoped<ComplianceReportService>();
builder.Services.AddHostedService<ReportHostedService>();
builder.Services.AddScoped<DeviceLinkService>();
builder.Services.AddScoped<DBTracker.Infrastructure.Scanning.ConnectivityChecker>();

builder.Services.AddScoped<AuditRecorder>();
builder.Services.AddScoped<InstanceArchiveService>();
builder.Services.AddScoped<InstanceBulkEditService>();
builder.Services.AddScoped<AccountLifecycleService>();

builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<BackupPolicyService>();
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
switch (builder.Configuration["Email:Provider"])
{
    case "Smtp":
        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
        break;
    case "ArxMail":
        builder.Services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<ArxMailEmailSender>());
        break;
    default:
        builder.Services.AddScoped<IEmailSender, NullEmailSender>();
        break;
}

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

// No global FallbackPolicy: every page and endpoint declares its authorization explicitly. A fallback also caught /_blazor transport
// endpoints, redirecting them to login and breaking the circuit on anonymous pages (e.g. Forgot/Reset password).
builder.Services.AddAuthorization();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<DBTrackerDbContext>()
    .AddSignInManager<AppSignInManager>()
    .AddDefaultTokenProviders();

builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DBTrackerDbContext>();
    db.Database.Migrate();

    if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Testing")
    {
        await SeedData.SeedAsync(db);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await IdentitySeedData.SeedAsync(roleManager, userManager);
    }
}

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
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapIdentityLogoutEndpoint();
app.MapDeviceLinkEndpoints();

app.Run();

public partial class Program;
