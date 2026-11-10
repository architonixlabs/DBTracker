using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class InstanceValidatorTests
{
    private static DatabaseInstance Valid() => new()
    {
        Name = "ARX-01",
        Engine = DatabaseEngine.SqlServer,
        ExpectedBackupIntervalHours = 24
    };

    [Fact]
    public void ValidInstance_HasNoErrors()
    {
        Assert.Empty(InstanceValidator.Validate(Valid()));
    }

    [Fact]
    public void MissingName_IsRequiredError()
    {
        var instance = Valid();
        instance.Name = "  ";

        var errors = InstanceValidator.Validate(instance);

        Assert.Contains(errors, e => e.Field == nameof(DatabaseInstance.Name));
    }

    [Fact]
    public void NameTooLong_IsLengthError()
    {
        var instance = Valid();
        instance.Name = new string('x', 201);

        var errors = InstanceValidator.Validate(instance);

        Assert.Contains(errors, e => e.Field == nameof(DatabaseInstance.Name) && e.Message.Contains("200"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveBackupInterval_IsRejected(int hours)
    {
        var instance = Valid();
        instance.ExpectedBackupIntervalHours = hours;

        var errors = InstanceValidator.Validate(instance);

        Assert.Contains(errors, e => e.Field == nameof(DatabaseInstance.ExpectedBackupIntervalHours));
    }

    [Fact]
    public void NegativeRetentionDays_IsRejected()
    {
        var instance = Valid();
        instance.RetentionPolicyDays = -1;

        var errors = InstanceValidator.Validate(instance);

        Assert.Contains(errors, e => e.Field == nameof(DatabaseInstance.RetentionPolicyDays));
    }

    [Fact]
    public void ZeroRetentionDays_IsAllowed()
    {
        var instance = Valid();
        instance.RetentionPolicyDays = 0;

        Assert.Empty(InstanceValidator.Validate(instance));
    }

    [Fact]
    public void NonPositiveRestoreTestIntervalOverride_IsRejected()
    {
        var instance = Valid();
        instance.RestoreTestIntervalDaysOverride = 0;

        var errors = InstanceValidator.Validate(instance);

        Assert.Contains(errors, e => e.Field == nameof(DatabaseInstance.RestoreTestIntervalDaysOverride));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://files.example.com/runbook")]
    [InlineData("javascript:alert(1)")]
    public void UnsupportedRunbookLinkScheme_IsRejected(string url)
    {
        var instance = Valid();
        instance.RunbookUrl = url;

        var errors = InstanceValidator.Validate(instance);

        Assert.Contains(errors, e => e.Field == nameof(DatabaseInstance.RunbookUrl));
    }

    [Theory]
    [InlineData("http://wiki.example.com/runbook")]
    [InlineData("https://wiki.example.com/runbook")]
    public void HttpOrHttpsRunbookLink_IsAccepted(string url)
    {
        var instance = Valid();
        instance.RunbookUrl = url;

        Assert.Empty(InstanceValidator.Validate(instance));
    }

    [Fact]
    public void InvalidContactEmail_IsRejected()
    {
        var instance = Valid();
        instance.ContactEmail = "not-an-email";

        var errors = InstanceValidator.Validate(instance);

        Assert.Contains(errors, e => e.Field == nameof(DatabaseInstance.ContactEmail));
    }

    [Fact]
    public void MultipleProblems_AllReturnedTogether_NotJustTheFirst()
    {
        var instance = new DatabaseInstance
        {
            Name = "",
            ExpectedBackupIntervalHours = -1,
            RetentionPolicyDays = -5
        };

        var errors = InstanceValidator.Validate(instance);

        Assert.True(errors.Count >= 3);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    public void MeasurementValidation_RejectsNegativeSizeOrDuration(decimal sizeMb, bool shouldBeValid)
    {
        var errors = InstanceValidator.ValidateMeasurements(sizeMb, null);
        Assert.Equal(shouldBeValid, errors.Count == 0);
    }
}
