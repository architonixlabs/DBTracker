using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupFileFilterTests
{
    private static readonly DateTime T = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StableUnchangedFile_IsEligible_WhenPatternMatchesAndNonZero()
    {
        var result = BackupFileFilter.Evaluate("full.bak", 1024, T, 1024, T, BackupFileFilter.MatchAllPatterns);
        Assert.Equal(FileEligibility.Eligible, result);
    }

    [Fact]
    public void ZeroLengthFile_IsExcluded()
    {
        var result = BackupFileFilter.Evaluate("empty.bak", 0, T, 0, T, BackupFileFilter.MatchAllPatterns);
        Assert.Equal(FileEligibility.ZeroLength, result);
    }

    [Fact]
    public void SizeChangedBetweenChecks_IsStillChanging()
    {
        var result = BackupFileFilter.Evaluate("growing.bak", 1024, T, 2048, T, BackupFileFilter.MatchAllPatterns);
        Assert.Equal(FileEligibility.StillChanging, result);
    }

    [Fact]
    public void TimestampChangedBetweenChecks_IsStillChanging()
    {
        var result = BackupFileFilter.Evaluate("growing.bak", 1024, T, 1024, T.AddSeconds(1), BackupFileFilter.MatchAllPatterns);
        Assert.Equal(FileEligibility.StillChanging, result);
    }

    [Fact]
    public void NonMatchingPattern_IsExcluded()
    {
        var result = BackupFileFilter.Evaluate("readme.txt", 1024, T, 1024, T, "*.bak;*.trn");
        Assert.Equal(FileEligibility.PatternMismatch, result);
    }

    [Fact]
    public void MatchingPattern_IsEligible()
    {
        var result = BackupFileFilter.Evaluate("full-2026-11-10.trn", 1024, T, 1024, T, "*.bak;*.trn");
        Assert.Equal(FileEligibility.Eligible, result);
    }

    [Theory]
    [InlineData("backup.bak", "*.BAK", true)]
    [InlineData("BACKUP.BAK", "*.bak", true)]
    public void PatternMatching_IsCaseInsensitive(string fileName, string pattern, bool expectedMatch)
    {
        Assert.Equal(expectedMatch, BackupFileFilter.MatchesPatterns(fileName, pattern));
    }

    [Fact]
    public void EmptyPattern_MatchesEverything()
    {
        Assert.True(BackupFileFilter.MatchesPatterns("anything.xyz", ""));
    }

    [Fact]
    public void MultiplePatterns_MatchIfAnyOneMatches()
    {
        Assert.True(BackupFileFilter.MatchesPatterns("db.trn", "*.bak, *.trn"));
        Assert.False(BackupFileFilter.MatchesPatterns("db.log", "*.bak, *.trn"));
    }

    [Fact]
    public void ZeroLengthWinsOverPatternMismatch_SoReasonIsAccurate()
    {
        // Zero-length is checked before pattern matching — the user should see "zero-length",
        // not a misleading "doesn't match pattern" for a file whose name would have matched fine.
        var result = BackupFileFilter.Evaluate("readme.txt", 0, T, 0, T, "*.bak");
        Assert.Equal(FileEligibility.ZeroLength, result);
    }
}
