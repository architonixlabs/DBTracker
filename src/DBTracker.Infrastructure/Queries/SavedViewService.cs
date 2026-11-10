using DBTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Queries;

/// <summary>Per-user saved filter/sort views (U05) for the Instances list and Reports page.</summary>
public class SavedViewService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task<List<SavedView>> ListAsync(string userId, string viewType)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.SavedViews
            .AsNoTracking()
            .Where(v => v.UserId == userId && v.ViewType == viewType)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    /// <summary>Returns false (no-op) if the user already has a view with this name for this page.</summary>
    public async Task<bool> SaveAsync(string userId, string viewType, string name, string queryString)
    {
        using var db = await factory.CreateDbContextAsync();
        var exists = await db.SavedViews.AnyAsync(v => v.UserId == userId && v.ViewType == viewType && v.Name == name);
        if (exists)
        {
            return false;
        }

        db.SavedViews.Add(new SavedView { UserId = userId, ViewType = viewType, Name = name, QueryString = queryString });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task RenameAsync(int id, string userId, string newName)
    {
        using var db = await factory.CreateDbContextAsync();
        var view = await db.SavedViews.FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);
        if (view is not null)
        {
            view.Name = newName;
            view.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int id, string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        await db.SavedViews.Where(v => v.Id == id && v.UserId == userId).ExecuteDeleteAsync();
    }
}
