namespace DBTracker.Core.Enums;

/// <summary>What a certificate dependency protects. TLS expiry and key-rotation lapses are different risks with different owners.</summary>
public enum CertificateKind
{
    Tls,
    RecoveryKeyOrEncryptionCertificate
}
