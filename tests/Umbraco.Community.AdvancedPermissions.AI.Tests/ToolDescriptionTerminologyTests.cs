using NSubstitute;
using Umbraco.AI.Core;
using Umbraco.AI.Core.Contexts.KnowledgeSets;
using Umbraco.AI.Core.RuntimeContext;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Context;
using Umbraco.Community.AdvancedPermissions.AI.Knowledge;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Guards the copilot's terminology rules against the artifact that most easily undermines them: the tool
/// descriptions themselves.
/// </summary>
/// <remarks>
/// The system prompt forbids "role", "the Delete action", bare Allow/Deny nouns and calling entries
/// "rules" — but the tool descriptions sit in the model's context every single turn, so a description
/// written in the banned vocabulary is a fluent counter-example competing with the rule. A concrete
/// example beats an abstract instruction, which is why terminology kept leaking through several rounds of
/// strengthening the prompt. These tests make the prompt and the tool descriptions answer to one standard.
/// <para>
/// Argument <i>values</i> used to be exempt — the reasoning being that the model has to type them, so only
/// the surrounding prose could be constrained. That exemption was dropped before 17.0.0: the schema was
/// renamed so the values say <c>user-group</c> too (see <see cref="ToolSchemaTerminologyTests"/>), because
/// a literal the model is forced to type is the strongest counter-example of all. A description must now
/// quote the new values; the old ones are banned outright below.
/// </para>
/// </remarks>
public sealed class ToolDescriptionTerminologyTests
{
    /// <summary>Builds the explain-access tool with throwaway substitutes, purely to read its description.</summary>
    /// <returns>The tool.</returns>
    private static ExplainAccessTool NewExplainAccess() => new(
        Substitute.For<IAdvancedPermissionService>(),
        Substitute.For<IContentPathResolver>(),
        Substitute.For<IPermissionPresenter>(),
        Substitute.For<IUserGroupService>(),
        Substitute.For<IBackOfficeSecurityAccessor>(),
        Substitute.For<IDocTypePermissionService>(),
        Substitute.For<IContentTypeService>(),
        Substitute.For<IEntityService>(),
        Substitute.For<IPermissionRemediationService>(),
        Substitute.For<IUserService>(),
        Substitute.For<IPermissionLookupGuard>(),
        Substitute.For<IStartNodeReachResolver>());

    /// <summary>Builds the Library explain tool with throwaway substitutes, purely to read its description.</summary>
    /// <returns>The tool.</returns>
    private static ExplainLibraryAccessTool NewExplainLibraryAccess() => new(
        Substitute.For<IElementNodePermissionService>(),
        Substitute.For<IElementTreeResolver>(),
        Substitute.For<IPermissionPresenter>(),
        Substitute.For<IUserGroupService>(),
        Substitute.For<IBackOfficeSecurityAccessor>(),
        Substitute.For<IDocTypePermissionService>(),
        Substitute.For<ILibraryElementTypeProvider>(),
        Substitute.For<IPermissionRemediationService>(),
        Substitute.For<IUserService>(),
        Substitute.For<IPermissionLookupGuard>(),
        Substitute.For<IStartNodeReachResolver>(),
        Substitute.For<ILibraryItemNameResolver>());

    /// <summary>Builds the audit tool with throwaway substitutes, purely to read its description.</summary>
    /// <returns>The tool.</returns>
    private static AuditPermissionsTool NewAudit() => new(
        Substitute.For<IAdvancedPermissionRepository>(),
        Substitute.For<IElementPermissionRepository>(),
        Substitute.For<IDocTypePermissionRepository>(),
        Substitute.For<IPermissionAuditAnalyzer>(),
        Substitute.For<IPermissionPresenter>(),
        Substitute.For<IEntityService>(),
        Substitute.For<IElementTreeResolver>(),
        Substitute.For<IUserGroupService>(),
        Substitute.For<IPermissionLookupGuard>(),
        Substitute.For<IStartNodeReachResolver>());

    /// <summary>Every tool description the model sees, keyed by tool name.</summary>
    /// <returns>Tool name and description pairs.</returns>
    public static TheoryData<string, string> AllDescriptions() => new()
    {
        { "uap_explain_access", NewExplainAccess().Description },
        { "uap_explain_library_access", NewExplainLibraryAccess().Description },
        { "uap_audit_permissions", NewAudit().Description },
    };

    /// <summary>
    /// Every piece of knowledge text the model sees: the set description and the two item descriptions
    /// (listed in the system prompt of every request), and the two item bodies (fetched on demand).
    /// </summary>
    /// <returns>Source label and text pairs.</returns>
    public static TheoryData<string, string> AllKnowledgeText()
    {
        var set = new AdvancedPermissionsKnowledgeSet();
        var items = ((IAIKnowledgeSet)set).GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult();
        var data = new TheoryData<string, string> { { "set description", set.Description ?? string.Empty } };
        foreach (var item in items)
        {
            data.Add($"{item.Key} description", item.Description ?? string.Empty);
            data.Add($"{item.Key} content", item.GetContentAsync(CancellationToken.None).GetAwaiter().GetResult());
        }

        return data;
    }

    /// <summary>The always-on grounding for a document, a Library item and no focused entity.</summary>
    /// <returns>Each variant of the grounding text.</returns>
    private static IEnumerable<string> AllGroundingVariants()
    {
        foreach (var entityType in new[] { "document", "element", null })
        {
            var context = new AIRuntimeContext([]);
            if (entityType is not null)
            {
                context.SetValue(Constants.ContextKeys.EntityType, entityType);
            }

            new AdvancedPermissionsGroundingContributor().Contribute(context);
            yield return Assert.Single(context.SystemMessageParts);
        }
    }

    /// <summary>
    /// Phrases that contradict the terminology rules. Each was found in a real description during the
    /// prompt audit; together they cover the four banned constructs (role-for-user-group, permission-as-
    /// action, bare Allow/Deny noun, entry-as-rule).
    /// </summary>
    public static readonly string[] BannedPhrases =
    [
        "permission Deny",   // bare Deny noun, one word from the banned "a Deny permission"
        "action's",          // the permission IS the action — "each action's result"
        "denied action",
        "which role",
        "for one role",
        "for role X",
        "a specific role",
        "single role",
        "descendants' rules", // entries are entries, never rules
        "roleAlias",          // the argument is userGroupAlias; naming the old one would send the model wrong
        "all-roles",          // the subject value is all-user-groups
        "scope=role",         // the audit scope value is user-group
    ];

    /// <summary>
    /// No tool description may contain a phrase that contradicts the terminology the system prompt
    /// mandates, because the description is in context every turn and teaches by example.
    /// </summary>
    /// <param name="toolName">The tool whose description is checked.</param>
    /// <param name="description">The description text.</param>
    [Theory]
    [MemberData(nameof(AllDescriptions))]
    public void Description_UsesNoBannedTerminology(string toolName, string description)
    {
        var violations = BannedPhrases
            .Where(p => description.Contains(p, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"{toolName} description contradicts the copilot's own terminology rules: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// The knowledge text teaches by example exactly like a tool description does, and its bodies are the
    /// longest model-visible prose the package ships, so it answers to the same banned list.
    /// </summary>
    /// <param name="source">Which piece of knowledge text is checked.</param>
    /// <param name="text">The text.</param>
    [Theory]
    [MemberData(nameof(AllKnowledgeText))]
    public void KnowledgeText_UsesNoBannedTerminology(string source, string text)
    {
        var violations = BannedPhrases
            .Where(p => text.Contains(p, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Knowledge {source} contradicts the copilot's own terminology rules: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// <c>uap_explain_concepts</c> and <c>uap_explain_editors</c> became knowledge items. Any model-visible
    /// text that still names them would send the model to call a tool that no longer exists, so every
    /// source is checked: the remaining tools' descriptions, the knowledge text, and the grounding.
    /// </summary>
    [Fact]
    public void ModelVisibleText_NeverNamesARemovedTool()
    {
        var texts = AllDescriptions().Select(row => (string)row[1])
            .Concat(AllKnowledgeText().Select(row => (string)row[1]))
            .Concat(AllGroundingVariants());

        Assert.All(texts, text =>
        {
            Assert.DoesNotContain("uap_explain_concepts", text, StringComparison.Ordinal);
            Assert.DoesNotContain("uap_explain_editors", text, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The two tools that talk about groups must name them "user groups", matching the prompt, so the
    /// model sees the mandated wording modelled rather than merely instructed.
    /// </summary>
    /// <param name="toolName">The tool whose description is checked.</param>
    [Theory]
    [InlineData("uap_explain_access")]
    [InlineData("uap_audit_permissions")]
    public void Description_NamesUserGroups(string toolName)
    {
        var description = toolName == "uap_explain_access"
            ? NewExplainAccess().Description
            : NewAudit().Description;

        Assert.Contains("user group", description, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Both Haiku and Opus concluded an unpublished node could not be checked, after an unrelated content
    /// tool failed on it. Permissions do not care about publish state, so the description has to say so —
    /// otherwise a neighbouring tool's limitation is silently attributed to ours.
    /// </summary>
    [Fact]
    public void ExplainDescription_SaysPublishStateIsIrrelevant()
    {
        var description = NewExplainAccess().Description;

        Assert.Contains("published", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("draft", description, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// "Why can I delete this but not its parent?" needs two calls and a comparison. Left to improvise,
    /// models instead inferred the second node's verdict from the first — or fabricated it. The description
    /// must name the pattern and point at the ancestor chain that makes it possible.
    /// </summary>
    [Fact]
    public void ExplainDescription_ExplainsHowToCompareTwoNodes()
    {
        var description = NewExplainAccess().Description;

        Assert.Contains("ancestors", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("once per node", description, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The audit tool's description invites "is anything misconfigured?", but its <c>scope</c> defaults to
    /// a single user group and errors without one. The description must therefore say which scope answers
    /// a site-wide question, or that invitation is a trap.
    /// </summary>
    [Fact]
    public void AuditDescription_SaysWhichScopeAnswersASiteWideQuestion()
    {
        var description = NewAudit().Description;

        Assert.Contains("scope=all", description, StringComparison.OrdinalIgnoreCase);
    }
}
