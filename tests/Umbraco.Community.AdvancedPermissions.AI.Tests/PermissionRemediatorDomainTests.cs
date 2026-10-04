using NSubstitute;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for the <see cref="PermissionDomain"/> awareness added to
/// <see cref="PermissionRemediator"/> in the v18 update.
/// </summary>
/// <remarks>
/// <para>
/// Only one dependency is domain-specific: the repository the current entries are read from. The pure
/// <see cref="IPermissionResolver"/> is shared, because it resolves a verb against a set of entries
/// without caring which table they came from — which is exactly why the simulation approach carries over
/// to the Library unchanged.
/// </para>
/// <para>
/// The risk these tests exist to close is a silent cross-tree read: remediating a Library denial while
/// reading <i>content</i> entries would simulate against the wrong entry set and confidently return a
/// "confirmed" fix that does nothing. So the tests assert not just that the right repository is read, but
/// that the other one is never touched.
/// </para>
/// </remarks>
public sealed class PermissionRemediatorDomainTests
{
    /// <summary>The mocked content repository.</summary>
    private readonly IAdvancedPermissionRepository _contentRepository = Substitute.For<IAdvancedPermissionRepository>();

    /// <summary>The mocked Library element repository.</summary>
    private readonly IElementPermissionRepository _elementRepository = Substitute.For<IElementPermissionRepository>();

    /// <summary>The real, pure resolver — shared across domains and never mocked.</summary>
    private readonly IPermissionResolver _resolver = new PermissionResolver();

    /// <summary>The root node in the simulated path.</summary>
    private readonly Guid _root = Guid.NewGuid();

    /// <summary>The node the denial is evaluated at.</summary>
    private readonly Guid _node = Guid.NewGuid();

    /// <summary>The role set the verdict was resolved with.</summary>
    private static readonly string[] Roles = ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias];

    /// <summary>Builds the system under test over both repositories.</summary>
    /// <returns>A fresh remediator.</returns>
    private PermissionRemediator CreateSut() => new(_resolver, _contentRepository, _elementRepository);

    /// <summary>
    /// Configures a repository with the canonical remediable shape: an explicit same-node Deny for the
    /// user's group suppressing an inherited All Users Allow. The underlying Allow is what makes
    /// "remove the Deny entry" a genuine fix — with nothing else granting the verb, removal would still
    /// resolve to Denied and the option would (correctly) not be confirmed.
    /// </summary>
    /// <param name="repository">The repository to stub.</param>
    /// <param name="verb">The verb both entries control.</param>
    private void SetupDenyOverInheritedAllow(INodePermissionRepository repository, string verb) =>
        repository
            .GetByRolesAndNodesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([
                new AdvancedPermissionEntry(
                    Guid.NewGuid(), _node, "editors", verb, PermissionState.Deny, PermissionScope.ThisNodeOnly),
                new AdvancedPermissionEntry(
                    Guid.NewGuid(),
                    AdvancedPermissionsConstants.VirtualRootNodeKey,
                    AdvancedPermissionsConstants.EveryoneRoleAlias,
                    verb,
                    PermissionState.Allow,
                    PermissionScope.ThisNodeAndDescendants),
            ]);

    /// <summary>Runs the remediator for the given domain and verb.</summary>
    /// <param name="domain">The permission domain to remediate in.</param>
    /// <param name="verb">The denied verb.</param>
    /// <returns>The confirmed remediation options.</returns>
    private Task<IReadOnlyList<RemediationOption>> SuggestAsync(PermissionDomain domain, string verb) =>
        CreateSut().SuggestAsync(_node, [_root, _node], Roles, verb, PermissionState.Deny, domain);

    /// <summary>Asserts a repository's bulk entry read was made.</summary>
    /// <param name="repository">The repository expected to have been read.</param>
    private static Task AssertRead(INodePermissionRepository repository) =>
        repository.Received().GetByRolesAndNodesAsync(
            Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());

    /// <summary>Asserts a repository's bulk entry read was never made.</summary>
    /// <param name="repository">The repository expected to be untouched.</param>
    private static Task AssertNotRead(INodePermissionRepository repository) =>
        repository.DidNotReceive().GetByRolesAndNodesAsync(
            Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());

    /// <summary>The Library domain reads the element repository, and never the content one.</summary>
    [Fact]
    public async Task SuggestAsync_LibraryDomain_ReadsTheElementRepositoryOnly()
    {
        SetupDenyOverInheritedAllow(_elementRepository, AdvancedPermissionsConstants.VerbElementDelete);

        var options = await SuggestAsync(PermissionDomain.Library, AdvancedPermissionsConstants.VerbElementDelete);

        Assert.NotEmpty(options);
        await AssertRead(_elementRepository);
        await AssertNotRead(_contentRepository);
    }

    /// <summary>The content domain reads the content repository, and never the element one.</summary>
    [Fact]
    public async Task SuggestAsync_ContentDomain_ReadsTheContentRepositoryOnly()
    {
        SetupDenyOverInheritedAllow(_contentRepository, AdvancedPermissionsConstants.VerbDelete);

        var options = await SuggestAsync(PermissionDomain.Content, AdvancedPermissionsConstants.VerbDelete);

        Assert.NotEmpty(options);
        await AssertRead(_contentRepository);
        await AssertNotRead(_elementRepository);
    }

    /// <summary>Omitting the domain keeps the v17 content behaviour, so existing callers are unaffected.</summary>
    [Fact]
    public async Task SuggestAsync_DomainOmitted_DefaultsToContent()
    {
        SetupDenyOverInheritedAllow(_contentRepository, AdvancedPermissionsConstants.VerbDelete);

        var options = await CreateSut().SuggestAsync(
            _node, [_root, _node], Roles, AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny);

        Assert.NotEmpty(options);
        await AssertNotRead(_elementRepository);
    }

    /// <summary>
    /// The simulation semantics carry over intact: a same-node Deny is still not beaten by a plain Allow,
    /// so the Library gets the same two confirmed options — remove the Deny entry, or a Priority Override.
    /// </summary>
    [Fact]
    public async Task SuggestAsync_LibraryDomain_PlainAllowRejected_RemoveAndOverrideReturned()
    {
        SetupDenyOverInheritedAllow(_elementRepository, AdvancedPermissionsConstants.VerbElementDelete);

        var options = await SuggestAsync(PermissionDomain.Library, AdvancedPermissionsConstants.VerbElementDelete);

        Assert.DoesNotContain(options, o => o.Kind == RemediationActionKind.AddAllowOnNode);
        Assert.Contains(options, o => o.Kind == RemediationActionKind.RemoveDeny);
        Assert.Contains(options, o => o.Kind == RemediationActionKind.AddPriorityOverrideAllow);
    }

    /// <summary>
    /// A Library "remove the Deny" option still names the grant that takes over, so the copilot can say
    /// <i>why</i> the removal works instead of merely asserting that it does.
    /// </summary>
    [Fact]
    public async Task SuggestAsync_LibraryDomain_RemoveDenyStillCarriesTheGrantThatTakesOver()
    {
        SetupDenyOverInheritedAllow(_elementRepository, AdvancedPermissionsConstants.VerbElementDelete);

        var options = await SuggestAsync(PermissionDomain.Library, AdvancedPermissionsConstants.VerbElementDelete);

        var remove = Assert.Single(options, o => o.Kind == RemediationActionKind.RemoveDeny);
        Assert.NotNull(remove.GrantedBy);
        Assert.Equal(PermissionState.Allow, remove.GrantedBy!.State);
        Assert.Equal(AdvancedPermissionsConstants.EveryoneRoleAlias, remove.GrantedBy.ContributingRole);
    }
}
