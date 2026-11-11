using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Imports vendor security advisories. Rows match a stored advisory by Engine, Family and VendorAdvisoryId
/// (ignoring case), so a repeat import updates instead of duplicating. Importing never classifies an instance; that still
/// happens per instance, and reviewed assessments are never changed by an import.</summary>
public static class AdvisoryCsvImporter
{
    public static readonly string[] Columns =
    [
        "Engine", "Family", "VendorAdvisoryId", "Title", "Description", "Severity", "AffectedMinBuild", "AffectedMaxBuild",
        "AffectedConfiguration", "FixedBuild", "ProvenanceUrl", "PublishedAt"
    ];

    public static IReadOnlyList<ImportColumnHelp> ColumnGuide { get; } =
    [
        new("Engine", true, string.Join(" | ", Enum.GetNames<DatabaseEngine>())),
        new("Family", true, "Product family as versions resolve to it, e.g. MySQL."),
        new("VendorAdvisoryId", true, "Vendor or CVE identifier. With Engine and Family it identifies the row on re-import."),
        new("Title", false, "Short title."),
        new("Description", false, "Free text. Quote the cell to include commas or line breaks."),
        new("Severity", false, string.Join(" | ", Enum.GetNames<AdvisorySeverity>()) + ". Default Unknown, which is ranked like High."),
        new("AffectedMinBuild", false, "Lowest affected build, inclusive. Empty means any build up to the maximum."),
        new("AffectedMaxBuild", false, "Highest affected build, inclusive."),
        new("AffectedConfiguration", false, "Configuration the advisory needs, e.g. a plugin or feature that must be enabled."),
        new("FixedBuild", false, "First build with the fix."),
        new("ProvenanceUrl", false, "http:// or https:// link to the vendor advisory."),
        new("PublishedAt", false, "yyyy-MM-dd."),
    ];

    public static string TemplateCsv() => LifecycleCsvSupport.BuildTemplate(Columns,
        "Lines starting with # are ignored. Engine, Family and VendorAdvisoryId are required; leave other cells empty if unknown.",
        "MySql,MySQL,CVE-2025-0001,Example privilege escalation,Server component issue,High,8.0.0,8.0.40,,8.0.41,https://www.oracle.com/security-alerts/,2026-10-21",
        ColumnGuide);

    public static string Key(DatabaseEngine engine, string family, string vendorId) =>
        $"{engine}|{family.Trim().ToUpperInvariant()}|{vendorId.Trim().ToUpperInvariant()}";

    public static LifecycleImportPreview<AdvisoryRecord> ParseForImport(string csvContent, IReadOnlyCollection<AdvisoryRecord> existingAdvisories)
    {
        var rows = new List<LifecycleImportRow<AdvisoryRecord>>();
        var errors = new List<ImportRowError>();

        var table = LifecycleCsvTable.Read(csvContent, Columns);
        if (table is null)
        {
            return new LifecycleImportPreview<AdvisoryRecord>(rows, errors);
        }
        if (!table.Has("Engine") || !table.Has("Family") || !table.Has("VendorAdvisoryId"))
        {
            errors.Add(new ImportRowError(1, "Header row must include Engine, Family and VendorAdvisoryId columns."));
            return new LifecycleImportPreview<AdvisoryRecord>(rows, errors);
        }

        var existingByKey = existingAdvisories
            .GroupBy(a => Key(a.Engine, a.Family, a.VendorAdvisoryId))
            .ToDictionary(g => g.Key, g => g.ToList());
        var seen = new HashSet<string>();

        foreach (var (rowNumber, cells) in table.DataRows)
        {
            var rowErrors = new List<string>();
            var engineRaw = table.Get(cells, "Engine");
            if (!Enum.TryParse<DatabaseEngine>(engineRaw, true, out var engine) || int.TryParse(engineRaw, out _))
            {
                errors.Add(new ImportRowError(rowNumber, $"Unrecognized engine '{engineRaw}'. Expected one of: {string.Join(", ", Enum.GetNames<DatabaseEngine>())}."));
                continue;
            }
            var family = table.Get(cells, "Family");
            var vendorId = table.Get(cells, "VendorAdvisoryId");
            if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(vendorId))
            {
                errors.Add(new ImportRowError(rowNumber, "Family and VendorAdvisoryId are required."));
                continue;
            }

            var key = Key(engine, family, vendorId);
            existingByKey.TryGetValue(key, out var matches);
            var existing = matches is { Count: 1 } ? matches[0] : null;

            var item = new AdvisoryRecord { Engine = engine, Family = family, VendorAdvisoryId = vendorId };
            if (existing is not null)
            {
                CopyImportedFields(existing, item);
                item.Id = existing.Id;
            }

            item.Title = table.GetOrKeep(cells, "Title", item.Title);
            item.Description = table.GetOrKeep(cells, "Description", item.Description);
            item.Severity = ReleaseCatalogCsvImporter.OptionalEnum(table, cells, "Severity", item.Severity, rowErrors);
            item.AffectedMinBuild = table.GetOrKeep(cells, "AffectedMinBuild", item.AffectedMinBuild);
            item.AffectedMaxBuild = table.GetOrKeep(cells, "AffectedMaxBuild", item.AffectedMaxBuild);
            item.AffectedConfiguration = table.GetOrKeep(cells, "AffectedConfiguration", item.AffectedConfiguration);
            item.FixedBuild = table.GetOrKeep(cells, "FixedBuild", item.FixedBuild);
            item.ProvenanceUrl = table.GetOrKeep(cells, "ProvenanceUrl", item.ProvenanceUrl);
            item.PublishedAtUtc = ReleaseCatalogCsvImporter.OptionalDate(table, cells, "PublishedAt", item.PublishedAtUtc, rowErrors);

            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "Family", item.Family, 100);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "VendorAdvisoryId", item.VendorAdvisoryId, 100);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "Title", item.Title, 300);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "Description", item.Description, 2000);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "AffectedMinBuild", item.AffectedMinBuild, 100);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "AffectedMaxBuild", item.AffectedMaxBuild, 100);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "AffectedConfiguration", item.AffectedConfiguration, 500);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "FixedBuild", item.FixedBuild, 100);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "ProvenanceUrl", item.ProvenanceUrl, 500);
            if (!LifecycleCsvSupport.IsHttpUrlOrEmpty(item.ProvenanceUrl))
            {
                rowErrors.Add("ProvenanceUrl must be an http:// or https:// link.");
            }
            if (!string.IsNullOrWhiteSpace(item.AffectedMinBuild) && !string.IsNullOrWhiteSpace(item.AffectedMaxBuild) &&
                VersionBuildComparer.Compare(item.AffectedMinBuild, item.AffectedMaxBuild) > 0)
            {
                rowErrors.Add("AffectedMinBuild is higher than AffectedMaxBuild.");
            }

            if (rowErrors.Count > 0)
            {
                errors.Add(new ImportRowError(rowNumber, string.Join(" ", rowErrors)));
                continue;
            }

            var display = $"{vendorId} ({engine} / {family})";
            if (!seen.Add(key))
            {
                rows.Add(new(rowNumber, ImportRowPlan.Ambiguous, null, null, display, "This advisory appears more than once in this file."));
                continue;
            }
            if (matches is { Count: > 1 })
            {
                rows.Add(new(rowNumber, ImportRowPlan.Ambiguous, null, null, display, $"{matches.Count} stored advisories already use this ID for this family."));
                continue;
            }

            if (existing is null)
            {
                rows.Add(new(rowNumber, ImportRowPlan.Create, item, null, display, null));
                continue;
            }

            var unchanged = existing.Title == item.Title && existing.Description == item.Description && existing.Severity == item.Severity &&
                            existing.AffectedMinBuild == item.AffectedMinBuild && existing.AffectedMaxBuild == item.AffectedMaxBuild &&
                            existing.AffectedConfiguration == item.AffectedConfiguration && existing.FixedBuild == item.FixedBuild &&
                            existing.ProvenanceUrl == item.ProvenanceUrl && existing.PublishedAtUtc == item.PublishedAtUtc;
            rows.Add(new(rowNumber, unchanged ? ImportRowPlan.Skip : ImportRowPlan.Update, item, existing.Id, display,
                unchanged ? null : "Updates the stored advisory. Reviewed assessments keep their classification; re-evaluate instances to refresh the others."));
        }

        return new LifecycleImportPreview<AdvisoryRecord>(rows, errors);
    }

    public static void CopyImportedFields(AdvisoryRecord from, AdvisoryRecord to)
    {
        to.Engine = from.Engine;
        to.Family = from.Family;
        to.VendorAdvisoryId = from.VendorAdvisoryId;
        to.Title = from.Title;
        to.Description = from.Description;
        to.Severity = from.Severity;
        to.AffectedMinBuild = from.AffectedMinBuild;
        to.AffectedMaxBuild = from.AffectedMaxBuild;
        to.AffectedConfiguration = from.AffectedConfiguration;
        to.FixedBuild = from.FixedBuild;
        to.ProvenanceUrl = from.ProvenanceUrl;
        to.PublishedAtUtc = from.PublishedAtUtc;
    }
}
