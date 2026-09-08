namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// What kind of node a Library key identifies. The Library tree holds two kinds, and which one a node is
/// changes <i>which permissions even apply to it</i>.
/// </summary>
/// <remarks>
/// <para>
/// This is not cosmetic. The base package's Library editors show a hatched <b>N/A</b> cell for
/// combinations that have no meaning — "Create" on a single item (an item cannot contain other items),
/// and the item-only actions (Publish, Unpublish, Duplicate, Rollback) on a folder, which apply only to
/// the items inside it. The resolver still returns a value for those cells, and reporting it would tell
/// an editor "Publish is denied on this folder" when the product itself says the question does not apply.
/// Knowing the kind is what lets the copilot say "not applicable" instead of a misleading "denied".
/// </para>
/// </remarks>
public enum LibraryNodeKind
{
    /// <summary>
    /// The key could not be found in the Library tree, so no applicability judgement can be made. The
    /// safe default: report the resolved permissions without claiming anything is not applicable.
    /// </summary>
    Unknown,

    /// <summary>
    /// A Library item (an element, <c>UmbracoObjectTypes.Element</c>). It holds content but cannot
    /// contain other nodes, so the Create permission has no meaning on it.
    /// </summary>
    Item,

    /// <summary>
    /// A Library folder (an element container, <c>UmbracoObjectTypes.ElementContainer</c>). It contains
    /// other nodes but is not itself a piece of content, so the item-only actions — Publish, Unpublish,
    /// Duplicate and Rollback — do not apply to the folder itself, only to the items inside it.
    /// </summary>
    Folder,
}
