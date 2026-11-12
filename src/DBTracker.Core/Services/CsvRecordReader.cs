using System.Text;

namespace DBTracker.Core.Services;

/// <summary>RFC 4180 CSV reading and writing shared by the importers, the error downloads and the templates.</summary>
public static class CsvRecordReader
{
    /// <summary>Splits CSV text into records with RFC 4180 quoting: quoted fields may contain commas and newlines, and "" is a literal quote.</summary>
    public static List<List<string>> Split(string content)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var sawAnyCellInContent = false;

        var i = 0;
        while (i < content.Length)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                current.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    sawAnyCellInContent = true;
                    i++;
                    continue;
                case ',':
                    row.Add(current.ToString());
                    current.Clear();
                    sawAnyCellInContent = true;
                    i++;
                    continue;
                case '\r':
                    i++;
                    continue;
                case '\n':
                    row.Add(current.ToString());
                    current.Clear();
                    records.Add(row);
                    row = new List<string>();
                    sawAnyCellInContent = false;
                    i++;
                    continue;
                default:
                    current.Append(c);
                    sawAnyCellInContent = true;
                    i++;
                    continue;
            }
        }

        if (current.Length > 0 || row.Count > 0 || sawAnyCellInContent)
        {
            row.Add(current.ToString());
            records.Add(row);
        }

        return records;
    }

    /// <summary>Quotes a cell when it holds a comma, quote or line break, so a downloaded file opens and re-imports cleanly.</summary>
    public static string Escape(string? value)
    {
        var v = value ?? "";
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    public static string Line(IEnumerable<string?> cells) => string.Join(",", cells.Select(Escape));
}
