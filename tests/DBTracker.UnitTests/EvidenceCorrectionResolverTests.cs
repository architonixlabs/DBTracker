using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class EvidenceCorrectionResolverTests
{
    [Fact]
    public void NoCorrections_EffectiveStatus_IsOriginal()
    {
        var record = new BackupRecord { Id = 1, Status = BackupStatus.Success, Notes = "original" };

        Assert.Equal(BackupStatus.Success, EvidenceCorrectionResolver.EffectiveStatus(record, []));
        Assert.Equal("original", EvidenceCorrectionResolver.EffectiveNotes(record, []));
    }

    [Fact]
    public void OneCorrection_EffectiveStatus_IsCorrectedValue_OriginalUnchanged()
    {
        var record = new BackupRecord { Id = 1, Status = BackupStatus.Success, Notes = "original" };
        var correction = new BackupRecordCorrection { BackupRecordId = 1, NewStatus = BackupStatus.Failed, CorrectedAtUtc = DateTime.UtcNow };

        Assert.Equal(BackupStatus.Failed, EvidenceCorrectionResolver.EffectiveStatus(record, [correction]));
        // The original record's own field is never mutated by a correction.
        Assert.Equal(BackupStatus.Success, record.Status);
    }

    [Fact]
    public void MultipleCorrections_EffectiveValue_IsTheNewestOne()
    {
        var record = new BackupRecord { Id = 1, Status = BackupStatus.Success };
        var older = new BackupRecordCorrection { Id = 1, BackupRecordId = 1, NewStatus = BackupStatus.Failed, CorrectedAtUtc = DateTime.UtcNow.AddHours(-2) };
        var newer = new BackupRecordCorrection { Id = 2, BackupRecordId = 1, NewStatus = BackupStatus.Missed, CorrectedAtUtc = DateTime.UtcNow.AddHours(-1) };

        Assert.Equal(BackupStatus.Missed, EvidenceCorrectionResolver.EffectiveStatus(record, [older, newer]));
    }

    [Fact]
    public void CorrectionForADifferentRecord_IsIgnored()
    {
        var record = new BackupRecord { Id = 1, Status = BackupStatus.Success };
        var otherRecordCorrection = new BackupRecordCorrection { BackupRecordId = 2, NewStatus = BackupStatus.Failed };

        Assert.Equal(BackupStatus.Success, EvidenceCorrectionResolver.EffectiveStatus(record, [otherRecordCorrection]));
    }

    [Fact]
    public void RestoreEvent_EffectiveOutcome_UsesNewestCorrection()
    {
        var evt = new RestoreEvent { Id = 1, Outcome = RestoreOutcome.Success };
        var correction = new RestoreEventCorrection { RestoreEventId = 1, NewOutcome = RestoreOutcome.Failed, CorrectedAtUtc = DateTime.UtcNow };

        Assert.Equal(RestoreOutcome.Failed, EvidenceCorrectionResolver.EffectiveOutcome(evt, [correction]));
        Assert.Equal(RestoreOutcome.Success, evt.Outcome);
    }

    [Fact]
    public void CorrectionTouchingOnlyNotes_LeavesStatusAtOriginal()
    {
        var record = new BackupRecord { Id = 1, Status = BackupStatus.Success, Notes = "original" };
        var correction = new BackupRecordCorrection { BackupRecordId = 1, NewStatus = null, NewNotes = "fixed typo" };

        Assert.Equal(BackupStatus.Success, EvidenceCorrectionResolver.EffectiveStatus(record, [correction]));
        Assert.Equal("fixed typo", EvidenceCorrectionResolver.EffectiveNotes(record, [correction]));
    }
}
