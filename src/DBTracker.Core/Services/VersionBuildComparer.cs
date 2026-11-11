using System.Text.RegularExpressions;

namespace DBTracker.Core.Services;

/// <summary>Compares build strings numerically by segment ("9" &lt; "10"); non-numeric segments compare as ordinal text.</summary>
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
