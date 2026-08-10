using Umbraco.AI.Core;
using Umbraco.AI.Core.RuntimeContext;
using Umbraco.Community.AdvancedPermissions.AI.Context;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="AdvancedPermissionsGroundingContributor"/>. They exercise the contributor
/// through the real <see cref="AIRuntimeContext"/> type (constructed from request items and primed via
/// <see cref="AIRuntimeContext.SetValue(string, object?)"/>), asserting the verified-safe defensive
/// pattern: gate on a document entity context, append-only into
/// <see cref="AIRuntimeContext.SystemMessageParts"/>, and never touch the
/// <see cref="AIRuntimeContext.Variables"/> or <see cref="AIRuntimeContext.Data"/> bags.
/// </summary>
public sealed class AdvancedPermissionsGroundingContributorTests
{
    /// <summary>The system under test.</summary>
    private readonly AdvancedPermissionsGroundingContributor _contributor = new();

    /// <summary>
    /// Builds an empty <see cref="AIRuntimeContext"/> with no request items, ready to be primed with
    /// <see cref="AIRuntimeContext.SetValue(string, object?)"/>.
    /// </summary>
    /// <returns>A fresh runtime context.</returns>
    private static AIRuntimeContext NewContext() => new([]);

    /// <summary>
    /// When the focused entity type is <c>document</c> (the value the built-in entity contributor stores
    /// before ours runs), the contributor appends exactly one grounding line that mentions the package.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_AppendsGroundingLine()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");

        _contributor.Contribute(context);

        Assert.Single(context.SystemMessageParts);
        Assert.Contains("Advanced Permissions", context.SystemMessageParts[0]);
        Assert.Contains("uap_explain_access", context.SystemMessageParts[0]);
        Assert.Contains("uap_audit_permissions", context.SystemMessageParts[0]);
    }

    /// <summary>
    /// On a non-document conversation (e.g. <c>media</c>) the contributor still supplies the concepts
    /// block, because "what is a priority override?" is a conceptual question an editor can ask from
    /// anywhere — and with no grounding at all the model invents an answer. The document-only guidance
    /// (which assumes a focused node and the read tools) must NOT come along.
    /// </summary>
    [Fact]
    public void Contribute_NonDocumentContext_AppendsConceptsOnly()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "media");

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("uap_explain_concepts", grounding, StringComparison.Ordinal);
        Assert.Contains("Deny entry", grounding, StringComparison.Ordinal);
        Assert.DoesNotContain("suggestFix", grounding, StringComparison.Ordinal);
    }

    /// <summary>
    /// When no entity type has been set (a non-entity conversation, or the entity contributor did not
    /// run), the concepts block is still contributed — this is the case that previously produced a wholly
    /// ungrounded, invented explanation.
    /// </summary>
    [Fact]
    public void Contribute_NoEntityType_AppendsConceptsOnly()
    {
        var context = NewContext();

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("uap_explain_concepts", grounding, StringComparison.Ordinal);
        Assert.DoesNotContain("suggestFix", grounding, StringComparison.Ordinal);
    }

    /// <summary>
    /// A document conversation is a strict SUPERSET: it must carry the concepts block as well as the
    /// document-specific guidance, in a single part. Splitting the grounding must not cost a document
    /// conversation any of the conceptual grounding it had before.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_CarriesConceptsAndDocumentGuidance()
    {
        var documentContext = NewContext();
        documentContext.SetValue(Constants.ContextKeys.EntityType, "document");
        var otherContext = NewContext();
        otherContext.SetValue(Constants.ContextKeys.EntityType, "media");

        _contributor.Contribute(documentContext);
        _contributor.Contribute(otherContext);

        var document = Assert.Single(documentContext.SystemMessageParts);
        var concepts = Assert.Single(otherContext.SystemMessageParts);

        // Every concept available elsewhere is still present here, plus the document-only guidance.
        Assert.Contains(concepts, document, StringComparison.Ordinal);
        Assert.Contains("uap_explain_access", document, StringComparison.Ordinal);
        Assert.Contains("suggestFix", document, StringComparison.Ordinal);
    }

    /// <summary>
    /// The grounding line must tell the model the tools (and the assistant) are read-only and that it
    /// must never offer to make a permission change itself — it should hand the change off to the user in
    /// the backoffice. This is the guard against the "would you like me to set up a Deny?" quirk, where the
    /// copilot offered an action it has no tool to perform.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_StatesToolsAreReadOnly()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("read-only", grounding, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never offer", grounding, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("backoffice", grounding, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The real backoffice navigation now lives in <c>uap_explain_concepts</c> (asserted by that tool's
    /// tests), so the grounding must send the model there for it rather than carrying the menu path. The
    /// original quirk this guards — inventing a Settings-section path — comes back if the model describes
    /// navigation from memory, so the instruction must forbid exactly that.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_DefersNavigationToTheConceptsTool()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("backoffice navigation", grounding, StringComparison.Ordinal);
        Assert.Contains("from memory", grounding, StringComparison.Ordinal);
        Assert.DoesNotContain("Content Permissions", grounding, StringComparison.Ordinal);
    }

    /// <summary>
    /// The grounding line must steer the model toward editor-grounded terminology rather than raw
    /// data-model tokens: a permission record is an "entry" (never a bare "Allow"/"Deny" noun), an action
    /// is a "permission" (never a bare verb), and the collections are "user groups" (never "roles"). This
    /// guards the copilot from narrating permissions like a database ("there's a deny on delete").
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_GuidesEditorGroundedTerminology()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("Deny entry", grounding, StringComparison.Ordinal);
        Assert.Contains("user group", grounding, StringComparison.Ordinal);
        Assert.Contains("Delete permission", grounding, StringComparison.Ordinal);
    }

    /// <summary>
    /// The grounding line must tell the model to base any recommended fix on the confirmed
    /// <c>suggestFix</c> output rather than a hand-rolled guess, and to admit it cannot see why an entry was
    /// created — offering an audit instead. These guard the "guessed the fix" and "dropped the 'do you know
    /// why?' question" quirks seen in real conversations.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_TellsModelToConfirmFixesAndAdmitUnknownIntent()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("suggestFix", grounding, StringComparison.Ordinal);
        Assert.Contains("guess", grounding, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uap_audit_permissions", grounding, StringComparison.Ordinal);
        Assert.Contains("why, when", grounding, StringComparison.Ordinal);
    }

    /// <summary>
    /// The grounding line must explicitly disclaim the two wordings the model was observed to invent: "a
    /// Deny permission" (a state is not a permission — it's an entry) and "the Delete action" (the action
    /// IS the permission). The disclaimers appear verbatim as "never say" examples.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_DisclaimsStateAsPermissionAndActionWording()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("a Deny permission", grounding, StringComparison.Ordinal);
        Assert.Contains("the Delete action", grounding, StringComparison.Ordinal);
    }

    /// <summary>
    /// The grounding line must make the answer readable and honest about consequences: relay a
    /// remediation's Caution, never claim a removal results in access without saying what then grants it,
    /// write plain prose instead of dumping the tool's fields as a labelled list, and never offer to make
    /// or arrange a change (not even "would you like an administrator to…").
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_RequiresCautionPlainProseAndNoArranging()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("Caution", grounding, StringComparison.Ordinal);
        Assert.Contains("GrantedBy", grounding, StringComparison.Ordinal);
        Assert.Contains("labelled list", grounding, StringComparison.Ordinal);
        Assert.Contains("arrange", grounding, StringComparison.Ordinal);
    }

    /// <summary>
    /// The definitions now live in <c>uap_explain_concepts</c>, so the permanent grounding must POINT at
    /// that tool rather than restate them — and must tell the model not to answer conceptual questions from
    /// its own knowledge. This pointer is the whole reason the tool works: the failure it fixes was the
    /// model answering confidently and wrongly without ever realising it needed a reference.
    /// </summary>
    [Theory]
    [InlineData("document")]
    [InlineData("media")]
    [InlineData(null)]
    public void Contribute_AnyContext_PointsAtConceptsToolInsteadOfDefining(string? entityType)
    {
        var context = NewContext();
        if (entityType is not null)
        {
            context.SetValue(Constants.ContextKeys.EntityType, entityType);
        }

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("uap_explain_concepts", grounding, StringComparison.Ordinal);
        Assert.Contains("own knowledge", grounding, StringComparison.Ordinal);

        // The definitions themselves must NOT be duplicated back into the always-on prompt — that would
        // reintroduce the per-conversation cost the tool exists to avoid.
        Assert.DoesNotContain("flag on a single entry", grounding, StringComparison.Ordinal);
        Assert.DoesNotContain("creatable by default", grounding, StringComparison.Ordinal);
    }

    /// <summary>
    /// The grounding bans bare Allow/Deny nouns and "role", but never actually told the model not to call
    /// an entry a "rule" — and in testing the copilot duly described entries as "rules that control…".
    /// The convention is "entry" (it is the word the base package's own concepts doc uses), so the prompt
    /// has to say so rather than leaving it implied.
    /// </summary>
    [Fact]
    public void Contribute_AnyContext_BansCallingAnEntryARule()
    {
        var context = NewContext();

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("never a 'rule'", grounding, StringComparison.Ordinal);
    }

    /// <summary>
    /// Observed in the wild: the copilot stated a permission result for a node it had never queried
    /// ("I can see the Delete permission on it: you're denied on this page too"), after a content tool
    /// failed and it could not retrieve the node. Calling the tool "first" was already instructed; what
    /// was missing is the flat prohibition on asserting a result you have not actually retrieved.
    /// </summary>
    [Fact]
    public void Contribute_AnyContext_ForbidsAssertingAnUnretrievedResult()
    {
        var context = NewContext();

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("never state a permission result", grounding, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The contributor is append-only: even on a document context it must leave the
    /// <see cref="AIRuntimeContext.Variables"/> and <see cref="AIRuntimeContext.Data"/> bags untouched
    /// (it must not call <see cref="AIRuntimeContext.SetValue(string, object?)"/> itself). The only
    /// <c>Data</c> entry present is the one the test primed.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_DoesNotWriteVariablesOrData()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");
        var dataCountBefore = context.Data.Count;

        _contributor.Contribute(context);

        Assert.Empty(context.Variables);
        Assert.Equal(dataCountBefore, context.Data.Count);
    }
}
