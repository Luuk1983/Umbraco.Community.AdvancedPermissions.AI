using NSubstitute;
using Umbraco.Community.AdvancedPermissions.AI.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Shared test doubles for the cross-cutting services every tool now takes, so tests that are not about
/// those services keep exercising exactly the behaviour they did before.
/// </summary>
internal static class TestGuards
{
    /// <summary>A lookup guard that lets the current user inspect anyone (they have the Users section).</summary>
    /// <returns>A substitute guard returning <see langword="true"/>.</returns>
    public static IPermissionLookupGuard AllowAll()
    {
        var guard = Substitute.For<IPermissionLookupGuard>();
        guard.CanInspectOthers().Returns(true);
        return guard;
    }

    /// <summary>
    /// A start-node reach resolver under which nobody is ever blocked — every subject reaches every node,
    /// as for a site where start nodes do not restrict anyone.
    /// </summary>
    /// <returns>A substitute whose every method returns <see langword="null"/>.</returns>
    public static IStartNodeReachResolver NeverBlocked()
    {
        var reach = Substitute.For<IStartNodeReachResolver>();
        reach.ForUserAsync(default, default, default).ReturnsForAnyArgs((Umbraco.Community.AdvancedPermissions.AI.Models.StartNodeReach?)null);
        reach.ForUserGroupAsync(default!, default, default).ReturnsForAnyArgs((Umbraco.Community.AdvancedPermissions.AI.Models.StartNodeReach?)null);
        reach.ForLibraryCreateAsync(default, default).ReturnsForAnyArgs((Umbraco.Community.AdvancedPermissions.AI.Models.StartNodeReach?)null);
        return reach;
    }
}
