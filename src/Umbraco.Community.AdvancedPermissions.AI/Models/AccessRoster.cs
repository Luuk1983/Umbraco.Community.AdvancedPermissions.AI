namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// A friendly "who can do this here" roster for a single permission at a content node: which user groups
/// are allowed and which are denied. Every field is an editor-facing label — there are no raw verb
/// identifiers, user group aliases, or node GUIDs, so the answer surfaced to an editor never leaks code
/// identifiers.
/// </summary>
/// <param name="Permission">The friendly permission that was evaluated, e.g. <c>Publish</c> — the action being controlled.</param>
/// <param name="Node">The friendly name of the content node the permission was evaluated at.</param>
/// <param name="AllowedUserGroups">The display names of user groups whose effective permission is allowed.</param>
/// <param name="DeniedUserGroups">The display names of user groups whose effective permission is denied.</param>
/// <param name="BlockedUserGroups">
/// The display names of user groups whose OWN start node does not reach the node, so on its own the group
/// gives no access here. They still appear in the allowed or denied list with their permission result,
/// because users combine the start nodes of all their groups and a member may reach the node through
/// another group or their own start node. <see langword="null"/> when no group is flagged.
/// </param>
/// <param name="StartNodeNote">
/// Set whenever <c>BlockedUserGroups</c> is: the caveat that users combine the start nodes of all their
/// groups, so a flagged group's permission still applies to members who reach the node another way.
/// </param>
public sealed record AccessRoster(
    string Permission,
    string Node,
    IReadOnlyList<string> AllowedUserGroups,
    IReadOnlyList<string> DeniedUserGroups,
    IReadOnlyList<string>? BlockedUserGroups = null,
    string? StartNodeNote = null);

/// <summary>
/// A friendly roster report covering several permissions at a content node: one <see cref="AccessRoster"/>
/// per evaluated permission. Used when the all-user-groups explanation is asked for every permission at once.
/// </summary>
/// <param name="Node">The friendly name of the content node the permissions were evaluated at.</param>
/// <param name="Permissions">One allowed/denied roster per evaluated permission.</param>
public sealed record AccessRosterReport(
    string Node,
    IReadOnlyList<AccessRoster> Permissions);
