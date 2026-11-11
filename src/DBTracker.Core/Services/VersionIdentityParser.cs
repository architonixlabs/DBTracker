using System.Text.RegularExpressions;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Structured identity fields parsed from one raw version string.</summary>
public record VersionParseResult(
    VersionResolutionStatus Status,
    string Family,
    int? Major,
    int? Minor,
    string Build,
    string Distribution,
    string PatchTrain,
    string UnresolvedReason)
{
    public static VersionParseResult Unresolved(string reason) =>
        new(VersionResolutionStatus.Unresolved, "", null, null, "", "", "", reason);
}

/// <summary>Engine-specific version normalization. A generic semver rule is wrong here (SQL Server marketing year vs build, Oracle "19c", MySQL/MariaDB ambiguity); Unresolved and NeedsReview are valid outcomes.</summary>
public static class VersionIdentityParser
{
    public static VersionParseResult Parse(DatabaseEngine engine, string? rawVersionText)
    {
        var raw = (rawVersionText ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return VersionParseResult.Unresolved("No version text provided.");
        }

        return engine switch
        {
            DatabaseEngine.PostgreSql => ParsePostgres(raw),
            DatabaseEngine.SqlServer => ParseSqlServer(raw),
            DatabaseEngine.MySql => ParseMySql(raw),
            DatabaseEngine.MongoDb => ParseMongo(raw),
            DatabaseEngine.Oracle => ParseOracle(raw),
            _ => VersionParseResult.Unresolved("Engine is Other/custom — no normalization rule exists for it; needs manual review.")
        };
    }

    private static VersionParseResult ParsePostgres(string raw)
    {
        var m = Regex.Match(raw, @"^(\d+)(?:\.(\d+))?");
        if (!m.Success)
        {
            return VersionParseResult.Unresolved($"'{raw}' does not look like a PostgreSQL version (expected e.g. '16.2').");
        }

        var major = int.Parse(m.Groups[1].Value);
        int? minor = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : null;

        var distribution = "";
        foreach (var d in new[] { "Debian", "Ubuntu", "RDS", "Azure", "Aurora", "Alpine" })
        {
            if (raw.Contains(d, StringComparison.OrdinalIgnoreCase))
            {
                distribution = d;
                break;
            }
        }

        return new VersionParseResult(VersionResolutionStatus.Resolved, "PostgreSQL", major, minor, "", distribution, "", "");
    }

    // SQL Server internal major build number -> marketing year, per Microsoft's public release numbering.
    private static readonly Dictionary<int, int> SqlServerBuildYears = new()
    {
        [16] = 2022,
        [15] = 2019,
        [14] = 2017,
        [13] = 2016,
        [12] = 2014,
        [11] = 2012,
        [10] = 2008
    };

    private static VersionParseResult ParseSqlServer(string raw)
    {
        // Marketing-year form, optionally with a CU/GDR patch train: "2022", "2019 CU18", "2016 GDR".
        var yearMatch = Regex.Match(raw, @"^(20\d{2})(?:\s+(CU\d+|GDR\d*))?", RegexOptions.IgnoreCase);
        if (yearMatch.Success)
        {
            var year = int.Parse(yearMatch.Groups[1].Value);
            var patchTrain = yearMatch.Groups[2].Success ? yearMatch.Groups[2].Value.ToUpperInvariant() : "";
            return new VersionParseResult(VersionResolutionStatus.Resolved, "SQL Server", year, null, "", "", patchTrain, "");
        }

        // Internal build form: "15.0.2000.5".
        var buildMatch = Regex.Match(raw, @"^(\d+)\.(\d+)\.(\d+)\.(\d+)$");
        if (buildMatch.Success)
        {
            var internalMajor = int.Parse(buildMatch.Groups[1].Value);
            if (!SqlServerBuildYears.TryGetValue(internalMajor, out var year))
            {
                return VersionParseResult.Unresolved($"Internal SQL Server major build {internalMajor} is not in the known build-to-year map — needs manual review.");
            }

            var build = $"{buildMatch.Groups[3].Value}.{buildMatch.Groups[4].Value}";
            return new VersionParseResult(VersionResolutionStatus.Resolved, "SQL Server", year, null, build, "", "", "");
        }

        return VersionParseResult.Unresolved($"'{raw}' does not match a recognized SQL Server version/build form.");
    }

    private static VersionParseResult ParseMySql(string raw)
    {
        if (raw.Contains("MariaDB", StringComparison.OrdinalIgnoreCase))
        {
            // MariaDB shares the MySQL engine value but has different support and patch rules, so it's flagged.
            var m = Regex.Match(raw, @"(\d+)\.(\d+)\.(\d+)");
            int? maj = m.Success ? int.Parse(m.Groups[1].Value) : null;
            int? min = m.Success ? int.Parse(m.Groups[2].Value) : null;
            var mariaBuild = m.Success ? m.Groups[3].Value : "";
            return new VersionParseResult(VersionResolutionStatus.NeedsReview, "MariaDB", maj, min, mariaBuild, "MariaDB",
                "", "Version string identifies MariaDB, a related but distinct product from MySQL — confirm the correct support/patch catalogue before relying on this.");
        }

        var match = Regex.Match(raw, @"^(\d+)\.(\d+)\.(\d+)(?:-(\w+))?");
        if (!match.Success)
        {
            return VersionParseResult.Unresolved($"'{raw}' does not look like a MySQL version (expected e.g. '8.0.36').");
        }

        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var build = match.Groups[3].Value;
        var suffix = match.Groups[4].Success ? match.Groups[4].Value : "";

        return new VersionParseResult(VersionResolutionStatus.Resolved, "MySQL", major, minor, build, suffix, "", "");
    }

    private static VersionParseResult ParseMongo(string raw)
    {
        var m = Regex.Match(raw, @"^(\d+)\.(\d+)(?:\.(\d+))?");
        if (!m.Success)
        {
            return VersionParseResult.Unresolved($"'{raw}' does not look like a MongoDB version (expected e.g. '7.0.5').");
        }

        var major = int.Parse(m.Groups[1].Value);
        var minor = int.Parse(m.Groups[2].Value);
        var build = m.Groups[3].Success ? m.Groups[3].Value : "";

        return new VersionParseResult(VersionResolutionStatus.Resolved, "MongoDB", major, minor, build, "", "", "");
    }

    private static VersionParseResult ParseOracle(string raw)
    {
        // "19c", "21c" — the "c" (Cloud) naming used from Oracle Database 12c onward.
        var cMatch = Regex.Match(raw, @"^(\d+)c$", RegexOptions.IgnoreCase);
        if (cMatch.Success)
        {
            return new VersionParseResult(VersionResolutionStatus.Resolved, "Oracle Database", int.Parse(cMatch.Groups[1].Value), null, "", "", "", "");
        }

        // Legacy dotted form: "11.2.0.4".
        var dotted = Regex.Match(raw, @"^(\d+)\.(\d+)\.(\d+)\.(\d+)$");
        if (dotted.Success)
        {
            var build = $"{dotted.Groups[3].Value}.{dotted.Groups[4].Value}";
            return new VersionParseResult(VersionResolutionStatus.Resolved, "Oracle Database", int.Parse(dotted.Groups[1].Value), int.Parse(dotted.Groups[2].Value), build, "", "", "");
        }

        return VersionParseResult.Unresolved($"'{raw}' does not match a recognized Oracle Database version form.");
    }
}
