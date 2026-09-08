namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// Which of the package's two permission trees a lookup applies to.
/// </summary>
/// <remarks>
/// <para>
/// Advanced Permissions governs two independent trees with the same machinery — the same Allow/Deny
/// states, scopes, inheritance, All Users baseline and Priority Override flag — but stored in separate
/// tables, keyed on different Umbraco object types, and using different verbs. This enum is what the
/// shared services (the presenter, the remediator and the audit analyzer) switch on so a single
/// implementation can serve both without either tree's details leaking into the other.
/// </para>
/// <para>
/// It is an <b>internal</b> plumbing concept, not a tool argument. The copilot never sees it: a caller
/// picks the domain because it knows which tool it is, and each tool serves exactly one tree. Keeping it
/// out of the model-visible schema is deliberate — the model chooses between
/// <c>uap_explain_access</c> and <c>uap_explain_library_access</c> by name, which is a clearer choice
/// than a flag on one overloaded tool.
/// </para>
/// <para>
/// How it is threaded differs by service, on purpose. The nine-method
/// <see cref="Services.IPermissionPresenter"/> is <i>bound</i> once via
/// <see cref="Services.IPermissionPresenter.For(PermissionDomain)"/>, so no signature or call site has to
/// carry it. The single-method remediator and analyzer simply take it as an argument, where an extra
/// binding step would be ceremony. Both default to <see cref="Content"/>, so every v17 call site keeps
/// working untouched.
/// </para>
/// </remarks>
public enum PermissionDomain
{
    /// <summary>
    /// The content tree: document nodes (<c>UmbracoObjectTypes.Document</c>), the
    /// <c>Umb.Document.*</c> verbs, and the <c>AdvancedPermission</c> table. The default, and the only
    /// domain the package covered before v18.
    /// </summary>
    Content,

    /// <summary>
    /// The Library tree: elements and element folders (<c>UmbracoObjectTypes.Element</c> and
    /// <c>UmbracoObjectTypes.ElementContainer</c>), the <c>Umb.Element.*</c> and
    /// <c>Umb.ElementContainer.*</c> verbs, and the <c>ElementPermission</c> table. Introduced in the
    /// base package's v18 line.
    /// </summary>
    Library,
}
