using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="PermissionPresenter"/>. These verify that every raw identifier
/// (role alias, verb, scope, state, node key) is mapped to a friendly, editor-facing label,
/// so the LLM never sees <c>$everyone</c>, <c>Umb.Document.*</c>, enum names, or raw GUIDs.
/// </summary>
public sealed class PermissionPresenterTests
{
    /// <summary>The mocked user group service used to resolve group aliases to display names.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service used to resolve content node keys to names.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service used to resolve document type keys to names.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

    /// <summary>Builds a substitute user group with the given alias and name.</summary>
    /// <param name="alias">The alias the group reports.</param>
    /// <param name="name">The display name the group reports.</param>
    /// <returns>The substitute user group.</returns>
    private static IUserGroup Group(string alias, string name)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(name);
        return group;
    }

    /// <summary>Configures the user group service to return the supplied groups in a single page.</summary>
    /// <param name="groups">The groups to expose.</param>
    private void SetupGroups(params IUserGroup[] groups) =>
        _userGroupService
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(groups.Length, groups));

    /// <summary>Configures the entity service so the given node key resolves to the given name.</summary>
    /// <param name="key">The node key.</param>
    /// <param name="name">The node name to report.</param>
    private void SetupNode(Guid key, string name)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Name.Returns(name);
        _entityService.Get(key, UmbracoObjectTypes.Document).Returns(entity);
    }

    /// <summary>Creates the presenter under test with no groups configured by default.</summary>
    /// <returns>The presenter.</returns>
    private PermissionPresenter CreateSut()
    {
        if (!_userGroupService.ReceivedCalls().Any())
        {
            SetupGroups();
        }

        return new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);
    }

    /// <summary>The <c>$everyone</c> alias maps to the "All Users" display name.</summary>
    [Fact]
    public async Task RoleDisplayName_Everyone_MapsToAllUsers()
    {
        var sut = CreateSut();
        var name = await sut.GetRoleDisplayNameAsync(AdvancedPermissionsConstants.EveryoneRoleAlias);
        Assert.Equal(AdvancedPermissionsConstants.EveryoneRoleDisplayName, name);
        Assert.Equal("All Users", name);
    }

    /// <summary>A known group alias maps to the group's name.</summary>
    [Fact]
    public async Task RoleDisplayName_KnownGroup_MapsToName()
    {
        SetupGroups(Group("editors", "Editors"));
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var name = await sut.GetRoleDisplayNameAsync("editors");

        Assert.Equal("Editors", name);
    }

    /// <summary>An unknown alias falls back to the alias itself.</summary>
    [Fact]
    public async Task RoleDisplayName_UnknownGroup_FallsBackToAlias()
    {
        SetupGroups(Group("editors", "Editors"));
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var name = await sut.GetRoleDisplayNameAsync("ghosts");

        Assert.Equal("ghosts", name);
    }

    /// <summary>A verb maps to the substring after the last dot.</summary>
    [Theory]
    [InlineData("Umb.Document.Delete", "Delete")]
    [InlineData("Umb.Document.Read", "Read")]
    [InlineData("Umb.Document.Publish", "Publish")]
    [InlineData("Read", "Read")]
    public void Verb_MapsToFriendlyName(string verb, string expected)
    {
        var sut = CreateSut();
        Assert.Equal(expected, sut.GetVerbDisplayName(verb));
    }

    /// <summary>Each scope value maps to friendly text.</summary>
    [Theory]
    [InlineData(PermissionScope.ThisNodeOnly, "This node only (the node itself, not its children)")]
    [InlineData(PermissionScope.ThisNodeAndDescendants, "This node and descendants (the node and everything beneath it)")]
    [InlineData(PermissionScope.DescendantsOnly, "Descendants only (the children but not the node itself)")]
    public void Scope_MapsToFriendlyText(PermissionScope scope, string expected)
    {
        var sut = CreateSut();
        Assert.Equal(expected, sut.GetScopeText(scope));
    }

    /// <summary>Each state value maps to friendly text.</summary>
    [Theory]
    [InlineData(PermissionState.Allow, "Allowed")]
    [InlineData(PermissionState.Deny, "Denied")]
    public void State_MapsToFriendlyText(PermissionState state, string expected)
    {
        var sut = CreateSut();
        Assert.Equal(expected, sut.GetStateText(state));
    }

    /// <summary>A resolvable node key maps to its content name.</summary>
    [Fact]
    public void NodeName_Resolvable_MapsToName()
    {
        var key = Guid.NewGuid();
        SetupNode(key, "Homepage");
        var sut = CreateSut();

        Assert.Equal("Homepage", sut.GetNodeName(key));
    }

    /// <summary>The virtual-root sentinel key maps to the special root-default label.</summary>
    [Fact]
    public void NodeName_VirtualRoot_MapsToSpecialLabel()
    {
        var sut = CreateSut();
        Assert.Equal(
            "All content (root-level default)",
            sut.GetNodeName(AdvancedPermissionsConstants.VirtualRootNodeKey));
    }

    /// <summary>An unresolvable node key falls back to the generic "this node" label.</summary>
    [Fact]
    public void NodeName_Unresolvable_FallsBackToThisNode()
    {
        var sut = CreateSut();
        Assert.Equal("this node", sut.GetNodeName(Guid.NewGuid()));
    }

    /// <summary>
    /// The verdict the model receives must name the controlled action as a "Permission" field, not an
    /// "Action" field: a field literally named <c>Action</c> steers the model to write "the Delete action"
    /// instead of "the Delete permission". This guards the JSON contract the terminology depends on.
    /// </summary>
    [Fact]
    public async Task ToVerdict_ExposesPermissionFieldNotActionField()
    {
        var sut = CreateSut();
        var permission = new EffectivePermission(
            "Umb.Document.Delete", IsAllowed: false, IsExplicit: true, Reasoning: []);

        var verdict = await sut.ToVerdictAsync(permission);
        var json = System.Text.Json.JsonSerializer.Serialize(verdict);

        Assert.Contains("\"Permission\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Action\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// An effective permission with reasoning maps to a friendly verdict: friendly action,
    /// friendly result, and friendly reasons with no raw identifiers.
    /// </summary>
    [Fact]
    public async Task ToVerdict_MapsReasoningToFriendlyLabels()
    {
        SetupGroups(Group("editors", "Editors"));
        var nodeKey = Guid.NewGuid();
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var permission = new EffectivePermission(
            "Umb.Document.Delete",
            IsAllowed: false,
            IsExplicit: true,
            Reasoning:
            [
                new PermissionReasoning(
                    "editors",
                    PermissionState.Deny,
                    IsExplicit: true,
                    SourceNodeKey: nodeKey,
                    SourceScope: PermissionScope.ThisNodeOnly,
                    IsFromGroupDefault: false,
                    IsPriorityOverride: true),
                new PermissionReasoning(
                    AdvancedPermissionsConstants.EveryoneRoleAlias,
                    PermissionState.Allow,
                    IsExplicit: false,
                    SourceNodeKey: AdvancedPermissionsConstants.VirtualRootNodeKey,
                    SourceScope: PermissionScope.ThisNodeAndDescendants,
                    IsFromGroupDefault: true),
            ]);

        var verdict = await sut.ToVerdictAsync(permission);

        Assert.Equal("Delete", verdict.Permission);
        Assert.Equal("Denied", verdict.Result);
        Assert.Equal(2, verdict.Reasons.Count);

        var first = verdict.Reasons[0];
        Assert.Equal("Editors", first.UserGroup);
        Assert.Equal("Denied", first.Decision);
        Assert.Equal("This node only (the node itself, not its children)", first.Scope);
        Assert.Equal("News", first.SetOn);
        Assert.False(first.Inherited);
        Assert.True(first.PriorityOverride);

        var second = verdict.Reasons[1];
        Assert.Equal("All Users", second.UserGroup);
        Assert.Equal("Allowed", second.Decision);
        Assert.Equal("This node and descendants (the node and everything beneath it)", second.Scope);
        Assert.Equal("All content (root-level default)", second.SetOn);
        Assert.True(second.Inherited);
        Assert.False(second.PriorityOverride);
    }

    /// <summary>
    /// A verb dictionary maps to a friendly explanation with the node name and one verdict per verb.
    /// </summary>
    [Fact]
    public async Task ToExplanation_MapsDictionaryToFriendlyExplanation()
    {
        SetupGroups(Group("editors", "Editors"));
        var nodeKey = Guid.NewGuid();
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var permissions = new Dictionary<string, EffectivePermission>
        {
            ["Umb.Document.Read"] = new(
                "Umb.Document.Read", IsAllowed: true, IsExplicit: false, Reasoning: []),
            ["Umb.Document.Delete"] = new(
                "Umb.Document.Delete", IsAllowed: false, IsExplicit: true, Reasoning: []),
        };

        var explanation = await sut.ToExplanationAsync(permissions, nodeKey, []);

        Assert.Equal("News", explanation.Node);
        Assert.Equal(2, explanation.Permissions.Count);
        Assert.Contains(explanation.Permissions, p => p.Permission == "Read" && p.Result == "Allowed");
        Assert.Contains(explanation.Permissions, p => p.Permission == "Delete" && p.Result == "Denied");
    }

    /// <summary>
    /// A raw audit report is projected to friendly findings: role alias becomes display name, verb
    /// becomes friendly action, node key becomes node name, severity becomes text, and the rewritten
    /// message contains none of the raw identifiers.
    /// </summary>
    [Fact]
    public async Task ToFriendlyAudit_ProjectsFindingsToFriendlyLabels()
    {
        SetupGroups(Group("editors", "Editors"));
        var nodeKey = Guid.NewGuid();
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var report = new AuditReport(
            new[]
            {
                new AuditFinding(
                    "everyone-broad-write",
                    AuditSeverity.Risk,
                    "Everyone is allowed 'Umb.Document.Update' across the whole tree from the root.",
                    AdvancedPermissionsConstants.VirtualRootNodeKey,
                    AdvancedPermissionsConstants.EveryoneRoleAlias,
                    "Umb.Document.Update"),
                new AuditFinding(
                    "allow-deny-conflict",
                    AuditSeverity.Warning,
                    "Role 'editors' has both Allow and Deny for 'Umb.Document.Delete' on the same node.",
                    nodeKey,
                    "editors",
                    "Umb.Document.Delete"),
            },
            EntriesAnalyzed: 5);

        var friendly = await sut.ToFriendlyAuditAsync(report);

        Assert.Equal(5, friendly.EntriesAnalyzed);
        Assert.Equal(2, friendly.Findings.Count);

        var first = friendly.Findings[0];
        Assert.Equal("everyone-broad-write", first.RuleId);
        Assert.Equal("Risk", first.Severity);
        Assert.Equal("All Users", first.UserGroup);
        Assert.Equal("Update", first.Permission);
        Assert.Equal("All content (root-level default)", first.Node);

        var second = friendly.Findings[1];
        Assert.Equal("Warning", second.Severity);
        Assert.Equal("Editors", second.UserGroup);
        Assert.Equal("Delete", second.Permission);
        Assert.Equal("News", second.Node);
        // The conflict message uses editor-grounded terminology ("entry"/"permission"), not bare tokens.
        Assert.Contains("Allow entry", second.Message, StringComparison.Ordinal);
        Assert.Contains("Deny entry", second.Message, StringComparison.Ordinal);
        Assert.Contains("Delete permission", second.Message, StringComparison.Ordinal);

        // No raw identifiers anywhere in the projected report.
        var json = System.Text.Json.JsonSerializer.Serialize(friendly);
        Assert.DoesNotContain("$everyone", json);
        Assert.DoesNotContain("Umb.Document.", json);
        Assert.DoesNotContain(nodeKey.ToString(), json);
    }

    /// <summary>
    /// A "remove the Deny" remediation projects to friendly labels, an administrator-action sentence, and
    /// a "Remove" change verb — and leaks no raw role alias, verb, scope/state enum, or node GUID.
    /// </summary>
    [Fact]
    public async Task ToRemediation_RemoveDeny_ProjectsFriendlyAdminAction()
    {
        var nodeKey = Guid.NewGuid();
        SetupGroups(Group("editors", "Editors"));
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var option = new RemediationOption(
            RemediationActionKind.RemoveDeny,
            "editors",
            AdvancedPermissionsConstants.VerbDelete,
            nodeKey,
            Scope: null,
            RemovedRoleAliases: ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias]);

        var friendly = await sut.ToRemediationAsync(option);

        Assert.Equal("Remove", friendly.Action);
        Assert.Equal("Delete", friendly.Permission);
        Assert.Equal("News", friendly.SetOn);
        Assert.Null(friendly.Scope);
        Assert.Contains("administrator", friendly.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allow", friendly.Description, StringComparison.OrdinalIgnoreCase);
        // Editor-grounded terminology: the record is an "entry", the action is a "permission", and the
        // collections are "user groups" — never bare "Deny"/"Delete"/"role".
        Assert.Contains("Deny entry", friendly.Description, StringComparison.Ordinal);
        Assert.Contains("Delete permission", friendly.Description, StringComparison.Ordinal);
        Assert.Contains("user group", friendly.Description, StringComparison.Ordinal);

        var json = System.Text.Json.JsonSerializer.Serialize(friendly);
        Assert.DoesNotContain("$everyone", json);
        Assert.DoesNotContain("Umb.Document.", json);
        Assert.DoesNotContain("RemoveDeny", json);
        Assert.DoesNotContain(nodeKey.ToString(), json);
        // The All Users role is named friendly, not by alias.
        Assert.Contains("All Users", friendly.Description);
    }

    /// <summary>
    /// A "remove the Deny entry" remediation must explain WHY the permission ends up allowed, naming the
    /// user group and the entry that take over once the Deny is gone. Without this the sentence asserts an
    /// outcome an editor cannot verify — and which only holds because some other entry grants it, since
    /// the resolver defaults to deny.
    /// </summary>
    [Fact]
    public async Task ToRemediation_RemoveDeny_ExplainsWhatGrantsItAfterwards()
    {
        var nodeKey = Guid.NewGuid();
        var ancestorKey = Guid.NewGuid();
        SetupGroups(Group("admins", "Administrators"));
        SetupNode(nodeKey, "People");
        SetupNode(ancestorKey, "Home");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var option = new RemediationOption(
            RemediationActionKind.RemoveDeny,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            AdvancedPermissionsConstants.VerbDelete,
            nodeKey,
            Scope: null,
            RemovedRoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            GrantedBy: new PermissionReasoning(
                "admins",
                PermissionState.Allow,
                IsExplicit: false,
                SourceNodeKey: ancestorKey,
                SourceScope: PermissionScope.ThisNodeAndDescendants,
                IsFromGroupDefault: false));

        var friendly = await sut.ToRemediationAsync(option);

        Assert.Contains("because", friendly.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Administrators", friendly.Description, StringComparison.Ordinal);
        Assert.Contains("Home", friendly.Description, StringComparison.Ordinal);
        Assert.NotNull(friendly.GrantedBy);
        Assert.Contains("Administrators", friendly.GrantedBy!, StringComparison.Ordinal);
        // A removal is the least-privileged fix; it carries no caution.
        Assert.Null(friendly.Caution);
    }

    /// <summary>
    /// An "add an entry" remediation must state the scope in prose with its plain-English meaning
    /// attached, rather than the terse "(scope: This node only)" parenthetical — an editor should not have
    /// to already know what a scope name implies. The label is kept verbatim as the anchor to the
    /// Permissions Editor's scope dropdown, and the meaning comes from the base package's own concepts doc.
    /// </summary>
    [Fact]
    public async Task ToRemediation_Add_StatesScopeWithItsMeaningInProse()
    {
        var nodeKey = Guid.NewGuid();
        SetupGroups(Group("editors", "Editors"));
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var option = new RemediationOption(
            RemediationActionKind.AddAllowOnNode,
            "editors",
            AdvancedPermissionsConstants.VerbPublish,
            nodeKey,
            PermissionScope.ThisNodeOnly,
            RemovedRoleAliases: []);

        var friendly = await sut.ToRemediationAsync(option);

        Assert.Contains("This node only", friendly.Description, StringComparison.Ordinal);
        Assert.Contains("the node itself, not its children", friendly.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("(scope:", friendly.Description, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// "All Users" is already a group name, so the generic "the {name} user group" template renders the
    /// clumsy "the All Users user group". It also assumes the reader knows that group reaches everyone —
    /// the very thing that makes a Deny entry on it so consequential. Both are fixed by naming it properly
    /// and glossing it once.
    /// </summary>
    [Fact]
    public async Task ToRemediation_RemoveDenyOnAllUsers_ReadsNaturallyAndExplainsTheGroup()
    {
        var nodeKey = Guid.NewGuid();
        SetupNode(nodeKey, "People");
        var sut = CreateSut();

        var option = new RemediationOption(
            RemediationActionKind.RemoveDeny,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            AdvancedPermissionsConstants.VerbDelete,
            nodeKey,
            Scope: null,
            RemovedRoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias]);

        var friendly = await sut.ToRemediationAsync(option);

        Assert.DoesNotContain("All Users user group", friendly.Description, StringComparison.Ordinal);
        Assert.Contains("All Users group", friendly.Description, StringComparison.Ordinal);
        Assert.Contains("every backoffice user", friendly.Description, StringComparison.Ordinal);
    }

    /// <summary>
    /// When the question is about the asker themselves, the grant must be addressed to them directly
    /// ("you are in the Administrators user group…") rather than stated impersonally. For the current-user
    /// subject the granting group is, by construction, one the asker belongs to — the remediation is
    /// resolved with exactly that user's groups — so naming it in the third person makes the reader work
    /// out that it applies to them.
    /// </summary>
    [Fact]
    public async Task ToRemediation_ForAsker_AddressesTheUserDirectly()
    {
        var nodeKey = Guid.NewGuid();
        SetupGroups(Group("admins", "Administrators"));
        SetupNode(nodeKey, "People");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var option = new RemediationOption(
            RemediationActionKind.RemoveDeny,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            AdvancedPermissionsConstants.VerbDelete,
            nodeKey,
            Scope: null,
            RemovedRoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            GrantedBy: new PermissionReasoning(
                "admins",
                PermissionState.Allow,
                IsExplicit: true,
                SourceNodeKey: nodeKey,
                SourceScope: PermissionScope.ThisNodeOnly,
                IsFromGroupDefault: false));

        var friendly = await sut.ToRemediationAsync(option, forAsker: true);

        Assert.Contains("you are in the Administrators user group", friendly.GrantedBy!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("you are in the Administrators user group", friendly.Description, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The All Users group reaches every backoffice user rather than being one the asker joined, so the
    /// second-person wording must say they are <i>covered by</i> it, not a member of it.
    /// </summary>
    [Fact]
    public async Task ToRemediation_ForAsker_GrantedByAllUsers_SaysCoveredBy()
    {
        var nodeKey = Guid.NewGuid();
        SetupNode(nodeKey, "People");
        var sut = CreateSut();

        var option = new RemediationOption(
            RemediationActionKind.RemoveDeny,
            "editors",
            AdvancedPermissionsConstants.VerbDelete,
            nodeKey,
            Scope: null,
            RemovedRoleAliases: ["editors"],
            GrantedBy: new PermissionReasoning(
                AdvancedPermissionsConstants.EveryoneRoleAlias,
                PermissionState.Allow,
                IsExplicit: true,
                SourceNodeKey: nodeKey,
                SourceScope: PermissionScope.ThisNodeOnly,
                IsFromGroupDefault: false));

        var friendly = await sut.ToRemediationAsync(option, forAsker: true);

        Assert.Contains("covered by the All Users group", friendly.GrantedBy!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("you are in the All Users", friendly.GrantedBy!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// When the question is about somebody else (a named user, or a user group), the wording must stay in
    /// the third person — addressing the reader as the grantee would be plainly wrong.
    /// </summary>
    [Fact]
    public async Task ToRemediation_NotForAsker_StaysImpersonal()
    {
        var nodeKey = Guid.NewGuid();
        SetupGroups(Group("admins", "Administrators"));
        SetupNode(nodeKey, "People");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var option = new RemediationOption(
            RemediationActionKind.RemoveDeny,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            AdvancedPermissionsConstants.VerbDelete,
            nodeKey,
            Scope: null,
            RemovedRoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            GrantedBy: new PermissionReasoning(
                "admins",
                PermissionState.Allow,
                IsExplicit: true,
                SourceNodeKey: nodeKey,
                SourceScope: PermissionScope.ThisNodeOnly,
                IsFromGroupDefault: false));

        var friendly = await sut.ToRemediationAsync(option);

        Assert.Contains("the Administrators user group has an Allow entry", friendly.GrantedBy!, StringComparison.Ordinal);
        Assert.DoesNotContain("you are in", friendly.GrantedBy!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A priority-override remediation must ALWAYS carry a caution: an override wins even over a Deny
    /// entry, so it is the heaviest change and makes the effective permissions harder to review later.
    /// It must never be presented as an equal-footing alternative to simply removing the Deny entry.
    /// </summary>
    [Fact]
    public async Task ToRemediation_PriorityOverrideAllow_AlwaysCarriesCaution()
    {
        var nodeKey = Guid.NewGuid();
        SetupGroups(Group("editors", "Editors"));
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var option = new RemediationOption(
            RemediationActionKind.AddPriorityOverrideAllow,
            "editors",
            AdvancedPermissionsConstants.VerbPublish,
            nodeKey,
            PermissionScope.ThisNodeOnly,
            RemovedRoleAliases: []);

        var friendly = await sut.ToRemediationAsync(option);

        Assert.NotNull(friendly.Caution);
        Assert.Contains("sparingly", friendly.Caution!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A priority-override Allow remediation projects to the "Override" change verb, names the friendly
    /// scope, and leaks no raw identifiers.
    /// </summary>
    [Fact]
    public async Task ToRemediation_PriorityOverrideAllow_ProjectsOverrideAction()
    {
        var nodeKey = Guid.NewGuid();
        SetupGroups(Group("editors", "Editors"));
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var option = new RemediationOption(
            RemediationActionKind.AddPriorityOverrideAllow,
            "editors",
            AdvancedPermissionsConstants.VerbPublish,
            nodeKey,
            PermissionScope.ThisNodeOnly,
            RemovedRoleAliases: []);

        var friendly = await sut.ToRemediationAsync(option);

        Assert.Equal("Override", friendly.Action);
        Assert.Equal("Publish", friendly.Permission);
        Assert.Equal("Editors", friendly.UserGroup);
        Assert.Equal("This node only (the node itself, not its children)", friendly.Scope);
        // Editor-grounded terminology, including naming the conflicting entry it overrides.
        Assert.Contains("Allow entry", friendly.Description, StringComparison.Ordinal);
        Assert.Contains("Deny entry", friendly.Description, StringComparison.Ordinal);
        Assert.Contains("Publish permission", friendly.Description, StringComparison.Ordinal);
        Assert.Contains("user group", friendly.Description, StringComparison.Ordinal);

        var json = System.Text.Json.JsonSerializer.Serialize(friendly);
        Assert.DoesNotContain("Umb.Document.", json);
        Assert.DoesNotContain("ThisNodeOnly", json);
        Assert.DoesNotContain("AddPriorityOverrideAllow", json);
        Assert.DoesNotContain(nodeKey.ToString(), json);
    }
}
