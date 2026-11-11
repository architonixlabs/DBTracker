namespace DBTracker.Core.Enums;

/// <summary>What a certificate history entry records. A renewal changes the expiry date; a key rotation replaces key material and may leave the expiry unchanged.</summary>
public enum CertificateEventType
{
    Renewal,
    KeyRotation
}
