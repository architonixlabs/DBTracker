namespace DBTracker.Core.Enums;

/// <summary>What kind of dependency an InstanceDependency row describes.</summary>
public enum DependencyKind
{
    Application,
    Deployment,
    Node,
    Driver,
    Extension,
    Job,
    LinkedService,
    Replica,
    BackupTooling,
    /// <summary>A downstream consumer of this instance, not something it depends on.</summary>
    Consumer,
    Other,
    /// <summary>A database hosted on this instance, e.g. one database on a shared SQL Server. Counted in the lifecycle workspace.</summary>
    HostedDatabase
}
