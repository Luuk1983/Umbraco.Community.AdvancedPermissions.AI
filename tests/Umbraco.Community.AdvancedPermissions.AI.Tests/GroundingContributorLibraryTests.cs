using Umbraco.AI.Core;
using Umbraco.AI.Core.RuntimeContext;
using Umbraco.Community.AdvancedPermissions.AI.Context;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for the Library half of <see cref="AdvancedPermissionsGroundingContributor"/>, added in the
/// v18 update when the grounding went from two parts to three.
/// </summary>
/// <remarks>
/// <para>
/// The composition rule these tests pin is that every conversation is a <b>strict superset</b> of the
/// always-on concepts half: a document conversation gets concepts + document guidance, a Library
/// conversation gets concepts + Library guidance, and anything else gets concepts alone. No conversation
/// may lose grounding to the split, and the two domain halves must never both appear — they point at
/// different tools for the same question.
/// </para>
/// <para>
/// They also guard the naming trap: <c>Umbraco.AI.Core</c> exposes both
/// <see cref="Constants.ContextKeys.EntityType"/> and <c>ContextKeys.ElementType</c>, and the latter
/// means a <i>block-editor</i> element type with nothing to do with the Library. Gating on the wrong key
/// would silently never fire.
/// </para>
/// </remarks>
public sealed class GroundingContributorLibraryTests
{
    /// <summary>The system under test.</summary>
    private readonly AdvancedPermissionsGroundingContributor _contributor = new();

    /// <summary>Builds an empty runtime context, ready to be primed.</summary>
    /// <returns>A fresh runtime context.</returns>
    private static AIRuntimeContext NewContext() => new([]);

    /// <summary>Contributes for the given focused entity type and returns the single grounding part.</summary>
    /// <param name="entityType">The focused entity type, or <see langword="null"/> to set none.</param>
    /// <returns>The contributed grounding text.</returns>
    private string Contribute(string? entityType)
    {
        var context = NewContext();
        if (entityType is not null)
        {
            context.SetValue(Constants.ContextKeys.EntityType, entityType);
        }

        _contributor.Contribute(context);

        return Assert.Single(context.SystemMessageParts);
    }

    /// <summary>
    /// The Library entity types Umbraco uses, taken from the base package's own condition classes. Both
    /// must trigger the Library half — a folder is as much a Library node as an item.
    /// </summary>
    /// <returns>Each Library entity type.</returns>
    public static TheoryData<string> LibraryEntityTypes() => ["element", "element-folder"];

    /// <summary>A Library conversation is pointed at the Library tool, not the content one.</summary>
    /// <param name="entityType">The focused Library entity type.</param>
    [Theory]
    [MemberData(nameof(LibraryEntityTypes))]
    public void Contribute_LibraryContext_PointsAtTheLibraryTool(string entityType)
    {
        var text = Contribute(entityType);

        Assert.Contains("uap_explain_library_access", text, StringComparison.Ordinal);
        Assert.Contains("LIBRARY", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Library conversation must still receive the whole concepts half — the terminology rules, the
    /// read-only stance and the tool pointers govern every answer regardless of domain.
    /// </summary>
    /// <param name="entityType">The focused Library entity type.</param>
    [Theory]
    [MemberData(nameof(LibraryEntityTypes))]
    public void Contribute_LibraryContext_IsAStrictSupersetOfConcepts(string entityType)
    {
        var conceptsOnly = Contribute("media");
        var library = Contribute(entityType);

        Assert.StartsWith(conceptsOnly, library, StringComparison.Ordinal);
        Assert.True(library.Length > conceptsOnly.Length, "The Library half added nothing.");
    }

    /// <summary>
    /// The two domain halves are mutually exclusive: they point at different tools for the same question,
    /// so contributing both would tell the model to use the content tool on a Library node.
    /// </summary>
    /// <param name="entityType">The focused Library entity type.</param>
    [Theory]
    [MemberData(nameof(LibraryEntityTypes))]
    public void Contribute_LibraryContext_DoesNotAlsoGetTheDocumentHalf(string entityType)
    {
        var text = Contribute(entityType);

        // The document half's distinctive instruction — "the editor appears read-only" — must be absent.
        Assert.DoesNotContain("the editor appears read-only", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A document conversation must not pick up the Library half either.</summary>
    [Fact]
    public void Contribute_DocumentContext_DoesNotGetTheLibraryHalf()
    {
        var text = Contribute("document");

        Assert.DoesNotContain("uap_explain_library_access", text, StringComparison.Ordinal);
        Assert.Contains("uap_explain_access", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Library half must carry the N/A rule, because it has no content-tree equivalent: without it
    /// the copilot would relay "Publish is denied on this folder" for a combination the product treats as
    /// meaningless, and send someone hunting for an entry that does not exist.
    /// </summary>
    /// <param name="entityType">The focused Library entity type.</param>
    [Theory]
    [MemberData(nameof(LibraryEntityTypes))]
    public void Contribute_LibraryContext_CarriesTheNotApplicableRule(string entityType)
    {
        var text = Contribute(entityType);

        Assert.Contains("Not applicable", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never as denied", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The Library half must keep the confirmed-remediation discipline the document half established:
    /// suggestFix only, GrantedBy relayed, Caution relayed.
    /// </summary>
    [Fact]
    public void Contribute_LibraryContext_KeepsTheConfirmedRemediationRules()
    {
        var text = Contribute("element");

        Assert.Contains("suggestFix=true", text, StringComparison.Ordinal);
        Assert.Contains("GrantedBy", text, StringComparison.Ordinal);
        Assert.Contains("Caution", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Library half must say the element-type aspect is section-wide and takes no node, since that is
    /// the single most surprising thing about it.
    /// </summary>
    [Fact]
    public void Contribute_LibraryContext_ExplainsTheSectionWideElementTypeAspect()
    {
        var text = Contribute("element");

        Assert.Contains("element-type-create", text, StringComparison.Ordinal);
        Assert.Contains("section-wide", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// THE NAMING TRAP. The gate must read <see cref="Constants.ContextKeys.EntityType"/>, never
    /// <c>ContextKeys.ElementType</c> — which carries a block-editor element type and is unrelated to the
    /// Library. Setting only the wrong key must NOT trigger the Library half.
    /// </summary>
    [Fact]
    public void Contribute_ElementTypeKeySetInstead_DoesNotTriggerTheLibraryHalf()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.ElementType, "element");

        _contributor.Contribute(context);

        var text = Assert.Single(context.SystemMessageParts);
        Assert.DoesNotContain("uap_explain_library_access", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The always-on half must point at both reference tools and name the two trees, because a
    /// conversation with no focused entity is where the model is most likely to guess.
    /// </summary>
    [Fact]
    public void Contribute_NoEntityType_StillPointsAtBothReferenceTools()
    {
        var text = Contribute(null);

        Assert.Contains("uap_explain_concepts", text, StringComparison.Ordinal);
        Assert.Contains("uap_explain_editors", text, StringComparison.Ordinal);
        Assert.Contains("Library", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The always-on half must carry the Library terminology, since it governs every sentence — including
    /// answers that paraphrase a tool's output without a Library node in context.
    /// </summary>
    [Fact]
    public void Contribute_AnyContext_CarriesLibraryTerminology()
    {
        var text = Contribute("media");

        Assert.Contains("Library Item", text, StringComparison.Ordinal);
        Assert.Contains("element types", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// An unknown entity type falls back to concepts only, and never throws — the contributor runs with no
    /// try/catch around it in Umbraco AI, so a failure here would abort the whole agent run.
    /// </summary>
    [Fact]
    public void Contribute_UnknownEntityType_AppendsConceptsOnlyWithoutThrowing()
    {
        var conceptsOnly = Contribute("media");

        Assert.Equal(conceptsOnly, Contribute("some-future-entity-type"));
    }
}
