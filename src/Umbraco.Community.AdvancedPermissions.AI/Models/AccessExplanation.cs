namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// Why a single verdict was reached. Every field is a friendly, editor-facing label —
/// there are no raw role aliases, verb identifiers, enum names, or node GUIDs.
/// </summary>
/// <param name="UserGroup">The friendly user group name that contributed this decision (e.g. "Editors", "All Users").</param>
/// <param name="Decision">The friendly decision contributed by this role ("Allowed" or "Denied").</param>
/// <param name="Scope">
/// The friendly scope of the contributing entry — the Permissions Editor's label with its plain-English
/// meaning attached, e.g. "This node only (the node itself, not its children)".
/// </param>
/// <param name="SetOn">The friendly name of the node the contributing entry was set on.</param>
/// <param name="Inherited">
/// <see langword="true"/> when this decision was inherited from an ancestor or a group default;
/// <see langword="false"/> when it was set directly on the evaluated node.
/// </param>
/// <param name="PriorityOverride">
/// <see langword="true"/> when the contributing entry used the priority-override ("!important") path.
/// </param>
public sealed record AccessReason(
    string UserGroup,
    string Decision,
    string Scope,
    string SetOn,
    bool Inherited,
    bool PriorityOverride);

/// <summary>
/// One permission's friendly verdict at a content node: whether it is allowed or denied, and why.
/// </summary>
/// <param name="Permission">The friendly permission name (e.g. "Delete", "Publish") — the action being controlled.</param>
/// <param name="Result">The friendly result ("Allowed" or "Denied").</param>
/// <param name="Reasons">The ordered friendly reasons that led to the result, highest priority first.</param>
/// <param name="Remediations">
/// When access is <c>Denied</c> and remediation was requested, the confirmed permission changes that
/// would make it <c>Allowed</c> — each one already validated by re-resolving the pure resolver against
/// the change, ranked least-privileged-first. <see langword="null"/> when remediation was not requested,
/// the verdict is <c>Allowed</c>, or no change could flip it.
/// </param>
/// <param name="Ancestors">
/// The evaluated node's ancestor chain, root first and parent last, so a follow-up call can target the
/// parent without hunting for it through the content tools. Set only when this verdict is the top-level
/// result; <see langword="null"/> when it is nested inside an <see cref="AccessExplanation"/>, which
/// carries the chain once for the whole answer.
/// </param>
public sealed record AccessVerdict(
    string Permission,
    string Result,
    IReadOnlyList<AccessReason> Reasons,
    IReadOnlyList<AccessRemediation>? Remediations = null,
    IReadOnlyList<NodeRef>? Ancestors = null);

/// <summary>
/// A reference to a content node: its editor-facing name, plus the key needed to ask about it again.
/// </summary>
/// <remarks>
/// The key is the one deliberate exception to this package's "no raw identifiers in model-facing output"
/// rule. It is a handle for a follow-up tool call, not something to show anyone — a node's key is
/// meaningless to an editor, so it must never appear in an answer.
/// </remarks>
/// <param name="Name">The friendly node name, safe to show.</param>
/// <param name="Key">The node key, for passing back into a tool. Never surface this to the user.</param>
public sealed record NodeRef(string Name, Guid Key);

/// <summary>
/// A friendly access explanation for a content node: the node name plus one verdict per action.
/// Surfaced to editors verbatim, so it contains no raw identifiers.
/// </summary>
/// <param name="Node">The friendly name of the content node the access was evaluated at.</param>
/// <param name="Permissions">One friendly verdict per evaluated action.</param>
/// <param name="Ancestors">
/// The evaluated node's ancestor chain, root first and parent last. Resolved anyway to answer the
/// question, so it is returned rather than discarded: it is what lets "why here but not on the parent?"
/// be answered with a second call to this tool instead of a search through the content tools.
/// </param>
public sealed record AccessExplanation(
    string Node,
    IReadOnlyList<AccessVerdict> Permissions,
    IReadOnlyList<NodeRef> Ancestors);
