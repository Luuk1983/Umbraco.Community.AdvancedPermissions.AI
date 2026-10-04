using Umbraco.AI.Core;
using Umbraco.AI.Core.RuntimeContext;

namespace Umbraco.Community.AdvancedPermissions.AI.Context;

/// <summary>
/// An <see cref="IAIRuntimeContextContributor"/> that prepends a short grounding line to the copilot's
/// system prompt, in two halves.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ConceptsGrounding"/> goes on <b>every</b> conversation and teaches: (1) how the package's
/// model and precedence work — including that an unset permission <i>inherits</i> rather than storing a
/// Deny, and what Priority Override actually is (a per-entry flag for when a user's groups disagree, not
/// an ancestor-versus-descendant conflict); (2) editor-grounded <b>terminology</b> — a record is an
/// "entry" (not a bare "Allow"/"Deny"), an action is a "permission", and the collections are "user groups"
/// (not "roles"); (3) that the copilot is strictly <b>read-only</b>, so it must never offer to make or
/// arrange a permission change and must hand it off to the user; and (4) the real backoffice navigation
/// (the <b>Users</b> section → <b>Content Permissions</b> → <b>Permissions Editor</b>), so "how do I change
/// this?" is answered with a real menu path rather than an invented one.
/// </para>
/// <para>
/// <see cref="DocumentGrounding"/> is appended only on document conversations, and covers what presumes a
/// focused node and the read tools: that a <c>Deny</c> entry is a likely cause when something looks blocked,
/// that a recommended fix must come from the confirmed <c>suggestFix</c> output rather than a guess, and
/// that the tool's <c>GrantedBy</c>/<c>Caution</c> fields must be relayed.
/// </para>
/// <para>
/// The concepts half is deliberately ungated: "what is a Priority Override?" is a conceptual question an
/// editor can ask from any section, and with no grounding at all the model answers it confidently and
/// wrongly. A document conversation receives concepts <i>plus</i> document guidance, so it is a strict
/// superset and never loses grounding to the split.
/// </para>
/// <para>
/// This contributor follows a strict defensive pattern, because Umbraco AI runs every contributor
/// on every agent run with <b>no</b> try/catch around them — an exception thrown here would abort the
/// whole agent run:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Additive on document context.</b> The built-in <c>SerializedEntityContributor</c> stores the focused
/// entity's type under <see cref="Constants.ContextKeys.EntityType"/> before this contributor runs (this
/// one is appended to the collection), so that value selects which halves are contributed. When it is
/// absent or anything other than a document, only <see cref="ConceptsGrounding"/> is contributed.
/// </description>
/// </item>
/// <item><description><b>Append-only.</b> It only adds to <see cref="AIRuntimeContext.SystemMessageParts"/>; it never writes <see cref="AIRuntimeContext.Variables"/> or <see cref="AIRuntimeContext.Data"/>.</description></item>
/// <item><description><b>Never throws.</b> The whole method body is wrapped in a catch-all so a bug here can never break an agent run.</description></item>
/// <item><description><b>Cheap.</b> It contributes a single static string; no I/O and no service calls (the contract is synchronous).</description></item>
/// </list>
/// </remarks>
internal sealed class AdvancedPermissionsGroundingContributor : IAIRuntimeContextContributor
{
    /// <summary>
    /// The entity-type value the built-in document entity adapter stores in the runtime context's data
    /// bag (<c>DocumentEntityAdapter.EntityType</c>). The document half of the grounding is appended only
    /// when the focused entity matches this value.
    /// </summary>
    private const string DocumentEntityType = "document";

    /// <summary>
    /// The entity-type values Umbraco uses for the Library's two node kinds, taken from the base
    /// package's own condition classes (<c>ELEMENT_ENTITY_TYPE</c> / <c>ELEMENT_FOLDER_ENTITY_TYPE</c>).
    /// The Library half of the grounding is appended when the focused entity matches either.
    /// </summary>
    /// <remarks>
    /// These are read from <see cref="Constants.ContextKeys.EntityType"/> — NOT
    /// <c>ContextKeys.ElementType</c>, which is a different thing entirely: that key carries a
    /// <i>block-editor</i> element type and has nothing to do with the Library. The names are close
    /// enough to invite the mistake, so the distinction is asserted by a test.
    /// </remarks>
    private static readonly string[] LibraryEntityTypes = ["element", "element-folder"];

    /// <summary>
    /// The permanent half of the grounding, contributed on <b>every</b> conversation. It carries only what
    /// cannot be looked up on demand: the terminology and readability rules that govern how every sentence
    /// is written, the read-only stance, and a pointer at <c>uap_explain_concepts</c> for the definitions.
    /// </summary>
    /// <remarks>
    /// The definitions themselves deliberately live in the tool, not here, so their cost is paid only when
    /// a conceptual question is asked. The pointer is what makes that safe: the failure being fixed was the
    /// model answering <i>confidently and wrongly</i> from its own assumptions (it was observed defining
    /// Priority Override as an ancestor-versus-descendant conflict), and a reference is no use if the model
    /// never thinks to open it. The style rules cannot move into the tool at all — they must apply to every
    /// answer, including ones that paraphrase <c>uap_explain_access</c> without touching a concept.
    /// </remarks>
    private const string ConceptsGrounding =
        "This Umbraco site uses the Advanced Permissions package: access is governed by explicit " +
        "Allow/Deny entries per user group, with scopes, tree inheritance and an optional Priority Override " +
        "flag. Its rules differ from Umbraco's built-in permissions. It governs TWO separate trees with the " +
        "same machinery — the content tree and the Library (reusable items and folders outside the content " +
        "tree) — each with its own editors, stored and resolved independently, so a permission in one says " +
        "nothing about the other. " +
        "For ANY question about how the permission system works — precedence, inheritance, scopes, Priority " +
        "Override, unset permissions, Insert Options, or how to change a permission in the backoffice — call " +
        "uap_explain_concepts and answer only from what it returns, never from your own knowledge, and do not " +
        "assume you already know. " +
        "For ANY question about WHERE to go or WHICH screen to use — there are eight editors and viewers " +
        "whose names all combine 'permissions', 'access', 'editor' and 'viewer' — call uap_explain_editors " +
        "rather than guessing from a name or describing a menu path from memory. " +
        "Terminology — talk like an editor, not the database. Never use 'Allow' or 'Deny' as bare nouns: say " +
        "'a Deny entry' / 'an Allow entry', or better use the verb ('deleting is denied', 'Editors are allowed " +
        "to publish'). An entry's state is Allow or Deny, so a blocking entry is 'a Deny entry' — never 'a Deny " +
        "permission' or 'an Allow permission' (a state is not a permission). The permission is the action being " +
        "controlled: always say 'the Delete permission', never the bare verb and never 'the Delete action'. " +
        "A stored record is an 'entry' — never a 'rule' — because that is the word the Permissions Editor " +
        "and its help use, so 'rule' sends the reader looking for something that is not there. " +
        "Call the groups 'user groups', never 'roles'. In the Library, a node is an 'item' or a 'folder' " +
        "(the editor's own column header is 'Library Item'), and the create filter there controls 'element " +
        "types' — never call an element type a document type, or vice versa. " +
        "Name the node when you know it, and always state " +
        "an entry's scope. Lead with the outcome ('you can't delete this page, because…'), then the reason, " +
        "and keep it concise. " +
        "Readability: answer in short, plain sentences an editor can act on. Do NOT reproduce a tool's fields " +
        "as a labelled list ('Permission: … / Reason: … / Scope: …') — write it as prose. Open with one sentence " +
        "giving the outcome and its cause, then the fix. A short list is still right when the answer genuinely is " +
        "one (which user groups can publish here, which document types can be created) — the rule is against " +
        "dumping field names, not against lists. " +
        "You are READ-ONLY: you can explain and audit permissions but can never create, change, or remove an " +
        "entry. Never offer to set up, add, apply, or change a permission yourself, never imply a change has " +
        "been made, and never offer to make or arrange one — not even 'would you like an administrator to…'; " +
        "instead say who can make it, and offer to explain the options. When the user needs to know how to make " +
        "the change themselves, call uap_explain_concepts for the exact backoffice navigation rather than " +
        "describing a menu path from memory. " +
        "Never state a permission result you have not just retrieved from a tool. If a lookup fails or a node " +
        "cannot be resolved, say so and ask for what you need — do not carry a result over from another node, " +
        "and do not infer one. If you reason to a likely cause, label it as unconfirmed and say what you would " +
        "need to confirm it. " +
        "You cannot see why, when, or by whom an entry was created — the package stores the entry, not the " +
        "intent. Say so plainly rather than guessing.";

    /// <summary>
    /// The document half of the grounding, appended to <see cref="ConceptsGrounding"/> only when the focused
    /// entity is a document. Everything here assumes a focused node and the read tools: the "a Deny entry is
    /// a likely cause" nudge, the confirmed-remediation rules, and relaying the tool's GrantedBy/Caution
    /// fields. Kept out of the always-on half so non-document conversations are not told to call tools that
    /// need a document key.
    /// </summary>
    private const string DocumentGrounding =
        " If a user cannot perform an action or the editor appears read-only, a Deny entry is a likely cause — " +
        "use the Advanced Permissions tools (uap_explain_access, uap_audit_permissions) to check before " +
        "concluding the cause is structural. " +
        "When you recommend a change or explain how to fix a denial, first call uap_explain_access with " +
        "suggestFix=true and present ONLY the confirmed changes it returns (each is verified by simulating it " +
        "against the resolver) — never hand-roll or guess a fix, and remember suggestFix describes the entries " +
        "a human must add; it does not apply them. When a suggested change carries a GrantedBy phrase, always " +
        "include it: removing a Deny entry only works because something else already allows the permission, so " +
        "never say a removal 'would then be allowed' without saying what allows it. When a change carries a " +
        "Caution, always relay it and never present that option as an equal-footing alternative — removing the " +
        "Deny entry is the preferred fix, and a Priority Override is a last resort. " +
        "When asked why an entry exists, say you cannot tell and offer to run uap_audit_permissions to check " +
        "whether it is part of a deliberate, site-wide pattern.";

    /// <summary>
    /// The Library half of the grounding, appended to <see cref="ConceptsGrounding"/> only when the focused
    /// entity is a Library item or folder. Mirrors <see cref="DocumentGrounding"/> but points at the
    /// Library tools, and adds the one thing the content tree has no equivalent for: some permissions do
    /// not apply to a given kind of node at all.
    /// </summary>
    private const string LibraryGrounding =
        " You are focused on a LIBRARY item or folder, so use uap_explain_library_access (NOT " +
        "uap_explain_access, which covers the content tree only — the two are stored separately, so an " +
        "answer from the wrong one is about the wrong thing). " +
        "If a user cannot perform an action here, a Deny entry is a likely cause — check with " +
        "uap_explain_library_access before concluding the cause is structural. " +
        "Some permissions are reported 'Not applicable' rather than allowed or denied, because they have no " +
        "meaning for that kind of node: Create on a single item, and Publish/Unpublish/Duplicate/Rollback on " +
        "a folder (those apply to the items inside it). Relay that as not applicable — never as denied, and " +
        "never send someone looking for an entry that causes it, because there is none. " +
        "When you recommend a change or explain how to fix a denial, first call uap_explain_library_access " +
        "with suggestFix=true and present ONLY the confirmed changes it returns; never hand-roll or guess a " +
        "fix, and remember suggestFix describes the entries a human must add — it does not apply them. When a " +
        "suggested change carries a GrantedBy phrase, always include it: removing a Deny entry only works " +
        "because something else already allows the permission. When a change carries a Caution, always relay " +
        "it and never present that option as an equal-footing alternative — removing the Deny entry is the " +
        "preferred fix, and a Priority Override is a last resort. " +
        "For which element types may be created in the Library, use aspect=element-type-create and pass NO " +
        "node: that decision is section-wide, and element types are creatable by default.";

    /// <inheritdoc />
    public void Contribute(AIRuntimeContext context)
    {
        try
        {
            // The concepts half goes on every conversation; the document half is additive. A document
            // conversation is therefore a strict superset, never a different message. The entity-type value
            // is set by the built-in entity contributor before this one runs.
            // EntityType, never ElementType: the latter means a block-editor element type, which has
            // nothing to do with the Library. See LibraryEntityTypes.
            var entityType = context.GetValue<string>(Constants.ContextKeys.EntityType);

            var domainHalf = string.Empty;
            if (string.Equals(entityType, DocumentEntityType, StringComparison.Ordinal))
            {
                domainHalf = DocumentGrounding;
            }
            else if (entityType is not null && LibraryEntityTypes.Contains(entityType, StringComparer.Ordinal))
            {
                domainHalf = LibraryGrounding;
            }

            // Append-only (b): never write Variables/Data. Contributed as ONE part in every case, and
            // every case is a strict superset of the concepts half — a conversation never loses grounding
            // to the split.
            context.SystemMessageParts.Add(ConceptsGrounding + domainHalf);
        }
        catch
        {
            // Never throw (c): Umbraco AI has no try/catch around contributors, so a failure here would
            // abort the entire agent run. Grounding is a best-effort nudge; swallow everything.
        }
    }
}
