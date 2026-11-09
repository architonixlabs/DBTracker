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
}
