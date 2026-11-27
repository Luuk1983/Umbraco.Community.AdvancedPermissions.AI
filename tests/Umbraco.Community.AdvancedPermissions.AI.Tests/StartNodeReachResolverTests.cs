using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="StartNodeReachResolver"/>: it turns the base package's start-node
/// classification into a friendly <see cref="StartNodeReach"/>, and returns <see langword="null"/>
/// whenever start nodes do not block the subject (or it cannot tell), so callers only ever change an
/// answer when the base package's own viewers would show a blocked cell.
/// </summary>
public sealed class StartNodeReachResolverTests
{
    /// <summary>The node under test.</summary>
    private static readonly Guid Node = Guid.NewGuid();

    /// <summary>The node's materialised path.</summary>
    private const string NodePath = "-1,1050,1063";

    /// <summary>The mocked base start-node service.</summary>
    private readonly IStartNodeAccessService _startNodes = Substitute.For<IStartNodeAccessService>();

    /// <summary>The mocked entity service used to read node paths.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The user under test.</summary>
    private readonly Guid _user = Guid.NewGuid();

    /// <summary>Registers <see cref="Node"/> as a content node with <see cref="NodePath"/>.</summary>
    public StartNodeReachResolverTests()
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Path.Returns(NodePath);
        _entityService.Get(Node, UmbracoObjectTypes.Document).Returns(entity);
    }

    /// <summary>Builds the resolver under test.</summary>
    /// <returns>A resolver over the mocks.</returns>
    private StartNodeReachResolver CreateResolver() => new(_startNodes, _entityService);

    /// <summary>Builds a scope in the given state.</summary>
    /// <param name="state">The subject's start-node state.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>A scope.</returns>
    private static StartNodeScope Scope(StartNodeSubjectState state, StartNodeTree tree = StartNodeTree.Content) =>
        new(tree, state, state == StartNodeSubjectState.Restricted ? [2000] : [], state == StartNodeSubjectState.Restricted ? ["-1,2000"] : []);

    /// <summary>A snapshot whose only effective start node is "News" (id 2000), given by the Editors group.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>A restricted snapshot.</returns>
    private static StartNodeSnapshot NewsSnapshot(StartNodeTree tree = StartNodeTree.Content) =>
        new(
            tree,
            StartNodeSubjectState.Restricted,
            [2000],
            new Dictionary<int, StartNodeInfo> { [2000] = new(2000, Guid.NewGuid(), "News", "-1,2000") },
            [
                new StartNodeSourceResult(new StartNodeSource(StartNodeSourceKind.UserGroup, "editors", "Editors", 2000), StartNodeSourceStatus.InUse, null),
                new StartNodeSourceResult(new StartNodeSource(StartNodeSourceKind.UserGroup, "writers", "Writers", 3000), StartNodeSourceStatus.CoveredBy, 2000),
            ]);

    /// <summary>Configures the user's scope, its classification of <see cref="NodePath"/>, and its snapshot.</summary>
    /// <param name="scope">The scope to return.</param>
    /// <param name="access">The classification to return.</param>
    /// <param name="tree">The tree.</param>
    private void SetupUser(StartNodeScope scope, StartNodeAccess access, StartNodeTree tree = StartNodeTree.Content)
    {
        _startNodes.GetScopeForUserAsync(_user, tree).Returns(scope);
        _startNodes.Classify(scope, Arg.Any<string>()).Returns(access);
        _startNodes.GetForUserAsync(_user, tree).Returns(NewsSnapshot(tree));
    }

    /// <summary>A reachable node is not blocked, so there is nothing to report.</summary>
    [Fact]
    public async Task ForUser_Accessible_ReturnsNull()
    {
        SetupUser(Scope(StartNodeSubjectState.Restricted), StartNodeAccess.Accessible);

        Assert.Null(await CreateResolver().ForUserAsync(_user, Node, PermissionDomain.Content));
    }

    /// <summary>An ancestor of a start node is "Browse only", with the start nodes in effect and who set them.</summary>
    [Fact]
    public async Task ForUser_PathOnly_ReturnsBrowseOnlyWithStartNodes()
    {
        SetupUser(Scope(StartNodeSubjectState.Restricted), StartNodeAccess.PathOnly);

        var reach = await CreateResolver().ForUserAsync(_user, Node, PermissionDomain.Content);

        Assert.NotNull(reach);
        Assert.Equal("Browse only (start nodes)", reach.Access);
        Assert.Equal(
            "Above a start node. Umbraco shows it in the tree only so users can browse down to their start node; it can't be opened.",
            reach.Explanation);
        Assert.Equal(new StartNodeRef("News", "from Editors"), Assert.Single(reach.StartNodes));
        Assert.Contains("No permission entry can change this", reach.HowToFix);
        Assert.Null(reach.Note);
        _startNodes.Received(1).Classify(Arg.Any<StartNodeScope>(), NodePath);
    }

    /// <summary>A node outside every start node is "Not visible".</summary>
    [Fact]
    public async Task ForUser_NotVisible_ReturnsNotVisible()
    {
        SetupUser(Scope(StartNodeSubjectState.Restricted), StartNodeAccess.NotVisible);

        var reach = await CreateResolver().ForUserAsync(_user, Node, PermissionDomain.Content);

        Assert.Equal("Not visible (start nodes)", reach!.Access);
        Assert.Equal("Outside every start node, so Umbraco hides it from the tree.", reach.Explanation);
    }

    /// <summary>A user with no start node at all gets the no-access explanation rather than a per-node one.</summary>
    [Fact]
    public async Task ForUser_NoAccess_UsesNoAccessExplanation()
    {
        SetupUser(Scope(StartNodeSubjectState.NoAccess), StartNodeAccess.NotVisible);

        var reach = await CreateResolver().ForUserAsync(_user, Node, PermissionDomain.Content);

        Assert.Equal("This user has no start nodes, so they can't reach any content. Every node is blocked.", reach!.Explanation);
    }

    /// <summary>Start nodes do not apply to the All Users group, so it is never reported as blocked.</summary>
    [Fact]
    public async Task ForUserGroup_Everyone_ReturnsNull()
    {
        var reach = await CreateResolver().ForUserGroupAsync(
            AdvancedPermissionsConstants.EveryoneRoleAlias, Node, PermissionDomain.Content);

        Assert.Null(reach);
        Assert.Empty(_startNodes.ReceivedCalls());
    }

    /// <summary>A blocked user group carries the note that users combine the start nodes of all their groups.</summary>
    [Fact]
    public async Task ForUserGroup_Blocked_CarriesRoleModeNote()
    {
        var scope = Scope(StartNodeSubjectState.Restricted);
        _startNodes.GetScopeForUserGroupAsync("editors", StartNodeTree.Content).Returns(scope);
        _startNodes.Classify(scope, NodePath).Returns(StartNodeAccess.NotVisible);
        _startNodes.GetForUserGroupAsync("editors", StartNodeTree.Content).Returns(NewsSnapshot());

        var reach = await CreateResolver().ForUserGroupAsync("editors", Node, PermissionDomain.Content);

        Assert.Equal(
            "Users get the combined start nodes of all their groups, so ask about a specific user to see the real result.",
            reach!.Note);
    }

    /// <summary>An unknown user cannot be verified, so no restriction is claimed (the base viewer does the same).</summary>
    [Fact]
    public async Task ForUser_UnknownUser_ReturnsNull()
    {
        _startNodes.GetScopeForUserAsync(_user, StartNodeTree.Content).Returns((StartNodeScope?)null);

        Assert.Null(await CreateResolver().ForUserAsync(_user, Node, PermissionDomain.Content));
    }

    /// <summary>The virtual root (the Default permissions row, or creating at the root) is classified as <c>-1</c>.</summary>
    [Fact]
    public async Task VirtualRoot_ClassifiesPathMinusOne()
    {
        SetupUser(Scope(StartNodeSubjectState.Restricted), StartNodeAccess.PathOnly);

        var reach = await CreateResolver().ForUserAsync(_user, AdvancedPermissionsConstants.VirtualRootNodeKey, PermissionDomain.Content);

        Assert.NotNull(reach);
        _startNodes.Received(1).Classify(Arg.Any<StartNodeScope>(), "-1");
    }

    /// <summary>A node whose path cannot be read is not claimed as blocked.</summary>
    [Fact]
    public async Task UnresolvableNode_ReturnsNull()
    {
        SetupUser(Scope(StartNodeSubjectState.Restricted), StartNodeAccess.NotVisible);

        Assert.Null(await CreateResolver().ForUserAsync(_user, Guid.NewGuid(), PermissionDomain.Content));
    }

    /// <summary>The Library reads the Library's start nodes, finds element paths, and uses the Library wording.</summary>
    [Fact]
    public async Task Library_UsesLibraryTreeAndCopy()
    {
        var item = Guid.NewGuid();
        var entity = Substitute.For<IEntitySlim>();
        entity.Path.Returns("-1,4000");
        _entityService.Get(item, UmbracoObjectTypes.Element).Returns(entity);
        SetupUser(Scope(StartNodeSubjectState.Restricted, StartNodeTree.Library), StartNodeAccess.NotVisible, StartNodeTree.Library);

        var reach = await CreateResolver().ForUserAsync(_user, item, PermissionDomain.Library);

        Assert.NotNull(reach);
        Assert.StartsWith("Library start nodes", reach.HowToFix);
        _startNodes.Received(1).Classify(Arg.Any<StartNodeScope>(), "-1,4000");
        await _startNodes.DidNotReceive().GetScopeForUserAsync(_user, StartNodeTree.Content);
    }

    /// <summary>A Library folder's path is found under the element-container object type.</summary>
    [Fact]
    public async Task Library_Folder_PathFromElementContainer()
    {
        var folder = Guid.NewGuid();
        var entity = Substitute.For<IEntitySlim>();
        entity.Path.Returns("-1,4100");
        _entityService.Get(folder, UmbracoObjectTypes.ElementContainer).Returns(entity);
        SetupUser(Scope(StartNodeSubjectState.Restricted, StartNodeTree.Library), StartNodeAccess.NotVisible, StartNodeTree.Library);

        Assert.NotNull(await CreateResolver().ForUserAsync(_user, folder, PermissionDomain.Library));
        _startNodes.Received(1).Classify(Arg.Any<StartNodeScope>(), "-1,4100");
    }

    /// <summary>Library creation is section-wide: a subject with any Library start node can create inside it.</summary>
    [Fact]
    public async Task ForLibraryCreate_Restricted_ReturnsNull()
    {
        _startNodes.GetScopeForUserAsync(_user, StartNodeTree.Library).Returns(Scope(StartNodeSubjectState.Restricted, StartNodeTree.Library));

        Assert.Null(await CreateResolver().ForLibraryCreateAsync(_user, null));
    }

    /// <summary>A subject with no Library start node cannot create anything in the Library.</summary>
    [Fact]
    public async Task ForLibraryCreate_NoAccess_ReturnsBlocked()
    {
        _startNodes.GetScopeForUserAsync(_user, StartNodeTree.Library).Returns(Scope(StartNodeSubjectState.NoAccess, StartNodeTree.Library));
        _startNodes.GetForUserAsync(_user, StartNodeTree.Library).Returns(
            new StartNodeSnapshot(StartNodeTree.Library, StartNodeSubjectState.NoAccess, [], new Dictionary<int, StartNodeInfo>(), []));

        var reach = await CreateResolver().ForLibraryCreateAsync(_user, null);

        Assert.NotNull(reach);
        Assert.Equal(
            "This user has no Library start nodes, so they can't reach the Library at all. Everything is blocked.",
            reach.Explanation);
        Assert.Empty(reach.StartNodes);
    }

    /// <summary>A user group with no Library start node is blocked from Library creation, with the group wording.</summary>
    [Fact]
    public async Task ForLibraryCreate_GroupNoAccess_ReturnsBlockedWithGroupWording()
    {
        _startNodes.GetScopeForUserGroupAsync("editors", StartNodeTree.Library).Returns(Scope(StartNodeSubjectState.NoAccess, StartNodeTree.Library));
        _startNodes.GetForUserGroupAsync("editors", StartNodeTree.Library).Returns(
            new StartNodeSnapshot(StartNodeTree.Library, StartNodeSubjectState.NoAccess, [], new Dictionary<int, StartNodeInfo>(), []));

        var reach = await CreateResolver().ForLibraryCreateAsync(null, "editors");

        Assert.Equal(
            "This user group has no Library start node, so on its own it gives no Library access. Everything is blocked.",
            reach!.Explanation);
    }
}
