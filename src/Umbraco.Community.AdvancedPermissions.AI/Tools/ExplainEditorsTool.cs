using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>
/// Returns the reference for the package's eight editors and viewers: how to choose between them, the
/// three distinctions that separate them, what each one does and does not do, and where to find it.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <see cref="ExplainConceptsTool"/>, split along the same line the base package's own
/// help docs use: <c>concepts.md</c> describes the permission <i>model</i> and is byte-identical between
/// the v17 and v18 lines, while the per-surface docs went from five files to nine. The model did not
/// change; the set of editors did. Keeping them in separate tools means a conceptual question does not
/// pay for a tour of the backoffice.
/// </para>
/// <para>
/// The content is ordered boundaries-first. Eight surfaces whose names all combine "permissions",
/// "access", "editor" and "viewer" are individually easy to describe and collectively easy to confuse,
/// and the failure worth preventing is answering about the wrong one — so the disambiguation, the three
/// contrast pairs, and a per-surface "not this, use that instead" carry more weight here than the
/// summaries do.
/// </para>
/// <para>
/// Every string is sourced from the base package's own <c>help-docs/en/*.md</c> and
/// <c>localization/en.ts</c> (verified against the released <c>v18.1.0</c> tag), never invented — the
/// same rule the grounding and the scope strings follow. Like the concepts tool it takes no arguments and
/// does no I/O, so the answer is the same every time and its cost is paid only when asked.
/// </para>
/// </remarks>
[AITool("uap_explain_editors", "Explain the permission editors", ScopeId = "advanced-permissions:read")]
public sealed class ExplainEditorsTool : AIToolBase<ExplainEditorsArgs>
{
    /// <summary>The reference. Static: the same answer every time, with no I/O.</summary>
    private static readonly EditorGuide Guide = new(
        HowToChoose:
            "Three questions pick the right screen, and answering them in order is more reliable than " +
            "matching a name — all eight are called some combination of 'permissions', 'access', 'editor' " +
            "and 'viewer'. (1) WHICH TREE? Content (the site's pages) or the Library (reusable items and " +
            "folders that live outside the content tree). (2) WHICH MECHANISM? Permissions on nodes " +
            "themselves (read, update, delete, publish, move…), or the create filter that decides which " +
            "types may be added. (3) CHANGE IT OR JUST SEE IT? An Editor is where a permission is set; an " +
            "Access Viewer is read-only and explains what someone effectively gets. Two trees × two " +
            "mechanisms × two modes = the eight surfaces.",

        EditorVersusViewer:
            "This is the distinction that gets confused most often, and the two answer genuinely different " +
            "questions. An EDITOR shows what ONE user group has stored, node by node — it is the " +
            "configuration surface, and a blank cell there means no entry is stored for that group, not " +
            "that access is denied. An ACCESS VIEWER shows what a chosen subject — a user OR a user group " +
            "— EFFECTIVELY gets: fully resolved, combined across every group the user belongs to " +
            "(including the All Users group), with the reasoning chain behind each result, and it shows " +
            "what the outcome would have been without a Priority Override when one changed it. A viewer is " +
            "strictly read-only: it explains, it never changes anything. So: 'what " +
            "has been configured for Editors?' is an editor question; 'why can't this particular person " +
            "publish?' is a viewer question. Pick a viewer whenever the question is about a person, or " +
            "about why an outcome happened.",

        PermissionsVersusCreateFilters:
            "The two mechanisms default in OPPOSITE directions, so the same Allow entry means different " +
            "things in each. Node permissions (the Content and Library Permissions Editors) are DENIED " +
            "unless something allows them: nothing configured anywhere up the tree means no access. The " +
            "create filters (the Document Type and Library Element Type Permissions editors) are ALLOWED " +
            "by default and only ever narrow — they are a filter, not a grant. So in a create filter you " +
            "add a Deny to take a type away, and an Allow there only KEEPS a type that is already offered; " +
            "it can never make a type appear that Umbraco does not already permit. A type with no entries " +
            "anywhere therefore reads as allowed in a create filter, and as denied in a node permission.",

        PerNodeVersusSectionWide:
            "Seven of the eight are per-node: you pick a node in a tree and the entry applies there, with " +
            "a scope deciding how far down it reaches. The exception is Library Element Type Permissions " +
            "(and its viewer), which is SECTION-WIDE — one decision for the whole Library, with no tree at " +
            "all. The reason is structural, not a design choice: Umbraco does not supply a parent node when " +
            "you create an item in the Library, so there is nothing to key the entry to. If someone asks " +
            "to restrict a Library element type 'only under a particular folder', that cannot be expressed.",

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
                    "their groups are combined. For that, use the Content Access Viewer. It also does not " +
                    "control which document types can be created; that is the Document Type Permissions " +
                    "Editor.",
                Navigation: "Users section → Content Permissions → Permissions Editor"),

            new EditorSurface(
                Name: "Content Access Viewer",
                Purpose:
                    "Show the effective, fully resolved permission for any user or user group at any " +
                    "content node, and explain exactly how each result was reached.",
                UseWhen:
                    "Someone cannot do something and you need to know why: 'why is this page read-only " +
                    "for me?', 'who can publish here?'. Click a cell for the reasoning chain — which " +
                    "group contributed, from which node, explicitly or inherited.",
                NotThis:
                    "Read-only: nothing can be changed here. To make the change, use the Content " +
                    "Permissions Editor. For document-type creation, use the Document Type Access Viewer.",
                Navigation: "Users section → Content Permissions → Access Viewer"),

            new EditorSurface(
                Name: "Document Type Permissions Editor",
                Purpose:
                    "Decide, per user group, which document types may be created under which content " +
                    "nodes — going beyond Umbraco's allowed-child-types, which is the same for everyone.",
                UseWhen:
                    "One group should be able to add a type where another should not: 'only Publishers may " +
                    "add a News Article under News'.",
                NotThis:
                    "A filter, not a grant: it only narrows what Umbraco already offers, so an Allow here " +
                    "cannot make a type creatable where Umbraco does not permit it as a child. It also has " +
                    "nothing to do with editing or publishing existing nodes — that is the Content " +
                    "Permissions Editor — and it does not cover the Library, which is the Library Element " +
                    "Type Permissions Editor.",
                Navigation: "Users section → Document Type Permissions → Permissions Editor"),

            new EditorSurface(
                Name: "Document Type Access Viewer",
                Purpose:
                    "Show, node by node, whether a user or user group can create a given document type " +
                    "there — fully resolved across every group — and why.",
                UseWhen:
                    "'Why can this editor add a News Article here but not there?', or 'what types can I " +
                    "create at this node?'.",
                NotThis:
                    "Read-only — to change the outcome use the Document Type Permissions Editor. It covers " +
                    "the content tree only; for element types in the Library use the Library Element Type " +
                    "Access Viewer. Also, a dimmed or N/A cell means the type is not an allowed child of " +
                    "that node in Umbraco's own configuration, so it would not be offered there regardless " +
                    "of permissions — that is not a Deny entry, and looking for one is a dead end. " +
                    "Sometimes still called the Insert Options Viewer.",
                Navigation: "Users section → Document Type Permissions → Access Viewer"),

            new EditorSurface(
                Name: "Library Permissions Editor",
                Purpose:
                    "Manage one user group's Allow/Deny entries across the Library tree — Read, Create, " +
                    "Update, Delete, Publish, Unpublish, Duplicate, Move and Rollback on each item and " +
                    "folder.",
                UseWhen:
                    "You want to set or review what a user group may do to reusable Library items: 'only " +
                    "Designers may delete anything in Campaign Assets'.",
                NotThis:
                    "The Library is stored and resolved SEPARATELY from content, so this says nothing " +
                    "about the content tree and the Content Permissions Editor says nothing about the " +
                    "Library. It also does not control which element types may be created — that is the " +
                    "Library Element Type Permissions Editor. A hatched N/A cell means the permission does " +
                    "not apply to that kind of node, not that it is denied.",
                Navigation: "Users section → Library Permissions → Permissions Editor"),

            new EditorSurface(
                Name: "Library Access Viewer",
                Purpose:
                    "Show the effective Library permission for any user or user group at each item or " +
                    "folder, combined across all their groups, with the reasoning behind each result.",
                UseWhen:
                    "'Why can't I delete this library item?', or 'who can update things in this folder?'.",
                NotThis:
                    "Read-only, and Library-only — for a content node use the Content Access Viewer. A " +
                    "hatched N/A cell means the permission has no meaning for that kind of node: Create on " +
                    "a single item (an item cannot contain other items), or the item-only actions " +
                    "(Publish, Unpublish, Duplicate, Rollback) on a folder, which apply to the items " +
                    "inside it rather than to the folder itself.",
                Navigation: "Users section → Library Permissions → Access Viewer"),

            new EditorSurface(
                Name: "Library Element Type Permissions Editor",
                Purpose:
                    "Control which element types each user group may create in the Library — a flat list " +
                    "of element types with a single 'Create in Library' column.",
                UseWhen:
                    "An element type should be hidden from, or explicitly kept for, a particular group in " +
                    "the Library.",
                NotThis:
                    "SECTION-WIDE, not per node: there is no tree here, because Umbraco supplies no parent " +
                    "when creating in the Library, so this cannot be restricted to one folder. It is also " +
                    "a filter, not a grant — element types are creatable by default, so you add a Deny to " +
                    "hide one. For permissions on Library items that already exist, use the Library " +
                    "Permissions Editor; for document types in the content tree, the Document Type " +
                    "Permissions Editor. If the list is empty, no document type is yet marked as an " +
                    "element type with 'Allow in Library' enabled.",
                Navigation: "Users section → Library Element Type Permissions → Permissions Editor"),

            new EditorSurface(
                Name: "Library Element Type Access Viewer",
                Purpose:
                    "List every element type with whether a chosen user or user group can create it in " +
                    "the Library, resolved across all their groups, with the reasoning per row.",
                UseWhen:
                    "'Which element types can I add in the Library?', or 'why can't I add this element " +
                    "type?'.",
                NotThis:
                    "Read-only, section-wide (a flat list, no tree), and about CREATING element types " +
                    "only — not about what may be done to existing Library items, which is the Library " +
                    "Access Viewer. Creating is allowed by default, so a type with no entries anywhere " +
                    "shows as allowed. Sometimes still called the Library Insert Viewer.",
                Navigation: "Users section → Library Element Type Permissions → Access Viewer"),
        ],

        Navigation:
            "All eight live in the backoffice's Users section. Its sidebar carries four groups, one per " +
            "domain — 'Content Permissions', 'Document Type Permissions', 'Library Permissions' and " +
            "'Library Element Type Permissions' — and each group holds exactly two items, 'Permissions " +
            "Editor' and 'Access Viewer'. So the path is always: Users section → the group for the domain " +
            "→ Permissions Editor to change it, or Access Viewer to understand it. Inside an editor, pick " +
            "the user group from the toolbar (the Document Type editor also needs a document type), find " +
            "the node in the tree or use the Default permissions row for a baseline, click the cell, set " +
            "the state and scope, and Save.");

    /// <inheritdoc />
    public override string Description =>
        "Explain the Advanced Permissions package's eight editors and viewers — which one to use for a " +
        "given question, how they differ from each other, what each one does and does NOT do, and where to " +
        "find it in the backoffice. " +
        "Call this for ANY question about where to go or which screen to use: 'where do I change this?', " +
        "'which editor shows me why a user can't publish?', 'what's the difference between the Permissions " +
        "Editor and the Access Viewer?', 'where do I manage Library permissions?', 'how do I stop a group " +
        "creating an element type?'. " +
        "Answer from what this returns and not from your own knowledge: there are eight screens whose names " +
        "all combine the words 'permissions', 'access', 'editor' and 'viewer', so a name-based guess sounds " +
        "right and sends the editor to the wrong one. It also carries the exact sidebar navigation, which " +
        "changed in this version — do not describe a menu path from memory. " +
        "Takes no arguments and needs no node in context, so it works in any section. " +
        "For how the permission rules themselves work (Allow/Deny, scopes, precedence, Priority Override), " +
        "use uap_explain_concepts. For the effective permissions on an actual node, use uap_explain_access " +
        "(content) or uap_explain_library_access (Library).";

    /// <inheritdoc />
    protected override Task<object> ExecuteAsync(
        ExplainEditorsArgs args,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<object>(Guide);
}
