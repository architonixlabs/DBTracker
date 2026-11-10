using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ImportRowError(int RowNumber, string Message);
public record ImportResult(IReadOnlyList<DatabaseInstance> Valid, IReadOnlyList<ImportRowError> Errors);

/// <summary>
/// Parses a CSV of database instances for bulk onboarding — adding fifty databases one at a time
/// through the form doesn't scale. Pure (no I/O): the caller reads the uploaded file and persists
/// the result. Header is order-independent and case-insensitive; only Name and Engine are required.
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

    public static ImportResult Parse(string csvContent)
    {
        var valid = new List<DatabaseInstance>();
        var errors = new List<ImportRowError>();

        var lines = csvContent.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
        if (lines.Count == 0)
        {
            return new ImportResult(valid, errors);
        }

        var header = SplitCsvLine(lines[0]).Select(h => h.Trim()).ToList();
        var indexOf = Columns.ToDictionary(c => c, c => header.FindIndex(h => string.Equals(h, c, StringComparison.OrdinalIgnoreCase)));

        if (indexOf["Name"] < 0 || indexOf["Engine"] < 0)
        {
            errors.Add(new ImportRowError(1, "Header row must include at least Name and Engine columns."));
            return new ImportResult(valid, errors);
        }

        for (var i = 1; i < lines.Count; i++)
        {
            var rowNumber = i + 1; // 1-based; header is row 1
            var cells = SplitCsvLine(lines[i]);

            string Get(string column)
            {
                var idx = indexOf[column];
                return idx >= 0 && idx < cells.Count ? cells[idx].Trim() : "";
            }

            var name = Get("Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new ImportRowError(rowNumber, "Name is required."));
                continue;
            }

            var engineRaw = Get("Engine");
            if (!Enum.TryParse<DatabaseEngine>(engineRaw, true, out var engine))
            {
                errors.Add(new ImportRowError(rowNumber, $"Unrecognized engine '{engineRaw}'. Expected one of: {string.Join(", ", Enum.GetNames<DatabaseEngine>())}."));
                continue;
            }

            var role = InstanceRole.Active;
            var roleRaw = Get("Role");
            if (!string.IsNullOrWhiteSpace(roleRaw) && !Enum.TryParse(roleRaw, true, out role))
            {
                errors.Add(new ImportRowError(rowNumber, $"Unrecognized role '{roleRaw}'. Expected Active or Passive."));
                continue;
            }

            var environment = EnvironmentTier.Production;
            var envRaw = Get("Environment");
            if (!string.IsNullOrWhiteSpace(envRaw) && !Enum.TryParse(envRaw, true, out environment))
            {
                errors.Add(new ImportRowError(rowNumber, $"Unrecognized environment '{envRaw}'. Expected one of: {string.Join(", ", Enum.GetNames<EnvironmentTier>())}."));
                continue;
            }

            var interval = 24;
            var intervalRaw = Get("ExpectedBackupIntervalHours");
            if (!string.IsNullOrWhiteSpace(intervalRaw) && !int.TryParse(intervalRaw, out interval))
            {
                errors.Add(new ImportRowError(rowNumber, $"ExpectedBackupIntervalHours '{intervalRaw}' is not a whole number."));
                continue;
            }

            int? retention = null;
            var retentionRaw = Get("RetentionPolicyDays");
            if (!string.IsNullOrWhiteSpace(retentionRaw))
            {
                if (!int.TryParse(retentionRaw, out var retentionValue))
                {
                    errors.Add(new ImportRowError(rowNumber, $"RetentionPolicyDays '{retentionRaw}' is not a whole number."));
                    continue;
                }
                retention = retentionValue;
            }

            valid.Add(new DatabaseInstance
            {
                Name = name,
                Engine = engine,
                Version = Get("Version"),
                Application = Get("Application"),
                Company = Get("Company"),
                Department = Get("Department"),
                ContactName = Get("ContactName"),
                ContactEmail = Get("ContactEmail"),
                ContactPhone = Get("ContactPhone"),
                Role = role,
                Environment = environment,
                DatabaseIp = Get("DatabaseIp"),
                ServerIp = Get("ServerIp"),
                ExpectedBackupIntervalHours = interval,
                BackupFileLocation = Get("BackupFileLocation"),
                RetentionPolicyDays = retention,
                Tags = Get("Tags"),
                RunbookUrl = Get("RunbookUrl"),
                Notes = Get("Notes"),
            });
        }

        return new ImportResult(valid, errors);
    }

    private static List<string> SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
