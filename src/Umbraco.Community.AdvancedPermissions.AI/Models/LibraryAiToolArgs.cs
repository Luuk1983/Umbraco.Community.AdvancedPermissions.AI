using System.ComponentModel;

namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// Selects which dimension of Library access the <c>uap_explain_library_access</c> tool explains: the
/// permissions on a Library item or folder, or which element types may be created in the Library.
/// </summary>
/// <remarks>
/// The two are not variations on a theme — they are different editors with different mechanics. Node
/// permissions are denied unless something allows them and are resolved <i>at a node</i>; element-type
/// creation is allowed unless something denies it and is resolved <b>section-wide</b>, with no node at
/// all. The argument requirements differ accordingly, which is exactly why this is a two-value enum on a
/// Library-specific tool rather than two more values on the content tool's aspect.
/// </remarks>
public enum LibraryAspect
{
    /// <summary>
    /// Permissions on a Library item or folder — Read, Create, Update, Delete, Publish, Unpublish,
    /// Duplicate, Move, Rollback — resolved through the Library node permission system with tree
    /// inheritance. Requires <see cref="ExplainLibraryAccessArgs.NodeKey"/>. This is the default.
    /// </summary>
    Node,

    /// <summary>
    /// Element-type creation in the Library — which element types the subject may create. Resolved
    /// section-wide through the separate create-filter system, so it takes no node.
    /// </summary>
    ElementTypeCreate,
}

/// <summary>
/// Arguments for the <c>uap_explain_library_access</c> tool. Mirrors
/// <see cref="ExplainAccessArgs"/> — the same four subjects and the same detail levels — so the two tools
/// are learned once and used twice, differing only where the Library genuinely differs.
/// </summary>
/// <param name="Subject">Whose access to evaluate.</param>
/// <param name="NodeKey">
/// The Library item or folder to evaluate access at. Required for <see cref="LibraryAspect.Node"/> and
/// ignored for <see cref="LibraryAspect.ElementTypeCreate"/>, which is section-wide.
/// </param>
/// <param name="UserKey">The user key; required when <see cref="ExplainSubject.User"/> is chosen.</param>
/// <param name="UserGroupAlias">
/// The user group alias (accepts '$everyone'); required when <see cref="ExplainSubject.UserGroup"/> is
/// chosen. Carries the value the base package calls a role alias.
/// </param>
/// <param name="Permission">
/// Optional single Library permission to focus on; omit to evaluate all of them.
/// </param>
/// <param name="ResponseFormat">How much reasoning detail to return.</param>
/// <param name="Aspect">Which dimension of Library access to explain.</param>
/// <param name="ElementTypeKey">
/// Optional element type to focus on when <see cref="LibraryAspect.ElementTypeCreate"/> is chosen; omit
/// for the full roster.
/// </param>
/// <param name="SuggestFix">
/// When <see langword="true"/> and access is denied, also return the concrete, confirmed permission
/// changes that would grant it. Only honoured for <see cref="LibraryAspect.Node"/>, the
/// current-user/user/user-group subjects, and when a single <paramref name="Permission"/> is supplied.
/// </param>
public sealed record ExplainLibraryAccessArgs(
    [property: Description("Whose access to evaluate: current-user (the editor asking about themselves), user (a specific user, requires userKey), user-group (one user group or 'All Users', requires userGroupAlias), or all-user-groups (who can/can't do this).")]
    ExplainSubject Subject,
    [property: Description("The GUID key of the Library item or folder to evaluate access at. Required when aspect is 'node'; omit for aspect=element-type-create, which is section-wide and takes no node.")]
    Guid? NodeKey = null,
    [property: Description("The GUID key of the user to evaluate. Required when subject is 'user'.")]
    Guid? UserKey = null,
    [property: Description("The user group alias to evaluate, or '$everyone' for All Users. Required when subject is 'user-group'.")]
    string? UserGroupAlias = null,
    [property: Description("Optional Library permission such as 'Umb.Element.Delete' to focus on a single one. Omit to evaluate all of them. Ignored when aspect is 'element-type-create'.")]
    string? Permission = null,
    [property: Description("How much detail to return: concise (decision plus one summary reason per permission) or detailed (full reasoning chain).")]
    ExplainResponseFormat ResponseFormat = ExplainResponseFormat.Concise,
    [property: Description("node = permissions on a Library item or folder (read/update/delete/publish/move/…); element-type-create = which element types can be created in the Library (section-wide, no node)")]
    LibraryAspect Aspect = LibraryAspect.Node,
    [property: Description("optional — focus a single element type when Aspect=element-type-create")]
    Guid? ElementTypeKey = null,
    [property: Description("When true, and access is denied, also return the concrete permission changes that would grant it.")]
    bool SuggestFix = false);
