using System.Text;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ImportRowError(int RowNumber, string Message);
public record ImportColumnHelp(string Name, bool Required, string Help);
public record ImportResult(IReadOnlyList<DatabaseInstance> Valid, IReadOnlyList<ImportRowError> Errors);

/// <summary>Which column identifies the stored instance a CSV row updates.</summary>
public enum ImportMatchRule
{
    /// <summary>Match by Name, ignoring case. A new name creates an instance; names can't be changed by import.</summary>
    Name,

    /// <summary>Match by the DBTracker Id column (from an export). An empty Id creates an instance; a matched row may rename it.</summary>
    Id
}

/// <summary>How a parsed CSV row relates to the inventory, matched by the chosen <see cref="ImportMatchRule"/>.</summary>
public enum ImportRowPlan
{
    /// <summary>No existing instance has this name; a new row would be created.</summary>
    Create,

    /// <summary>Exactly one existing instance has this name; it would be updated in place.</summary>
    Update,

    /// <summary>Matches an existing instance whose fields are already identical; nothing would change.</summary>
    Skip,

    /// <summary>Matches several instances, or repeats a name earlier in the file; too ambiguous to apply.</summary>
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

/// <summary>Parses an instance CSV for onboarding and repeat re-import, updating existing rows instead of duplicating them. Headers are order- and case-insensitive; only Name and Engine are required, and validation is shared with the form via InstanceValidator.</summary>
public static class InstanceCsvImporter
{
    public static readonly string[] Columns =
    [
        "Name", "Engine", "Version", "Application", "Company", "Department", "ContactName",
        "ContactEmail", "ContactPhone", "Role", "Environment", "DatabaseIp", "ServerIp",
        "ExpectedBackupIntervalHours", "BackupFileLocation", "RetentionPolicyDays", "Tags",
        "RunbookUrl", "Notes",
        "Region", "Cluster", "ReplicaSet", "Criticality", "OwnerTeam",
        "BackupContactName", "BackupContactEmail", "BackupContactPhone",
        "BackupFileNamePatterns", "ConnectivityHost", "ConnectivityPort", "RestoreTestIntervalDays",
        "Id"
    ];

    /// <summary>One line per column for the template's comment rows and the import page's column guide, so the two can't drift.</summary>
    public static IReadOnlyList<ImportColumnHelp> ColumnGuide { get; } =
    [
        new("Name", true, "Unique instance name. Matches an existing instance (ignoring case) to update it."),
        new("Engine", true, string.Join(" | ", Enum.GetNames<DatabaseEngine>())),
        new("Version", false, "Free text, e.g. 2022 or 16.4."),
        new("Application", false, "Application the database serves."),
        new("Company", false, "Owning company."),
        new("Department", false, "Owning department."),
        new("ContactName", false, "Primary contact."),
        new("ContactEmail", false, "Primary contact email."),
        new("ContactPhone", false, "Primary contact phone."),
        new("Role", false, "Active | Passive. Default Active."),
        new("Environment", false, string.Join(" | ", Enum.GetNames<EnvironmentTier>()) + ". Default Production."),
        new("DatabaseIp", false, "Database IP or host name."),
        new("ServerIp", false, "Server IP or host name."),
        new("ExpectedBackupIntervalHours", false, "Whole number of hours, more than 0. Default 24."),
        new("BackupFileLocation", false, @"Folder or UNC path the scanner reads, e.g. \\fileserver\backups\arxmail."),
        new("RetentionPolicyDays", false, "Whole number of days, 0 or more."),
        new("Tags", false, "Separated by semicolons, or by commas inside a quoted cell."),
        new("RunbookUrl", false, "http:// or https:// link."),
        new("Notes", false, "Free text. Quote the cell to include commas or line breaks."),
        new("Region", false, "e.g. eu-west."),
        new("Cluster", false, "Cluster or availability group name."),
        new("ReplicaSet", false, "Replica set name."),
        new("Criticality", false, string.Join(" | ", Enum.GetNames<InstanceCriticality>()) + ". Default Unknown."),
        new("OwnerTeam", false, "Name of an existing team (create teams on the Ownership page first)."),
        new("BackupContactName", false, "Second contact."),
        new("BackupContactEmail", false, "Second contact email."),
        new("BackupContactPhone", false, "Second contact phone."),
        new("BackupFileNamePatterns", false, "Which files count as backups, e.g. *.bak;*.trn. Empty means every file."),
        new("ConnectivityHost", false, "Host for the connection check, if not the database IP."),
        new("ConnectivityPort", false, "Port for the connection check, 1 to 65535. Empty uses the engine's default."),
        new("RestoreTestIntervalDays", false, "Days between restore tests for this instance, more than 0. Empty uses the policy."),
        new("Id", false, "DBTracker instance number, only used when matching by Id. Leave empty for a new instance."),
    ];

    /// <summary>Header, an example row and the allowed values. Lines starting with # are ignored on import, so the
    /// example can be kept for reference or turned into a real row by deleting its #.</summary>
    public static string TemplateCsv()
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", Columns)).Append('\n');
        sb.Append("# Lines starting with # are ignored. Only Name and Engine are required; leave other cells empty if unknown.\n");
        sb.Append(@"#EXAMPLE-SQL-01,SqlServer,2022,ArxMail,Architonix Labs,Platform,Priya Nair,priya.nair@example.com,555-0101,Active,Production,10.0.0.10,10.0.0.11,24,")
          .Append(@"\\fileserver\backups\arxmail,30,prod;email,https://wiki.example.com/runbooks/arxmail,Nightly full plus hourly logs,")
          .Append("eu-west,sql-cluster-1,,High,Platform Team,Sam Okoro,sam.okoro@example.com,555-0102,*.bak;*.trn,,1433,90,\n");
        foreach (var column in ColumnGuide)
        {
            sb.Append("# ").Append(column.Name).Append(column.Required ? " (required): " : ": ").Append(column.Help.Replace(',', ';')).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>The current inventory in the import's own columns, Id included, so it can be edited and imported again by Id
    /// (to rename) or by Name. Owner teams are written by name.</summary>
    public static string ExportCsv(IEnumerable<DatabaseInstance> instances, IReadOnlyDictionary<int, string> teamNamesById)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", Columns)).Append('\n');
        foreach (var i in instances.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
        {
            var team = i.OwnerTeamId is { } t && teamNamesById.TryGetValue(t, out var teamName) ? teamName : "";
            sb.Append(CsvRecordReader.Line([
                i.Name, i.Engine.ToString(), i.Version, i.Application, i.Company, i.Department, i.ContactName,
                i.ContactEmail, i.ContactPhone, i.Role.ToString(), i.Environment.ToString(), i.DatabaseIp, i.ServerIp,
                i.ExpectedBackupIntervalHours.ToString(), i.BackupFileLocation, i.RetentionPolicyDays?.ToString() ?? "", i.Tags,
                i.RunbookUrl, i.Notes,
                i.Region, i.Cluster, i.ReplicaSet, i.Criticality.ToString(), team,
                i.BackupContactName, i.BackupContactEmail, i.BackupContactPhone,
                i.BackupFileNamePatterns, i.ConnectivityHostOverride, i.ConnectivityPortOverride?.ToString() ?? "", i.RestoreTestIntervalDaysOverride?.ToString() ?? "",
                i.Id.ToString()])).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Parse without matching against existing inventory; every clean row comes back as Create.</summary>
    public static ImportResult Parse(string csvContent)
    {
        var preview = ParseForImport(csvContent, new Dictionary<string, DatabaseInstance>(StringComparer.OrdinalIgnoreCase));
        var valid = preview.Rows.Where(r => r.Instance is not null).Select(r => r.Instance!).ToList();
        return new ImportResult(valid, preview.Errors);
    }

    /// <summary>Parses the CSV and classifies each row as Create/Update/Skip/Ambiguous against existing instances by Name. A name repeated within the file is Ambiguous, so one import can't update the same instance twice.</summary>
    /// <param name="blockedNames">Names that must not be imported into, with the reason shown on the row: a name shared by several
    /// instances, or one belonging to an archived instance (updating it would silently bring it back).</param>
    /// <param name="teamIdsByName">Existing teams for the OwnerTeam column. An unknown team name is a row error.</param>
    public static ImportPreviewResult ParseForImport(string csvContent, IReadOnlyDictionary<string, DatabaseInstance> existingByName,
        IReadOnlyDictionary<string, string>? blockedNames = null, IReadOnlyDictionary<string, int>? teamIdsByName = null,
        ImportMatchRule matchRule = ImportMatchRule.Name, IReadOnlyDictionary<int, DatabaseInstance>? existingById = null)
    {
        var rows = new List<ImportRowPreview>();
        var errors = new List<ImportRowError>();
        var namesSeenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var idsSeenInFile = new HashSet<int>();

        var records = CsvRecordReader.Split(csvContent);
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

        if (matchRule == ImportMatchRule.Id && indexOf["Id"] < 0)
        {
            errors.Add(new ImportRowError(1, "Matching by Id needs an Id column. Download the current inventory from the import page to get one, or match by Name."));
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

            if (cells.Count > 0 && cells[0].TrimStart().StartsWith('#'))
            {
                continue; // comment line, e.g. the template's example row and allowed values
            }

            string Get(string column)
            {
                var idx = indexOf[column];
                return idx >= 0 && idx < cells.Count ? cells[idx].Trim() : "";
            }

            // A column missing from the header keeps the existing value on Update, so a partial re-export can't blank
            // fields like contact info. A column present but empty still means set to empty.
            var name = Get("Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new ImportRowError(rowNumber, "Name is required."));
                continue;
            }

            DatabaseInstance? existing;
            int? matchedId = null;
            if (matchRule == ImportMatchRule.Id)
            {
                var idRaw = Get("Id");
                existing = null;
                if (idRaw.Length > 0)
                {
                    if (!int.TryParse(idRaw, out var id))
                    {
                        errors.Add(new ImportRowError(rowNumber, $"Id '{idRaw}' is not a whole number."));
                        continue;
                    }
                    if (existingById is null || !existingById.TryGetValue(id, out existing))
                    {
                        errors.Add(new ImportRowError(rowNumber, $"No instance has Id {id}. Leave Id empty to create a new instance."));
                        continue;
                    }
                    matchedId = id;
                }
            }
            else
            {
                existingByName.TryGetValue(name, out existing);
            }

            string GetOrKeep(string column, string existingValue) =>
                indexOf[column] >= 0 ? Get(column) : existingValue;

            // Missing column keeps the stored value; present but empty clears it; anything else must be a whole number.
            bool TryOptionalInt(string column, int? existingValue, out int? value, out string? error)
            {
                error = null;
                value = existingValue;
                if (indexOf[column] < 0)
                {
                    return true;
                }

                var raw = Get(column);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    value = null;
                    return true;
                }

                if (int.TryParse(raw, out var parsed))
                {
                    value = parsed;
                    return true;
                }

                error = $"{column} '{raw}' is not a whole number.";
                return false;
            }

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

            var criticality = existing?.Criticality ?? InstanceCriticality.Unknown;
            var criticalityRaw = GetOrKeep("Criticality", "");
            if (!string.IsNullOrWhiteSpace(criticalityRaw) && !Enum.TryParse(criticalityRaw, true, out criticality))
            {
                errors.Add(new ImportRowError(rowNumber, $"Unrecognized criticality '{criticalityRaw}'. Expected one of: {string.Join(", ", Enum.GetNames<InstanceCriticality>())}."));
                continue;
            }

            if (!TryOptionalInt("ConnectivityPort", existing?.ConnectivityPortOverride, out var port, out var intError)
                || !TryOptionalInt("RestoreTestIntervalDays", existing?.RestoreTestIntervalDaysOverride, out var restoreInterval, out intError))
            {
                errors.Add(new ImportRowError(rowNumber, intError!));
                continue;
            }

            var ownerTeamId = existing?.OwnerTeamId;
            if (indexOf["OwnerTeam"] >= 0)
            {
                var teamName = Get("OwnerTeam");
                if (string.IsNullOrWhiteSpace(teamName))
                {
                    ownerTeamId = null;
                }
                else if (teamIdsByName is not null && teamIdsByName.TryGetValue(teamName, out var teamId))
                {
                    ownerTeamId = teamId;
                }
                else
                {
                    errors.Add(new ImportRowError(rowNumber, $"Team '{teamName}' doesn't exist. Create it on the Ownership page first, or leave OwnerTeam empty."));
                    continue;
                }
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
                Region = GetOrKeep("Region", existing?.Region ?? ""),
                Cluster = GetOrKeep("Cluster", existing?.Cluster ?? ""),
                ReplicaSet = GetOrKeep("ReplicaSet", existing?.ReplicaSet ?? ""),
                Criticality = criticality,
                OwnerTeamId = ownerTeamId,
                BackupContactName = GetOrKeep("BackupContactName", existing?.BackupContactName ?? ""),
                BackupContactEmail = GetOrKeep("BackupContactEmail", existing?.BackupContactEmail ?? ""),
                BackupContactPhone = GetOrKeep("BackupContactPhone", existing?.BackupContactPhone ?? ""),
                BackupFileNamePatterns = GetOrKeep("BackupFileNamePatterns", existing?.BackupFileNamePatterns ?? ""),
                ConnectivityHostOverride = GetOrKeep("ConnectivityHost", existing?.ConnectivityHostOverride ?? ""),
                ConnectivityPortOverride = port,
                RestoreTestIntervalDaysOverride = restoreInterval,
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

            if (matchRule == ImportMatchRule.Id)
            {
                if (matchedId is { } mid && !idsSeenInFile.Add(mid))
                {
                    rows.Add(new ImportRowPreview(rowNumber, ImportRowPlan.Ambiguous, null, null, $"Id {mid} appears more than once in this file."));
                    continue;
                }

                if (existing is { IsArchived: true })
                {
                    rows.Add(new ImportRowPreview(rowNumber, ImportRowPlan.Ambiguous, null, null, $"Instance #{existing.Id} '{existing.Name}' is archived. Unarchive it first if it should be updated."));
                    continue;
                }

                // The row's name must not collide with a different instance, whether this row creates or renames.
                var nameOwner = existingByName.TryGetValue(name, out var owner) ? owner : null;
                if (nameOwner is not null && nameOwner.Id != existing?.Id)
                {
                    rows.Add(new ImportRowPreview(rowNumber, ImportRowPlan.Ambiguous, null, null, existing is null
                        ? $"'{name}' already exists as instance #{nameOwner.Id}. Add its Id to update it, or use a different name."
                        : $"Can't rename #{existing.Id} to '{name}': instance #{nameOwner.Id} already has that name."));
                    continue;
                }

                if ((existing is null || !string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
                    && blockedNames is not null && blockedNames.TryGetValue(name, out var blockedForId))
                {
                    rows.Add(new ImportRowPreview(rowNumber, ImportRowPlan.Ambiguous, null, null, blockedForId));
                    continue;
                }
            }
            else if (blockedNames is not null && blockedNames.TryGetValue(name, out var blockedReason))
            {
                rows.Add(new ImportRowPreview(rowNumber, ImportRowPlan.Ambiguous, null, null, blockedReason));
                continue;
            }

            if (existing is not null)
            {
                candidate.Id = existing.Id;
                // Carry over the stored RowVersion; a fresh instance's empty token would fail EF's concurrency check.
                candidate.RowVersion = existing.RowVersion;
                var unchanged = IsUnchanged(existing, candidate) && (matchRule == ImportMatchRule.Name || existing.Name == candidate.Name);
                var plan = unchanged ? ImportRowPlan.Skip : ImportRowPlan.Update;
                rows.Add(new ImportRowPreview(rowNumber, plan, candidate, existing.Id, null));
            }
            else
            {
                rows.Add(new ImportRowPreview(rowNumber, ImportRowPlan.Create, candidate, null, null));
            }
        }

        return new ImportPreviewResult(rows, errors);
    }

    /// <summary>Copies only the columns this importer owns from a parsed row onto a stored instance. Everything else (owner team,
    /// facets, scan status, policy, archive state, creation time) is left as it is.</summary>
    /// <param name="includeName">True when matching by Id, where the file may rename the instance. Matching by Name never renames.</param>
    public static void ApplyImportedFields(DatabaseInstance target, DatabaseInstance imported, bool includeName = false)
    {
        if (includeName)
        {
            target.Name = imported.Name;
        }
        target.Engine = imported.Engine;
        target.Version = imported.Version;
        target.Application = imported.Application;
        target.Company = imported.Company;
        target.Department = imported.Department;
        target.ContactName = imported.ContactName;
        target.ContactEmail = imported.ContactEmail;
        target.ContactPhone = imported.ContactPhone;
        target.Role = imported.Role;
        target.Environment = imported.Environment;
        target.DatabaseIp = imported.DatabaseIp;
        target.ServerIp = imported.ServerIp;
        target.ExpectedBackupIntervalHours = imported.ExpectedBackupIntervalHours;
        target.BackupFileLocation = imported.BackupFileLocation;
        target.RetentionPolicyDays = imported.RetentionPolicyDays;
        target.Tags = imported.Tags;
        target.RunbookUrl = imported.RunbookUrl;
        target.Notes = imported.Notes;
        target.Region = imported.Region;
        target.Cluster = imported.Cluster;
        target.ReplicaSet = imported.ReplicaSet;
        target.Criticality = imported.Criticality;
        target.OwnerTeamId = imported.OwnerTeamId;
        target.BackupContactName = imported.BackupContactName;
        target.BackupContactEmail = imported.BackupContactEmail;
        target.BackupContactPhone = imported.BackupContactPhone;
        target.BackupFileNamePatterns = imported.BackupFileNamePatterns;
        target.ConnectivityHostOverride = imported.ConnectivityHostOverride;
        target.ConnectivityPortOverride = imported.ConnectivityPortOverride;
        target.RestoreTestIntervalDaysOverride = imported.RestoreTestIntervalDaysOverride;
    }

    /// <summary>A detached copy of the importer-owned fields plus Id and Name, used as the "before" side of the change log and audit.</summary>
    public static DatabaseInstance Clone(DatabaseInstance source)
    {
        var copy = new DatabaseInstance { Id = source.Id };
        ApplyImportedFields(copy, source, includeName: true);
        return copy;
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
        existing.Notes == candidate.Notes &&
        existing.Region == candidate.Region &&
        existing.Cluster == candidate.Cluster &&
        existing.ReplicaSet == candidate.ReplicaSet &&
        existing.Criticality == candidate.Criticality &&
        existing.OwnerTeamId == candidate.OwnerTeamId &&
        existing.BackupContactName == candidate.BackupContactName &&
        existing.BackupContactEmail == candidate.BackupContactEmail &&
        existing.BackupContactPhone == candidate.BackupContactPhone &&
        existing.BackupFileNamePatterns == candidate.BackupFileNamePatterns &&
        existing.ConnectivityHostOverride == candidate.ConnectivityHostOverride &&
        existing.ConnectivityPortOverride == candidate.ConnectivityPortOverride &&
        existing.RestoreTestIntervalDaysOverride == candidate.RestoreTestIntervalDaysOverride;
}
