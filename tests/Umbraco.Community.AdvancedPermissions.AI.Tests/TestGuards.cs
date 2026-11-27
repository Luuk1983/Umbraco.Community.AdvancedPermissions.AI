using NSubstitute;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Ready-made substitutes for the cross-cutting services every tool now takes, so a test that is not
/// about the Users-section gate gets a permissive one in a single call.
/// </summary>
internal static class TestGuards
{
    /// <summary>A lookup guard that lets the asker inspect anyone, as an administrator would.</summary>
    /// <returns>The guard substitute.</returns>
    public static IPermissionLookupGuard AllowAll()
    {
        var guard = Substitute.For<IPermissionLookupGuard>();
        guard.CanInspectOthers().Returns(true);
        return guard;
    }

    /// <summary>A start-node resolver that never blocks anyone, as for a subject with root access.</summary>
    /// <returns>The resolver substitute.</returns>
    public static IStartNodeReachResolver NeverBlocked()
    {
        var reach = Substitute.For<IStartNodeReachResolver>();
        reach.ForUserAsync(default, default).ReturnsForAnyArgs((StartNodeReach?)null);
        reach.ForUserGroupAsync(default!, default).ReturnsForAnyArgs((StartNodeReach?)null);
        return reach;
    }
}
