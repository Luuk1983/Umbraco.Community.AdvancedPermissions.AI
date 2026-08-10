using System.Reflection;
using Umbraco.Community.AdvancedPermissions.AI.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Guards the copilot's terminology rules against the surface the prose rules cannot reach: the tool
/// <i>schema</i> — argument names, enum values, and the field names of everything a tool returns.
/// </summary>
/// <remarks>
/// <para>
/// The system prompt orders the model to call the collections "user groups" and never "roles", while the
/// schema used to make it pass <c>subject: "role"</c> and <c>roleAlias</c>, and read back
/// <c>allowedRoles</c>. That is the same fluent counter-example problem
/// <see cref="ToolDescriptionTerminologyTests"/> exists to stop, one layer down: an instruction competing
/// with a name the model has to type. Argument values and field names are now spelled the way the answer
/// should be.
/// </para>
/// <para>
/// The boundary is deliberate and stops here. The base package's own API is role-named throughout
/// (<c>AdvancedPermissionEntry.RoleAlias</c>, <c>ResolveForRoleAsync</c>, <c>ContributingRole</c>), and
/// internal types that merely carry one of its values — <see cref="AuditFinding"/>,
/// <see cref="RemediationOption"/> — keep that name, because renaming them would only disguise where the
/// value came from. Neither is ever serialized to the model; the presenter converts them first.
/// </para>
/// </remarks>
public sealed class ToolSchemaTerminologyTests
{
    /// <summary>
    /// Every type whose member names reach the model: the two argument records, plus everything a tool
    /// returns (directly or nested).
    /// </summary>
    /// <returns>The model-visible types.</returns>
    public static TheoryData<Type> ModelVisibleTypes() =>
    [
        typeof(ExplainAccessArgs),
        typeof(AuditPermissionsArgs),
        typeof(ExplainConceptsArgs),
        typeof(ExplainSubject),
        typeof(ExplainAspect),
        typeof(ExplainResponseFormat),
        typeof(AuditScope),
        typeof(AuditSeverity),
        typeof(AccessReason),
        typeof(AccessVerdict),
        typeof(AccessExplanation),
        typeof(AccessRemediation),
        typeof(AccessRoster),
        typeof(AccessRosterReport),
        typeof(AccessError),
        typeof(TypeCreateVerdict),
        typeof(TypeCreateExplanation),
        typeof(TypeCreateRoster),
        typeof(TypeCreateRosterReport),
        typeof(FriendlyAuditFinding),
        typeof(FriendlyAuditReport),
        typeof(NodeRef),
        typeof(PermissionConcepts),
    ];

    /// <summary>
    /// No name the model reads or types may say "role". The model cannot be told to avoid a word it is
    /// simultaneously required to spell.
    /// </summary>
    /// <param name="type">A type whose member names reach the model.</param>
    [Theory]
    [MemberData(nameof(ModelVisibleTypes))]
    public void ModelVisibleNames_NeverSayRole(Type type)
    {
        var names = type.IsEnum
            ? Enum.GetNames(type)
            : type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

        var violations = names
            .Where(n => n.Contains("role", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"{type.Name} exposes role-named members to the model: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// The positive half: the members that identify a user group must actually say so, so the rename is
    /// pinned rather than merely the old word being absent.
    /// </summary>
    /// <param name="typeName">Display name of the type, for the failure message.</param>
    /// <param name="type">The type to inspect.</param>
    /// <param name="expectedMember">The member name that must exist.</param>
    [Theory]
    [InlineData("ExplainAccessArgs", typeof(ExplainAccessArgs), "UserGroupAlias")]
    [InlineData("AuditPermissionsArgs", typeof(AuditPermissionsArgs), "UserGroupAlias")]
    [InlineData("AccessReason", typeof(AccessReason), "UserGroup")]
    [InlineData("AccessRemediation", typeof(AccessRemediation), "UserGroup")]
    [InlineData("AccessRoster", typeof(AccessRoster), "AllowedUserGroups")]
    [InlineData("AccessRoster", typeof(AccessRoster), "DeniedUserGroups")]
    [InlineData("TypeCreateRoster", typeof(TypeCreateRoster), "AllowedUserGroups")]
    [InlineData("TypeCreateRoster", typeof(TypeCreateRoster), "DeniedUserGroups")]
    [InlineData("FriendlyAuditFinding", typeof(FriendlyAuditFinding), "UserGroup")]
    public void ModelVisibleNames_SayUserGroup(string typeName, Type type, string expectedMember)
    {
        var names = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);

        Assert.True(
            names.Contains(expectedMember),
            $"{typeName} is missing the user-group member '{expectedMember}'.");
    }

    /// <summary>
    /// The subject and scope values the model has to type must name the user group, since those literals
    /// appear in its tool calls and, from there, often in its prose.
    /// </summary>
    [Fact]
    public void SubjectAndScope_NameTheUserGroup()
    {
        Assert.Contains("UserGroup", Enum.GetNames<ExplainSubject>());
        Assert.Contains("AllUserGroups", Enum.GetNames<ExplainSubject>());
        Assert.Contains("UserGroup", Enum.GetNames<AuditScope>());
    }
}
