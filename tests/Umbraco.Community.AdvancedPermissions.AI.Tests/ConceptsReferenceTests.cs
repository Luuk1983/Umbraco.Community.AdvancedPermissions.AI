using Umbraco.Community.AdvancedPermissions.AI.Knowledge;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="ConceptsReference"/>, the content of the "Permission concepts" knowledge
/// item. It carries the definitional half of the copilot's grounding — the half that used to sit in every conversation's system prompt. Each test pins
/// a fact the copilot was observed to get wrong (or would get wrong) when left to its own knowledge, so
/// the reference cannot silently lose the correction it exists to deliver.
/// </summary>
public sealed class ConceptsReferenceTests
{
    /// <summary>The concepts reference, exactly as the knowledge item renders it.</summary>
    private static PermissionConcepts Concepts => ConceptsReference.Content;

    /// <summary>
    /// Priority Override must be defined as a per-entry flag for when a user's groups disagree, and the
    /// inheritance reading must be ruled out explicitly. The copilot was observed defining it as a child
    /// entry overriding an inherited ancestor entry, which is ordinary precedence and not an override.
    /// </summary>
    [Fact]
    public void PriorityOverride_IsDefinedAsPerEntryFlagAndNotInheritance()
    {
        var concepts = Concepts;

        Assert.Contains("flag on a single entry", concepts.PriorityOverride, StringComparison.Ordinal);
        Assert.Contains("several user groups", concepts.PriorityOverride, StringComparison.Ordinal);
        Assert.Contains("NOT about inheritance", concepts.PriorityOverride, StringComparison.Ordinal);
    }

    /// <summary>
    /// An unset permission stores nothing and inherits; it is not a stored Deny. Getting this wrong sends
    /// an editor hunting the Permissions Editor for an entry that does not exist.
    /// </summary>
    [Fact]
    public void Model_ExplainsUnsetMeansInheritNotAStoredDeny()
    {
        var concepts = Concepts;

        Assert.Contains("unset", concepts.Model, StringComparison.Ordinal);
        Assert.Contains("does not store a Deny", concepts.Model, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reference must describe what an entry is MADE OF. Asked "what are Allow and Deny entries?", the
    /// copilot invented the anatomy — and invented it using the banned word "action" for a permission.
    /// Supplying the parts (user group, permission, state, scope, optional Priority Override) removes the
    /// need to improvise, and improvising is where the terminology slips.
    /// </summary>
    [Fact]
    public void EntryAnatomy_ListsThePartsOfAnEntrySoTheModelNeedNotInvent()
    {
        var concepts = Concepts;

        Assert.Contains("user group", concepts.EntryAnatomy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission", concepts.EntryAnatomy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scope", concepts.EntryAnatomy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Priority Override", concepts.EntryAnatomy, StringComparison.Ordinal);

        // The anatomy must not itself model the banned wording.
        Assert.DoesNotContain("an action", concepts.EntryAnatomy, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The four-step resolution order, highest first, exactly as the base package's help docs state it.</summary>
    [Fact]
    public void Precedence_StatesTheResolutionOrder()
    {
        var concepts = Concepts;

        Assert.Contains("explicit Deny", concepts.Precedence, StringComparison.Ordinal);
        Assert.Contains("inherited Allow", concepts.Precedence, StringComparison.Ordinal);
    }

    /// <summary>Each scope must say what it reaches, not merely name itself.</summary>
    [Fact]
    public void Scopes_ExplainWhatEachOneReaches()
    {
        var concepts = Concepts;

        Assert.Contains("not its children", concepts.Scopes, StringComparison.Ordinal);
        Assert.Contains("everything beneath it", concepts.Scopes, StringComparison.Ordinal);
        Assert.Contains("not the node itself", concepts.Scopes, StringComparison.Ordinal);
    }

    /// <summary>
    /// Insert Options invert the content default: creatable unless filtered out or denied. Carrying the
    /// content default over to creation would report the exact opposite of the truth.
    /// </summary>
    [Fact]
    public void InsertOptions_InvertTheContentDefault()
    {
        var concepts = Concepts;

        Assert.Contains("creatable by default", concepts.InsertOptions, StringComparison.Ordinal);
    }

    /// <summary>
    /// The backoffice navigation must name the real section and editor, so a "how do I change this?"
    /// answer points somewhere that exists rather than an invented menu path.
    /// </summary>
    [Fact]
    public void HowToChange_NamesTheRealBackofficeNavigation()
    {
        var concepts = Concepts;

        Assert.Contains("Users section", concepts.HowToChange, StringComparison.Ordinal);
        Assert.Contains("Editors", concepts.HowToChange, StringComparison.Ordinal);
        Assert.Contains("Content Permissions Editor", concepts.HowToChange, StringComparison.Ordinal);
        Assert.Contains("Document Type Permissions Editor", concepts.HowToChange, StringComparison.Ordinal);
        Assert.Contains("'Permission editors and viewers'", concepts.HowToChange, StringComparison.Ordinal);

        // The v17 release sent editors to a sidebar group that does not exist.
        Assert.DoesNotContain("'Content Permissions' in the sidebar", concepts.HowToChange, StringComparison.Ordinal);
    }

    /// <summary>
    /// Start nodes are explained with the base package's own rules (<c>concepts.md</c>, "Start nodes"):
    /// checked before any permission, none means no content access, groups combine, a user-account start
    /// node replaces group ones on its branch, no special case for Administrators, deleted ones ignored.
    /// </summary>
    /// <param name="phrase">A phrase each rule must be stated with.</param>
    [Theory]
    [InlineData("before any permission")]
    [InlineData("no content access")]
    [InlineData("combine")]
    [InlineData("user account")]
    [InlineData("Administrators")]
    [InlineData("ignored")]
    [InlineData("No permission entry can change that")]
    public void Concepts_StartNodes_CoversTheBaseBullets(string phrase)
    {
        var concepts = Concepts;

        Assert.Contains(phrase, concepts.StartNodes, StringComparison.Ordinal);
    }

    /// <summary>v17 has no Library, so the start-node concept never mentions one.</summary>
    [Fact]
    public void Concepts_StartNodes_NeverMentionsTheLibrary()
    {
        var concepts = Concepts;

        Assert.DoesNotContain("Library", concepts.StartNodes, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An Allow entry in Insert Options never adds a type Umbraco does not already permit; it only keeps it.
    /// A Deny takes it away. Without this the copilot suggests an Allow to make a type appear.
    /// </summary>
    [Fact]
    public void Concepts_InsertOptions_AllowOnlyKeepsAType()
    {
        var concepts = Concepts;

        Assert.Contains("never adds", concepts.InsertOptions, StringComparison.Ordinal);
        Assert.Contains("takes it away", concepts.InsertOptions, StringComparison.Ordinal);
    }

    /// <summary>Creating under a node needs access to it, so Insert Options also depend on start nodes.</summary>
    [Fact]
    public void Concepts_InsertOptions_MentionsStartNodes()
    {
        var concepts = Concepts;

        Assert.Contains("start nodes", concepts.InsertOptions, StringComparison.Ordinal);
    }

    /// <summary>
    /// Read controls whether a page is shown in the content tree at all (base #64): a page missing from the
    /// tree, or an empty tree, comes from Read being denied or the page being outside the start nodes.
    /// </summary>
    [Fact]
    public void Concepts_ExplainsMissingFromTree()
    {
        var concepts = Concepts;
        var text = concepts.Model + " " + concepts.StartNodes;

        Assert.Contains("missing from the tree", text, StringComparison.Ordinal);
        Assert.Contains("empty tree", text, StringComparison.Ordinal);
        Assert.Contains("Read", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two reference tools were replaced by knowledge items, so the content must not send the model to
    /// a tool that no longer exists — it names the other knowledge item instead.
    /// </summary>
    [Fact]
    public void Content_NamesNoRemovedTool()
    {
        var markdown = ConceptsReference.ToMarkdown();

        Assert.DoesNotContain("uap_explain_concepts", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("uap_explain_editors", markdown, StringComparison.Ordinal);
    }
}
