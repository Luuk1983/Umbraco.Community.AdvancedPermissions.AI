using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Default <see cref="IStartNodeReachResolver"/>, over the base package's <see cref="IStartNodeAccessService"/>
/// — the same classification its Access Viewer and Insert Options Viewer use. Every editor-facing string is
/// copied from the base package's <c>localization/en.ts</c> (the <c>startNodes_*</c> keys) so the copilot
/// uses the words the editor sees in the grid.
/// </summary>
/// <remarks>
/// The v17 base service has no cached scope (v18 added one): each lookup loads one snapshot, which is used
/// both to classify the node and, when blocked, to list the start nodes in effect.
/// </remarks>
/// <param name="startNodes">The base package's start-node service.</param>
/// <param name="entityService">The Umbraco entity service, used to read a node's materialised path.</param>
public sealed class StartNodeReachResolver(IStartNodeAccessService startNodes, IEntityService entityService)
    : IStartNodeReachResolver
{
    /// <summary>The materialised path of the content root.</summary>
    private const string RootPath = "-1";

    /// <summary>What a "Browse only" node means (<c>startNodes_badgePathOnlyTitle</c>).</summary>
    private const string PathOnlyExplanation =
        "Above a start node. Umbraco shows it in the tree only so users can browse down to their start node; it can't be opened.";

    /// <summary>What a "Not visible" node means (<c>startNodes_badgeNotVisibleTitle</c>).</summary>
    private const string NotVisibleExplanation = "Outside every start node, so Umbraco hides it from the tree.";

    /// <summary>A user without any content start node (<c>startNodes_introNoAccessUser</c>).</summary>
    private const string NoAccessUser = "This user has no start nodes, so they can't reach any content. Every node is blocked.";

    /// <summary>A user group without a content start node (<c>startNodes_introNoAccessRole</c>).</summary>
    private const string NoAccessGroup = "This user group has no start node, so on its own it gives no content access. Every node is blocked.";

    /// <summary>
    /// The caveat for a single user group (<c>startNodes_roleModeNote</c>), with the viewer's "Pick a user"
    /// rephrased for a conversation, where there is no picker.
    /// </summary>
    private const string GroupNote =
        "Users get the combined start nodes of all their groups, so ask about a specific user to see the real result.";

    /// <summary>Where start nodes are changed, and that a permission entry cannot help.</summary>
    private const string HowToFix =
        "Start nodes are set in the Users section, on the user group or on the user account. No permission entry can change this.";

    /// <summary>A start node set on the user account (<c>startNodes_sourceUser</c>).</summary>
    private const string SetOnUser = "set on the user";

    /// <inheritdoc />
    public async Task<StartNodeReach?> ForUserAsync(Guid userKey, Guid nodeKey) =>
        Reach(await startNodes.GetForUserAsync(userKey), nodeKey, isGroup: false);

    /// <inheritdoc />
    public async Task<StartNodeReach?> ForUserGroupAsync(string userGroupAlias, Guid nodeKey) =>
        IsEveryone(userGroupAlias)
            ? null
            : Reach(await startNodes.GetForUserGroupAsync(userGroupAlias), nodeKey, isGroup: true);

    /// <summary>Whether an alias is the All Users group, which start nodes never apply to.</summary>
    /// <param name="alias">The alias.</param>
    /// <returns><see langword="true"/> for the All Users alias.</returns>
    private static bool IsEveryone(string alias) =>
        string.Equals(alias, AdvancedPermissionsConstants.EveryoneRoleAlias, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Classifies a node for a snapshot and builds the reach when it is blocked, or returns
    /// <see langword="null"/> when the node is reachable or cannot be verified.
    /// </summary>
    /// <param name="snapshot">The subject's snapshot; <see langword="null"/> for an unknown subject.</param>
    /// <param name="nodeKey">The node.</param>
    /// <param name="isGroup">Whether the subject is a single user group.</param>
    /// <returns>The reach, or <see langword="null"/>.</returns>
    private StartNodeReach? Reach(StartNodeSnapshot? snapshot, Guid nodeKey, bool isGroup)
    {
        if (snapshot is null || GetPath(nodeKey) is not { } path)
        {
            return null;
        }

        var access = startNodes.Classify(snapshot, path);
        return access == StartNodeAccess.Accessible ? null : Build(access, snapshot, isGroup);
    }

    /// <summary>Reads a content node's materialised path; the virtual-root sentinel is the content root.</summary>
    /// <param name="nodeKey">The node.</param>
    /// <returns>The path, or <see langword="null"/> when the node cannot be found.</returns>
    private string? GetPath(Guid nodeKey) =>
        nodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey
            ? RootPath
            : entityService.Get(nodeKey, UmbracoObjectTypes.Document)?.Path is { Length: > 0 } path ? path : null;

    /// <summary>Builds the friendly reach for a blocked subject.</summary>
    /// <param name="access">How the node is blocked.</param>
    /// <param name="snapshot">The subject's snapshot, for its state and the start nodes in effect.</param>
    /// <param name="isGroup">Whether the subject is a single user group.</param>
    /// <returns>The reach.</returns>
    private static StartNodeReach Build(StartNodeAccess access, StartNodeSnapshot snapshot, bool isGroup)
    {
        var explanation = snapshot.State == StartNodeSubjectState.NoAccess
            ? isGroup ? NoAccessGroup : NoAccessUser
            : access == StartNodeAccess.PathOnly ? PathOnlyExplanation : NotVisibleExplanation;

        return new StartNodeReach(
            access == StartNodeAccess.PathOnly ? StartNodeReach.PathOnlyAccess : StartNodeReach.NotVisibleAccess,
            explanation,
            StartNodesInEffect(snapshot),
            HowToFix,
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
