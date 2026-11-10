using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupCopyLocationMaskerTests
{
    [Fact]
    public void CanView_ReturnsRawPath()
    {
        Assert.Equal(@"\\nas01\backups\prod", BackupCopyLocationMasker.Mask(@"\\nas01\backups\prod", canViewSensitivePaths: true));
    }

    [Fact]
    public void CannotView_ReturnsMaskedPlaceholder_NotRawPath()
    {
        var result = BackupCopyLocationMasker.Mask(@"\\nas01\backups\prod", canViewSensitivePaths: false);

        Assert.Equal(BackupCopyLocationMasker.MaskedPlaceholder, result);
        Assert.DoesNotContain("nas01", result);
    }

    [Fact]
    public void EmptyPath_StaysEmpty_RegardlessOfRole()
    {
        Assert.Equal("", BackupCopyLocationMasker.Mask("", canViewSensitivePaths: false));
        Assert.Equal("", BackupCopyLocationMasker.Mask(null, canViewSensitivePaths: true));
    }
}
