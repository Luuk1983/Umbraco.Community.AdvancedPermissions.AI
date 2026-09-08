using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>Default <see cref="IPermissionAuditAnalyzer"/> implementing the MVP rule set.</summary>
/// <remarks>
/// Most rules are shared across every audit domain, because every domain's entries have the same shape.
/// The exception is the "broad All Users entry" rule, which has to point in opposite directions for the
/// two kinds of permission — see <see cref="AuditDomain"/>.
/// </remarks>
public sealed class PermissionAuditAnalyzer : IPermissionAuditAnalyzer
{
    /// <summary>
    /// Whether a domain holds create-filter entries (which are allowed by default and only narrow) rather
    /// than node permissions (which are denied unless allowed).
    /// </summary>
    /// <param name="domain">The audited domain.</param>
    /// <returns><see langword="true"/> for the two create-filter domains.</returns>
    private static bool IsCreateFilter(AuditDomain domain) =>
        domain is AuditDomain.DocumentTypes or AuditDomain.LibraryElementTypes;

    /// <summary>
    /// The read verb for a node-permission domain. Excluded from the broad-write risk rule, because an
    /// "All Users can read everything" baseline is the normal starting configuration in either tree — it
    /// is only the <i>other</i> verbs that constitute a site-wide write risk.
    /// </summary>
    /// <param name="domain">The audited domain.</param>
    /// <returns>The domain's read verb, or <see langword="null"/> for the create-filter domains, which have none.</returns>
    private static string? GetReadVerb(AuditDomain domain) => domain switch
    {
        AuditDomain.Content => AdvancedPermissionsConstants.VerbRead,
        AuditDomain.Library => AdvancedPermissionsConstants.VerbElementRead,
        _ => null,
    };

    /// <summary>Whether an entry is an All Users entry at the virtual root reaching the whole tree.</summary>
    /// <param name="entry">The entry to test.</param>
    /// <returns><see langword="true"/> when the entry applies to everyone, everywhere.</returns>
    private static bool IsBroadEveryoneEntry(AdvancedPermissionEntry entry) =>
        entry.RoleAlias == AdvancedPermissionsConstants.EveryoneRoleAlias
        && entry.NodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey
        && entry.Scope == PermissionScope.ThisNodeAndDescendants;

    /// <inheritdoc />
    public AuditReport Analyze(
        IReadOnlyList<AdvancedPermissionEntry> entries,
        AuditDomain domain = AuditDomain.Content)
    {
        var findings = new List<AuditFinding>();
        var isCreateFilter = IsCreateFilter(domain);
        var readVerb = GetReadVerb(domain);

        foreach (var e in entries)
        {
            // Node permissions are denied unless allowed, so the broad risk is a blanket ALLOW of a
            // write permission. Read is excluded: an everyone-can-read baseline is normal.
            if (!isCreateFilter
                && IsBroadEveryoneEntry(e)
                && e.State == PermissionState.Allow
                && e.Verb != readVerb)
            {
                findings.Add(new AuditFinding(
                    "everyone-broad-write", AuditSeverity.Risk,
                    $"Everyone is allowed '{e.Verb}' across the whole tree from the root.",
                    e.NodeKey, e.RoleAlias, e.Verb));
            }

            // Create filters are allowed unless denied and can only narrow, so the mirror image applies:
            // a blanket ALLOW grants nothing and is not worth reporting, while a blanket DENY hides the
            // type from everyone, everywhere. Reporting the Allow instead would be a false positive AND
            // would miss this.
            if (isCreateFilter
                && IsBroadEveryoneEntry(e)
                && e.State == PermissionState.Deny)
            {
                findings.Add(new AuditFinding(
                    "everyone-broad-create-deny", AuditSeverity.Risk,
                    $"Everyone is denied '{e.Verb}' across the whole tree from the root.",
                    e.NodeKey, e.RoleAlias, e.Verb));
            }

            if (e.Verb == AdvancedPermissionsConstants.VerbManagePermissions
                && e.State == PermissionState.Allow
                && e.Scope == PermissionScope.ThisNodeAndDescendants)
            {
                findings.Add(new AuditFinding(
                    "manage-permissions-descendants", AuditSeverity.Risk,
                    $"Role '{e.RoleAlias}' can manage permissions on this node and all descendants.",
                    e.NodeKey, e.RoleAlias, e.Verb));
            }

            if (e.IsPriorityOverride)
            {
                findings.Add(new AuditFinding(
                    "priority-override", AuditSeverity.Info,
                    $"Priority override is set for '{e.Verb}' on role '{e.RoleAlias}'.",
                    e.NodeKey, e.RoleAlias, e.Verb));
            }
        }

        // Grouping on (node, role, verb) is only sound because the caller has already partitioned
        // create-filter entries by content type — two different types at one node share all three keys
        // without being in conflict at all. See AuditPermissionsTool.
        var conflicts = entries
            .GroupBy(e => (e.NodeKey, e.RoleAlias, e.Verb))
            .Where(g => g.Any(x => x.State == PermissionState.Allow)
                     && g.Any(x => x.State == PermissionState.Deny));
        foreach (var g in conflicts)
        {
            findings.Add(new AuditFinding(
                "allow-deny-conflict", AuditSeverity.Warning,
                $"Role '{g.Key.RoleAlias}' has both Allow and Deny for '{g.Key.Verb}' on the same node.",
                g.Key.NodeKey, g.Key.RoleAlias, g.Key.Verb));
        }

        var ordered = findings.OrderByDescending(f => f.Severity).ToList();
        return new AuditReport(ordered, entries.Count);
    }
}
