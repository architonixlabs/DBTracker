using System.Globalization;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>How an evidence row relates to what is already recorded. Evidence is append-only, so a repeat import never updates a row.</summary>
public enum EvidenceImportRowPlan
{
    /// <summary>New result; it would be recorded.</summary>
    Create,

    /// <summary>The same result (same instance, check, time and outcome or value) is already recorded; nothing would change.</summary>
    Skip,

    /// <summary>Clashes with a recorded result or an earlier row in the file; not imported.</summary>
    Conflict
}

public record EvidenceImportRow<T>(int RowNumber, EvidenceImportRowPlan Plan, string InstanceName, T? Record, string? Reason) where T : class;

public record EvidenceImportPreview<T>(IReadOnlyList<EvidenceImportRow<T>> Rows, IReadOnlyList<ImportRowError> Errors) where T : class
{
    public int CreateCount => Rows.Count(r => r.Plan == EvidenceImportRowPlan.Create);
    public int SkipCount => Rows.Count(r => r.Plan == EvidenceImportRowPlan.Skip);
    public int ConflictCount => Rows.Count(r => r.Plan == EvidenceImportRowPlan.Conflict);
}

/// <summary>Shared parsing for the evidence imports: header lookup, '#' comment rows, instance lookup by name and UTC time parsing.</summary>
internal sealed class EvidenceCsvReader
{
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, int> _indexOf;
    private List<string> _cells = [];

    private EvidenceCsvReader(Dictionary<string, int> indexOf) => _indexOf = indexOf;

    public static (EvidenceCsvReader? Reader, List<List<string>> Records, string? HeaderError) Open(string csvContent, string[] columns, string[] required)
    {
        var records = CsvRecordReader.Split(csvContent);
        // Comment lines may come before the header, e.g. a copied template's notes.
        while (records.Count > 0 && (records[0].All(string.IsNullOrWhiteSpace) || records[0][0].TrimStart().StartsWith('#')))
        {
            records.RemoveAt(0);
        }
        if (records.Count == 0)
        {
            return (null, records, null);
        }

        var header = records[0].Select(h => h.Trim()).ToList();
        var indexOf = columns.ToDictionary(c => c, c => header.FindIndex(h => string.Equals(h, c, StringComparison.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(c => indexOf[c] < 0).ToList();
        return missing.Count > 0
            ? (null, records, $"Header row must include {string.Join(", ", missing)}.")
            : (new EvidenceCsvReader(indexOf), records, null);
    }

    public static bool IsSkippable(List<string> cells) =>
        cells.All(string.IsNullOrWhiteSpace) || (cells.Count > 0 && cells[0].TrimStart().StartsWith('#'));

    public void Use(List<string> cells) => _cells = cells;

    public string Get(string column)
    {
        var idx = _indexOf[column];
        return idx >= 0 && idx < _cells.Count ? _cells[idx].Trim() : "";
    }

    public static bool TryParseUtc(string raw, out DateTime value) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);

    public static long SecondsKey(DateTime utc) => utc.Ticks / TimeSpan.TicksPerSecond;
}

/// <summary>Imports integrity-check, replication/failover-drill and maintenance-job results exported from a monitoring or job system. Rows
/// match instances by name; a result already on record is skipped, so the same file can be imported again safely.</summary>
public static class MaintenanceEvidenceCsvImporter
{
    public static readonly string[] Columns =
    [
        "Instance", "CheckType", "RanAtUtc", "Outcome", "NodeOrDeployment", "Tool", "ToolVersion", "Schedule",
        "ExpectedIntervalHours", "Source", "SourceReference", "Notes"
    ];

    private static readonly string[] Required = ["Instance", "CheckType", "RanAtUtc", "Outcome"];

    public static IReadOnlyList<ImportColumnHelp> ColumnGuide { get; } =
    [
        new("Instance", true, "Instance name exactly as in the inventory (case is ignored)."),
        new("CheckType", true, string.Join(" | ", Enum.GetNames<MaintenanceCheckType>())),
        new("RanAtUtc", true, "When the check ran, in UTC, e.g. 2026-11-01T02:00:00Z. Future times are rejected."),
        new("Outcome", true, string.Join(" | ", Enum.GetNames<MaintenanceOutcome>())),
        new("NodeOrDeployment", false, "Node, replica or deployment the result covers. Empty means the instance as a whole."),
        new("Tool", false, "Tool or job that produced the result, e.g. Ola Hallengren DatabaseIntegrityCheck."),
        new("ToolVersion", false, "Tool version."),
        new("Schedule", false, "Schedule in words, e.g. Sundays 02:00."),
        new("ExpectedIntervalHours", false, "Hours between runs, more than 0. Without it the result's freshness stays Unknown."),
        new("Source", false, string.Join(" | ", Enum.GetNames<MaintenanceEvidenceSource>()) + ". Default ExternalMonitoringLink."),
        new("SourceReference", false, "Link or ticket id for the result. Required unless Source is ManuallyEntered."),
        new("Notes", false, "Free text. Anything that looks like a password or key is replaced before saving."),
    ];

    public static string TemplateCsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(string.Join(",", Columns)).Append('\n');
        sb.Append("# Lines starting with # are ignored. Results already on record are skipped, so re-importing the same file is safe.\n");
        sb.Append("#EXAMPLE-SQL-01,IntegrityCheck,2026-11-01T02:00:00Z,Passed,node-a,DatabaseIntegrityCheck,10.0,Sundays 02:00,168,ExternalMonitoringLink,https://monitoring.example.com/jobs/4411,Weekly CHECKDB\n");
        foreach (var column in ColumnGuide)
        {
            sb.Append("# ").Append(column.Name).Append(column.Required ? " (required): " : ": ").Append(column.Help.Replace(',', ';')).Append('\n');
        }
        return sb.ToString();
    }

    /// <param name="instanceIdsByName">Importable instances. Archived or ambiguous names go in blockedNames with the reason to show.</param>
    /// <param name="existing">Results already recorded for those instances, used to skip repeats.</param>
    public static EvidenceImportPreview<MaintenanceEvidenceRecord> Parse(
        string csvContent,
        IReadOnlyDictionary<string, int> instanceIdsByName,
        IReadOnlyDictionary<string, string> blockedNames,
        IReadOnlyCollection<MaintenanceEvidenceRecord> existing,
        DateTime nowUtc)
    {
        var rows = new List<EvidenceImportRow<MaintenanceEvidenceRecord>>();
        var errors = new List<ImportRowError>();

        var (reader, records, headerError) = EvidenceCsvReader.Open(csvContent, Columns, Required);
        if (headerError is not null)
        {
            errors.Add(new ImportRowError(1, headerError));
            return new(rows, errors);
        }
        if (reader is null)
        {
            return new(rows, errors);
        }

        var existingByKey = existing
            .Where(r => r.RanAtUtc is not null)
            .GroupBy(r => Key(r.DatabaseInstanceId, r.CheckType, r.NodeOrDeploymentLabel, r.RanAtUtc!.Value))
            .ToDictionary(g => g.Key, g => g.First());
        var seenInFile = new Dictionary<string, int>();

        for (var i = 1; i < records.Count; i++)
        {
            var rowNumber = i + 1;
            if (EvidenceCsvReader.IsSkippable(records[i]))
            {
                continue;
            }
            reader.Use(records[i]);

            var rowErrors = new List<string>();
            var instanceName = reader.Get("Instance");
            int instanceId = 0;
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                rowErrors.Add("Instance is required.");
            }
            else if (blockedNames.TryGetValue(instanceName, out var blockedReason))
            {
                rowErrors.Add(blockedReason);
            }
            else if (!instanceIdsByName.TryGetValue(instanceName, out instanceId))
            {
                rowErrors.Add($"No instance named '{instanceName}'. Add it to the inventory first.");
            }

            if (!Enum.TryParse<MaintenanceCheckType>(reader.Get("CheckType"), true, out var checkType) || !Enum.IsDefined(checkType))
            {
                rowErrors.Add($"CheckType '{reader.Get("CheckType")}' must be one of {string.Join(", ", Enum.GetNames<MaintenanceCheckType>())}.");
            }
            if (!Enum.TryParse<MaintenanceOutcome>(reader.Get("Outcome"), true, out var outcome) || !Enum.IsDefined(outcome))
            {
                rowErrors.Add($"Outcome '{reader.Get("Outcome")}' must be one of {string.Join(", ", Enum.GetNames<MaintenanceOutcome>())}.");
            }

            DateTime ranAt = default;
            var ranAtRaw = reader.Get("RanAtUtc");
            if (!EvidenceCsvReader.TryParseUtc(ranAtRaw, out ranAt))
            {
                rowErrors.Add($"RanAtUtc '{ranAtRaw}' is not a date and time, e.g. 2026-11-01T02:00:00Z.");
            }
            else if (ranAt > nowUtc + EvidenceCsvReader.FutureTolerance)
            {
                rowErrors.Add("RanAtUtc can't be in the future.");
            }

            int? interval = null;
            var intervalRaw = reader.Get("ExpectedIntervalHours");
            if (!string.IsNullOrWhiteSpace(intervalRaw))
            {
                if (int.TryParse(intervalRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                {
                    interval = parsed;
                }
                else
                {
                    rowErrors.Add($"ExpectedIntervalHours '{intervalRaw}' must be a whole number above 0.");
                }
            }

            var source = MaintenanceEvidenceSource.ExternalMonitoringLink;
            var sourceRaw = reader.Get("Source");
            if (!string.IsNullOrWhiteSpace(sourceRaw) && (!Enum.TryParse(sourceRaw, true, out source) || !Enum.IsDefined(source)))
            {
                rowErrors.Add($"Source '{sourceRaw}' must be one of {string.Join(", ", Enum.GetNames<MaintenanceEvidenceSource>())}.");
            }
            var sourceReference = SecretLeakGuard.Redact(reader.Get("SourceReference"));
            if (source != MaintenanceEvidenceSource.ManuallyEntered && string.IsNullOrWhiteSpace(sourceReference))
            {
                rowErrors.Add("SourceReference is required unless Source is ManuallyEntered.");
            }

            var node = reader.Get("NodeOrDeployment");
            CheckLength(rowErrors, "NodeOrDeployment", node, 200);
            CheckLength(rowErrors, "Tool", reader.Get("Tool"), 200);
            CheckLength(rowErrors, "ToolVersion", reader.Get("ToolVersion"), 100);
            CheckLength(rowErrors, "Schedule", reader.Get("Schedule"), 300);
            CheckLength(rowErrors, "SourceReference", sourceReference, 500);
            CheckLength(rowErrors, "Notes", reader.Get("Notes"), 2000);

            if (rowErrors.Count > 0)
            {
                errors.Add(new ImportRowError(rowNumber, string.Join(" ", rowErrors)));
                continue;
            }

            var record = new MaintenanceEvidenceRecord
            {
                DatabaseInstanceId = instanceId,
                CheckType = checkType,
                NodeOrDeploymentLabel = node,
                ToolName = reader.Get("Tool"),
                ToolVersion = reader.Get("ToolVersion"),
                ScheduleDescription = reader.Get("Schedule"),
                ExpectedIntervalHours = interval,
                RanAtUtc = ranAt,
                Outcome = outcome,
                Source = source,
                SourceReference = sourceReference,
                Notes = SecretLeakGuard.Redact(reader.Get("Notes"))
            };

            var key = Key(instanceId, checkType, node, ranAt);
            if (seenInFile.TryGetValue(key, out var firstRow))
            {
                rows.Add(new(rowNumber, EvidenceImportRowPlan.Conflict, instanceName, record, $"Repeats row {firstRow}: same instance, check, node and time."));
                continue;
            }
            seenInFile[key] = rowNumber;

            if (existingByKey.TryGetValue(key, out var recorded))
            {
                rows.Add(recorded.Outcome == outcome
                    ? new(rowNumber, EvidenceImportRowPlan.Skip, instanceName, record, "Already recorded.")
                    : new(rowNumber, EvidenceImportRowPlan.Conflict, instanceName, record,
                        $"Already recorded as {recorded.Outcome} for the same time. Recorded results are never overwritten; record a new run instead."));
                continue;
            }

            rows.Add(new(rowNumber, EvidenceImportRowPlan.Create, instanceName, record, null));
        }

        return new(rows, errors);
    }

    private static string Key(int instanceId, MaintenanceCheckType checkType, string node, DateTime ranAtUtc) =>
        $"{instanceId}|{checkType}|{node.Trim().ToUpperInvariant()}|{EvidenceCsvReader.SecondsKey(ranAtUtc)}";

    internal static void CheckLength(List<string> errors, string column, string value, int max)
    {
        if (value.Length > max)
        {
            errors.Add($"{column} is longer than {max} characters.");
        }
    }
}

/// <summary>Imports capacity and performance measurements exported from existing monitoring. Rows match instances by name; a measurement
/// already on record (same instance, type, unit and time) is skipped, so the same file can be imported again safely.</summary>
public static class CapacityMeasurementCsvImporter
{
    public static readonly string[] Columns = ["Instance", "MeasurementType", "MeasuredAtUtc", "Value", "Unit", "Source", "MeasurementPeriod", "Notes"];

    private static readonly string[] Required = ["Instance", "MeasurementType", "MeasuredAtUtc", "Value", "Unit", "Source"];

    public static IReadOnlyList<ImportColumnHelp> ColumnGuide { get; } =
    [
        new("Instance", true, "Instance name exactly as in the inventory (case is ignored)."),
        new("MeasurementType", true, string.Join(" | ", Enum.GetNames<CapacityMeasurementType>()) + ". Growth rows in GB feed the forecast."),
        new("MeasuredAtUtc", true, "When the value was measured, in UTC, e.g. 2026-11-01T00:00:00Z. Future times are rejected."),
        new("Value", true, "A number, using . for decimals."),
        new("Unit", true, "e.g. GB, IOPS, ms, transactions/s."),
        new("Source", true, "Which monitoring system or export the value came from."),
        new("MeasurementPeriod", false, "e.g. 30-day average, point-in-time, month-end peak."),
        new("Notes", false, "Free text."),
    ];

    public static string TemplateCsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(string.Join(",", Columns)).Append('\n');
        sb.Append("# Lines starting with # are ignored. Measurements already on record are skipped, so re-importing the same file is safe.\n");
        sb.Append("#EXAMPLE-SQL-01,Growth,2026-11-01T00:00:00Z,812.5,GB,Zabbix volume export,point-in-time,Data file total\n");
        foreach (var column in ColumnGuide)
        {
            sb.Append("# ").Append(column.Name).Append(column.Required ? " (required): " : ": ").Append(column.Help.Replace(',', ';')).Append('\n');
        }
        return sb.ToString();
    }

    public static EvidenceImportPreview<CapacityMeasurement> Parse(
        string csvContent,
        IReadOnlyDictionary<string, int> instanceIdsByName,
        IReadOnlyDictionary<string, string> blockedNames,
        IReadOnlyCollection<CapacityMeasurement> existing,
        DateTime nowUtc)
    {
        var rows = new List<EvidenceImportRow<CapacityMeasurement>>();
        var errors = new List<ImportRowError>();

        var (reader, records, headerError) = EvidenceCsvReader.Open(csvContent, Columns, Required);
        if (headerError is not null)
        {
            errors.Add(new ImportRowError(1, headerError));
            return new(rows, errors);
        }
        if (reader is null)
        {
            return new(rows, errors);
        }

        var existingByKey = existing
            .GroupBy(m => Key(m.DatabaseInstanceId, m.MeasurementType, m.Unit, m.MeasuredAtUtc))
            .ToDictionary(g => g.Key, g => g.First());
        var seenInFile = new Dictionary<string, int>();

        for (var i = 1; i < records.Count; i++)
        {
            var rowNumber = i + 1;
            if (EvidenceCsvReader.IsSkippable(records[i]))
            {
                continue;
            }
            reader.Use(records[i]);

            var rowErrors = new List<string>();
            var instanceName = reader.Get("Instance");
            int instanceId = 0;
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                rowErrors.Add("Instance is required.");
            }
            else if (blockedNames.TryGetValue(instanceName, out var blockedReason))
            {
                rowErrors.Add(blockedReason);
            }
            else if (!instanceIdsByName.TryGetValue(instanceName, out instanceId))
            {
                rowErrors.Add($"No instance named '{instanceName}'. Add it to the inventory first.");
            }

            if (!Enum.TryParse<CapacityMeasurementType>(reader.Get("MeasurementType"), true, out var type) || !Enum.IsDefined(type))
            {
                rowErrors.Add($"MeasurementType '{reader.Get("MeasurementType")}' must be one of {string.Join(", ", Enum.GetNames<CapacityMeasurementType>())}.");
            }

            DateTime measuredAt = default;
            var measuredRaw = reader.Get("MeasuredAtUtc");
            if (!EvidenceCsvReader.TryParseUtc(measuredRaw, out measuredAt))
            {
                rowErrors.Add($"MeasuredAtUtc '{measuredRaw}' is not a date and time, e.g. 2026-11-01T00:00:00Z.");
            }
            else if (measuredAt > nowUtc + EvidenceCsvReader.FutureTolerance)
            {
                rowErrors.Add("MeasuredAtUtc can't be in the future.");
            }

            var valueRaw = reader.Get("Value");
            if (!double.TryParse(valueRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || double.IsInfinity(value))
            {
                rowErrors.Add($"Value '{valueRaw}' is not a number.");
            }

            var unit = reader.Get("Unit");
            var source = reader.Get("Source");
            if (string.IsNullOrWhiteSpace(unit))
            {
                rowErrors.Add("Unit is required.");
            }
            if (string.IsNullOrWhiteSpace(source))
            {
                rowErrors.Add("Source is required, so the value can be traced to where it came from.");
            }
            MaintenanceEvidenceCsvImporter.CheckLength(rowErrors, "Unit", unit, 50);
            MaintenanceEvidenceCsvImporter.CheckLength(rowErrors, "Source", source, 300);
            MaintenanceEvidenceCsvImporter.CheckLength(rowErrors, "MeasurementPeriod", reader.Get("MeasurementPeriod"), 300);
            MaintenanceEvidenceCsvImporter.CheckLength(rowErrors, "Notes", reader.Get("Notes"), 1000);

            if (rowErrors.Count > 0)
            {
                errors.Add(new ImportRowError(rowNumber, string.Join(" ", rowErrors)));
                continue;
            }

            var measurement = new CapacityMeasurement
            {
                DatabaseInstanceId = instanceId,
                MeasurementType = type,
                MeasuredAtUtc = measuredAt,
                Value = value,
                Unit = unit,
                Source = source,
                MeasurementPeriodDescription = reader.Get("MeasurementPeriod"),
                Notes = SecretLeakGuard.Redact(reader.Get("Notes"))
            };

            var key = Key(instanceId, type, unit, measuredAt);
            if (seenInFile.TryGetValue(key, out var firstRow))
            {
                rows.Add(new(rowNumber, EvidenceImportRowPlan.Conflict, instanceName, measurement, $"Repeats row {firstRow}: same instance, type, unit and time."));
                continue;
            }
            seenInFile[key] = rowNumber;

            if (existingByKey.TryGetValue(key, out var recorded))
            {
                rows.Add(recorded.Value.Equals(value)
                    ? new(rowNumber, EvidenceImportRowPlan.Skip, instanceName, measurement, "Already recorded.")
                    : new(rowNumber, EvidenceImportRowPlan.Conflict, instanceName, measurement,
                        $"Already recorded as {recorded.Value} {recorded.Unit} for the same time. Recorded measurements are never overwritten."));
                continue;
            }

            rows.Add(new(rowNumber, EvidenceImportRowPlan.Create, instanceName, measurement, null));
        }

        return new(rows, errors);
    }

    private static string Key(int instanceId, CapacityMeasurementType type, string unit, DateTime measuredAtUtc) =>
        $"{instanceId}|{type}|{unit.Trim().ToUpperInvariant()}|{EvidenceCsvReader.SecondsKey(measuredAtUtc)}";
}
