namespace DBTracker.Infrastructure.Identity;

/// <summary>RBAC roles. DBA and Admin write; ITManager, Auditor and Viewer are read-only; only Admin manages users.</summary>
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

    /// <summary>Roles allowed to view and send the management compliance report.</summary>
    public const string ReportingPolicy = $"{Admin},{ItManager},{Auditor}";

    /// <summary>Roles allowed to view the general audit trail.</summary>
    public const string AuditViewPolicy = $"{Admin},{Auditor}";

    /// <summary>Roles allowed to record support-contract facts (vendor, coverage, dates; no amounts): the DBA who knows the
    /// product and the IT manager who owns the vendor relationship.</summary>
    public const string ContractEvidencePolicy = $"{Admin},{Dba},{ItManager}";

    /// <summary>Roles allowed to see cost figures. Narrower than ContractEvidencePolicy: a DBA can record a contract, but amounts are Admin/ITManager only.</summary>
    public const string CommercialEvidencePolicy = $"{Admin},{ItManager}";
}
