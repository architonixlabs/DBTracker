using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ExceptionApprovalAuthorizerTests
{
    [Fact]
    public void Admin_CanAlwaysApprove()
    {
        Assert.True(ExceptionApprovalAuthorizer.IsAuthorized("ITManager", ["Admin"]));
        Assert.True(ExceptionApprovalAuthorizer.IsAuthorized("", ["Admin"]));
    }

    [Fact]
    public void EmptyPermittedRole_IsAdminOnly()
    {
        Assert.False(ExceptionApprovalAuthorizer.IsAuthorized("", ["DBA"]));
        Assert.True(ExceptionApprovalAuthorizer.IsAuthorized("", ["Admin"]));
    }

    [Fact]
    public void MatchingRole_IsAuthorized()
    {
        Assert.True(ExceptionApprovalAuthorizer.IsAuthorized("ITManager", ["DBA", "ITManager"]));
    }

    [Fact]
    public void NonMatchingRole_IsNotAuthorized()
    {
        Assert.False(ExceptionApprovalAuthorizer.IsAuthorized("ITManager", ["DBA", "Auditor"]));
    }

    [Fact]
    public void RoleMatch_TrimsWhitespace()
    {
        Assert.True(ExceptionApprovalAuthorizer.IsAuthorized("  ITManager ", ["ITManager"]));
    }
}
