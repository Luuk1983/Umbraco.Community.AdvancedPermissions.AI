namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// One element type's friendly "can this be created in the Library" verdict, and why. Every field is an
/// editor-facing label — no raw content-type aliases/GUIDs, user group aliases, or verb identifiers.
/// </summary>
/// <remarks>
/// The Library counterpart to <see cref="TypeCreateVerdict"/>, and deliberately a separate type rather
/// than a reuse, for two reasons that both matter to the copilot's wording:
/// <list type="bullet">
/// <item><description>
/// The noun is different. <see cref="TypeCreateVerdict.DocumentType"/> would have the copilot call an
/// element type a "document type", which is the wrong word for a different editor's subject.
/// </description></item>
/// <item><description>
/// There is no node and no structural applicability. The document filter is evaluated under a parent and
/// can report "Not applicable" when a type is not an allowed child there; the Library filter is
/// <b>section-wide</b> — Umbraco supplies no parent when creating in the Library — so those fields would
/// be permanently meaningless here, and a field that is always null invites the model to invent a reason
/// for it.
/// </description></item>
/// </list>
/// </remarks>
/// <param name="ElementType">The friendly display name of the element type (e.g. "Promo Banner").</param>
/// <param name="Result">
/// The friendly result: "Allowed" (creatable in the Library) or "Denied" (hidden from the Library by a
/// permission entry).
/// </param>
/// <param name="Reasons">
/// The ordered friendly reasons that led to the result, highest priority first. Empty when creation is
/// allowed by default with no narrowing entry anywhere — the common case, since element types are
/// creatable unless denied.
/// </param>
public sealed record ElementTypeCreateVerdict(
    string ElementType,
    string Result,
    IReadOnlyList<AccessReason> Reasons);

/// <summary>
/// A friendly "which element types can be created in the Library / why can't I create element type X"
/// explanation for a single subject. Surfaced to editors verbatim, so it contains no raw identifiers.
/// </summary>
/// <remarks>
/// Carries no node, because this decision is section-wide rather than per-node. That absence is the
/// single most important thing about this surface, and the reason it is not folded into
/// <see cref="TypeCreateExplanation"/>.
/// </remarks>
/// <param name="ElementTypes">One friendly verdict per evaluated element type.</param>
public sealed record ElementTypeCreateExplanation(
    IReadOnlyList<ElementTypeCreateVerdict> ElementTypes);

/// <summary>
/// A friendly "who can create this element type in the Library" roster for a single element type: which
/// user groups are allowed and which are denied. Every field is an editor-facing label.
/// </summary>
/// <param name="ElementType">The friendly display name of the element type.</param>
/// <param name="AllowedUserGroups">The display names of user groups that may create the type in the Library.</param>
/// <param name="DeniedUserGroups">The display names of user groups that may not.</param>
public sealed record ElementTypeCreateRoster(
    string ElementType,
    IReadOnlyList<string> AllowedUserGroups,
    IReadOnlyList<string> DeniedUserGroups);

/// <summary>
/// A friendly roster report covering several element types: one <see cref="ElementTypeCreateRoster"/> per
/// evaluated element type. Used for the all-user-groups roster (who can create each element type in the
/// Library).
/// </summary>
/// <param name="ElementTypes">One allowed/denied roster per evaluated element type.</param>
public sealed record ElementTypeCreateRosterReport(
    IReadOnlyList<ElementTypeCreateRoster> ElementTypes);
