using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupFileTypeClassifierTests
{
    [Theory]
    [InlineData("ARXMAIL_20261110_0200.bak", BackupType.Full)]
    [InlineData("arxmail-full-2026-11-10.bak", BackupType.Full)]
    [InlineData("sales_db.dump", BackupType.Full)]
    [InlineData("ARXMAIL_20261110_0215.trn", BackupType.TransactionLog)]
    [InlineData("ARXMAIL_LOG_20261110_0215.bak", BackupType.TransactionLog)]
    [InlineData("arxmail-tlog-0215.bak", BackupType.TransactionLog)]
    [InlineData("ARXMAIL_DIFF_20261110.bak", BackupType.Differential)]
    [InlineData("arxmail.differential.bak", BackupType.Differential)]
    [InlineData("arxmail_20261110.dif", BackupType.Differential)]
    public void Classify_UsesExtensionAndNameParts(string fileName, BackupType expected) =>
        Assert.Equal(expected, BackupFileTypeClassifier.Classify(fileName));

    [Theory]
    [InlineData("CATALOG_20261110.bak")]
    [InlineData("blogdb_20261110.bak")]
    [InlineData("difficulty-stats.bak")]
    public void WordsThatOnlyContainLogOrDiff_StayFull(string fileName) =>
        Assert.Equal(BackupType.Full, BackupFileTypeClassifier.Classify(fileName));
}
