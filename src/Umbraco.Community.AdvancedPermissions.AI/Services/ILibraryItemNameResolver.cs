using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Finds a Library item or folder by the name an editor uses for it. The copilot is not offered in the
/// Library section, so it never has the key of the Library node in front of the editor; without this it
/// had to ask the editor for a key, which editors do not know (found in the live copilot check).
/// </summary>
/// <remarks>
/// <para>
/// An exact name (ignoring case and surrounding spaces) wins. Several exact matches, or no exact match but
/// similar names, come back as candidates for the editor to choose from — never a guess, because an answer
/// about the wrong node is worse than a question.
/// </para>
/// <para>
/// Someone without access to the Users section may only ask about their own access, and only finds the
/// nodes their Library start nodes let them see (including the folders above a start node, which Umbraco
/// shows for browsing). A name search must not reveal the Library items Umbraco hides from them.
/// </para>
/// </remarks>
public interface ILibraryItemNameResolver
{
    /// <summary>Looks a Library item or folder up by name.</summary>
    /// <param name="name">The name the editor used.</param>
    /// <returns>The node's key, the candidates to choose from, or an error.</returns>
    Task<LibraryItemLookup> ResolveAsync(string name);
}
