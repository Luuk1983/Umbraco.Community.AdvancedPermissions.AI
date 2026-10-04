using System.Text.Json;
using NSubstitute;
using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="ExplainLibraryAccessTool"/>, the Library sibling of
/// <see cref="ExplainAccessTool"/>.
/// </summary>
/// <remarks>
/// Every scenario runs through the public
/// <see cref="IAITool.ExecuteAsync(object?, CancellationToken)"/> entry point so the real base-class
/// plumbing is exercised, and a <b>real</b> <see cref="PermissionPresenter"/> wraps mocked Umbraco
/// services so the friendly projection is exercised end-to-end and the no-raw-identifiers guarantee can
/// actually be asserted.
/// </remarks>
public sealed class ExplainLibraryAccessToolTests
{
    /// <summary>The mocked Library node permission service.</summary>
    private readonly IElementNodePermissionService _permissions = Substitute.For<IElementNodePermissionService>();

    /// <summary>The mocked Library tree reader.</summary>
    private readonly IElementTreeResolver _treeResolver = Substitute.For<IElementTreeResolver>();

    /// <summary>The mocked user group service.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service backing the real presenter.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service backing the real presenter.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

    /// <summary>The mocked create-filter service used for the element-type aspect.</summary>
    private readonly IDocTypePermissionService _docTypePermissions = Substitute.For<IDocTypePermissionService>();

    /// <summary>The mocked Library element-type provider.</summary>
    private readonly ILibraryElementTypeProvider _elementTypes = Substitute.For<ILibraryElementTypeProvider>();

    /// <summary>The mocked backoffice security accessor.</summary>
    private readonly IBackOfficeSecurityAccessor _securityAccessor = Substitute.For<IBackOfficeSecurityAccessor>();

    /// <summary>The mocked remediation service.</summary>
    private readonly IPermissionRemediationService _remediation = Substitute.For<IPermissionRemediationService>();

    /// <summary>The mocked user service.</summary>
    private readonly IUserService _userService = Substitute.For<IUserService>();

    /// <summary>The Library item or folder under test.</summary>
    private readonly Guid _node = Guid.NewGuid();

    /// <summary>The resolved path for <see cref="_node"/>.</summary>
    private readonly Guid[] _path;

    /// <summary>Sets up a single "Editors" group and a one-node path.</summary>
    public ExplainLibraryAccessToolTests()
    {
        _path = [_node];
        _treeResolver.GetPathFromRoot(_node).Returns(_path);

        var editors = Group("editors", "Editors");
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(1, new[] { editors }));
    }

    /// <summary>Builds the tool under test over the current mocks, with a real presenter.</summary>
    /// <returns>A fresh tool instance.</returns>
    private ExplainLibraryAccessTool CreateTool() =>
        new(
            _permissions,
            _treeResolver,
            new PermissionPresenter(_userGroupService, _entityService, _contentTypeService),
            _userGroupService,
            _securityAccessor,
            _docTypePermissions,
            _elementTypes,
            _remediation,
            _userService);

    /// <summary>Executes the tool through its public interface.</summary>
    /// <param name="args">The arguments to run with.</param>
    /// <returns>The tool result.</returns>
    private Task<object> RunAsync(ExplainLibraryAccessArgs args) =>
        ((IAITool)CreateTool()).ExecuteAsync(args, CancellationToken.None);

    /// <summary>Builds a mocked user group.</summary>
    /// <param name="alias">The group alias.</param>
    /// <param name="name">The display name.</param>
    /// <returns>A substituted user group.</returns>
    private static IUserGroup Group(string alias, string name)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(name);
        return group;
    }

    /// <summary>Builds an effective permission with one reasoning line.</summary>
    /// <param name="verb">The verb resolved.</param>
    /// <param name="isAllowed">Whether it resolved to allowed.</param>
    /// <param name="role">The contributing role alias.</param>
    /// <param name="sourceNodeKey">The node the deciding entry sits on.</param>
    /// <returns>An effective permission.</returns>
    private static EffectivePermission Perm(string verb, bool isAllowed, string role, Guid sourceNodeKey) =>
        new(
            verb,
            isAllowed,
            IsExplicit: true,
            Reasoning:
            [
                new PermissionReasoning(
                    role,
                    isAllowed ? PermissionState.Allow : PermissionState.Deny,
                    IsExplicit: true,
                    sourceNodeKey,
                    PermissionScope.ThisNodeOnly,
                    IsFromGroupDefault: false),
            ]);

    /// <summary>Names the node under test in the Library tree, as an element.</summary>
    /// <param name="name">The name the item should report.</param>
    private void SetupLibraryItemName(string name)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Name.Returns(name);
        _entityService.Get(_node, UmbracoObjectTypes.Element).Returns(entity);
    }

    /// <summary>Configures the current backoffice user.</summary>
    /// <param name="userKey">The user's key.</param>
    private void SetupCurrentUser(Guid userKey)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(userKey);
        var security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns(user);
        _securityAccessor.BackOfficeSecurity.Returns(security);
    }

    /// <summary>Configures a user with the "editors" group so remediation can build a role set.</summary>
    /// <param name="userKey">The user's key.</param>
    private void SetupUserGroups(Guid userKey)
    {
        var group = Substitute.For<IReadOnlyUserGroup>();
        group.Alias.Returns("editors");
        var user = Substitute.For<IUser>();
        user.Key.Returns(userKey);
        user.Groups.Returns(new[] { group });
        _userService.GetAsync(userKey).Returns(user);
    }

    /// <summary>Builds a mocked element type.</summary>
    /// <param name="key">The type's key.</param>
    /// <param name="name">The type's display name.</param>
    /// <returns>A substituted content type.</returns>
    private static IContentType ElementType(Guid key, string name)
    {
        var ct = Substitute.For<IContentType>();
        ct.Key.Returns(key);
        ct.Name.Returns(name);
        ct.Alias.Returns(name.ToLowerInvariant());
        ct.IsElement.Returns(true);
        ct.AllowedInLibrary.Returns(true);
        return ct;
    }

    // ── Node aspect ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A focused Library permission resolves through the LIBRARY service and is presented with the item's
    /// own name — proving the presenter was rebound to the Library domain rather than left on content.
    /// </summary>
    [Fact]
    public async Task NodeAspect_FocusedPermission_ResolvesViaLibraryServiceAndNamesTheItem()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        SetupLibraryItemName("Promo Banner");
        _permissions
            .ResolveAsync(userKey, _node, _path, AdvancedPermissionsConstants.VerbElementDelete, Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementDelete, false, "editors", _node));

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: AdvancedPermissionsConstants.VerbElementDelete));

        var verdict = Assert.IsType<AccessVerdict>(result);
        Assert.Equal("Delete", verdict.Permission);
        Assert.Equal("Denied", verdict.Result);
        Assert.Equal("Editors", Assert.Single(verdict.Reasons).UserGroup);
        Assert.Equal("Promo Banner", Assert.Single(verdict.Reasons).SetOn);
    }

    /// <summary>
    /// The Create permission has no meaning on a Library item, so it is reported "Not applicable" — and
    /// the resolver is never consulted, because relaying its perfectly ordinary Allow/Deny is the bug.
    /// </summary>
    [Fact]
    public async Task NodeAspect_CreateOnAnItem_IsNotApplicableAndNeverResolved()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Item);

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: AdvancedPermissionsConstants.VerbElementCreate));

        var verdict = Assert.IsType<AccessVerdict>(result);
        Assert.Equal("Not applicable", verdict.Result);
        Assert.Empty(verdict.Reasons);
        await _permissions.DidNotReceive().ResolveAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The item-only permissions do not apply to a folder itself — only to the items inside it — so they
    /// are reported "Not applicable" rather than denied.
    /// </summary>
    /// <param name="verb">An item-only Library verb.</param>
    [Theory]
    [InlineData("Umb.Element.Publish")]
    [InlineData("Umb.Element.Unpublish")]
    [InlineData("Umb.Element.Duplicate")]
    [InlineData("Umb.Element.Rollback")]
    public async Task NodeAspect_ItemOnlyPermissionOnAFolder_IsNotApplicable(string verb)
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Folder);

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: verb));

        Assert.Equal("Not applicable", Assert.IsType<AccessVerdict>(result).Result);
    }

    /// <summary>Create DOES apply to a folder, which can contain items — so it resolves normally.</summary>
    [Fact]
    public async Task NodeAspect_CreateOnAFolder_ResolvesNormally()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Folder);
        _permissions
            .ResolveAsync(userKey, _node, _path, AdvancedPermissionsConstants.VerbElementCreate, Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementCreate, true, "editors", _node));

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: AdvancedPermissionsConstants.VerbElementCreate));

        Assert.Equal("Allowed", Assert.IsType<AccessVerdict>(result).Result);
    }

    /// <summary>
    /// The all-permissions explanation substitutes "Not applicable" per permission, so one answer can
    /// correctly mix real verdicts with inapplicable ones.
    /// </summary>
    [Fact]
    public async Task NodeAspect_AllPermissionsOnAFolder_MixesVerdictsAndNotApplicable()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Folder);
        _permissions
            .ResolveAllAsync(userKey, _node, _path, null, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                [AdvancedPermissionsConstants.VerbElementRead] =
                    Perm(AdvancedPermissionsConstants.VerbElementRead, true, "editors", _node),
                [AdvancedPermissionsConstants.VerbElementPublish] =
                    Perm(AdvancedPermissionsConstants.VerbElementPublish, false, "editors", _node),
            });

        var result = await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.CurrentUser, _node));

        var explanation = Assert.IsType<AccessExplanation>(result);
        Assert.Equal("Allowed", explanation.Permissions.Single(p => p.Permission == "Read").Result);
        Assert.Equal("Not applicable", explanation.Permissions.Single(p => p.Permission == "Publish").Result);
    }

    /// <summary>The node aspect needs a node, and says so plainly rather than resolving nothing.</summary>
    [Fact]
    public async Task NodeAspect_WithoutNodeKey_ReturnsAFriendlyError()
    {
        var result = await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.CurrentUser));

        Assert.Contains("nodeKey", Assert.IsType<AccessError>(result).Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A user-group subject without an alias is an error, not a silent empty answer.</summary>
    [Fact]
    public async Task NodeAspect_UserGroupSubjectWithoutAlias_ReturnsAFriendlyError()
    {
        var result = await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.UserGroup, _node));

        Assert.Contains("userGroupAlias", Assert.IsType<AccessError>(result).Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A denied Library permission with suggestFix must simulate against the LIBRARY entries. Passing the
    /// content domain would confirm a fix that changes nothing, so the domain is asserted explicitly.
    /// </summary>
    [Fact]
    public async Task NodeAspect_SuggestFix_RemediatesInTheLibraryDomain()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        _permissions
            .ResolveAsync(userKey, _node, _path, AdvancedPermissionsConstants.VerbElementDelete, Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementDelete, false, "editors", _node));
        _remediation
            .SuggestAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<string>(), Arg.Any<PermissionState>(), Arg.Any<PermissionDomain>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node,
            Permission: AdvancedPermissionsConstants.VerbElementDelete,
            SuggestFix: true));

        await _remediation.Received().SuggestAsync(
            _node, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<string>>(),
            AdvancedPermissionsConstants.VerbElementDelete, PermissionState.Deny,
            PermissionDomain.Library, Arg.Any<CancellationToken>());
    }

    /// <summary>The all-user-groups roster names groups, and drops permissions that do not apply.</summary>
    [Fact]
    public async Task NodeAspect_AllUserGroups_BuildsARosterAndOmitsInapplicablePermissions()
    {
        SetupLibraryItemName("Promo Banner");
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Folder);
        _permissions
            .ResolveForRoleAsync(Arg.Any<string>(), _node, _path, null, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                [AdvancedPermissionsConstants.VerbElementRead] =
                    Perm(AdvancedPermissionsConstants.VerbElementRead, true, "editors", _node),
                [AdvancedPermissionsConstants.VerbElementRollback] =
                    Perm(AdvancedPermissionsConstants.VerbElementRollback, true, "editors", _node),
            });

        var result = await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.AllUserGroups, _node));

        var report = Assert.IsType<AccessRosterReport>(result);
        Assert.Equal("Promo Banner", report.Node);
        Assert.Contains(report.Permissions, r => r.Permission == "Read");
        Assert.DoesNotContain(report.Permissions, r => r.Permission == "Rollback");
    }

    // ── Element-type create aspect ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The element-type aspect needs NO node and must resolve with the ELEMENT create verb at the virtual
    /// root. Both matter: element-type and document-type entries share one store and are told apart only
    /// by the verb, so the document verb here would silently answer about document types.
    /// </summary>
    [Fact]
    public async Task ElementTypeCreate_ResolvesSectionWideWithTheElementCreateVerb()
    {
        var userKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        var bannerType = ElementType(typeKey, "Promo Banner");
        _contentTypeService.Get(typeKey).Returns(bannerType);
        _docTypePermissions
            .ResolveCreateForRolesAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<Guid>>(), typeKey,
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementCreateOfType, false, "editors", AdvancedPermissionsConstants.VirtualRootNodeKey));

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser,
            Aspect: LibraryAspect.ElementTypeCreate,
            ElementTypeKey: typeKey));

        var verdict = Assert.IsType<ElementTypeCreateVerdict>(result);
        Assert.Equal("Promo Banner", verdict.ElementType);
        Assert.Equal("Denied", verdict.Result);

        await _docTypePermissions.Received().ResolveCreateForRolesAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Is<IReadOnlyList<Guid>>(p => p.Count == 1 && p[0] == AdvancedPermissionsConstants.VirtualRootNodeKey),
            typeKey,
            AdvancedPermissionsConstants.VerbElementCreateOfType,
            Arg.Any<CancellationToken>());
        await _docTypePermissions.DidNotReceive().ResolveCreateForRolesAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<Guid>(),
            AdvancedPermissionsConstants.VerbCreateOfType, Arg.Any<CancellationToken>());
    }

    /// <summary>Without a focused type, every creatable element type is listed from the provider.</summary>
    [Fact]
    public async Task ElementTypeCreate_WithoutAFocusedType_ListsEveryLibraryElementType()
    {
        var userKey = Guid.NewGuid();
        var banner = Guid.NewGuid();
        var quote = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        var bannerType = ElementType(banner, "Promo Banner");
        var quoteType = ElementType(quote, "Pull Quote");
        _elementTypes.GetLibraryElementTypes().Returns([bannerType, quoteType]);
        _contentTypeService.Get(banner).Returns(bannerType);
        _contentTypeService.Get(quote).Returns(quoteType);
        _docTypePermissions
            .ResolveCreateForRolesAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<Guid>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementCreateOfType, true, "editors", AdvancedPermissionsConstants.VirtualRootNodeKey));

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, Aspect: LibraryAspect.ElementTypeCreate));

        var explanation = Assert.IsType<ElementTypeCreateExplanation>(result);
        Assert.Equal(2, explanation.ElementTypes.Count);
        Assert.Contains(explanation.ElementTypes, v => v.ElementType == "Promo Banner");
        Assert.Contains(explanation.ElementTypes, v => v.ElementType == "Pull Quote");
    }

    /// <summary>
    /// A site with no Library element types is a legitimate state, so it yields an empty list rather than
    /// an error the copilot would have to interpret.
    /// </summary>
    [Fact]
    public async Task ElementTypeCreate_WithNoElementTypes_ReturnsAnEmptyList()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        _elementTypes.GetLibraryElementTypes().Returns([]);

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, Aspect: LibraryAspect.ElementTypeCreate));

        Assert.Empty(Assert.IsType<ElementTypeCreateExplanation>(result).ElementTypes);
    }

    /// <summary>The all-user-groups roster partitions groups into allowed and denied per element type.</summary>
    [Fact]
    public async Task ElementTypeCreate_AllUserGroups_PartitionsGroups()
    {
        var typeKey = Guid.NewGuid();
        var bannerType = ElementType(typeKey, "Promo Banner");
        _contentTypeService.Get(typeKey).Returns(bannerType);
        _docTypePermissions
            .ResolveCreateForRolesAsync(
                Arg.Is<IReadOnlyList<string>>(r => r.Contains("editors")), Arg.Any<IReadOnlyList<Guid>>(),
                typeKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementCreateOfType, true, "editors", AdvancedPermissionsConstants.VirtualRootNodeKey));
        _docTypePermissions
            .ResolveCreateForRolesAsync(
                Arg.Is<IReadOnlyList<string>>(r => r.Count == 1 && r[0] == AdvancedPermissionsConstants.EveryoneRoleAlias),
                Arg.Any<IReadOnlyList<Guid>>(), typeKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementCreateOfType, false, AdvancedPermissionsConstants.EveryoneRoleAlias, AdvancedPermissionsConstants.VirtualRootNodeKey));

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.AllUserGroups, Aspect: LibraryAspect.ElementTypeCreate, ElementTypeKey: typeKey));

        var roster = Assert.IsType<ElementTypeCreateRoster>(result);
        Assert.Equal("Promo Banner", roster.ElementType);
        Assert.Contains("Editors", roster.AllowedUserGroups);
        Assert.Contains(AdvancedPermissionsConstants.EveryoneRoleDisplayName, roster.DeniedUserGroups);
    }

    // ── Cross-cutting guarantees ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nothing the model receives may contain a raw identifier. Serializing the whole result and scanning
    /// it is the only way to catch a leak from a nested field nobody thought to assert on.
    /// </summary>
    [Fact]
    public async Task Result_NeverLeaksRawIdentifiers()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        SetupLibraryItemName("Promo Banner");
        _permissions
            .ResolveAsync(userKey, _node, _path, AdvancedPermissionsConstants.VerbElementDelete, Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementDelete, false, "editors", _node));

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: AdvancedPermissionsConstants.VerbElementDelete));

        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("Umb.Element.", json, StringComparison.Ordinal);
        Assert.DoesNotContain(AdvancedPermissionsConstants.EveryoneRoleAlias, json, StringComparison.Ordinal);
        Assert.DoesNotContain("ThisNodeOnly", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"editors\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// An unknown node kind makes no applicability claim at all, so an unrecognised key yields a plain
    /// answer rather than a fabricated "not applicable".
    /// </summary>
    [Fact]
    public async Task NodeAspect_UnknownNodeKind_MakesNoApplicabilityClaim()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Unknown);
        _permissions
            .ResolveAsync(userKey, _node, _path, AdvancedPermissionsConstants.VerbElementCreate, Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementCreate, true, "editors", _node));

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: AdvancedPermissionsConstants.VerbElementCreate));

        Assert.Equal("Allowed", Assert.IsType<AccessVerdict>(result).Result);
    }
}
