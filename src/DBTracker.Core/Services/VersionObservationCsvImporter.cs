using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Imports version metadata gathered outside DBTracker (a script, a provider console export) as observations.
/// Each row is one observation for an instance named in the Name column. Observations are append-only history, so a row
/// is either new (Create) or already recorded (Skip: same instance, time, outcome and version text). A failed attempt
/// (PermissionDenied or Unreachable) is recorded as such and never changes the declared identity.</summary>
public static class VersionObservationCsvImporter
{
    public static readonly string[] Columns =
    [
        "Name", "ObservedAt", "Outcome", "RawVersionText", "ObservedEdition", "ObservedCompatibilityLevel", "FailureDetail", "Notes"
    ];

    public static IReadOnlyList<ImportColumnHelp> ColumnGuide { get; } =
    [
        new("Name", true, "Instance name, ignoring case. Archived instances can't be imported into."),
        new("ObservedAt", true, "When the version was read, in UTC: yyyy-MM-dd HH:mm. Needed so a repeat import recognises rows it already recorded."),
        new("Outcome", false, string.Join(" | ", Enum.GetNames<ObservationOutcome>()) + ". Default Success."),
        new("RawVersionText", false, "Version exactly as reported, e.g. 16.4 or 15.0.4385.2. Required when Outcome is Success."),
        new("ObservedEdition", false, "Edition as reported, compared with the declared edition."),
        new("ObservedCompatibilityLevel", false, "Database compatibility level as reported, compared with the declared one."),
        new("FailureDetail", false, "What went wrong, e.g. the permission that was refused. Required when Outcome isn't Success."),
        new("Notes", false, "Free text, e.g. which script or export produced the row."),
    ];

    public static string TemplateCsv() => LifecycleCsvSupport.BuildTemplate(Columns,
        "Lines starting with # are ignored. Name and ObservedAt are required. Record failed reads too: they show where metadata couldn't be collected.",
        "ARXMAIL-PROD-01,2026-11-10 06:00,Success,16.4,,,,Nightly inventory script",
        ColumnGuide);

    public static string Key(int instanceId, DateTime observedAtUtc, ObservationOutcome outcome, string rawVersionText) =>
        $"{instanceId}|{observedAtUtc:yyyy-MM-ddTHH:mm:ss}|{outcome}|{rawVersionText.Trim().ToUpperInvariant()}";

    /// <param name="instanceIdsByName">Importable (single, unarchived) instances by name.</param>
    /// <param name="blockedNames">Names that can't be imported into, with the reason shown on the row.</param>
    /// <param name="existingKeys">Keys (see Key) of observations already stored.</param>
    public static LifecycleImportPreview<VersionObservation> ParseForImport(string csvContent,
        IReadOnlyDictionary<string, int> instanceIdsByName, IReadOnlyDictionary<string, string> blockedNames, IReadOnlySet<string> existingKeys)
    {
        var rows = new List<LifecycleImportRow<VersionObservation>>();
        var errors = new List<ImportRowError>();

        var table = LifecycleCsvTable.Read(csvContent, Columns);
        if (table is null)
        {
            return new LifecycleImportPreview<VersionObservation>(rows, errors);
        }
        if (!table.Has("Name") || !table.Has("ObservedAt"))
        {
            errors.Add(new ImportRowError(1, "Header row must include Name and ObservedAt columns."));
            return new LifecycleImportPreview<VersionObservation>(rows, errors);
        }

        var seen = new HashSet<string>();
        foreach (var (rowNumber, cells) in table.DataRows)
        {
            var rowErrors = new List<string>();
            var name = table.Get(cells, "Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new ImportRowError(rowNumber, "Name is required."));
                continue;
            }

            var observedRaw = table.Get(cells, "ObservedAt");
            if (string.IsNullOrWhiteSpace(observedRaw))
            {
                rowErrors.Add("ObservedAt is required.");
            }
            else if (!LifecycleCsvSupport.TryParseUtcDate(observedRaw, out var parsedTime) || parsedTime is null)
            {
                rowErrors.Add($"ObservedAt '{observedRaw}' isn't a date and time in yyyy-MM-dd HH:mm form.");
            }

            var outcome = ReleaseCatalogCsvImporter.OptionalEnum(table, cells, "Outcome", ObservationOutcome.Success, rowErrors);
            var raw = table.Get(cells, "RawVersionText");
            var failure = table.Get(cells, "FailureDetail");
            if (outcome == ObservationOutcome.Success && string.IsNullOrWhiteSpace(raw))
            {
                rowErrors.Add("RawVersionText is required when Outcome is Success.");
            }
            if (outcome != ObservationOutcome.Success && string.IsNullOrWhiteSpace(failure))
            {
                rowErrors.Add($"FailureDetail is required when Outcome is {outcome}.");
            }

            var item = new VersionObservation
            {
                Source = VersionObservationSource.CsvImport,
                Outcome = outcome,
                RawVersionText = raw,
                ObservedEdition = table.Get(cells, "ObservedEdition"),
                ObservedCompatibilityLevel = table.Get(cells, "ObservedCompatibilityLevel"),
                FailureDetail = failure,
                Notes = table.Get(cells, "Notes")
            };
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "RawVersionText", item.RawVersionText, 200);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "ObservedEdition", item.ObservedEdition, 100);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "ObservedCompatibilityLevel", item.ObservedCompatibilityLevel, 100);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "FailureDetail", item.FailureDetail, 500);
            ReleaseCatalogCsvImporter.CheckLength(rowErrors, "Notes", item.Notes, 1000);

            if (rowErrors.Count > 0)
            {
                errors.Add(new ImportRowError(rowNumber, string.Join(" ", rowErrors)));
                continue;
            }

            LifecycleCsvSupport.TryParseUtcDate(observedRaw, out var observedAt);
            item.ObservedAtUtc = observedAt!.Value;
            var display = $"{name} at {item.ObservedAtUtc:yyyy-MM-dd HH:mm} ({outcome})";

            if (blockedNames.TryGetValue(name, out var blockedReason))
            {
                rows.Add(new(rowNumber, ImportRowPlan.Ambiguous, null, null, display, blockedReason));
                continue;
            }
            if (!instanceIdsByName.TryGetValue(name, out var instanceId))
            {
                errors.Add(new ImportRowError(rowNumber, $"No instance is named '{name}'."));
                continue;
            }

            item.DatabaseInstanceId = instanceId;
            var key = Key(instanceId, item.ObservedAtUtc, outcome, raw);
            if (!seen.Add(key))
            {
                rows.Add(new(rowNumber, ImportRowPlan.Ambiguous, null, null, display, "This observation appears more than once in this file."));
                continue;
            }

            rows.Add(existingKeys.Contains(key)
                ? new(rowNumber, ImportRowPlan.Skip, item, instanceId, display, "Already recorded.")
                : new(rowNumber, ImportRowPlan.Create, item, instanceId, display, null));
        }

        return new LifecycleImportPreview<VersionObservation>(rows, errors);
    }
}
