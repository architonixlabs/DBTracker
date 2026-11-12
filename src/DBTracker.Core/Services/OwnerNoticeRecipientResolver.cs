namespace DBTracker.Core.Services;

/// <summary>Who an owner-directed notice goes to, and a plain description of that choice for the message and audit trail.</summary>
public record OwnerNoticeRecipients(IReadOnlyList<string> Emails, string RoutedTo, bool UsedFallback);

/// <summary>Routes a notice to the record's own owner first: the owner's email, then the owner team's primary and backup contacts and its
/// email subscribers. The fallback contact (usually the instance contact) is used only when no owner address exists, and the result says so.</summary>
public static class OwnerNoticeRecipientResolver
{
    public static OwnerNoticeRecipients Resolve(
        string? ownerEmail,
        string? ownerTeamName,
        IEnumerable<string?> ownerTeamContactEmails,
        IEnumerable<string?> ownerTeamSubscriberEmails,
        string? fallbackEmail,
        string fallbackLabel)
    {
        var emails = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? email)
        {
            var trimmed = email?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && trimmed.Contains('@') && seen.Add(trimmed))
            {
                emails.Add(trimmed);
            }
        }

        Add(ownerEmail);
        var hadOwnerEmail = emails.Count > 0;
        foreach (var email in ownerTeamContactEmails)
        {
            Add(email);
        }
        foreach (var email in ownerTeamSubscriberEmails)
        {
            Add(email);
        }

        if (emails.Count > 0)
        {
            var parts = new List<string>();
            if (hadOwnerEmail)
            {
                parts.Add("the recorded owner");
            }
            if (!string.IsNullOrWhiteSpace(ownerTeamName) && emails.Count > (hadOwnerEmail ? 1 : 0))
            {
                parts.Add($"team {ownerTeamName}");
            }
            return new OwnerNoticeRecipients(emails, string.Join(" and ", parts), false);
        }

        Add(fallbackEmail);
        return emails.Count > 0
            ? new OwnerNoticeRecipients(emails, $"{fallbackLabel}, because no owner email or owner team contact is recorded", true)
            : new OwnerNoticeRecipients(emails, "nobody by email: no owner, team or fallback contact address is recorded", true);
    }
}
