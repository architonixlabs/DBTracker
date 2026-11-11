namespace DBTracker.Core.Enums;

/// <summary>How complete and confirmed an instance's ownership information is. Informational only; it never affects authorization.</summary>
public enum OwnershipStatus
{
    /// <summary>No owner team and no contact email: nobody to call.</summary>
    Missing,

    /// <summary>Some owner/contact information exists but has never been explicitly confirmed current.</summary>
    Unconfirmed,

    /// <summary>An owner explicitly confirmed the contact/team information is current.</summary>
    Confirmed
}
