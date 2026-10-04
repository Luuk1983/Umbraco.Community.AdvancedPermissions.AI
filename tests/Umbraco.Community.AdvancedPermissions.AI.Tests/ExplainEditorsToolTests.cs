using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Tools;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="ExplainEditorsTool"/>.
/// </summary>
/// <remarks>
/// <para>
/// These tests hold a requirement that is easy to satisfy badly: the reference must explain the
/// DIFFERENCES between the eight editors and viewers, not merely describe each one. Eight surfaces whose
/// names all combine "permissions", "access", "editor" and "viewer" are individually easy to summarise
/// and collectively easy to confuse, and the failure worth preventing is the copilot answering about the
/// wrong screen.
/// </para>
/// <para>
/// So the assertions are structural rather than a spot-check of wording: every surface must be present,
/// every surface must state what it is NOT and name a neighbour to use instead, and all three contrast
/// pairs must actually be stated. A future edit that turns this back into eight blurbs fails here.
/// </para>
/// </remarks>
public sealed class ExplainEditorsToolTests
{
    /// <summary>The system under test.</summary>
    private readonly ExplainEditorsTool _sut = new();

    /// <summary>Runs the tool through its public interface and returns the guide.</summary>
    /// <returns>The editor guide.</returns>
    private async Task<EditorGuide> GetGuideAsync() =>
        Assert.IsType<EditorGuide>(await ((IAITool)_sut).ExecuteAsync(new ExplainEditorsArgs(), CancellationToken.None));

    /// <summary>The eight surfaces the base package registers, by their own on-screen titles.</summary>
    /// <returns>Each expected surface name.</returns>
    public static TheoryData<string> ExpectedSurfaces() =>
    [
        "Content Permissions Editor",
        "Content Access Viewer",
        "Document Type Permissions Editor",
        "Document Type Access Viewer",
        "Library Permissions Editor",
        "Library Access Viewer",
        "Library Element Type Permissions Editor",
        "Library Element Type Access Viewer",
    ];

    /// <summary>
    /// All eight surfaces are covered — and exactly eight, so a ninth added to the base package without
    /// updating this reference is caught rather than silently omitted.
    /// </summary>
    [Fact]
    public async Task Guide_CoversExactlyTheEightSurfaces()
    {
        var guide = await GetGuideAsync();

        Assert.Equal(8, guide.Surfaces.Count);
        Assert.Equal(
            ExpectedSurfaces().Cast<object[]>().Select(r => (string)r[0]).OrderBy(n => n),
            guide.Surfaces.Select(s => s.Name).OrderBy(n => n));
    }

    /// <summary>Each expected surface is present by name.</summary>
    /// <param name="name">The surface's on-screen title.</param>
    [Theory]
    [MemberData(nameof(ExpectedSurfaces))]
    public async Task Guide_IncludesSurface(string name)
    {
        var guide = await GetGuideAsync();

        Assert.Contains(guide.Surfaces, s => s.Name == name);
    }

    /// <summary>
    /// THE CORE REQUIREMENT. Every surface must say what it is NOT and point at another surface to use
    /// instead. A description that only says what a screen does is exactly what leaves the model choosing
    /// between eight similar names on vibes.
    /// </summary>
    /// <param name="name">The surface's on-screen title.</param>
    [Theory]
    [MemberData(nameof(ExpectedSurfaces))]
    public async Task EverySurface_StatesWhatItIsNotAndNamesAnAlternative(string name)
    {
        var guide = await GetGuideAsync();
        var surface = Assert.Single(guide.Surfaces, s => s.Name == name);

        Assert.False(string.IsNullOrWhiteSpace(surface.NotThis), $"{name} does not say what it is not.");

        // It must name at least one OTHER surface as the alternative — a boundary with nothing on the
        // other side of it does not help anyone choose.
        var others = guide.Surfaces.Where(s => s.Name != name).Select(s => s.Name);
        Assert.True(
            others.Any(other => surface.NotThis.Contains(other, StringComparison.OrdinalIgnoreCase)),
            $"{name} states a boundary but names no alternative surface to use instead: {surface.NotThis}");
    }

    /// <summary>Every surface carries the cue for choosing it, and its purpose.</summary>
    /// <param name="name">The surface's on-screen title.</param>
    [Theory]
    [MemberData(nameof(ExpectedSurfaces))]
    public async Task EverySurface_HasPurposeUseWhenAndNavigation(string name)
    {
        var guide = await GetGuideAsync();
        var surface = Assert.Single(guide.Surfaces, s => s.Name == name);

        Assert.False(string.IsNullOrWhiteSpace(surface.Purpose));
        Assert.False(string.IsNullOrWhiteSpace(surface.UseWhen));
        Assert.Contains("Users section", surface.Navigation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every surface's navigation must name its real sidebar group and its real menu item, verified
    /// against the base package's released v18.1.0 manifests: four groups, each holding exactly
    /// "Permissions Editor" and "Access Viewer".
    /// </summary>
    /// <param name="name">The surface's on-screen title.</param>
    /// <param name="group">The sidebar group label it must sit under.</param>
    /// <param name="menuItem">The menu item label it must be reached by.</param>
    [Theory]
    [InlineData("Content Permissions Editor", "Content Permissions", "Permissions Editor")]
    [InlineData("Content Access Viewer", "Content Permissions", "Access Viewer")]
    [InlineData("Document Type Permissions Editor", "Document Type Permissions", "Permissions Editor")]
    [InlineData("Document Type Access Viewer", "Document Type Permissions", "Access Viewer")]
    [InlineData("Library Permissions Editor", "Library Permissions", "Permissions Editor")]
    [InlineData("Library Access Viewer", "Library Permissions", "Access Viewer")]
    [InlineData("Library Element Type Permissions Editor", "Library Element Type Permissions", "Permissions Editor")]
    [InlineData("Library Element Type Access Viewer", "Library Element Type Permissions", "Access Viewer")]
    public async Task EverySurface_NavigationMatchesTheRealSidebar(string name, string group, string menuItem)
    {
        var guide = await GetGuideAsync();
        var surface = Assert.Single(guide.Surfaces, s => s.Name == name);

        Assert.Contains(group, surface.Navigation, StringComparison.Ordinal);
        Assert.Contains(menuItem, surface.Navigation, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Editor-versus-Viewer distinction must be stated explicitly, and must carry the substance that
    /// makes it useful: an editor shows ONE group's stored entries, a viewer shows the EFFECTIVE result
    /// combined across all of a subject's groups.
    /// </summary>
    [Fact]
    public async Task Guide_ExplainsEditorVersusViewer()
    {
        var text = (await GetGuideAsync()).EditorVersusViewer;

        Assert.Contains("effective", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reasoning", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("read-only", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one user group", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The two mechanisms default in opposite directions, and the reference must say so — this is what
    /// stops the copilot claiming an Allow entry in a create filter grants something.
    /// </summary>
    [Fact]
    public async Task Guide_ExplainsPermissionsVersusCreateFilters()
    {
        var text = (await GetGuideAsync()).PermissionsVersusCreateFilters;

        Assert.Contains("filter, not a grant", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("narrow", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The section-wide exception must be stated along with WHY it exists, so the copilot can explain that
    /// a per-folder element-type restriction is not expressible rather than inventing a way to do it.
    /// </summary>
    [Fact]
    public async Task Guide_ExplainsPerNodeVersusSectionWide()
    {
        var text = (await GetGuideAsync()).PerNodeVersusSectionWide;

        Assert.Contains("section-wide", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Library Element Type Permissions", text, StringComparison.Ordinal);

        // The WHY matters more than any particular phrasing: without it the copilot may invent a way to
        // scope an element type to one folder. Assert the substance (it is about the missing parent, and
        // it is structural), not an exact wording.
        Assert.Contains("parent", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("structural", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The disambiguation must lead with the three questions that actually pick a surface, since that —
    /// not the eight summaries — is what prevents a name-based guess.
    /// </summary>
    [Fact]
    public async Task Guide_HowToChoose_AsksAllThreeQuestions()
    {
        var text = (await GetGuideAsync()).HowToChoose;

        Assert.Contains("tree", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Library", text, StringComparison.Ordinal);
        Assert.Contains("mechanism", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Access Viewer", text, StringComparison.Ordinal);
    }

    /// <summary>The shared navigation names all four sidebar groups.</summary>
    /// <param name="group">A sidebar group label.</param>
    [Theory]
    [InlineData("Content Permissions")]
    [InlineData("Document Type Permissions")]
    [InlineData("Library Permissions")]
    [InlineData("Library Element Type Permissions")]
    public async Task Guide_Navigation_NamesEverySidebarGroup(string group)
    {
        var text = (await GetGuideAsync()).Navigation;

        Assert.Contains("Users section", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(group, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The description must route the model here for "where/which screen" questions and hand off the
    /// other question types, so the five tools do not compete for the same prompts.
    /// </summary>
    [Fact]
    public void Description_RoutesWhereQuestionsHereAndHandsOffTheRest()
    {
        var description = _sut.Description;

        Assert.Contains("uap_explain_concepts", description, StringComparison.Ordinal);
        Assert.Contains("uap_explain_access", description, StringComparison.Ordinal);
        Assert.Contains("uap_explain_library_access", description, StringComparison.Ordinal);
        Assert.Contains("eight", description, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The tool must not leak raw identifiers: an editor reads this prose verbatim, so a verb id or an
    /// entity-type string would be noise at best and confusing at worst.
    /// </summary>
    [Fact]
    public async Task Guide_NeverLeaksRawIdentifiers()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(await GetGuideAsync());

        Assert.DoesNotContain("Umb.Document.", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Umb.Element.", json, StringComparison.Ordinal);
        Assert.DoesNotContain("$everyone", json, StringComparison.Ordinal);
        Assert.DoesNotContain("element-folder", json, StringComparison.Ordinal);
    }
}
