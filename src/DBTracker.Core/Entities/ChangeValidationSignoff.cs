using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One reviewer's sign-off on a D13 ChangeValidation, within their own named scope (e.g. "DBA", "Application owner", "Security") — append-only history, never overwritten by a later sign-off from someone else.</summary>
public class ChangeValidationSignoff
{
    public int Id { get; set; }

    public int ChangeValidationId { get; set; }
    public ChangeValidation? ChangeValidation { get; set; }

    public string ReviewerScope { get; set; } = string.Empty;
    public string ReviewerName { get; set; } = string.Empty;
    public SignoffOutcome Outcome { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime SignedAtUtc { get; set; } = DateTime.UtcNow;
}
