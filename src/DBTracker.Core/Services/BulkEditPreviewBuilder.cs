using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>Which fields a bulk edit changes — null/false means "leave this field alone" (U17). Archiving is deliberately not an option here; that stays a separate, individually-confirmed action (U14).</summary>
public record BulkEditRequest(bool SetOwner, string? Company, string? Department, string? ContactName, string? ContactEmail, bool SetTags, string? Tags, bool SetRetention, int? RetentionPolicyDays);

public record BulkEditFieldDiff(string Field, string Before, string After);
public record BulkEditRowPreview(int InstanceId, string InstanceName, IReadOnlyList<BulkEditFieldDiff> Changes);

/// <summary>
/// Builds the before/after diff preview for a bulk owner/tag/retention edit across many instances
/// (U17) — pure (no DB writes); the caller applies the same request after the user confirms.
/// </summary>
public static class BulkEditPreviewBuilder
{
    public static IReadOnlyList<BulkEditRowPreview> Preview(IReadOnlyList<DatabaseInstance> instances, BulkEditRequest request)
    {
        var rows = new List<BulkEditRowPreview>();
        foreach (var instance in instances)
        {
            var diffs = new List<BulkEditFieldDiff>();

            if (request.SetOwner)
            {
                AddIfChanged(diffs, "Company", instance.Company, request.Company ?? "");
                AddIfChanged(diffs, "Department", instance.Department, request.Department ?? "");
                AddIfChanged(diffs, "ContactName", instance.ContactName, request.ContactName ?? "");
                AddIfChanged(diffs, "ContactEmail", instance.ContactEmail, request.ContactEmail ?? "");
            }

            if (request.SetTags)
            {
                AddIfChanged(diffs, "Tags", instance.Tags, request.Tags ?? "");
            }

            if (request.SetRetention)
            {
                AddIfChanged(diffs, "RetentionPolicyDays", instance.RetentionPolicyDays?.ToString() ?? "", request.RetentionPolicyDays?.ToString() ?? "");
            }

            if (diffs.Count > 0)
            {
                rows.Add(new BulkEditRowPreview(instance.Id, instance.Name, diffs));
            }
        }

        return rows;
    }

    /// <summary>Applies the request to an in-memory instance — the caller is responsible for persisting it. Separate from Preview so "what would change" and "what actually changed" can never drift.</summary>
    public static void Apply(DatabaseInstance instance, BulkEditRequest request)
    {
        if (request.SetOwner)
        {
            instance.Company = request.Company ?? "";
            instance.Department = request.Department ?? "";
            instance.ContactName = request.ContactName ?? "";
            instance.ContactEmail = request.ContactEmail ?? "";
        }
        if (request.SetTags)
        {
            instance.Tags = request.Tags ?? "";
        }
        if (request.SetRetention)
        {
            instance.RetentionPolicyDays = request.RetentionPolicyDays;
        }
    }

    private static void AddIfChanged(List<BulkEditFieldDiff> diffs, string field, string before, string after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            diffs.Add(new BulkEditFieldDiff(field, before, after));
        }
    }
}
