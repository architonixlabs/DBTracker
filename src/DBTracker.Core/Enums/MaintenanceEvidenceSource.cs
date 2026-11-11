namespace DBTracker.Core.Enums;

/// <summary>Where a maintenance result came from. A reviewed external monitoring or ticket link is a valid source.</summary>
public enum MaintenanceEvidenceSource
{
    ManuallyEntered,
    ExternalMonitoringLink,
    TicketLink
}
