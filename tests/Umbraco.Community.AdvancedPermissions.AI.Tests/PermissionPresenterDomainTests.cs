using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for the <see cref="PermissionDomain"/> awareness added to <see cref="PermissionPresenter"/>
/// in the v18 update, when the package gained the Library as a second permission tree.
/// </summary>
/// <remarks>
/// <para>
/// Only <b>node-name resolution</b> is genuinely domain-dependent: a content key resolves against
/// <see cref="UmbracoObjectTypes.Document"/>, a Library key against
/// <see cref="UmbracoObjectTypes.Element"/> / <see cref="UmbracoObjectTypes.ElementContainer"/>, and the
/// virtual-root sentinel needs a different label in each tree. Rather than thread a domain argument
/// through all nine presenter methods, the domain is bound once via
/// <see cref="IPermissionPresenter.For(PermissionDomain)"/>; the default instance stays Content, so the
/// content tool needs no changes at all.
/// </para>
/// <para>
/// Verb display names deliberately do <b>not</b> take a domain: the verb string itself already says which
/// tree it belongs to (<c>Umb.Document.*</c> / <c>Umb.Element.*</c> / <c>Umb.ElementContainer.*</c>), so
/// dispatching on the verb is both sufficient and impossible to get wrong at a call site.
/// </para>
/// </remarks>
public sealed class PermissionPresenterDomainTests
{
    /// <summary>The mocked user group service backing role display names.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service backing node-name resolution.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service backing document/element type names.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

    /// <summary>The presenter under test, in its default (content) domain.</summary>
    private IPermissionPresenter Sut => new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

    /// <summary>Creates a stubbed entity exposing the given name.</summary>
    /// <param name="name">The name the entity should report.</param>
    /// <returns>A substituted <see cref="IEntitySlim"/>.</returns>
    private static IEntitySlim StubNamed(string name)
    {
        var e = Substitute.For<IEntitySlim>();
        e.Name.Returns(name);
        return e;
    }

    /// <summary>A presenter with no domain bound resolves node names as content — the v17 behaviour.</summary>
    [Fact]
    public void GetNodeName_DefaultDomain_ResolvesAsContent()
    {
        var key = Guid.NewGuid();
        var entity = StubNamed("Home");
        _entityService.Get(key, UmbracoObjectTypes.Document).Returns(entity);

        Assert.Equal("Home", Sut.GetNodeName(key));
    }

    /// <summary>An explicit content binding behaves identically to the default.</summary>
    [Fact]
    public void GetNodeName_ContentDomain_ResolvesAgainstDocumentObjectType()
    {
        var key = Guid.NewGuid();
        var entity = StubNamed("Press Releases");
        _entityService.Get(key, UmbracoObjectTypes.Document).Returns(entity);

        Assert.Equal("Press Releases", Sut.For(PermissionDomain.Content).GetNodeName(key));
    }

    /// <summary>A Library binding resolves an item against the element object type, not the document one.</summary>
    [Fact]
    public void GetNodeName_LibraryDomain_ResolvesElementAsLibraryItem()
    {
        var key = Guid.NewGuid();
        var entity = StubNamed("Promo Banner");
        _entityService.Get(key, UmbracoObjectTypes.Element).Returns(entity);

        Assert.Equal("Promo Banner", Sut.For(PermissionDomain.Library).GetNodeName(key));
        _entityService.DidNotReceive().Get(key, UmbracoObjectTypes.Document);
    }

    /// <summary>
    /// A Library key that is a folder rather than an element must fall back to the element-container
    /// object type — otherwise every folder in a reasoning chain would read as the generic fallback label.
    /// </summary>
    [Fact]
    public void GetNodeName_LibraryDomain_FallsBackToElementContainerForFolders()
    {
        var key = Guid.NewGuid();
        var folder = StubNamed("Campaign Assets");
        _entityService.Get(key, UmbracoObjectTypes.Element).Returns((IEntitySlim?)null);
        _entityService.Get(key, UmbracoObjectTypes.ElementContainer).Returns(folder);

        Assert.Equal("Campaign Assets", Sut.For(PermissionDomain.Library).GetNodeName(key));
    }

    /// <summary>
    /// The virtual-root sentinel is the Default permissions row, and its label must name the tree it
    /// baselines — saying "All content" while explaining a Library permission would be simply wrong.
    /// </summary>
    [Fact]
    public void GetNodeName_VirtualRoot_IsLabelledPerDomain()
    {
        var root = AdvancedPermissionsConstants.VirtualRootNodeKey;

        var content = Sut.For(PermissionDomain.Content).GetNodeName(root);
        var library = Sut.For(PermissionDomain.Library).GetNodeName(root);

        Assert.Contains("content", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("librar", library, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(content, library);
    }

    /// <summary>An unresolvable Library key falls back to a generic label rather than throwing or leaking a GUID.</summary>
    [Fact]
    public void GetNodeName_LibraryDomain_UnresolvableKey_FallsBackWithoutLeakingTheGuid()
    {
        var key = Guid.NewGuid();
        _entityService.Get(key, UmbracoObjectTypes.Element).Returns((IEntitySlim?)null);
        _entityService.Get(key, UmbracoObjectTypes.ElementContainer).Returns((IEntitySlim?)null);

        var name = Sut.For(PermissionDomain.Library).GetNodeName(key);

        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.DoesNotContain(key.ToString(), name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Binding a domain must not disturb any of the presenter's domain-independent behaviour.</summary>
    [Fact]
    public async Task For_PreservesDomainIndependentBehaviour()
    {
        var bound = Sut.For(PermissionDomain.Library);

        Assert.Equal(
            AdvancedPermissionsConstants.EveryoneRoleDisplayName,
            await bound.GetRoleDisplayNameAsync(AdvancedPermissionsConstants.EveryoneRoleAlias));
        Assert.Equal("Allowed", bound.GetStateText(Core.Models.PermissionState.Allow));
        Assert.Equal(Sut.GetScopeText(Core.Models.PermissionScope.ThisNodeOnly), bound.GetScopeText(Core.Models.PermissionScope.ThisNodeOnly));
    }

    /// <summary>
    /// The nine canonical element verbs present as their bare action name, exactly as the document verbs
    /// already do — so the Library reasoning chain never leaks an <c>Umb.Element.*</c> identifier.
    /// </summary>
    /// <param name="verb">The raw element verb.</param>
    /// <param name="expected">The friendly action name.</param>
    [Theory]
    [InlineData("Umb.Element.Read", "Read")]
    [InlineData("Umb.Element.Create", "Create")]
    [InlineData("Umb.Element.Update", "Update")]
    [InlineData("Umb.Element.Delete", "Delete")]
    [InlineData("Umb.Element.Publish", "Publish")]
    [InlineData("Umb.Element.Unpublish", "Unpublish")]
    [InlineData("Umb.Element.Duplicate", "Duplicate")]
    [InlineData("Umb.Element.Move", "Move")]
    [InlineData("Umb.Element.Rollback", "Rollback")]
    public void GetVerbDisplayName_ElementVerbs_PresentAsBareAction(string verb, string expected) =>
        Assert.Equal(expected, Sut.GetVerbDisplayName(verb));

    /// <summary>The element-folder verbs present as their bare action name too.</summary>
    /// <param name="verb">The raw element-container verb.</param>
    /// <param name="expected">The friendly action name.</param>
    [Theory]
    [InlineData("Umb.ElementContainer.Read", "Read")]
    [InlineData("Umb.ElementContainer.Create", "Create")]
    [InlineData("Umb.ElementContainer.Update", "Update")]
    [InlineData("Umb.ElementContainer.Delete", "Delete")]
    [InlineData("Umb.ElementContainer.Move", "Move")]
    public void GetVerbDisplayName_ElementContainerVerbs_PresentAsBareAction(string verb, string expected) =>
        Assert.Equal(expected, Sut.GetVerbDisplayName(verb));

    /// <summary>
    /// The element-type create verb gets the Library Element Type Permissions editor's own column label
    /// ("Create in Library", from the base package's <c>elementTypePermissions_verbCreate</c>) rather than
    /// the document editor's "Insert" — the two filters are different surfaces and must not read alike.
    /// </summary>
    [Fact]
    public void GetVerbDisplayName_ElementTypeCreateVerb_UsesTheLibraryColumnLabel()
    {
        var label = Sut.GetVerbDisplayName(AdvancedPermissionsConstants.VerbElementCreateOfType);

        Assert.Equal("Create in Library", label);
        Assert.NotEqual(Sut.GetVerbDisplayName(AdvancedPermissionsConstants.VerbCreateOfType), label);
    }

    /// <summary>The document create verb keeps its existing "Insert" label — a regression guard.</summary>
    [Fact]
    public void GetVerbDisplayName_DocumentTypeCreateVerb_StillReadsInsert() =>
        Assert.Equal("Insert", Sut.GetVerbDisplayName(AdvancedPermissionsConstants.VerbCreateOfType));
}
