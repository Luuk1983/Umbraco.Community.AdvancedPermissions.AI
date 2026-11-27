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

    /// <summary>A group beyond the first page is still found, so large sites are not misread.</summary>
    [Fact]
    public async Task SecondPage_IsSearched()
    {
        var firstPage = new PagedModel<IUserGroup>(101, Enumerable.Range(0, 100).Select(i => Group($"g{i}", $"Group {i}")).ToArray());
        var secondPage = new PagedModel<IUserGroup>(101, new[] { Group("translators", "Translators") });
        _userGroupService.GetAllAsync(0, 100).Returns(firstPage);
        _userGroupService.GetAllAsync(100, 100).Returns(secondPage);

        var match = await CreateResolver().ResolveAsync("Translators");

        Assert.Equal("translators", match.Alias);
    }

    /// <summary>Replaces the registered groups with the given ones, on a single page.</summary>
    /// <param name="groups">The groups.</param>
    private void SetupGroups(params IUserGroup[] groups) =>
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(groups.Length, groups));

    /// <summary>
    /// "everyone" is only a convenience shorthand for All Users. When the site has a real group called
    /// "Everyone", that group wins — the shorthand must never hide a group that exists. All Users stays
    /// reachable by its own name.
    /// </summary>
    /// <param name="alias">The real group's alias.</param>
    /// <param name="name">The real group's display name.</param>
    [Theory]
    [InlineData("everyoneGroup", "Everyone")]
    [InlineData("everyone", "Staff (everyone)")]
    public async Task Everyone_WithARealGroupOfThatName_ResolvesToTheRealGroup(string alias, string name)
    {
        SetupGroups(Group("writer", "Writers"), Group(alias, name));

        Assert.Equal(alias, (await CreateResolver().ResolveAsync("everyone")).Alias);
        Assert.Equal(AdvancedPermissionsConstants.EveryoneRoleAlias, (await CreateResolver().ResolveAsync("All Users")).Alias);
    }

    /// <summary>
    /// Two groups with the same display name are ambiguous. Picking the first one silently would answer
    /// about the wrong group; the resolver refuses and lists the candidates with their aliases so the
    /// model can ask which is meant, or pass the alias.
    /// </summary>
    [Fact]
    public async Task DuplicateDisplayName_IsRefusedListingTheCandidates()
    {
        SetupGroups(Group("writer", "Writers"), Group("newsWriters", "Writers"), Group("editor", "Editors"));

        var match = await CreateResolver().ResolveAsync("writers");

        Assert.Null(match.Alias);
        Assert.Equal(
            "More than one user group is called 'writers': Writers (alias 'writer'), Writers (alias 'newsWriters'). " +
            "Ask which one is meant, or pass its alias.",
            match.Error);
    }

    /// <summary>
    /// The alias is unique, so it settles an ambiguous display name — which is what the refusal above
    /// tells the model to do.
    /// </summary>
    [Fact]
    public async Task DuplicateDisplayName_AliasStillResolves()
    {
        SetupGroups(Group("writer", "Writers"), Group("newsWriters", "Writers"));

        Assert.Equal("newsWriters", (await CreateResolver().ResolveAsync("newsWriters")).Alias);
    }
}
