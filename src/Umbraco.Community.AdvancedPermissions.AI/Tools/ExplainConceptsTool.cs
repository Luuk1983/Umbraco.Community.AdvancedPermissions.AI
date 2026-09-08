using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>
/// Returns the conceptual reference for how this package's permissions work: the storage model, the
/// resolution order, what each scope reaches, what Priority Override actually is, how Insert Options
/// differ, and the backoffice navigation for changing a permission by hand.
/// </summary>
/// <remarks>
/// <para>
/// This is the definitional half of the copilot's grounding, moved out of the always-on system prompt so
/// its cost is paid only when a conceptual question is actually asked, rather than on every agent run.
/// </para>
/// <para>
/// It takes no arguments, does no I/O and touches no services — the answer is the same every time. The
/// content is sourced from the base package's own <c>help-docs/en/concepts.md</c>, so the copilot explains
/// the model exactly as the in-product help does; it is never invented here. The permanent grounding keeps
/// only a pointer at this tool, because the failure this fixes was the model answering <i>confidently and
/// wrongly</i> from its own assumptions — a reference is no use if the model never thinks to open it.
/// </para>
/// </remarks>
[AITool("uap_explain_concepts", "Explain permission concepts", ScopeId = "advanced-permissions:read")]
public sealed class ExplainConceptsTool : AIToolBase<ExplainConceptsArgs>
{
    /// <summary>The conceptual reference. Static: the same answer every time, with no I/O.</summary>
    private static readonly PermissionConcepts Concepts = new(
        Model:
            "The package governs TWO independent trees with the same machinery: the content tree, and the " +
            "Library (reusable items and folders that live outside the content tree). Everything below — " +
            "Allow/Deny, scopes, inheritance, the All Users baseline, Priority Override and the precedence " +
            "order — applies identically to both, but they are stored and resolved SEPARATELY, so a " +
            "permission in one says nothing about the other. " +
            "Access is governed by explicit Allow/Deny entries per user group (including the special 'All " +
            "Users' group, a baseline reaching every backoffice user) on content nodes, with tree " +
            "inheritance. Leaving a permission unset does not store a Deny — there is no separate 'inherit' " +
            "value; the entry simply does not exist and the node inherits whatever applies from its nearest " +
            "ancestor, or from the Default permissions row. It ends up denied only when nothing anywhere up " +
            "the tree allows it, so an unset permission is not a Deny entry that someone could go and " +
            "remove. Each permission is independent: one can be overridden on a node while the rest keep " +
            "inheriting.",
        EntryAnatomy:
            "A single entry is the unit an administrator creates in the Permissions Editor, and it is made " +
            "of four things: the user group it applies to (for example Editors, or the All Users group); the " +
            "permission it controls (for example the Delete permission, or Publish); its state, which is " +
            "either Allow or Deny; and its scope, which decides how far down the tree it reaches. An entry " +
            "may additionally carry the Priority Override flag. So 'a Deny entry on the Delete permission " +
            "for All Users, scoped to this node only' is one complete entry, and reads as: nobody may delete " +
            "this particular node.",
        Precedence:
            "Every applicable entry from every group the user belongs to is gathered, each entry's scope is " +
            "respected, and this order applies, highest first: (1) an explicit Deny overrides everything " +
            "else; (2) an explicit Allow overrides any inherited permission; (3) an inherited Deny overrides " +
            "an inherited Allow; (4) an inherited Allow. A plain Allow therefore cannot override a Deny on " +
            "the same node — only Priority Override can.",
        Scopes:
            "Every entry has a scope, and what it reaches matters more than its name. 'This node only' means " +
            "the node itself, not its children. 'This node and descendants' means the node and everything " +
            "beneath it. 'Descendants only' means the children but not the node itself.",
        PriorityOverride:
            "A flag on a single entry, ticked deliberately in the Permissions Editor. It exists because a " +
            "user can belong to several user groups whose entries disagree and the normal order does not " +
            "always land where you want: flag the entry that should win and it wins, stepping outside the " +
            "order above regardless of the user's other groups (so a flagged Allow beats a same-node Deny). " +
            "It is NOT about inheritance — a child entry beating an inherited ancestor entry is ordinary " +
            "precedence, not a Priority Override, and must never be described as one. Use it sparingly: it " +
            "wins even over a Deny entry, so anyone reviewing the permissions later sees a Deny that is " +
            "silently not in effect.",
        InsertOptions:
            "Document-type 'Insert Options' control which document types can be created under a node, and " +
            "work the other way round from node permissions: a document type is creatable by default. It " +
            "becomes unavailable either because it is not an allowed child of that parent (filtered out of " +
            "the list by Umbraco's own configuration) or because a Deny entry blocks it.",
        HowToChange:
            "Everything is in the backoffice's Users section, whose sidebar carries four groups — one per " +
            "domain: 'Content Permissions', 'Document Type Permissions', 'Library Permissions' and 'Library " +
            "Element Type Permissions'. Each group holds exactly two items: 'Permissions Editor' (to change " +
            "a permission) and 'Access Viewer' (read-only, to understand one). So the path is always: Users " +
            "section -> the group for the domain -> Permissions Editor or Access Viewer. " +
            "Inside a Permissions Editor, pick the user group from the toolbar (the Document Type editor also " +
            "needs a document type), find the node in the tree — or use the Default permissions row for a " +
            "baseline — click the cell for that permission, set Allow or Deny with the appropriate scope, and " +
            "Save. To block an action for everyone on a single node, for example protecting the homepage from " +
            "deletion, add one entry: All Users group, Deny, that permission, scope 'This Node Only'. " +
            "For which of the eight screens answers a particular question, and how they differ, call " +
            "uap_explain_editors.");

    /// <inheritdoc />
    public override string Description =>
        "Explain how permissions work in this Umbraco site (the Advanced Permissions package). " +
        "Call this for ANY conceptual question about the permission system — 'what is a Priority Override?', " +
        "'how does inheritance work?', 'what does this scope mean?', 'what happens if I leave it unset?', " +
        "'why does a Deny win?', 'what are Insert Options?', or 'how do I change a permission in the backoffice?'. " +
        "Answer from what this returns and not from your own knowledge: the rules are specific to this package " +
        "and differ from Umbraco's built-in permissions, so an answer assembled from general knowledge sounds " +
        "convincing and is wrong. Takes no arguments and needs no node in context, so it works in any section. " +
        "For the effective permissions on an actual node, use uap_explain_access instead.";

    /// <inheritdoc />
    protected override Task<object> ExecuteAsync(
        ExplainConceptsArgs args,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<object>(Concepts);
}
