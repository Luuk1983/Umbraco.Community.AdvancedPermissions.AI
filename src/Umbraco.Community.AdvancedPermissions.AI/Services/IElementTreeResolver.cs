using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Reads the structure of the <b>Library</b> tree: the ordered root-to-node key path the permission
/// resolver needs in order to walk inheritance, and whether a given key is an item or a folder.
/// </summary>
/// <remarks>
/// <para>
/// The Library counterpart to <see cref="IContentPathResolver"/>, and deliberately a separate interface
/// rather than the content resolver taking a <see cref="PermissionDomain"/>: these are different logic,
/// not one logic parameterised. A Library path mixes elements and element folders, so both the path
/// lookup and the id-to-key mapping have to span two Umbraco object types, and one of the obvious
/// <c>IEntityService</c> overloads cannot be used at all for element containers (see
/// <see cref="ElementTreeResolver"/>). Keeping them apart also avoids a breaking rename of the existing
/// content contract.
/// </para>
/// <para>
/// Both members are here because both answer the same question — "what does this key mean in the Library
/// tree?" — and both need the same two-object-type probing expertise. Splitting them would duplicate that
/// knowledge across two components for no gain.
/// </para>
/// </remarks>
public interface IElementTreeResolver
{
    /// <summary>
    /// Resolves the ordered list of node keys from the Library root down to (and including) the given
    /// element or element folder.
    /// </summary>
    /// <param name="nodeKey">The key of the target element or element folder.</param>
    /// <returns>
    /// The ordered keys, root first and target last, with Umbraco's <c>-1</c> root sentinel excluded.
    /// Empty when the key cannot be found in the Library tree — never a partial or invented path, so a
    /// caller can never end up explaining permissions for the wrong node.
    /// </returns>
    IReadOnlyList<Guid> GetPathFromRoot(Guid nodeKey);

    /// <summary>
    /// Determines whether a Library key is an item or a folder, which decides <i>which permissions apply
    /// to it at all</i>.
    /// </summary>
    /// <remarks>
    /// Needed so the copilot can report "not applicable" for the combinations the base package's own
    /// editors show as a hatched N/A cell, instead of parroting the resolved value and telling an editor
    /// that (say) Publish is denied on a folder — a claim the product itself does not make.
    /// </remarks>
    /// <param name="nodeKey">The key of the target element or element folder.</param>
    /// <returns>
    /// The node's kind, or <see cref="LibraryNodeKind.Unknown"/> when the key is not in the Library tree.
    /// </returns>
    LibraryNodeKind GetNodeKind(Guid nodeKey);
}
