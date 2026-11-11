using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ChangeWindowApprovalAuthorizerTests
{
    [Fact]
    public void Admin_CanApproveAnyScope()
    {
        Assert.True(ChangeWindowApprovalAuthorizer.IsAuthorized("Security", ["Admin"]));
        Assert.True(ChangeWindowApprovalAuthorizer.IsAuthorized("Some made-up scope", ["Admin"]));
    }

    [Fact]
    public void Dba_CanApproveDbaScope_NotChangeManagerScope()
    {
        Assert.True(ChangeWindowApprovalAuthorizer.IsAuthorized("DBA", ["DBA"]));
        Assert.False(ChangeWindowApprovalAuthorizer.IsAuthorized("Change manager", ["DBA"]));
    }

    [Fact]
    public void ItManager_CanApproveChangeManagerScope()
    {
        Assert.True(ChangeWindowApprovalAuthorizer.IsAuthorized("Change manager", ["ITManager"]));
    }

    [Fact]
    public void Auditor_CanApproveSecurityScope_NotDbaScope()
    {
        Assert.True(ChangeWindowApprovalAuthorizer.IsAuthorized("Security", ["Auditor"]));
        Assert.False(ChangeWindowApprovalAuthorizer.IsAuthorized("DBA", ["Auditor"]));
    }

    [Fact]
    public void ScopeMatching_IsCaseInsensitiveAndTrimmed()
    {
        Assert.True(ChangeWindowApprovalAuthorizer.IsAuthorized("  dba ", ["DBA"]));
    }

    [Fact]
    public void UnrecognizedScope_RequiresAdmin_RegardlessOfOtherRoles()
    {
        Assert.False(ChangeWindowApprovalAuthorizer.IsAuthorized("Legal", ["DBA", "ITManager", "Auditor"]));
        Assert.True(ChangeWindowApprovalAuthorizer.IsAuthorized("Legal", ["Admin"]));
    }

    [Fact]
    public void Viewer_CannotApproveAnyKnownScope()
    {
        Assert.False(ChangeWindowApprovalAuthorizer.IsAuthorized("DBA", ["Viewer"]));
        Assert.False(ChangeWindowApprovalAuthorizer.IsAuthorized("Application owner", ["Viewer"]));
    }

    [Fact]
    public void AuthorizedScopes_ListsOnlyWhatTheRoleMaySign()
    {
        Assert.Equal(["Change manager", "Application owner", "Platform"], ChangeWindowApprovalAuthorizer.AuthorizedScopes(["ITManager"]));
        Assert.Equal(["Security"], ChangeWindowApprovalAuthorizer.AuthorizedScopes(["Auditor"]));
        Assert.Empty(ChangeWindowApprovalAuthorizer.AuthorizedScopes(["Viewer"]));
        Assert.Equal(ChangeWindowApprovalAuthorizer.KnownScopes, ChangeWindowApprovalAuthorizer.AuthorizedScopes(["Admin"]));
    }
}
