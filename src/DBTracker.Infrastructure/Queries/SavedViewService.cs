using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Queries;

/// <summary>Per-user saved views for the Instances list and Reports page.</summary>
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

    /// <summary>Returns false (no-op) if the name is invalid or the user already has a view with this name for this page.</summary>
    public async Task<bool> SaveAsync(string userId, string viewType, string name, string queryString)
    {
        if (SavedViewNameRules.Validate(name) is not null)
        {
            return false;
        }

        name = SavedViewNameRules.Normalize(name);
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

    /// <summary>Renames one of the user's own views. Returns an error message instead of saving when the name is invalid or already used.</summary>
    public async Task<string?> RenameAsync(int id, string userId, string newName)
    {
        var error = SavedViewNameRules.Validate(newName);
        if (error is not null)
        {
            return error;
        }

        var name = SavedViewNameRules.Normalize(newName);
        using var db = await factory.CreateDbContextAsync();
        var view = await db.SavedViews.FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);
        if (view is null)
        {
            return "That view no longer exists.";
        }
        if (view.Name == name)
        {
            return null;
        }
        if (await db.SavedViews.AnyAsync(v => v.UserId == userId && v.ViewType == view.ViewType && v.Name == name && v.Id != id))
        {
            return "You already have a saved view with that name.";
        }

        view.Name = name;
        view.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task DeleteAsync(int id, string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        await db.SavedViews.Where(v => v.Id == id && v.UserId == userId).ExecuteDeleteAsync();
    }
}
