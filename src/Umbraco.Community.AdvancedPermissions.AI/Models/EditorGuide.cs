namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// One of the package's eight editors and viewers, described so it can be told apart from its
/// neighbours rather than merely summarised.
/// </summary>
/// <remarks>
/// <see cref="NotThis"/> is the load-bearing field. Eight surfaces whose names share the words
/// "permissions", "access", "editor" and "viewer" are easy to describe individually and easy to confuse
/// with each other, and pointing an editor at the wrong one wastes their time and produces a confidently
/// wrong answer. Every entry therefore states what it is <i>not</i> and names the neighbour to use
/// instead.
/// </remarks>
/// <param name="Name">The surface's own title, as it appears at the top of the screen.</param>
/// <param name="Purpose">What it is for, in one sentence.</param>
/// <param name="UseWhen">The question it answers — the cue for choosing it.</param>
/// <param name="NotThis">
/// What this surface does <b>not</b> do, and which of the other seven to use for that instead.
/// </param>
/// <param name="Navigation">Where to find it in the backoffice.</param>
public sealed record EditorSurface(
    string Name,
    string Purpose,
    string UseWhen,
    string NotThis,
    string Navigation);

/// <summary>
/// The reference for the package's eight editors and viewers: how to choose between them, the three
/// distinctions that separate them, what each one does and does not do, and where to find them.
/// </summary>
/// <remarks>
/// <para>
/// Structured so that the <i>boundaries</i> come first and the per-surface descriptions come last. That
/// ordering is deliberate: the failure this reference exists to prevent is not "the model cannot describe
/// the Library Permissions Editor", it is "the model answers about the wrong one of eight similarly-named
/// screens". Reading eight descriptions does not reliably produce the distinction; being told the
/// distinction does.
/// </para>
/// <para>
/// Split from <see cref="PermissionConcepts"/> because the two answer different questions. The permission
/// <i>model</i> — Allow/Deny, scopes, precedence, Priority Override — did not change between the base
/// package's v17 and v18 lines (its <c>concepts.md</c> is byte-identical); what grew from four surfaces
/// to eight is the set of <i>editors</i>. Keeping them apart means "what is a Priority Override?" does not
/// pay for a tour of the backoffice, and vice versa.
/// </para>
/// </remarks>
/// <param name="HowToChoose">
/// The three questions that pick a surface, so the choice is made deliberately rather than by matching a
/// name.
/// </param>
/// <param name="EditorVersusViewer">
/// The most-confused distinction: an editor shows what one user group has stored; a viewer shows what a
/// subject effectively gets across all of their groups, with reasoning.
/// </param>
/// <param name="PermissionsVersusCreateFilters">
/// Why an Allow entry means something different in the two mechanisms: tree permissions are denied unless
/// allowed, while the create filters are allowed unless denied.
/// </param>
/// <param name="PerNodeVersusSectionWide">
/// Why one of the eight has no tree at all: element-type creation is decided once for the whole Library.
/// </param>
/// <param name="Surfaces">All eight editors and viewers, each with its own boundary.</param>
/// <param name="Navigation">The backoffice path shared by all eight.</param>
public sealed record EditorGuide(
    string HowToChoose,
    string EditorVersusViewer,
    string PermissionsVersusCreateFilters,
    string PerNodeVersusSectionWide,
    IReadOnlyList<EditorSurface> Surfaces,
    string Navigation);
