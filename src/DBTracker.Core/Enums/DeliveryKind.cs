namespace DBTracker.Core.Enums;

/// <summary>What a send attempt was for.</summary>
public enum DeliveryKind
{
    ComplianceReport,
    Alert,
    Recovery,
    TestEmail,
    SupportRunwayNotice
}
