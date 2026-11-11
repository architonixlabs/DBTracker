using System.Text.RegularExpressions;

namespace DBTracker.Core.Services;

public enum FileEligibility
{
    Eligible,
    PatternMismatch,
    ZeroLength,
    StillChanging
}

public record FileEvaluation(string FileName, long SizeBytes, DateTime LastWriteUtc, FileEligibility Eligibility)
{
    public bool IsEligible => Eligibility == FileEligibility.Eligible;

    public string Reason => Eligibility switch
    {
        FileEligibility.Eligible => "Matches configured rules",
        FileEligibility.PatternMismatch => "Name doesn't match the configured pattern(s)",
        FileEligibility.ZeroLength => "File is zero-length",
        FileEligibility.StillChanging => "File size/timestamp changed between checks — still being written",
        _ => ""
    };
}

/// <summary>Per-instance rules for which shared-drive files count as backup evidence: unmatched names, zero-length files and files still being written are excluded. Callers pass a second stat taken a moment later to detect in-progress writes.</summary>
public static class BackupFileFilter
{
    /// <summary>Default rule set: an empty pattern string matches every file.</summary>
    public const string MatchAllPatterns = "";

    public static FileEligibility Evaluate(
        string fileName,
        long sizeBytesFirstCheck,
        DateTime lastWriteUtcFirstCheck,
        long sizeBytesSecondCheck,
        DateTime lastWriteUtcSecondCheck,
        string patterns)
    {
        if (sizeBytesFirstCheck <= 0 || sizeBytesSecondCheck <= 0)
        {
            return FileEligibility.ZeroLength;
        }

        if (sizeBytesFirstCheck != sizeBytesSecondCheck || lastWriteUtcFirstCheck != lastWriteUtcSecondCheck)
        {
            return FileEligibility.StillChanging;
        }

        if (!MatchesPatterns(fileName, patterns))
        {
            return FileEligibility.PatternMismatch;
        }

        return FileEligibility.Eligible;
    }

    /// <summary>True if <paramref name="fileName"/> matches at least one of the semicolon/comma-separated glob patterns (e.g. "*.bak;*.trn"). An empty/whitespace pattern string matches everything.</summary>
    public static bool MatchesPatterns(string fileName, string patterns)
    {
        if (string.IsNullOrWhiteSpace(patterns))
        {
            return true;
        }

        var parts = patterns.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return true;
        }

        return parts.Any(p => Regex.IsMatch(fileName, GlobToRegex(p), RegexOptions.IgnoreCase));
    }

    private static string GlobToRegex(string glob)
    {
        var escaped = Regex.Escape(glob).Replace(@"\*", ".*").Replace(@"\?", ".");
        return $"^{escaped}$";
    }
}
