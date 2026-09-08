using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Default <see cref="IElementTreeResolver"/> that derives the root-to-node key path for a Library
/// element or element folder from Umbraco's materialized path.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the logic of the base package's own <c>ElementTreePathResolver.BuildPathFromRoot</c>, which is
/// <c>internal</c> and therefore cannot be referenced — the same reason
/// <see cref="ContentPathResolver"/> mirrors the base package's controller base. Keeping the two in step
/// matters: the permission resolver must receive an identically-shaped path to the one the base package's
/// own enforcement adapters build, or inheritance would be walked differently here than at runtime.
/// </para>
/// <para>
/// Two details are specific to the Library tree, and both are traps:
/// </para>
/// <list type="number">
/// <item><description>
/// A key may identify either an element or a folder, and there is no way to know which up front, so the
/// path lookup tries <see cref="UmbracoObjectTypes.Element"/> and falls back to
/// <see cref="UmbracoObjectTypes.ElementContainer"/>.
/// </description></item>
/// <item><description>
/// A single path can contain ids of <i>both</i> object types, so id-to-key mapping must use the
/// multi-object-type <see cref="IEntityService.GetAll(IEnumerable{UmbracoObjectTypes}, int[])"/> overload.
/// The single-type <c>GetAll(UmbracoObjectTypes.ElementContainer, ids)</c> overload <b>throws</b> —
/// element containers have no CLR entity type mapping — so it must never be reached for. (The
/// <c>GetAllPaths</c> single-type overload is fine for containers; only <c>GetAll</c> is not.)
/// </description></item>
/// </list>
/// </remarks>
/// <param name="entityService">The Umbraco entity service used to read materialized paths and map ids to keys.</param>
public sealed class ElementTreeResolver(IEntityService entityService) : IElementTreeResolver
{
    /// <summary>The Library tree's object types, in the order a target key is probed.</summary>
    private static readonly UmbracoObjectTypes[] LibraryObjectTypes =
        [UmbracoObjectTypes.Element, UmbracoObjectTypes.ElementContainer];

    /// <inheritdoc />
    public IReadOnlyList<Guid> GetPathFromRoot(Guid nodeKey)
    {
        // The key may be an element or a folder; try both, elements first (the common case).
        var pathEntry =
            entityService.GetAllPaths(UmbracoObjectTypes.Element, nodeKey).FirstOrDefault()
            ?? entityService.GetAllPaths(UmbracoObjectTypes.ElementContainer, nodeKey).FirstOrDefault();

        if (pathEntry?.Path is not { } path)
        {
            return [];
        }

        // Drop Umbraco's "-1" root sentinel (and any unparseable segment) — it is not a real node, and
        // virtual-root defaults are applied by the resolver rather than by the path.
        var pathIds = path
            .Split(',')
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToArray();

        if (pathIds.Length == 0)
        {
            return [];
        }

        // Elements and folders share the tree, so both object types are resolved in ONE call. See the
        // remarks: the single-object-type overload throws for element containers.
        var idToKey = entityService
            .GetAll(LibraryObjectTypes, pathIds)
            .ToDictionary(e => e.Id, e => e.Key);

        var result = new List<Guid>(pathIds.Length);
        foreach (var id in pathIds)
        {
            // An id that cannot be mapped is skipped rather than throwing: a partially-resolvable path
            // still yields the ancestors that are known.
            if (idToKey.TryGetValue(id, out var key))
            {
                result.Add(key);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public LibraryNodeKind GetNodeKind(Guid nodeKey) =>
        // GetObjectType reads the node's stored object type and materializes no entity, so unlike the
        // GetAll(type, ids) overload it is safe for element containers — and it answers in one call
        // rather than probing each object type in turn.
        entityService.GetObjectType(nodeKey) switch
        {
            UmbracoObjectTypes.Element => LibraryNodeKind.Item,
            UmbracoObjectTypes.ElementContainer => LibraryNodeKind.Folder,

            // Anything else — including a content key handed to a Library tool by mistake — is reported
            // as Unknown rather than guessed at. The caller then simply makes no applicability claim,
            // which is the safe failure: an unhelpful answer instead of a confidently wrong one.
            _ => LibraryNodeKind.Unknown,
        };
}
