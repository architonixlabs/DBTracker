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
public record FilePreview(string FileName, long SizeBytes, DateTime LastWriteUtc, FileEligibility Eligibility, string Reason, bool AlreadyKnown, BackupType RecordedAs);

/// <summary>Scans an instance's backup location and records eligible files not already known as Discovered records. Matching is by
/// name, exact size and last-write time, so a file rewritten under the same name counts as new evidence even at the same size.
/// The backup type comes from the file name (BackupFileTypeClassifier), so a .trn log backup isn't recorded as a full one.</summary>
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

                if (known.Signatures.Contains((file.FileName, file.SizeBytes, file.LastWriteUtc)))
                {
                    continue; // unchanged — already recorded, never duplicated.
                }

                var record = new BackupRecord
                {
                    DatabaseInstanceId = instanceId,
                    TimestampUtc = file.LastWriteUtc,
                    Type = BackupFileTypeClassifier.ClassifyForChain(file.FileName),
                    ChainRole = BackupFileTypeClassifier.ClassifyChainRole(file.FileName),
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
                    // A concurrent scan already recorded this exact (name, size, write time) signature.
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

        var known = instanceId is null ? null : await LoadKnownSignaturesAsync(instanceId.Value, ct);

        return evaluations
            .Select(f => new FilePreview(f.FileName, f.SizeBytes, f.LastWriteUtc, f.Eligibility, f.Reason,
                known is not null && (known.Signatures.Contains((f.FileName, f.SizeBytes, f.LastWriteUtc)) || known.NamesWithUnknownSignature.Contains(f.FileName)),
                BackupFileTypeClassifier.Classify(f.FileName)))
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
            .Select(r => new { r.SourceFile, r.ObservedFileBytes, r.TimestampUtc })
            .ToListAsync(ct);

        var namesWithUnknownSignature = new HashSet<string>(
            rows.Where(r => r.ObservedFileBytes is null).Select(r => r.SourceFile),
            StringComparer.OrdinalIgnoreCase);

        // Discovered records store the file's last-write time as TimestampUtc, so records from before this signature included it still match.
        var signatures = new HashSet<(string Name, long Bytes, DateTime LastWriteUtc)>(
            rows.Where(r => r.ObservedFileBytes is not null).Select(r => (r.SourceFile, r.ObservedFileBytes!.Value, r.TimestampUtc)));

        return new KnownSignatures(namesWithUnknownSignature, signatures);
    }

    private sealed record KnownSignatures(HashSet<string> NamesWithUnknownSignature, HashSet<(string Name, long Bytes, DateTime LastWriteUtc)> Signatures);

    /// <summary>Name-only matching, still used for older Discovered records with no byte signature.</summary>
    public static IEnumerable<string> FindNewFiles(IEnumerable<string> alreadyKnownFileNames, IEnumerable<string> fileNamesOnDisk)
    {
        var known = new HashSet<string>(alreadyKnownFileNames, StringComparer.OrdinalIgnoreCase);
        return fileNamesOnDisk.Where(f => !known.Contains(f));
    }
}
