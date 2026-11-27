using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Default <see cref="IPermissionLookupGuard"/>: the current user may inspect others when their allowed
/// sections include the Users section — the same rule as the base package's
/// <c>AuthorizationPolicies.SectionAccessUsers</c> policy on its viewer endpoints.
/// </summary>
/// <param name="backOfficeSecurityAccessor">Accessor for the current backoffice user.</param>
public sealed class PermissionLookupGuard(IBackOfficeSecurityAccessor backOfficeSecurityAccessor) : IPermissionLookupGuard
{
    /// <summary>
    /// The error the explain tools return when the gate refuses a lookup of someone else. Shared so every
    /// subject phrases the refusal identically.
    /// </summary>
    public const string OtherSubjectRefusal =
        "Looking up the permissions of other users or user groups needs access to the Users section. " +
        "You can still ask about your own access.";

    /// <summary>The error the audit tool returns when the gate refuses.</summary>
    public const string AuditRefusal = "Auditing permissions needs access to the Users section.";

    /// <inheritdoc />
    public bool CanInspectOthers() =>
        backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.AllowedSections
            .Contains(Constants.Applications.Users, StringComparer.OrdinalIgnoreCase) ?? false;
}
