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

    /// <summary>The mocked lookup guard; allows inspecting others unless a test says otherwise.</summary>
    private readonly IPermissionLookupGuard _lookupGuard = TestGuards.AllowAll();

    /// <summary>The mocked start-node reach resolver; returns null (not blocked) unless a test configures it.</summary>
    private readonly IStartNodeReachResolver _reach = Substitute.For<IStartNodeReachResolver>();

    /// <summary>The mocked Library name lookup; only consulted when a test passes a node name.</summary>
    private readonly ILibraryItemNameResolver _itemNames = Substitute.For<ILibraryItemNameResolver>();

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
            _userService,
            _lookupGuard,
            _reach,
            _itemNames);

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

    /// <summary>
    /// Without Users-section access, asking about another user, a user group or every user group in the
    /// Library is refused before anything is resolved.
    /// </summary>
    /// <param name="subject">A subject other than the asker.</param>
    [Theory]
    [InlineData(ExplainSubject.User)]
    [InlineData(ExplainSubject.UserGroup)]
    [InlineData(ExplainSubject.AllUserGroups)]
    public async Task OtherSubject_WithoutUsersSection_ReturnsError(ExplainSubject subject)
    {
        _lookupGuard.CanInspectOthers().Returns(false);

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            subject, _node, UserKey: Guid.NewGuid(), UserGroupAlias: "editors"));

        Assert.Equal("Looking up the permissions of other users or user groups needs access to the Users section. You can still ask about your own access.", Assert.IsType<AccessError>(result).Error);
        Assert.Empty(_permissions.ReceivedCalls());
    }

    /// <summary>The asker's own Library access stays answerable without Users-section access.</summary>
    [Fact]
    public async Task CurrentUser_WithoutUsersSection_StillAnswers()
    {
        _lookupGuard.CanInspectOthers().Returns(false);
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Item);
        _permissions
            .ResolveAsync(userKey, _node, _path, AdvancedPermissionsConstants.VerbElementDelete, Arg.Any<CancellationToken>())
            .Returns(Perm(AdvancedPermissionsConstants.VerbElementDelete, isAllowed: true, "editors", _node));

        var result = await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: AdvancedPermissionsConstants.VerbElementDelete));

        Assert.IsType<AccessVerdict>(result);
    }

    /// <summary>A blocking Library reach, as the resolver would return outside the Library start nodes.</summary>
    private static readonly StartNodeReach LibraryBlocked = new(
        "Not visible (start nodes)",
        "Outside every start node, so Umbraco hides it from the tree.",
        [new StartNodeRef("Campaign assets", "from Editors")],
        "Library start nodes are set in the Users section, on the user group or on the user account. No permission entry can change this.");

    /// <summary>A Library item outside the user's Library start nodes is blocked, never allowed.</summary>
    [Fact]
    public async Task Item_OutsideLibraryStartNodes_Blocked()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Item);
        var allowed = Perm(AdvancedPermissionsConstants.VerbElementDelete, true, "editors", _node);
        _permissions
            .ResolveAsync(userKey, _node, _path, AdvancedPermissionsConstants.VerbElementDelete, Arg.Any<CancellationToken>())
            .Returns(allowed);
        _reach.ForUserAsync(userKey, _node, PermissionDomain.Library).Returns(LibraryBlocked);

        var verdict = Assert.IsType<AccessVerdict>(await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: AdvancedPermissionsConstants.VerbElementDelete)));

        Assert.Equal("Blocked (start nodes)", verdict.Result);
        Assert.Equal("Allowed", verdict.IfInsideStartNodes);
        Assert.Same(LibraryBlocked, verdict.StartNodes);
    }

    /// <summary>
    /// Review focus 4: a permission that never applies to a folder stays "Not applicable" even when the
    /// folder is outside the start nodes — N/A wins over blocked, as in the Library Access Viewer.
    /// </summary>
    [Fact]
    public async Task Folder_NotApplicableVerb_StaysNotApplicableWhenBlocked()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Folder);
        _reach.ForUserAsync(userKey, _node, PermissionDomain.Library).Returns(LibraryBlocked);

        var verdict = Assert.IsType<AccessVerdict>(await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: AdvancedPermissionsConstants.VerbElementPublish)));

        Assert.Equal("Not applicable", verdict.Result);
        Assert.Null(verdict.IfInsideStartNodes);
    }

    /// <summary>
    /// In an all-permissions answer for a blocked folder, applicable permissions are blocked and
    /// inapplicable ones stay "Not applicable"; the reach is carried once at the top.
    /// </summary>
    [Fact]
    public async Task Folder_AllPermissions_BlockedExceptNotApplicable()
    {
        var userKey = Guid.NewGuid();
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Folder);
        var resolved = new Dictionary<string, EffectivePermission>
        {
            [AdvancedPermissionsConstants.VerbElementDelete] = Perm(AdvancedPermissionsConstants.VerbElementDelete, true, "editors", _node),
            [AdvancedPermissionsConstants.VerbElementPublish] = Perm(AdvancedPermissionsConstants.VerbElementPublish, true, "editors", _node),
        };
        _permissions.ResolveAllAsync(userKey, _node, _path, null, Arg.Any<CancellationToken>()).Returns(resolved);
        _reach.ForUserAsync(userKey, _node, PermissionDomain.Library).Returns(LibraryBlocked);

        var explanation = Assert.IsType<AccessExplanation>(await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.User, _node, UserKey: userKey)));

        Assert.Same(LibraryBlocked, explanation.StartNodes);
        Assert.Contains(explanation.Permissions, v => v.Permission == "Delete" && v.Result == "Blocked (start nodes)");
        Assert.Contains(explanation.Permissions, v => v.Result == "Not applicable");
        Assert.DoesNotContain(explanation.Permissions, v => v.Result == "Allowed");
    }

    /// <summary>A group whose own Library start node misses the item is listed as blocked in the roster.</summary>
    [Fact]
    public async Task AllUserGroups_BlockedGroupsListedSeparately()
    {
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Item);
        var resolved = new Dictionary<string, EffectivePermission>
        {
            [AdvancedPermissionsConstants.VerbElementDelete] = Perm(AdvancedPermissionsConstants.VerbElementDelete, true, "editors", _node),
        };
        _permissions
            .ResolveForRoleAsync(Arg.Any<string>(), _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(resolved);
        _reach.ForUserGroupAsync("editors", _node, PermissionDomain.Library).Returns(LibraryBlocked);

        var roster = Assert.IsType<AccessRoster>(await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.AllUserGroups, _node, Permission: AdvancedPermissionsConstants.VerbElementDelete)));

        Assert.Equal(["Editors"], roster.BlockedUserGroups!);
        Assert.Contains("Editors", roster.AllowedUserGroups);
        Assert.Contains("combined start nodes", roster.StartNodeNote!, StringComparison.Ordinal);
        Assert.Contains("All Users", roster.AllowedUserGroups);
    }

    /// <summary>
    /// The Umbraco 18 upgrade trap: a user with no Library start node cannot create any element type,
    /// whatever the create filter says, and the answer explains why.
    /// </summary>
    [Fact]
    public async Task ElementTypeCreate_NoLibraryStartNode_AllBlocked()
    {
        var userKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        var bannerType = ElementType(typeKey, "Promo Banner");
        _contentTypeService.Get(typeKey).Returns(bannerType);
        _elementTypes.GetLibraryElementTypes().Returns([bannerType]);
        var allowed = Perm(AdvancedPermissionsConstants.VerbElementCreateOfType, true, "editors", AdvancedPermissionsConstants.VirtualRootNodeKey);
        _docTypePermissions
            .ResolveCreateForRolesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<Guid>>(), typeKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(allowed);
        var noAccess = LibraryBlocked with
        {
            Explanation = "This user has no Library start nodes, so they can't reach the Library at all. Everything is blocked.",
            StartNodes = [],
        };
        _reach.ForLibraryCreateAsync(userKey, null).Returns(noAccess);

        var explanation = Assert.IsType<ElementTypeCreateExplanation>(await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, Aspect: LibraryAspect.ElementTypeCreate)));

        Assert.Same(noAccess, explanation.StartNodes);
        var verdict = Assert.Single(explanation.ElementTypes);
        Assert.Equal("Blocked (start nodes)", verdict.Result);
        Assert.Equal("Allowed", verdict.IfInsideStartNodes);
    }

    /// <summary>A user with a Library start node can create inside it, so creation is not blocked.</summary>
    [Fact]
    public async Task ElementTypeCreate_RestrictedButHasStartNode_NotBlocked()
    {
        var userKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        var bannerType = ElementType(typeKey, "Promo Banner");
        _contentTypeService.Get(typeKey).Returns(bannerType);
        var allowed = Perm(AdvancedPermissionsConstants.VerbElementCreateOfType, true, "editors", AdvancedPermissionsConstants.VirtualRootNodeKey);
        _docTypePermissions
            .ResolveCreateForRolesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<Guid>>(), typeKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(allowed);

        var verdict = Assert.IsType<ElementTypeCreateVerdict>(await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, Aspect: LibraryAspect.ElementTypeCreate, ElementTypeKey: typeKey)));

        Assert.Equal("Allowed", verdict.Result);
        Assert.Null(verdict.StartNodes);
        await _reach.Received(1).ForLibraryCreateAsync(userKey, null);
    }

    /// <summary>"Who can create Promo Banner?": groups with no Library start node are listed as blocked.</summary>
    [Fact]
    public async Task ElementTypeCreate_Roster_GroupsWithoutLibraryStartNodeBlocked()
    {
        var typeKey = Guid.NewGuid();
        var bannerType = ElementType(typeKey, "Promo Banner");
        _contentTypeService.Get(typeKey).Returns(bannerType);
        var allowed = Perm(AdvancedPermissionsConstants.VerbElementCreateOfType, true, "editors", AdvancedPermissionsConstants.VirtualRootNodeKey);
        _docTypePermissions
            .ResolveCreateForRolesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<Guid>>(), typeKey, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(allowed);
        _reach.ForLibraryCreateAsync(null, "editors").Returns(LibraryBlocked);

        var roster = Assert.IsType<ElementTypeCreateRoster>(await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.AllUserGroups, Aspect: LibraryAspect.ElementTypeCreate, ElementTypeKey: typeKey)));

        Assert.Equal(["Editors"], roster.BlockedUserGroups!);
        Assert.Contains("Editors", roster.AllowedUserGroups);
        Assert.Contains("combined start nodes", roster.StartNodeNote!, StringComparison.Ordinal);
    }

    /// <summary>A blocked Library node cannot be fixed with a permission entry, so no fix is computed.</summary>
    [Fact]
    public async Task SuggestFix_WhenBlocked_ReturnsNoRemediations()
    {
        var userKey = Guid.NewGuid();
        SetupCurrentUser(userKey);
        SetupUserGroups(userKey);
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Item);
        var denied = Perm(AdvancedPermissionsConstants.VerbElementDelete, false, "editors", _node);
        _permissions
            .ResolveAsync(userKey, _node, _path, AdvancedPermissionsConstants.VerbElementDelete, Arg.Any<CancellationToken>())
            .Returns(denied);
        _reach.ForUserAsync(userKey, _node, PermissionDomain.Library).Returns(LibraryBlocked);

        var verdict = Assert.IsType<AccessVerdict>(await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.CurrentUser, _node, Permission: AdvancedPermissionsConstants.VerbElementDelete, SuggestFix: true)));

        Assert.Equal("Blocked (start nodes)", verdict.Result);
        Assert.Null(verdict.Remediations);
        await _remediation.DidNotReceiveWithAnyArgs().SuggestAsync(default, default!, default!, default!, default, default, default);
    }

    /// <summary>The Library tool also maps a user group's display name to its alias before resolving.</summary>
    [Fact]
    public async Task UserGroup_DisplayName_ResolvesToTheAlias()
    {
        _treeResolver.GetNodeKind(_node).Returns(LibraryNodeKind.Item);

        await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.UserGroup, _node, UserGroupAlias: "Editors"));

        await _permissions.Received(1).ResolveForRoleAsync(
            "editors", _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>An unknown user group is refused rather than resolved as an empty entry set.</summary>
    [Fact]
    public async Task UserGroup_Unknown_ReturnsError()
    {
        var result = await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.UserGroup, _node, UserGroupAlias: "Marketing"));

        Assert.StartsWith("There is no user group called 'Marketing'.", Assert.IsType<AccessError>(result).Error, StringComparison.Ordinal);
    }

    // ── Lookup by name ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Live-check finding: the copilot has no Library node key, so the editor's name for the node is
    /// resolved to the key and the permissions are explained at that node.
    /// </summary>
    [Fact]
    public async Task NodeName_ResolvesToTheNode()
    {
        _itemNames.ResolveAsync("Spring banner").Returns(new LibraryItemLookup(_node, null, null));

        await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.UserGroup, UserGroupAlias: "Editors", NodeName: "Spring banner"));

        await _permissions.Received(1).ResolveForRoleAsync(
            "editors", _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>An ambiguous name returns the candidates for the editor to choose from, and resolves nothing.</summary>
    [Fact]
    public async Task NodeName_Ambiguous_ReturnsTheCandidates()
    {
        var candidates = new LibraryItemCandidates("Several", [new LibraryItemCandidate("Logo", "Item", "Library root", _node)]);
        _itemNames.ResolveAsync("Logo").Returns(new LibraryItemLookup(null, candidates, null));

        var result = await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.UserGroup, UserGroupAlias: "Editors", NodeName: "Logo"));

        Assert.Same(candidates, result);
        Assert.Empty(_permissions.ReceivedCalls());
    }

    /// <summary>A name that matches nothing is relayed as an error.</summary>
    [Fact]
    public async Task NodeName_NotFound_ReturnsTheError()
    {
        _itemNames.ResolveAsync("Winter banner").Returns(new LibraryItemLookup(null, null, "Not found."));

        var result = await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.UserGroup, UserGroupAlias: "Editors", NodeName: "Winter banner"));

        Assert.Equal("Not found.", Assert.IsType<AccessError>(result).Error);
    }

    /// <summary>A key, when given, is used as-is: the name is not looked up.</summary>
    [Fact]
    public async Task NodeKey_WinsOverNodeName()
    {
        await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.UserGroup, _node, UserGroupAlias: "Editors", NodeName: "Spring banner"));

        await _itemNames.DidNotReceiveWithAnyArgs().ResolveAsync(default!);
        await _permissions.Received(1).ResolveForRoleAsync(
            "editors", _node, _path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Without a key or a name, the node aspect asks for either.</summary>
    [Fact]
    public async Task NoNodeKeyOrName_ReturnsError()
    {
        var result = await RunAsync(new ExplainLibraryAccessArgs(ExplainSubject.UserGroup, UserGroupAlias: "Editors"));

        Assert.Equal(
            "A nodeKey or nodeName is required to explain permissions on a library item or folder.",
            Assert.IsType<AccessError>(result).Error);
    }

    /// <summary>The section-wide element-type aspect takes no node, so a name is never looked up for it.</summary>
    [Fact]
    public async Task ElementTypeCreate_IgnoresNodeName()
    {
        _elementTypes.GetLibraryElementTypes().Returns(Array.Empty<IContentType>());

        await RunAsync(new ExplainLibraryAccessArgs(
            ExplainSubject.UserGroup, UserGroupAlias: "Editors", Aspect: LibraryAspect.ElementTypeCreate, NodeName: "Spring banner"));

        await _itemNames.DidNotReceiveWithAnyArgs().ResolveAsync(default!);
    }
}
