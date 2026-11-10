namespace DBTracker.Core.Enums;

/// <summary>Where a MaintenanceEvidenceRecord's (D18) result came from — a reviewed external monitoring/ticket link is a valid first-class source, not just manually-typed results.</summary>
public enum MaintenanceEvidenceSource
{
    ManuallyEntered,
    ExternalMonitoringLink,
    TicketLink
}
