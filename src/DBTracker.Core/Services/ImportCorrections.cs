using System.Text;

namespace DBTracker.Core.Services;

/// <summary>Builds the "rows to fix" download: the uploaded file's own header and cells for every rejected row, with the reason
/// in a leading ImportError column. Fix the cells, delete the ImportError column (or leave it: importers ignore unknown columns)
/// and upload again.</summary>
public static class ImportCorrections
{
    public const string ErrorColumn = "ImportError";
    public const string RowColumn = "SourceRow";

    /// <param name="csvContent">The file exactly as uploaded.</param>
    /// <param name="problems">Row number (1-based, header is row 1, as the importers report it) and the message for that row.
    /// Several messages for one row are joined. Row 1 problems (header errors) are listed with no cells.</param>
    public static string BuildCsv(string csvContent, IEnumerable<(int RowNumber, string Message)> problems)
    {
        var records = CsvRecordReader.Split(csvContent);
        var header = records.Count > 0 ? records[0] : [];
        var byRow = problems
            .GroupBy(p => p.RowNumber)
            .OrderBy(g => g.Key)
            .ToList();

        var sb = new StringBuilder();
        sb.Append(CsvRecordReader.Line(new[] { ErrorColumn, RowColumn }.Concat(header))).Append('\n');
        foreach (var group in byRow)
        {
            var message = string.Join(" ", group.Select(p => p.Message).Distinct());
            var index = group.Key - 1;
            var cells = group.Key > 1 && index < records.Count ? records[index] : [];
            // Pad short rows so every cell lines up under its header.
            var padded = cells.Concat(Enumerable.Repeat("", Math.Max(0, header.Count - cells.Count)));
            sb.Append(CsvRecordReader.Line(new[] { message, group.Key.ToString() }.Concat(padded))).Append('\n');
        }

        return sb.ToString();
    }
}
