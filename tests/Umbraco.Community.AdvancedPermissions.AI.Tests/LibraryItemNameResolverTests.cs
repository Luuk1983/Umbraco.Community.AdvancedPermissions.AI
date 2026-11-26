using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="LibraryItemNameResolver"/>. The copilot is not offered in the Library
/// section, so it never has a Library node's key; in the live check it had to ask the editor for one.
/// The resolver turns the name the editor uses into the key, and never guesses between candidates.
/// </summary>
public sealed class LibraryItemNameResolverTests
{
    /// <summary>The mocked Umbraco entity search.</summary>
    private readonly IEntitySearchService _search = Substitute.For<IEntitySearchService>();

    /// <summary>The mocked entity service, naming the folders a candidate sits in.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked Library tree reader, telling items and folders apart.</summary>
    private readonly IElementTreeResolver _treeResolver = Substitute.For<IElementTreeResolver>();

    /// <summary>The lookup guard; the asker has Users-section access unless a test says otherwise.</summary>
    private readonly IPermissionLookupGuard _lookupGuard = TestGuards.AllowAll();

    /// <summary>The start-node reach resolver; nothing is hidden unless a test configures it.</summary>
    private readonly IStartNodeReachResolver _reach = TestGuards.NeverBlocked();

    /// <summary>The mocked backoffice security accessor.</summary>
    private readonly IBackOfficeSecurityAccessor _securityAccessor = Substitute.For<IBackOfficeSecurityAccessor>();

    /// <summary>The asker's key.</summary>
    private readonly Guid _asker = Guid.NewGuid();

    /// <summary>The "Marketing" folder (id 10), at the Library root.</summary>
    private readonly IEntitySlim _marketing;

    /// <summary>The "Banners" folder (id 11), inside Marketing.</summary>
    private readonly IEntitySlim _banners;

    /// <summary>Registers the asker and the two folders that candidate locations are named from.</summary>
    public LibraryItemNameResolverTests()
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(_asker);
        var security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns(user);
        _securityAccessor.BackOfficeSecurity.Returns(security);

        _marketing = Entity(10, "Marketing", "-1,10", LibraryNodeKind.Folder);
        _banners = Entity(11, "Banners", "-1,10,11", LibraryNodeKind.Folder);
        var folders = new[] { _marketing, _banners };
        _entityService.GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<int[]>()).Returns(folders);
    }

    /// <summary>Builds a Library entity and registers its kind.</summary>
    /// <param name="id">The entity id.</param>
    /// <param name="name">The name.</param>
    /// <param name="path">The materialised path.</param>
    /// <param name="kind">Whether it is an item or a folder.</param>
    /// <param name="trashed">Whether it is in the recycle bin.</param>
    /// <returns>The substitute entity.</returns>
    private IEntitySlim Entity(int id, string name, string path, LibraryNodeKind kind = LibraryNodeKind.Item, bool trashed = false)
    {
        var key = Guid.NewGuid();
        var entity = Substitute.For<IEntitySlim>();
        entity.Id.Returns(id);
        entity.Key.Returns(key);
        entity.Name.Returns(name);
        entity.Path.Returns(path);
        entity.Trashed.Returns(trashed);
        _treeResolver.GetNodeKind(key).Returns(kind);
        return entity;
    }

    /// <summary>Makes the name search return the given entities.</summary>
    /// <param name="found">What the search finds.</param>
    private void SearchFinds(params IEntitySlim[] found) =>
        _search.Search(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IEntitySlim>(found.Length, found));

    /// <summary>Builds the resolver under test.</summary>
    /// <returns>The resolver.</returns>
    private LibraryItemNameResolver CreateResolver() =>
        new(_search, _entityService, _treeResolver, _lookupGuard, _reach, _securityAccessor);

    /// <summary>One exact match, ignoring case and spaces, resolves to its key.</summary>
    [Fact]
    public async Task ExactName_OneMatch_ReturnsItsKey()
    {
        var banner = Entity(20, "Spring banner", "-1,10,11,20");
        SearchFinds(banner);

        var lookup = await CreateResolver().ResolveAsync("  spring BANNER ");

        Assert.Equal(banner.Key, lookup.NodeKey);
        Assert.Null(lookup.Candidates);
        Assert.Null(lookup.Error);
    }

    /// <summary>The search covers Library items AND folders, and searches the trimmed name.</summary>
    [Fact]
    public async Task Search_CoversItemsAndFolders()
    {
        SearchFinds();

        await CreateResolver().ResolveAsync(" Banners ");

        _search.Received().Search(
            Arg.Is<IEnumerable<UmbracoObjectTypes>>(t =>
                t.Contains(UmbracoObjectTypes.Element) && t.Contains(UmbracoObjectTypes.ElementContainer)),
            "Banners",
            Arg.Any<int>(),
            Arg.Any<int>());
    }

    /// <summary>An exact match wins over names that merely contain the text.</summary>
    [Fact]
    public async Task ExactName_WinsOverSimilarNames()
    {
        var exact = Entity(20, "Banner", "-1,20");
        SearchFinds(Entity(21, "Spring banner", "-1,10,11,21"), exact);

        Assert.Equal(exact.Key, (await CreateResolver().ResolveAsync("banner")).NodeKey);
    }

    /// <summary>
    /// Several nodes with the same name are never guessed between: they come back as candidates, each
    /// with its kind and the folders it sits in.
    /// </summary>
    [Fact]
    public async Task SeveralExactMatches_ReturnCandidatesWithLocation()
    {
        var inBanners = Entity(20, "Logo", "-1,10,11,20");
        var atRoot = Entity(21, "Logo", "-1,21", LibraryNodeKind.Folder);
        SearchFinds(inBanners, atRoot);

        var lookup = await CreateResolver().ResolveAsync("Logo");

        Assert.Null(lookup.NodeKey);
        Assert.NotNull(lookup.Candidates);
        Assert.StartsWith("Several Library items and folders are called 'Logo'.", lookup.Candidates.Message, StringComparison.Ordinal);
        Assert.Equal(
            [
                new LibraryItemCandidate("Logo", "Item", "Marketing / Banners", inBanners.Key),
                new LibraryItemCandidate("Logo", "Folder", "Library root", atRoot.Key),
            ],
            lookup.Candidates.Candidates);
    }

    /// <summary>
    /// With no exact match, nodes with a similar name are offered as candidates — even a single one is
    /// offered, not assumed.
    /// </summary>
    [Fact]
    public async Task NoExactMatch_SimilarNames_ReturnCandidates()
    {
        var similar = Entity(20, "Spring banner", "-1,10,11,20");
        SearchFinds(similar);

        var lookup = await CreateResolver().ResolveAsync("banner");

        Assert.Null(lookup.NodeKey);
        Assert.NotNull(lookup.Candidates);
        Assert.StartsWith("There is no Library item or folder called 'banner'.", lookup.Candidates.Message, StringComparison.Ordinal);
        Assert.Equal([similar.Key], lookup.Candidates.Candidates.Select(c => c.NodeKey));
    }

    /// <summary>A vague name never lists more than <see cref="LibraryItemCandidates.MaxCandidates"/> nodes.</summary>
    [Fact]
    public async Task Candidates_AreCapped()
    {
        SearchFinds(Enumerable.Range(0, 15).Select(i => Entity(100 + i, $"Banner {i}", $"-1,{100 + i}")).ToArray());

        var lookup = await CreateResolver().ResolveAsync("banner");

        Assert.Equal(LibraryItemCandidates.MaxCandidates, lookup.Candidates!.Candidates.Count);
    }

    /// <summary>Nothing with that name or a similar one is an error.</summary>
    [Fact]
    public async Task NoMatch_ReturnsError()
    {
        SearchFinds();

        var lookup = await CreateResolver().ResolveAsync("Winter banner");

        Assert.Null(lookup.NodeKey);
        Assert.Null(lookup.Candidates);
        Assert.Equal("There is no Library item or folder called 'Winter banner'.", lookup.Error);
    }

    /// <summary>Nodes in the recycle bin are not in the Library and are never matched.</summary>
    [Fact]
    public async Task TrashedNodes_AreIgnored()
    {
        SearchFinds(Entity(20, "Spring banner", "-1,20", trashed: true));

        var lookup = await CreateResolver().ResolveAsync("Spring banner");

        Assert.Null(lookup.NodeKey);
        Assert.NotNull(lookup.Error);
    }

    /// <summary>
    /// Someone without Users-section access only finds what their Library start nodes let them see, so a
    /// name search never reveals a node Umbraco hides from them; the error says why it may be missing.
    /// </summary>
    [Fact]
    public async Task RestrictedAsker_HiddenNodesAreNotFound()
    {
        _lookupGuard.CanInspectOthers().Returns(false);
        var hidden = Entity(20, "Secret banner", "-1,20");
        _reach.ForUserAsync(_asker, hidden.Key, PermissionDomain.Library)
            .Returns(new StartNodeReach(StartNodeReach.NotVisibleAccess, "Outside", [], "Fix"));
        SearchFinds(hidden);

        var lookup = await CreateResolver().ResolveAsync("Secret banner");

        Assert.Null(lookup.NodeKey);
        Assert.Null(lookup.Candidates);
        Assert.Equal(
            "There is no Library item or folder called 'Secret banner' that you can see. Only what your Library start nodes let you see is searched.",
            lookup.Error);
    }

    /// <summary>
    /// A folder above the asker's start node is shown in their tree for browsing, so it can be found by
    /// name even though it is blocked.
    /// </summary>
    [Fact]
    public async Task RestrictedAsker_BrowseOnlyFolderIsFound()
    {
        _lookupGuard.CanInspectOthers().Returns(false);
        var browseOnly = Entity(20, "Campaigns", "-1,20", LibraryNodeKind.Folder);
        _reach.ForUserAsync(_asker, browseOnly.Key, PermissionDomain.Library)
            .Returns(new StartNodeReach(StartNodeReach.PathOnlyAccess, "Above", [], "Fix"));
        SearchFinds(browseOnly);

        Assert.Equal(browseOnly.Key, (await CreateResolver().ResolveAsync("Campaigns")).NodeKey);
    }

    /// <summary>
    /// Hidden nodes are dropped before the exact-match count, so a restricted asker is not told that
    /// "several" exist when they can see only one.
    /// </summary>
    [Fact]
    public async Task RestrictedAsker_HiddenDuplicateDoesNotMakeItAmbiguous()
    {
        _lookupGuard.CanInspectOthers().Returns(false);
        var visible = Entity(20, "Logo", "-1,10,20");
        var hidden = Entity(21, "Logo", "-1,21");
        _reach.ForUserAsync(_asker, hidden.Key, PermissionDomain.Library)
            .Returns(new StartNodeReach(StartNodeReach.NotVisibleAccess, "Outside", [], "Fix"));
        SearchFinds(visible, hidden);

        Assert.Equal(visible.Key, (await CreateResolver().ResolveAsync("Logo")).NodeKey);
    }

    /// <summary>
    /// When the asker cannot be determined, a restricted lookup finds nothing rather than everything.
    /// </summary>
    [Fact]
    public async Task RestrictedAsker_Unknown_FindsNothing()
    {
        _lookupGuard.CanInspectOthers().Returns(false);
        _securityAccessor.BackOfficeSecurity.Returns((IBackOfficeSecurity?)null);
        SearchFinds(Entity(20, "Spring banner", "-1,20"));

        Assert.Null((await CreateResolver().ResolveAsync("Spring banner")).NodeKey);
    }
}
