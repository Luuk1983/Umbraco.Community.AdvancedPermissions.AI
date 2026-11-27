using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Default <see cref="ILibraryItemNameResolver"/>, over Umbraco's own entity name search.
/// </summary>
/// <remarks>
/// The search is the multi-object-type <see cref="IEntitySearchService"/> overload, which matches names
/// that contain the query and reads both object types in one call. Like <see cref="ElementTreeResolver"/>,
/// it must never go through a single-object-type <c>GetAll</c> for <see cref="UmbracoObjectTypes.ElementContainer"/>:
/// that overload throws for Library folders.
/// </remarks>
/// <param name="search">Umbraco's entity search, which matches names containing the query.</param>
/// <param name="entityService">The entity service, used to name the folders a candidate sits in.</param>
/// <param name="treeResolver">Tells Library items and folders apart.</param>
/// <param name="lookupGuard">Whether the asker may see the whole Library (Users-section access).</param>
/// <param name="startNodeReach">Which nodes the asker's Library start nodes hide.</param>
/// <param name="backOfficeSecurityAccessor">Resolves the asker.</param>
public sealed class LibraryItemNameResolver(
    IEntitySearchService search,
    IEntityService entityService,
    IElementTreeResolver treeResolver,
    IPermissionLookupGuard lookupGuard,
    IStartNodeReachResolver startNodeReach,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    : ILibraryItemNameResolver
{
    /// <summary>The Library tree's object types: items and folders.</summary>
    private static readonly UmbracoObjectTypes[] LibraryObjectTypes =
        [UmbracoObjectTypes.Element, UmbracoObjectTypes.ElementContainer];

    /// <summary>
    /// How many search hits are read. Enough to find every exact match behind a common word, while a vague
    /// name is still answered with a short list rather than the whole Library.
    /// </summary>
    private const int SearchSize = 100;

    /// <summary>The location shown for a node directly under the Library root.</summary>
    private const string RootLocation = "Library root";

    /// <inheritdoc />
    public async Task<LibraryItemLookup> ResolveAsync(string name)
    {
        var wanted = name.Trim();
        var found = await VisibleAsync(
            search.Search(LibraryObjectTypes, wanted, 0, SearchSize).Items.Where(e => !e.Trashed).ToList());

        var exact = found.Where(e => string.Equals(e.Name?.Trim(), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
        {
            return new LibraryItemLookup(exact[0].Key, null, null);
        }

        if (exact.Count > 1)
        {
            return Choose(
                $"Several Library items and folders are called '{wanted}'. Ask the editor which one they mean, then call again with its nodeKey.",
                exact);
        }

        if (found.Count > 0)
        {
            return Choose(
                $"There is no Library item or folder called '{wanted}'. These have a similar name: ask the editor which one they mean, then call again with its nodeKey.",
                found);
        }

        return new LibraryItemLookup(
            null,
            null,
            lookupGuard.CanInspectOthers()
                ? $"There is no Library item or folder called '{wanted}'."
                : $"There is no Library item or folder called '{wanted}' that you can see. Only what your Library start nodes let you see is searched.");
    }

    /// <summary>
    /// Drops the nodes the asker's Library start nodes hide from them, unless they may see the whole
    /// Library. Folders above a start node stay: Umbraco shows them for browsing.
    /// </summary>
    /// <param name="found">The search hits.</param>
    /// <returns>The hits the asker may be told about.</returns>
    private async Task<IReadOnlyList<IEntitySlim>> VisibleAsync(IReadOnlyList<IEntitySlim> found)
    {
        if (found.Count == 0 || lookupGuard.CanInspectOthers())
        {
            return found;
        }

        // An asker who cannot be identified sees nothing: failing closed is the only safe answer here.
        if (backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key is not { } asker)
        {
            return [];
        }

        var visible = new List<IEntitySlim>(found.Count);
        foreach (var entity in found)
        {
            var reach = await startNodeReach.ForUserAsync(asker, entity.Key, PermissionDomain.Library);
            if (reach?.Access != StartNodeReach.NotVisibleAccess)
            {
                visible.Add(entity);
            }
        }

        return visible;
    }

    /// <summary>Builds the candidates answer, naming each node's kind and the folders it sits in.</summary>
    /// <param name="message">What happened and what to do next.</param>
    /// <param name="matches">The matching nodes.</param>
    /// <returns>The lookup carrying the candidates.</returns>
    private LibraryItemLookup Choose(string message, IReadOnlyList<IEntitySlim> matches)
    {
        var listed = matches.Take(LibraryItemCandidates.MaxCandidates).ToList();
        var parentIds = listed.SelectMany(ParentIds).Distinct().ToArray();

        // One call for every folder involved, through the multi-object-type overload (see the remarks).
        var folderNames = parentIds.Length == 0
            ? new Dictionary<int, string>()
            : entityService.GetAll(LibraryObjectTypes, parentIds).ToDictionary(e => e.Id, e => e.Name ?? string.Empty);

        var candidates = listed
            .Select(e => new LibraryItemCandidate(
                e.Name ?? string.Empty,
                treeResolver.GetNodeKind(e.Key) == LibraryNodeKind.Folder ? "Folder" : "Item",
                Location(e, folderNames),
                e.Key))
            .ToList();

        return new LibraryItemLookup(null, new LibraryItemCandidates(message, candidates), null);
    }

    /// <summary>The ids of the folders a node sits in, root first, from its materialised path.</summary>
    /// <param name="entity">The node.</param>
    /// <returns>The folder ids; the <c>-1</c> root sentinel and the node itself are left out.</returns>
    private static IEnumerable<int> ParentIds(IEntitySlim entity) =>
        (entity.Path ?? string.Empty)
            .Split(',')
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0 && id != entity.Id);

    /// <summary>Names the folders a node sits in, e.g. "Marketing / Banners", or the Library root.</summary>
    /// <param name="entity">The node.</param>
    /// <param name="folderNames">Folder names by id.</param>
    /// <returns>The location.</returns>
    private static string Location(IEntitySlim entity, IReadOnlyDictionary<int, string> folderNames)
    {
        var names = ParentIds(entity)
            .Select(id => folderNames.TryGetValue(id, out var n) ? n : null)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
        return names.Count == 0 ? RootLocation : string.Join(" / ", names);
    }
}
