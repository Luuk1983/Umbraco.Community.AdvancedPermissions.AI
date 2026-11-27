using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="UserGroupNameResolver"/>. The model only ever sees user groups by their
/// display name ("Writers"), so it passes that as the alias; the base package resolves an unknown alias to
/// an empty entry set, which reads as "nothing allows it" — a confidently wrong answer found in the live
/// copilot check. The resolver maps a name or alias to the real alias, and refuses an unknown one.
/// </summary>
public sealed class UserGroupNameResolverTests
{
    /// <summary>The mocked user group service holding Writers (alias "writer") and Editors (alias "editor").</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>Registers the two groups.</summary>
    public UserGroupNameResolverTests()
    {
        var page = new PagedModel<IUserGroup>(2, new[] { Group("writer", "Writers"), Group("editor", "Editors") });
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(page);
    }

    /// <summary>Builds a user group.</summary>
    /// <param name="alias">The alias.</param>
    /// <param name="name">The display name.</param>
    /// <returns>A substitute group.</returns>
    private static IUserGroup Group(string alias, string name)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(name);
        return group;
    }

    /// <summary>Builds the resolver under test.</summary>
    /// <returns>The resolver.</returns>
    private UserGroupNameResolver CreateResolver() => new(_userGroupService);

    /// <summary>The real alias passes through unchanged.</summary>
    [Fact]
    public async Task Alias_ResolvesToItself()
    {
        var match = await CreateResolver().ResolveAsync("writer");

        Assert.Equal("writer", match.Alias);
        Assert.Null(match.Error);
    }

    /// <summary>The display name — what the model actually sends — resolves to the alias.</summary>
    [Fact]
    public async Task DisplayName_ResolvesToAlias()
    {
        Assert.Equal("writer", (await CreateResolver().ResolveAsync("Writers")).Alias);
    }

    /// <summary>Matching ignores case and surrounding whitespace.</summary>
    [Fact]
    public async Task Match_IgnoresCaseAndWhitespace()
    {
        Assert.Equal("editor", (await CreateResolver().ResolveAsync("  EDITORS ")).Alias);
    }

    /// <summary>The All Users group is reachable by its display name, its alias, or "everyone".</summary>
    /// <param name="input">A way of naming the All Users group.</param>
    [Theory]
    [InlineData("All Users")]
    [InlineData("all users")]
    [InlineData("$everyone")]
    [InlineData("everyone")]
    public async Task AllUsers_ResolvesToEveryoneAlias(string input)
    {
        Assert.Equal(AdvancedPermissionsConstants.EveryoneRoleAlias, (await CreateResolver().ResolveAsync(input)).Alias);
    }

    /// <summary>
    /// An unknown group is refused with the list of real groups, so the copilot can correct itself instead
    /// of reporting that "nothing allows it".
    /// </summary>
    [Fact]
    public async Task Unknown_ReturnsErrorListingTheGroups()
    {
        var match = await CreateResolver().ResolveAsync("Marketing");

        Assert.Null(match.Alias);
        Assert.Equal(
            "There is no user group called 'Marketing'. The user groups are: All Users, Writers, Editors.",
            match.Error);
    }
}
