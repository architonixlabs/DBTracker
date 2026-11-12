using System.Globalization;
using System.Text;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public enum HistoryEventKind
{
    Backup,
    Restore
}

/// <summary>An event already stored for an instance, enough to tell whether an imported row repeats it.</summary>
public record HistoryExistingEvent(int InstanceId, HistoryEventKind Kind, DateTime OccurredUtc, string TypeOrReason, string StatusOrOutcome);

/// <summary>One parsed history row and what applying it would do. Backup or Restore is set for Create rows (and for Skip rows, for display).</summary>
public record HistoryImportRow(
    int RowNumber, ImportRowPlan Plan, HistoryEventKind Kind, string InstanceName, int InstanceId,
    DateTime OccurredUtc, string ZoneId, string TypeOrReason, string StatusOrOutcome,
    BackupRecord? Backup, RestoreEvent? Restore, string? Detail);

public record HistoryImportPreview(IReadOnlyList<HistoryImportRow> Rows, IReadOnlyList<ImportRowError> Errors)
{
    public int CreateCount => Rows.Count(r => r.Plan == ImportRowPlan.Create);
    public int SkipCount => Rows.Count(r => r.Plan == ImportRowPlan.Skip);
    public int AmbiguousCount => Rows.Count(r => r.Plan == ImportRowPlan.Ambiguous);
    public int BackupCreateCount => Rows.Count(r => r.Plan == ImportRowPlan.Create && r.Kind == HistoryEventKind.Backup);
    public int RestoreCreateCount => Rows.Count(r => r.Plan == ImportRowPlan.Create && r.Kind == HistoryEventKind.Restore);
}

/// <summary>Parses a backup and restore history CSV for onboarding. Rows are matched to instances by name, and an event already
/// stored (same instance, kind, time to the second, and type or reason) is skipped, so importing the same file twice adds nothing.
/// Every created entry is Source = Imported with the importing user, and never counts as verified evidence.</summary>
public static class EvidenceHistoryCsvImporter
{
    public const int MaxNotesLength = 2000;
    public const int MaxPerformedByLength = 200;
    public const int MaxEvidenceReferenceLength = 500;

    public static readonly string[] Columns =
    [
        "Kind", "InstanceName", "EventTime", "TimeZone", "Type", "Status", "SizeMb", "DurationMinutes", "Notes", "PerformedBy", "EvidenceReference"
    ];

    public static IReadOnlyList<ImportColumnHelp> ColumnGuide { get; } =
    [
        new("Kind", true, "Backup | Restore"),
        new("InstanceName", true, "Name of an existing, active instance, ignoring case."),
        new("EventTime", true, "When it happened: yyyy-MM-dd HH:mm with a TimeZone, or ISO 8601 with an offset such as 2026-10-01T02:00:00+01:00 or ...Z. Future times are rejected."),
        new("TimeZone", false, "IANA name such as Europe/London or America/New_York, or UTC. Required unless EventTime has an offset."),
        new("Type", true, "Backup: " + string.Join(" | ", Enum.GetNames<BackupType>()) + ". Restore reason: " + string.Join(" | ", Enum.GetNames<RestoreReason>()) + "."),
        new("Status", true, "Backup: " + string.Join(" | ", Enum.GetNames<BackupStatus>()) + ". Restore outcome: " + string.Join(" | ", Enum.GetNames<RestoreOutcome>()) + "."),
        new("SizeMb", false, "Backups only. Number of megabytes, 0 or more."),
        new("DurationMinutes", false, "Number of minutes, 0 or more."),
        new("Notes", false, $"Free text, up to {MaxNotesLength} characters. Quote the cell to include commas or line breaks."),
        new("PerformedBy", false, $"Restores only. Who ran the restore, up to {MaxPerformedByLength} characters."),
        new("EvidenceReference", false, $"Restores only. Ticket, change record or report link, up to {MaxEvidenceReferenceLength} characters."),
    ];

    /// <summary>Header, two example rows and the allowed values, all as # lines that are ignored on import.</summary>
    public static string TemplateCsv()
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", Columns)).Append('\n');
        sb.Append("# Lines starting with # are ignored. Delete the # from an example row to use it. Imported entries are labelled Imported and never count as verified evidence.\n");
        sb.Append("#Backup,EXAMPLE-SQL-01,2026-10-01 02:00,Europe/London,Full,Success,51200,42,Nightly full from the old scheduler,,\n");
        sb.Append("#Restore,EXAMPLE-SQL-01,2026-10-15T09:30:00Z,,Test,Success,,35,Quarterly restore drill,Priya Nair,CHG-1042\n");
        foreach (var column in ColumnGuide)
        {
            sb.Append("# ").Append(column.Name).Append(column.Required ? " (required): " : ": ").Append(column.Help.Replace(',', ';')).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>The repeat-safety key: instance, kind, time to the second, and type or reason. Status is compared separately so a
    /// changed status is reported instead of silently added as a second entry.</summary>
    public static string MatchKey(int instanceId, HistoryEventKind kind, DateTime occurredUtc, string typeOrReason) =>
        $"{instanceId}|{kind}|{TruncateToSecond(occurredUtc):yyyyMMddHHmmss}|{typeOrReason.ToUpperInvariant()}";

    public static DateTime TruncateToSecond(DateTime utc) => new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    /// <param name="instanceIdsByName">Active instances that can receive history, by name (case-insensitive).</param>
    /// <param name="blockedNames">Names that can't receive history, with the reason (archived, or shared by several instances).</param>
    /// <param name="existingEvents">Events already stored for those instances, for repeat-safe matching.</param>
    public static HistoryImportPreview ParseForImport(
        string csvContent,
        IReadOnlyDictionary<string, int> instanceIdsByName,
        IReadOnlyDictionary<string, string>? blockedNames,
        IEnumerable<HistoryExistingEvent> existingEvents,
        DateTime nowUtc,
        string importedBy,
        string sourceFileName = "")
    {
        var rows = new List<HistoryImportRow>();
        var errors = new List<ImportRowError>();

        var records = CsvRecordReader.Split(csvContent);
        if (records.Count == 0)
        {
            return new HistoryImportPreview(rows, errors);
        }

        var header = records[0].Select(h => h.Trim()).ToList();
        var indexOf = Columns.ToDictionary(c => c, c => header.FindIndex(h => string.Equals(h, c, StringComparison.OrdinalIgnoreCase)));
        var missing = new[] { "Kind", "InstanceName", "EventTime", "Type", "Status" }.Where(c => indexOf[c] < 0).ToList();
        if (missing.Count > 0)
        {
            errors.Add(new ImportRowError(1, $"Header row is missing required column(s): {string.Join(", ", missing)}. Download the template for the full header."));
            return new HistoryImportPreview(rows, errors);
        }

        var stored = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in existingEvents)
        {
            stored.TryAdd(MatchKey(e.InstanceId, e.Kind, e.OccurredUtc, e.TypeOrReason), e.StatusOrOutcome);
        }
        var seenInFile = new Dictionary<string, int>(StringComparer.Ordinal);
        var fileLabel = sourceFileName.Length > 260 ? sourceFileName[..260] : sourceFileName;

        for (var i = 1; i < records.Count; i++)
        {
            var rowNumber = i + 1;
            var cells = records[i];
            if (cells.All(string.IsNullOrWhiteSpace) || (cells.Count > 0 && cells[0].TrimStart().StartsWith('#')))
            {
                continue;
            }

            string Get(string column)
            {
                var idx = indexOf[column];
                return idx >= 0 && idx < cells.Count ? cells[idx].Trim() : "";
            }

            var problems = new List<string>();

            var kindRaw = Get("Kind");
            if (!Enum.TryParse<HistoryEventKind>(kindRaw, true, out var kind) || int.TryParse(kindRaw, out _))
            {
                errors.Add(new ImportRowError(rowNumber, $"Kind '{kindRaw}' must be Backup or Restore."));
                continue;
            }

            var name = Get("InstanceName");
            var instanceId = 0;
            if (name.Length == 0)
            {
                problems.Add("InstanceName is required.");
            }
            else if (blockedNames is not null && blockedNames.TryGetValue(name, out var blocked))
            {
                problems.Add(blocked);
            }
            else if (!instanceIdsByName.TryGetValue(name, out instanceId))
            {
                problems.Add($"No active instance is named '{name}'. Import or create the instance first.");
            }

            var time = EventTimeZones.Parse(Get("EventTime"), Get("TimeZone"));
            if (!time.Ok)
            {
                problems.Add(time.Error!);
            }
            else if (EventTimeZones.FutureError(time.Utc!.Value, nowUtc) is { } futureError)
            {
                problems.Add(futureError);
            }

            var typeRaw = Get("Type");
            var statusRaw = Get("Status");
            BackupType backupType = default;
            BackupStatus backupStatus = default;
            RestoreReason restoreReason = default;
            RestoreOutcome restoreOutcome = default;
            if (kind == HistoryEventKind.Backup)
            {
                if (!TryParseBackupType(typeRaw, out backupType))
                {
                    problems.Add($"Type '{typeRaw}' isn't a backup type. Expected one of: {string.Join(", ", Enum.GetNames<BackupType>())}.");
                }
                if (!TryParseName(statusRaw, out backupStatus))
                {
                    problems.Add($"Status '{statusRaw}' isn't a backup status. Expected one of: {string.Join(", ", Enum.GetNames<BackupStatus>())}.");
                }
            }
            else
            {
                if (!TryParseName(typeRaw, out restoreReason))
                {
                    problems.Add($"Type '{typeRaw}' isn't a restore reason. Expected one of: {string.Join(", ", Enum.GetNames<RestoreReason>())}.");
                }
                if (!TryParseName(statusRaw, out restoreOutcome))
                {
                    problems.Add($"Status '{statusRaw}' isn't a restore outcome. Expected one of: {string.Join(", ", Enum.GetNames<RestoreOutcome>())}.");
                }
            }

            var size = ParseMeasure("SizeMb", Get("SizeMb"), problems);
            var duration = ParseMeasure("DurationMinutes", Get("DurationMinutes"), problems);
            var notes = Get("Notes");
            var performedBy = Get("PerformedBy");
            var evidenceReference = Get("EvidenceReference");

            if (notes.Length > MaxNotesLength) problems.Add($"Notes is longer than {MaxNotesLength} characters.");
            if (kind == HistoryEventKind.Backup)
            {
                if (performedBy.Length > 0) problems.Add("PerformedBy applies to restores only. Leave it empty for a backup row.");
                if (evidenceReference.Length > 0) problems.Add("EvidenceReference applies to restores only. Put backup references in Notes.");
            }
            else
            {
                if (size is not null) problems.Add("SizeMb applies to backups only. Leave it empty for a restore row.");
                if (performedBy.Length > MaxPerformedByLength) problems.Add($"PerformedBy is longer than {MaxPerformedByLength} characters.");
                if (evidenceReference.Length > MaxEvidenceReferenceLength) problems.Add($"EvidenceReference is longer than {MaxEvidenceReferenceLength} characters.");
            }

            if (problems.Count > 0)
            {
                errors.Add(new ImportRowError(rowNumber, string.Join(" ", problems)));
                continue;
            }

            var occurredUtc = TruncateToSecond(time.Utc!.Value);
            var typeName = kind == HistoryEventKind.Backup ? backupType.ToString() : restoreReason.ToString();
            var statusName = kind == HistoryEventKind.Backup ? backupStatus.ToString() : restoreOutcome.ToString();
            var key = MatchKey(instanceId, kind, occurredUtc, typeName);

            HistoryImportRow Row(ImportRowPlan plan, string? detail, BackupRecord? b, RestoreEvent? r) =>
                new(rowNumber, plan, kind, name, instanceId, occurredUtc, time.ZoneId, typeName, statusName, b, r, detail);

            if (seenInFile.TryGetValue(key, out var firstRow))
            {
                rows.Add(Row(ImportRowPlan.Ambiguous, $"Repeats row {firstRow} (same instance, time and type).", null, null));
                continue;
            }
            seenInFile[key] = rowNumber;

            if (stored.TryGetValue(key, out var storedStatus))
            {
                rows.Add(string.Equals(storedStatus, statusName, StringComparison.OrdinalIgnoreCase)
                    ? Row(ImportRowPlan.Skip, "Already recorded.", null, null)
                    : Row(ImportRowPlan.Ambiguous, $"An entry at this time is already recorded as {storedStatus}. Correct it on the instance page instead of importing a second one.", null, null));
                continue;
            }

            if (kind == HistoryEventKind.Backup)
            {
                rows.Add(Row(ImportRowPlan.Create, null, new BackupRecord
                {
                    DatabaseInstanceId = instanceId,
                    TimestampUtc = occurredUtc,
                    Type = backupType,
                    Status = backupStatus,
                    SizeMb = size,
                    DurationMinutes = duration,
                    Notes = notes,
                    Source = BackupSource.Imported,
                    SourceFile = fileLabel,
                    RecordedByUserName = importedBy,
                    OccurredTimeZone = time.ZoneId,
                    CreatedAtUtc = nowUtc
                }, null));
            }
            else
            {
                rows.Add(Row(ImportRowPlan.Create, null, null, new RestoreEvent
                {
                    DatabaseInstanceId = instanceId,
                    TimestampUtc = occurredUtc,
                    Reason = restoreReason,
                    Outcome = restoreOutcome,
                    DurationMinutes = duration,
                    Notes = notes,
                    PerformedBy = performedBy,
                    EvidenceReference = evidenceReference,
                    Source = BackupSource.Imported,
                    RecordedByUserName = importedBy,
                    OccurredTimeZone = time.ZoneId,
                    CreatedAtUtc = nowUtc
                }));
            }
        }

        return new HistoryImportPreview(rows, errors);
    }

    private static bool TryParseBackupType(string raw, out BackupType type)
    {
        switch (raw.Trim().ToUpperInvariant())
        {
            case "LOG":
            case "TLOG":
                type = BackupType.TransactionLog;
                return true;
            case "DIFF":
                type = BackupType.Differential;
                return true;
            default:
                return TryParseName(raw, out type);
        }
    }

    /// <summary>Enum by name only: "1" is not accepted as a status, so a misaligned column can't import as a valid value.</summary>
    private static bool TryParseName<T>(string raw, out T value) where T : struct, Enum
    {
        value = default;
        return raw.Length > 0 && !raw.Any(char.IsDigit) && Enum.TryParse(raw, true, out value) && Enum.IsDefined(value);
    }

    private static double? ParseMeasure(string column, string raw, List<string> problems)
    {
        if (raw.Length == 0)
        {
            return null;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || double.IsInfinity(value))
        {
            problems.Add($"{column} '{raw}' is not a number.");
            return null;
        }

        if (value < 0)
        {
            problems.Add($"{column} can't be negative.");
            return null;
        }

        return value;
    }
}
