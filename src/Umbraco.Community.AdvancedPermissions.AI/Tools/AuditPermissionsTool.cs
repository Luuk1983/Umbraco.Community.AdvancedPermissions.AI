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
/// <item><description><b>UserGroup</b> — every entry for one user group across the tree, via <see cref="IAdvancedPermissionRepository.GetByRoleAsync"/>.</description></item>
/// <item><description><b>Subtree</b> — every entry on a node and its descendant document nodes; descendant keys are gathered from <see cref="IEntityService"/> and entries loaded via <see cref="IAdvancedPermissionRepository.GetByNodesAsync"/>.</description></item>
/// <item><description><b>All</b> — a best-effort sweep of the whole configuration: every live document node (descendants of root) plus the virtual-root sentinel, loaded via <see cref="IAdvancedPermissionRepository.GetByNodesAsync"/>. The document-types domain reads every group's entries and keeps the same node set. See the limitation note on the description.</description></item>
/// </list>
/// In every case the entries are handed to the analyzer, the findings are optionally filtered to a
/// minimum severity, and the report is projected through the presenter.
/// <see cref="AuditPermissionsArgs.Domain"/> chooses the store: the content permission entries (default),
/// or the document-type Insert Options, which are analyzed one document type at a time.
/// </summary>
/// <param name="repository">The repository used to load stored permission entries.</param>
/// <param name="analyzer">The analyzer that inspects the entries and produces findings.</param>
/// <param name="presenter">The presenter that maps the raw report to friendly labels.</param>
/// <param name="entityService">The Umbraco entity service used to enumerate descendant document nodes for subtree/all scopes.</param>
/// <param name="userGroupService">The Umbraco user group service, used to map the user group the model names to its alias.</param>
/// <param name="lookupGuard">Refuses the audit unless the asker has Users-section access, as the base package's audit endpoints do.</param>
/// <param name="docTypeRepository">
/// The Insert Options store, for the document-types domain. Its entries are keyed per user group, and the
/// only complete way to gather them all is to ask for each group's entries in turn.
/// </param>
[AITool("uap_audit_permissions", "Audit permissions", ScopeId = "advanced-permissions:read")]
public sealed class AuditPermissionsTool(
    IAdvancedPermissionRepository repository,
    IPermissionAuditAnalyzer analyzer,
    IPermissionPresenter presenter,
    IEntityService entityService,
    IPermissionLookupGuard lookupGuard,
    IUserGroupService userGroupService,
    IDocTypePermissionRepository docTypeRepository)
    : AIToolBase<AuditPermissionsArgs>
{
    /// <summary>The page size used when enumerating descendant document nodes.</summary>
    private const int PageSize = 500;

    /// <summary>The page size used when enumerating user groups.</summary>
    private const int RolePageSize = 100;

    /// <summary>
    /// The maximum number of findings returned. Larger sweeps (notably <see cref="AuditScope.All"/>) can
    /// produce many findings; the report is capped to keep the answer readable and truncation is noted.
    /// </summary>
    private const int MaxFindings = 100;

    /// <summary>
    /// Maps the user group the model names — usually by display name, which is all it ever sees — to the
    /// real alias, so an unknown alias is never silently resolved as a group with no entries.
    /// </summary>
    private readonly UserGroupNameResolver _userGroupNames = new(userGroupService);

    /// <inheritdoc />
    public override string Description =>
        "Scan stored permission configuration for risks and conflicts. Two checks run in every domain: an " +
        "Allow entry and a Deny entry for the same permission on the same node; and Priority Override entries. " +
        "For `content` there are two more: a user group able to manage permissions across a node and its " +
        "descendants; and All Users ALLOWED a write permission across the whole site from the root. For " +
        "`document-types` there is one more: All Users DENIED creating a document type across the whole site " +
        "(an Allow there grants nothing, since Insert Options can only narrow what Umbraco already offers; and " +
        "Insert Options hold no manage-permissions entries, so that check does not apply). " +
        "That is the WHOLE rule set — a clean result means these checks found nothing, not that the " +
        "configuration was verified correct, so do not present it as a broader all-clear, and do not claim a " +
        "check that is not in this list. " +
        "Set `scope`: `user-group` (every entry for one user group — the default, and it requires `userGroupAlias`), " +
        "`subtree` (everything under a node, requires `nodeKey`), or `all` (the whole configuration, needs nothing else). " +
        "Optionally filter by minimum severity. " +
        "Use for 'audit/review the permissions for the Editors user group' (scope=user-group), 'any risks under /News?' " +
        "(scope=subtree), or 'is anything misconfigured?' / any site-wide question (scope=all — do not leave the " +
        "default in place for these, it will fail without a user group). " +
        "Set `domain` to choose WHICH configuration to scan: `content` (the permission entries on the content " +
        "tree — the default) or `document-types` (the Insert Options deciding which document types each user " +
        "group may create where). The two are stored separately, so each audit says nothing about the other — " +
        "when someone asks to audit 'everything', run it once per domain and say which ones you covered. " +
        "A `document-types` finding names the document type it concerns — always relay that, since a group " +
        "can have entries for many types. " +
        "Needs access to the Users section, like the base package's own viewers. " +
        "Note: `all` is best-effort — in either domain it sweeps every live document node plus the Default " +
        "permissions row, so entries left behind on deleted/trashed nodes are not included.";

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(AuditPermissionsArgs args, CancellationToken cancellationToken = default)
    {
        if (!lookupGuard.CanInspectOthers())
        {
            return new AccessError(PermissionLookupGuard.AuditRefusal);
        }

        if (args.Scope == AuditScope.UserGroup && !string.IsNullOrWhiteSpace(args.UserGroupAlias))
        {
            var match = await _userGroupNames.ResolveAsync(args.UserGroupAlias);
            if (match.Alias is null)
            {
                return new AccessError(match.Error!);
            }

            args = args with { UserGroupAlias = match.Alias };
        }

        var result = args.Domain == AuditDomain.DocumentTypes
            ? await AnalyzeCreateFilterAsync(args, cancellationToken)
            : await AnalyzeContentAsync(args, cancellationToken);

        if (result.Error is not null)
        {
            return result.Error;
        }

        var filtered = ApplyFilters(result.Report!, args.SeverityMin);
        return await presenter.ToFriendlyAuditAsync(filtered, cancellationToken);
    }

    /// <summary>Loads and analyzes the content tree's permission entries for the requested scope.</summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The report, or a friendly error.</returns>
    private async Task<(AuditReport? Report, AccessError? Error)> AnalyzeContentAsync(
        AuditPermissionsArgs args,
        CancellationToken cancellationToken)
    {
        var entriesResult = await LoadEntriesAsync(args, cancellationToken);
        return entriesResult.Error is not null
            ? (null, entriesResult.Error)
            : (analyzer.Analyze(entriesResult.Entries!, AuditDomain.Content), null);
    }

    /// <summary>
    /// Loads and analyzes the document-type Insert Options for the requested scope, one document type at
    /// a time.
    /// </summary>
    /// <remarks>
    /// The analyzer reasons over (node, user group, verb). Two different document types with an Allow and
    /// a Deny at the same node for the same group share all three, so analyzing them together would report
    /// a conflict that does not exist; the partition by document type has to happen here, and each
    /// finding gets its document type back afterwards.
    /// </remarks>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The report, or a friendly error.</returns>
    private async Task<(AuditReport? Report, AccessError? Error)> AnalyzeCreateFilterAsync(
        AuditPermissionsArgs args,
        CancellationToken cancellationToken)
    {
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
            stored.Where(e => string.Equals(e.Verb, AdvancedPermissionsConstants.VerbCreateOfType, StringComparison.Ordinal));

        if (args.Scope == AuditScope.Subtree)
        {
            var subtreeKeys = GatherSubtreeKeys(args.NodeKey!.Value).ToHashSet();
            relevant = relevant.Where(e => subtreeKeys.Contains(e.NodeKey));
        }
        else if (args.Scope == AuditScope.All)
        {
            // The same node set as the content sweep: the store has no by-node read, so every group's
            // entries are read and those left behind on deleted nodes dropped here. They no longer decide
            // anything, and a finding on one points at a node nobody can find.
            var liveKeys = GatherAllKeys().ToHashSet();
            relevant = relevant.Where(e => liveKeys.Contains(e.NodeKey));
        }

        var findings = new List<AuditFinding>();
        var analyzed = 0;

        foreach (var group in relevant.GroupBy(e => e.ContentTypeKey))
        {
            // Projected onto the entry shape the analyzer works on. That shape has no room for a document
            // type, so it is re-attached to the findings below.
            var projected = group
                .Select(e => new AdvancedPermissionEntry(
                    e.Id, e.NodeKey, e.RoleAlias, e.Verb, e.State, e.Scope, e.IsPriorityOverride))
                .ToList();

            var report = analyzer.Analyze(projected, AuditDomain.DocumentTypes);
            analyzed += report.EntriesAnalyzed;
            findings.AddRange(report.Findings.Select(f => f with { ContentTypeKey = group.Key }));
        }

        var ordered = findings.OrderByDescending(f => f.Severity).ToList();
        return (new AuditReport(ordered, analyzed), null);
    }

    /// <summary>
    /// Loads every stored Insert Options entry by asking for each user group's entries in turn.
    /// </summary>
    /// <remarks>
    /// The repository exposes no "everything" read, and the obvious shortcut — discovering which document
    /// types exist from the All Users entries and then loading those — is <b>incomplete</b>: a document type
    /// configured only for a named group has no All Users entry and would be missed entirely. In a report
    /// an administrator reads as a risk assessment a silent under-report is the worst failure mode, so this
    /// enumerates the groups instead. Each entry belongs to exactly one group, so the union needs no
    /// de-duplication.
    /// </remarks>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>Every stored Insert Options entry, across all groups, nodes and document types.</returns>
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
        switch (args.Scope)
        {
            case AuditScope.UserGroup:
                if (string.IsNullOrWhiteSpace(args.UserGroupAlias))
                {
                    return (null, new AccessError("A userGroupAlias is required when auditing a single user group — or set scope to 'all' to audit the whole configuration."));
                }

                return (await repository.GetByRoleAsync(args.UserGroupAlias, cancellationToken), null);

            case AuditScope.Subtree:
                if (args.NodeKey is null)
                {
                    return (null, new AccessError("A nodeKey is required when auditing a subtree."));
                }

                var subtreeKeys = GatherSubtreeKeys(args.NodeKey.Value);
                return (await repository.GetByNodesAsync(subtreeKeys, cancellationToken), null);

            case AuditScope.All:
                return (await repository.GetByNodesAsync(GatherAllKeys(), cancellationToken), null);

            default:
                return (null, new AccessError("Unknown audit scope."));
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
