using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
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

/// <summary>A file seen at a backup location, with the eligibility decision the scan or preview made.</summary>
public record FilePreview(string FileName, long SizeBytes, DateTime LastWriteUtc, FileEligibility Eligibility, string Reason, bool AlreadyKnown);

/// <summary>Scans an instance's backup location and records eligible files not already known as Discovered records. Matching is by name and exact size, so a changed file reusing a name counts as new evidence.</summary>
/// <remarks>A unique index plus the per-row catch below stops concurrent scans double-inserting the same file.</remarks>
public class BackupFileScanner(DBTrackerDbContext db)
{
    /// <summary>Delay between the two stability checks used to decide a file isn't still being written.</summary>
    public static readonly TimeSpan StabilityCheckDelay = TimeSpan.FromMilliseconds(250);

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
            var evaluations = await EvaluateFilesAsync(instance.BackupFileLocation, instance.BackupFileNamePatterns, ct);

            var known = await LoadKnownSignaturesAsync(instanceId, ct);

            var discoveredCount = 0;
            foreach (var file in evaluations.Where(f => f.IsEligible))
            {
                if (known.NamesWithUnknownSignature.Contains(file.FileName))
                {
                    continue; // legacy Discovered record for this name — grandfathered, not re-recorded.
                }

                if (known.Signatures.Contains((file.FileName, file.SizeBytes)))
                {
                    continue; // unchanged — already recorded, never duplicated.
                }

                var record = new BackupRecord
                {
                    DatabaseInstanceId = instanceId,
                    TimestampUtc = file.LastWriteUtc,
                    Type = BackupType.Full,
                    Status = BackupStatus.Success,
                    SizeMb = Math.Round(file.SizeBytes / 1024.0 / 1024.0, 1),
                    ObservedFileBytes = file.SizeBytes,
                    Source = BackupSource.Discovered,
                    SourceFile = file.FileName,
                    Notes = $"Discovered on shared drive: {file.FileName}"
                };

                db.BackupRecords.Add(record);
                try
                {
                    await db.SaveChangesAsync(ct);
                    discoveredCount++;
                }
                catch (DbUpdateException)
                {
                    // A concurrent scan already recorded this exact (name, size) signature.
                    db.Entry(record).State = EntityState.Detached;
                }
            }

            instance.LastScanUtc = now;
            instance.LastScanStatus = ScanStatus.Success;
            instance.LastScanError = string.Empty;
            await db.SaveChangesAsync(ct);

            return new ScanResult(ScanOutcome.Success, discoveredCount);
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

    /// <summary>Dry run of a scan, so rules can be previewed before saving.</summary>
    public async Task<IReadOnlyList<FilePreview>> PreviewAsync(string backupFileLocation, string backupFileNamePatterns, int? instanceId = null, CancellationToken ct = default)
    {
        var evaluations = await EvaluateFilesAsync(backupFileLocation, backupFileNamePatterns, ct);

        var knownNames = instanceId is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await LoadKnownSignaturesAsync(instanceId.Value, ct)).AllKnownNames;

        return evaluations
            .Select(f => new FilePreview(f.FileName, f.SizeBytes, f.LastWriteUtc, f.Eligibility, f.Reason, knownNames.Contains(f.FileName)))
            .ToList();
    }

    private static async Task<List<FileEvaluation>> EvaluateFilesAsync(string backupFileLocation, string backupFileNamePatterns, CancellationToken ct)
    {
        if (!Directory.Exists(backupFileLocation))
        {
            throw new DirectoryNotFoundException($"'{backupFileLocation}' does not exist or is not reachable.");
        }

        var fileNames = Directory.GetFiles(backupFileLocation)
            .Select(Path.GetFileName)
            .Where(f => f is not null)
            .Cast<string>()
            .ToList();

        var firstStats = fileNames.ToDictionary(f => f, f => Stat(backupFileLocation, f));
        await Task.Delay(StabilityCheckDelay, ct);
        var secondStats = fileNames.ToDictionary(f => f, f => Stat(backupFileLocation, f));

        return fileNames
            .Select(f =>
            {
                var (size1, mtime1) = firstStats[f];
                var (size2, mtime2) = secondStats[f];
                var eligibility = BackupFileFilter.Evaluate(f, size1, mtime1, size2, mtime2, backupFileNamePatterns);
                return new FileEvaluation(f, size2, mtime2, eligibility);
            })
            .ToList();
    }

    private static (long Size, DateTime LastWriteUtc) Stat(string directory, string fileName)
    {
        var info = new FileInfo(Path.Combine(directory, fileName));
        return (info.Length, info.LastWriteTimeUtc);
    }

    private async Task<KnownSignatures> LoadKnownSignaturesAsync(int instanceId, CancellationToken ct)
    {
        var rows = await db.BackupRecords
            .Where(r => r.DatabaseInstanceId == instanceId && r.Source == BackupSource.Discovered)
            .Select(r => new { r.SourceFile, r.ObservedFileBytes })
            .ToListAsync(ct);

        var namesWithUnknownSignature = new HashSet<string>(
            rows.Where(r => r.ObservedFileBytes is null).Select(r => r.SourceFile),
            StringComparer.OrdinalIgnoreCase);

        var signatures = new HashSet<(string Name, long Bytes)>(
            rows.Where(r => r.ObservedFileBytes is not null).Select(r => (r.SourceFile, r.ObservedFileBytes!.Value)));

        var allKnownNames = new HashSet<string>(rows.Select(r => r.SourceFile), StringComparer.OrdinalIgnoreCase);

        return new KnownSignatures(namesWithUnknownSignature, signatures, allKnownNames);
    }

    private sealed record KnownSignatures(HashSet<string> NamesWithUnknownSignature, HashSet<(string Name, long Bytes)> Signatures, HashSet<string> AllKnownNames);

    /// <summary>Name-only matching, still used for older Discovered records with no byte signature.</summary>
    public static IEnumerable<string> FindNewFiles(IEnumerable<string> alreadyKnownFileNames, IEnumerable<string> fileNamesOnDisk)
    {
        var known = new HashSet<string>(alreadyKnownFileNames, StringComparer.OrdinalIgnoreCase);
        return fileNamesOnDisk.Where(f => !known.Contains(f));
    }
}
