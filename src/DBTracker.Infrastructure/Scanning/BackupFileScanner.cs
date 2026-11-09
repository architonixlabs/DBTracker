using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Scanning;

public enum ScanOutcome
{
    InstanceNotFound,
    LocationNotConfigured,
    Success,
    Error
}

public record ScanResult(ScanOutcome Outcome, int FilesDiscovered, string? ErrorMessage = null);

/// <summary>
/// Scans an instance's configured backup file location (UNC path or local folder) and records any
/// file not already known as a Discovered BackupRecord. Safe to call repeatedly — already-seen
/// files (matched by name) are skipped, so re-scanning never duplicates history.
/// </summary>
public class BackupFileScanner(DBTrackerDbContext db)
{
    public async Task<ScanResult> ScanAsync(int instanceId, CancellationToken ct = default)
    {
        var instance = await db.DatabaseInstances.FindAsync([instanceId], ct);
        if (instance is null)
        {
            return new ScanResult(ScanOutcome.InstanceNotFound, 0);
        }

        if (string.IsNullOrWhiteSpace(instance.BackupFileLocation))
        {
            return new ScanResult(ScanOutcome.LocationNotConfigured, 0);
        }

        var now = DateTime.UtcNow;

        try
        {
            if (!Directory.Exists(instance.BackupFileLocation))
            {
                throw new DirectoryNotFoundException($"'{instance.BackupFileLocation}' does not exist or is not reachable.");
            }

            var filesOnDisk = Directory.GetFiles(instance.BackupFileLocation)
                .Select(Path.GetFileName)
                .Where(f => f is not null)
                .Cast<string>()
                .ToList();

            var alreadyKnown = await db.BackupRecords
                .Where(r => r.DatabaseInstanceId == instanceId && r.Source == BackupSource.Discovered)
                .Select(r => r.SourceFile)
                .ToListAsync(ct);

            var newFileNames = FindNewFiles(alreadyKnown, filesOnDisk).ToList();

            foreach (var fileName in newFileNames)
            {
                var fullPath = Path.Combine(instance.BackupFileLocation, fileName);
                var info = new FileInfo(fullPath);

                db.BackupRecords.Add(new BackupRecord
                {
                    DatabaseInstanceId = instanceId,
                    TimestampUtc = info.LastWriteTimeUtc,
                    Type = BackupType.Full,
                    Status = BackupStatus.Success,
                    SizeMb = Math.Round(info.Length / 1024.0 / 1024.0, 1),
                    Source = BackupSource.Discovered,
                    SourceFile = fileName,
                    Notes = $"Discovered on shared drive: {fileName}"
                });
            }

            instance.LastScanUtc = now;
            instance.LastScanStatus = ScanStatus.Success;
            instance.LastScanError = string.Empty;
            await db.SaveChangesAsync(ct);

            return new ScanResult(ScanOutcome.Success, newFileNames.Count);
        }
        catch (Exception ex)
        {
            instance.LastScanUtc = now;
            instance.LastScanStatus = ScanStatus.Error;
            instance.LastScanError = ex.Message;
            await db.SaveChangesAsync(ct);

            return new ScanResult(ScanOutcome.Error, 0, ex.Message);
        }
    }

    /// <summary>Pure matching logic, split out so it's unit-testable without touching the file system or a database.</summary>
    public static IEnumerable<string> FindNewFiles(IEnumerable<string> alreadyKnownFileNames, IEnumerable<string> fileNamesOnDisk)
    {
        var known = new HashSet<string>(alreadyKnownFileNames, StringComparer.OrdinalIgnoreCase);
        return fileNamesOnDisk.Where(f => !known.Contains(f));
    }
}
