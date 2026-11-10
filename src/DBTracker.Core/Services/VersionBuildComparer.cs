using System.Text.RegularExpressions;

namespace DBTracker.Core.Services;

/// <summary>
/// Compares two build/version strings numerically segment-by-segment (e.g. "2000.5" &lt; "4000.1",
/// "9" &lt; "10") instead of lexicographically, which would wrongly rank "9" above "10". Non-numeric
/// segments compare as equal-weight ordinal text so a mixed string still orders sensibly. Pure and
/// engine-agnostic — used by PatchBaselineComparer and AdvisoryApplicabilityClassifier.
/// </summary>
public static class VersionBuildComparer
{
    public static int Compare(string? a, string? b)
    {
        var segmentsA = Split(a);
        var segmentsB = Split(b);
        var length = Math.Max(segmentsA.Count, segmentsB.Count);

        for (var i = 0; i < length; i++)
        {
            var sa = i < segmentsA.Count ? segmentsA[i] : "";
            var sb = i < segmentsB.Count ? segmentsB[i] : "";

            if (int.TryParse(sa, out var na) && int.TryParse(sb, out var nb))
            {
                var cmp = na.CompareTo(nb);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
            else
            {
                var cmp = string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
        }

        return 0;
    }

    public static bool IsInRange(string? value, string? min, string? max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(min) && Compare(value, min) < 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(max) && Compare(value, max) > 0)
        {
            return false;
        }

        return true;
    }

    private static List<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : Regex.Split(value, @"[.\-_]").Where(s => s.Length > 0).ToList();
}
