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

    private static readonly string[] CompressionExtensions = [".gz", ".zst", ".lz4", ".bz2", ".xz", ".zip"];

    /// <summary>The chain member a discovered file satisfies when BackupType can't express it: a PostgreSQL WAL segment (24 hex digits, or a
    /// "wal" name part) or a MySQL/MariaDB binary log ("mysql-bin.000123", "binlog.000001"). Null means the type mapping applies.</summary>
    public static ChainMemberType? ClassifyChainRole(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var ext in CompressionExtensions)
        {
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^ext.Length];
                break;
            }
        }
        if (name.EndsWith(".partial", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^".partial".Length];
        }

        if (name.Length == 24 && name.All(Uri.IsHexDigit))
        {
            return ChainMemberType.WalArchive;
        }

        var parts = name.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => p.Equals("wal", StringComparison.OrdinalIgnoreCase)))
        {
            return ChainMemberType.WalArchive;
        }

        var extension = Path.GetExtension(name).TrimStart('.');
        var hasSequenceExtension = extension.Length >= 6 && extension.All(char.IsAsciiDigit);
        if (parts.Any(p => p.Equals("binlog", StringComparison.OrdinalIgnoreCase)) ||
            (hasSequenceExtension && parts.Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase))))
        {
            return ChainMemberType.Binlog;
        }

        return null;
    }

    /// <summary>The backup type to record for a discovered file: WAL and binlog files are log backups even when their names say nothing about logs.</summary>
    public static BackupType ClassifyForChain(string fileName) =>
        ClassifyChainRole(fileName) is ChainMemberType.WalArchive or ChainMemberType.Binlog ? BackupType.TransactionLog : Classify(fileName);
}
