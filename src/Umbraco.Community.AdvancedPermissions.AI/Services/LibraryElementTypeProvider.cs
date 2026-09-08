using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Default <see cref="ILibraryElementTypeProvider"/>, selecting element types exactly as the base
/// package's own <c>doc-type-permissions/element-types</c> endpoint does.
/// </summary>
/// <remarks>
/// The predicate is <c>IsElement &amp;&amp; AllowedInLibrary</c> — both flags are required. A document
/// type marked as an element type but not allowed in the Library is not creatable there, so listing it
/// would have the copilot discuss a permission the editor has no way to exercise. Keeping this identical
/// to the base package's endpoint is what makes the copilot's list match the editor's screen.
/// </remarks>
/// <param name="contentTypeService">The Umbraco content-type service used to enumerate content types.</param>
public sealed class LibraryElementTypeProvider(IContentTypeService contentTypeService) : ILibraryElementTypeProvider
{
    /// <inheritdoc />
    public IReadOnlyList<IContentType> GetLibraryElementTypes() =>
        contentTypeService
            .GetAll()
            .Where(ct => ct.IsElement && ct.AllowedInLibrary)
            .OrderBy(ct => ct.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
