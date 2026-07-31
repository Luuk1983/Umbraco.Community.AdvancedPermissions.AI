namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// A friendly "who can do this here" roster for a single permission at a content node: which user groups
/// are allowed and which are denied. Every field is an editor-facing label — there are no raw verb
/// identifiers, role aliases, or node GUIDs, so the answer surfaced to an editor never leaks code
/// identifiers.
/// </summary>
/// <param name="Permission">The friendly permission that was evaluated, e.g. <c>Publish</c> — the action being controlled.</param>
/// <param name="Node">The friendly name of the content node the permission was evaluated at.</param>
/// <param name="AllowedRoles">The display names of user groups whose effective permission is allowed.</param>
/// <param name="DeniedRoles">The display names of user groups whose effective permission is denied.</param>
public sealed record AccessRoster(
    string Permission,
    string Node,
    IReadOnlyList<string> AllowedRoles,
    IReadOnlyList<string> DeniedRoles);

/// <summary>
/// A friendly roster report covering several permissions at a content node: one <see cref="AccessRoster"/>
/// per evaluated permission. Used when the all-roles explanation is asked for every permission at once.
/// </summary>
/// <param name="Node">The friendly name of the content node the permissions were evaluated at.</param>
/// <param name="Permissions">One allowed/denied roster per evaluated permission.</param>
public sealed record AccessRosterReport(
    string Node,
    IReadOnlyList<AccessRoster> Permissions);
