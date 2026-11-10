namespace DBTracker.Core.Enums;

/// <summary>Which way a technical dependency points (D07): what this instance relies on, vs. what relies on this instance.</summary>
public enum DependencyDirection
{
    /// <summary>This instance depends on the named thing (a driver, extension, linked service, etc.).</summary>
    Upstream,
    /// <summary>The named thing depends on this instance — a downstream consumer that would be affected by a shared-engine change.</summary>
    Downstream
}
