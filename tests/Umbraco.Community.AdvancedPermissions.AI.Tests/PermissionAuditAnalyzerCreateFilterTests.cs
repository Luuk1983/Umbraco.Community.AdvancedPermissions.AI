using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for the analyzer's handling of the two <b>create-filter</b> audit domains — document-type
/// Insert Options and Library element types.
/// </summary>
/// <remarks>
/// <para>
/// The create filters default in the opposite direction from node permissions: a type is creatable
/// unless something denies it, and an Allow entry only ever <i>keeps</i> a type Umbraco already offers.
/// That inverts what counts as a risk, and applying the node-permission rule set unchanged would be
/// actively misleading in both directions:
/// </para>
/// <list type="bullet">
/// <item><description>
/// A false positive. "All Users allowed across the whole tree" is a genuine risk for a node permission,
/// but for a create filter it is the <i>default state expressed explicitly</i> and grants nothing — so
/// reporting it as a site-wide risk sends an administrator chasing a non-problem.
/// </description></item>
/// <item><description>
/// A false negative. The real broad risk in a filter is the mirror image: an All Users <b>Deny</b> at the
/// root scoped across the tree, which hides a type from everyone everywhere. The node-permission rule
/// only ever looks at Allow entries, so it would miss it entirely.
/// </description></item>
/// </list>
/// </remarks>
public sealed class PermissionAuditAnalyzerCreateFilterTests
{
    /// <summary>The system under test.</summary>
    private readonly IPermissionAuditAnalyzer _sut = new PermissionAuditAnalyzer();

    /// <summary>The two create-filter domains and their create verbs.</summary>
    /// <returns>Domain and verb pairs.</returns>
    public static TheoryData<AuditDomain, string> CreateFilterDomains() => new()
    {
        { AuditDomain.DocumentTypes, AdvancedPermissionsConstants.VerbCreateOfType },
        { AuditDomain.LibraryElementTypes, AdvancedPermissionsConstants.VerbElementCreateOfType },
    };

    /// <summary>Builds an All Users entry at the virtual root, scoped across the whole tree.</summary>
    /// <param name="verb">The verb the entry controls.</param>
    /// <param name="state">Allow or Deny.</param>
    /// <returns>The entry.</returns>
    private static AdvancedPermissionEntry EveryoneRoot(string verb, PermissionState state) =>
        new(
            Guid.NewGuid(),
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            verb,
            state,
            PermissionScope.ThisNodeAndDescendants);

    /// <summary>Whether the report contains a finding with the given rule id.</summary>
    /// <param name="report">The report to inspect.</param>
    /// <param name="ruleId">The rule id to look for.</param>
    /// <returns><see langword="true"/> when the rule fired.</returns>
    private static bool Has(AuditReport report, string ruleId) =>
        report.Findings.Any(f => f.RuleId == ruleId);

    /// <summary>
    /// THE FALSE POSITIVE. An All Users Allow across the whole tree is the create filter's default state
    /// written down explicitly — it grants nothing Umbraco does not already permit, so it must not be
    /// reported as a site-wide write risk.
    /// </summary>
    /// <param name="domain">The create-filter domain.</param>
    /// <param name="verb">Its create verb.</param>
    [Theory]
    [MemberData(nameof(CreateFilterDomains))]
    public void Analyze_CreateFilter_EveryoneAllowAcrossTheTree_IsNotARisk(AuditDomain domain, string verb) =>
        Assert.False(Has(_sut.Analyze([EveryoneRoot(verb, PermissionState.Allow)], domain), "everyone-broad-write"));

    /// <summary>
    /// THE FALSE NEGATIVE. An All Users Deny at the root across the whole tree hides the type from
    /// everyone, everywhere — the real broad risk in a filter, and one the Allow-only node rule misses.
    /// </summary>
    /// <param name="domain">The create-filter domain.</param>
    /// <param name="verb">Its create verb.</param>
    [Theory]
    [MemberData(nameof(CreateFilterDomains))]
    public void Analyze_CreateFilter_EveryoneDenyAcrossTheTree_IsARisk(AuditDomain domain, string verb)
    {
        var report = _sut.Analyze([EveryoneRoot(verb, PermissionState.Deny)], domain);

        Assert.True(Has(report, "everyone-broad-create-deny"));
        Assert.Equal(AuditSeverity.Risk, report.Findings.Single(f => f.RuleId == "everyone-broad-create-deny").Severity);
    }

    /// <summary>
    /// A narrowly-scoped Deny is ordinary configuration, not a broad risk — the rule must key on the
    /// root-plus-descendants shape rather than on Deny alone, or every filter entry becomes a finding.
    /// </summary>
    /// <param name="domain">The create-filter domain.</param>
    /// <param name="verb">Its create verb.</param>
    [Theory]
    [MemberData(nameof(CreateFilterDomains))]
    public void Analyze_CreateFilter_NarrowlyScopedDeny_IsNotABroadRisk(AuditDomain domain, string verb)
    {
        var entry = new AdvancedPermissionEntry(
            Guid.NewGuid(), Guid.NewGuid(), "editors", verb, PermissionState.Deny, PermissionScope.ThisNodeOnly);

        Assert.False(Has(_sut.Analyze([entry], domain), "everyone-broad-create-deny"));
    }

    /// <summary>
    /// The new rule is create-filter-only. A broad All Users Deny on a NODE permission is not reported by
    /// it — a Deny there is the normal way to lock something down, and the node domains already have
    /// their own rule set.
    /// </summary>
    /// <param name="domain">A node-permission domain.</param>
    /// <param name="verb">Its delete verb.</param>
    [Theory]
    [InlineData(AuditDomain.Content, "Umb.Document.Delete")]
    [InlineData(AuditDomain.Library, "Umb.Element.Delete")]
    public void Analyze_NodePermissions_BroadDeny_DoesNotFireTheCreateFilterRule(AuditDomain domain, string verb) =>
        Assert.False(Has(_sut.Analyze([EveryoneRoot(verb, PermissionState.Deny)], domain), "everyone-broad-create-deny"));

    /// <summary>The node domains keep their existing broad-write rule unchanged.</summary>
    /// <param name="domain">A node-permission domain.</param>
    /// <param name="readVerb">The domain's read verb, which is excluded.</param>
    /// <param name="writeVerb">A write verb, which is not.</param>
    [Theory]
    [InlineData(AuditDomain.Content, "Umb.Document.Read", "Umb.Document.Delete")]
    [InlineData(AuditDomain.Library, "Umb.Element.Read", "Umb.Element.Delete")]
    public void Analyze_NodePermissions_BroadWriteRuleUnchanged(AuditDomain domain, string readVerb, string writeVerb)
    {
        Assert.False(Has(_sut.Analyze([EveryoneRoot(readVerb, PermissionState.Allow)], domain), "everyone-broad-write"));
        Assert.True(Has(_sut.Analyze([EveryoneRoot(writeVerb, PermissionState.Allow)], domain), "everyone-broad-write"));
    }

    /// <summary>
    /// The domain-independent rules still apply to create filters: a Priority Override is still worth
    /// surfacing, and an Allow/Deny clash on the same node for the same group is still a conflict.
    /// </summary>
    /// <param name="domain">The create-filter domain.</param>
    /// <param name="verb">Its create verb.</param>
    [Theory]
    [MemberData(nameof(CreateFilterDomains))]
    public void Analyze_CreateFilter_DomainIndependentRulesStillFire(AuditDomain domain, string verb)
    {
        var node = Guid.NewGuid();
        var report = _sut.Analyze(
            [
                new(Guid.NewGuid(), node, "editors", verb, PermissionState.Allow, PermissionScope.ThisNodeOnly, IsPriorityOverride: true),
                new(Guid.NewGuid(), node, "editors", verb, PermissionState.Deny, PermissionScope.ThisNodeOnly),
            ],
            domain);

        Assert.True(Has(report, "priority-override"));
        Assert.True(Has(report, "allow-deny-conflict"));
    }

    /// <summary>Omitting the domain still means the content tree, so existing callers are unaffected.</summary>
    [Fact]
    public void Analyze_DomainOmitted_StillDefaultsToContent()
    {
        Assert.True(Has(
            _sut.Analyze([EveryoneRoot(AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow)]),
            "everyone-broad-write"));
    }
}
