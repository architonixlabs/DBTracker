namespace DBTracker.Core.Enums;

/// <summary>A catalogued release's channel (D02). Previews are excluded from automatic patch-baseline recommendations by default (D04).</summary>
public enum ReleaseChannel
{
    GA,
    Preview,

    /// <summary>A named support channel/track, e.g. MySQL LTS vs Innovation, rather than a single numbered release.</summary>
    Channel,

    /// <summary>A patch/build/CU within an already-GA branch.</summary>
    Patch
}
