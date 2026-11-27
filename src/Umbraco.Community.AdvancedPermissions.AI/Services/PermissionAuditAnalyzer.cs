using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>Default <see cref="IPermissionAuditAnalyzer"/> implementing the MVP rule set.</summary>
public sealed class PermissionAuditAnalyzer : IPermissionAuditAnalyzer
{
    /// <summary>
    /// Whether an entry is an All Users entry at the virtual root reaching the whole tree — the shape of
    /// a site-wide decision, in either domain.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns><see langword="true"/> for a broad All Users entry.</returns>
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
        var isCreateFilter = domain == AuditDomain.DocumentTypes;

        foreach (var e in entries)
        {
            // Content permissions are denied unless allowed, so the broad risk is a blanket ALLOW of a
            // write permission. Read is excluded: an everyone-can-read baseline is normal.
            if (!isCreateFilter
                && IsBroadEveryoneEntry(e)
                && e.State == PermissionState.Allow
                && e.Verb != AdvancedPermissionsConstants.VerbRead)
            {
                findings.Add(new AuditFinding(
                    "everyone-broad-write", AuditSeverity.Risk,
                    $"Everyone is allowed '{e.Verb}' across the whole tree from the root.",
                    e.NodeKey, e.RoleAlias, e.Verb));
            }

            // Insert Options are allowed unless denied and can only narrow, so the mirror image applies:
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
        // Insert Options entries by document type — two different types at one node share all three keys
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
