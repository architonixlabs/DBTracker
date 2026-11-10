using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class CampaignStateTransitionValidatorTests
{
    [Theory]
    [InlineData(CampaignState.Proposed, CampaignState.Assessing)]
    [InlineData(CampaignState.Proposed, CampaignState.Cancelled)]
    [InlineData(CampaignState.Assessing, CampaignState.Blocked)]
    [InlineData(CampaignState.Assessing, CampaignState.Ready)]
    [InlineData(CampaignState.Blocked, CampaignState.Assessing)]
    [InlineData(CampaignState.Ready, CampaignState.Scheduled)]
    [InlineData(CampaignState.Scheduled, CampaignState.InProgress)]
    [InlineData(CampaignState.InProgress, CampaignState.Validating)]
    [InlineData(CampaignState.InProgress, CampaignState.RolledBack)]
    [InlineData(CampaignState.Validating, CampaignState.Completed)]
    [InlineData(CampaignState.Validating, CampaignState.RolledBack)]
    [InlineData(CampaignState.Completed, CampaignState.RolledBack)]
    [InlineData(CampaignState.RolledBack, CampaignState.Assessing)]
    public void KnownValidTransitions_AreAccepted(CampaignState from, CampaignState to)
    {
        Assert.True(CampaignStateTransitionValidator.IsValidTransition(from, to));
    }

    [Theory]
    [InlineData(CampaignState.Proposed, CampaignState.Completed)]
    [InlineData(CampaignState.Proposed, CampaignState.Scheduled)]
    [InlineData(CampaignState.Ready, CampaignState.InProgress)]
    [InlineData(CampaignState.Scheduled, CampaignState.Validating)]
    [InlineData(CampaignState.Cancelled, CampaignState.Proposed)]
    [InlineData(CampaignState.Completed, CampaignState.Proposed)]
    [InlineData(CampaignState.RolledBack, CampaignState.Ready)]
    [InlineData(CampaignState.RolledBack, CampaignState.Scheduled)]
    public void KnownInvalidTransitions_AreRejected(CampaignState from, CampaignState to)
    {
        Assert.False(CampaignStateTransitionValidator.IsValidTransition(from, to));
    }

    [Fact]
    public void SameStateTransition_IsAlwaysRejected()
    {
        foreach (var state in Enum.GetValues<CampaignState>())
        {
            Assert.False(CampaignStateTransitionValidator.IsValidTransition(state, state));
        }
    }

    [Fact]
    public void Cancelled_IsTerminal_NoAllowedNextStates()
    {
        Assert.Empty(CampaignStateTransitionValidator.AllowedNextStates(CampaignState.Cancelled));
    }

    [Fact]
    public void EveryNonTerminalState_HasCancelledOrARecoveryPath()
    {
        // Cancelled itself has no next states; every other state must have somewhere to go.
        foreach (var state in Enum.GetValues<CampaignState>())
        {
            if (state == CampaignState.Cancelled)
            {
                continue;
            }
            Assert.NotEmpty(CampaignStateTransitionValidator.AllowedNextStates(state));
        }
    }

    [Fact]
    public void DescribeInvalidReason_SameState_MentionsAlreadyIn()
    {
        var reason = CampaignStateTransitionValidator.DescribeInvalidReason(CampaignState.Assessing, CampaignState.Assessing);
        Assert.Contains("Already in", reason);
    }

    [Fact]
    public void DescribeInvalidReason_TerminalState_MentionsTerminal()
    {
        var reason = CampaignStateTransitionValidator.DescribeInvalidReason(CampaignState.Cancelled, CampaignState.Proposed);
        Assert.Contains("terminal", reason);
    }

    [Fact]
    public void DescribeInvalidReason_InvalidJump_ListsValidNextStates()
    {
        var reason = CampaignStateTransitionValidator.DescribeInvalidReason(CampaignState.Proposed, CampaignState.Completed);
        Assert.Contains("Valid next state", reason);
        Assert.Contains("Assessing", reason);
    }
}
