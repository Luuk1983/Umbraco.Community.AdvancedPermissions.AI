using Umbraco.AI.Core.Contexts.KnowledgeSets;

namespace Umbraco.Community.AdvancedPermissions.AI.Knowledge;

/// <summary>
/// The Advanced Permissions reference, shipped as an Umbraco AI knowledge set: two items, "Permission
/// concepts" (how the rules work) and "Permission editors and viewers" (which screen to use, and where).
/// </summary>
/// <remarks>
/// <para>
/// Umbraco AI discovers this class by its <see cref="AIKnowledgeSetAttribute"/> and switches it on for
/// every chat request on the site: no composer code, no agent setup and no tool scope. Each item's name
/// and description are listed in the system prompt under "Available On-Demand Context Resources", and the
/// model fetches the full text with Umbraco AI's built-in <c>get_context_resource</c> tool, which every
/// agent always has.
/// </para>
/// <para>
/// Both items used to be tools (<c>uap_explain_concepts</c> and <c>uap_explain_editors</c>). They became
/// knowledge because their answer never depends on live data, and as tools they only worked after an
/// administrator had allowed the package's tool scope on the agent. The model still decides when to
/// fetch, so the always-on grounding keeps pointing at both items by name, and the descriptions below are
/// written to make the model recognise a question that needs them.
/// </para>
/// <para>
/// The item keys seed Umbraco's deterministic resource id for each item, so they must never change.
/// </para>
/// </remarks>
[AIKnowledgeSet(
    SetId,
    "Advanced Permissions",
    Description = "Reference for the Advanced Permissions package on this site: how its permissions work and " +
                  "which screen to use. It explains the model; it does not report anyone's effective permissions " +
                  "on a particular node.",
    Icon = "icon-lock")]
public sealed class AdvancedPermissionsKnowledgeSet : AIKnowledgeSetBase
{
    /// <summary>The knowledge set's immutable id, as Umbraco AI stores and lists it.</summary>
    public const string SetId = "advanced-permissions";

    /// <summary>The stable key of the concepts item. Never change it: it seeds the resource id.</summary>
    internal const string ConceptsKey = "concepts";

    /// <summary>The stable key of the editors item. Never change it: it seeds the resource id.</summary>
    internal const string EditorsKey = "editors";

    /// <summary>The concepts item's name, as the model sees it listed and as the grounding names it.</summary>
    internal const string ConceptsItemName = "Permission concepts";

    /// <summary>The editors item's name, as the model sees it listed and as the grounding names it.</summary>
    internal const string EditorsItemName = "Permission editors and viewers";

    /// <summary>
    /// The breadcrumb the model reads before deciding whether to fetch the concepts item: the topics a
    /// question would mention, and why general knowledge will not do.
    /// </summary>
    internal const string ConceptsDescription =
        "How this site's permission rules work: Allow and Deny entries, precedence, inheritance and unset " +
        "permissions, scopes, Priority Override, Insert Options, start nodes, why a page is missing from the " +
        "tree, and how to change a permission. " +
        "Read it before answering any question about how permissions work: the rules differ from Umbraco's " +
        "built-in permissions, so an answer from general knowledge sounds right and is wrong.";

    /// <summary>
    /// The breadcrumb the model reads before deciding whether to fetch the editors item.
    /// </summary>
    internal const string EditorsDescription =
        "Which of the four permission editors and viewers in the Users section answers a question, how they " +
        "differ, what each one does and does not do, and where to find it. Read it before telling anyone " +
        "where to go or which screen to use.";

    /// <summary>The two items. Static: the content is the same every time, so it is rendered once.</summary>
    private static readonly IReadOnlyList<AIKnowledgeSetItem> Items =
    [
        AIKnowledgeSetItem.FromContent(ConceptsKey, ConceptsItemName, ConceptsReference.ToMarkdown(), ConceptsDescription),
        AIKnowledgeSetItem.FromContent(EditorsKey, EditorsItemName, EditorsReference.ToMarkdown(), EditorsDescription),
    ];

    /// <summary>Returns the two reference items, concepts first.</summary>
    /// <param name="cancellationToken">Unused: the items are static and need no I/O.</param>
    /// <returns>The "Permission concepts" and "Permission editors and viewers" items.</returns>
    public override Task<IReadOnlyList<AIKnowledgeSetItem>> GetItemsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Items);
}
