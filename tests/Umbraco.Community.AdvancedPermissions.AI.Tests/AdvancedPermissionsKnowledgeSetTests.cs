using System.Reflection;
using System.Text.RegularExpressions;
using Umbraco.AI.Core.Contexts.KnowledgeSets;
using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Knowledge;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="AdvancedPermissionsKnowledgeSet"/>, the Umbraco AI knowledge set that replaced
/// the <c>uap_explain_concepts</c> and <c>uap_explain_editors</c> tools.
/// </summary>
/// <remarks>
/// <para>
/// Umbraco AI discovers the set by type scan and lists each item in the system prompt of every chat
/// request (name, description and a resource id derived from the set id and item key). The model then
/// fetches the full text with <c>get_context_resource</c>. So the things worth pinning are: that the scan
/// can find it at all (a discovery miss is silent), that the keys never drift (they seed the resource id),
/// that the descriptions give the model a reason to fetch, and that the rendered text drops nothing from
/// the structured source.
/// </para>
/// </remarks>
public sealed class AdvancedPermissionsKnowledgeSetTests
{
    /// <summary>The system under test.</summary>
    private readonly AdvancedPermissionsKnowledgeSet _sut = new();

    /// <summary>Returns the set's items through the <see cref="IAIKnowledgeSet"/> contract, as Umbraco AI does.</summary>
    /// <returns>The items, in order.</returns>
    private Task<IReadOnlyList<AIKnowledgeSetItem>> GetItemsAsync() =>
        ((IAIKnowledgeSet)_sut).GetItemsAsync(CancellationToken.None);

    /// <summary>
    /// Umbraco AI finds knowledge sets with a type scan for <c>[AIKnowledgeSet]</c> on a concrete
    /// <see cref="IAIKnowledgeSet"/>, and creates them through DI. A non-public, abstract or attribute-less
    /// class is simply never listed, with no error anywhere, so the shape is pinned here.
    /// </summary>
    [Fact]
    public void KnowledgeSet_IsDiscoverable()
    {
        var type = typeof(AdvancedPermissionsKnowledgeSet);

        Assert.True(type.IsPublic);
        Assert.False(type.IsAbstract);
        Assert.True(typeof(IAIKnowledgeSet).IsAssignableFrom(type));
        Assert.NotNull(type.GetCustomAttribute<AIKnowledgeSetAttribute>());
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));

        Assert.Equal("advanced-permissions", _sut.Id);
        Assert.Equal("Advanced Permissions", _sut.Name);
        Assert.False(string.IsNullOrWhiteSpace(_sut.Description));
    }

    /// <summary>
    /// The item keys seed Umbraco's deterministic resource id, so renaming one silently changes the id the
    /// model was given. They must also be URL-safe, because the admin API puts them in a URL.
    /// </summary>
    [Fact]
    public async Task Items_HaveTheStableKeysAndNames()
    {
        var items = await GetItemsAsync();

        Assert.Equal(["concepts", "editors"], items.Select(i => i.Key));
        Assert.Equal(["Permission concepts", "Permission editors and viewers"], items.Select(i => i.Name));
        Assert.All(items, i => Assert.Matches(new Regex("^[a-z0-9-]+$"), i.Key));
    }

    /// <summary>
    /// The description is the only part of the item the model sees before it decides whether to fetch, so
    /// it has to name the topics a question would mention and say why general knowledge will not do.
    /// </summary>
    [Fact]
    public async Task ConceptsItem_DescriptionSaysToReadItInsteadOfGeneralKnowledge()
    {
        var description = (await GetItemsAsync())[0].Description;

        Assert.NotNull(description);
        Assert.Contains("Priority Override", description, StringComparison.Ordinal);
        Assert.Contains("start nodes", description, StringComparison.Ordinal);
        Assert.Contains("scopes", description, StringComparison.Ordinal);
        Assert.Contains("general knowledge", description, StringComparison.Ordinal);
    }

    /// <summary>
    /// On v17 the removed tool's description also routed "why is a page missing from the tree?" to the
    /// concepts reference (Read and start nodes decide what the tree shows). The item description carries
    /// the same cue, so that question still finds the reference.
    /// </summary>
    [Fact]
    public async Task ConceptsItem_DescriptionCoversMissingPages()
    {
        var description = (await GetItemsAsync())[0].Description;

        Assert.NotNull(description);
        Assert.Contains("missing from the tree", description, StringComparison.Ordinal);
    }

    /// <summary>
    /// The set and item descriptions are listed in the system prompt of EVERY Umbraco AI request on the
    /// site, including agents that were never allowed this package's tool scope. Naming a tool there would
    /// point those agents at something they cannot call, so the listed text names no tool at all.
    /// </summary>
    [Fact]
    public async Task ListedText_NamesNoTool()
    {
        var listed = (await GetItemsAsync()).Select(i => i.Description).Prepend(_sut.Description);

        Assert.All(listed, text => Assert.DoesNotContain("uap_", text, StringComparison.Ordinal));
    }

    /// <summary>"Where do I go?" and "which screen?" questions must recognise themselves in the editors item.</summary>
    [Fact]
    public async Task EditorsItem_DescriptionRoutesWhereAndWhichScreenQuestions()
    {
        var description = (await GetItemsAsync())[1].Description;

        Assert.NotNull(description);
        Assert.Contains("which screen", description, StringComparison.Ordinal);
        Assert.Contains("Users section", description, StringComparison.Ordinal);

        // v17 has four screens, not v18's eight: a wrong count invites the model to look for screens
        // that do not exist.
        Assert.Contains("four", description, StringComparison.Ordinal);
        Assert.DoesNotContain("eight", description, StringComparison.Ordinal);
    }

    /// <summary>What the model fetches is exactly the rendered reference, nothing added or lost on the way.</summary>
    [Fact]
    public async Task ItemContent_IsTheRenderedReference()
    {
        var items = await GetItemsAsync();

        Assert.Equal(ConceptsReference.ToMarkdown(), await items[0].GetContentAsync(CancellationToken.None));
        Assert.Equal(EditorsReference.ToMarkdown(), await items[1].GetContentAsync(CancellationToken.None));
    }

    /// <summary>
    /// A field added to the concepts record but forgotten in the renderer would silently vanish from what
    /// the model reads, so every string property must appear verbatim in the markdown.
    /// </summary>
    [Fact]
    public void ConceptsMarkdown_RendersEveryField()
    {
        var markdown = ConceptsReference.ToMarkdown();

        Assert.All(StringValues(ConceptsReference.Content), value => Assert.Contains(value, markdown, StringComparison.Ordinal));
    }

    /// <summary>The same guarantee for the editors guide, including every field of every surface.</summary>
    [Fact]
    public void EditorsMarkdown_RendersEveryFieldOfEverySurface()
    {
        var markdown = EditorsReference.ToMarkdown();
        var guide = EditorsReference.Content;

        Assert.All(
            StringValues(guide).Concat(guide.Surfaces.SelectMany(StringValues)),
            value => Assert.Contains(value, markdown, StringComparison.Ordinal));
    }

    /// <summary>
    /// The markdown is split under headings so the model can quote the part that answers the question
    /// rather than reciting the whole reference: one heading per concepts section, one per screen.
    /// </summary>
    [Fact]
    public void Markdown_HasAHeadingPerSection()
    {
        var concepts = ConceptsReference.ToMarkdown();
        var editors = EditorsReference.ToMarkdown();

        Assert.StartsWith("# Permission concepts", concepts, StringComparison.Ordinal);
        Assert.Equal(8, Lines(concepts).Count(l => l.StartsWith("## ", StringComparison.Ordinal)));

        Assert.StartsWith("# Permission editors and viewers", editors, StringComparison.Ordinal);
        Assert.All(
            EditorsReference.Content.Surfaces,
            s => Assert.Contains($"### {s.Name}", Lines(editors)));
    }

    /// <summary>
    /// The knowledge set replaces the two reference tools; it does not sit beside them. Leaving a tool
    /// registered would give the model two sources for the same answer, one of which only works when the
    /// agent's tool scope is allowed.
    /// </summary>
    /// <param name="toolId">A tool id that the knowledge set replaced.</param>
    [Theory]
    [InlineData("uap_explain_concepts")]
    [InlineData("uap_explain_editors")]
    public void ReplacedTool_IsNoLongerRegistered(string toolId)
    {
        var registered = typeof(AdvancedPermissionsKnowledgeSet).Assembly
            .GetTypes()
            .Select(t => t.GetCustomAttribute<AIToolAttribute>()?.Id)
            .Where(id => id is not null);

        Assert.DoesNotContain(toolId, registered);
    }

    /// <summary>Reads every public string property of a record.</summary>
    /// <param name="value">The record.</param>
    /// <returns>The non-empty string values.</returns>
    private static IEnumerable<string> StringValues(object value) =>
        value.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => (string)p.GetValue(value)!)
            .Where(s => !string.IsNullOrEmpty(s));

    /// <summary>Splits markdown into lines.</summary>
    /// <param name="markdown">The markdown.</param>
    /// <returns>The lines.</returns>
    private static string[] Lines(string markdown) => markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
}
