using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>One field in a save conflict: its value when the form opened, what the other person saved, and what this form holds.</summary>
public record ConcurrentFieldRow(string Field, string WhenOpened, string TheirValue, string YourValue)
{
    public bool TheyChanged => !string.Equals(WhenOpened, TheirValue, StringComparison.Ordinal);
    public bool YouChanged => !string.Equals(WhenOpened, YourValue, StringComparison.Ordinal);

    /// <summary>Both changed the field to different values: one of the two must lose.</summary>
    public bool Conflicts => TheyChanged && YouChanged && !string.Equals(TheirValue, YourValue, StringComparison.Ordinal);
}

public record ConcurrentEditComparison(IReadOnlyList<ConcurrentFieldRow> Rows)
{
    /// <summary>Fields either side changed. Untouched fields are left out so the review stays short.</summary>
    public IEnumerable<ConcurrentFieldRow> Changed => Rows.Where(r => r.TheyChanged || r.YouChanged);
    public bool HasConflicts => Rows.Any(r => r.Conflicts);
}

/// <summary>Compares a form's edits with a change someone else saved meanwhile, and merges them when they touch different fields.</summary>
public static class ConcurrentEditComparer
{
    /// <param name="whenOpened">Field values captured when the form loaded (from <see cref="InstanceChangeLogBuilder.Fields"/>).</param>
    public static ConcurrentEditComparison Compare(IReadOnlyList<(string Name, string Value)> whenOpened, DatabaseInstance theirs, DatabaseInstance yours)
    {
        var opened = whenOpened.ToDictionary(f => f.Name, f => f.Value, StringComparer.Ordinal);
        var their = InstanceChangeLogBuilder.Fields(theirs);
        var mine = InstanceChangeLogBuilder.Fields(yours);

        var rows = their.Zip(mine, (t, y) => new ConcurrentFieldRow(t.Name, opened.GetValueOrDefault(t.Name, ""), t.Value, y.Value)).ToList();
        return new ConcurrentEditComparison(rows);
    }

    /// <summary>Copies only the fields this form changed onto the latest saved instance, keeping everything the other person saved.
    /// Returns false (and changes nothing) when a field was changed by both sides to different values.</summary>
    public static bool TryMerge(ConcurrentEditComparison comparison, DatabaseInstance latest, DatabaseInstance yours)
    {
        if (comparison.HasConflicts)
        {
            return false;
        }

        foreach (var row in comparison.Rows.Where(r => r.YouChanged))
        {
            var property = typeof(DatabaseInstance).GetProperty(row.Field)
                ?? throw new InvalidOperationException($"No DatabaseInstance property named {row.Field}.");
            property.SetValue(latest, property.GetValue(yours));
        }

        return true;
    }
}
