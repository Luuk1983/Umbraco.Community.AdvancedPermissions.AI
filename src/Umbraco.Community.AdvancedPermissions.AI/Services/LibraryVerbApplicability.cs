using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Decides whether a Library permission applies at all to a given kind of Library node — the rule behind
/// the hatched <b>N/A</b> cells in the base package's Library Permissions Editor and Library Access Viewer.
/// </summary>
/// <remarks>
/// <para>
/// This exists as its own unit, rather than an inline check inside the tool, because it encodes a
/// <i>product</i> rule that must stay in step with the base package's UI. Its source of truth is that
/// package's own <c>help-docs/en/library-permissions.md</c>:
/// </para>
/// <para>
/// "A hatched <b>N/A</b> cell means the permission doesn't apply to that kind of item. 'Create' has no
/// meaning on a single item (it can't contain other items), and item-only actions (Publish, Unpublish,
/// Duplicate, Rollback) don't apply to a folder itself — only to the items inside it."
/// </para>
/// <para>
/// Why it matters for the copilot: the permission resolver happily returns a value for those
/// combinations, and relaying it would have the copilot assert "Publish is denied on this folder" when
/// the product shows the question as inapplicable. That is a confidently wrong answer of exactly the kind
/// the grounding rules exist to prevent — and it would send an editor hunting for a Deny entry that is
/// not in effect.
/// </para>
/// </remarks>
public static class LibraryVerbApplicability
{
    /// <summary>
    /// The item-only permissions. A folder is a container, not a piece of content, so these apply only to
    /// the items inside it and never to the folder itself.
    /// </summary>
    private static readonly HashSet<string> ItemOnlyVerbs =
    [
        AdvancedPermissionsConstants.VerbElementPublish,
        AdvancedPermissionsConstants.VerbElementUnpublish,
        AdvancedPermissionsConstants.VerbElementDuplicate,
        AdvancedPermissionsConstants.VerbElementRollback,
    ];

    /// <summary>
    /// Determines whether a permission is meaningful for a kind of Library node.
    /// </summary>
    /// <param name="verb">The canonical Library verb (an <c>Umb.Element.*</c> verb).</param>
    /// <param name="kind">The kind of node the permission is being resolved at.</param>
    /// <returns>
    /// <see langword="false"/> only for the combinations the base package shows as N/A: the Create
    /// permission on an item, and the item-only permissions on a folder. <see langword="true"/> for
    /// everything else — including every permission when the node's kind is
    /// <see cref="LibraryNodeKind.Unknown"/>, so an unrecognised node yields a plain answer rather than a
    /// fabricated "not applicable".
    /// </returns>
    public static bool Applies(string verb, LibraryNodeKind kind) => kind switch
    {
        // An item cannot contain other nodes, so there is nothing to create inside it.
        LibraryNodeKind.Item => !string.Equals(verb, AdvancedPermissionsConstants.VerbElementCreate, StringComparison.Ordinal),

        // A folder is not content, so the content lifecycle actions do not apply to the folder itself.
        LibraryNodeKind.Folder => !ItemOnlyVerbs.Contains(verb),

        _ => true,
    };
}
