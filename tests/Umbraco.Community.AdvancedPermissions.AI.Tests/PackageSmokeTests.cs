using Umbraco.Community.AdvancedPermissions.AI;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Smoke tests that verify the package assembly is wired up correctly — it builds, is referenced by
/// the test project, exposes its internals to tests, and loads with the expected identity.
/// </summary>
public class PackageSmokeTests
{
    /// <summary>
    /// The internal assembly marker is visible to the test project (via <c>InternalsVisibleTo</c>)
    /// and its assembly carries the expected simple name.
    /// </summary>
    [Fact]
    public void Package_assembly_is_referenced_and_internals_visible()
    {
        var assemblyName = typeof(AdvancedPermissionsAiAssembly).Assembly.GetName().Name;

        Assert.Equal("Umbraco.Community.AdvancedPermissions.AI", assemblyName);
    }
}
