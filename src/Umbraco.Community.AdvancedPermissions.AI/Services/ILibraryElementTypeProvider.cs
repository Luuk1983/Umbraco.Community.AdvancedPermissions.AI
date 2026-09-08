using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Enumerates the element types that can be created in the Library — the candidate set the Library
/// Element Type Permissions editor and Library Insert Viewer list.
/// </summary>
/// <remarks>
/// A named service rather than an inline LINQ query in the tool, because the definition of "a Library
/// element type" is a base-package/Umbraco rule (<c>IsElement &amp;&amp; AllowedInLibrary</c>, mirroring
/// the base package's own <c>doc-type-permissions/element-types</c> endpoint) and getting it wrong would
/// silently change which types the copilot reports on. Naming it also makes it substitutable in tests
/// without mocking the whole content-type service.
/// </remarks>
public interface ILibraryElementTypeProvider
{
    /// <summary>
    /// Gets every element type that is creatable in the Library, ordered by display name so answers are
    /// stable between calls.
    /// </summary>
    /// <returns>
    /// The candidate element types. Empty when the site has no element types marked as allowed in the
    /// Library — a legitimate state, not an error, and one the copilot should report plainly.
    /// </returns>
    IReadOnlyList<IContentType> GetLibraryElementTypes();
}
