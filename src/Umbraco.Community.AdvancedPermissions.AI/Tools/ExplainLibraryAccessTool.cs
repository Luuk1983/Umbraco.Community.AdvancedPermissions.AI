using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>
/// The Library counterpart to <see cref="ExplainAccessTool"/>: answers "what is the effective permission
/// decision, and why" for the <b>Library</b> tree — its items and folders, and which element types may be
/// created in it.
/// </summary>
/// <remarks>
/// <para>
/// A sibling tool rather than two more aspects on the content tool, because the two trees differ in
/// arguments and reasoning, not merely in data. Library element-type creation takes <b>no node at all</b>
/// (Umbraco supplies no parent when creating in the Library, so the decision is section-wide), Library
/// nodes come in two kinds with different applicable permissions, and folding all four combinations into
/// one tool would double a description that is already at the limit of useful length — a cost paid on
/// every agent run. Naming the tool by domain also matches how editors think: the Library is a separate
/// section.
/// </para>
/// <para>
/// Everything shared is shared: the same four subjects, the same Concise/Detailed formats, the same
/// confirmed-remediation path, and the same presenter and remediator — rebound to
/// <see cref="PermissionDomain.Library"/> so node names resolve against the Library tree and the
/// simulation reads the Library's own entries. As with the content tool, every result is projected through
/// <see cref="IPermissionPresenter"/> so the model only ever sees friendly labels.
/// </para>
/// </remarks>
/// <param name="permissions">The Library node permission service used to resolve effective item/folder permissions.</param>
/// <param name="treeResolver">The Library tree reader: the root-to-node key path, and whether a key is an item or a folder.</param>
/// <param name="presenterRoot">The presenter, rebound to the Library domain on construction.</param>
/// <param name="userGroupService">The Umbraco user group service used to enumerate user groups for the all-user-groups subject.</param>
/// <param name="backOfficeSecurityAccessor">Accessor used to resolve the current backoffice user for the current-user subject.</param>
/// <param name="docTypePermissions">The create-filter service, used with the element-type create verb for the section-wide aspect.</param>
/// <param name="elementTypes">Provider for the element types creatable in the Library.</param>
/// <param name="remediation">The remediation service used to compute confirmed denial-to-allow changes when <see cref="ExplainLibraryAccessArgs.SuggestFix"/> is set.</param>
/// <param name="userService">The Umbraco user service used to resolve a user's group memberships.</param>
[AITool("uap_explain_library_access", "Explain Library access", ScopeId = "advanced-permissions:read")]
public sealed class ExplainLibraryAccessTool(
    IElementNodePermissionService permissions,
    IElementTreeResolver treeResolver,
    IPermissionPresenter presenterRoot,
    IUserGroupService userGroupService,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
    IDocTypePermissionService docTypePermissions,
    ILibraryElementTypeProvider elementTypes,
    IPermissionRemediationService remediation,
    IUserService userService)
    : AIToolBase<ExplainLibraryAccessArgs>
{
    /// <summary>The page size used when enumerating user groups, mirroring the roles metadata endpoint.</summary>
    private const int PageSize = 100;

    /// <summary>
    /// The presenter, bound once to the Library domain. Every projection below therefore resolves node
    /// keys against the Library tree and labels the virtual root as the Library's Default permissions row.
    /// </summary>
    private readonly IPermissionPresenter _presenter = presenterRoot.For(PermissionDomain.Library);

    /// <summary>
    /// The section-wide path used for element-type creation. Umbraco supplies no parent when creating in
    /// the Library, so the decision is resolved at the virtual root — mirroring the base package's own
    /// element-type audit endpoint.
    /// </summary>
    private static readonly Guid[] SectionWidePath = [AdvancedPermissionsConstants.VirtualRootNodeKey];

    /// <inheritdoc />
    public override string Description =>
        "Answer 'what is the effective permission decision, and why' for the LIBRARY — the reusable items " +
        "and folders that live outside the content tree, managed in the Library Permissions Editor and " +
        "Library Access Viewer. Use this INSTEAD OF uap_explain_access whenever the thing in question is a " +
        "library item, a library folder, or an element type: uap_explain_access covers the content tree only, " +
        "and the two are stored and resolved separately, so an answer from the wrong one is simply about the " +
        "wrong thing. " +
        "Set `subject`: `current-user` (the editor asking about themselves — 'why can't I delete this library item?'), " +
        "`user` (one specific user), `user-group` (one user group, or 'All Users'), or `all-user-groups` " +
        "(every user group — 'who can update library items here?'). " +
        "With aspect=node (the default) `nodeKey` is a library item or folder, and it returns each Library " +
        "permission's Allowed/Denied result with the reason (which user group and item, and whether the entry " +
        "is set directly or inherited). Some permissions are reported 'Not applicable' rather than allowed or " +
        "denied, because they have no meaning for that kind of node — Create on a single item, and " +
        "Publish/Unpublish/Duplicate/Rollback on a folder (those apply to the items inside it). Report that as " +
        "not applicable; do NOT describe it as denied or hunt for an entry causing it. " +
        "Set aspect=element-type-create for 'which element types can I create in the Library?' or 'why can't I " +
        "add element type X in the Library?'. That decision is SECTION-WIDE: pass NO nodeKey, because Umbraco " +
        "gives no parent when you create in the Library. Element types are creatable by DEFAULT, so a type with " +
        "no entries anywhere shows as allowed, and a Deny is what hides one. " +
        "Set suggestFix=true (with a single permission, aspect=node, and the `current-user`/`user`/`user-group` " +
        "subject) to also get the concrete, confirmed changes that would grant a denied permission — computed by " +
        "simulating them against the resolver, so do NOT guess fixes yourself (a plain Allow entry cannot beat a " +
        "Deny entry on the same node). " +
        "This is READ-ONLY: it describes the entries a human must add in the backoffice; it never applies them, " +
        "and you cannot apply or change permissions — never offer to.";

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(
        ExplainLibraryAccessArgs args,
        CancellationToken cancellationToken = default) =>
        args.Aspect == LibraryAspect.ElementTypeCreate
            ? await ExplainElementTypeCreateAsync(args, cancellationToken)
            : await ExplainNodeAsync(args, cancellationToken);

    // ----------------------------------------------------------------------------------------------
    // Node aspect: permissions on a Library item or folder. Mirrors the content tool's node aspect —
    // denied unless something allows it, resolved along the tree with scopes — with one addition the
    // content tree does not need: applicability. The Library holds two kinds of node, and some
    // permissions are meaningless for one of them.
    // ----------------------------------------------------------------------------------------------

    /// <summary>Dispatches the Library node aspect to the appropriate subject handler.</summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly explanation/verdict/roster, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainNodeAsync(ExplainLibraryAccessArgs args, CancellationToken cancellationToken)
    {
        if (args.NodeKey is not { } nodeKey)
        {
            return new AccessError("A nodeKey is required to explain permissions on a library item or folder.");
        }

        var path = treeResolver.GetPathFromRoot(nodeKey);
        var kind = treeResolver.GetNodeKind(nodeKey);

        return args.Subject switch
        {
            ExplainSubject.CurrentUser => await ExplainCurrentUserAsync(args, nodeKey, path, kind, cancellationToken),
            ExplainSubject.User => await ExplainUserAsync(args, nodeKey, path, kind, cancellationToken),
            ExplainSubject.UserGroup => await ExplainRoleAsync(args, nodeKey, path, kind, cancellationToken),
            ExplainSubject.AllUserGroups => await ExplainAllRolesAsync(args, nodeKey, path, kind, cancellationToken),
            _ => new AccessError("Unknown subject."),
        };
    }

    /// <summary>Resolves the current backoffice user and explains their Library access.</summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="nodeKey">The Library item or folder.</param>
    /// <param name="path">The resolved root-to-node key path.</param>
    /// <param name="kind">Whether the node is an item or a folder.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly explanation/verdict, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainCurrentUserAsync(
        ExplainLibraryAccessArgs args,
        Guid nodeKey,
        IReadOnlyList<Guid> path,
        LibraryNodeKind kind,
        CancellationToken cancellationToken)
    {
        var userKey = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        return userKey is null
            ? new AccessError("Could not determine the current user.")
            : await ResolveForUserAsync(userKey.Value, args, nodeKey, path, kind, cancellationToken);
    }

    /// <summary>Explains a specific user's Library access.</summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="nodeKey">The Library item or folder.</param>
    /// <param name="path">The resolved root-to-node key path.</param>
    /// <param name="kind">Whether the node is an item or a folder.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly explanation/verdict, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainUserAsync(
        ExplainLibraryAccessArgs args,
        Guid nodeKey,
        IReadOnlyList<Guid> path,
        LibraryNodeKind kind,
        CancellationToken cancellationToken) =>
        args.UserKey is null
            ? new AccessError("A userKey is required when explaining access for a specific user.")
            : await ResolveForUserAsync(args.UserKey.Value, args, nodeKey, path, kind, cancellationToken);

    /// <summary>
    /// Resolves and presents a single user's effective Library access: one verdict per permission, or a
    /// single verdict when one permission is focused.
    /// </summary>
    /// <param name="userKey">The user to resolve for.</param>
    /// <param name="args">The tool arguments.</param>
    /// <param name="nodeKey">The Library item or folder.</param>
    /// <param name="path">The resolved root-to-node key path.</param>
    /// <param name="kind">Whether the node is an item or a folder.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly verdict or explanation.</returns>
    private async Task<object> ResolveForUserAsync(
        Guid userKey,
        ExplainLibraryAccessArgs args,
        Guid nodeKey,
        IReadOnlyList<Guid> path,
        LibraryNodeKind kind,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(args.Permission))
        {
            // Applicability is checked BEFORE resolving: the resolver would return a perfectly ordinary
            // Allow/Deny for a combination the product shows as N/A, and relaying that is the error.
            if (!LibraryVerbApplicability.Applies(args.Permission, kind))
            {
                return _presenter.ToNotApplicableVerdict(args.Permission) with
                {
                    Ancestors = BuildAncestors(nodeKey, path),
                };
            }

            var single = await permissions.ResolveAsync(userKey, nodeKey, path, args.Permission, cancellationToken);
            var verdict = Format(await _presenter.ToVerdictAsync(single, cancellationToken), args.ResponseFormat)
                with { Ancestors = BuildAncestors(nodeKey, path) };
            var roleAliases = await GetUserRoleAliasesAsync(userKey);
            return await AttachRemediationAsync(verdict, single, args, nodeKey, path, roleAliases, cancellationToken);
        }

        var all = await permissions.ResolveAllAsync(userKey, nodeKey, path, null, cancellationToken);
        return Format(
            await ToExplanationAsync(all, nodeKey, path, kind, cancellationToken),
            args.ResponseFormat);
    }

    /// <summary>Explains a single user group's Library access.</summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="nodeKey">The Library item or folder.</param>
    /// <param name="path">The resolved root-to-node key path.</param>
    /// <param name="kind">Whether the node is an item or a folder.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly explanation/verdict, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainRoleAsync(
        ExplainLibraryAccessArgs args,
        Guid nodeKey,
        IReadOnlyList<Guid> path,
        LibraryNodeKind kind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args.UserGroupAlias))
        {
            return new AccessError("A userGroupAlias is required when explaining access for a single user group.");
        }

        if (!string.IsNullOrWhiteSpace(args.Permission)
            && !LibraryVerbApplicability.Applies(args.Permission, kind))
        {
            return _presenter.ToNotApplicableVerdict(args.Permission) with
            {
                Ancestors = BuildAncestors(nodeKey, path),
            };
        }

        var verbs = string.IsNullOrWhiteSpace(args.Permission) ? null : new[] { args.Permission };
        var resolved = await permissions.ResolveForRoleAsync(args.UserGroupAlias, nodeKey, path, verbs, cancellationToken);

        if (!string.IsNullOrWhiteSpace(args.Permission) && resolved.TryGetValue(args.Permission, out var single))
        {
            var verdict = Format(await _presenter.ToVerdictAsync(single, cancellationToken), args.ResponseFormat)
                with { Ancestors = BuildAncestors(nodeKey, path) };

            // Single-group resolution uses ONLY that group — $everyone is excluded, mirroring the base
            // package's ResolveForRoleAsync. The remediation must simulate with the same single-role set
            // or its baseline would not match the verdict being explained.
            IReadOnlyList<string> roleAliases = [args.UserGroupAlias];
            return await AttachRemediationAsync(verdict, single, args, nodeKey, path, roleAliases, cancellationToken);
        }

        return Format(await ToExplanationAsync(resolved, nodeKey, path, kind, cancellationToken), args.ResponseFormat);
    }

    /// <summary>
    /// Builds a "who can do this here" roster across every assignable user group for a Library node.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="nodeKey">The Library item or folder.</param>
    /// <param name="path">The resolved root-to-node key path.</param>
    /// <param name="kind">Whether the node is an item or a folder.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>An <see cref="AccessRoster"/> (single permission) or <see cref="AccessRosterReport"/> (all).</returns>
    private async Task<object> ExplainAllRolesAsync(
        ExplainLibraryAccessArgs args,
        Guid nodeKey,
        IReadOnlyList<Guid> path,
        LibraryNodeKind kind,
        CancellationToken cancellationToken)
    {
        var node = _presenter.GetNodeName(nodeKey);

        if (!string.IsNullOrWhiteSpace(args.Permission)
            && !LibraryVerbApplicability.Applies(args.Permission, kind))
        {
            // No group can hold a permission that does not apply, so both rosters are empty rather than
            // one of them being populated with a meaningless verdict.
            return new AccessRoster(_presenter.GetVerbDisplayName(args.Permission), node, [], []);
        }

        var verbs = string.IsNullOrWhiteSpace(args.Permission) ? null : new[] { args.Permission };

        // verb -> (allowed groups, denied groups), preserving first-seen verb order.
        var rosters = new Dictionary<string, (List<string> Allowed, List<string> Denied)>(StringComparer.Ordinal);
        var verbOrder = new List<string>();

        foreach (var roleAlias in await GetRoleAliasesAsync())
        {
            var resolved = await permissions.ResolveForRoleAsync(roleAlias, nodeKey, path, verbs, cancellationToken);
            if (resolved.Count == 0)
            {
                continue;
            }

            var displayName = await _presenter.GetRoleDisplayNameAsync(roleAlias, cancellationToken);
            foreach (var permission in resolved.Values)
            {
                // Inapplicable permissions are dropped from the roster entirely: listing every group as
                // "denied Publish" on a folder would be a misleading answer, not a useful one.
                if (!LibraryVerbApplicability.Applies(permission.Verb, kind))
                {
                    continue;
                }

                if (!rosters.TryGetValue(permission.Verb, out var bucket))
                {
                    bucket = (new List<string>(), new List<string>());
                    rosters[permission.Verb] = bucket;
                    verbOrder.Add(permission.Verb);
                }

                (permission.IsAllowed ? bucket.Allowed : bucket.Denied).Add(displayName);
            }
        }

        var actions = verbOrder
            .Select(verb => new AccessRoster(
                _presenter.GetVerbDisplayName(verb),
                node,
                rosters[verb].Allowed,
                rosters[verb].Denied))
            .ToList();

        if (!string.IsNullOrWhiteSpace(args.Permission))
        {
            return actions.Count == 1
                ? actions[0]
                : new AccessRoster(_presenter.GetVerbDisplayName(args.Permission), node, [], []);
        }

        return new AccessRosterReport(node, actions);
    }

    /// <summary>
    /// Projects a resolved permission set to a friendly explanation, substituting a "not applicable"
    /// verdict for every permission that has no meaning for this kind of Library node.
    /// </summary>
    /// <param name="resolved">The resolved permissions keyed by verb.</param>
    /// <param name="nodeKey">The Library item or folder.</param>
    /// <param name="path">The resolved root-to-node key path.</param>
    /// <param name="kind">Whether the node is an item or a folder.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly explanation.</returns>
    private async Task<AccessExplanation> ToExplanationAsync(
        IReadOnlyDictionary<string, EffectivePermission> resolved,
        Guid nodeKey,
        IReadOnlyList<Guid> path,
        LibraryNodeKind kind,
        CancellationToken cancellationToken)
    {
        var verdicts = new List<AccessVerdict>(resolved.Count);
        foreach (var permission in resolved.Values)
        {
            verdicts.Add(
                LibraryVerbApplicability.Applies(permission.Verb, kind)
                    ? await _presenter.ToVerdictAsync(permission, cancellationToken)
                    : _presenter.ToNotApplicableVerdict(permission.Verb));
        }

        return new AccessExplanation(_presenter.GetNodeName(nodeKey), verdicts, BuildAncestors(nodeKey, path));
    }

    // ----------------------------------------------------------------------------------------------
    // Element-type create aspect: which element types may be created in the Library.
    //
    // Works the opposite way round from node permissions — a type is creatable by DEFAULT and entries
    // narrow by exception — and, unlike the document-type filter, it is SECTION-WIDE: Umbraco supplies
    // no parent when creating in the Library, so it is resolved at the virtual root and takes no node.
    // There is therefore no structural "not an allowed child here" case to report.
    // ----------------------------------------------------------------------------------------------

    /// <summary>Dispatches the section-wide element-type create aspect to the appropriate subject handler.</summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly verdict/explanation/roster, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainElementTypeCreateAsync(
        ExplainLibraryAccessArgs args,
        CancellationToken cancellationToken) =>
        args.Subject switch
        {
            ExplainSubject.CurrentUser => backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key is { } key
                ? await ElementTypeCreateForUserAsync(key, args, cancellationToken)
                : new AccessError("Could not determine the current user."),
            ExplainSubject.User => args.UserKey is { } userKey
                ? await ElementTypeCreateForUserAsync(userKey, args, cancellationToken)
                : new AccessError("A userKey is required when explaining access for a specific user."),
            ExplainSubject.UserGroup => string.IsNullOrWhiteSpace(args.UserGroupAlias)
                ? new AccessError("A userGroupAlias is required when explaining access for a single user group.")
                : await ElementTypeCreateForRolesAsync(
                    [args.UserGroupAlias, AdvancedPermissionsConstants.EveryoneRoleAlias], args, cancellationToken),
            ExplainSubject.AllUserGroups => await ElementTypeCreateAllRolesAsync(args, cancellationToken),
            _ => new AccessError("Unknown subject."),
        };

    /// <summary>Explains element-type creation for one user, using their groups plus the All Users baseline.</summary>
    /// <param name="userKey">The user to resolve for.</param>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly verdict or explanation, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ElementTypeCreateForUserAsync(
        Guid userKey,
        ExplainLibraryAccessArgs args,
        CancellationToken cancellationToken)
    {
        var roleAliases = await GetUserRoleAliasesAsync(userKey);
        return roleAliases.Count == 0
            ? new AccessError("Could not resolve that user's user groups.")
            : await ElementTypeCreateForRolesAsync(roleAliases, args, cancellationToken);
    }

    /// <summary>
    /// Resolves element-type creation for an explicit role set: one verdict for a focused element type, or
    /// one per creatable element type.
    /// </summary>
    /// <param name="roleAliases">The role set to resolve with (the caller includes <c>$everyone</c> where appropriate).</param>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>An <see cref="ElementTypeCreateVerdict"/> or <see cref="ElementTypeCreateExplanation"/>.</returns>
    private async Task<object> ElementTypeCreateForRolesAsync(
        IReadOnlyList<string> roleAliases,
        ExplainLibraryAccessArgs args,
        CancellationToken cancellationToken)
    {
        if (args.ElementTypeKey is { } focused)
        {
            return await ToElementTypeVerdictAsync(focused, roleAliases, cancellationToken);
        }

        var candidates = elementTypes.GetLibraryElementTypes();
        var verdicts = new List<ElementTypeCreateVerdict>(candidates.Count);
        foreach (var candidate in candidates)
        {
            verdicts.Add(await ToElementTypeVerdictAsync(candidate.Key, roleAliases, cancellationToken));
        }

        return new ElementTypeCreateExplanation(verdicts);
    }

    /// <summary>Resolves and presents one element type's create verdict for a role set.</summary>
    /// <param name="elementTypeKey">The element type to evaluate.</param>
    /// <param name="roleAliases">The role set to resolve with.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly verdict.</returns>
    private async Task<ElementTypeCreateVerdict> ToElementTypeVerdictAsync(
        Guid elementTypeKey,
        IReadOnlyList<string> roleAliases,
        CancellationToken cancellationToken)
    {
        var permission = await docTypePermissions.ResolveCreateForRolesAsync(
            roleAliases,
            SectionWidePath,
            elementTypeKey,
            // The element-type create verb, NOT the document one: both live in the same store and are
            // told apart only by the verb, so passing the wrong one would resolve document entries.
            AdvancedPermissionsConstants.VerbElementCreateOfType,
            cancellationToken);

        var verdict = await _presenter.ToTypeCreateVerdictAsync(
            elementTypeKey,
            permission,
            // Always applicable: there is no parent, so there is no allowed-children check to fail.
            isInAllowedChildren: true,
            cancellationToken);

        return new ElementTypeCreateVerdict(verdict.DocumentType, verdict.Result, verdict.Reasons);
    }

    /// <summary>
    /// Builds the "who can create this element type in the Library" roster across every assignable user
    /// group.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>An <see cref="ElementTypeCreateRoster"/> (focused type) or <see cref="ElementTypeCreateRosterReport"/>.</returns>
    private async Task<object> ElementTypeCreateAllRolesAsync(
        ExplainLibraryAccessArgs args,
        CancellationToken cancellationToken)
    {
        var candidates = args.ElementTypeKey is { } focused
            ? [focused]
            : elementTypes.GetLibraryElementTypes().Select(ct => ct.Key).ToArray();

        var roleAliases = await GetRoleAliasesAsync();
        var displayNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var alias in roleAliases)
        {
            displayNames[alias] = await _presenter.GetRoleDisplayNameAsync(alias, cancellationToken);
        }

        var rosters = new List<ElementTypeCreateRoster>(candidates.Length);
        foreach (var elementTypeKey in candidates)
        {
            var allowed = new List<string>();
            var denied = new List<string>();

            foreach (var alias in roleAliases)
            {
                // Resolve per group with just that group + $everyone, matching the single-group semantics
                // used elsewhere; $everyone alone for the All Users group itself.
                IReadOnlyList<string> roles = alias == AdvancedPermissionsConstants.EveryoneRoleAlias
                    ? [AdvancedPermissionsConstants.EveryoneRoleAlias]
                    : [alias, AdvancedPermissionsConstants.EveryoneRoleAlias];

                var permission = await docTypePermissions.ResolveCreateForRolesAsync(
                    roles,
                    SectionWidePath,
                    elementTypeKey,
                    AdvancedPermissionsConstants.VerbElementCreateOfType,
                    cancellationToken);

                (permission.IsAllowed ? allowed : denied).Add(displayNames[alias]);
            }

            rosters.Add(new ElementTypeCreateRoster(
                _presenter.GetContentTypeName(elementTypeKey), allowed, denied));
        }

        return args.ElementTypeKey is not null && rosters.Count == 1
            ? rosters[0]
            : new ElementTypeCreateRosterReport(rosters);
    }

    // ----------------------------------------------------------------------------------------------
    // Shared helpers. Mirror the content tool's, so the two read alike.
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds the evaluated node's ancestor chain — root first, parent last — from the path the resolver
    /// already needed. The node itself and the virtual-root sentinel are excluded, so a root-level item
    /// yields an empty chain rather than a parent that does not exist.
    /// </summary>
    /// <param name="nodeKey">The evaluated node, excluded from its own ancestor chain.</param>
    /// <param name="path">The resolved root-to-node key path.</param>
    /// <returns>The named ancestor chain, empty when the item sits at the Library root.</returns>
    private IReadOnlyList<NodeRef> BuildAncestors(Guid nodeKey, IReadOnlyList<Guid> path) =>
        path
            .Where(k => k != nodeKey && k != AdvancedPermissionsConstants.VirtualRootNodeKey)
            .Select(k => new NodeRef(_presenter.GetNodeName(k), k))
            .ToList();

    /// <summary>
    /// Attaches confirmed remediations to a single denied verdict when remediation was requested.
    /// </summary>
    /// <param name="verdict">The friendly verdict produced for the focused permission.</param>
    /// <param name="permission">The raw effective permission backing the verdict.</param>
    /// <param name="args">The tool arguments.</param>
    /// <param name="nodeKey">The Library item or folder.</param>
    /// <param name="path">The resolved root-to-node key path.</param>
    /// <param name="roleAliases">The exact role set the verdict used.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The verdict, possibly enriched with confirmed remediations.</returns>
    private async Task<AccessVerdict> AttachRemediationAsync(
        AccessVerdict verdict,
        EffectivePermission permission,
        ExplainLibraryAccessArgs args,
        Guid nodeKey,
        IReadOnlyList<Guid> path,
        IReadOnlyList<string> roleAliases,
        CancellationToken cancellationToken)
    {
        if (!args.SuggestFix || permission.IsAllowed || string.IsNullOrWhiteSpace(args.Permission) || roleAliases.Count == 0)
        {
            return verdict;
        }

        var resolvePath = path.Count > 0 ? path : [AdvancedPermissionsConstants.VirtualRootNodeKey];

        var options = await remediation.SuggestAsync(
            nodeKey,
            resolvePath,
            roleAliases,
            args.Permission,
            PermissionState.Deny,
            // The Library domain, so the simulation reads the Library's own entries. Reading the content
            // table here would confirm a fix that changes nothing.
            PermissionDomain.Library,
            cancellationToken);

        if (options.Count == 0)
        {
            return verdict;
        }

        var forAsker = args.Subject == ExplainSubject.CurrentUser;

        var friendly = new List<AccessRemediation>(options.Count);
        foreach (var option in options)
        {
            friendly.Add(await _presenter.ToRemediationAsync(option, forAsker, cancellationToken));
        }

        return verdict with { Remediations = friendly };
    }

    /// <summary>
    /// Builds the role set used to resolve a user's permissions: every user group alias the user belongs
    /// to, plus the virtual <c>$everyone</c> role. Returns an empty list when the user cannot be resolved.
    /// </summary>
    /// <param name="userKey">The user whose group memberships to resolve.</param>
    /// <returns>The user's role aliases plus <c>$everyone</c>, or an empty list when unresolved.</returns>
    private async Task<IReadOnlyList<string>> GetUserRoleAliasesAsync(Guid userKey)
    {
        var user = await userService.GetAsync(userKey);
        if (user is null)
        {
            return [];
        }

        var aliases = new List<string>();
        aliases.AddRange(user.Groups.Select(g => g.Alias));
        aliases.Add(AdvancedPermissionsConstants.EveryoneRoleAlias);
        return aliases;
    }

    /// <summary>
    /// Enumerates all assignable role aliases: the virtual <c>$everyone</c> role first, followed by every
    /// Umbraco user group alias.
    /// </summary>
    /// <returns>The ordered list of role aliases.</returns>
    private async Task<IReadOnlyList<string>> GetRoleAliasesAsync()
    {
        var aliases = new List<string> { AdvancedPermissionsConstants.EveryoneRoleAlias };

        var skip = 0;
        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, PageSize);
            foreach (var group in page.Items)
            {
                aliases.Add(group.Alias);
            }

            skip += PageSize;
            if (skip >= page.Total)
            {
                break;
            }
        }

        return aliases;
    }

    /// <summary>Applies the requested response format to a single verdict.</summary>
    /// <param name="verdict">The friendly verdict to format.</param>
    /// <param name="format">The requested response format.</param>
    /// <returns>The formatted verdict.</returns>
    private static AccessVerdict Format(AccessVerdict verdict, ExplainResponseFormat format) =>
        format == ExplainResponseFormat.Detailed || verdict.Reasons.Count <= 1
            ? verdict
            : verdict with { Reasons = [verdict.Reasons[0]] };

    /// <summary>Applies the requested response format to a full explanation.</summary>
    /// <param name="explanation">The friendly explanation to format.</param>
    /// <param name="format">The requested response format.</param>
    /// <returns>The formatted explanation.</returns>
    private static AccessExplanation Format(AccessExplanation explanation, ExplainResponseFormat format) =>
        format == ExplainResponseFormat.Detailed
            ? explanation
            : explanation with { Permissions = explanation.Permissions.Select(v => Format(v, format)).ToList() };
}
