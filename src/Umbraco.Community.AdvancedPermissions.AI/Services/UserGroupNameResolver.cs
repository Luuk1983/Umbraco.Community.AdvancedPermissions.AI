using Umbraco.Cms.Core.Models.Membership;
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
/// resolves as "nothing allows it" — a confidently wrong answer, found in the live copilot check.
/// </summary>
/// <remarks>
/// The order of the rules is what keeps every answer about the group that was meant:
/// <list type="number">
/// <item><description>The All Users group's alias or display name always means All Users.</description></item>
/// <item><description>A real group's alias means that group. Aliases are unique, so this settles any
/// ambiguous display name — which is what the ambiguity refusal tells the model to do.</description></item>
/// <item><description>A display name shared by more than one group is refused, listing the candidates with
/// their aliases. Picking the first match would answer about the wrong group without a word.</description></item>
/// <item><description>"everyone" is a convenience shorthand for All Users, used only when no real group
/// is called that, so it never hides a group that exists.</description></item>
/// <item><description>Anything else is refused with the list of real user groups.</description></item>
/// </list>
/// Matching ignores case and surrounding whitespace.
/// </remarks>
/// <param name="userGroupService">The Umbraco user group service.</param>
public sealed class UserGroupNameResolver(IUserGroupService userGroupService)
{
    /// <summary>The page size used when enumerating user groups.</summary>
    private const int PageSize = 100;

    /// <summary>The All Users group's own names: its alias and display name.</summary>
    private static readonly string[] EveryoneNames =
        [AdvancedPermissionsConstants.EveryoneRoleAlias, AdvancedPermissionsConstants.EveryoneRoleDisplayName];

    /// <summary>The shorthand for All Users, honoured only when no real group answers to it.</summary>
    private const string EveryoneShorthand = "everyone";

    /// <summary>Resolves a user group alias or display name to the real alias.</summary>
    /// <param name="aliasOrName">What the model passed.</param>
    /// <returns>The match: the alias, or an error listing the real user groups or the ambiguous candidates.</returns>
    public async Task<UserGroupMatch> ResolveAsync(string aliasOrName)
    {
        var wanted = aliasOrName.Trim();
        if (EveryoneNames.Any(n => Matches(n, wanted)))
        {
            return new UserGroupMatch(AdvancedPermissionsConstants.EveryoneRoleAlias, null);
        }

        var groups = await GetAllAsync();

        if (groups.FirstOrDefault(g => Matches(g.Alias, wanted)) is { } byAlias)
        {
            return new UserGroupMatch(byAlias.Alias, null);
        }

        var byName = groups.Where(g => Matches(g.Name, wanted)).ToList();
        if (byName.Count == 1)
        {
            return new UserGroupMatch(byName[0].Alias, null);
        }

        if (byName.Count > 1)
        {
            var candidates = string.Join(", ", byName.Select(g => $"{g.Name} (alias '{g.Alias}')"));
            return new UserGroupMatch(
                null,
                $"More than one user group is called '{wanted}': {candidates}. Ask which one is meant, or pass its alias.");
        }

        if (Matches(EveryoneShorthand, wanted))
        {
            return new UserGroupMatch(AdvancedPermissionsConstants.EveryoneRoleAlias, null);
        }

        var names = groups.Select(g => g.Name ?? g.Alias).Prepend(AdvancedPermissionsConstants.EveryoneRoleDisplayName);
        return new UserGroupMatch(
            null,
            $"There is no user group called '{wanted}'. The user groups are: {string.Join(", ", names)}.");
    }

    /// <summary>Whether a group's alias or name matches what was asked for, ignoring case.</summary>
    /// <param name="candidate">The alias or name; <see langword="null"/> never matches.</param>
    /// <param name="wanted">What was asked for, already trimmed.</param>
    /// <returns><see langword="true"/> on a match.</returns>
    private static bool Matches(string? candidate, string wanted) =>
        string.Equals(candidate, wanted, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads every user group, page by page.</summary>
    /// <returns>All user groups.</returns>
    private async Task<IReadOnlyList<IUserGroup>> GetAllAsync()
    {
        var groups = new List<IUserGroup>();
        var skip = 0;
        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, PageSize);
            groups.AddRange(page.Items);

            skip += PageSize;
            if (skip >= page.Total)
            {
                return groups;
            }
        }
    }
}
