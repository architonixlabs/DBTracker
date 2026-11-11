namespace DBTracker.Core.Enums;

/// <summary>Which way a dependency points: what this instance relies on, or what relies on it.</summary>
public enum DependencyDirection
{
    /// <summary>This instance depends on the named thing (a driver, extension, linked service, etc.).</summary>
    Upstream,
    /// <summary>The named thing depends on this instance: a downstream consumer affected by a shared-engine change.</summary>
    Downstream
}
