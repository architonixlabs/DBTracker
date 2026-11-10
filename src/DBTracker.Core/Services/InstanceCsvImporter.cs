using System.Text;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ImportRowError(int RowNumber, string Message);
public record ImportResult(IReadOnlyList<DatabaseInstance> Valid, IReadOnlyList<ImportRowError> Errors);

/// <summary>How a parsed CSV row relates to the existing inventory (U17) — computed by matching on Name (case-insensitive), the chosen stable identity for re-imports.</summary>
public enum ImportRowPlan
{
    /// <summary>No existing instance has this name — would insert a new row.</summary>
    Create,

    /// <summary>Exactly one existing instance has this name — would update it in place rather than duplicating.</summary>
    Update,

    /// <summary>Matches an existing instance whose fields are already identical — nothing would change.</summary>
    Skip,

    /// <summary>Matches more than one existing instance, or repeats a name already used earlier in this same file — too ambiguous to apply automatically.</summary>
    Ambiguous
}

public record ImportRowPreview(int RowNumber, ImportRowPlan Plan, DatabaseInstance? Instance, int? MatchedExistingId, string? AmbiguityReason);
public record ImportPreviewResult(IReadOnlyList<ImportRowPreview> Rows, IReadOnlyList<ImportRowError> Errors)
{
    public int CreateCount => Rows.Count(r => r.Plan == ImportRowPlan.Create);
    public int UpdateCount => Rows.Count(r => r.Plan == ImportRowPlan.Update);
    public int SkipCount => Rows.Count(r => r.Plan == ImportRowPlan.Skip);
    public int AmbiguousCount => Rows.Count(r => r.Plan == ImportRowPlan.Ambiguous);
}

/// <summary>
/// Parses a CSV of database instances for bulk onboarding and repeat re-import (U17) — adding fifty
/// databases one at a time through the form doesn't scale, and re-importing the same export should
/// update existing rows rather than duplicate them. Pure (no I/O): the caller reads the uploaded
/// file and persists the result. Header is order-independent and case-insensitive; only Name and
/// Engine are required. Validation is shared with the create/edit form via InstanceValidator (U13).
/// </summary>
public static class InstanceCsvImporter
{
    public static readonly string[] Columns =
    [
        "Name", "Engine", "Version", "Application", "Company", "Department", "ContactName",
        "ContactEmail", "ContactPhone", "Role", "Environment", "DatabaseIp", "ServerIp",
        "ExpectedBackupIntervalHours", "BackupFileLocation", "RetentionPolicyDays", "Tags",
        "RunbookUrl", "Notes"
    ];

    public static string TemplateCsv() => string.Join(",", Columns) + "\n";

    /// <summary>Back-compat simple parse (no matching against existing inventory) — every row that parses cleanly comes back as "would create".</summary>
    public static ImportResult Parse(string csvContent)
    {
        var preview = ParseForImport(csvContent, new Dictionary<string, DatabaseInstance>(StringComparer.OrdinalIgnoreCase));
        var valid = preview.Rows.Where(r => r.Instance is not null).Select(r => r.Instance!).ToList();
        return new ImportResult(valid, preview.Errors);
    }

    /// <summary>
    /// Parses the CSV and classifies every row as Create/Update/Skip/Ambiguous against the supplied
    /// existing instances (keyed by Name, case-insensitive — the stable match rule). A name repeated
    /// within the file itself is also Ambiguous, so a typo'd duplicate row never silently overwrites
    /// the same instance twice in one import.
    /// </summary>
    public static ImportPreviewResult ParseForImport(string csvContent, IReadOnlyDictionary<string, DatabaseInstance> existingByName)
    {
        var rows = new List<ImportRowPreview>();
        var errors = new List<ImportRowError>();
        var namesSeenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var records = SplitCsvRecords(csvContent);
        if (records.Count == 0)
        {
            return new ImportPreviewResult(rows, errors);
        }

        var header = records[0].Select(h => h.Trim()).ToList();
        var indexOf = Columns.ToDictionary(c => c, c => header.FindIndex(h => string.Equals(h, c, StringComparison.OrdinalIgnoreCase)));

        if (indexOf["Name"] < 0 || indexOf["Engine"] < 0)
        {
            errors.Add(new ImportRowError(1, "Header row must include at least Name and Engine columns."));
            return new ImportPreviewResult(rows, errors);
        }

        for (var i = 1; i < records.Count; i++)
        {
            var rowNumber = i + 1; // 1-based; header is row 1
            var cells = records[i];
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue; // blank line — not a malformed row, just skip it
            }

            string Get(string column)
            {
                var idx = indexOf[column];
                return idx >= 0 && idx < cells.Count ? cells[idx].Trim() : "";
            }

            // A column simply absent from this file's header (vs. present but empty) must never be
            // read as "clear this field" on an Update row — a lean re-export covering only a few
            // columns (e.g. Name/Engine/Application) would otherwise silently blank every other
            // field, including contact info, on the matched existing instance. Present-but-empty
            // still means "set to empty": only a genuinely missing column falls back to whatever
            // the matched existing instance already has.
            var name = Get("Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new ImportRowError(rowNumber, "Name is required."));
                continue;
            }

            existingByName.TryGetValue(name, out var existing);

            string GetOrKeep(string column, string existingValue) =>
                indexOf[column] >= 0 ? Get(column) : existingValue;

            var engineRaw = GetOrKeep("Engine", existing?.Engine.ToString() ?? "");
            if (!Enum.TryParse<DatabaseEngine>(engineRaw, true, out var engine))
            {
                errors.Add(new ImportRowError(rowNumber, $"Unrecognized engine '{engineRaw}'. Expected one of: {string.Join(", ", Enum.GetNames<DatabaseEngine>())}."));
                continue;
            }

            var role = existing?.Role ?? InstanceRole.Active;
            var roleRaw = GetOrKeep("Role", "");
            if (!string.IsNullOrWhiteSpace(roleRaw) && !Enum.TryParse(roleRaw, true, out role))
            {
                errors.Add(new ImportRowError(rowNumber, $"Unrecognized role '{roleRaw}'. Expected Active or Passive."));
                continue;
            }

            var environment = existing?.Environment ?? EnvironmentTier.Production;
            var envRaw = GetOrKeep("Environment", "");
            if (!string.IsNullOrWhiteSpace(envRaw) && !Enum.TryParse(envRaw, true, out environment))
            {
                errors.Add(new ImportRowError(rowNumber, $"Unrecognized environment '{envRaw}'. Expected one of: {string.Join(", ", Enum.GetNames<EnvironmentTier>())}."));
                continue;
            }

            var interval = existing?.ExpectedBackupIntervalHours ?? 24;
            var intervalRaw = GetOrKeep("ExpectedBackupIntervalHours", "");
            if (!string.IsNullOrWhiteSpace(intervalRaw) && !int.TryParse(intervalRaw, out interval))
            {
                errors.Add(new ImportRowError(rowNumber, $"ExpectedBackupIntervalHours '{intervalRaw}' is not a whole number."));
                continue;
            }

            var retention = existing?.RetentionPolicyDays;
            var retentionRaw = GetOrKeep("RetentionPolicyDays", "");
            if (!string.IsNullOrWhiteSpace(retentionRaw))
            {
                if (!int.TryParse(retentionRaw, out var retentionValue))
                {
                    errors.Add(new ImportRowError(rowNumber, $"RetentionPolicyDays '{retentionRaw}' is not a whole number."));
                    continue;
                }
                retention = retentionValue;
            }
            else if (indexOf["RetentionPolicyDays"] >= 0)
            {
                retention = null; // column present but blank — an explicit clear, not "keep existing"
            }

            var candidate = new DatabaseInstance
            {
                Name = name,
                Engine = engine,
                Version = GetOrKeep("Version", existing?.Version ?? ""),
                Application = GetOrKeep("Application", existing?.Application ?? ""),
                Company = GetOrKeep("Company", existing?.Company ?? ""),
                Department = GetOrKeep("Department", existing?.Department ?? ""),
                ContactName = GetOrKeep("ContactName", existing?.ContactName ?? ""),
                ContactEmail = GetOrKeep("ContactEmail", existing?.ContactEmail ?? ""),
                ContactPhone = GetOrKeep("ContactPhone", existing?.ContactPhone ?? ""),
                Role = role,
                Environment = environment,
                DatabaseIp = GetOrKeep("DatabaseIp", existing?.DatabaseIp ?? ""),
                ServerIp = GetOrKeep("ServerIp", existing?.ServerIp ?? ""),
                ExpectedBackupIntervalHours = interval,
                BackupFileLocation = GetOrKeep("BackupFileLocation", existing?.BackupFileLocation ?? ""),
                RetentionPolicyDays = retention,
                Tags = GetOrKeep("Tags", existing?.Tags ?? ""),
                RunbookUrl = GetOrKeep("RunbookUrl", existing?.RunbookUrl ?? ""),
                Notes = GetOrKeep("Notes", existing?.Notes ?? ""),
            };

            var validationErrors = InstanceValidator.Validate(candidate);
            if (validationErrors.Count > 0)
            {
                errors.Add(new ImportRowError(rowNumber, string.Join(" ", validationErrors.Select(e => e.Message))));
                continue;
            }

            if (!namesSeenInFile.Add(name))
            {
                rows.Add(new ImportRowPreview(rowNumber, ImportRowPlan.Ambiguous, null, null, $"Name '{name}' appears more than once in this file."));
                continue;
            }

            if (existing is not null)
            {
                candidate.Id = existing.Id;
                // Carries over the existing row's concurrency token (U13) — without this, updating
                // via CSV import would always fail EF's optimistic-concurrency check, since a fresh
                // DatabaseInstance's RowVersion defaults to empty rather than what's actually stored.
                candidate.RowVersion = existing.RowVersion;
                var plan = IsUnchanged(existing, candidate) ? ImportRowPlan.Skip : ImportRowPlan.Update;
                rows.Add(new ImportRowPreview(rowNumber, plan, candidate, existing.Id, null));
            }
            else
            {
                rows.Add(new ImportRowPreview(rowNumber, ImportRowPlan.Create, candidate, null, null));
            }
        }

        return new ImportPreviewResult(rows, errors);
    }

    private static bool IsUnchanged(DatabaseInstance existing, DatabaseInstance candidate) =>
        existing.Engine == candidate.Engine &&
        existing.Version == candidate.Version &&
        existing.Application == candidate.Application &&
        existing.Company == candidate.Company &&
        existing.Department == candidate.Department &&
        existing.ContactName == candidate.ContactName &&
        existing.ContactEmail == candidate.ContactEmail &&
        existing.ContactPhone == candidate.ContactPhone &&
        existing.Role == candidate.Role &&
        existing.Environment == candidate.Environment &&
        existing.DatabaseIp == candidate.DatabaseIp &&
        existing.ServerIp == candidate.ServerIp &&
        existing.ExpectedBackupIntervalHours == candidate.ExpectedBackupIntervalHours &&
        existing.BackupFileLocation == candidate.BackupFileLocation &&
        existing.RetentionPolicyDays == candidate.RetentionPolicyDays &&
        existing.Tags == candidate.Tags &&
        existing.RunbookUrl == candidate.RunbookUrl &&
        existing.Notes == candidate.Notes;

    /// <summary>
    /// Splits raw CSV text into records (rows of cells), respecting RFC-4180-style quoting: a
    /// quoted field can contain commas and literal newlines, and "" inside a quoted field is a
    /// literal quote character. Unlike splitting on '\n' first, this never breaks a quoted
    /// multi-line field into two rows.
    /// </summary>
    private static List<List<string>> SplitCsvRecords(string content)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var sawAnyCellInContent = false;

        var i = 0;
        while (i < content.Length)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                current.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    sawAnyCellInContent = true;
                    i++;
                    continue;
                case ',':
                    row.Add(current.ToString());
                    current.Clear();
                    sawAnyCellInContent = true;
                    i++;
                    continue;
                case '\r':
                    i++;
                    continue;
                case '\n':
                    row.Add(current.ToString());
                    current.Clear();
                    records.Add(row);
                    row = new List<string>();
                    sawAnyCellInContent = false;
                    i++;
                    continue;
                default:
                    current.Append(c);
                    sawAnyCellInContent = true;
                    i++;
                    continue;
            }
        }

        if (current.Length > 0 || row.Count > 0 || sawAnyCellInContent)
        {
            row.Add(current.ToString());
            records.Add(row);
        }

        return records;
    }
}
