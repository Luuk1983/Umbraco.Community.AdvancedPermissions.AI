using NSubstitute;
using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for auditing the <b>create-filter</b> domain through <see cref="AuditPermissionsTool"/>:
/// document-type Insert Options.
/// </summary>
/// <remarks>
/// These entries live in their own store, keyed on (node, user group, <b>content type</b>, verb), and
/// that extra key is what the tests here are mostly about. Two of the behaviours they pin were outright
/// defects on the first pass:
/// <list type="bullet">
/// <item><description>
/// <b>Incomplete <c>all</c> sweep.</b> Discovering which content types to load from the All Users
/// entries alone misses any type configured only for a named group — a silent under-report, and the
/// worst kind in something an administrator reads as a risk report.
/// </description></item>
/// <item><description>
/// <b>False conflicts.</b> The analyzer reasons over (node, group, verb). Two <i>different</i> content
/// types with an Allow and a Deny at the same node for the same group share all three, so analyzing them
/// together reports an Allow/Deny conflict that does not exist.
/// </description></item>
/// </list>
/// </remarks>
public sealed class AuditPermissionsCreateFilterTests
{
    /// <summary>The mocked content repository (unused by this domain).</summary>
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();


    /// <summary>The mocked create-filter repository — the store this domain reads.</summary>
    private readonly IDocTypePermissionRepository _docTypeRepository = Substitute.For<IDocTypePermissionRepository>();

    /// <summary>The real analyzer.</summary>
    private readonly IPermissionAuditAnalyzer _analyzer = new PermissionAuditAnalyzer();

    /// <summary>The mocked user group service, used to enumerate every group for the whole-config sweep.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service, backing content-type display names.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();


    /// <summary>Exposes an "Editors" group so roles can be enumerated and named.</summary>
    public AuditPermissionsCreateFilterTests()
    {
        var editors = Substitute.For<IUserGroup>();
        editors.Alias.Returns("editors");
        editors.Name.Returns("Editors");
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(1, new[] { editors }));
    }

    /// <summary>Builds the tool under test with a real presenter and analyzer.</summary>
    /// <returns>A fresh tool instance.</returns>
    private AuditPermissionsTool CreateTool() =>
        new(
            _repository,
            _analyzer,
            new PermissionPresenter(_userGroupService, _entityService, _contentTypeService),
            _entityService,
            TestGuards.AllowAll(),
            _userGroupService,
            _docTypeRepository);

    /// <summary>Executes the tool through its public interface.</summary>
    /// <param name="args">The arguments to run with.</param>
    /// <returns>The tool result.</returns>
    private Task<object> RunAsync(AuditPermissionsArgs args) =>
        ((IAITool)CreateTool()).ExecuteAsync(args, CancellationToken.None);

    /// <summary>Builds a create-filter entry.</summary>
    /// <param name="nodeKey">The node the entry applies to.</param>
    /// <param name="contentTypeKey">The content type it controls.</param>
    /// <param name="role">The role alias.</param>
    /// <param name="verb">The create verb.</param>
    /// <param name="state">Allow or Deny.</param>
    /// <param name="scope">The entry's scope.</param>
    /// <param name="priority">Whether it is a priority override.</param>
    /// <returns>The entry.</returns>
    private static DocTypePermissionEntry Entry(
        Guid nodeKey,
        Guid contentTypeKey,
        string role,
        string verb,
        PermissionState state,
        PermissionScope scope = PermissionScope.ThisNodeAndDescendants,
        bool priority = false) =>
        new(Guid.NewGuid(), nodeKey, contentTypeKey, role, verb, state, scope, priority);

    /// <summary>Stubs the create-filter store's by-role read for one role alias.</summary>
    /// <param name="role">The role alias.</param>
    /// <param name="entries">The entries it should return.</param>
    private void SetupRoleEntries(string role, params DocTypePermissionEntry[] entries) =>
        _docTypeRepository.GetByRoleAsync(role, Arg.Any<CancellationToken>()).Returns(entries);

    /// <summary>Names a content type so findings can carry its display name.</summary>
    /// <param name="key">The content type key.</param>
    /// <param name="name">Its display name.</param>
    private void SetupContentTypeName(Guid key, string name)
    {
        var ct = Substitute.For<IContentType>();
        ct.Key.Returns(key);
        ct.Name.Returns(name);
        _contentTypeService.Get(key).Returns(ct);
    }

    /// <summary>Makes the given nodes the whole live content tree, as the whole-configuration sweep reads it.</summary>
    /// <param name="keys">The live document keys.</param>
    private void SetupLiveNodes(params Guid[] keys) =>
        _entityService
            .GetPagedDescendants(
                UmbracoObjectTypes.Document,
                Arg.Any<long>(),
                Arg.Any<int>(),
                out Arg.Any<long>(),
                Arg.Any<IQuery<IUmbracoEntity>?>(),
                Arg.Any<Ordering?>(),
                Arg.Any<bool>())
            .Returns(callInfo =>
            {
                callInfo[3] = (long)keys.Length;
                return keys.Select(k =>
                {
                    var entity = Substitute.For<IEntitySlim>();
                    entity.Key.Returns(k);
                    return entity;
                }).ToArray();
            });

    /// <summary>
    /// The whole-configuration sweep covers the same nodes in both domains: every live node plus the
    /// Default permissions row. An Insert Options entry left behind on a deleted node no longer decides
    /// anything, and reporting it would point an administrator at a node they cannot find.
    /// </summary>
    [Fact]
    public async Task DocumentTypes_AllScope_SkipsEntriesOnDeletedNodes()
    {
        const string verb = AdvancedPermissionsConstants.VerbCreateOfType;
        var type = Guid.NewGuid();
        var live = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        SetupContentTypeName(type, "Article");
        SetupLiveNodes(live);
        SetupRoleEntries(
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, type, AdvancedPermissionsConstants.EveryoneRoleAlias, verb, PermissionState.Deny));
        SetupRoleEntries(
            "editors",
            Entry(live, type, "editors", verb, PermissionState.Allow, PermissionScope.ThisNodeOnly, priority: true),
            Entry(deleted, type, "editors", verb, PermissionState.Allow, PermissionScope.ThisNodeOnly, priority: true));

        var result = await RunAsync(new AuditPermissionsArgs(AuditScope.All, Domain: AuditDomain.DocumentTypes));

        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Equal(2, report.EntriesAnalyzed);
        Assert.Single(report.Findings, f => f.RuleId == "priority-override");
        Assert.Single(report.Findings, f => f.RuleId == "everyone-broad-create-deny");
    }

    /// <summary>The document-type domain reads the create-filter store and audits its entries.</summary>
    [Fact]
    public async Task DocumentTypes_UserGroupScope_AuditsInsertOptionEntries()
    {
        var type = Guid.NewGuid();
        SetupContentTypeName(type, "Article");
        SetupRoleEntries(
            "editors",
            Entry(Guid.NewGuid(), type, "editors", AdvancedPermissionsConstants.VerbCreateOfType,
                PermissionState.Allow, PermissionScope.ThisNodeOnly, priority: true));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, "editors", Domain: AuditDomain.DocumentTypes));

        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Equal(1, report.EntriesAnalyzed);
        Assert.Contains(report.Findings, f => f.RuleId == "priority-override");
        await _docTypeRepository.Received().GetByRoleAsync("editors", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Document-type Insert Options are per node, so the subtree scope is valid and must not be refused.
    /// </summary>
    [Fact]
    public async Task DocumentTypes_SubtreeScope_IsSupported()
    {
        var inside = Guid.NewGuid();
        var outside = Guid.NewGuid();
        var type = Guid.NewGuid();
        SetupContentTypeName(type, "Article");

        SetupRoleEntries(
            "editors",
            Entry(inside, type, "editors", AdvancedPermissionsConstants.VerbCreateOfType,
                PermissionState.Allow, PermissionScope.ThisNodeOnly, priority: true),
            Entry(outside, type, "editors", AdvancedPermissionsConstants.VerbCreateOfType,
                PermissionState.Allow, PermissionScope.ThisNodeOnly, priority: true));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.Subtree, NodeKey: inside, Domain: AuditDomain.DocumentTypes));

        // Only the entry stored inside the subtree is analyzed; the one elsewhere is filtered out.
        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Equal(1, report.EntriesAnalyzed);
    }

    /// <summary>
    /// THE COMPLETENESS FIX. The whole-configuration sweep must enumerate every user group, not infer the
    /// content types from the All Users entries. Here All Users has nothing stored and Editors has a
    /// risky entry — the earlier implementation found nothing at all.
    /// </summary>
    [Fact]
    public async Task DocumentTypes_AllScope_FindsEntriesBelongingOnlyToANamedGroup()
    {
        const AuditDomain domain = AuditDomain.DocumentTypes;
        const string verb = AdvancedPermissionsConstants.VerbCreateOfType;
        var type = Guid.NewGuid();
        SetupContentTypeName(type, "Article");
        var node = Guid.NewGuid();
        SetupLiveNodes(node);
        SetupRoleEntries(AdvancedPermissionsConstants.EveryoneRoleAlias);
        SetupRoleEntries(
            "editors",
            Entry(node, type, "editors", verb, PermissionState.Allow, PermissionScope.ThisNodeOnly, priority: true));

        var result = await RunAsync(new AuditPermissionsArgs(AuditScope.All, Domain: domain));

        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Equal(1, report.EntriesAnalyzed);
        Assert.Contains(report.Findings, f => f.RuleId == "priority-override");
    }

    /// <summary>
    /// THE FALSE-CONFLICT FIX. Two different content types with an Allow and a Deny at the same node for
    /// the same group are not in conflict — they are independent decisions about different types. The
    /// audit must analyze each content type separately.
    /// </summary>
    [Fact]
    public async Task DifferentContentTypesAtTheSameNode_AreNotReportedAsAConflict()
    {
        var node = Guid.NewGuid();
        var article = Guid.NewGuid();
        var news = Guid.NewGuid();
        SetupContentTypeName(article, "Article");
        SetupContentTypeName(news, "News");
        SetupRoleEntries(
            "editors",
            Entry(node, article, "editors", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow),
            Entry(node, news, "editors", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, "editors", Domain: AuditDomain.DocumentTypes));

        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Equal(2, report.EntriesAnalyzed);
        Assert.DoesNotContain(report.Findings, f => f.RuleId == "allow-deny-conflict");
    }

    /// <summary>
    /// A genuine conflict — the SAME content type with both an Allow and a Deny at one node for one group
    /// — is still reported, so the per-type split does not blind the rule.
    /// </summary>
    [Fact]
    public async Task SameContentTypeWithAllowAndDeny_IsStillReportedAsAConflict()
    {
        var node = Guid.NewGuid();
        var article = Guid.NewGuid();
        SetupContentTypeName(article, "Article");
        SetupRoleEntries(
            "editors",
            Entry(node, article, "editors", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow),
            Entry(node, article, "editors", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, "editors", Domain: AuditDomain.DocumentTypes));

        Assert.Contains(
            Assert.IsType<FriendlyAuditReport>(result).Findings,
            f => f.RuleId == "allow-deny-conflict");
    }

    /// <summary>
    /// A create-filter finding must name the content type it concerns. Without it the reader is told a
    /// group has a risky create entry somewhere, with no way to tell which type — the audit would be
    /// technically correct and practically useless.
    /// </summary>
    [Fact]
    public async Task CreateFilterFinding_NamesTheContentType()
    {
        var type = Guid.NewGuid();
        SetupContentTypeName(type, "Article");
        SetupRoleEntries(
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, type,
                AdvancedPermissionsConstants.EveryoneRoleAlias,
                AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, AdvancedPermissionsConstants.EveryoneRoleAlias,
            Domain: AuditDomain.DocumentTypes));

        var finding = Assert.Single(Assert.IsType<FriendlyAuditReport>(result).Findings);
        Assert.Equal("everyone-broad-create-deny", finding.RuleId);
        Assert.Equal("Article", finding.ContentType);
        Assert.Contains("Article", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>A create-filter audit must never leak a raw verb, alias, or GUID.</summary>
    [Fact]
    public async Task CreateFilterFindings_NeverLeakRawIdentifiers()
    {
        var type = Guid.NewGuid();
        SetupContentTypeName(type, "Article");
        SetupRoleEntries(
            "editors",
            Entry(Guid.NewGuid(), type, "editors", AdvancedPermissionsConstants.VerbCreateOfType,
                PermissionState.Allow, PermissionScope.ThisNodeOnly, priority: true));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, "editors", Domain: AuditDomain.DocumentTypes));

        var json = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.DoesNotContain("Umb.Document.", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"editors\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(type.ToString(), json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Only Insert Options entries are audited: an entry in the store with any other verb is not a
    /// document-type create decision and is left out, so it can neither raise nor hide a finding.
    /// </summary>
    [Fact]
    public async Task DocumentTypes_IgnoresEntriesWithAnotherVerb()
    {
        var type = Guid.NewGuid();
        SetupContentTypeName(type, "Article");
        SetupRoleEntries(
            "editors",
            Entry(Guid.NewGuid(), type, "editors", AdvancedPermissionsConstants.VerbCreateOfType,
                PermissionState.Allow, PermissionScope.ThisNodeOnly, priority: true),
            Entry(Guid.NewGuid(), type, "editors", AdvancedPermissionsConstants.VerbCreate,
                PermissionState.Allow, PermissionScope.ThisNodeOnly, priority: true));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, "editors", Domain: AuditDomain.DocumentTypes));

        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Equal(1, report.EntriesAnalyzed);
        Assert.Single(report.Findings);
    }

    /// <summary>The content domain (the default) never reads the Insert Options store.</summary>
    [Fact]
    public async Task ContentDomain_ReadsNoDocTypeEntries()
    {
        _repository.GetByRoleAsync("editors", Arg.Any<CancellationToken>()).Returns(new List<AdvancedPermissionEntry>());

        await RunAsync(new AuditPermissionsArgs(AuditScope.UserGroup, "editors"));

        Assert.Empty(_docTypeRepository.ReceivedCalls());
    }
}
