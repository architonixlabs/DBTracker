using DBTracker.Infrastructure;
using DBTracker.Infrastructure.Queries;
using DBTracker.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DBTracker")
    ?? throw new InvalidOperationException("Connection string 'DBTracker' not configured.");

builder.Services.AddDbContextFactory<DBTrackerDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<InstanceOverviewService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DBTrackerDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.Migrate();
    if (app.Environment.IsDevelopment())
    {
        await SeedData.SeedAsync(db);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
