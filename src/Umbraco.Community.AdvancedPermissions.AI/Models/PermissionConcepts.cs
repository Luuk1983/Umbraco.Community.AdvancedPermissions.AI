namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// The conceptual reference for how this package's permissions work, returned by the
/// <c>uap_explain_concepts</c> tool. Split into named sections so the copilot can quote the part that
/// answers the question rather than reciting the whole model.
/// </summary>
/// <remarks>
/// This content is the definitional half of the copilot's grounding, moved out of the always-on system
/// prompt and paid for only when a conceptual question is actually asked. The wording is sourced from the
/// base package's own <c>help-docs/en/concepts.md</c> — it is never invented here, so that the copilot
/// explains the model exactly as the in-product help does.
/// </remarks>
/// <param name="Model">How access is stored and what an <i>unset</i> permission means.</param>
/// <param name="EntryAnatomy">What a single entry is made of, answering "what are Allow and Deny entries?".</param>
/// <param name="Precedence">The order in which competing entries are resolved.</param>
/// <param name="Scopes">The three scopes and what each one actually reaches.</param>
/// <param name="PriorityOverride">What Priority Override is, and what it is not.</param>
/// <param name="InsertOptions">How document-type creation differs from node permissions.</param>
/// <param name="HowToChange">The backoffice navigation for changing a permission by hand.</param>
public sealed record PermissionConcepts(
    string Model,
    string EntryAnatomy,
    string Precedence,
    string Scopes,
    string PriorityOverride,
    string InsertOptions,
    string HowToChange);
