using System.Text.Json;
using System.Text.RegularExpressions;
using Umbraco.AI.Core;
using Umbraco.AI.Core.RuntimeContext;
using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Context;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Tools;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Holds every piece of model-visible text to the screen names the v17 base package actually shows.
/// </summary>
/// <remarks>
/// The v18 base package has a "Permissions Editor" in each of four sidebar groups; v17 has none by that
/// name — its screens are the Content Permissions Editor and the Document Type Permissions Editor. Text
/// ported from v18, or written before the v17 layout was checked, kept saying "the Permissions Editor",
/// which sends an editor looking for a menu item that does not exist (found in the catch-up's review).
/// </remarks>
public sealed class V17ScreenNameTests
{
    /// <summary>A bare "Permissions Editor": one not preceded by "Content" or "Document Type".</summary>
    private static readonly Regex BareEditorName = new(@"(?<!Content |Document Type )Permissions Editor", RegexOptions.Compiled);

    /// <summary>Every tool description, the concepts and editors references, and both grounding halves.</summary>
    /// <returns>Each named piece of model-visible text.</returns>
    public static TheoryData<string, string> ModelVisibleText()
    {
        var data = new TheoryData<string, string>();
        foreach (var row in ToolDescriptionTerminologyTests.AllDescriptions().Cast<object[]>())
        {
            data.Add($"description of {row[0]}", (string)row[1]);
        }

        data.Add("uap_explain_concepts result", Serialize(new ExplainConceptsTool(), new ExplainConceptsArgs()));
        data.Add("uap_explain_editors result", Serialize(new ExplainEditorsTool(), new ExplainEditorsArgs()));
        data.Add("grounding (any section)", Grounding(entityType: null));
        data.Add("grounding (document)", Grounding("document"));
        return data;
    }

    /// <summary>Runs a no-argument tool and serializes what the model would receive.</summary>
    /// <param name="tool">The tool.</param>
    /// <param name="args">Its (empty) arguments.</param>
    /// <returns>The serialized result.</returns>
    private static string Serialize(IAITool tool, object args) =>
        JsonSerializer.Serialize(tool.ExecuteAsync(args, CancellationToken.None).GetAwaiter().GetResult());

    /// <summary>Collects the grounding the contributor adds for a conversation.</summary>
    /// <param name="entityType">The focused entity type, or none.</param>
    /// <returns>The grounding text.</returns>
    private static string Grounding(string? entityType)
    {
        var context = new AIRuntimeContext([]);
        if (entityType is not null)
        {
            context.SetValue(Constants.ContextKeys.EntityType, entityType);
        }

        new AdvancedPermissionsGroundingContributor().Contribute(context);
        return string.Join(" ", context.SystemMessageParts);
    }

    /// <summary>No model-visible text names a "Permissions Editor" that v17 does not have.</summary>
    /// <param name="source">Where the text comes from.</param>
    /// <param name="text">The text.</param>
    [Theory]
    [MemberData(nameof(ModelVisibleText))]
    public void ModelVisibleText_NamesOnlyRealV17Screens(string source, string text)
    {
        var match = BareEditorName.Match(text);

        Assert.False(
            match.Success,
            $"{source} names a bare 'Permissions Editor', which v17 does not have: …{(match.Success ? text.Substring(Math.Max(0, match.Index - 60), Math.Min(text.Length - Math.Max(0, match.Index - 60), 100)) : string.Empty)}…");
    }
}
