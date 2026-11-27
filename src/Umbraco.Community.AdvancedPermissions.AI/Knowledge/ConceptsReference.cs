using System.Text;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Knowledge;

/// <summary>
/// The conceptual reference for how this package's permissions work: the storage model, the resolution
/// order, what each scope reaches, what Priority Override actually is, how Insert Options differ, start
/// nodes, and the backoffice navigation for changing a permission by hand. It is the content of the
/// "Permission concepts" item of <see cref="AdvancedPermissionsKnowledgeSet"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the definitional half of the copilot's grounding, kept out of the always-on system prompt so
/// its cost is paid only when the model fetches it. It used to be the <c>uap_explain_concepts</c> tool;
/// it became knowledge because the answer never depends on live data.
/// </para>
/// <para>
/// The content is sourced from the base package's own <c>help-docs/en/concepts.md</c>, so the copilot
/// explains the model exactly as the in-product help does; it is never invented here. The permanent
/// grounding keeps a pointer at this item, because the model still decides when to fetch it, and the
/// failure being fixed was the model answering <i>confidently and wrongly</i> from its own assumptions.
/// </para>
/// </remarks>
internal static class ConceptsReference
{
    /// <summary>The reference, as structured sections. Static: the same every time, with no I/O.</summary>
    public static PermissionConcepts Content { get; } = new(
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
            "the list by Umbraco's own configuration) or because a Deny entry blocks it. Creating under a node " +
            "also needs access to that node through start nodes: a parent outside the subject's start nodes " +
            "blocks every type, whatever the entries say. In the Library, element types are only creatable for " +
            "someone with a Library start node.",
        StartNodes:
            "Start nodes decide which part of the content tree a user can reach at all, and Umbraco checks them " +
            "before any permission — so a node outside them is blocked whatever the entries say. No permission " +
            "entry can change that; start nodes are set in the Users section, on the user group or on the user " +
            "account. A user group has one start node, root access, or none, and a group without a start node " +
            "gives no content access. A user's groups combine: they can reach every group's start node, and a " +
            "higher start node covers a lower one on the same branch. Start nodes and permissions are separate, " +
            "so a permission from one group also applies inside a start node that another group provides. A " +
            "start node set on the user account replaces any group start node above or below it on the same " +
            "branch, even root access; on a branch that no group start node covers, it adds access. " +
            "Administrators reach everything only because their group has root access — there is no other " +
            "exception. Deleted or trashed start nodes are ignored; if a user's own start node is deleted, they " +
            "fall back to the group start nodes. The Library has its own start nodes (a Library folder, the whole " +
            "Library, or none), which combine exactly the same way and are checked before any Library " +
            "permission. After an upgrade to Umbraco 18, only the Administrators group has access to the whole " +
            "Library: every other existing user group has no Library start node, so its users can't reach the " +
            "Library until the group is given one. The Access Viewers show a node outside the start nodes as a " +
            "blocked cell — 'Browse only (start nodes)' for an ancestor of a start node, which Umbraco shows so " +
            "users can browse down to it, and 'Not visible (start nodes)' for everything else. Some pickers can " +
            "be configured to ignore start nodes, and backoffice search applies them slightly differently; the " +
            "viewers show the tree and editor rules.",
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
            "For which of the eight screens answers a particular question, and how they differ, read the " +
            "'Permission editors and viewers' reference.");

    /// <summary>
    /// Renders <see cref="Content"/> as the markdown the model reads: a title, then one heading per
    /// section, so it can quote the part that answers the question rather than reciting the whole model.
    /// </summary>
    /// <returns>The markdown body of the "Permission concepts" knowledge item.</returns>
    public static string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(AdvancedPermissionsKnowledgeSet.ConceptsItemName).Append('\n');
        AppendSection(sb, "How access is stored", Content.Model);
        AppendSection(sb, "What an entry is made of", Content.EntryAnatomy);
        AppendSection(sb, "Precedence", Content.Precedence);
        AppendSection(sb, "Scopes", Content.Scopes);
        AppendSection(sb, "Priority Override", Content.PriorityOverride);
        AppendSection(sb, "Insert Options", Content.InsertOptions);
        AppendSection(sb, "Start nodes", Content.StartNodes);
        AppendSection(sb, "How to change a permission", Content.HowToChange);
        return sb.ToString();
    }

    /// <summary>Appends one <c>##</c> section.</summary>
    /// <param name="sb">The markdown being built.</param>
    /// <param name="heading">The section heading.</param>
    /// <param name="text">The section text.</param>
    private static void AppendSection(StringBuilder sb, string heading, string text) =>
        sb.Append("\n## ").Append(heading).Append("\n\n").Append(text).Append('\n');
}
