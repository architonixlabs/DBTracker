using System.Globalization;
using System.Text;

namespace DBTracker.Core.Services;

/// <summary>One parsed row of a lifecycle import: what would happen to it, the item that would be written, and the stored
/// row it matched.</summary>
public record LifecycleImportRow<T>(int RowNumber, ImportRowPlan Plan, T? Item, int? MatchedExistingId, string Label, string? Detail) where T : class;

public record LifecycleImportPreview<T>(IReadOnlyList<LifecycleImportRow<T>> Rows, IReadOnlyList<ImportRowError> Errors) where T : class
{
    public int CreateCount => Rows.Count(r => r.Plan == ImportRowPlan.Create);
    public int UpdateCount => Rows.Count(r => r.Plan == ImportRowPlan.Update);
    public int SkipCount => Rows.Count(r => r.Plan == ImportRowPlan.Skip);
    public int AmbiguousCount => Rows.Count(r => r.Plan == ImportRowPlan.Ambiguous);
}

/// <summary>Shared CSV plumbing for the lifecycle importers (release catalogue, advisories, version metadata), following
/// the instance import's rules: header matched by name ignoring case and order, blank and '#' lines ignored, RFC 4180
/// quoting, and a missing column keeps the stored value on update.</summary>
public sealed class LifecycleCsvTable
{
    private readonly Dictionary<string, int> _indexOf;

    public IReadOnlyList<(int RowNumber, IReadOnlyList<string> Cells)> DataRows { get; }

    private LifecycleCsvTable(Dictionary<string, int> indexOf, IReadOnlyList<(int, IReadOnlyList<string>)> rows)
    {
        _indexOf = indexOf;
        DataRows = rows;
    }

    public static LifecycleCsvTable? Read(string content, IReadOnlyList<string> columns)
    {
        var records = LifecycleCsvSupport.SplitRecords(content);
        if (records.Count == 0)
        {
            return null;
        }

        var header = records[0].Select(h => h.Trim()).ToList();
        var indexOf = columns.ToDictionary(c => c, c => header.FindIndex(h => string.Equals(h, c, StringComparison.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);

        var rows = new List<(int, IReadOnlyList<string>)>();
        for (var i = 1; i < records.Count; i++)
        {
            var cells = records[i];
            if (cells.All(string.IsNullOrWhiteSpace) || (cells.Count > 0 && cells[0].TrimStart().StartsWith('#')))
            {
                continue;
            }
            rows.Add((i + 1, cells));
        }

        return new LifecycleCsvTable(indexOf, rows);
    }

    public bool Has(string column) => _indexOf.TryGetValue(column, out var idx) && idx >= 0;

    public string Get(IReadOnlyList<string> cells, string column)
    {
        var idx = _indexOf.TryGetValue(column, out var i) ? i : -1;
        return idx >= 0 && idx < cells.Count ? cells[idx].Trim() : "";
    }

    /// <summary>The cell when the column is present, otherwise the stored value.</summary>
    public string GetOrKeep(IReadOnlyList<string> cells, string column, string existing) =>
        Has(column) ? Get(cells, column) : existing;
}

public static class LifecycleCsvSupport
{
    public static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss.fffZ"];

    /// <summary>Parses a UTC date in one of DateFormats. Empty gives null with no error.</summary>
    public static bool TryParseUtcDate(string raw, out DateTime? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (DateTime.TryParseExact(raw.Trim(), DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            value = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        return false;
    }

    public static bool TryParseBool(string raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        switch (raw.Trim().ToLowerInvariant())
        {
            case "true" or "yes" or "y" or "1":
                value = true;
                return true;
            case "false" or "no" or "n" or "0":
                return true;
            default:
                return false;
        }
    }

    public static bool IsHttpUrlOrEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

    /// <summary>Header, a '#' note, a commented example row, then one '#' line per column. Commas in help text become
    /// semicolons so each note stays one cell.</summary>
    public static string BuildTemplate(IReadOnlyList<string> columns, string note, string exampleRow, IReadOnlyList<ImportColumnHelp> guide)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", columns)).Append('\n');
        sb.Append("# ").Append(note.Replace(',', ';')).Append('\n');
        sb.Append('#').Append(exampleRow).Append('\n');
        foreach (var column in guide)
        {
            sb.Append("# ").Append(column.Name).Append(column.Required ? " (required): " : ": ").Append(column.Help.Replace(',', ';')).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Splits CSV text into records with RFC 4180 quoting: quoted fields may contain commas and newlines, and "" is a literal quote.</summary>
    public static List<List<string>> SplitRecords(string content)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var sawAnyCell = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    sawAnyCell = true;
                    break;
                case ',':
                    row.Add(current.ToString());
                    current.Clear();
                    sawAnyCell = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(current.ToString());
                    current.Clear();
                    records.Add(row);
                    row = new List<string>();
                    sawAnyCell = false;
                    break;
                default:
                    current.Append(c);
                    sawAnyCell = true;
                    break;
            }
        }

        if (current.Length > 0 || row.Count > 0 || sawAnyCell)
        {
            row.Add(current.ToString());
            records.Add(row);
        }

        return records;
    }
}
