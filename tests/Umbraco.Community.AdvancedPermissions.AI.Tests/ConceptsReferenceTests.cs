using Umbraco.Community.AdvancedPermissions.AI.Knowledge;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="ConceptsReference"/>, the content of the "Permission concepts" knowledge item.
/// It carries the definitional half of the copilot's grounding — the half that used to sit in every conversation's system prompt. Each test pins
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
        Assert.Contains("Content Permissions", concepts.HowToChange, StringComparison.Ordinal);
        Assert.Contains("Permissions Editor", concepts.HowToChange, StringComparison.Ordinal);
    }

    /// <summary>
    /// Start nodes are a concept of their own (base <c>concepts.md</c> "Start nodes"): checked before any
    /// permission, per group or none, combining across groups, replaced by a user-account start node, no
    /// Administrator exception, a separate set for the Library, and the Umbraco 18 upgrade state.
    /// </summary>
    [Fact]
    public void StartNodes_CoversTheBaseBullets()
    {
        var concepts = Concepts;

        Assert.Contains("before any permission", concepts.StartNodes, StringComparison.Ordinal);
        Assert.Contains("no content access", concepts.StartNodes, StringComparison.Ordinal);
        Assert.Contains("combine", concepts.StartNodes, StringComparison.Ordinal);
        Assert.Contains("user account", concepts.StartNodes, StringComparison.Ordinal);
        Assert.Contains("Administrators", concepts.StartNodes, StringComparison.Ordinal);
        Assert.Contains("Library", concepts.StartNodes, StringComparison.Ordinal);
        Assert.Contains("Umbraco 18", concepts.StartNodes, StringComparison.Ordinal);
        Assert.Contains("No permission entry", concepts.StartNodes, StringComparison.Ordinal);
    }

    /// <summary>Creating under a node also needs access to it, so Insert Options mention start nodes.</summary>
    [Fact]
    public void InsertOptions_MentionsStartNodes()
    {
        var concepts = Concepts;

        Assert.Contains("start node", concepts.InsertOptions, StringComparison.Ordinal);
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

    /// <summary>The navigation section hands "which screen?" questions to the editors knowledge item.</summary>
    [Fact]
    public void HowToChange_PointsAtTheEditorsKnowledgeItem()
    {
        Assert.Contains("Permission editors and viewers", Concepts.HowToChange, StringComparison.Ordinal);
    }
}
