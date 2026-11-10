using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

public record FieldError(string Field, string Message);

/// <summary>
/// Shared validation for a DatabaseInstance — used by the create/edit form (U13) and the CSV
/// importer (U17) so the two never drift: a row that would be rejected by one is rejected by the
/// other. Pure (no I/O, no DB round-trip), returns every field error found rather than stopping at
/// the first one, so a form can show field-level errors plus a summary in one pass.
/// </summary>
public static class InstanceValidator
{
    // Mirrors the HasMaxLength(...) values configured in DBTrackerDbContext — kept here instead of
    // reading the model at runtime so this stays pure and testable without EF Core.
    private const int NameMaxLength = 200;
    private const int VersionMaxLength = 50;
    private const int ShortFieldMaxLength = 200;
    private const int EmailMaxLength = 320;
    private const int PhoneMaxLength = 50;
    private const int IpMaxLength = 100;
    private const int NotesMaxLength = 2000;
    private const int PathMaxLength = 500;
    private const int PatternsMaxLength = 300;
    private const int TagsMaxLength = 300;
    private const int UrlMaxLength = 500;

    private static readonly string[] AllowedLinkSchemes = ["http", "https"];

    public static IReadOnlyList<FieldError> Validate(DatabaseInstance instance)
    {
        var errors = new List<FieldError>();

        void Required(string field, string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new FieldError(field, $"{label} is required."));
            }
        }

        void MaxLen(string field, string value, int max, string label)
        {
            if (!string.IsNullOrEmpty(value) && value.Length > max)
            {
                errors.Add(new FieldError(field, $"{label} must be {max} characters or fewer."));
            }
        }

        Required(nameof(instance.Name), instance.Name, "Instance name");
        MaxLen(nameof(instance.Name), instance.Name, NameMaxLength, "Instance name");

        if (!Enum.IsDefined(instance.Engine))
        {
            errors.Add(new FieldError(nameof(instance.Engine), "Engine is not a recognized value."));
        }
        if (!Enum.IsDefined(instance.Role))
        {
            errors.Add(new FieldError(nameof(instance.Role), "Role is not a recognized value."));
        }
        if (!Enum.IsDefined(instance.Environment))
        {
            errors.Add(new FieldError(nameof(instance.Environment), "Environment is not a recognized value."));
        }

        MaxLen(nameof(instance.Version), instance.Version, VersionMaxLength, "Version");
        MaxLen(nameof(instance.Application), instance.Application, ShortFieldMaxLength, "Application");
        MaxLen(nameof(instance.Company), instance.Company, ShortFieldMaxLength, "Company");
        MaxLen(nameof(instance.Department), instance.Department, ShortFieldMaxLength, "Department");
        MaxLen(nameof(instance.ContactName), instance.ContactName, ShortFieldMaxLength, "Contact name");
        MaxLen(nameof(instance.ContactEmail), instance.ContactEmail, EmailMaxLength, "Contact email");
        MaxLen(nameof(instance.ContactPhone), instance.ContactPhone, PhoneMaxLength, "Contact phone");
        MaxLen(nameof(instance.DatabaseIp), instance.DatabaseIp, IpMaxLength, "Database IP/host");
        MaxLen(nameof(instance.ServerIp), instance.ServerIp, IpMaxLength, "Server IP/host");
        MaxLen(nameof(instance.Notes), instance.Notes, NotesMaxLength, "Notes");
        MaxLen(nameof(instance.BackupFileLocation), instance.BackupFileLocation, PathMaxLength, "Backup file location");
        MaxLen(nameof(instance.BackupFileNamePatterns), instance.BackupFileNamePatterns, PatternsMaxLength, "Backup file name pattern(s)");
        MaxLen(nameof(instance.Tags), instance.Tags, TagsMaxLength, "Tags");
        MaxLen(nameof(instance.RunbookUrl), instance.RunbookUrl, UrlMaxLength, "Runbook / monitoring link");

        if (!string.IsNullOrEmpty(instance.ContactEmail) && !LooksLikeEmail(instance.ContactEmail))
        {
            errors.Add(new FieldError(nameof(instance.ContactEmail), "Contact email is not a valid email address."));
        }

        if (instance.ExpectedBackupIntervalHours <= 0)
        {
            errors.Add(new FieldError(nameof(instance.ExpectedBackupIntervalHours), "Expected backup interval must be a positive number of hours."));
        }

        if (instance.RetentionPolicyDays is { } retention && retention < 0)
        {
            errors.Add(new FieldError(nameof(instance.RetentionPolicyDays), "Retention policy can't be negative."));
        }

        if (instance.RestoreTestIntervalDaysOverride is { } restoreInterval && restoreInterval <= 0)
        {
            errors.Add(new FieldError(nameof(instance.RestoreTestIntervalDaysOverride), "Restore-test interval override must be a positive number of days."));
        }

        if (!string.IsNullOrWhiteSpace(instance.RunbookUrl) && !HasAllowedScheme(instance.RunbookUrl))
        {
            errors.Add(new FieldError(nameof(instance.RunbookUrl), "Runbook / monitoring link must be a valid http:// or https:// URL."));
        }

        return errors;
    }

    /// <summary>Nonnegative-measurement validation for a backup/restore log entry — shared with the manual entry forms (U11/U13).</summary>
    public static IReadOnlyList<FieldError> ValidateMeasurements(decimal? sizeMb, int? durationMinutes)
    {
        var errors = new List<FieldError>();
        if (sizeMb is { } s && s < 0)
        {
            errors.Add(new FieldError("SizeMb", "Size can't be negative."));
        }
        if (durationMinutes is { } d && d < 0)
        {
            errors.Add(new FieldError("DurationMinutes", "Duration can't be negative."));
        }
        return errors;
    }

    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && !value.Contains(' ');
    }

    private static bool HasAllowedScheme(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && AllowedLinkSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
}
