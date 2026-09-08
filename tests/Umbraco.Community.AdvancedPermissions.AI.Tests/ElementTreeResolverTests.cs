using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="ElementTreeResolver"/>, the Library counterpart to
/// <see cref="ContentPathResolver"/>.
/// </summary>
/// <remarks>
/// Written test-first and mirroring the base package's own (internal, therefore un-referenceable)
/// <c>ElementTreePathResolver.BuildPathFromRoot</c>. Two behaviours are specific to the Library tree and
/// are the reason this is a separate resolver rather than a parameter on the content one:
/// <list type="bullet">
/// <item><description>
/// The target key may be either an element or an element folder, so the path lookup tries
/// <see cref="UmbracoObjectTypes.Element"/> first and falls back to
/// <see cref="UmbracoObjectTypes.ElementContainer"/>.
/// </description></item>
/// <item><description>
/// A single Library path may contain ids of <i>both</i> object types, so id-to-key mapping must use the
/// multi-object-type <see cref="IEntityService.GetAll(IEnumerable{UmbracoObjectTypes}, int[])"/> overload.
/// The single-type <c>GetAll(UmbracoObjectTypes.ElementContainer, ids)</c> overload throws — element
/// containers have no CLR entity type mapping — which is precisely the trap this resolver must avoid.
/// </description></item>
/// </list>
/// </remarks>
public sealed class ElementTreeResolverTests
{
    /// <summary>The mocked Umbraco entity service the resolver depends on.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The system under test.</summary>
    private readonly IElementTreeResolver _sut;

    /// <summary>Initializes a new instance of the <see cref="ElementTreeResolverTests"/> class.</summary>
    public ElementTreeResolverTests() => _sut = new ElementTreeResolver(_entityService);

    /// <summary>Creates a stubbed <see cref="IEntitySlim"/> with the given int id and Guid key.</summary>
    /// <param name="id">The integer id of the entity.</param>
    /// <param name="key">The Guid key of the entity.</param>
    /// <returns>A substituted <see cref="IEntitySlim"/>.</returns>
    private static IEntitySlim StubEntity(int id, Guid key)
    {
        var e = Substitute.For<IEntitySlim>();
        e.Id.Returns(id);
        e.Key.Returns(key);
        return e;
    }

    /// <summary>Creates a stubbed <see cref="TreeEntityPath"/> for the given path string.</summary>
    /// <param name="path">The materialized path, e.g. <c>-1,1001,1002</c>.</param>
    /// <returns>A tree entity path carrying that string.</returns>
    private static TreeEntityPath Path(string path) => new() { Path = path };

    /// <summary>
    /// Stubs the multi-object-type id-to-key lookup — the only overload safe for the Library tree.
    /// </summary>
    /// <param name="entities">The entities the lookup should return.</param>
    private void SetupIdToKeyLookup(params IEntitySlim[] entities) =>
        _entityService
            .GetAll(
                Arg.Is<IEnumerable<UmbracoObjectTypes>>(t =>
                    t.Contains(UmbracoObjectTypes.Element) && t.Contains(UmbracoObjectTypes.ElementContainer)),
                Arg.Any<int[]>())
            .Returns(entities);

    /// <summary>
    /// Happy path for an element: the path is found under <see cref="UmbracoObjectTypes.Element"/>, the
    /// <c>-1</c> sentinel is dropped, and the keys come back root-first, target-last.
    /// </summary>
    [Fact]
    public void GetPathFromRoot_ForElement_ResolvesPathRootFirstSentinelDropped()
    {
        var target = Guid.NewGuid();
        var keyFolder = Guid.NewGuid();
        var keyTarget = Guid.NewGuid();

        _entityService.GetAllPaths(UmbracoObjectTypes.Element, target).Returns([Path("-1,1001,1002")]);
        SetupIdToKeyLookup(StubEntity(1001, keyFolder), StubEntity(1002, keyTarget));

        var result = _sut.GetPathFromRoot(target);

        Assert.Equal([keyFolder, keyTarget], result);
    }

    /// <summary>
    /// A folder key is not found under <see cref="UmbracoObjectTypes.Element"/>, so the resolver must fall
    /// back to <see cref="UmbracoObjectTypes.ElementContainer"/> rather than giving up.
    /// </summary>
    [Fact]
    public void GetPathFromRoot_ForFolder_FallsBackToElementContainerLookup()
    {
        var target = Guid.NewGuid();
        var keyTarget = Guid.NewGuid();

        _entityService.GetAllPaths(UmbracoObjectTypes.Element, target).Returns([]);
        _entityService.GetAllPaths(UmbracoObjectTypes.ElementContainer, target).Returns([Path("-1,1001")]);
        SetupIdToKeyLookup(StubEntity(1001, keyTarget));

        var result = _sut.GetPathFromRoot(target);

        Assert.Equal([keyTarget], result);
    }

    /// <summary>
    /// The core Library-specific requirement: a path mixing a folder and an element resolves fully. This
    /// only works via the multi-object-type overload, so the test also proves the resolver never reaches
    /// for the single-type overload that throws for element containers.
    /// </summary>
    [Fact]
    public void GetPathFromRoot_MixedFolderAndElementPath_ResolvesEveryNodeViaMultiTypeLookup()
    {
        var target = Guid.NewGuid();
        var keyFolder = Guid.NewGuid();
        var keySubFolder = Guid.NewGuid();
        var keyElement = Guid.NewGuid();

        _entityService.GetAllPaths(UmbracoObjectTypes.Element, target).Returns([Path("-1,1001,1002,1003")]);
        SetupIdToKeyLookup(
            StubEntity(1001, keyFolder),
            StubEntity(1002, keySubFolder),
            StubEntity(1003, keyElement));

        var result = _sut.GetPathFromRoot(target);

        Assert.Equal([keyFolder, keySubFolder, keyElement], result);

        // The single-object-type overload throws for element containers, so it must never be used.
        _entityService.DidNotReceive().GetAll(UmbracoObjectTypes.ElementContainer, Arg.Any<int[]>());
    }

    /// <summary>
    /// An unknown key resolves under neither object type, so the result is empty — never a partial or
    /// invented path, which would make the resolver silently answer about the wrong node.
    /// </summary>
    [Fact]
    public void GetPathFromRoot_UnknownKey_ReturnsEmpty()
    {
        var target = Guid.NewGuid();

        _entityService.GetAllPaths(UmbracoObjectTypes.Element, target).Returns([]);
        _entityService.GetAllPaths(UmbracoObjectTypes.ElementContainer, target).Returns([]);

        Assert.Empty(_sut.GetPathFromRoot(target));
    }

    /// <summary>
    /// A root-level item's path is just the sentinel, which is dropped — yielding an empty ancestor path
    /// rather than a bogus entry for node <c>-1</c>.
    /// </summary>
    [Fact]
    public void GetPathFromRoot_RootLevelItem_DropsSentinelAndReturnsEmpty()
    {
        var target = Guid.NewGuid();

        _entityService.GetAllPaths(UmbracoObjectTypes.Element, target).Returns([Path("-1")]);
        SetupIdToKeyLookup();

        Assert.Empty(_sut.GetPathFromRoot(target));
    }

    /// <summary>
    /// An id in the path that the entity service cannot map to a key is skipped rather than throwing, so a
    /// partially-resolvable path still yields the ancestors that <i>are</i> known.
    /// </summary>
    [Fact]
    public void GetPathFromRoot_UnmappableId_SkipsItAndKeepsTheRest()
    {
        var target = Guid.NewGuid();
        var keyTarget = Guid.NewGuid();

        _entityService.GetAllPaths(UmbracoObjectTypes.Element, target).Returns([Path("-1,1001,1002")]);
        SetupIdToKeyLookup(StubEntity(1002, keyTarget));

        Assert.Equal([keyTarget], _sut.GetPathFromRoot(target));
    }
}
