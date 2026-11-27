using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>Analyzes stored permission entries for misconfigurations and risks.</summary>
public interface IPermissionAuditAnalyzer
{
    /// <summary>Inspects the supplied entries and returns an ordered set of findings.</summary>
    /// <param name="entries">The stored permission entries to analyze.</param>
    /// <param name="domain">
    /// Which configuration the entries come from. Content permissions and Insert Options default in
    /// opposite directions, so the broad All Users rule differs between them.
    /// </param>
    /// <returns>A report containing all findings, most-severe first.</returns>
    AuditReport Analyze(
        IReadOnlyList<AdvancedPermissionEntry> entries,
        AuditDomain domain = AuditDomain.Content);
}
