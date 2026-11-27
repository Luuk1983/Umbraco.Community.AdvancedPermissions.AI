namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// One start node in effect for a subject, by its editor-facing name, with who set it — the same two
/// facts the base package's start-node card shows under the viewers' picker.
/// </summary>
/// <param name="Name">The start node's name, e.g. "News".</param>
/// <param name="SetBy">
/// Who set it, in the viewer's own words: "set on the user", "from Editors", or "set on the user and from
/// Editors".
/// </param>
public sealed record StartNodeRef(string Name, string SetBy);

/// <summary>
/// Why start nodes block a subject from a node (or, for Library creation, from the whole Library).
/// Umbraco checks start nodes <b>before</b> any permission, so a node outside them is blocked whatever the
/// permission entries say. Attached to an answer only when the subject is blocked; its presence is what
/// turns a result into "Blocked (start nodes)". Every field is an editor-facing label.
/// </summary>
/// <param name="Access">
/// The viewer's label for the blocked cell: "Browse only (start nodes)" for an ancestor of a start node,
/// "Not visible (start nodes)" for everything outside them.
/// </param>
/// <param name="Explanation">What that means, in the viewer's own words.</param>
/// <param name="StartNodes">The start nodes in effect and who set each one; empty when there are none.</param>
/// <param name="HowToFix">
/// Where start nodes are changed. Always says that no permission entry can change this, because a
/// suggested permission change would not help.
/// </param>
/// <param name="Note">
/// An optional caveat — set when the subject is a single user group, whose own start node is not what a
/// user gets: users combine the start nodes of all their groups.
/// </param>
public sealed record StartNodeReach(
    string Access,
    string Explanation,
    IReadOnlyList<StartNodeRef> StartNodes,
    string HowToFix,
    string? Note = null)
{
    /// <summary>
    /// The result every verdict carries when start nodes block it, in place of "Allowed"/"Denied". The
    /// permission-only result moves to the verdict's <c>IfInsideStartNodes</c> field, mirroring the
    /// viewer's "If this node were inside a start node" detail.
    /// </summary>
    public const string BlockedResult = "Blocked (start nodes)";

    /// <summary>
    /// The <see cref="Access"/> label for an ancestor of a start node (<c>startNodes_badgePathOnly</c>):
    /// Umbraco shows it in the tree for browsing, but it can't be opened.
    /// </summary>
    public const string PathOnlyAccess = "Browse only (start nodes)";

    /// <summary>
    /// The <see cref="Access"/> label for a node outside every start node (<c>startNodes_badgeNotVisible</c>):
    /// Umbraco hides it from the tree.
    /// </summary>
    public const string NotVisibleAccess = "Not visible (start nodes)";

    /// <summary>
    /// The caveat every roster carries when it flags user groups whose own start node does not reach the
    /// node — the conversational form of the base package's <c>startNodes_roleModeNote</c>. A group's own
    /// start node is not the whole story: a permission from one group also applies inside a start node that
    /// another group provides, so the flagged groups keep their allowed/denied result.
    /// </summary>
    public const string RosterNote =
        "These user groups' own start nodes don't reach here, so on its own each gives no access here. Users get " +
        "the combined start nodes of all their groups, so a member can still get in through another group or " +
        "their own start node, and the permissions listed then apply. Ask about a specific user to see the real result.";

    /// <summary>
    /// The shorter caveat a roster carries for user groups with no start node at all, such as the stock
    /// "Sensitive data" group. Such a group is blocked on every node by design — it only adds rights to
    /// members who get their content access elsewhere — so the full <see cref="RosterNote"/> on every
    /// roster would be noise.
    /// </summary>
    public const string NoStartNodeRosterNote =
        "These user groups have no start node, so they only add their permissions for members who reach this " +
        "node another way. That is normal; mention it only if the question is about one of them.";
}
