using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Default <see cref="IPermissionPresenter"/>. Resolves role aliases to user group names via
/// <see cref="IUserGroupService"/> and node keys to content names via <see cref="IEntityService"/>,
/// mirroring the lookup the management API meta controller uses. Role lookups are cached for the
/// lifetime of the call chain so a single tool invocation never pages the user group service twice.
/// </summary>
/// <param name="userGroupService">The Umbraco user group service used to resolve role display names.</param>
/// <param name="entityService">The Umbraco entity service used to resolve node names.</param>
/// <param name="contentTypeService">The Umbraco content-type service used to resolve document type display names.</param>
public sealed class PermissionPresenter(
    IUserGroupService userGroupService,
    IEntityService entityService,
    IContentTypeService contentTypeService)
    : IPermissionPresenter
{
    /// <summary>The page size used when enumerating user groups, mirroring the roles metadata endpoint.</summary>
    private const int PageSize = 100;

    /// <summary>The generic label used when a node key cannot be resolved to a content name.</summary>
    private const string UnresolvedNodeLabel = "this node";

    /// <summary>The label used for the virtual-root sentinel node key.</summary>
    private const string VirtualRootLabel = "All content (root-level default)";

    /// <summary>The generic label used when a content-type key cannot be resolved to a name.</summary>
    private const string UnresolvedContentTypeLabel = "this document type";

    /// <summary>The friendly action label presented for the doc-type create verb.</summary>
    private const string TypeCreateActionLabel = "Insert";

    /// <summary>The friendly result label for a structurally-disallowed (not an allowed child) document type.</summary>
    private const string NotApplicableLabel = "Not applicable";

    /// <summary>
    /// The caution attached to every Priority Override remediation. An override is the only change that
    /// beats a same-node Deny entry, which also makes it the easiest to forget and the hardest to reason
    /// about later — so it is never presented as an equal-footing alternative to removing the Deny entry.
    /// </summary>
    private const string OverrideCaution =
        "Use Priority Override sparingly. It wins even over a Deny entry, so anyone reviewing these " +
        "permissions later sees a Deny that is silently not in effect. Prefer removing the Deny entry " +
        "when the restriction is no longer wanted, and use an override only when the Deny must stay in " +
        "place for everyone else.";

    /// <summary>
    /// Lazily-built map of user group alias to display name, cached for the lifetime of this
    /// presenter instance so repeated role lookups within a single tool call do not re-page.
    /// </summary>
    private IReadOnlyDictionary<string, string>? _roleNameCache;

    /// <inheritdoc />
    public async Task<string> GetRoleDisplayNameAsync(string roleAlias, CancellationToken cancellationToken = default)
    {
        if (roleAlias == AdvancedPermissionsConstants.EveryoneRoleAlias)
        {
            return AdvancedPermissionsConstants.EveryoneRoleDisplayName;
        }

        var map = await GetRoleNameMapAsync();
        return map.TryGetValue(roleAlias, out var name) ? name : roleAlias;
    }

    /// <inheritdoc />
    public string GetVerbDisplayName(string verb)
    {
        // The doc-type create verb is presented as the editor-facing "Insert" action rather than the raw
        // "CreateOfType" suffix so the type-create aspect never leaks the internal verb name.
        if (string.Equals(verb, AdvancedPermissionsConstants.VerbCreateOfType, StringComparison.Ordinal))
        {
            return TypeCreateActionLabel;
        }

        var lastDot = verb.LastIndexOf('.');
        return lastDot >= 0 && lastDot < verb.Length - 1
            ? verb[(lastDot + 1)..]
            : verb;
    }

    /// <inheritdoc />
    public string GetScopeText(PermissionScope scope) =>
        GetScopeGloss(scope) is { } gloss
            ? $"{GetScopeLabel(scope)} ({gloss})"
            : GetScopeLabel(scope);

    /// <summary>
    /// The scope's label exactly as it appears in the base package's Permissions Editor scope dropdown
    /// (<c>scope_thisNodeOnly</c> and friends). Kept verbatim so the copilot's wording anchors to what the
    /// editor actually sees on screen; the plain-English meaning is attached separately.
    /// </summary>
    /// <param name="scope">The scope to label.</param>
    /// <returns>The editor-facing scope label.</returns>
    private static string GetScopeLabel(PermissionScope scope) => scope switch
    {
        PermissionScope.ThisNodeOnly => "This node only",
        PermissionScope.ThisNodeAndDescendants => "This node and descendants",
        PermissionScope.DescendantsOnly => "Descendants only",
        _ => scope.ToString(),
    };

    /// <summary>
    /// The scope's plain-English meaning — what it actually reaches — taken verbatim from the base
    /// package's own <c>concepts.md</c> so the copilot explains a scope the same way the help docs do.
    /// A scope name alone ("This node only") tells an editor nothing about whether children are affected.
    /// </summary>
    /// <param name="scope">The scope to explain.</param>
    /// <returns>The meaning, or <see langword="null"/> for an unrecognised scope that has no gloss.</returns>
    private static string? GetScopeGloss(PermissionScope scope) => scope switch
    {
        PermissionScope.ThisNodeOnly => "the node itself, not its children",
        PermissionScope.ThisNodeAndDescendants => "the node and everything beneath it",
        PermissionScope.DescendantsOnly => "the children but not the node itself",
        _ => null,
    };

    /// <inheritdoc />
    public string GetStateText(PermissionState state) => state switch
    {
        PermissionState.Allow => "Allowed",
        PermissionState.Deny => "Denied",
        _ => state.ToString(),
    };

    /// <inheritdoc />
    public string GetNodeName(Guid nodeKey)
    {
        if (nodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey)
        {
            return VirtualRootLabel;
        }

        var entity = entityService.Get(nodeKey, UmbracoObjectTypes.Document);
        return string.IsNullOrWhiteSpace(entity?.Name) ? UnresolvedNodeLabel : entity.Name!;
    }

    /// <inheritdoc />
    public string GetContentTypeName(Guid contentTypeKey)
    {
        var contentType = contentTypeService.Get(contentTypeKey);
        return string.IsNullOrWhiteSpace(contentType?.Name) ? UnresolvedContentTypeLabel : contentType.Name!;
    }

    /// <inheritdoc />
    public async Task<TypeCreateVerdict> ToTypeCreateVerdictAsync(
        Guid contentTypeKey,
        EffectivePermission permission,
        bool isInAllowedChildren,
        CancellationToken cancellationToken = default)
    {
        var name = GetContentTypeName(contentTypeKey);

        // A type that is not an allowed child is structurally unavailable: report it distinctly from a
        // permission Deny, and do not surface permission reasoning that would not actually apply.
        if (!isInAllowedChildren)
        {
            return new TypeCreateVerdict(name, NotApplicableLabel, []);
        }

        var reasons = new List<AccessReason>(permission.Reasoning.Count);
        foreach (var r in permission.Reasoning)
        {
            reasons.Add(await ToReasonAsync(r));
        }

        return new TypeCreateVerdict(
            name,
            permission.IsAllowed ? "Allowed" : "Denied",
            reasons);
    }

    /// <inheritdoc />
    public async Task<AccessVerdict> ToVerdictAsync(EffectivePermission permission, CancellationToken cancellationToken = default)
    {
        var reasons = new List<AccessReason>(permission.Reasoning.Count);
        foreach (var r in permission.Reasoning)
        {
            reasons.Add(await ToReasonAsync(r));
        }

        return new AccessVerdict(
            GetVerbDisplayName(permission.Verb),
            permission.IsAllowed ? "Allowed" : "Denied",
            reasons);
    }

    /// <inheritdoc />
    public async Task<AccessRemediation> ToRemediationAsync(
        RemediationOption option,
        bool forAsker = false,
        CancellationToken cancellationToken = default)
    {
        var permission = GetVerbDisplayName(option.Verb);
        var node = GetNodeName(option.NodeKey);
        var scope = option.Scope is { } s ? GetScopeText(s) : null;

        if (option.Kind == RemediationActionKind.RemoveDeny)
        {
            // Name every user group whose Deny entry must be removed together, friendly.
            var roleNames = new List<string>(option.RemovedRoleAliases.Count);
            foreach (var alias in option.RemovedRoleAliases)
            {
                roleNames.Add(await GetRoleDisplayNameAsync(alias, cancellationToken));
            }

            var role = await GetRoleDisplayNameAsync(option.RoleAlias, cancellationToken);

            // Removing a Deny only grants access because something ELSE already allows it (no entry means
            // deny), so name that grant. Without it the sentence asserts an outcome the reader cannot check.
            var grantedBy = option.GrantedBy is { } granted
                ? await GrantedByTextAsync(granted, permission, forAsker, cancellationToken)
                : null;

            var removeDescription =
                $"An administrator could remove the Deny entry on the {permission} permission for {GroupsText(roleNames)} " +
                $"on {node}. {permission} would then be allowed" +
                (grantedBy is null ? "." : $", because {grantedBy}.");

            return new AccessRemediation(
                removeDescription, "Remove", role, permission, Scope: null, SetOn: node, GrantedBy: grantedBy);
        }

        // An addition: name the single target user group and build the action-specific sentence.
        var addRole = await GetRoleDisplayNameAsync(option.RoleAlias, cancellationToken);

        // State the scope in prose with its meaning attached: a scope name alone does not tell an editor
        // whether the entry reaches the children. Empty when the option carries no scope of its own.
        var scopePhrase = option.Scope is { } scoped
            ? $", with scope '{GetScopeLabel(scoped)}'" +
              (GetScopeGloss(scoped) is { } g ? $" ({g})" : string.Empty)
            : string.Empty;

        var (action, description, caution) = option.Kind switch
        {
            RemediationActionKind.AddPriorityOverrideAllow => (
                "Override",
                $"An administrator could add a Priority Override Allow entry on the {permission} permission for the " +
                $"{addRole} user group on {node}{scopePhrase}. That would override the conflicting Deny entry " +
                $"and allow {permission}.",
                (string?)OverrideCaution),
            RemediationActionKind.AddAllowOnAncestor => (
                "Add",
                $"An administrator could add an Allow entry on the {permission} permission for the {addRole} user " +
                $"group on {node}{scopePhrase}. {permission} would then be allowed here through inheritance.",
                null),
            _ => (
                "Add",
                $"An administrator could add an Allow entry on the {permission} permission for the {addRole} user " +
                $"group on {node}{scopePhrase}. {permission} would then be allowed.",
                null),
        };

        return new AccessRemediation(description, action, addRole, permission, scope, node, Caution: caution);
    }

    /// <summary>
    /// Builds the plain-language phrase naming what allows a permission once the contributing Deny entries
    /// are removed, covering the three ways a grant can reach the node: an entry set directly on it, an
    /// entry inherited from an ancestor, or the user group's own default.
    /// </summary>
    /// <param name="granted">The deciding Allow reasoning line from the confirming re-resolution.</param>
    /// <param name="permission">The friendly permission name the change is about.</param>
    /// <param name="forAsker">
    /// When <see langword="true"/> the grant is addressed to the reader in the second person, because the
    /// question was about their own access and the granting group is therefore one that applies to them.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A phrase suitable for use after "because …".</returns>
    private async Task<string> GrantedByTextAsync(
        PermissionReasoning granted,
        string permission,
        bool forAsker,
        CancellationToken cancellationToken)
    {
        var group = await GetRoleDisplayNameAsync(granted.ContributingRole, cancellationToken);

        // "All Users" reaches every backoffice user rather than being a group anyone joined, so the reader
        // is *covered by* it — saying they are "in" it would misdescribe how that group works.
        var subject = forAsker
            ? granted.ContributingRole == AdvancedPermissionsConstants.EveryoneRoleAlias
                ? $"you are covered by the {group} group, which"
                : $"you are in the {group} user group, which"
            : $"the {group} user group";

        // A group default carries no source scope, so there is no meaningful node to name.
        if (granted.IsFromGroupDefault || granted.SourceScope is null)
        {
            return $"{subject} is allowed {permission} by its group default";
        }

        var source = GetNodeName(granted.SourceNodeKey);
        return granted.IsExplicit
            ? $"{subject} has an Allow entry on the {permission} permission set directly on {source}"
            : $"{subject} has an Allow entry on the {permission} permission inherited from {source}";
    }

    /// <inheritdoc />
    public async Task<AccessExplanation> ToExplanationAsync(
        IReadOnlyDictionary<string, EffectivePermission> permissions,
        Guid nodeKey,
        CancellationToken cancellationToken = default)
    {
        var verdicts = new List<AccessVerdict>(permissions.Count);
        foreach (var permission in permissions.Values)
        {
            verdicts.Add(await ToVerdictAsync(permission, cancellationToken));
        }

        return new AccessExplanation(GetNodeName(nodeKey), verdicts);
    }

    /// <inheritdoc />
    public async Task<FriendlyAuditReport> ToFriendlyAuditAsync(AuditReport report, CancellationToken cancellationToken = default)
    {
        var findings = new List<FriendlyAuditFinding>(report.Findings.Count);
        foreach (var f in report.Findings)
        {
            var role = f.RoleAlias is null ? null : await GetRoleDisplayNameAsync(f.RoleAlias, cancellationToken);
            var action = f.Verb is null ? null : GetVerbDisplayName(f.Verb);
            var node = f.NodeKey is null ? null : GetNodeName(f.NodeKey.Value);

            findings.Add(new FriendlyAuditFinding(
                f.RuleId,
                f.Severity.ToString(),
                BuildFriendlyMessage(f.RuleId, role, action, node),
                role,
                action,
                node));
        }

        return new FriendlyAuditReport(findings, report.EntriesAnalyzed);
    }

    /// <summary>
    /// Maps a single reasoning line to a friendly <see cref="AccessReason"/>.
    /// </summary>
    /// <param name="reasoning">The raw reasoning line.</param>
    /// <returns>The friendly reason.</returns>
    private async Task<AccessReason> ToReasonAsync(PermissionReasoning reasoning) =>
        new(
            await GetRoleDisplayNameAsync(reasoning.ContributingRole),
            GetStateText(reasoning.State),
            reasoning.SourceScope is { } scope ? GetScopeText(scope) : "Group default",
            GetNodeName(reasoning.SourceNodeKey),
            Inherited: !reasoning.IsExplicit,
            PriorityOverride: reasoning.IsPriorityOverride);

    /// <summary>
    /// Rebuilds an audit finding's message from its rule id and friendly fields so it contains no
    /// raw role aliases, verb identifiers, or node GUIDs. Falls back to a generic message for
    /// unknown rule ids.
    /// </summary>
    /// <param name="ruleId">The stable rule identifier the finding came from.</param>
    /// <param name="role">The friendly role name, if any.</param>
    /// <param name="action">The friendly action name, if any.</param>
    /// <param name="node">The friendly node name, if any.</param>
    /// <returns>The friendly, identifier-free message.</returns>
    private static string BuildFriendlyMessage(string ruleId, string? role, string? action, string? node) => ruleId switch
    {
        "everyone-broad-write" =>
            $"The {role ?? "All Users"} group is allowed to {Lower(action) ?? "perform this action"} across the whole site from {node ?? "the root"}.",
        "manage-permissions-descendants" =>
            $"The {role ?? "relevant"} user group can manage permissions on {node ?? "this node"} and all of its descendants.",
        "priority-override" =>
            $"A Priority Override is set on the {role ?? "relevant"} user group for the {action ?? "action"} permission.",
        "allow-deny-conflict" =>
            $"{role ?? "This user group"} has both an Allow entry and a Deny entry for the {action ?? "same"} permission on {node ?? "the same node"}.",
        _ =>
            $"{role ?? "This user group"} has a configuration worth reviewing"
                + (action is null ? string.Empty : $" for the {action} permission")
                + (node is null ? string.Empty : $" on {node}") + ".",
    };

    /// <summary>Lower-cases the first character of a friendly action for use mid-sentence.</summary>
    /// <param name="value">The value to lower-case, or <see langword="null"/>.</param>
    /// <returns>The value with a lower-cased first character, or <see langword="null"/>.</returns>
    private static string? Lower(string? value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    /// <summary>
    /// Joins friendly role names into a natural-language list ("Editors", "Editors and All Users", or
    /// "A, B and C") for use in a remediation sentence.
    /// </summary>
    /// <param name="names">The friendly role names, in order.</param>
    /// <returns>The joined, human-readable list.</returns>
    private static string Join(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "the user group",
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}",
    };

    /// <summary>
    /// Renders friendly user group names as a natural-language phrase that names them as user groups
    /// ("the Editors user group", "the Editors and All Users user groups"), so a remediation sentence
    /// grounds the reader in what they see in the editor rather than a bare group name.
    /// </summary>
    /// <param name="names">The friendly user group names, in order.</param>
    /// <returns>The grounded, human-readable phrase including the "user group(s)" noun.</returns>
    private static string GroupsText(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "the relevant user group",
        // "All Users" is already a group name, and its reach is the whole point of a Deny entry on it —
        // so name it properly rather than as "the All Users user group", and say what it covers.
        1 when names[0] == AdvancedPermissionsConstants.EveryoneRoleDisplayName =>
            $"the {names[0]} group (which covers every backoffice user)",
        1 => $"the {names[0]} user group",
        _ => $"the {Join(names)} user groups",
    };

    /// <summary>
    /// Builds (once) and returns the map of user group alias to display name, paging the user group
    /// service exactly as the roles metadata endpoint does.
    /// </summary>
    /// <returns>The alias-to-name map.</returns>
    private async Task<IReadOnlyDictionary<string, string>> GetRoleNameMapAsync()
    {
        if (_roleNameCache is not null)
        {
            return _roleNameCache;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var skip = 0;
        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, PageSize);
            foreach (var group in page.Items)
            {
                map[group.Alias] = group.Name ?? group.Alias;
            }

            skip += PageSize;
            if (skip >= page.Total)
            {
                break;
            }
        }

        _roleNameCache = map;
        return map;
    }
}
