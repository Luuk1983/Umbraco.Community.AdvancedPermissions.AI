using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Tools;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="ExplainConceptsTool"/>. This tool carries the definitional half of the
/// copilot's grounding — the half that used to sit in every conversation's system prompt. Each test pins
/// a fact the copilot was observed to get wrong (or would get wrong) when left to its own knowledge, so
/// the reference cannot silently lose the correction it exists to deliver.
/// </summary>
public sealed class ExplainConceptsToolTests
{
    /// <summary>Executes the tool through the <see cref="IAITool"/> contract, as the runtime does.</summary>
    /// <returns>The returned concepts.</returns>
    private static async Task<PermissionConcepts> ExecuteAsync() =>
        Assert.IsType<PermissionConcepts>(
            await ((IAITool)new ExplainConceptsTool()).ExecuteAsync(new ExplainConceptsArgs(), CancellationToken.None));

    /// <summary>
    /// Priority Override must be defined as a per-entry flag for when a user's groups disagree, and the
    /// inheritance reading must be ruled out explicitly. The copilot was observed defining it as a child
    /// entry overriding an inherited ancestor entry, which is ordinary precedence and not an override.
    /// </summary>
    [Fact]
    public async Task PriorityOverride_IsDefinedAsPerEntryFlagAndNotInheritance()
    {
        var concepts = await ExecuteAsync();

        Assert.Contains("flag on a single entry", concepts.PriorityOverride, StringComparison.Ordinal);
        Assert.Contains("several user groups", concepts.PriorityOverride, StringComparison.Ordinal);
        Assert.Contains("NOT about inheritance", concepts.PriorityOverride, StringComparison.Ordinal);
    }

    /// <summary>
    /// An unset permission stores nothing and inherits; it is not a stored Deny. Getting this wrong sends
    /// an editor hunting the Permissions Editor for an entry that does not exist.
    /// </summary>
    [Fact]
    public async Task Model_ExplainsUnsetMeansInheritNotAStoredDeny()
    {
        var concepts = await ExecuteAsync();

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
    public async Task EntryAnatomy_ListsThePartsOfAnEntrySoTheModelNeedNotInvent()
    {
        var concepts = await ExecuteAsync();

        Assert.Contains("user group", concepts.EntryAnatomy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission", concepts.EntryAnatomy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scope", concepts.EntryAnatomy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Priority Override", concepts.EntryAnatomy, StringComparison.Ordinal);

        // The anatomy must not itself model the banned wording.
        Assert.DoesNotContain("an action", concepts.EntryAnatomy, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The four-step resolution order, highest first, exactly as the base package's help docs state it.</summary>
    [Fact]
    public async Task Precedence_StatesTheResolutionOrder()
    {
        var concepts = await ExecuteAsync();

        Assert.Contains("explicit Deny", concepts.Precedence, StringComparison.Ordinal);
        Assert.Contains("inherited Allow", concepts.Precedence, StringComparison.Ordinal);
    }

    /// <summary>Each scope must say what it reaches, not merely name itself.</summary>
    [Fact]
    public async Task Scopes_ExplainWhatEachOneReaches()
    {
        var concepts = await ExecuteAsync();

        Assert.Contains("not its children", concepts.Scopes, StringComparison.Ordinal);
        Assert.Contains("everything beneath it", concepts.Scopes, StringComparison.Ordinal);
        Assert.Contains("not the node itself", concepts.Scopes, StringComparison.Ordinal);
    }

    /// <summary>
    /// Insert Options invert the content default: creatable unless filtered out or denied. Carrying the
    /// content default over to creation would report the exact opposite of the truth.
    /// </summary>
    [Fact]
    public async Task InsertOptions_InvertTheContentDefault()
    {
        var concepts = await ExecuteAsync();

        Assert.Contains("creatable by default", concepts.InsertOptions, StringComparison.Ordinal);
    }

    /// <summary>
    /// The backoffice navigation must name the real section and editor, so a "how do I change this?"
    /// answer points somewhere that exists rather than an invented menu path.
    /// </summary>
    [Fact]
    public async Task HowToChange_NamesTheRealBackofficeNavigation()
    {
        var concepts = await ExecuteAsync();

        Assert.Contains("Users section", concepts.HowToChange, StringComparison.Ordinal);
        Assert.Contains("Content Permissions", concepts.HowToChange, StringComparison.Ordinal);
        Assert.Contains("Permissions Editor", concepts.HowToChange, StringComparison.Ordinal);
    }

    /// <summary>
    /// The tool must be reachable under the package's read scope and carry a description that tells the
    /// model to prefer it over its own knowledge — the tool is useless if the model never calls it.
    /// </summary>
    [Fact]
    public void Description_TellsModelNotToAnswerFromItsOwnKnowledge()
    {
        var description = new ExplainConceptsTool().Description;

        Assert.Contains("Priority Override", description, StringComparison.Ordinal);
        Assert.Contains("own knowledge", description, StringComparison.Ordinal);
    }
}
