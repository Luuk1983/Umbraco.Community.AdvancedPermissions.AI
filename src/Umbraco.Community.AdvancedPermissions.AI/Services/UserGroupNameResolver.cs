using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// The outcome of resolving what the model passed as a user group.
/// </summary>
/// <param name="Alias">The real user group alias (or <c>$everyone</c>), or <see langword="null"/> when there is no such group.</param>
/// <param name="Error">An editor-facing refusal naming the real user groups, set only when <see cref="Alias"/> is <see langword="null"/>.</param>
public sealed record UserGroupMatch(string? Alias, string? Error);

/// <summary>
/// Maps what the model calls a user group to the alias the base package needs. Every answer shows the
/// model user groups by display name ("Writers"), never by alias ("writer"), so it naturally passes the
/// name back. The base package treats an unknown alias as a group with no entries, so that call silently
/// resolves as "nothing allows it" — a confidently wrong answer, found in the live copilot check. This
/// accepts the alias or the display name (ignoring case), maps the All Users group's names to
/// <c>$everyone</c>, and refuses anything else with the list of real user groups so the model can correct
/// itself.
/// </summary>
/// <param name="userGroupService">The Umbraco user group service.</param>
public sealed class UserGroupNameResolver(IUserGroupService userGroupService)
{
    /// <summary>The page size used when enumerating user groups.</summary>
    private const int PageSize = 100;

    /// <summary>The ways the All Users group is named: its alias, display name, and the obvious shorthand.</summary>
    private static readonly string[] EveryoneNames =
        [AdvancedPermissionsConstants.EveryoneRoleAlias, AdvancedPermissionsConstants.EveryoneRoleDisplayName, "everyone"];

    /// <summary>Resolves a user group alias or display name to the real alias.</summary>
    /// <param name="aliasOrName">What the model passed.</param>
    /// <returns>The match: the alias, or an error listing the real user groups.</returns>
    public async Task<UserGroupMatch> ResolveAsync(string aliasOrName)
    {
        var wanted = aliasOrName.Trim();
        if (EveryoneNames.Any(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase)))
        {
            return new UserGroupMatch(AdvancedPermissionsConstants.EveryoneRoleAlias, null);
        }

        var names = new List<string> { AdvancedPermissionsConstants.EveryoneRoleDisplayName };
        var skip = 0;
        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, PageSize);
            foreach (var group in page.Items)
            {
                if (string.Equals(group.Alias, wanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(group.Name, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return new UserGroupMatch(group.Alias, null);
                }

                names.Add(group.Name ?? group.Alias);
            }

            skip += PageSize;
            if (skip >= page.Total)
            {
                break;
            }
        }

        return new UserGroupMatch(
            null,
            $"There is no user group called '{wanted}'. The user groups are: {string.Join(", ", names)}.");
    }
}
