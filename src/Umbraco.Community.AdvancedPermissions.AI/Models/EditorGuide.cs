namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// One of the package's four editors and viewers, described so it can be told apart from its neighbours
/// rather than merely summarised.
/// </summary>
/// <remarks>
/// <see cref="NotThis"/> is the load-bearing field. Surfaces whose names share the words "permissions",
/// "editor" and "viewer" are easy to describe individually and easy to confuse with each other, and
/// pointing an editor at the wrong one wastes their time and produces a confidently wrong answer. Every
/// entry therefore states what it is <i>not</i> and names the neighbour to use instead.
/// </remarks>
/// <param name="Name">The surface's own menu label, as it appears in the Users section sidebar.</param>
/// <param name="Purpose">What it is for, in one sentence.</param>
/// <param name="UseWhen">The question it answers — the cue for choosing it.</param>
/// <param name="NotThis">
/// What this surface does <b>not</b> do, and which of the other three to use for that instead.
/// </param>
/// <param name="Navigation">Where to find it in the backoffice.</param>
internal sealed record EditorSurface(
    string Name,
    string Purpose,
    string UseWhen,
    string NotThis,
    string Navigation);

/// <summary>
/// The reference for the package's four editors and viewers: how to choose between them, the two
/// distinctions that separate them, what each one does and does not do, and where to find them.
/// </summary>
/// <remarks>
/// Structured so that the <i>boundaries</i> come first and the per-surface descriptions come last. The
/// failure this reference exists to prevent is not "the model cannot describe the Access Viewer", it is
/// "the model answers about the wrong screen" — or describes a menu path that does not exist, as the
/// concepts reference (then a tool) did on the first v17 release. Being told the distinction does more than reading four
/// descriptions.
/// </remarks>
/// <param name="HowToChoose">
/// The two questions that pick a surface, so the choice is made deliberately rather than by matching a
/// name.
/// </param>
/// <param name="EditorVersusViewer">
/// The most-confused distinction: an editor shows what one user group has stored; a viewer shows what a
/// subject effectively gets across all of their groups, with reasoning.
/// </param>
/// <param name="PermissionsVersusCreateFilters">
/// Why an Allow entry means something different in the two mechanisms: content permissions are denied
/// unless allowed, while Insert Options are allowed unless denied.
/// </param>
/// <param name="Surfaces">The four editors and viewers, each with its boundaries.</param>
/// <param name="Navigation">How the Users section sidebar is laid out, and who can open it.</param>
internal sealed record EditorGuide(
    string HowToChoose,
    string EditorVersusViewer,
    string PermissionsVersusCreateFilters,
    IReadOnlyList<EditorSurface> Surfaces,
    string Navigation);
