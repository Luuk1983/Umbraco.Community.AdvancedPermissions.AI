using Umbraco.Community.AdvancedPermissions.AI.Knowledge;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="EditorsReference"/>, the content of the "Permission editors and viewers"
/// knowledge item.
/// </summary>
/// <remarks>
/// <para>
/// These tests hold a requirement that is easy to satisfy badly: the reference must explain the
/// DIFFERENCES between the four editors and viewers, not merely describe each one. Four surfaces whose
/// names combine "permissions", "editor" and "viewer" are individually easy to summarise and collectively
/// easy to confuse, and the failure worth preventing is the copilot answering about the wrong screen — or
/// sending someone down a menu path that does not exist, as the v17 release did ("Content Permissions" in
/// the sidebar).
/// </para>
/// <para>
/// So the assertions are structural rather than a spot-check of wording: every surface must be present,
/// every surface must state what it is NOT and name a neighbour to use instead, and both contrast pairs
/// must actually be stated.
/// </para>
/// </remarks>
public sealed class EditorsReferenceTests
{
    /// <summary>The editors reference, exactly as the knowledge item renders it.</summary>
    private static EditorGuide Guide => EditorsReference.Content;

    /// <summary>The four surfaces the base package registers on v17, by their own menu labels.</summary>
    /// <returns>Each expected surface name.</returns>
    public static TheoryData<string> ExpectedSurfaces() =>
    [
        "Content Permissions Editor",
        "Access Viewer",
        "Document Type Permissions Editor",
        "Insert Options Viewer",
    ];

    /// <summary>
    /// All four surfaces are covered — and exactly four, so a fifth added to the base package without
    /// updating this reference is caught rather than silently omitted.
    /// </summary>
    [Fact]
    public void Guide_CoversExactlyTheFourSurfaces()
    {
        var guide = Guide;

        Assert.Equal(4, guide.Surfaces.Count);
        Assert.Equal(
            ExpectedSurfaces().Cast<object[]>().Select(r => (string)r[0]).OrderBy(n => n),
            guide.Surfaces.Select(s => s.Name).OrderBy(n => n));
    }

    /// <summary>Each surface is present by its on-screen name.</summary>
    /// <param name="name">The surface name.</param>
    [Theory]
    [MemberData(nameof(ExpectedSurfaces))]
    public void Guide_IncludesSurface(string name)
    {
        var guide = Guide;

        Assert.Contains(guide.Surfaces, s => s.Name == name);
    }

    /// <summary>Every surface states what it is not, and names another surface to use instead.</summary>
    /// <param name="name">The surface name.</param>
    [Theory]
    [MemberData(nameof(ExpectedSurfaces))]
    public void EverySurface_StatesWhatItIsNotAndNamesAnAlternative(string name)
    {
        var guide = Guide;
        var surface = Assert.Single(guide.Surfaces, s => s.Name == name);

        Assert.False(string.IsNullOrWhiteSpace(surface.NotThis), $"{name} does not say what it is not.");

        // It must name at least one OTHER surface as the alternative — a boundary with nothing on the
        // other side of it does not help anyone choose. ("Access Viewer" is a substring of nothing else,
        // and no surface name contains another, so a plain substring check is exact here.)
        var others = guide.Surfaces.Where(s => s.Name != name).Select(s => s.Name);
        Assert.True(
            others.Any(other => surface.NotThis.Contains(other, StringComparison.OrdinalIgnoreCase)),
            $"{name} states a boundary but names no alternative surface to use instead: {surface.NotThis}");
    }

    /// <summary>Every surface carries a purpose, a when-to-use and a navigation path in the Users section.</summary>
    /// <param name="name">The surface name.</param>
    [Theory]
    [MemberData(nameof(ExpectedSurfaces))]
    public void EverySurface_HasPurposeUseWhenAndNavigation(string name)
    {
        var guide = Guide;
        var surface = Assert.Single(guide.Surfaces, s => s.Name == name);

        Assert.False(string.IsNullOrWhiteSpace(surface.Purpose));
        Assert.False(string.IsNullOrWhiteSpace(surface.UseWhen));
        Assert.Contains("Users section", surface.Navigation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Each navigation path names the sidebar group the base package actually registers on v17
    /// (<c>manifests.ts</c>: "Editors" and "Viewers") and the menu item itself.
    /// </summary>
    /// <param name="name">The surface name.</param>
    /// <param name="group">The sidebar group.</param>
    /// <param name="menuItem">The menu item label.</param>
    [Theory]
    [InlineData("Content Permissions Editor", "Editors", "Content Permissions Editor")]
    [InlineData("Access Viewer", "Viewers", "Access Viewer")]
    [InlineData("Document Type Permissions Editor", "Editors", "Document Type Permissions Editor")]
    [InlineData("Insert Options Viewer", "Viewers", "Insert Options Viewer")]
    public void EverySurface_NavigationMatchesTheRealSidebar(string name, string group, string menuItem)
    {
        var guide = Guide;
        var surface = Assert.Single(guide.Surfaces, s => s.Name == name);

        Assert.Equal($"Users section → {group} → {menuItem}", surface.Navigation);
    }

    /// <summary>The editor-versus-viewer contrast is stated in substance.</summary>
    [Fact]
    public void Guide_ExplainsEditorVersusViewer()
    {
        var text = Guide.EditorVersusViewer;

        Assert.Contains("effective", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reasoning", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("read-only", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one user group", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The permissions-versus-Insert-Options contrast is stated, including that an Allow only keeps a type
    /// and a Deny takes it away.
    /// </summary>
    [Fact]
    public void Guide_ExplainsPermissionsVersusCreateFilters()
    {
        var text = Guide.PermissionsVersusCreateFilters;

        Assert.Contains("filter, not a grant", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only KEEPS", text, StringComparison.Ordinal);
        Assert.Contains("never make a type appear", text, StringComparison.Ordinal);
        Assert.Contains("A Deny takes it away", text, StringComparison.Ordinal);
    }

    /// <summary>Choosing a screen asks both questions: which mechanism, and change or just see.</summary>
    [Fact]
    public void Guide_HowToChoose_AsksBothQuestions()
    {
        var text = Guide.HowToChoose;

        Assert.Contains("mechanism", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("change it or just see it", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("four", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The overall navigation names both sidebar groups and the Users-section requirement.</summary>
    /// <param name="group">A sidebar group.</param>
    [Theory]
    [InlineData("Editors")]
    [InlineData("Viewers")]
    public void Guide_Navigation_NamesBothSidebarGroups(string group)
    {
        var text = Guide.Navigation;

        Assert.Contains("Users section", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"'{group}'", text, StringComparison.Ordinal);
        Assert.Contains("Only people with access to the Users section", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The v17 release's invented menu path must not come back: there is no "Content Permissions" group
    /// in the v17 sidebar. Checked on the rendered markdown, which is what the model reads (the JSON the
    /// tool used to return escaped both the arrow and the quotes, so this check could never fail there).
    /// </summary>
    [Fact]
    public void Guide_NeverNamesTheInventedSidebarGroup()
    {
        var markdown = EditorsReference.ToMarkdown();

        Assert.DoesNotContain("→ Content Permissions →", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("'Content Permissions'", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rendered knowledge must not leak raw identifiers: an editor reads this prose verbatim, so a verb
    /// id or an alias would be noise at best and confusing at worst.
    /// </summary>
    [Fact]
    public void Guide_NeverLeaksRawIdentifiers()
    {
        var markdown = EditorsReference.ToMarkdown();

        Assert.DoesNotContain("Umb.Document.", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("$everyone", markdown, StringComparison.Ordinal);
    }

    /// <summary>v17 has no Library, so the guide never mentions one.</summary>
    [Fact]
    public void Guide_NeverMentionsTheLibrary()
    {
        var markdown = EditorsReference.ToMarkdown();

        Assert.DoesNotContain("Library", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("element type", markdown, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Both viewers explain start-node blocked cells.</summary>
    /// <param name="name">A viewer surface.</param>
    [Theory]
    [InlineData("Access Viewer")]
    [InlineData("Insert Options Viewer")]
    public void ViewerSurfaces_MentionStartNodeBlocks(string name)
    {
        var surface = Assert.Single(Guide.Surfaces, s => s.Name == name);
        var text = surface.Purpose + " " + surface.UseWhen + " " + surface.NotThis;

        Assert.Contains("start node", text, StringComparison.Ordinal);
        Assert.Contains("blocked", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The editor-versus-viewer contrast explains that a blocked cell is neither an Allow nor a Deny.</summary>
    [Fact]
    public void EditorVersusViewer_ExplainsBlockedCells()
    {
        var guide = Guide;

        Assert.Contains("Blocked (start nodes)", guide.EditorVersusViewer, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two reference tools were replaced by knowledge items, so the content must not send the model to
    /// a tool that no longer exists.
    /// </summary>
    [Fact]
    public void Content_NamesNoRemovedTool()
    {
        var markdown = EditorsReference.ToMarkdown();

        Assert.DoesNotContain("uap_explain_concepts", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("uap_explain_editors", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rule questions are handed to the concepts knowledge item by its name: the hand-off the removed
    /// tool's description used to carry.
    /// </summary>
    [Fact]
    public void Content_PointsRuleQuestionsAtTheConceptsKnowledgeItem()
    {
        Assert.Contains("'Permission concepts'", EditorsReference.ToMarkdown(), StringComparison.Ordinal);
    }
}
