using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class DeliveryOutcomeSummarizerTests
{
    private static DeliveryRecipientResult Accepted => new(EmailSendOutcome.Accepted);
    private static DeliveryRecipientResult Disabled => new(EmailSendOutcome.Disabled);
    private static DeliveryRecipientResult Failed(string detail) => new(EmailSendOutcome.Failed, detail);

    [Fact]
    public void NoRecipients()
    {
        var s = DeliveryOutcomeSummarizer.Summarize([]);
        Assert.Equal(DeliveryOutcome.NoRecipients, s.Outcome);
        Assert.Equal(0, s.RecipientCount);
    }

    [Fact]
    public void DisabledSender_IsNeverAccepted()
    {
        var s = DeliveryOutcomeSummarizer.Summarize([Disabled, Disabled]);
        Assert.Equal(DeliveryOutcome.Disabled, s.Outcome);
        Assert.Equal(0, s.AcceptedCount);
    }

    [Fact]
    public void AllAccepted()
    {
        var s = DeliveryOutcomeSummarizer.Summarize([Accepted, Accepted]);
        Assert.Equal(DeliveryOutcome.Accepted, s.Outcome);
        Assert.Equal(2, s.AcceptedCount);
        Assert.Equal("", s.Error);
    }

    [Fact]
    public void Partial_KeepsCountsAndFirstError()
    {
        var s = DeliveryOutcomeSummarizer.Summarize([Accepted, Failed("550 mailbox unavailable")]);
        Assert.Equal(DeliveryOutcome.Partial, s.Outcome);
        Assert.Equal(1, s.AcceptedCount);
        Assert.Equal(1, s.FailedCount);
        Assert.Equal("550 mailbox unavailable", s.Error);
    }

    [Fact]
    public void AllFailed_SummarizesCount()
    {
        var s = DeliveryOutcomeSummarizer.Summarize([Failed("timeout"), Failed("refused")]);
        Assert.Equal(DeliveryOutcome.Failed, s.Outcome);
        Assert.Equal("2 recipients failed. First error: timeout", s.Error);
    }

    [Fact]
    public void LongError_IsTrimmedToColumnLength()
    {
        var s = DeliveryOutcomeSummarizer.Summarize([Failed(new string('x', 5000))]);
        Assert.Equal(DeliveryOutcomeSummarizer.MaxErrorLength, s.Error.Length);
    }

    [Fact]
    public void Describe_NeverClaimsDelivery()
    {
        Assert.Equal("Accepted by provider", DeliveryOutcomeSummarizer.Describe(DeliveryOutcome.Accepted));
        Assert.DoesNotContain("Delivered", DeliveryOutcomeSummarizer.Describe(DeliveryOutcome.Accepted));
    }
}
