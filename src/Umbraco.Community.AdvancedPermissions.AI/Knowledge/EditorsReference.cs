using System.Text;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Knowledge;

/// <summary>
/// The reference for the package's four editors and viewers: how to choose between them, the two
/// distinctions that separate them, what each one does and does not do, and where to find it. It is the
/// content of the "Permission editors and viewers" item of <see cref="AdvancedPermissionsKnowledgeSet"/>.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <see cref="ConceptsReference"/>, split along the line the base package's own help docs
/// use: <c>concepts.md</c> describes the permission <i>model</i>, the per-surface docs describe the
/// screens. Keeping them in separate items means a conceptual question does not pay for a tour of the
/// backoffice. It used to be the <c>uap_explain_editors</c> tool; it became knowledge because the answer
/// never depends on live data.
/// </para>
/// <para>
/// The content is ordered boundaries-first. The failure worth preventing is answering about the wrong
/// screen, or naming a menu path that does not exist — the first v17 release sent editors to a
/// "Content Permissions" sidebar group that v17 never had. So the disambiguation, the two contrast pairs,
/// and a per-surface "not this, use that instead" carry more weight than the summaries do.
/// </para>
/// <para>
/// Every string is sourced from the base package's own <c>help-docs/en/*.md</c>, <c>localization/en.ts</c>
/// and <c>manifests.ts</c> (verified against <c>origin/v17/main</c> at the released <c>v17.5.0</c> tag),
/// never invented.
/// </para>
/// </remarks>
internal static class EditorsReference
{
    /// <summary>The reference, as structured sections. Static: the same every time, with no I/O.</summary>
    public static EditorGuide Content { get; } = new(
        HowToChoose:
            "Two questions pick the right screen, and answering them in order is more reliable than " +
            "matching a name — all four are called some combination of 'permissions', 'editor' and " +
            "'viewer'. (1) WHICH MECHANISM? Permissions on content nodes themselves (read, update, delete, " +
            "publish, move…), or the Insert Options that decide which document types may be created where. " +
            "(2) CHANGE IT OR JUST SEE IT? An editor is where a permission is set; a viewer is read-only and " +
            "explains what someone effectively gets. Two mechanisms × two modes = the four screens.",

        EditorVersusViewer:
            "This is the distinction that gets confused most often, and the two answer genuinely different " +
            "questions. An EDITOR shows what ONE user group has stored, node by node — it is the " +
            "configuration surface, and a blank cell there means no entry is stored for that group, not " +
            "that access is denied. A VIEWER shows what a chosen subject — a user OR a user group — " +
            "EFFECTIVELY gets: fully resolved, combined across every group the user belongs to (including " +
            "the All Users group), with the reasoning chain behind each result, and it shows what the " +
            "outcome would have been without a Priority Override when one changed it. A viewer also applies " +
            "start nodes, so a cell can be 'Blocked (start nodes)' — neither an Allow nor a Deny, and not " +
            "something any editor can change, because start nodes are set on the user group or user account " +
            "in the Users section. A viewer is strictly read-only: it explains, it never changes anything. " +
            "So: 'what has been configured for Editors?' is an editor question; 'why can't this particular " +
            "person publish?' is a viewer question. Pick a viewer whenever the question is about a person, " +
            "or about why an outcome happened.",

        PermissionsVersusCreateFilters:
            "The two mechanisms default in OPPOSITE directions, so the same Allow entry means different " +
            "things in each. Content permissions (the Content Permissions Editor) are DENIED unless " +
            "something allows them: nothing configured anywhere up the tree means no access. Insert Options " +
            "(the Document Type Permissions Editor) are ALLOWED by default and only ever narrow — they are a " +
            "filter, not a grant. So in Insert Options an Allow only KEEPS a type that Umbraco already " +
            "offers there; it can never make a type appear that Umbraco does not already permit as a child. " +
            "A Deny takes it away. A type with no entries anywhere therefore reads as allowed in Insert " +
            "Options, and a permission with no entries anywhere reads as denied.",

        Surfaces:
        [
            new EditorSurface(
                Name: "Content Permissions Editor",
                Purpose:
                    "Manage all of one user group's permission entries across the content tree in one " +
                    "place — what that group may do on each node, and how far each entry reaches.",
                UseWhen:
                    "You want to set or review what a user group may do on content nodes: 'stop anyone " +
                    "deleting the homepage', 'let Editors publish under News but not Press Releases'.",
                NotThis:
                    "It shows what ONE group has stored — not what a person effectively gets once all " +
                    "their groups are combined. For that, use the Access Viewer. It also does not control " +
                    "which document types can be created; that is the Document Type Permissions Editor.",
                Navigation: "Users section → Editors → Content Permissions Editor"),

            new EditorSurface(
                Name: "Access Viewer",
                Purpose:
                    "Show the effective, fully resolved permission for any user or user group at any " +
                    "content node, and explain exactly how each result was reached.",
                UseWhen:
                    "Someone cannot do something and you need to know why: 'why is this page read-only " +
                    "for me?', 'who can publish here?'. Click a cell for the reasoning chain — which group " +
                    "contributed, from which node, explicitly or inherited.",
                NotThis:
                    "Read-only: nothing can be changed here. To make the change, use the Content " +
                    "Permissions Editor. For document-type creation, use the Insert Options Viewer. " +
                    "The viewer also applies the subject's start nodes, which Umbraco checks before any " +
                    "permission: a node outside them shows a blocked cell — 'Browse only (start nodes)' " +
                    "or 'Not visible (start nodes)' — instead of allow or deny, the start-node card under " +
                    "the picker lists the start nodes in effect and who set them, and clicking a blocked " +
                    "cell shows what the permissions would be inside a start node. With a user group " +
                    "selected it uses that group's own start node; users combine the start nodes of all " +
                    "their groups, so pick a user for the real result.",
                Navigation: "Users section → Viewers → Access Viewer"),

            new EditorSurface(
                Name: "Document Type Permissions Editor",
                Purpose:
                    "Decide, per user group, which document types may be created under which content " +
                    "nodes — going beyond Umbraco's allowed child types, which are the same for everyone.",
                UseWhen:
                    "One group should be able to add a type where another should not: 'only Publishers may " +
                    "add a News Article under News'.",
                NotThis:
                    "A filter, not a grant: it only narrows what Umbraco already offers, so an Allow here " +
                    "cannot make a type creatable where Umbraco does not permit it as a child. It also has " +
                    "nothing to do with editing or publishing existing nodes — that is the Content " +
                    "Permissions Editor — and to see what a person effectively gets, use the Insert Options " +
                    "Viewer.",
                Navigation: "Users section → Editors → Document Type Permissions Editor"),

            new EditorSurface(
                Name: "Insert Options Viewer",
                Purpose:
                    "Show, node by node, whether a user or user group can create a given document type " +
                    "there — fully resolved across every group — and why.",
                UseWhen:
                    "'Why can this editor add a News Article here but not there?', or 'what types can I " +
                    "create at this node?'.",
                NotThis:
                    "Read-only — to change the outcome use the Document Type Permissions Editor; for the " +
                    "other permissions on a node, use the Access Viewer. A dimmed or N/A cell means the " +
                    "type is not an allowed child of that node in Umbraco's own configuration, so it would " +
                    "not be offered there regardless of permissions — that is not a Deny entry, and looking " +
                    "for one is a dead end. Creating under a node needs access to that node, so rows under " +
                    "a node outside the subject's start nodes are blocked — again not a Deny entry; the " +
                    "start-node card under the picker shows which start nodes apply and who set them.",
                Navigation: "Users section → Viewers → Insert Options Viewer"),
        ],

        Navigation:
            "All four are in the backoffice's Users section. Its sidebar has two groups: 'Editors' (Content " +
            "Permissions Editor, Document Type Permissions Editor), where permissions are changed, and " +
            "'Viewers' (Access Viewer, Insert Options Viewer), which are read-only. Only people with access " +
            "to the Users section can open them. Inside an editor, pick the user group from the toolbar (the " +
            "Document Type Permissions Editor also needs a document type), find the node in the tree or use " +
            "the Default permissions row for a baseline, click the cell, set the state and scope, and Save.");

    /// <summary>
    /// Renders <see cref="Content"/> as the markdown the model reads: the boundaries first, then one
    /// sub-heading per screen, then the shared navigation, and a closing hand-off to the concepts item for
    /// questions about the rules themselves (the hand-off the removed tool's description used to carry).
    /// </summary>
    /// <returns>The markdown body of the "Permission editors and viewers" knowledge item.</returns>
    public static string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(AdvancedPermissionsKnowledgeSet.EditorsItemName).Append('\n');
        AppendSection(sb, "How to choose", Content.HowToChoose);
        AppendSection(sb, "Editor versus viewer", Content.EditorVersusViewer);
        AppendSection(sb, "Permissions versus Insert Options", Content.PermissionsVersusCreateFilters);

        sb.Append("\n## The four screens\n");
        foreach (var surface in Content.Surfaces)
        {
            sb.Append("\n### ").Append(surface.Name).Append("\n\n")
                .Append(surface.Purpose).Append("\n\n")
                .Append("Use it when: ").Append(surface.UseWhen).Append("\n\n")
                .Append("Not this: ").Append(surface.NotThis).Append("\n\n")
                .Append("Where: ").Append(surface.Navigation).Append('\n');
        }

        AppendSection(sb, "Navigation", Content.Navigation);
        sb.Append("\nFor how the permission rules themselves work (Allow and Deny entries, scopes, precedence, ")
            .Append("Priority Override, start nodes), read the '").Append(AdvancedPermissionsKnowledgeSet.ConceptsItemName)
            .Append("' reference.\n");
        return sb.ToString();
    }

    /// <summary>Appends one <c>##</c> section.</summary>
    /// <param name="sb">The markdown being built.</param>
    /// <param name="heading">The section heading.</param>
    /// <param name="text">The section text.</param>
    private static void AppendSection(StringBuilder sb, string heading, string text) =>
        sb.Append("\n## ").Append(heading).Append("\n\n").Append(text).Append('\n');
}
