using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One reviewer's sign-off within their named scope (e.g. "DBA", "Security"). Append-only.</summary>
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
