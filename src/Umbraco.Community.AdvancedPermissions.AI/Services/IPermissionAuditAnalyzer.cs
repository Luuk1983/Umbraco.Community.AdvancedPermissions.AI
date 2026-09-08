using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>Analyzes stored permission entries for misconfigurations and risks.</summary>
public interface IPermissionAuditAnalyzer
{
    /// <summary>Inspects the supplied entries and returns an ordered set of findings.</summary>
    /// <param name="entries">The stored permission entries to analyze.</param>
    /// <param name="domain">
    /// Which stored configuration the entries came from. Most rules are shared, since every domain's
    /// entries have the same shape, but two things depend on this:
    /// <list type="bullet">
    /// <item><description>
    /// The <b>read</b> verb excluded from the broad-write rule differs per tree, because an
    /// everyone-can-read baseline is normal rather than risky. Passing the wrong tree would report an
    /// ordinary Library read baseline as a site-wide write risk.
    /// </description></item>
    /// <item><description>
    /// Which <i>direction</i> a broad All Users entry is risky in. Node permissions are denied unless
    /// allowed, so a blanket Allow is the risk; the create filters are allowed unless denied and can only
    /// narrow, so a blanket Allow grants nothing and the risk is a blanket <b>Deny</b>. Applying one rule
    /// set to both yields a false positive in one direction and misses the real risk in the other.
    /// </description></item>
    /// </list>
    /// Defaults to <see cref="AuditDomain.Content"/>, preserving the pre-v18 behaviour for existing callers.
    /// </param>
    /// <remarks>
    /// For the create-filter domains the caller must pass entries for a <b>single content type</b> at a
    /// time. The conflict rule groups on (node, user group, verb), which two different content types at
    /// one node share without being in conflict — so mixing them together invents conflicts that do not
    /// exist.
    /// </remarks>
    /// <returns>A report containing all findings, most-severe first.</returns>
    AuditReport Analyze(
        IReadOnlyList<AdvancedPermissionEntry> entries,
        AuditDomain domain = AuditDomain.Content);
}
