using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.AdvancedPermissions.AI.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="PermissionLookupGuard"/>: inspecting other users' or user groups' access is
/// allowed only to users with access to the Users section, the same rule the base package's Access
/// Viewer endpoints enforce with <c>AuthorizationPolicies.SectionAccessUsers</c>.
/// </summary>
public sealed class PermissionLookupGuardTests
{
    /// <summary>A user whose allowed sections include the Users section may inspect others.</summary>
    [Fact]
    public void UserWithUsersSection_CanInspect()
    {
        var guard = new PermissionLookupGuard(AccessorFor([Constants.Applications.Content, Constants.Applications.Users]));

        Assert.True(guard.CanInspectOthers());
    }

    /// <summary>A user without the Users section may not inspect others.</summary>
    [Fact]
    public void UserWithoutUsersSection_CannotInspect()
    {
        var guard = new PermissionLookupGuard(AccessorFor([Constants.Applications.Content]));

        Assert.False(guard.CanInspectOthers());
    }

    /// <summary>With no backoffice user in context the guard fails closed.</summary>
    [Fact]
    public void NoCurrentUser_CannotInspect()
    {
        var accessor = Substitute.For<IBackOfficeSecurityAccessor>();
        accessor.BackOfficeSecurity.Returns((IBackOfficeSecurity?)null);

        Assert.False(new PermissionLookupGuard(accessor).CanInspectOthers());
    }

    /// <summary>Builds a security accessor whose current user has the given allowed sections.</summary>
    /// <param name="sections">The section aliases the user may access.</param>
    /// <returns>A substitute accessor.</returns>
    private static IBackOfficeSecurityAccessor AccessorFor(IEnumerable<string> sections)
    {
        var user = Substitute.For<IUser>();
        user.AllowedSections.Returns(sections);

        var security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns(user);

        var accessor = Substitute.For<IBackOfficeSecurityAccessor>();
        accessor.BackOfficeSecurity.Returns(security);
        return accessor;
    }
}
