using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A curated product release and its support milestones, attributed to a source. Each date has its own precision, overrides carry a reason and expiry, and a failed refresh marks the row stale instead of clearing it.</summary>
public class ReleaseCatalogEntry
{
    public int Id { get; set; }

    public DatabaseEngine Engine { get; set; }

    /// <summary>Normalized product family, matched against VersionIdentity.Family (e.g. "PostgreSQL", "SQL Server", "MariaDB" for a MySQL-engine instance actually running MariaDB).</summary>
    public string Family { get; set; } = string.Empty;

    public string ReleaseLabel { get; set; } = string.Empty;
    public int? Major { get; set; }
    public int? Minor { get; set; }
    public string Build { get; set; } = string.Empty;

    public ReleaseChannel Channel { get; set; } = ReleaseChannel.GA;
    public string PatchTrain { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Distribution { get; set; } = string.Empty;

    /// <summary>Conditions under which this release/milestone set applies (e.g. "Azure Database for PostgreSQL only" vs. community upstream).</summary>
    public string ApplicabilityNotes { get; set; } = string.Empty;

    public DateTime? GaDateUtc { get; set; }
    public DatePrecision GaDatePrecision { get; set; } = DatePrecision.Exact;

    public DateTime? MainstreamSupportEndUtc { get; set; }
    public DatePrecision MainstreamSupportEndPrecision { get; set; } = DatePrecision.Exact;

    public DateTime? ExtendedSupportEndUtc { get; set; }
    public DatePrecision ExtendedSupportEndPrecision { get; set; } = DatePrecision.Exact;

    public DateTime? EsuSupportEndUtc { get; set; }
    public DatePrecision EsuSupportEndPrecision { get; set; } = DatePrecision.Exact;

    /// <summary>At least one of the dates above is explicitly "to be determined" rather than simply absent.</summary>
    public bool DatesAreTbd { get; set; }

    /// <summary>Retrieved source material disagreed on a date — surfaced explicitly rather than silently picking one.</summary>
    public bool DatesConflict { get; set; }

    public string DateTimezone { get; set; } = "UTC";

    public string SourceUrl { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public string ReleaseNotesUrl { get; set; } = string.Empty;
    public DateTime? RetrievedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string ReviewedBy { get; set; } = string.Empty;

    public bool IsManualOverride { get; set; }
    public string OverrideReason { get; set; } = string.Empty;
    public string OverrideReviewedBy { get; set; } = string.Empty;
    public DateTime? OverrideExpiresUtc { get; set; }

    /// <summary>Set when a refresh attempt (ReleaseCatalogService.RefreshAsync) failed — last-known facts above are retained unchanged; this is just the staleness warning.</summary>
    public bool IsStale { get; set; }
    public DateTime? LastRefreshAttemptUtc { get; set; }
    public string LastRefreshError { get; set; } = string.Empty;

    /// <summary>Bumped on every edit/override/refresh; paired with a ReleaseCatalogRevision row recording what changed.</summary>
    public int RevisionNumber { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;

    public ICollection<ReleaseCatalogRevision> Revisions { get; set; } = new List<ReleaseCatalogRevision>();
}
