namespace DBTracker.Core.Enums;

/// <summary>What a CertificateDependency (D20) actually protects — kept distinct since a TLS certificate expiring and an encryption/recovery key rotation lapsing are different operational risks with different owners.</summary>
public enum CertificateKind
{
    Tls,
    RecoveryKeyOrEncryptionCertificate
}
