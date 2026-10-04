using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Maps raw permission data (role aliases, verb identifiers, scope/state enums, node GUIDs and the
/// reasoning chain) to friendly, editor-facing labels. The AI tools use this so the model only ever
/// sees human-readable text and never parrots code identifiers such as <c>$everyone</c>,
/// <c>Umb.Document.Delete</c>, <c>ThisNodeOnly</c>, or a raw node GUID back to an editor.
/// </summary>
public interface IPermissionPresenter
{
    /// <summary>
    /// Returns a presenter bound to the given permission domain, so node keys are resolved against the
    /// right tree and the virtual-root sentinel gets the right label.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Node-name resolution is the only genuinely domain-dependent behaviour here, but it is reached from
    /// most of the other methods via the reasoning chain. Binding the domain once therefore keeps all nine
    /// signatures — and every existing call site — unchanged; the alternative was threading a
    /// <see cref="PermissionDomain"/> argument through the lot of them.
    /// </para>
    /// <para>
    /// The default, unbound presenter is <see cref="PermissionDomain.Content"/>, so a caller that predates
    /// the Library (or simply does not care) behaves exactly as it did in v17. Binding is cheap — the
    /// result is a new instance over the same injected services — and calling it with the already-bound
    /// domain is a no-op.
    /// </para>
    /// </remarks>
    /// <param name="domain">The permission tree the returned presenter should resolve against.</param>
    /// <returns>A presenter bound to <paramref name="domain"/>.</returns>
    IPermissionPresenter For(PermissionDomain domain);

    /// <summary>
    /// Resolves a role alias to its display name: the special "All Users" label for the
    /// <c>$everyone</c> role, the user group's name for a known group alias, or the alias itself
    /// as a last-resort fallback.
    /// </summary>
    /// <param name="roleAlias">The raw role alias.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly role display name.</returns>
    Task<string> GetRoleDisplayNameAsync(string roleAlias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a full permission verb to its friendly action name — the substring after the last
    /// dot (e.g. <c>Umb.Document.Delete</c> becomes "Delete").
    /// </summary>
    /// <param name="verb">The raw verb identifier.</param>
    /// <returns>The friendly action name.</returns>
    string GetVerbDisplayName(string verb);

    /// <summary>
    /// Converts a permission scope to friendly text: the Permissions Editor's own label followed by its
    /// plain-English meaning, e.g. "This node only (the node itself, not its children)". The label anchors
    /// the wording to what the editor sees in the scope dropdown; the meaning spares them from having to
    /// know what it reaches.
    /// </summary>
    /// <param name="scope">The scope to convert.</param>
    /// <returns>The friendly scope text, label and meaning together.</returns>
    string GetScopeText(PermissionScope scope);

    /// <summary>Converts a permission state to friendly text ("Allowed" or "Denied").</summary>
    /// <param name="state">The state to convert.</param>
    /// <returns>The friendly state text.</returns>
    string GetStateText(PermissionState state);

    /// <summary>
    /// Resolves a node key to a friendly name: a domain-specific label for the virtual-root sentinel (the
    /// Default permissions row), the node's own name when it can be resolved, or a generic fallback when
    /// it cannot.
    /// </summary>
    /// <remarks>
    /// Which tree the key is looked up in depends on the bound <see cref="PermissionDomain"/> — see
    /// <see cref="For(PermissionDomain)"/>. In the Library domain the key may be either an element or an
    /// element folder, so both object types are tried; and the sentinel must not be labelled "All content"
    /// while explaining a Library permission.
    /// </remarks>
    /// <param name="nodeKey">The content node, Library element, or element folder key.</param>
    /// <returns>The friendly node name.</returns>
    string GetNodeName(Guid nodeKey);

    /// <summary>
    /// Resolves a content-type (document type) key to its friendly display name via the content-type
    /// service, falling back to a generic "this document type" label when it cannot be resolved. Used by
    /// the type-create aspect so the model never sees a raw content-type alias or GUID.
    /// </summary>
    /// <param name="contentTypeKey">The document type key.</param>
    /// <returns>The friendly document type name.</returns>
    string GetContentTypeName(Guid contentTypeKey);

    /// <summary>
    /// Maps a resolved doc-type create <see cref="EffectivePermission"/> (and its reasoning chain) for a
    /// single document type to a friendly <see cref="TypeCreateVerdict"/>. The verb is presented as the
    /// human-facing "Insert" / "Create of type" action rather than the raw <c>Umb.Document.CreateOfType</c>
    /// identifier, and a structurally-disallowed type is reported as "Not applicable".
    /// </summary>
    /// <param name="contentTypeKey">The document type the permission was resolved for.</param>
    /// <param name="permission">The resolved effective create permission and its reasoning chain.</param>
    /// <param name="isInAllowedChildren">
    /// <see langword="false"/> when the type is structurally not an allowed child of the parent; the
    /// verdict is then "Not applicable" regardless of <paramref name="permission"/>.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly type-create verdict.</returns>
    Task<TypeCreateVerdict> ToTypeCreateVerdictAsync(
        Guid contentTypeKey,
        EffectivePermission permission,
        bool isInAllowedChildren,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the verdict for a permission that does not apply to the node at all, so the copilot can say
    /// "not applicable" instead of relaying a resolved value the product itself treats as meaningless.
    /// </summary>
    /// <remarks>
    /// Used for the Library combinations the base package renders as a hatched <b>N/A</b> cell — the
    /// Create permission on an item, and the item-only permissions on a folder (see
    /// <see cref="LibraryVerbApplicability"/>). The resolver still returns a value for those, and
    /// reporting it would have the copilot assert something like "Publish is denied on this folder",
    /// sending an editor hunting for a Deny entry that is not in effect. No reasons are attached, because
    /// there is no decision to explain.
    /// </remarks>
    /// <param name="verb">The raw verb that does not apply.</param>
    /// <returns>A verdict whose result is the same "Not applicable" label the type-create aspect uses.</returns>
    AccessVerdict ToNotApplicableVerdict(string verb);

    /// <summary>
    /// Maps a resolved <see cref="EffectivePermission"/> (and its reasoning chain) to a friendly
    /// <see cref="AccessVerdict"/> with no raw identifiers.
    /// </summary>
    /// <param name="permission">The resolved effective permission.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly verdict.</returns>
    Task<AccessVerdict> ToVerdictAsync(EffectivePermission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects a confirmed engine-level <see cref="RemediationOption"/> to a friendly, editor-facing
    /// <see cref="AccessRemediation"/>. The role alias, raw verb, scope/state enums, and node GUID are all
    /// replaced by display names, and a plain-language administrator-action sentence is built. Every
    /// returned remediation is a confirmed fact — the change has already been validated to flip the
    /// verdict to Allowed — so the wording states the outcome positively.
    /// </summary>
    /// <param name="option">The confirmed remediation option to project.</param>
    /// <param name="forAsker">
    /// <see langword="true"/> when the remediation explains the asker's <i>own</i> access, so the grant is
    /// addressed to them directly ("you are in the Administrators user group, which…") instead of being
    /// stated impersonally. Only set for the current-user subject, where the granting group is by
    /// construction one the asker belongs to — the remediation is simulated with exactly that user's groups.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly remediation.</returns>
    Task<AccessRemediation> ToRemediationAsync(
        RemediationOption option,
        bool forAsker = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps a verb-to-effective-permission dictionary and a node key to a friendly
    /// <see cref="AccessExplanation"/> with one verdict per verb.
    /// </summary>
    /// <param name="permissions">The resolved permissions keyed by verb.</param>
    /// <param name="nodeKey">The node the permissions were resolved at.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly explanation.</returns>
    /// <param name="ancestors">
    /// The node's ancestor chain, root first and parent last, carried through so the copilot can ask about
    /// the parent without searching for it. Empty for a root-level node.
    /// </param>
    Task<AccessExplanation> ToExplanationAsync(
        IReadOnlyDictionary<string, EffectivePermission> permissions,
        Guid nodeKey,
        IReadOnlyList<NodeRef> ancestors,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects a raw <see cref="AuditReport"/> to a <see cref="FriendlyAuditReport"/>, rewriting each
    /// finding's role, action, node, and message to friendly labels free of raw identifiers.
    /// </summary>
    /// <param name="report">The raw audit report.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly audit report.</returns>
    Task<FriendlyAuditReport> ToFriendlyAuditAsync(AuditReport report, CancellationToken cancellationToken = default);
}
