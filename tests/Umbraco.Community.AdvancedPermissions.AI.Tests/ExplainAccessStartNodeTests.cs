using NSubstitute;
using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Start nodes in <see cref="ExplainAccessTool"/>'s node aspect. Umbraco checks start nodes before any
/// permission, so a node outside the subject's start nodes must be reported "Blocked (start nodes)" —
/// never "Allowed" — with the permission-only result kept in <c>IfInsideStartNodes</c>, and no permission
/// change suggested as a fix.
/// </summary>
public sealed class ExplainAccessStartNodeTests
{
    /// <summary>The focused verb used throughout.</summary>
    private const string Delete = "Umb.Document.Delete";

    /// <summary>The mocked permission service.</summary>
    private readonly IAdvancedPermissionService _permissions = Substitute.For<IAdvancedPermissionService>();

    /// <summary>The mocked path resolver.</summary>
    private readonly IContentPathResolver _pathResolver = Substitute.For<IContentPathResolver>();

    /// <summary>The mocked user group service.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

    /// <summary>The mocked security accessor.</summary>
    private readonly IBackOfficeSecurityAccessor _securityAccessor = Substitute.For<IBackOfficeSecurityAccessor>();

    /// <summary>The mocked remediation service; a blocked answer must never consult it.</summary>
    private readonly IPermissionRemediationService _remediation = Substitute.For<IPermissionRemediationService>();

    /// <summary>The mocked user service.</summary>
    private readonly IUserService _userService = Substitute.For<IUserService>();

    /// <summary>The mocked start-node reach resolver; returns null (not blocked) unless configured.</summary>
    private readonly IStartNodeReachResolver _reach = Substitute.For<IStartNodeReachResolver>();

    /// <summary>The node under test.</summary>
    private readonly Guid _node = Guid.NewGuid();

    /// <summary>The node's path from the root.</summary>
    private readonly Guid[] _path;

    /// <summary>The current user.</summary>
    private readonly Guid _user = Guid.NewGuid();

    /// <summary>A blocking reach, as the resolver would return for a node above a start node.</summary>
    private static readonly StartNodeReach Blocked = new(
        "Browse only (start nodes)",
        "Above a start node.",
        [new StartNodeRef("News", "from Editors")],
        "Start nodes are set in the Users section, on the user group or on the user account. No permission entry can change this.");

    /// <summary>
    /// The reach for a group with no start node at all (the stock "Sensitive data" group): blocked
    /// everywhere, with no start node in effect to list.
    /// </summary>
    private static readonly StartNodeReach NoStartNode = new(
        "Not visible (start nodes)",
        "This user group has no start node, so on its own it gives no content access. Every node is blocked.",
        [],
        "Start nodes are set in the Users section, on the user group or on the user account. No permission entry can change this.");

    /// <summary>Sets up two user groups, a named node, a current user in the Editors group, and its path.</summary>
    public ExplainAccessStartNodeTests()
    {
        _path = [Guid.NewGuid(), _node];
        _pathResolver.GetPathFromRoot(_node).Returns(_path);

        var groupPage = new PagedModel<IUserGroup>(2, new[] { Group("editors", "Editors"), Group("writers", "Writers") });
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(groupPage);

        var entity = Substitute.For<IEntitySlim>();
        entity.Name.Returns("Homepage");
        _entityService.Get(_node, UmbracoObjectTypes.Document).Returns(entity);

        var user = Substitute.For<IUser>();
        user.Key.Returns(_user);
        var groups = new[] { ReadOnlyGroup("editors") };
        user.Groups.Returns(groups);
        var security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns(user);
        _securityAccessor.BackOfficeSecurity.Returns(security);
        _userService.GetAsync(_user).Returns(user);
    }

    /// <summary>Builds the tool under test.</summary>
    /// <returns>A tool over the mocks.</returns>
    private ExplainAccessTool CreateTool() =>
        new(
            _permissions,
            _pathResolver,
            new PermissionPresenter(_userGroupService, _entityService, _contentTypeService),
            _userGroupService,
            _securityAccessor,
            Substitute.For<IDocTypePermissionService>(),
            _contentTypeService,
            _entityService,
            _remediation,
            _userService,
            TestGuards.AllowAll(),
            _reach);

    /// <summary>Runs the tool through the public entry point.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The tool's result.</returns>
    private Task<object> RunAsync(ExplainAccessArgs args) =>
        ((IAITool)CreateTool()).ExecuteAsync(args, CancellationToken.None);

    /// <summary>Builds a user group.</summary>
    /// <param name="alias">The alias.</param>
    /// <param name="name">The display name.</param>
    /// <returns>A substitute group.</returns>
    private static IUserGroup Group(string alias, string name)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(name);
        return group;
    }

    /// <summary>Builds a read-only user group membership.</summary>
    /// <param name="alias">The alias.</param>
    /// <returns>A substitute membership.</returns>
    private static IReadOnlyUserGroup ReadOnlyGroup(string alias)
    {
        var group = Substitute.For<IReadOnlyUserGroup>();
        group.Alias.Returns(alias);
        return group;
    }

    /// <summary>Builds an effective permission set directly on the node by the given role.</summary>
    /// <param name="verb">The verb.</param>
    /// <param name="isAllowed">Whether it is allowed.</param>
    /// <param name="role">The contributing role.</param>
    /// <returns>The permission.</returns>
    private EffectivePermission Perm(string verb, bool isAllowed, string role = "editors") =>
        new(
            verb,
            isAllowed,
            IsExplicit: true,
            Reasoning:
            [
                new PermissionReasoning(
                    role,
                    isAllowed ? PermissionState.Allow : PermissionState.Deny,
                    IsExplicit: true,
                    SourceNodeKey: _node,
                    SourceScope: PermissionScope.ThisNodeOnly,
                    IsFromGroupDefault: false),
            ]);

    /// <summary>
    /// Review focus 1: the permissions allow it, but the node is outside the user's start nodes — the
    /// answer must be Blocked, never Allowed, with the permission-only result kept alongside.
    /// </summary>
    [Fact]
    public async Task CurrentUser_OutsideStartNodes_IsBlockedNotAllowed()
    {
        _permissions.ResolveAsync(_user, _node, _path, Delete, Arg.Any<CancellationToken>()).Returns(Perm(Delete, isAllowed: true));
        _reach.ForUserAsync(_user, _node, PermissionDomain.Content).Returns(Blocked);

        var verdict = Assert.IsType<AccessVerdict>(await RunAsync(new ExplainAccessArgs(ExplainSubject.CurrentUser, _node, Verb: Delete)));

        Assert.Equal("Blocked (start nodes)", verdict.Result);
        Assert.Equal("Allowed", verdict.IfInsideStartNodes);
        Assert.Equal("Browse only (start nodes)", verdict.StartNodes!.Access);
    }

    /// <summary>Every verdict of an all-permissions answer is blocked, and the reach is carried once, at the top.</summary>
    [Fact]
    public async Task User_AllVerbs_Blocked_MarksEveryVerdictAndExplanation()
    {
        _permissions.ResolveAllAsync(_user, _node, _path, null, Arg.Any<CancellationToken>()).Returns(
            new Dictionary<string, EffectivePermission>
            {
                ["Umb.Document.Read"] = Perm("Umb.Document.Read", isAllowed: true),
                [Delete] = Perm(Delete, isAllowed: false),
            });
        _reach.ForUserAsync(_user, _node, PermissionDomain.Content).Returns(Blocked);

        var explanation = Assert.IsType<AccessExplanation>(await RunAsync(new ExplainAccessArgs(ExplainSubject.User, _node, UserKey: _user)));

        Assert.Same(Blocked, explanation.StartNodes);
        Assert.All(explanation.Permissions, v =>
        {
            Assert.Equal("Blocked (start nodes)", v.Result);
            Assert.Null(v.StartNodes);
        });
        Assert.Contains(explanation.Permissions, v => v.Permission == "Read" && v.IfInsideStartNodes == "Allowed");
        Assert.Contains(explanation.Permissions, v => v.Permission == "Delete" && v.IfInsideStartNodes == "Denied");
    }

    /// <summary>A blocked user group's answer carries the resolver's reach, including its caveat about combined start nodes.</summary>
    [Fact]
    public async Task UserGroup_Blocked_CarriesRoleModeNote()
    {
        var withNote = Blocked with { Note = "Users get the combined start nodes of all their groups." };
        _permissions.ResolveForRoleAsync("editors", _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [Delete] = Perm(Delete, isAllowed: true) });
        _reach.ForUserGroupAsync("editors", _node, PermissionDomain.Content).Returns(withNote);

        var verdict = Assert.IsType<AccessVerdict>(await RunAsync(
            new ExplainAccessArgs(ExplainSubject.UserGroup, _node, UserGroupAlias: "editors", Verb: Delete)));

        Assert.Equal("Blocked (start nodes)", verdict.Result);
        Assert.Equal(withNote.Note, verdict.StartNodes!.Note);
    }

    /// <summary>Review focus 2: a subject start nodes do not restrict gets exactly the answer it got before.</summary>
    [Fact]
    public async Task RootAccess_LeavesVerdictUnchanged()
    {
        _permissions.ResolveAsync(_user, _node, _path, Delete, Arg.Any<CancellationToken>()).Returns(Perm(Delete, isAllowed: true));

        var verdict = Assert.IsType<AccessVerdict>(await RunAsync(new ExplainAccessArgs(ExplainSubject.CurrentUser, _node, Verb: Delete)));

        Assert.Equal("Allowed", verdict.Result);
        Assert.Null(verdict.IfInsideStartNodes);
        Assert.Null(verdict.StartNodes);
        await _reach.Received(1).ForUserAsync(_user, _node, PermissionDomain.Content);
    }

    /// <summary>A blocked node cannot be fixed with a permission entry, so no fix is computed or offered.</summary>
    [Fact]
    public async Task SuggestFix_WhenBlocked_ReturnsNoRemediations()
    {
        _permissions.ResolveAsync(_user, _node, _path, Delete, Arg.Any<CancellationToken>()).Returns(Perm(Delete, isAllowed: false));
        _reach.ForUserAsync(_user, _node, PermissionDomain.Content).Returns(Blocked);

        var verdict = Assert.IsType<AccessVerdict>(await RunAsync(
            new ExplainAccessArgs(ExplainSubject.CurrentUser, _node, Verb: Delete, SuggestFix: true)));

        Assert.Equal("Blocked (start nodes)", verdict.Result);
        Assert.Equal("Denied", verdict.IfInsideStartNodes);
        Assert.Null(verdict.Remediations);
        await _remediation.DidNotReceiveWithAnyArgs().SuggestAsync(default, default!, default!, default!, default, default, default);
    }

    /// <summary>
    /// "Who can delete here?": a group whose own start node does not reach the node keeps its permission
    /// result — its members may reach the node through another group (base <c>concepts.md</c>: a permission
    /// from one group also applies inside a start node another group provides) — and is additionally
    /// flagged in <c>BlockedUserGroups</c>, with the combined-start-nodes caveat. All Users is never flagged.
    /// </summary>
    [Fact]
    public async Task AllUserGroups_BlockedGroupsListedSeparately()
    {
        _permissions.ResolveForRoleAsync(Arg.Any<string>(), _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [Delete] = Perm(Delete, isAllowed: true) });
        _reach.ForUserGroupAsync("editors", _node, PermissionDomain.Content).Returns(Blocked);

        var roster = Assert.IsType<AccessRoster>(await RunAsync(new ExplainAccessArgs(ExplainSubject.AllUserGroups, _node, Verb: Delete)));

        Assert.Equal(["Editors"], roster.BlockedUserGroups!);
        Assert.Contains("Editors", roster.AllowedUserGroups);
        Assert.Contains("Writers", roster.AllowedUserGroups);
        Assert.Equal(3, roster.AllowedUserGroups.Count + roster.DeniedUserGroups.Count);
        Assert.Contains("combined start nodes", roster.StartNodeNote!, StringComparison.Ordinal);
    }

    /// <summary>With no group blocked, the roster has no blocked list at all — the answer reads exactly as before.</summary>
    [Fact]
    public async Task AllUserGroups_NoneBlocked_HasNoBlockedList()
    {
        _permissions.ResolveForRoleAsync(Arg.Any<string>(), _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [Delete] = Perm(Delete, isAllowed: true) });

        var roster = Assert.IsType<AccessRoster>(await RunAsync(new ExplainAccessArgs(ExplainSubject.AllUserGroups, _node, Verb: Delete)));

        Assert.Null(roster.BlockedUserGroups);
        Assert.Null(roster.StartNodeNote);
    }

    /// <summary>The all-permissions roster report blocks the same groups in every permission's roster.</summary>
    [Fact]
    public async Task AllUserGroups_AllVerbs_BlockedInEveryRoster()
    {
        _permissions.ResolveForRoleAsync(Arg.Any<string>(), _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                ["Umb.Document.Read"] = Perm("Umb.Document.Read", isAllowed: true),
                [Delete] = Perm(Delete, isAllowed: false),
            });
        _reach.ForUserGroupAsync("writers", _node, PermissionDomain.Content).Returns(Blocked);

        var report = Assert.IsType<AccessRosterReport>(await RunAsync(new ExplainAccessArgs(ExplainSubject.AllUserGroups, _node)));

        Assert.All(report.Permissions, r => Assert.Equal(["Writers"], r.BlockedUserGroups!));
    }

    /// <summary>
    /// A group with no start node at all — the stock "Sensitive data" group, which only adds rights — is
    /// blocked on every node, so flagging it with the full caveat would put the same noise in every roster.
    /// It gets a list and a shorter note of its own, and the full caveat stays for groups whose start node
    /// is elsewhere.
    /// </summary>
    [Fact]
    public async Task AllUserGroups_GroupWithNoStartNode_GetsItsOwnShorterNote()
    {
        _permissions.ResolveForRoleAsync(Arg.Any<string>(), _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [Delete] = Perm(Delete, isAllowed: true) });
        _reach.ForUserGroupAsync("writers", _node, PermissionDomain.Content).Returns(NoStartNode);

        var roster = Assert.IsType<AccessRoster>(await RunAsync(new ExplainAccessArgs(ExplainSubject.AllUserGroups, _node, Verb: Delete)));

        Assert.Null(roster.BlockedUserGroups);
        Assert.Null(roster.StartNodeNote);
        Assert.Equal(["Writers"], roster.NoStartNodeUserGroups!);
        Assert.Equal(StartNodeReach.NoStartNodeRosterNote, roster.NoStartNodeNote);
        Assert.True(StartNodeReach.NoStartNodeRosterNote.Length < StartNodeReach.RosterNote.Length);
        Assert.Contains("Writers", roster.AllowedUserGroups);
    }

    /// <summary>Both kinds of flag can appear in one roster, each with its own note.</summary>
    [Fact]
    public async Task AllUserGroups_BlockedAndNoStartNode_AreListedSeparately()
    {
        _permissions.ResolveForRoleAsync(Arg.Any<string>(), _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [Delete] = Perm(Delete, isAllowed: true) });
        _reach.ForUserGroupAsync("editors", _node, PermissionDomain.Content).Returns(Blocked);
        _reach.ForUserGroupAsync("writers", _node, PermissionDomain.Content).Returns(NoStartNode);

        var roster = Assert.IsType<AccessRoster>(await RunAsync(new ExplainAccessArgs(ExplainSubject.AllUserGroups, _node, Verb: Delete)));

        Assert.Equal(["Editors"], roster.BlockedUserGroups!);
        Assert.Equal(StartNodeReach.RosterNote, roster.StartNodeNote);
        Assert.Equal(["Writers"], roster.NoStartNodeUserGroups!);
        Assert.Equal(StartNodeReach.NoStartNodeRosterNote, roster.NoStartNodeNote);
    }
}
