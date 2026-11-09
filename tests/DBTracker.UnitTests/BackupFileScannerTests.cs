using DBTracker.Infrastructure.Scanning;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupFileScannerTests
{
    [Fact]
    public void NoExistingFiles_AllDiskFilesAreNew()
    {
        var result = BackupFileScanner.FindNewFiles(
            alreadyKnownFileNames: Array.Empty<string>(),
            fileNamesOnDisk: ["backup-2026-11-01.bak", "backup-2026-11-02.bak"]);

        Assert.Equal(["backup-2026-11-01.bak", "backup-2026-11-02.bak"], result.OrderBy(f => f));
    }

    [Fact]
    public void FilesAlreadyKnown_AreExcluded()
    {
        var result = BackupFileScanner.FindNewFiles(
            alreadyKnownFileNames: ["backup-2026-11-01.bak"],
            fileNamesOnDisk: ["backup-2026-11-01.bak", "backup-2026-11-02.bak"]);

        Assert.Equal(["backup-2026-11-02.bak"], result);
    }

    [Fact]
    public void AllFilesAlreadyKnown_ReturnsEmpty()
    {
        var result = BackupFileScanner.FindNewFiles(
            alreadyKnownFileNames: ["a.bak", "b.bak"],
            fileNamesOnDisk: ["a.bak", "b.bak"]);

        Assert.Empty(result);
    }

    [Fact]
    public void MatchingIsCaseInsensitive()
    {
        var result = BackupFileScanner.FindNewFiles(
            alreadyKnownFileNames: ["Backup-2026-11-01.BAK"],
            fileNamesOnDisk: ["backup-2026-11-01.bak"]);

        Assert.Empty(result);
    }

    [Fact]
    public void EmptyDiskListing_ReturnsEmpty()
    {
        var result = BackupFileScanner.FindNewFiles(
            alreadyKnownFileNames: ["a.bak"],
            fileNamesOnDisk: Array.Empty<string>());

        Assert.Empty(result);
    }
}
