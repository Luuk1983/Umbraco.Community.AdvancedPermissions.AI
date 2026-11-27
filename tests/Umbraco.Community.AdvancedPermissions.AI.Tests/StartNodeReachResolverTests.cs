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

    /// <summary>
    /// A snapshot whose only effective start node is "News" (id 2000), given by the Editors group; the
    /// Writers group's own start node is covered by it and so is not in effect.
    /// </summary>
    /// <param name="state">The subject's start-node state.</param>
    /// <returns>The snapshot.</returns>
    private static StartNodeSnapshot NewsSnapshot(StartNodeSubjectState state = StartNodeSubjectState.Restricted) =>
        new(
            state,
            state == StartNodeSubjectState.Restricted ? [2000] : [],
            new Dictionary<int, StartNodeInfo> { [2000] = new(2000, Guid.NewGuid(), "News", "-1,2000") },
            state == StartNodeSubjectState.Restricted
                ?
                [
                    new StartNodeSourceResult(new StartNodeSource(StartNodeSourceKind.UserGroup, "editors", "Editors", 2000), StartNodeSourceStatus.InUse, null),
                    new StartNodeSourceResult(new StartNodeSource(StartNodeSourceKind.UserGroup, "writers", "Writers", 3000), StartNodeSourceStatus.CoveredBy, 2000),
                ]
                : []);

    /// <summary>Configures the user's snapshot and the classification of any path against it.</summary>
    /// <param name="snapshot">The snapshot to return.</param>
    /// <param name="access">The classification to return.</param>
    private void SetupUser(StartNodeSnapshot snapshot, StartNodeAccess access)
    {
        _startNodes.GetForUserAsync(_user).Returns(snapshot);
        _startNodes.Classify(snapshot, Arg.Any<string>()).Returns(access);
    }

    /// <summary>A reachable node is not blocked, so there is nothing to report.</summary>
    [Fact]
    public async Task ForUser_Accessible_ReturnsNull()
    {
        SetupUser(NewsSnapshot(), StartNodeAccess.Accessible);

        Assert.Null(await CreateResolver().ForUserAsync(_user, Node));
    }

    /// <summary>An ancestor of a start node is "Browse only", with the start nodes in effect and who set them.</summary>
    [Fact]
    public async Task ForUser_PathOnly_ReturnsBrowseOnlyWithStartNodes()
    {
        SetupUser(NewsSnapshot(), StartNodeAccess.PathOnly);

        var reach = await CreateResolver().ForUserAsync(_user, Node);

        Assert.NotNull(reach);
        Assert.Equal("Browse only (start nodes)", reach.Access);
        Assert.Equal(
            "Above a start node. Umbraco shows it in the tree only so users can browse down to their start node; it can't be opened.",
            reach.Explanation);
        Assert.Equal(new StartNodeRef("News", "from Editors"), Assert.Single(reach.StartNodes));
        Assert.Equal(
            "Start nodes are set in the Users section, on the user group or on the user account. No permission entry can change this.",
            reach.HowToFix);
        Assert.Null(reach.Note);
        _startNodes.Received(1).Classify(Arg.Any<StartNodeSnapshot>(), NodePath);
    }

    /// <summary>A node outside every start node is "Not visible".</summary>
    [Fact]
    public async Task ForUser_NotVisible_ReturnsNotVisible()
    {
        SetupUser(NewsSnapshot(), StartNodeAccess.NotVisible);

        var reach = await CreateResolver().ForUserAsync(_user, Node);

        Assert.Equal("Not visible (start nodes)", reach!.Access);
        Assert.Equal("Outside every start node, so Umbraco hides it from the tree.", reach.Explanation);
    }

    /// <summary>A user with no start node at all gets the no-access explanation rather than a per-node one.</summary>
    [Fact]
    public async Task ForUser_NoAccess_UsesNoAccessExplanation()
    {
        SetupUser(NewsSnapshot(StartNodeSubjectState.NoAccess), StartNodeAccess.NotVisible);

        var reach = await CreateResolver().ForUserAsync(_user, Node);

        Assert.Equal("This user has no start nodes, so they can't reach any content. Every node is blocked.", reach!.Explanation);
        Assert.Empty(reach.StartNodes);
    }

    /// <summary>A start node set both on the account and by a group says both, in the viewer's words.</summary>
    [Fact]
    public async Task ForUser_StartNodeSetOnUserAndGroup_SaysBoth()
    {
        var snapshot = new StartNodeSnapshot(
            StartNodeSubjectState.Restricted,
            [2000],
            new Dictionary<int, StartNodeInfo> { [2000] = new(2000, Guid.NewGuid(), "News", "-1,2000") },
            [
                new StartNodeSourceResult(new StartNodeSource(StartNodeSourceKind.User, null, null, 2000), StartNodeSourceStatus.InUse, null),
                new StartNodeSourceResult(new StartNodeSource(StartNodeSourceKind.UserGroup, "editors", "Editors", 2000), StartNodeSourceStatus.InUse, null),
            ]);
        SetupUser(snapshot, StartNodeAccess.NotVisible);

        var reach = await CreateResolver().ForUserAsync(_user, Node);

        Assert.Equal(new StartNodeRef("News", "set on the user and from Editors"), Assert.Single(reach!.StartNodes));
    }

    /// <summary>A start node set only on the user account reads "set on the user".</summary>
    [Fact]
    public async Task ForUser_StartNodeSetOnUserOnly_SaysSetOnTheUser()
    {
        var snapshot = new StartNodeSnapshot(
            StartNodeSubjectState.Restricted,
            [2000],
            new Dictionary<int, StartNodeInfo> { [2000] = new(2000, Guid.NewGuid(), "News", "-1,2000") },
            [new StartNodeSourceResult(new StartNodeSource(StartNodeSourceKind.User, null, null, 2000), StartNodeSourceStatus.InUse, null)]);
        SetupUser(snapshot, StartNodeAccess.NotVisible);

        var reach = await CreateResolver().ForUserAsync(_user, Node);

        Assert.Equal(new StartNodeRef("News", "set on the user"), Assert.Single(reach!.StartNodes));
    }

    /// <summary>Configured start nodes that change nothing (here: covered by a higher one) are left out.</summary>
    [Fact]
    public async Task ForUser_OnlyInUseSourcesListed()
    {
        SetupUser(NewsSnapshot(), StartNodeAccess.NotVisible);

        var reach = await CreateResolver().ForUserAsync(_user, Node);

        Assert.DoesNotContain(reach!.StartNodes, s => s.SetBy.Contains("Writers", StringComparison.Ordinal));
    }

    /// <summary>Start nodes do not apply to the All Users group, so it is never reported as blocked.</summary>
    /// <param name="alias">The All Users alias, in any casing.</param>
    [Theory]
    [InlineData(AdvancedPermissionsConstants.EveryoneRoleAlias)]
    [InlineData("$EVERYONE")]
    public async Task ForUserGroup_Everyone_ReturnsNull(string alias)
    {
        Assert.Null(await CreateResolver().ForUserGroupAsync(alias, Node));
        Assert.Empty(_startNodes.ReceivedCalls());
    }

    /// <summary>A blocked user group carries the note that users combine the start nodes of all their groups.</summary>
    [Fact]
    public async Task ForUserGroup_Blocked_CarriesGroupNote()
    {
        var snapshot = NewsSnapshot();
        _startNodes.GetForUserGroupAsync("editors").Returns(snapshot);
        _startNodes.Classify(snapshot, NodePath).Returns(StartNodeAccess.NotVisible);

        var reach = await CreateResolver().ForUserGroupAsync("editors", Node);

        Assert.Equal(
            "Users get the combined start nodes of all their groups, so ask about a specific user to see the real result.",
            reach!.Note);
    }

    /// <summary>A user group with no start node gets the group's no-access wording.</summary>
    [Fact]
    public async Task ForUserGroup_NoAccess_UsesGroupWording()
    {
        var snapshot = NewsSnapshot(StartNodeSubjectState.NoAccess);
        _startNodes.GetForUserGroupAsync("editors").Returns(snapshot);
        _startNodes.Classify(snapshot, NodePath).Returns(StartNodeAccess.NotVisible);

        var reach = await CreateResolver().ForUserGroupAsync("editors", Node);

        Assert.Equal(
            "This user group has no start node, so on its own it gives no content access. Every node is blocked.",
            reach!.Explanation);
    }

    /// <summary>An unknown user cannot be verified, so no restriction is claimed (the base viewer does the same).</summary>
    [Fact]
    public async Task ForUser_UnknownUser_ReturnsNull()
    {
        _startNodes.GetForUserAsync(_user).Returns((StartNodeSnapshot?)null);

        Assert.Null(await CreateResolver().ForUserAsync(_user, Node));
    }

    /// <summary>An unknown user group cannot be verified either.</summary>
    [Fact]
    public async Task ForUserGroup_UnknownGroup_ReturnsNull()
    {
        _startNodes.GetForUserGroupAsync("ghosts").Returns((StartNodeSnapshot?)null);

        Assert.Null(await CreateResolver().ForUserGroupAsync("ghosts", Node));
    }

    /// <summary>
    /// A node whose path cannot be read is not claimed as blocked. (The base controllers show such a node
    /// as not visible; the copilot does not claim a block it cannot verify.)
    /// </summary>
    [Fact]
    public async Task ForUser_UnreadablePath_ReturnsNull()
    {
        SetupUser(NewsSnapshot(), StartNodeAccess.NotVisible);

        Assert.Null(await CreateResolver().ForUserAsync(_user, Guid.NewGuid()));
    }

    /// <summary>The virtual root (the Default permissions row, or creating at the root) is classified as <c>-1</c>.</summary>
    [Fact]
    public async Task VirtualRoot_ClassifiesPathMinusOne()
    {
        SetupUser(NewsSnapshot(), StartNodeAccess.PathOnly);

        var reach = await CreateResolver().ForUserAsync(_user, AdvancedPermissionsConstants.VirtualRootNodeKey);

        Assert.NotNull(reach);
        _startNodes.Received(1).Classify(Arg.Any<StartNodeSnapshot>(), "-1");
    }
}
