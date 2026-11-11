using System.Text.Json;
using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>Before/after JSON for audit events, built from the same field list as the instance change log.</summary>
public static class AuditDiffJson
{
    /// <summary>Only the changed fields: {"Field":"old"} and {"Field":"new"}. Both null when nothing changed.</summary>
    public static (string? Before, string? After) FromChanges(IEnumerable<InstanceChangeLog> changes)
    {
        var list = changes.ToList();
        if (list.Count == 0)
        {
            return (null, null);
        }

        var before = list.ToDictionary(c => c.FieldName, c => c.OldValue);
        var after = list.ToDictionary(c => c.FieldName, c => c.NewValue);
        return (JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
    }

    /// <summary>The fields a new instance was created with, leaving out empty ones so the entry stays readable.</summary>
    public static string ForCreate(DatabaseInstance created) =>
        JsonSerializer.Serialize(InstanceChangeLogBuilder.Fields(created)
            .Where(f => f.Value.Length > 0)
            .ToDictionary(f => f.Name, f => f.Value));

    /// <summary>Flattens a stored before/after JSON object into "Field: value" lines for the audit page. Non-object JSON is shown as is.</summary>
    public static IReadOnlyList<(string Field, string Value)> Describe(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [("", json)];
            }

            return doc.RootElement.EnumerateObject()
                .Select(p => (p.Name, p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString() ?? "",
                    JsonValueKind.Null => "",
                    _ => p.Value.GetRawText()
                }))
                .ToList();
        }
        catch (JsonException)
        {
            return [("", json)];
        }
    }

    /// <summary>Rows of a before/after pair, one per field in either side, for a side-by-side view.</summary>
    public static IReadOnlyList<(string Field, string Before, string After)> Pair(string? beforeJson, string? afterJson)
    {
        var before = Describe(beforeJson).ToDictionary(f => f.Field, f => f.Value);
        var after = Describe(afterJson).ToDictionary(f => f.Field, f => f.Value);
        return before.Keys.Concat(after.Keys).Distinct()
            .Select(k => (k, before.GetValueOrDefault(k, ""), after.GetValueOrDefault(k, "")))
            .ToList();
    }
}
