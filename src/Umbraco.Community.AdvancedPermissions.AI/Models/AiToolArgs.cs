using System.ComponentModel;

namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// Identifies whose effective access the <c>uap_explain_access</c> tool should evaluate at a content node.
/// </summary>
public enum ExplainSubject
{
    /// <summary>The backoffice user currently asking the question (resolved from the authenticated session).</summary>
    CurrentUser,

    /// <summary>A specific user identified by their key.</summary>
    User,

    /// <summary>A single user group, or the special "All Users" group.</summary>
    UserGroup,

    /// <summary>Every assignable user group, partitioned into who is allowed and who is denied.</summary>
    AllUserGroups,
}

/// <summary>
/// Selects which dimension of access the <c>uap_explain_access</c> tool explains at a content node:
/// the node's action permissions, or which document types can be created (inserted) under the node.
/// </summary>
public enum ExplainAspect
{
    /// <summary>
    /// Node action permissions — edit, delete, publish, move, etc. — resolved via the node-level
    /// permission system. This is the default and unchanged behaviour.
    /// </summary>
    Node,

    /// <summary>
    /// Document-type creation ("Insert Options") — which document types may be created under the node,
    /// resolved via the separate doc-type permission system. Answers the "why can't I create/insert
    /// type X here?", "what types can I create here?", and "who can create type X here?" questions.
    /// </summary>
    TypeCreate,
}

/// <summary>
/// Controls how much detail the <c>uap_explain_access</c> tool returns for a single- or all-verb explanation.
/// </summary>
public enum ExplainResponseFormat
{
    /// <summary>Each action's decision plus a single summarized reason per action.</summary>
    Concise,

    /// <summary>Each action's decision plus the full reasoning chain (all contributing reasons).</summary>
    Detailed,
}

/// <summary>
/// Arguments for the consolidated <c>uap_explain_access</c> tool. A single parameterized shape covers
/// explaining access for the current user, a specific user, a single user group, or every user group at a
/// content node.
/// </summary>
/// <param name="Subject">Whose access to evaluate.</param>
/// <param name="NodeKey">The content node to evaluate access at.</param>
/// <param name="UserKey">The user key; required when <see cref="ExplainSubject.User"/> is chosen.</param>
/// <param name="UserGroupAlias">
/// The user group alias (accepts '$everyone'); required when <see cref="ExplainSubject.UserGroup"/> is
/// chosen. Carries the value the base package calls a role alias.
/// </param>
/// <param name="Verb">Optional single verb to focus on; omit to evaluate all standard verbs.</param>
/// <param name="ResponseFormat">How much reasoning detail to return.</param>
/// <param name="Aspect">Which dimension of access to explain: node action permissions, or document-type creation.</param>
/// <param name="ContentTypeKey">Optional document type to focus on when <see cref="ExplainAspect.TypeCreate"/> is chosen.</param>
/// <param name="SuggestFix">
/// When <see langword="true"/> and access is denied, also return the concrete, confirmed permission
/// changes that would grant it. Only honoured for the node aspect, the current-user/user/user-group
/// subjects, and when a single <see cref="Verb"/> is supplied.
/// </param>
public sealed record ExplainAccessArgs(
    [property: Description("Whose access to evaluate: current-user (the editor asking about themselves), user (a specific user, requires userKey), user-group (one user group or 'All Users', requires userGroupAlias), or all-user-groups (who can/can't do this).")]
    ExplainSubject Subject,
    [property: Description("The GUID key of the content node to evaluate access at. For aspect=type-create this is the PARENT node under which creation is evaluated.")]
    Guid NodeKey,
    [property: Description("The GUID key of the user to evaluate. Required when subject is 'user'.")]
    Guid? UserKey = null,
    [property: Description("The user group alias to evaluate, or '$everyone' for All Users. Required when subject is 'user-group'.")]
    string? UserGroupAlias = null,
    [property: Description("Optional verb such as 'Umb.Document.Delete' to focus on a single action. Omit to evaluate all actions. Ignored when aspect is 'type-create'.")]
    string? Verb = null,
    [property: Description("How much detail to return: concise (decision plus one summary reason per action) or detailed (full reasoning chain).")]
    ExplainResponseFormat ResponseFormat = ExplainResponseFormat.Concise,
    [property: Description("node = action permissions like edit/delete/publish; type-create = which document types can be created under this node")]
    ExplainAspect Aspect = ExplainAspect.Node,
    [property: Description("optional — focus a single document type when Aspect=type-create")]
    Guid? ContentTypeKey = null,
    [property: Description("When true, and access is denied, also return the concrete permission changes that would grant it.")]
    bool SuggestFix = false);

/// <summary>
/// Selects which slice of the stored permission configuration the <c>uap_audit_permissions</c> tool audits.
/// </summary>
public enum AuditScope
{
    /// <summary>Audit every stored entry for a single user group across the whole tree (requires an alias).</summary>
    UserGroup,

    /// <summary>Audit every stored entry on a node and all of its descendant document nodes (requires a node key).</summary>
    Subtree,

    /// <summary>Audit the whole stored configuration across every node and user group (best-effort; see the tool description).</summary>
    All,
}

/// <summary>
/// Selects which stored permission configuration the <c>uap_audit_permissions</c> tool audits.
/// </summary>
/// <remarks>
/// <para>
/// The audit takes a domain argument where the explain tools got a whole sibling tool, and the asymmetry
/// is deliberate: the explain tools differ in their <i>arguments and reasoning</i>, whereas these paths
/// differ mainly in <b>which store to read</b> — every one of them yields entries of the same shape, so
/// a second tool would be a near-duplicate.
/// </para>
/// <para>
/// The rules are <i>mostly</i> shared, with one real split. The two node-permission domains
/// (<see cref="Content"/>, <see cref="Library"/>) are denied unless allowed, so a broad All Users
/// <b>Allow</b> is the risk. The two create-filter domains (<see cref="DocumentTypes"/>,
/// <see cref="LibraryElementTypes"/>) are allowed unless denied and can only ever narrow what Umbraco
/// already offers, so a broad Allow there grants nothing and the risk is the mirror image: a broad All
/// Users <b>Deny</b>, which hides a type from everyone. Applying one rule set to both would produce a
/// false positive in one direction and miss the real risk in the other.
/// </para>
/// </remarks>
public enum AuditDomain
{
    /// <summary>
    /// The content tree's stored entries. The default, and the only configuration the package audited
    /// before v18.
    /// </summary>
    Content,

    /// <summary>The Library tree's stored entries — the permissions on library items and folders.</summary>
    Library,

    /// <summary>
    /// The document-type create entries — the "Insert Options" deciding which document types each user
    /// group may create under which content nodes. Per node, so every scope applies.
    /// </summary>
    DocumentTypes,

    /// <summary>
    /// The Library element-type create entries — which element types each user group may create in the
    /// Library. Section-wide rather than per node, so <see cref="AuditScope.Subtree"/> does not apply.
    /// </summary>
    LibraryElementTypes,
}

/// <summary>Arguments for the audit-permissions tool.</summary>
/// <param name="Scope">
/// Which slice of the configuration to audit: a single user group (default), a node's subtree, or the
/// whole configuration.
/// </param>
/// <param name="UserGroupAlias">
/// The user group alias whose stored entries are audited, or '$everyone'. Required when
/// <see cref="AuditScope.UserGroup"/>. Carries the value the base package calls a role alias.
/// </param>
/// <param name="NodeKey">The content node whose subtree (this node plus descendants) is audited. Required when <see cref="AuditScope.Subtree"/>.</param>
/// <param name="SeverityMin">Optional minimum severity; when set, only findings at or above this severity are returned.</param>
/// <param name="Domain">
/// Which stored configuration to audit: the content tree (default), the Library tree, or the Library
/// element-type create entries.
/// </param>
public sealed record AuditPermissionsArgs(
    [property: Description("What to audit: 'user-group' (all entries for one user group — the default, requires userGroupAlias), 'subtree' (everything stored on a node and its descendants, requires nodeKey), or 'all' (the whole stored configuration).")]
    AuditScope Scope = AuditScope.UserGroup,
    [property: Description("The user group alias whose stored permission entries to audit, or '$everyone'. Required when scope is 'user-group'.")]
    string? UserGroupAlias = null,
    [property: Description("The GUID key of the node whose subtree (this node and all descendants) to audit. Required when scope is 'subtree'.")]
    Guid? NodeKey = null,
    [property: Description("Optional minimum severity filter: 'Info', 'Warning', or 'Risk'. When set, only findings at or above this severity are returned.")]
    AuditSeverity? SeverityMin = null,
    [property: Description("Which configuration to audit: 'content' (the content tree — the default), 'library' (the Library tree of items and folders), 'document-types' (the Insert Options deciding which document types each user group may create where), or 'library-element-types' (which element types each user group may create in the Library; section-wide, so scope 'subtree' is not valid with it).")]
    AuditDomain Domain = AuditDomain.Content);

/// <summary>
/// Arguments for the explain-concepts tool. The tool returns the whole conceptual reference, so it takes
/// no arguments — a topic filter would only risk the model selecting the wrong section and answering from
/// its own (wrong) assumptions about the rest.
/// </summary>
public sealed record ExplainConceptsArgs();

/// <summary>
/// Arguments for the explain-editors tool. Like the concepts tool it returns the whole reference and so
/// takes no arguments — and here the reason is sharper: the point of the reference is the boundaries
/// <i>between</i> the eight surfaces, which a filter to one of them would remove exactly when it is
/// needed.
/// </summary>
public sealed record ExplainEditorsArgs();
