using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupFileChainRoleTests
{
    [Theory]
    [InlineData("000000010000000A000000FF")]
    [InlineData("000000010000000A000000FF.gz")]
    [InlineData("000000010000000A000000FF.partial")]
    [InlineData("pg_wal_2026-11-01.tar")]
    public void PostgresWalFiles_AreWalArchive_AndLogType(string fileName)
    {
        Assert.Equal(ChainMemberType.WalArchive, BackupFileTypeClassifier.ClassifyChainRole(fileName));
        Assert.Equal(BackupType.TransactionLog, BackupFileTypeClassifier.ClassifyForChain(fileName));
    }

    [Theory]
    [InlineData("mysql-bin.000123")]
    [InlineData("binlog.000001")]
    [InlineData("mariadb-bin.004211.zst")]
    public void MySqlBinaryLogs_AreBinlog(string fileName)
    {
        Assert.Equal(ChainMemberType.Binlog, BackupFileTypeClassifier.ClassifyChainRole(fileName));
        Assert.Equal(BackupType.TransactionLog, BackupFileTypeClassifier.ClassifyForChain(fileName));
    }

    [Theory]
    [InlineData("arxmail_full_20261101.bak", BackupType.Full)]
    [InlineData("arxmail_log_20261101.trn", BackupType.TransactionLog)]
    [InlineData("arxmail_diff_20261101.bak", BackupType.Differential)]
    public void OrdinaryBackups_HaveNoChainRole_AndKeepTheirType(string fileName, BackupType expected)
    {
        Assert.Null(BackupFileTypeClassifier.ClassifyChainRole(fileName));
        Assert.Equal(expected, BackupFileTypeClassifier.ClassifyForChain(fileName));
    }

    [Fact]
    public void BinWithoutSequenceExtension_IsNotABinlog()
    {
        Assert.Null(BackupFileTypeClassifier.ClassifyChainRole("app_bin_export.bak"));
    }
}
