using NSubstitute;
using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for the <see cref="AuditDomain"/> argument added to <see cref="AuditPermissionsTool"/> in
/// the v18 update.
/// </summary>
/// <remarks>
/// <para>
/// The audit takes a domain argument where the explain tools got a whole sibling tool, because these
/// paths differ only in which table to read — the analyzer's rules are domain-agnostic. These tests pin
/// that: the right store is read, the wrong one is never touched, and the domain reaches the analyzer
/// (which needs it so its broad-write rule excludes the right read verb).
/// </para>
/// <para>
/// The element-type domain gets extra attention because it is the trap: those entries share one store
/// with the document-type entries and are told apart <b>only by the verb</b>, so a missing filter would
/// silently report document-type findings as Library ones.
/// </para>
/// </remarks>
public sealed class AuditPermissionsDomainTests
{
    /// <summary>The mocked content repository.</summary>
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();

    /// <summary>The mocked Library element repository.</summary>
    private readonly IElementPermissionRepository _elementRepository = Substitute.For<IElementPermissionRepository>();

    /// <summary>The mocked create-filter repository (holds document-type AND element-type entries).</summary>
    private readonly IDocTypePermissionRepository _docTypeRepository = Substitute.For<IDocTypePermissionRepository>();

    /// <summary>The real analyzer — its domain handling is the thing under test downstream.</summary>
    private readonly IPermissionAuditAnalyzer _analyzer = new PermissionAuditAnalyzer();

    /// <summary>The mocked user group service backing the real presenter.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service backing the real presenter.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service backing the real presenter.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

    /// <summary>The mocked Library tree reader.</summary>
    private readonly IElementTreeResolver _treeResolver = Substitute.For<IElementTreeResolver>();

    /// <summary>
    /// Exposes an "Editors" group so the presenter can resolve a non-All-Users alias to a display name.
    /// The All Users alias short-circuits inside the presenter, so only tests using a real group alias
    /// need this.
    /// </summary>
    public AuditPermissionsDomainTests()
    {
        var editors = Substitute.For<IUserGroup>();
        editors.Alias.Returns("editors");
        editors.Name.Returns("Editors");
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(1, new[] { editors }));
    }

    /// <summary>Builds the tool under test with a real presenter and a real analyzer.</summary>
    /// <returns>A fresh tool instance.</returns>
    private AuditPermissionsTool CreateTool() =>
        new(
            _repository,
            _elementRepository,
            _docTypeRepository,
            _analyzer,
            new PermissionPresenter(_userGroupService, _entityService, _contentTypeService),
            _entityService,
            _treeResolver,
            _userGroupService);

    /// <summary>Executes the tool through its public interface.</summary>
    /// <param name="args">The arguments to run with.</param>
    /// <returns>The tool result.</returns>
    private Task<object> RunAsync(AuditPermissionsArgs args) =>
        ((IAITool)CreateTool()).ExecuteAsync(args, CancellationToken.None);

    /// <summary>Builds an All Users, virtual-root, whole-tree Allow entry for the given verb.</summary>
    /// <param name="verb">The verb the entry controls.</param>
    /// <returns>The entry.</returns>
    private static AdvancedPermissionEntry EveryoneRootAllow(string verb) =>
        new(
            Guid.NewGuid(),
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            verb,
            PermissionState.Allow,
            PermissionScope.ThisNodeAndDescendants);

    /// <summary>Stubs a node repository's by-role read.</summary>
    /// <param name="repository">The repository to stub.</param>
    /// <param name="entries">The entries it should return.</param>
    private static void SetupByRole(INodePermissionRepository repository, params AdvancedPermissionEntry[] entries) =>
        repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(entries);

    /// <summary>The Library domain reads the element store, and never the content one.</summary>
    [Fact]
    public async Task Library_ReadsTheElementRepositoryOnly()
    {
        SetupByRole(_elementRepository, EveryoneRootAllow(AdvancedPermissionsConstants.VerbElementDelete));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, AdvancedPermissionsConstants.EveryoneRoleAlias, Domain: AuditDomain.Library));

        Assert.IsType<FriendlyAuditReport>(result);
        await _elementRepository.Received().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>The content domain reads the content store, and never the element one.</summary>
    [Fact]
    public async Task Content_ReadsTheContentRepositoryOnly()
    {
        SetupByRole(_repository, EveryoneRootAllow(AdvancedPermissionsConstants.VerbDelete));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, AdvancedPermissionsConstants.EveryoneRoleAlias, Domain: AuditDomain.Content));

        Assert.IsType<FriendlyAuditReport>(result);
        await _repository.Received().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _elementRepository.DidNotReceive().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Omitting the domain keeps the v17 content behaviour.</summary>
    [Fact]
    public async Task DomainOmitted_DefaultsToContent()
    {
        SetupByRole(_repository, EveryoneRootAllow(AdvancedPermissionsConstants.VerbDelete));

        await RunAsync(new AuditPermissionsArgs(AuditScope.UserGroup, AdvancedPermissionsConstants.EveryoneRoleAlias));

        await _repository.Received().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _elementRepository.DidNotReceive().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The domain must reach the analyzer, not just the repository choice. An ordinary Library read
    /// baseline must not be reported as a site-wide write risk — the bug the analyzer's domain argument
    /// exists to prevent, asserted here end-to-end through the tool.
    /// </summary>
    [Fact]
    public async Task Library_ElementReadBaseline_IsNotReportedAsARisk()
    {
        SetupByRole(_elementRepository, EveryoneRootAllow(AdvancedPermissionsConstants.VerbElementRead));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, AdvancedPermissionsConstants.EveryoneRoleAlias, Domain: AuditDomain.Library));

        Assert.Empty(Assert.IsType<FriendlyAuditReport>(result).Findings);
    }

    /// <summary>A genuine Library write risk is still reported.</summary>
    [Fact]
    public async Task Library_ElementWriteAcrossTheTree_IsReportedAsARisk()
    {
        SetupByRole(_elementRepository, EveryoneRootAllow(AdvancedPermissionsConstants.VerbElementDelete));

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, AdvancedPermissionsConstants.EveryoneRoleAlias, Domain: AuditDomain.Library));

        Assert.NotEmpty(Assert.IsType<FriendlyAuditReport>(result).Findings);
    }

    /// <summary>
    /// Element-type permissions are set once for the whole Library, so a subtree audit is refused with an
    /// explanation rather than silently returning nothing — which the copilot would read as "all clear".
    /// </summary>
    [Fact]
    public async Task LibraryElementTypes_SubtreeScope_IsRefusedWithAnExplanation()
    {
        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.Subtree, NodeKey: Guid.NewGuid(), Domain: AuditDomain.LibraryElementTypes));

        var error = Assert.IsType<AccessError>(result).Error;
        Assert.Contains("whole Library", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user-group", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// THE ELEMENT-TYPE TRAP. Element-type and document-type create entries live in ONE store and differ
    /// only by verb, so the audit must keep only the element-type verb. Without the filter, a
    /// document-type override would be reported as a Library element-type finding.
    /// </summary>
    [Fact]
    public async Task LibraryElementTypes_FiltersOutDocumentTypeEntriesFromTheSharedStore()
    {
        _docTypeRepository
            .GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([
                // A Library element-type entry — must be audited.
                new DocTypePermissionEntry(
                    Guid.NewGuid(), AdvancedPermissionsConstants.VirtualRootNodeKey, Guid.NewGuid(), "editors",
                    AdvancedPermissionsConstants.VerbElementCreateOfType, PermissionState.Deny,
                    PermissionScope.ThisNodeAndDescendants, IsPriorityOverride: true),

                // A DOCUMENT-type entry in the same store — must be ignored by this domain.
                new DocTypePermissionEntry(
                    Guid.NewGuid(), AdvancedPermissionsConstants.VirtualRootNodeKey, Guid.NewGuid(), "editors",
                    AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny,
                    PermissionScope.ThisNodeAndDescendants, IsPriorityOverride: true),
            ]);

        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, "editors", Domain: AuditDomain.LibraryElementTypes));

        var report = Assert.IsType<FriendlyAuditReport>(result);

        // Both entries are priority overrides, so an unfiltered read would analyze two and find two.
        Assert.Equal(1, report.EntriesAnalyzed);
        Assert.Single(report.Findings);
    }

    /// <summary>
    /// The element-type domain reads the create-filter store, and neither node store — those hold a
    /// different kind of permission entirely.
    /// </summary>
    [Fact]
    public async Task LibraryElementTypes_ReadsTheCreateFilterStoreOnly()
    {
        _docTypeRepository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);

        await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, "editors", Domain: AuditDomain.LibraryElementTypes));

        await _docTypeRepository.Received().GetByRoleAsync("editors", Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _elementRepository.DidNotReceive().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A user-group audit still requires the alias in the element-type domain.</summary>
    [Fact]
    public async Task LibraryElementTypes_UserGroupScopeWithoutAlias_ReturnsAFriendlyError()
    {
        var result = await RunAsync(new AuditPermissionsArgs(
            AuditScope.UserGroup, Domain: AuditDomain.LibraryElementTypes));

        Assert.Contains(
            "userGroupAlias",
            Assert.IsType<AccessError>(result).Error,
            StringComparison.OrdinalIgnoreCase);
    }
}
