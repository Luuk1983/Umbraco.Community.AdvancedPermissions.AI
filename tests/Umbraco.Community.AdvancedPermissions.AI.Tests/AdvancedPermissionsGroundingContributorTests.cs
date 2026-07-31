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
    /// When the focused entity type is not a document (e.g. <c>media</c>), the contributor must not
    /// contribute anything — the gate keeps the line off non-document conversations.
    /// </summary>
    [Fact]
    public void Contribute_NonDocumentContext_AppendsNothing()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "media");

        _contributor.Contribute(context);

        Assert.Empty(context.SystemMessageParts);
    }

    /// <summary>
    /// When no entity type has been set (e.g. a non-entity conversation, or the entity contributor did
    /// not run), the contributor must not contribute anything.
    /// </summary>
    [Fact]
    public void Contribute_NoEntityType_AppendsNothing()
    {
        var context = NewContext();

        _contributor.Contribute(context);

        Assert.Empty(context.SystemMessageParts);
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
    /// The grounding line must supply the real backoffice navigation to the permissions editor so the
    /// model's "how do I change this?" answer is accurate. The editor lives in the <b>Users</b> section
    /// under the <b>Permissions Editor</b> menu item — not the Settings section the model otherwise
    /// invents. This is the guard against the wrong-menu-path quirk.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_IncludesPermissionsEditorNavigation()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");

        _contributor.Contribute(context);

        var grounding = Assert.Single(context.SystemMessageParts);
        Assert.Contains("Users section", grounding, StringComparison.Ordinal);
        Assert.Contains("Content Permissions", grounding, StringComparison.Ordinal);
        Assert.Contains("Permissions Editor", grounding, StringComparison.Ordinal);
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
