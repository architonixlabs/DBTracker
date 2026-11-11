using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Imports vendor release and support facts into the curated catalogue. Rows match a stored entry by Engine,
/// Family and ReleaseLabel (ignoring case), so re-importing the same file updates instead of duplicating. An entry under an
/// active manual override isn't overwritten: the override is internal policy and has to be reviewed first.</summary>
public static class ReleaseCatalogCsvImporter
{
    public static readonly string[] Columns =
    [
        "Engine", "Family", "ReleaseLabel", "Major", "Minor", "Build", "Channel", "PatchTrain", "Edition", "Distribution",
        "ApplicabilityNotes", "GaDate", "GaDatePrecision", "MainstreamSupportEnd", "MainstreamSupportEndPrecision",
        "ExtendedSupportEnd", "ExtendedSupportEndPrecision", "EsuSupportEnd", "EsuSupportEndPrecision",
        "DatesAreTbd", "DatesConflict", "DateTimezone", "SourceUrl", "SourceReference", "ReleaseNotesUrl", "RetrievedAt"
    ];

    private static readonly string Precisions = string.Join(" | ", Enum.GetNames<DatePrecision>()) + ". Default Exact.";

    public static IReadOnlyList<ImportColumnHelp> ColumnGuide { get; } =
    [
        new("Engine", true, string.Join(" | ", Enum.GetNames<DatabaseEngine>())),
        new("Family", true, "Product family as versions resolve to it, e.g. PostgreSQL, SQL Server, MySQL, MariaDB."),
        new("ReleaseLabel", true, "Name of the release, e.g. PostgreSQL 16. With Engine and Family it identifies the row on re-import."),
        new("Major", false, "Whole number."),
        new("Minor", false, "Whole number. Leave empty when support applies to the whole major version."),
        new("Build", false, "Build or patch number, e.g. 43 or 16.0.4135.4."),
        new("Channel", false, string.Join(" | ", Enum.GetNames<ReleaseChannel>()) + ". Default GA. Preview releases are never matched to instances."),
        new("PatchTrain", false, "Servicing track, e.g. CU, GDR, LTS or Innovation. Instances on another track don't match."),
        new("Edition", false, "Only instances with this edition match. Empty applies to every edition."),
        new("Distribution", false, "Only instances with this distribution match. Empty applies to every distribution."),
        new("ApplicabilityNotes", false, "When these facts apply, e.g. community builds only."),
        new("GaDate", false, "yyyy-MM-dd."),
        new("GaDatePrecision", false, Precisions),
        new("MainstreamSupportEnd", false, "yyyy-MM-dd."),
        new("MainstreamSupportEndPrecision", false, Precisions),
        new("ExtendedSupportEnd", false, "yyyy-MM-dd."),
        new("ExtendedSupportEndPrecision", false, Precisions),
        new("EsuSupportEnd", false, "yyyy-MM-dd. Counts for an instance only while it has an active Extended coverage contract."),
        new("EsuSupportEndPrecision", false, Precisions),
        new("DatesAreTbd", false, "true or false. True makes the runway Unknown."),
        new("DatesConflict", false, "true or false. True makes the runway Needs review."),
        new("DateTimezone", false, "Timezone the vendor publishes dates in. Default UTC."),
        new("SourceUrl", false, "http:// or https:// link to the vendor page."),
        new("SourceReference", false, "Document or page reference."),
        new("ReleaseNotesUrl", false, "http:// or https:// link to the release notes."),
        new("RetrievedAt", false, "yyyy-MM-dd. When the facts were read from the source."),
    ];

    public static string TemplateCsv() => LifecycleCsvSupport.BuildTemplate(Columns,
        "Lines starting with # are ignored. Engine, Family and ReleaseLabel are required; leave other cells empty if unknown.",
        "PostgreSql,PostgreSQL,PostgreSQL 16,16,,,GA,,,,Community releases,2023-09-14,Exact,2028-11-09,Exact,,,,,false,false,UTC,https://www.postgresql.org/support/versioning/,Versioning policy,https://www.postgresql.org/docs/release/16.0/,2026-11-01",
        ColumnGuide);

    public static string Key(DatabaseEngine engine, string family, string label) =>
        $"{engine}|{family.Trim().ToUpperInvariant()}|{label.Trim().ToUpperInvariant()}";

    public static LifecycleImportPreview<ReleaseCatalogEntry> ParseForImport(string csvContent,
        IReadOnlyCollection<ReleaseCatalogEntry> existingEntries, DateTime nowUtc)
    {
        var rows = new List<LifecycleImportRow<ReleaseCatalogEntry>>();
        var errors = new List<ImportRowError>();

        var table = LifecycleCsvTable.Read(csvContent, Columns);
        if (table is null)
        {
            return new LifecycleImportPreview<ReleaseCatalogEntry>(rows, errors);
        }
        if (!table.Has("Engine") || !table.Has("Family") || !table.Has("ReleaseLabel"))
        {
            errors.Add(new ImportRowError(1, "Header row must include Engine, Family and ReleaseLabel columns."));
            return new LifecycleImportPreview<ReleaseCatalogEntry>(rows, errors);
        }

        var existingByKey = existingEntries
            .GroupBy(e => Key(e.Engine, e.Family, e.ReleaseLabel))
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
            var label = table.Get(cells, "ReleaseLabel");
            if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(label))
            {
                errors.Add(new ImportRowError(rowNumber, "Family and ReleaseLabel are required."));
                continue;
            }

            var key = Key(engine, family, label);
            existingByKey.TryGetValue(key, out var matches);
            var existing = matches is { Count: 1 } ? matches[0] : null;

            var item = new ReleaseCatalogEntry { Engine = engine, Family = family, ReleaseLabel = label };
            if (existing is not null)
            {
                CopyImportedFields(existing, item);
                item.Id = existing.Id;
            }

            item.Major = OptionalInt(table, cells, "Major", item.Major, rowErrors);
            item.Minor = OptionalInt(table, cells, "Minor", item.Minor, rowErrors);
            item.Build = table.GetOrKeep(cells, "Build", item.Build);
            item.Channel = OptionalEnum(table, cells, "Channel", item.Channel, rowErrors);
            item.PatchTrain = table.GetOrKeep(cells, "PatchTrain", item.PatchTrain);
            item.Edition = table.GetOrKeep(cells, "Edition", item.Edition);
            item.Distribution = table.GetOrKeep(cells, "Distribution", item.Distribution);
            item.ApplicabilityNotes = table.GetOrKeep(cells, "ApplicabilityNotes", item.ApplicabilityNotes);
            item.GaDateUtc = OptionalDate(table, cells, "GaDate", item.GaDateUtc, rowErrors);
            item.GaDatePrecision = OptionalEnum(table, cells, "GaDatePrecision", item.GaDatePrecision, rowErrors);
            item.MainstreamSupportEndUtc = OptionalDate(table, cells, "MainstreamSupportEnd", item.MainstreamSupportEndUtc, rowErrors);
            item.MainstreamSupportEndPrecision = OptionalEnum(table, cells, "MainstreamSupportEndPrecision", item.MainstreamSupportEndPrecision, rowErrors);
            item.ExtendedSupportEndUtc = OptionalDate(table, cells, "ExtendedSupportEnd", item.ExtendedSupportEndUtc, rowErrors);
            item.ExtendedSupportEndPrecision = OptionalEnum(table, cells, "ExtendedSupportEndPrecision", item.ExtendedSupportEndPrecision, rowErrors);
            item.EsuSupportEndUtc = OptionalDate(table, cells, "EsuSupportEnd", item.EsuSupportEndUtc, rowErrors);
            item.EsuSupportEndPrecision = OptionalEnum(table, cells, "EsuSupportEndPrecision", item.EsuSupportEndPrecision, rowErrors);
            item.DatesAreTbd = OptionalBool(table, cells, "DatesAreTbd", item.DatesAreTbd, rowErrors);
            item.DatesConflict = OptionalBool(table, cells, "DatesConflict", item.DatesConflict, rowErrors);
            var tz = table.GetOrKeep(cells, "DateTimezone", item.DateTimezone);
            item.DateTimezone = string.IsNullOrWhiteSpace(tz) ? "UTC" : tz;
            item.SourceUrl = table.GetOrKeep(cells, "SourceUrl", item.SourceUrl);
            item.SourceReference = table.GetOrKeep(cells, "SourceReference", item.SourceReference);
            item.ReleaseNotesUrl = table.GetOrKeep(cells, "ReleaseNotesUrl", item.ReleaseNotesUrl);
            item.RetrievedAtUtc = OptionalDate(table, cells, "RetrievedAt", item.RetrievedAtUtc, rowErrors);

            CheckLength(rowErrors, "Family", item.Family, 100);
            CheckLength(rowErrors, "ReleaseLabel", item.ReleaseLabel, 200);
            CheckLength(rowErrors, "Build", item.Build, 100);
            CheckLength(rowErrors, "PatchTrain", item.PatchTrain, 100);
            CheckLength(rowErrors, "Edition", item.Edition, 100);
            CheckLength(rowErrors, "Distribution", item.Distribution, 100);
            CheckLength(rowErrors, "ApplicabilityNotes", item.ApplicabilityNotes, 1000);
            CheckLength(rowErrors, "DateTimezone", item.DateTimezone, 50);
            CheckLength(rowErrors, "SourceUrl", item.SourceUrl, 500);
            CheckLength(rowErrors, "SourceReference", item.SourceReference, 500);
            CheckLength(rowErrors, "ReleaseNotesUrl", item.ReleaseNotesUrl, 500);
            if (!LifecycleCsvSupport.IsHttpUrlOrEmpty(item.SourceUrl))
            {
                rowErrors.Add("SourceUrl must be an http:// or https:// link.");
            }
            if (!LifecycleCsvSupport.IsHttpUrlOrEmpty(item.ReleaseNotesUrl))
            {
                rowErrors.Add("ReleaseNotesUrl must be an http:// or https:// link.");
            }

            if (rowErrors.Count > 0)
            {
                errors.Add(new ImportRowError(rowNumber, string.Join(" ", rowErrors)));
                continue;
            }

            var display = $"{engine} / {family} / {label}";
            if (!seen.Add(key))
            {
                rows.Add(new(rowNumber, ImportRowPlan.Ambiguous, null, null, display, "This release appears more than once in this file."));
                continue;
            }
            if (matches is { Count: > 1 })
            {
                rows.Add(new(rowNumber, ImportRowPlan.Ambiguous, null, null, display, $"{matches.Count} catalogue entries already use this engine, family and label. Rename one before importing."));
                continue;
            }

            if (existing is null)
            {
                rows.Add(new(rowNumber, ImportRowPlan.Create, item, null, display, null));
                continue;
            }

            if (existing.IsManualOverride && !ReleaseCatalogMatcher.IsOverrideExpired(existing, nowUtc))
            {
                rows.Add(new(rowNumber, ImportRowPlan.Ambiguous, null, existing.Id, display,
                    $"This entry has an active manual override ({existing.OverrideReason}). Review or let the override expire before importing vendor facts over it."));
                continue;
            }

            var unchanged = SameImportedFields(existing, item);
            rows.Add(new(rowNumber, unchanged ? ImportRowPlan.Skip : ImportRowPlan.Update, item, existing.Id, display,
                unchanged ? null : existing.IsManualOverride ? "Replaces an expired manual override with the imported vendor facts." : null));
        }

        return new LifecycleImportPreview<ReleaseCatalogEntry>(rows, errors);
    }

    /// <summary>Copies the columns this importer owns. Override, review and staleness fields are left to the caller.</summary>
    public static void CopyImportedFields(ReleaseCatalogEntry from, ReleaseCatalogEntry to)
    {
        to.Engine = from.Engine;
        to.Family = from.Family;
        to.ReleaseLabel = from.ReleaseLabel;
        to.Major = from.Major;
        to.Minor = from.Minor;
        to.Build = from.Build;
        to.Channel = from.Channel;
        to.PatchTrain = from.PatchTrain;
        to.Edition = from.Edition;
        to.Distribution = from.Distribution;
        to.ApplicabilityNotes = from.ApplicabilityNotes;
        to.GaDateUtc = from.GaDateUtc;
        to.GaDatePrecision = from.GaDatePrecision;
        to.MainstreamSupportEndUtc = from.MainstreamSupportEndUtc;
        to.MainstreamSupportEndPrecision = from.MainstreamSupportEndPrecision;
        to.ExtendedSupportEndUtc = from.ExtendedSupportEndUtc;
        to.ExtendedSupportEndPrecision = from.ExtendedSupportEndPrecision;
        to.EsuSupportEndUtc = from.EsuSupportEndUtc;
        to.EsuSupportEndPrecision = from.EsuSupportEndPrecision;
        to.DatesAreTbd = from.DatesAreTbd;
        to.DatesConflict = from.DatesConflict;
        to.DateTimezone = from.DateTimezone;
        to.SourceUrl = from.SourceUrl;
        to.SourceReference = from.SourceReference;
        to.ReleaseNotesUrl = from.ReleaseNotesUrl;
        to.RetrievedAtUtc = from.RetrievedAtUtc;
    }

    private static bool SameImportedFields(ReleaseCatalogEntry a, ReleaseCatalogEntry b)
    {
        var copy = new ReleaseCatalogEntry();
        CopyImportedFields(a, copy);
        return copy.Engine == b.Engine && copy.Family == b.Family && copy.ReleaseLabel == b.ReleaseLabel &&
               copy.Major == b.Major && copy.Minor == b.Minor && copy.Build == b.Build && copy.Channel == b.Channel &&
               copy.PatchTrain == b.PatchTrain && copy.Edition == b.Edition && copy.Distribution == b.Distribution &&
               copy.ApplicabilityNotes == b.ApplicabilityNotes &&
               copy.GaDateUtc == b.GaDateUtc && copy.GaDatePrecision == b.GaDatePrecision &&
               copy.MainstreamSupportEndUtc == b.MainstreamSupportEndUtc && copy.MainstreamSupportEndPrecision == b.MainstreamSupportEndPrecision &&
               copy.ExtendedSupportEndUtc == b.ExtendedSupportEndUtc && copy.ExtendedSupportEndPrecision == b.ExtendedSupportEndPrecision &&
               copy.EsuSupportEndUtc == b.EsuSupportEndUtc && copy.EsuSupportEndPrecision == b.EsuSupportEndPrecision &&
               copy.DatesAreTbd == b.DatesAreTbd && copy.DatesConflict == b.DatesConflict && copy.DateTimezone == b.DateTimezone &&
               copy.SourceUrl == b.SourceUrl && copy.SourceReference == b.SourceReference && copy.ReleaseNotesUrl == b.ReleaseNotesUrl &&
               copy.RetrievedAtUtc == b.RetrievedAtUtc &&
               !a.IsManualOverride;
    }

    internal static int? OptionalInt(LifecycleCsvTable table, IReadOnlyList<string> cells, string column, int? existing, List<string> errors)
    {
        if (!table.Has(column))
        {
            return existing;
        }
        var raw = table.Get(cells, column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        if (int.TryParse(raw, out var value))
        {
            return value;
        }
        errors.Add($"{column} '{raw}' is not a whole number.");
        return existing;
    }

    internal static TEnum OptionalEnum<TEnum>(LifecycleCsvTable table, IReadOnlyList<string> cells, string column, TEnum existing, List<string> errors) where TEnum : struct, Enum
    {
        if (!table.Has(column))
        {
            return existing;
        }
        var raw = table.Get(cells, column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return existing;
        }
        if (!int.TryParse(raw, out _) && Enum.TryParse<TEnum>(raw, true, out var value))
        {
            return value;
        }
        errors.Add($"{column} '{raw}' isn't one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        return existing;
    }

    internal static DateTime? OptionalDate(LifecycleCsvTable table, IReadOnlyList<string> cells, string column, DateTime? existing, List<string> errors)
    {
        if (!table.Has(column))
        {
            return existing;
        }
        var raw = table.Get(cells, column);
        if (LifecycleCsvSupport.TryParseUtcDate(raw, out var value))
        {
            return value;
        }
        errors.Add($"{column} '{raw}' isn't a date in yyyy-MM-dd form.");
        return existing;
    }

    internal static bool OptionalBool(LifecycleCsvTable table, IReadOnlyList<string> cells, string column, bool existing, List<string> errors)
    {
        if (!table.Has(column))
        {
            return existing;
        }
        var raw = table.Get(cells, column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }
        if (LifecycleCsvSupport.TryParseBool(raw, out var value))
        {
            return value;
        }
        errors.Add($"{column} '{raw}' must be true or false.");
        return existing;
    }

    internal static void CheckLength(List<string> errors, string column, string value, int max)
    {
        if (value.Length > max)
        {
            errors.Add($"{column} is longer than {max} characters.");
        }
    }
}
