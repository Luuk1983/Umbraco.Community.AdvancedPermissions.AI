namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// One Library item or folder that matches a name the editor gave, with enough context for the editor to
/// tell same-named nodes apart.
/// </summary>
/// <param name="Name">The node's name as shown in the Library.</param>
/// <param name="Kind">"Item" or "Folder".</param>
/// <param name="Location">The folders it sits in, e.g. "Marketing / Banners", or "Library root".</param>
/// <param name="NodeKey">The node's key, to pass back as <c>nodeKey</c> once the editor has chosen.</param>
public sealed record LibraryItemCandidate(string Name, string Kind, string Location, Guid NodeKey);

/// <summary>
/// Returned instead of an access answer when a Library name does not identify exactly one node: several
/// nodes share the name, or none has it but some have a similar one. The model must ask the editor which
/// one they mean rather than pick one itself.
/// </summary>
/// <param name="Message">What happened and what to do next.</param>
/// <param name="Candidates">The matching nodes, at most <see cref="MaxCandidates"/>.</param>
public sealed record LibraryItemCandidates(string Message, IReadOnlyList<LibraryItemCandidate> Candidates)
{
    /// <summary>The most candidates ever listed, so a vague name cannot flood the conversation.</summary>
    public const int MaxCandidates = 10;
}

/// <summary>
/// The outcome of looking a Library item or folder up by name. Exactly one field is set: the node's key
/// when the name identifies one node, the candidates to choose from, or an error.
/// </summary>
/// <param name="NodeKey">The node the name identifies.</param>
/// <param name="Candidates">The nodes to choose from when the name is ambiguous or only similar.</param>
/// <param name="Error">Why nothing was found.</param>
public sealed record LibraryItemLookup(Guid? NodeKey, LibraryItemCandidates? Candidates, string? Error);
