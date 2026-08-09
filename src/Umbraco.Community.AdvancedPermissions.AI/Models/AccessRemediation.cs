namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// A single, friendly, editor-facing remediation: one concrete permission change that — confirmed by
/// re-resolving the package's pure resolver against the change — would flip a denied action to allowed.
/// Every field is a friendly label; there are no raw role aliases, verb identifiers, scope/state enum
/// names, or node GUIDs. The wording is phrased as an administrator action ("An administrator could …")
/// because applying the change requires permission-management rights the asker may not have, and because
/// this companion never writes anything itself.
/// </summary>
/// <param name="Description">
/// A plain-language sentence describing the change and that it would result in the action being allowed,
/// e.g. "An administrator could remove the Deny entry on the Delete permission for the Editors user group
/// on News — after which the Delete permission would be allowed."
/// </param>
/// <param name="Action">The friendly change verb: "Remove", "Add", or "Override".</param>
/// <param name="Role">The friendly role the change targets (e.g. "Editors", "All Users").</param>
/// <param name="Permission">The friendly action the change is about (e.g. "Delete", "Publish").</param>
/// <param name="Scope">
/// The friendly scope of the entry to add — the Permissions Editor's label with its plain-English meaning
/// attached, e.g. "This node only (the node itself, not its children)" — or <see langword="null"/> for a
/// removal, which carries no new scope of its own.
/// </param>
/// <param name="SetOn">The friendly name of the node the change is made on.</param>
/// <param name="GrantedBy">
/// For a removal, a plain-language phrase naming what allows the permission once the Deny entries are
/// gone (e.g. "the Administrators user group has an Allow entry … inherited from Home"). Removing a Deny
/// only works because something else already grants the permission — an unset permission inherits rather
/// than denying, and only ends up denied when nothing up the tree allows it — so this is the answer to
/// "…and why would it be allowed then?". <see langword="null"/> for additions, where the entry being added
/// is itself the grant.
/// </param>
/// <param name="Caution">
/// A warning the copilot must relay whenever it presents this option, or <see langword="null"/> when the
/// change carries no special risk. Set for Priority Override, which wins even over a Deny entry and so
/// makes the effective permissions materially harder to review later.
/// </param>
public sealed record AccessRemediation(
    string Description,
    string Action,
    string Role,
    string Permission,
    string? Scope,
    string SetOn,
    string? GrantedBy = null,
    string? Caution = null);
