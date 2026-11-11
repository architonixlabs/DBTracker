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
    Other
}
