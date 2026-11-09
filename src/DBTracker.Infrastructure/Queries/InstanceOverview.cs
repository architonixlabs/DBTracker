using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Infrastructure.Queries;

public class InstanceOverview
{
    public required DatabaseInstance Instance { get; init; }
    public BackupHealth Health { get; init; }
    public DateTime? LastBackupUtc { get; init; }
    public BackupRecord? LastRecord { get; init; }
}
