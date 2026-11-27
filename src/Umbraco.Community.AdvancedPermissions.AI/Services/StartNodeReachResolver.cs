using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Default <see cref="IStartNodeReachResolver"/>, over the base package's <see cref="IStartNodeAccessService"/>
/// — the same classification its Access Viewers use. Every editor-facing string is copied from the base
/// package's <c>localization/en.ts</c> (the <c>startNodes_*</c> keys) so the copilot uses the words the
/// editor sees in the grid.
/// </summary>
/// <param name="startNodes">The base package's start-node service.</param>
/// <param name="entityService">The Umbraco entity service, used to read a node's materialised path.</param>
public sealed class StartNodeReachResolver(IStartNodeAccessService startNodes, IEntityService entityService)
    : IStartNodeReachResolver
{
    /// <summary>The materialised path of a tree's root.</summary>
    private const string RootPath = "-1";

    /// <summary>The viewer's label for an ancestor of a start node (<c>startNodes_badgePathOnly</c>).</summary>
    private const string PathOnlyLabel = StartNodeReach.PathOnlyAccess;

    /// <summary>What <see cref="PathOnlyLabel"/> means (<c>startNodes_badgePathOnlyTitle</c>).</summary>
    private const string PathOnlyExplanation =
        "Above a start node. Umbraco shows it in the tree only so users can browse down to their start node; it can't be opened.";

    /// <summary>The viewer's label for a node outside every start node (<c>startNodes_badgeNotVisible</c>).</summary>
    private const string NotVisibleLabel = StartNodeReach.NotVisibleAccess;

    /// <summary>What <see cref="NotVisibleLabel"/> means (<c>startNodes_badgeNotVisibleTitle</c>).</summary>
    private const string NotVisibleExplanation = "Outside every start node, so Umbraco hides it from the tree.";

    /// <summary>A user without any content start node (<c>startNodes_introNoAccessUser</c>).</summary>
    private const string NoAccessUser = "This user has no start nodes, so they can't reach any content. Every node is blocked.";

    /// <summary>A user group without a content start node (<c>startNodes_introNoAccessRole</c>).</summary>
    private const string NoAccessGroup = "This user group has no start node, so on its own it gives no content access. Every node is blocked.";

    /// <summary>A user without any Library start node (<c>startNodes_library_introNoAccessUser</c>).</summary>
    private const string LibraryNoAccessUser =
        "This user has no Library start nodes, so they can't reach the Library at all. Everything is blocked.";

    /// <summary>A user group without a Library start node (<c>startNodes_library_introNoAccessRole</c>).</summary>
    private const string LibraryNoAccessGroup =
        "This user group has no Library start node, so on its own it gives no Library access. Everything is blocked.";

    /// <summary>
    /// The caveat for a single user group (<c>startNodes_roleModeNote</c>), with the viewer's "Pick a user"
    /// rephrased for a conversation, where there is no picker.
    /// </summary>
    private const string GroupNote =
        "Users get the combined start nodes of all their groups, so ask about a specific user to see the real result.";

    /// <summary>Where content start nodes are changed, and that a permission entry cannot help.</summary>
    private const string ContentFix =
        "Start nodes are set in the Users section, on the user group or on the user account. No permission entry can change this.";

    /// <summary>Where Library start nodes are changed, and that a permission entry cannot help.</summary>
    private const string LibraryFix =
        "Library start nodes are set in the Users section, on the user group or on the user account. No permission entry can change this.";

    /// <summary>A start node set on the user account (<c>startNodes_sourceUser</c>).</summary>
    private const string SetOnUser = "set on the user";

    /// <inheritdoc />
    public async Task<StartNodeReach?> ForUserAsync(Guid userKey, Guid nodeKey, PermissionDomain domain)
    {
        var tree = ToTree(domain);
        var access = Classify(await startNodes.GetScopeForUserAsync(userKey, tree), nodeKey, domain);
        return access is { } blocked
            ? Build(blocked.Access, blocked.State, await startNodes.GetForUserAsync(userKey, tree), domain, isGroup: false)
            : null;
    }

    /// <inheritdoc />
    public async Task<StartNodeReach?> ForUserGroupAsync(string userGroupAlias, Guid nodeKey, PermissionDomain domain)
    {
        if (IsEveryone(userGroupAlias))
        {
            return null;
        }

        var tree = ToTree(domain);
        var access = Classify(await startNodes.GetScopeForUserGroupAsync(userGroupAlias, tree), nodeKey, domain);
        return access is { } blocked
            ? Build(blocked.Access, blocked.State, await startNodes.GetForUserGroupAsync(userGroupAlias, tree), domain, isGroup: true)
            : null;
    }

    /// <inheritdoc />
    public async Task<StartNodeReach?> ForLibraryCreateAsync(Guid? userKey, string? userGroupAlias)
    {
        if (userKey is { } user)
        {
            var scope = await startNodes.GetScopeForUserAsync(user, StartNodeTree.Library);
            return scope?.State == StartNodeSubjectState.NoAccess
                ? Build(StartNodeAccess.NotVisible, scope.State, await startNodes.GetForUserAsync(user, StartNodeTree.Library), PermissionDomain.Library, isGroup: false)
                : null;
        }

        if (string.IsNullOrWhiteSpace(userGroupAlias) || IsEveryone(userGroupAlias))
        {
            return null;
        }

        var groupScope = await startNodes.GetScopeForUserGroupAsync(userGroupAlias, StartNodeTree.Library);
        return groupScope?.State == StartNodeSubjectState.NoAccess
            ? Build(StartNodeAccess.NotVisible, groupScope.State, await startNodes.GetForUserGroupAsync(userGroupAlias, StartNodeTree.Library), PermissionDomain.Library, isGroup: true)
            : null;
    }

    /// <summary>Whether an alias is the All Users group, which start nodes never apply to.</summary>
    /// <param name="alias">The alias.</param>
    /// <returns><see langword="true"/> for the All Users alias.</returns>
    private static bool IsEveryone(string alias) =>
        string.Equals(alias, AdvancedPermissionsConstants.EveryoneRoleAlias, StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps the package's tree to the base package's start-node tree.</summary>
    /// <param name="domain">The tree.</param>
    /// <returns>The matching start-node tree.</returns>
    private static StartNodeTree ToTree(PermissionDomain domain) =>
        domain == PermissionDomain.Library ? StartNodeTree.Library : StartNodeTree.Content;

    /// <summary>
    /// Classifies a node for a scope, returning the blocking access and the subject's state, or
    /// <see langword="null"/> when the node is reachable or cannot be verified.
    /// </summary>
    /// <param name="scope">The subject's scope; <see langword="null"/> for an unknown subject.</param>
    /// <param name="nodeKey">The node.</param>
    /// <param name="domain">The node's tree.</param>
    /// <returns>The blocking classification, or <see langword="null"/>.</returns>
    private (StartNodeAccess Access, StartNodeSubjectState State)? Classify(
        StartNodeScope? scope,
        Guid nodeKey,
        PermissionDomain domain)
    {
        if (scope is null || GetPath(nodeKey, domain) is not { } path)
        {
            return null;
        }

        var access = startNodes.Classify(scope, path);
        return access == StartNodeAccess.Accessible ? null : (access, scope.State);
    }

    /// <summary>Reads a node's materialised path; the virtual-root sentinel is the tree root.</summary>
    /// <param name="nodeKey">The node.</param>
    /// <param name="domain">The node's tree: documents, or Library elements and folders.</param>
    /// <returns>The path, or <see langword="null"/> when the node cannot be found.</returns>
    private string? GetPath(Guid nodeKey, PermissionDomain domain)
    {
        if (nodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey)
        {
            return RootPath;
        }

        return domain == PermissionDomain.Library
            ? PathOf(nodeKey, UmbracoObjectTypes.Element) ?? PathOf(nodeKey, UmbracoObjectTypes.ElementContainer)
            : PathOf(nodeKey, UmbracoObjectTypes.Document);
    }

    /// <summary>Reads a node's path under one object type; an empty path counts as not found.</summary>
    /// <param name="nodeKey">The node.</param>
    /// <param name="objectType">The object type to look it up as.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    private string? PathOf(Guid nodeKey, UmbracoObjectTypes objectType) =>
        entityService.Get(nodeKey, objectType)?.Path is { Length: > 0 } path ? path : null;

    /// <summary>Builds the friendly reach for a blocked subject.</summary>
    /// <param name="access">How the node is blocked.</param>
    /// <param name="state">The subject's overall start-node state.</param>
    /// <param name="snapshot">The subject's snapshot, for the start nodes in effect; may be missing.</param>
    /// <param name="domain">The tree.</param>
    /// <param name="isGroup">Whether the subject is a single user group.</param>
    /// <returns>The reach.</returns>
    private static StartNodeReach Build(
        StartNodeAccess access,
        StartNodeSubjectState state,
        StartNodeSnapshot? snapshot,
        PermissionDomain domain,
        bool isGroup)
    {
        var library = domain == PermissionDomain.Library;
        var explanation = state == StartNodeSubjectState.NoAccess
            ? (library, isGroup) switch
            {
                (true, true) => LibraryNoAccessGroup,
                (true, false) => LibraryNoAccessUser,
                (false, true) => NoAccessGroup,
                _ => NoAccessUser,
            }
            : access == StartNodeAccess.PathOnly ? PathOnlyExplanation : NotVisibleExplanation;

        return new StartNodeReach(
            access == StartNodeAccess.PathOnly ? PathOnlyLabel : NotVisibleLabel,
            explanation,
            snapshot is null ? [] : StartNodesInEffect(snapshot),
            library ? LibraryFix : ContentFix,
            isGroup ? GroupNote : null);
    }

    /// <summary>
    /// The start nodes in effect, one per node, with who set each. Configured start nodes that change
    /// nothing (covered, replaced, deleted) are left out, as on the viewer's card.
    /// </summary>
    /// <param name="snapshot">The subject's snapshot.</param>
    /// <returns>The start nodes in effect.</returns>
    private static IReadOnlyList<StartNodeRef> StartNodesInEffect(StartNodeSnapshot snapshot) =>
        snapshot.Sources
            .Where(s => s.Status == StartNodeSourceStatus.InUse && s.Source.StartNodeId is not null)
            .GroupBy(s => s.Source.StartNodeId!.Value)
            .Select(g => new StartNodeRef(
                snapshot.Nodes.TryGetValue(g.Key, out var info) && !string.IsNullOrEmpty(info.Name) ? info.Name : "the root",
                SetBy(g.Select(s => s.Source).ToList())))
            .ToList();

    /// <summary>Phrases who set a start node, in the viewer's words (<c>startNodes_source*</c>).</summary>
    /// <param name="sources">Every source that sets this start node.</param>
    /// <returns>"set on the user", "from Editors", or "set on the user and from Editors".</returns>
    private static string SetBy(IReadOnlyList<StartNodeSource> sources)
    {
        var onUser = sources.Any(s => s.Kind == StartNodeSourceKind.User);
        var groups = string.Join(", ", sources
            .Where(s => s.Kind == StartNodeSourceKind.UserGroup)
            .Select(s => s.UserGroupName ?? s.UserGroupAlias)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct());

        return (onUser, groups.Length > 0) switch
        {
            (true, true) => $"set on the user and from {groups}",
            (true, false) => SetOnUser,
            _ => $"from {groups}",
        };
    }
}
