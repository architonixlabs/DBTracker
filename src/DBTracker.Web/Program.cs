using DBTracker.Infrastructure;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Notifications;
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
builder.Services.AddScoped<DBTracker.Web.Services.ToastService>();
builder.Services.AddScoped<BackupFileScanner>();
builder.Services.AddHostedService<BackupScanHostedService>();

builder.Services.AddScoped<NotificationService>();
builder.Services.AddHostedService<AlertCheckHostedService>();
builder.Services.AddScoped<ComplianceReportService>();
builder.Services.AddHostedService<ReportHostedService>();
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

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<DBTrackerDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

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

app.Run();

public partial class Program;
