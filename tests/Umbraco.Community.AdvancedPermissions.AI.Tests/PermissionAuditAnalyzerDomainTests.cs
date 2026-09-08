using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for the <see cref="AuditDomain"/> awareness added to
/// <see cref="PermissionAuditAnalyzer"/> in the v18 update.
/// </summary>
/// <remarks>
/// <para>
/// These tests cover the two <b>node-permission</b> domains. Most rules are shared across every domain —
/// the analyzer reasons over <see cref="AdvancedPermissionEntry"/>, and the Library's element table has
/// the identical shape — but the <c>everyone-broad-write</c> rule excludes the domain's <b>read</b> verb,
/// because "everyone can read everything" is a normal baseline rather than a risk. Hard-coding
/// <see cref="AdvancedPermissionsConstants.VerbRead"/> would make the rule mis-fire on the Library, where
/// the read verb is <see cref="AdvancedPermissionsConstants.VerbElementRead"/>: a perfectly ordinary
/// Library read baseline would be reported to an administrator as a site-wide write risk.
/// </para>
/// <para>
/// The create-filter domains default the other way round and so need the rule pointed in the opposite
/// direction; that is covered by <see cref="PermissionAuditAnalyzerCreateFilterTests"/>.
/// </para>
/// <para>
/// Being a single-method pure function, the analyzer takes a plain domain argument rather than the
/// <c>For(domain)</c> binding used for the nine-method presenter.
/// </para>
/// </remarks>
public sealed class PermissionAuditAnalyzerDomainTests
{
    /// <summary>The system under test.</summary>
    private readonly IPermissionAuditAnalyzer _sut = new PermissionAuditAnalyzer();

    /// <summary>Builds a stored entry for the All Users group at the virtual root, scoped to the whole tree.</summary>
    /// <param name="verb">The verb the entry controls.</param>
    /// <returns>An Allow entry for <c>$everyone</c> at the virtual root, scoped node-and-descendants.</returns>
    private static AdvancedPermissionEntry EveryoneRootAllow(string verb) =>
        new(
            Guid.NewGuid(),
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            verb,
            PermissionState.Allow,
            PermissionScope.ThisNodeAndDescendants);

    /// <summary>Whether the report contains the broad-write risk finding.</summary>
    /// <param name="report">The report to inspect.</param>
    /// <returns><see langword="true"/> when the broad-write rule fired.</returns>
    private static bool HasBroadWriteFinding(AuditReport report) =>
        report.Findings.Any(f => f.RuleId == "everyone-broad-write");

    /// <summary>Reading is the expected content baseline for All Users, so it is never a risk.</summary>
    [Fact]
    public void Analyze_Content_EveryoneReadAtRoot_IsNotFlagged() =>
        Assert.False(HasBroadWriteFinding(
            _sut.Analyze([EveryoneRootAllow(AdvancedPermissionsConstants.VerbRead)], AuditDomain.Content)));

    /// <summary>A write verb allowed for All Users across the whole content tree is a risk.</summary>
    [Fact]
    public void Analyze_Content_EveryoneDeleteAtRoot_IsFlagged() =>
        Assert.True(HasBroadWriteFinding(
            _sut.Analyze([EveryoneRootAllow(AdvancedPermissionsConstants.VerbDelete)], AuditDomain.Content)));

    /// <summary>
    /// The actual bug this domain argument fixes: an ordinary Library read baseline must not be reported
    /// as a site-wide write risk just because the Library's read verb differs from the document one.
    /// </summary>
    [Fact]
    public void Analyze_Library_EveryoneElementReadAtRoot_IsNotFlagged() =>
        Assert.False(HasBroadWriteFinding(
            _sut.Analyze([EveryoneRootAllow(AdvancedPermissionsConstants.VerbElementRead)], AuditDomain.Library)));

    /// <summary>A Library write verb allowed for All Users across the whole tree is still a risk.</summary>
    [Fact]
    public void Analyze_Library_EveryoneElementDeleteAtRoot_IsFlagged() =>
        Assert.True(HasBroadWriteFinding(
            _sut.Analyze([EveryoneRootAllow(AdvancedPermissionsConstants.VerbElementDelete)], AuditDomain.Library)));

    /// <summary>
    /// The document read verb carries no special meaning in the Library domain, so it is judged on its
    /// merits — proving the exclusion tracks the domain rather than accepting either verb in both.
    /// </summary>
    [Fact]
    public void Analyze_Library_DocumentReadVerb_IsNotTreatedAsTheLibraryBaseline() =>
        Assert.True(HasBroadWriteFinding(
            _sut.Analyze([EveryoneRootAllow(AdvancedPermissionsConstants.VerbRead)], AuditDomain.Library)));

    /// <summary>Omitting the domain keeps the v17 content behaviour, so existing callers are unaffected.</summary>
    [Fact]
    public void Analyze_DomainOmitted_DefaultsToContent()
    {
        Assert.False(HasBroadWriteFinding(_sut.Analyze([EveryoneRootAllow(AdvancedPermissionsConstants.VerbRead)])));
        Assert.True(HasBroadWriteFinding(_sut.Analyze([EveryoneRootAllow(AdvancedPermissionsConstants.VerbDelete)])));
    }

    /// <summary>
    /// The domain-independent rules keep working in the Library: a priority override is still surfaced,
    /// and an Allow/Deny clash on one item is still a conflict.
    /// </summary>
    [Fact]
    public void Analyze_Library_DomainIndependentRulesStillFire()
    {
        var item = Guid.NewGuid();
        var report = _sut.Analyze(
            [
                new(Guid.NewGuid(), item, "editors", AdvancedPermissionsConstants.VerbElementUpdate,
                    PermissionState.Allow, PermissionScope.ThisNodeOnly, IsPriorityOverride: true),
                new(Guid.NewGuid(), item, "editors", AdvancedPermissionsConstants.VerbElementDelete,
                    PermissionState.Allow, PermissionScope.ThisNodeOnly),
                new(Guid.NewGuid(), item, "editors", AdvancedPermissionsConstants.VerbElementDelete,
                    PermissionState.Deny, PermissionScope.ThisNodeOnly),
            ],
            AuditDomain.Library);

        Assert.Contains(report.Findings, f => f.RuleId == "priority-override");
        Assert.Contains(report.Findings, f => f.RuleId == "allow-deny-conflict");
    }
}
