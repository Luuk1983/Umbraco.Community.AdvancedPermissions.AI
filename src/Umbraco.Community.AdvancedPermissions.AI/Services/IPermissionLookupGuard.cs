namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Decides whether the current backoffice user may look up the permissions of someone other than
/// themselves. Mirrors the base package, which gates its Access Viewer, by-role and audit endpoints behind
/// Users-section access so that a backoffice user cannot inspect another user's permissions. The copilot
/// tools call the permission services directly, so they must apply the same gate themselves.
/// </summary>
public interface IPermissionLookupGuard
{
    /// <summary>
    /// Whether the current user may inspect other users, user groups, or the stored configuration.
    /// Fails closed: no user in context means <see langword="false"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the current user has access to the Users section.</returns>
    bool CanInspectOthers();
}
