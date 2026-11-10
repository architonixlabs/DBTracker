namespace DBTracker.Core.Enums;

/// <summary>
/// How complete and confirmed an instance's ownership information is (U21). Purely informational —
/// never affects what a role is authorized to see or do; it only flags instances that need an owner
/// to look at the contact/team fields.
/// </summary>
public enum OwnershipStatus
{
    /// <summary>No owner team and no contact email of any kind — nobody to call.</summary>
    Missing,

    /// <summary>Some owner/contact information exists but has never been explicitly confirmed current.</summary>
    Unconfirmed,

    /// <summary>An owner explicitly confirmed the contact/team information is current.</summary>
    Confirmed
}
