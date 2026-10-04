using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>
/// Scans stored permission entries for misconfigurations, conflicts, and risks and reports them in a
/// fully friendly form (role display names, friendly actions, node names, identifier-free messages —
/// never raw aliases/verbs/GUIDs). The slice that is audited is selected by
/// <see cref="AuditPermissionsArgs.Scope"/>:
/// <list type="bullet">
/// <item><description><b>UserGroup</b> — every entry for one user group across the tree, via <see cref="INodePermissionRepository.GetByRoleAsync"/>.</description></item>
/// <item><description><b>Subtree</b> — every entry on a node and its descendant document nodes; descendant keys are gathered from <see cref="IEntityService"/> and entries loaded via <see cref="INodePermissionRepository.GetByNodesAsync"/>.</description></item>
/// <item><description><b>All</b> — a best-effort sweep of the whole configuration: every live document node (descendants of root) plus the virtual-root sentinel, loaded via <see cref="INodePermissionRepository.GetByNodesAsync"/>. See the limitation note on the description.</description></item>
/// </list>
/// In every case the entries are handed to the analyzer, the findings are optionally filtered to a
/// minimum severity, and the report is projected through the presenter.
/// <para>
/// The two create-filter domains (<see cref="AuditDomain.DocumentTypes"/>,
/// <see cref="AuditDomain.LibraryElementTypes"/>) take a separate path, because their entries carry a
/// content type as well as a node: they are grouped by content type and analyzed one type at a time, and
/// each finding is stamped with the type it came from. See <c>AnalyzeCreateFilterAsync</c>.
/// </para>
/// </summary>
/// <param name="repository">The content repository used to load stored content-tree permission entries.</param>
/// <param name="elementRepository">The Library repository used to load stored element/folder permission entries.</param>
/// <param name="docTypeRepository">
/// The create-filter repository. Holds BOTH document-type and Library element-type entries in one store,
/// told apart only by the verb, so the element-type audit reads it filtered to
/// <see cref="AdvancedPermissionsConstants.VerbElementCreateOfType"/>.
/// </param>
/// <param name="analyzer">The analyzer that inspects the entries and produces findings.</param>
/// <param name="presenter">The presenter that maps the raw report to friendly labels.</param>
/// <param name="entityService">The Umbraco entity service used to enumerate descendant document nodes for subtree/all scopes.</param>
/// <param name="treeResolver">The Library tree reader, used to enumerate a Library subtree.</param>
/// <param name="userGroupService">
/// The Umbraco user group service, used to enumerate every user group when sweeping the whole
/// create-filter configuration. Those entries are keyed per user group, and the only complete way to
/// gather them all is to ask for each group's entries in turn.
/// </param>
[AITool("uap_audit_permissions", "Audit permissions", ScopeId = "advanced-permissions:read")]
public sealed class AuditPermissionsTool(
    IAdvancedPermissionRepository repository,
    IElementPermissionRepository elementRepository,
    IDocTypePermissionRepository docTypeRepository,
    IPermissionAuditAnalyzer analyzer,
    IPermissionPresenter presenter,
    IEntityService entityService,
    IElementTreeResolver treeResolver,
    IUserGroupService userGroupService)
    : AIToolBase<AuditPermissionsArgs>
{
    /// <summary>The page size used when enumerating descendant document nodes.</summary>
    private const int PageSize = 500;

    /// <summary>The page size used when enumerating user groups, mirroring the roles metadata endpoint.</summary>
    private const int RolePageSize = 100;

    /// <summary>
    /// The maximum number of findings returned. Larger sweeps (notably <see cref="AuditScope.All"/>) can
    /// produce many findings; the report is capped to keep the answer readable and truncation is noted.
    /// </summary>
    private const int MaxFindings = 100;

    /// <inheritdoc />
    public override string Description =>
        "Scan stored permission configuration for risks and conflicts. Three checks run in every domain: an " +
        "Allow entry and a Deny entry for the same thing on the same node; a user group able to manage " +
        "permissions across a node and its descendants; and Priority Override entries. A fourth check depends " +
        "on the domain, because the two kinds of permission default in opposite directions: for `content` and " +
        "`library` it is All Users ALLOWED a write permission across the whole site from the root, and for " +
        "`document-types` and `library-element-types` it is All Users DENIED creating a type across the whole " +
        "site (an Allow there grants nothing, since a create filter can only narrow). " +
        "That is the WHOLE rule set — a clean result means these checks found nothing, not that the " +
        "configuration was verified correct, so do not present it as a broader all-clear, and do not claim a " +
        "check that is not in this list. " +
        "Set `scope`: `user-group` (every entry for one user group — the default, and it requires `userGroupAlias`), " +
        "`subtree` (everything under a node, requires `nodeKey`), or `all` (the whole configuration, needs nothing else). " +
        "Optionally filter by minimum severity. " +
        "Use for 'audit/review the permissions for the Editors user group' (scope=user-group), 'any risks under /News?' " +
        "(scope=subtree), or 'is anything misconfigured?' / any site-wide question (scope=all — do not leave the " +
        "default in place for these, it will fail without a user group). " +
        "Set `domain` to choose WHICH configuration to scan: `content` (the content tree — the default), " +
        "`library` (the Library tree of reusable items and folders), `document-types` (the Insert Options " +
        "deciding which document types each user group may create where), or `library-element-types` (which " +
        "element types each user group may create in the Library). All four are stored separately, so each " +
        "audit says nothing about the others — when someone asks to audit 'everything', run it once per domain " +
        "and say which ones you covered. `library-element-types` is set once for the whole Library rather than " +
        "per node, so `scope=subtree` is not valid with it; the other three are per node and accept every scope. " +
        "A finding in either create-filter domain names the document type or element type it concerns — always " +
        "relay that, since a group can have entries for many types. " +
        "Note: `all` is best-effort — it sweeps every live node plus the root-level defaults, so entries " +
        "left behind on deleted/trashed nodes are not included.";

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(AuditPermissionsArgs args, CancellationToken cancellationToken = default)
    {
        // The presenter needs a TREE so finding node names resolve correctly. The document-type filter is
        // keyed on content nodes, and the element-type filter's entries sit at the virtual root, so each
        // create-filter domain presents as the tree it belongs to.
        var tree = args.Domain is AuditDomain.Library or AuditDomain.LibraryElementTypes
            ? PermissionDomain.Library
            : PermissionDomain.Content;

        var report = IsCreateFilter(args.Domain)
            ? await AnalyzeCreateFilterAsync(args, cancellationToken)
            : await AnalyzeNodePermissionsAsync(args, cancellationToken);

        if (report.Error is not null)
        {
            return report.Error;
        }

        var filtered = ApplyFilters(report.Report!, args.SeverityMin);
        return await presenter.For(tree).ToFriendlyAuditAsync(filtered, cancellationToken);
    }

    /// <summary>
    /// Whether a domain holds create-filter entries, which are keyed on a content type as well as a node
    /// and therefore have to be analyzed per content type.
    /// </summary>
    /// <param name="domain">The audited domain.</param>
    /// <returns><see langword="true"/> for the two create-filter domains.</returns>
    private static bool IsCreateFilter(AuditDomain domain) =>
        domain is AuditDomain.DocumentTypes or AuditDomain.LibraryElementTypes;

    /// <summary>Loads and analyzes a node-permission domain (content or Library).</summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The raw report, or an error describing a missing argument.</returns>
    private async Task<(AuditReport? Report, AccessError? Error)> AnalyzeNodePermissionsAsync(
        AuditPermissionsArgs args,
        CancellationToken cancellationToken)
    {
        var entriesResult = await LoadEntriesAsync(args, cancellationToken);
        return entriesResult.Error is not null
            ? (null, entriesResult.Error)
            : (analyzer.Analyze(entriesResult.Entries!, args.Domain), null);
    }

    /// <summary>
    /// Loads the stored entries to audit for the requested scope, or returns a friendly error when a
    /// required argument for that scope is missing.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The loaded entries, or an <see cref="AccessError"/> describing the missing argument.</returns>
    private async Task<(IReadOnlyList<AdvancedPermissionEntry>? Entries, AccessError? Error)> LoadEntriesAsync(
        AuditPermissionsArgs args,
        CancellationToken cancellationToken)
    {
        var source = args.Domain == AuditDomain.Library
            ? elementRepository
            : (INodePermissionRepository)repository;

        switch (args.Scope)
        {
            case AuditScope.UserGroup:
                if (string.IsNullOrWhiteSpace(args.UserGroupAlias))
                {
                    return (null, new AccessError("A userGroupAlias is required when auditing a single user group — or set scope to 'all' to audit the whole configuration."));
                }

                return (await source.GetByRoleAsync(args.UserGroupAlias, cancellationToken), null);

            case AuditScope.Subtree:
                if (args.NodeKey is null)
                {
                    return (null, new AccessError("A nodeKey is required when auditing a subtree."));
                }

                var subtreeKeys = args.Domain == AuditDomain.Library
                    ? GatherLibrarySubtreeKeys(args.NodeKey.Value)
                    : GatherSubtreeKeys(args.NodeKey.Value);
                return (await source.GetByNodesAsync(subtreeKeys, cancellationToken), null);

            case AuditScope.All:
                var allKeys = args.Domain == AuditDomain.Library ? GatherAllLibraryKeys() : GatherAllKeys();
                return (await source.GetByNodesAsync(allKeys, cancellationToken), null);

            default:
                return (null, new AccessError("Unknown audit scope."));
        }
    }

    /// <summary>
    /// Loads and analyzes a create-filter domain, one content type at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create-filter entries are keyed on (node, user group, <b>content type</b>, verb), and the analyzer
    /// reasons over (node, user group, verb). Two <i>different</i> content types with an Allow and a Deny
    /// at the same node for the same group therefore share every key the analyzer looks at, without being
    /// in conflict at all — analyzing them together would invent a conflict. So the entries are grouped by
    /// content type, each group is analyzed on its own, and every resulting finding is stamped with the
    /// content type it came from before the reports are merged.
    /// </para>
    /// <para>
    /// Both create-filter domains share one store and are told apart only by the verb, so the wrong verb
    /// here would silently audit the other domain's entries.
    /// </para>
    /// </remarks>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The merged raw report, or an error describing an unusable scope or missing argument.</returns>
    private async Task<(AuditReport? Report, AccessError? Error)> AnalyzeCreateFilterAsync(
        AuditPermissionsArgs args,
        CancellationToken cancellationToken)
    {
        var verb = args.Domain == AuditDomain.LibraryElementTypes
            ? AdvancedPermissionsConstants.VerbElementCreateOfType
            : AdvancedPermissionsConstants.VerbCreateOfType;

        // Library element types are one decision for the whole Library, so there is no subtree to audit.
        // Document-type Insert Options ARE per node, so subtree is perfectly valid there.
        if (args.Scope == AuditScope.Subtree && args.Domain == AuditDomain.LibraryElementTypes)
        {
            return (null, new AccessError(
                "Library element-type permissions are set once for the whole Library, not per node, so there is no subtree to audit. Use scope 'user-group' or 'all' instead."));
        }

        if (args.Scope == AuditScope.UserGroup && string.IsNullOrWhiteSpace(args.UserGroupAlias))
        {
            return (null, new AccessError("A userGroupAlias is required when auditing a single user group — or set scope to 'all' to audit the whole configuration."));
        }

        if (args.Scope == AuditScope.Subtree && args.NodeKey is null)
        {
            return (null, new AccessError("A nodeKey is required when auditing a subtree."));
        }

        var stored = args.Scope == AuditScope.UserGroup
            ? await docTypeRepository.GetByRoleAsync(args.UserGroupAlias!, cancellationToken)
            : await LoadCreateFilterEntriesForEveryRoleAsync(cancellationToken);

        IEnumerable<DocTypePermissionEntry> relevant =
            stored.Where(e => string.Equals(e.Verb, verb, StringComparison.Ordinal));

        if (args.Scope == AuditScope.Subtree)
        {
            var subtreeKeys = GatherSubtreeKeys(args.NodeKey!.Value).ToHashSet();
            relevant = relevant.Where(e => subtreeKeys.Contains(e.NodeKey));
        }

        var findings = new List<AuditFinding>();
        var analyzed = 0;

        foreach (var group in relevant.GroupBy(e => e.ContentTypeKey))
        {
            // Projected onto the shared entry shape the analyzer works on. That shape has no room for a
            // content type, so it is re-attached to the findings below — which is exactly why the
            // grouping has to happen out here rather than inside the analyzer.
            var projected = group
                .Select(e => new AdvancedPermissionEntry(
                    e.Id, e.NodeKey, e.RoleAlias, e.Verb, e.State, e.Scope, e.IsPriorityOverride))
                .ToList();

            var report = analyzer.Analyze(projected, args.Domain);
            analyzed += report.EntriesAnalyzed;
            findings.AddRange(report.Findings.Select(f => f with { ContentTypeKey = group.Key }));
        }

        var ordered = findings.OrderByDescending(f => f.Severity).ToList();
        return (new AuditReport(ordered, analyzed), null);
    }

    /// <summary>
    /// Loads every stored create-filter entry by asking for each user group's entries in turn.
    /// </summary>
    /// <remarks>
    /// The repository exposes no "everything" read, and the obvious shortcut — discovering which content
    /// types exist from the All Users entries and then loading those — is <b>incomplete</b>: a content
    /// type configured only for a named group has no All Users entry and would be missed entirely. In a
    /// report an administrator reads as a risk assessment a silent under-report is the worst failure
    /// mode, so this enumerates the groups instead. Each entry belongs to exactly one group, so the union
    /// needs no de-duplication, and the call count is bounded by the number of user groups.
    /// </remarks>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>Every stored create-filter entry, across all groups, nodes and content types.</returns>
    private async Task<IReadOnlyList<DocTypePermissionEntry>> LoadCreateFilterEntriesForEveryRoleAsync(
        CancellationToken cancellationToken)
    {
        var all = new List<DocTypePermissionEntry>();

        foreach (var alias in await GetRoleAliasesAsync())
        {
            all.AddRange(await docTypeRepository.GetByRoleAsync(alias, cancellationToken));
        }

        return all;
    }

    /// <summary>
    /// Enumerates all assignable role aliases: the virtual everyone role first, followed by every Umbraco
    /// user group alias.
    /// </summary>
    /// <returns>The ordered list of role aliases.</returns>
    private async Task<IReadOnlyList<string>> GetRoleAliasesAsync()
    {
        var aliases = new List<string> { AdvancedPermissionsConstants.EveryoneRoleAlias };

        var skip = 0;
        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, RolePageSize);
            foreach (var group in page.Items)
            {
                aliases.Add(group.Alias);
            }

            skip += RolePageSize;
            if (skip >= page.Total)
            {
                break;
            }
        }

        return aliases;
    }

    /// <summary>
    /// Gathers a Library node plus every descendant item and folder key, so a Library subtree audit
    /// covers the whole branch.
    /// </summary>
    /// <param name="nodeKey">The root of the Library subtree to audit.</param>
    /// <returns>The node key followed by all descendant Library keys.</returns>
    private IReadOnlyList<Guid> GatherLibrarySubtreeKeys(Guid nodeKey)
    {
        var keys = new List<Guid> { nodeKey };
        keys.AddRange(EnumerateLibraryDescendantKeys(nodeKey));
        return keys;
    }

    /// <summary>
    /// Gathers every live Library key (items and folders) plus the virtual-root sentinel carrying the
    /// Library's default entries. Best-effort in the same way the content sweep is: entries left on
    /// nodes that no longer exist cannot be included.
    /// </summary>
    /// <returns>All live Library keys plus the virtual-root sentinel.</returns>
    private IReadOnlyList<Guid> GatherAllLibraryKeys()
    {
        var keys = new List<Guid> { AdvancedPermissionsConstants.VirtualRootNodeKey };
        keys.AddRange(EnumerateLibraryDescendantKeys(null));
        return keys;
    }

    /// <summary>
    /// Enumerates Library descendant keys across both Library object types, paging until exhausted.
    /// </summary>
    /// <param name="nodeKey">
    /// The node whose descendants to enumerate, or <see langword="null"/> to sweep the whole Library.
    /// </param>
    /// <returns>The descendant Library keys.</returns>
    private IEnumerable<Guid> EnumerateLibraryDescendantKeys(Guid? nodeKey)
    {
        // Items and folders are separate object types, so both are swept. A folder key is meaningful in
        // its own right here: folders carry permission entries just as items do.
        foreach (var objectType in new[] { UmbracoObjectTypes.Element, UmbracoObjectTypes.ElementContainer })
        {
            var pageIndex = 0L;
            long total;
            do
            {
                var page = entityService
                    .GetPagedDescendants(objectType, pageIndex, PageSize, out total)
                    .ToList();

                foreach (var entity in page)
                {
                    // When scoped to a subtree, keep only nodes whose resolved path contains the root.
                    if (nodeKey is null || treeResolver.GetPathFromRoot(entity.Key).Contains(nodeKey.Value))
                    {
                        yield return entity.Key;
                    }
                }

                pageIndex++;
            }
            while (pageIndex * PageSize < total);
        }
    }

    /// <summary>
    /// Gathers the node itself plus every descendant document node key, so the subtree audit covers the
    /// whole branch rooted at the node.
    /// </summary>
    /// <param name="nodeKey">The root of the subtree to audit.</param>
    /// <returns>The node key followed by all descendant document keys.</returns>
    private IReadOnlyList<Guid> GatherSubtreeKeys(Guid nodeKey)
    {
        var keys = new List<Guid> { nodeKey };
        keys.AddRange(EnumerateDescendantKeys(nodeKey));
        return keys;
    }

    /// <summary>
    /// Gathers every live document node key (the descendants of the tree root) plus the virtual-root
    /// sentinel that carries root-level default entries. This is the best-effort key set for the
    /// whole-configuration audit; entries on nodes that no longer exist cannot be included.
    /// </summary>
    /// <returns>All live document keys plus the virtual-root sentinel.</returns>
    private IReadOnlyList<Guid> GatherAllKeys()
    {
        var keys = new List<Guid> { AdvancedPermissionsConstants.VirtualRootNodeKey };

        var pageIndex = 0L;
        long total;
        do
        {
            var page = entityService
                .GetPagedDescendants(UmbracoObjectTypes.Document, pageIndex, PageSize, out total)
                .ToList();
            keys.AddRange(page.Select(e => e.Key));
            pageIndex++;
        }
        while (pageIndex * PageSize < total);

        return keys;
    }

    /// <summary>
    /// Enumerates all descendant document node keys of the given node, paging through the entity service
    /// until every descendant has been collected. The node is resolved to its integer id first so the
    /// unambiguous id-based descendants overload can be used.
    /// </summary>
    /// <param name="nodeKey">The node whose descendants to enumerate.</param>
    /// <returns>The descendant document keys.</returns>
    private IEnumerable<Guid> EnumerateDescendantKeys(Guid nodeKey)
    {
        var idAttempt = entityService.GetId(nodeKey, UmbracoObjectTypes.Document);
        if (!idAttempt.Success)
        {
            return [];
        }

        return EnumerateDescendantKeys(idAttempt.Result);
    }

    /// <summary>
    /// Enumerates all descendant document node keys of the node with the given integer id, paging through
    /// the entity service until every descendant has been collected.
    /// </summary>
    /// <param name="nodeId">The integer id of the node whose descendants to enumerate.</param>
    /// <returns>The descendant document keys.</returns>
    private IEnumerable<Guid> EnumerateDescendantKeys(int nodeId)
    {
        var pageIndex = 0L;
        long total;
        do
        {
            var page = entityService
                .GetPagedDescendants(nodeId, UmbracoObjectTypes.Document, pageIndex, PageSize, out total)
                .ToList();

            foreach (var entity in page)
            {
                yield return entity.Key;
            }

            pageIndex++;
        }
        while (pageIndex * PageSize < total);
    }

    /// <summary>
    /// Applies the optional minimum-severity filter and caps the number of findings, recording how many
    /// entries were analyzed (unchanged) so the friendly report still reports the true inspected count.
    /// </summary>
    /// <param name="report">The raw analyzer report.</param>
    /// <param name="severityMin">The optional minimum severity; <see langword="null"/> keeps all findings.</param>
    /// <returns>A report whose findings honour the severity filter and the findings cap.</returns>
    private static AuditReport ApplyFilters(AuditReport report, AuditSeverity? severityMin)
    {
        IEnumerable<AuditFinding> findings = report.Findings;

        if (severityMin is { } min)
        {
            findings = findings.Where(f => f.Severity >= min);
        }

        var capped = findings.Take(MaxFindings).ToList();
        return report with { Findings = capped };
    }
}
