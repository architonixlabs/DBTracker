namespace DBTracker.Infrastructure.Identity;

/// <summary>
/// RBAC roles. DBA/Admin can write (create/edit instances, log backups); ITManager, Auditor and
/// Viewer are read-only. Only Admin manages users.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Dba = "DBA";
    public const string ItManager = "ITManager";
    public const string Auditor = "Auditor";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Dba, ItManager, Auditor, Viewer];

    /// <summary>Roles allowed to create/edit instances and log backup results.</summary>
    public const string CanEditPolicy = $"{Admin},{Dba}";

    /// <summary>Roles allowed to trigger/view the management compliance report — leadership-facing, not DBA-only.</summary>
    public const string ReportingPolicy = $"{Admin},{ItManager},{Auditor}";

    /// <summary>Roles allowed to view the general audit trail (U19) — Angela's (Auditor) core need, plus Admin for account/administration events.</summary>
    public const string AuditViewPolicy = $"{Admin},{Auditor}";

    /// <summary>Roles allowed to see/record commercial evidence (D21 support-contract cost figures and edition-cost estimates) — deliberately narrower than CanEditPolicy: a DBA can record a contract/coverage fact but the dollar figures are Admin/ITManager-only, never every Viewer.</summary>
    public const string CommercialEvidencePolicy = $"{Admin},{ItManager}";
}
