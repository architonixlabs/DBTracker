namespace DBTracker.Core.Enums;

/// <summary>What kind of technical dependency one InstanceDependency row describes (D07).</summary>
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
    /// <summary>A downstream consumer of this instance — something that depends on it, not something it depends on.</summary>
    Consumer,
    Other
}
