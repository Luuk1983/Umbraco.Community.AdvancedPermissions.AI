using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Works out whether start nodes block a subject, using the base package's own start-node service, and
/// phrases the answer the way the base package's viewers do. The resolvers the tools call answer from
/// permission entries alone; Umbraco checks start nodes first, so without this an "Allowed" answer can be
/// about a node the user cannot even reach.
/// </summary>
/// <remarks>
/// Every method returns <see langword="null"/> when the subject is <b>not</b> blocked — and also when it
/// cannot tell (an unknown user or group, or a node whose path cannot be read), so an answer is never
/// changed on the strength of a restriction that was not verified. The All Users group has no start nodes
/// of its own and is never blocked.
/// </remarks>
public interface IStartNodeReachResolver
{
    /// <summary>Whether a user's combined start nodes block them from a node.</summary>
    /// <param name="userKey">The user.</param>
    /// <param name="nodeKey">The node; the virtual-root sentinel is the tree root.</param>
    /// <param name="domain">Which tree the node is in, and so which start nodes apply.</param>
    /// <returns>Why the user is blocked, or <see langword="null"/>.</returns>
    Task<StartNodeReach?> ForUserAsync(Guid userKey, Guid nodeKey, PermissionDomain domain);

    /// <summary>Whether one user group's own start node blocks it from a node.</summary>
    /// <param name="userGroupAlias">The user group alias.</param>
    /// <param name="nodeKey">The node; the virtual-root sentinel is the tree root.</param>
    /// <param name="domain">Which tree the node is in, and so which start nodes apply.</param>
    /// <returns>Why the group is blocked, or <see langword="null"/>.</returns>
    Task<StartNodeReach?> ForUserGroupAsync(string userGroupAlias, Guid nodeKey, PermissionDomain domain);

    /// <summary>
    /// Whether a subject can create anything in the Library at all. Library creation is section-wide, so
    /// the only start-node block is having no Library start node whatsoever — a subject with one can
    /// create inside it.
    /// </summary>
    /// <param name="userKey">The user, when the subject is a user.</param>
    /// <param name="userGroupAlias">The user group alias, when the subject is a user group.</param>
    /// <returns>Why the subject is blocked, or <see langword="null"/>.</returns>
    Task<StartNodeReach?> ForLibraryCreateAsync(Guid? userKey, string? userGroupAlias);
}
