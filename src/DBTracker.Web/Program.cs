using DBTracker.Infrastructure;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Diagnostics;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Lifecycle;
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

// ASP.NET Core only auto-loads Static Web Assets (how framework/RCL files like blazor.web.js are
// served from unpublished build output) in the Development environment. Any other environment
// name running unpublished output — e.g. this app's "Testing" environment, used for local E2E runs
// — otherwise 500s on every framework asset with a FileNotFoundException. A real `dotnet publish`
// copies these physically into wwwroot and doesn't need this.
if (!builder.Environment.IsProduction())
{
    builder.WebHost.UseStaticWebAssets();
}

var connectionString = builder.Configuration.GetConnectionString("DBTracker")
    ?? throw new InvalidOperationException("Connection string 'DBTracker' not configured.");

// A single registration path for DbContextOptions<DBTrackerDbContext>: the factory configures it
// once, and the scoped context everything injects directly is created FROM that factory rather
// than via a separate AddDbContext call (which would double-register the options and conflict).
// NotificationBell renders on every page alongside that page's own ambient scoped context, so
// NotificationService needs its own short-lived context via the factory to avoid "a second
// operation was started on this context instance" — two components querying the same DbContext
// concurrently, which is exactly the scenario the factory pattern exists for in Blazor Server.
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

// U19: append-only general audit trail, shared by instance lifecycle, account lifecycle, CSV
// import/bulk edit and maintenance windows.
builder.Services.AddScoped<AuditRecorder>();
builder.Services.AddScoped<InstanceArchiveService>();
builder.Services.AddScoped<InstanceBulkEditService>();
builder.Services.AddScoped<AccountLifecycleService>();

// U21: owner teams/memberships, reusable backup policies, and contact-correction requests.
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<BackupPolicyService>();
builder.Services.AddScoped<ContactCorrectionService>();

// Epic 18 (D01-D06): database identity, releases and support lifecycle.
builder.Services.AddScoped<VersionIdentityService>();
builder.Services.AddScoped<ReleaseCatalogService>();
builder.Services.AddScoped<SupportRunwayService>();
builder.Services.AddScoped<PatchBaselineService>();
builder.Services.AddScoped<AdvisoryService>();
builder.Services.AddScoped<VersionObservationService>();
builder.Services.AddHostedService<SupportRunwayCheckHostedService>();

// Process-lifetime record of each background worker's last/next run (U15 setup/diagnostics page).
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

// Generic outbound webhook (Slack/Teams/PagerDuty-style, via a plain JSON HTTP POST) alongside
// email for Overdue/Failed alerts — disabled (NullWebhookSender) unless a URL is configured.
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

// No global FallbackPolicy: every @page component declares its own [Authorize] (optionally
// Roles=…) or [AllowAnonymous], and every minimal-API endpoint calls .RequireAuthorization() or
// .AllowAnonymous() explicitly (see DeviceLinkEndpointRouteBuilderExtensions,
// IdentityComponentsEndpointRouteBuilderExtensions). A FallbackPolicy requiring authentication
// used to also catch the interactive-render-mode's own transport endpoints (/_blazor/negotiate,
// /_blazor/initializers, the SignalR hub) — which have no authorization metadata of their own and
// must stay reachable by an anonymous visitor — redirecting them to the login page instead of
// returning the JSON/handshake the Blazor JS runtime expects. That silently broke the interactive
// circuit on any genuinely anonymous page relying on normal C# event dispatch rather than a
// named-form SSR post-back (the way Login.razor's form happens to be built): the browser fell back
// to a real, un-enhanced POST with no antiforgery token, which UseAntiforgery() then rejected with
// a 400. Found building U16's Forgot/Reset password pages — the first anonymous pages in this app
// to rely on interactive OnValidSubmit. Making every page's authorization explicit (rather than
// relying on an implicit catch-all) removes the footgun without weakening any existing RBAC check.
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

// HSTS tells the browser to force https for this host for future requests — including upgrading
// the Blazor Server SignalR websocket to wss://. That silently breaks interactivity wherever this
// app is served over plain http without a TLS-terminating reverse proxy in front of it (e.g. local
// Testing-environment E2E runs today, and possibly early on-prem deployment). Scope it to
// Production, where TLS is expected to be in place.
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
