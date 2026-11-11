using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Infers the backup type of a discovered file from its name, so a log backup isn't recorded as a full one.</summary>
/// <remarks>Uses common naming conventions: a .trn extension or a "log"/"tlog"/"trn" name part means a log backup, and a
/// .dif/.diff extension or a "diff"/"differential" name part means a differential. Anything else is treated as full.</remarks>
public static class BackupFileTypeClassifier
{
    private static readonly char[] Separators = ['_', '-', '.', ' '];
    private static readonly HashSet<string> LogParts = new(StringComparer.OrdinalIgnoreCase) { "log", "tlog", "trn" };
    private static readonly HashSet<string> DiffParts = new(StringComparer.OrdinalIgnoreCase) { "diff", "differential", "dif" };

    public static BackupType Classify(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.');
        if (extension.Equals("trn", StringComparison.OrdinalIgnoreCase))
        {
            return BackupType.TransactionLog;
        }
        if (extension.Equals("dif", StringComparison.OrdinalIgnoreCase) || extension.Equals("diff", StringComparison.OrdinalIgnoreCase))
        {
            return BackupType.Differential;
        }

        var parts = Path.GetFileNameWithoutExtension(fileName).Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(LogParts.Contains))
        {
            return BackupType.TransactionLog;
        }
        if (parts.Any(DiffParts.Contains))
        {
            return BackupType.Differential;
        }

        return BackupType.Full;
    }
}
