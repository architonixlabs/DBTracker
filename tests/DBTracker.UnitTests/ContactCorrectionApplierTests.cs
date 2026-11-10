using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ContactCorrectionApplierTests
{
    [Fact]
    public void Apply_OnlyOverwritesFieldsThatWereProposed()
    {
        var instance = new DatabaseInstance
        {
            ContactName = "Old Name",
            ContactEmail = "old@acme.test",
            ContactPhone = "555-0000",
            BackupContactEmail = "oldbackup@acme.test"
        };
        var request = new ContactCorrectionRequest
        {
            ProposedContactEmail = "new@acme.test"
            // Every other proposed field left empty — "no change", not "clear this field".
        };

        ContactCorrectionApplier.Apply(instance, request);

        Assert.Equal("Old Name", instance.ContactName);
        Assert.Equal("new@acme.test", instance.ContactEmail);
        Assert.Equal("555-0000", instance.ContactPhone);
        Assert.Equal("oldbackup@acme.test", instance.BackupContactEmail);
    }

    [Fact]
    public void Apply_CanSetBackupContactFieldsIndependently()
    {
        var instance = new DatabaseInstance { BackupContactName = "", BackupContactEmail = "" };
        var request = new ContactCorrectionRequest { ProposedBackupContactName = "Backup Person", ProposedBackupContactEmail = "backup@acme.test" };

        ContactCorrectionApplier.Apply(instance, request);

        Assert.Equal("Backup Person", instance.BackupContactName);
        Assert.Equal("backup@acme.test", instance.BackupContactEmail);
    }

    [Theory]
    [InlineData("", "", "", "", "", "", false)]
    [InlineData("New Name", "", "", "", "", "", true)]
    [InlineData("", "", "", "", "backup@acme.test", "", true)]
    public void HasAnyProposedChange_TrueOnlyWhenAtLeastOneFieldIsProposed(
        string name, string email, string phone, string backupName, string backupEmail, string backupPhone, bool expected)
    {
        var request = new ContactCorrectionRequest
        {
            ProposedContactName = name,
            ProposedContactEmail = email,
            ProposedContactPhone = phone,
            ProposedBackupContactName = backupName,
            ProposedBackupContactEmail = backupEmail,
            ProposedBackupContactPhone = backupPhone
        };

        Assert.Equal(expected, ContactCorrectionApplier.HasAnyProposedChange(request));
    }
}
