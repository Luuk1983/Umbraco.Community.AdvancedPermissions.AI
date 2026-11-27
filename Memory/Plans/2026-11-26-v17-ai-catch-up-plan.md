# v17 AI Companion Catch-up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the v17 AI companion (`origin/v17/main`, 8d8a9a9) in line with base Advanced Permissions v17.5.0 (base `origin/v17/main`, 97be43d), by porting the finished v18 catch-up (commit `74f9359`, PR Luuk1983/Umbraco.Community.AdvancedPermissions.AI#10) without its Library parts, plus the v17-only gaps and the v18 improvements v17 never got.

**Architecture:** The same design as v18, two small services consumed by the existing tools:
- `IStartNodeReachResolver` wraps the base's public `IStartNodeAccessService` and returns a friendly `StartNodeReach` only when start nodes block the subject. Tools mark such results "Blocked (start nodes)", keep the permissions-only result in `IfInsideStartNodes`, and never suggest a fix.
- `IPermissionLookupGuard` answers "may the current user inspect others?" (Users-section access).
- `UserGroupNameResolver` maps the user group the model names to its real alias, or refuses.

The rest is copy (concepts, editors guide, grounding), the `document-types` audit domain, the test guards and CI, all backported from v18 with the Library removed.

**Tech Stack:** .NET 10, Umbraco 17, Umbraco.AI 17 (`[AITool]`), xUnit + NSubstitute.

**Spec:** the gap analysis, `Memory/Plans/2026-11-05-ai-catch-up-gap-analysis.md` on branch `feature/v18_ai_catch_up` (read with `git show feature/v18_ai_catch_up:Memory/Plans/2026-11-05-ai-catch-up-gap-analysis.md`). This plan covers its v17 items: A1–A5, B1, B3, B4, C1–C5. Reference: the v18 plan (`Memory/Plans/2026-11-05-v18-ai-catch-up-plan.md`, same branch) and the v18 code (`git show 74f9359:<path>`).

## What is different from v18 (verified, not assumed)

Checked in base `origin/v17/main` (97be43d = tag `v17.5.0`, on nuget.org):

- **`IStartNodeAccessService` exists, public, same namespace** (`Umbraco.Community.AdvancedPermissions.Services.StartNodes`), registered as a singleton in the base composer. **But it is the older, smaller shape:**
  - v17: `Task<StartNodeSnapshot?> GetForUserAsync(Guid userKey)`, `Task<StartNodeSnapshot?> GetForUserGroupAsync(string userGroupAlias)`, `StartNodeAccess Classify(StartNodeSnapshot snapshot, string nodePath)`.
  - **There is no `StartNodeScope`, no `StartNodeTree`, no `GetScopeFor*Async`, no `StartNodeTreeDefinition`, no scope cache.** v18's resolver classifies a cached scope first and loads the snapshot only when blocked; v17 loads one snapshot and uses it for both.
  - The models used by the v18 resolver exist with the same names and members: `StartNodeAccess` (Accessible, PathOnly, NotVisible), `StartNodeSubjectState` (NotApplicable, RootAccess, Restricted, NoAccess), `StartNodeSourceKind`, `StartNodeSourceStatus.InUse`, `StartNodeSource(Kind, UserGroupAlias, UserGroupName, StartNodeId)`, `StartNodeSourceResult`, `StartNodeInfo(Id, Key, Name, Path)`, `StartNodeSnapshot(State, EffectiveStartNodeIds, Nodes, Sources)`.
  - `GetForUserGroupAsync("$everyone")` returns `StartNodeSnapshot.NotApplicable`, and `Classify` returns `Accessible` for it.
- **`IDocTypePermissionRepository.GetByRoleAsync` and `DocTypePermissionEntry` are identical to v18.** `IDocTypePermissionService.ResolveCreateForRolesAsync` has **no verb parameter** on v17 (4 args); keep the v17 call sites as they are.
- `AdvancedPermissionsConstants.VerbCreateOfType` exists on v17. The v17 doc-type store holds only that verb (no Library element-type entries share it).
- **Copy sources match v18 for content:** base `en.ts` `startNodes_badgePathOnly`, `startNodes_badgePathOnlyTitle`, `startNodes_badgeNotVisible`, `startNodes_badgeNotVisibleTitle`, `startNodes_introNoAccessUser`, `startNodes_introNoAccessRole`, `startNodes_sourceUser`, `startNodes_sourceGroups`, `startNodes_sourceUserAndGroups`, `startNodes_roleModeNote`, `contentRoot` ("Default permissions") are word-for-word the strings v18's code uses. There are no `startNodes_library_*` keys on v17.
- **v17 navigation** (base `manifests.ts` + `en.ts`): Users section → **Editors** (`Content Permissions Editor`, `Document Type Permissions Editor`) and **Viewers** (`Access Viewer`, `Insert Options Viewer`). v18's "Users section → Content Permissions → Permissions Editor" paths do **not** exist on v17.
- **Help docs** on v17: `concepts.md` "Start nodes" has seven bullets (no Library bullet, no Umbraco 18 upgrade note); `access-viewer.md` and `insert-options-viewer.md` carry the start-node sections; there is no Library viewer.
- **CMS floor:** base 17.5.0 needs Umbraco ≥ 17.4.0 (since #64), and Umbraco.AI.Startup 17.0.0 also needs 17.4.0, so the computed floor stays **17.4.0**. Re-derive from the nuspecs in Task 0; do not restate from memory.

## Global Constraints

- **Work and branch.** Worktree `D:\github\Umbraco.Community.AdvancedPermissions.AI\.claude\worktrees\v17-ai-catch-up`, branch `v17/feature/ai_catch_up`, cut from `v17/main` (8d8a9a9). The main checkout stays on `feature/v18_ai_catch_up`; do not touch it.
- **No commits, pushes or PRs.** Leave everything uncommitted (maintainer rule; it overrides the skill's commit steps, which this plan therefore omits).
- **Test-first:** write each test, run it, watch it fail, then implement.
- **Base dependency:** `Umbraco.Community.AdvancedPermissions` `[17.5.0,18.0.0)`. Plain stable upper bound.
- **Read base source via git only:** `git -C C:/GitHub/UmbracoAdvancedSecurity show origin/v17/main:<path>`. Its working tree is on an unrelated branch. Umbraco reference source: `D:\github\UmbracoVersions\v17\Umbraco-CMS`.
- **No Library anything:** no `PermissionDomain`, element types, `uap_explain_library_access`, Library start nodes, `LibraryItemNameResolver`, `library-no-start-node`. Where a v18 signature takes `PermissionDomain`, the v17 one drops the parameter.
- **Copy rules.** Every editor-facing string comes from base `localization/en.ts` or `help-docs/en/*.md` on `origin/v17/main`, or follows existing AI terminology ("entry", "user group", "the X permission", "All Users"). Never "role" or "$everyone" in model-visible or editor-facing text. No raw ids, aliases or enum names in model-visible output.
- **Model-visible schema:** every new public model type goes into `ToolSchemaTerminologyTests.ModelVisibleTypes()` or `DeliberatelyNotModelVisible` with a reason (Task 1 adds the guard that enforces this).
- **C# conventions:** primary constructors, `var`, records, XML docs on public and protected members (warnings are errors, including missing `<param>` tags). New parameters go **before** the `CancellationToken`. NSubstitute: never configure a substitute inside `Returns(...)`.
- **Namespace collision:** keep `Umbraco.Cms.*` in `using` directives only.
- **MinVer re-stamps** `src/.../wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/umbraco-package.json` on every build. Restore it with `git checkout -- <that path>` at the end (Task 13).

## Review Focus

1. **A user whose only group has a start node on a sibling branch asks "can I delete this?" about a node outside it.** Expect Blocked, never Allowed, and no fix. Pinned in Task 5: `CurrentUser_OutsideStartNodes_IsBlockedNotAllowed`, `SuggestFix_WhenBlocked_ReturnsNoRemediations`.
2. **An Administrator, or any subject with root access.** Expect output identical to today: no `StartNodes`, no `IfInsideStartNodes`, no roster flags. Pinned in Task 5: `RootAccess_LeavesVerdictUnchanged`; Task 6: `ParentAccessible_Unchanged`.
3. **The All Users group, as subject or in a roster.** Start nodes don't apply to it; never reported blocked. Pinned in Task 4: `ForUserGroup_Everyone_ReturnsNull`; Task 5: `AllUserGroups_BlockedGroupsFlaggedButKeepTheirResult`.
4. **A copilot user without the Users section asks about their own access.** It keeps working; only other subjects and the audit are refused. Pinned in Task 2: `CurrentUser_WithoutUsersSection_StillAnswers`.
5. **The model passes a display name ("Writers") or a name that does not exist.** Display name resolves to the alias; an unknown name is refused with the list of real groups, never resolved as "a group with no entries". Pinned in Task 3: `DisplayName_ResolvesToAlias`, `Unknown_IsRefusedWithTheRealGroups`, and the tool-level `UserGroupByDisplayName_ResolvesBeforeLookup`.

---

### Task 0: Dependency floor

**Files:**
- Modify: `Directory.Packages.props` (base `PackageVersion` line and its comment)
- Modify: `README.md` (Requirements), `CLAUDE.md` (Dependencies bullet)

- [ ] **Step 1: Bump the floor.** Set `Umbraco.Community.AdvancedPermissions` to `[17.5.0,18.0.0)`. Comment, README and CLAUDE.md: 17.5.0 is the floor because it is the first release with the public `IStartNodeAccessService`, which the tools use to account for start nodes (and it carries the #41/#58/#61/#64 fixes whose symptoms the copilot otherwise contradicts).
- [ ] **Step 2: Re-derive the CMS floor.** After restore, read `~/.nuget/packages/umbraco.community.advancedpermissions/17.5.0/*.nuspec` and the `Umbraco.AI.Startup` 17.0.0 nuspec. Expected: both floors `17.4.0`, so the computed CMS floor stays 17.4.0. If it differs, update README Requirements, CLAUDE.md and the `Directory.Packages.props` comment to the derived value.
- [ ] **Step 3: Verify.** `dotnet build` and `dotnet test`. Expected: build succeeds, all existing tests pass, `obj/project.assets.json` resolves `Umbraco.Community.AdvancedPermissions/17.5.0`.

### Task 1: Test guards backport (C3)

Done first so every later model type is forced through the guard.

**Files:**
- Modify: `tests/.../ToolSchemaTerminologyTests.cs`
- Create: `tests/.../ToolLocalizationTests.cs` (port of `git show 74f9359:tests/Umbraco.Community.AdvancedPermissions.AI.Tests/ToolLocalizationTests.cs`, unchanged; it discovers tool ids by reflection)

**Interfaces:**
- Produces: `ModelVisibleTypes_ListIsComplete` and the `DeliberatelyNotModelVisible` dictionary. v17 exclusions: `AuditFinding`, `RemediationOption`, `AuditReport`, `RemediationActionKind` (same reasons as v18). No `PermissionDomain`/`LibraryNodeKind`/`LibraryItemLookup` entries.

- [ ] **Step 1: Write the tests.** Port `ModelVisibleTypes_ListIsComplete` and `DeliberatelyNotModelVisible` from 74f9359, minus the Library types. Port `ToolLocalizationTests` (`EveryTool_HasALabelAndDescriptionInEveryLocale`, `EveryLocale_DeclaresTheSameToolKeys`).
- [ ] **Step 2: Run them.** `dotnet test --filter "FullyQualifiedName~ToolSchemaTerminologyTests|FullyQualifiedName~ToolLocalizationTests"`. Expected: `ToolLocalizationTests` PASS (three tools, both locales have keys). `ModelVisibleTypes_ListIsComplete` either passes or names a type the v17 list has fallen behind on; if so, add it to the right list and re-run to PASS.

### Task 2: Users-section gate (A2)

**Files:**
- Create: `src/.../Services/IPermissionLookupGuard.cs`, `src/.../Services/PermissionLookupGuard.cs`
- Create: `tests/.../TestGuards.cs` (`AllowAll()` now; `NeverBlocked()` is added in Task 4)
- Modify: `Composing/AdvancedPermissionsAiComposer.cs` (singleton)
- Modify: `Tools/ExplainAccessTool.cs`, `Tools/AuditPermissionsTool.cs` (constructor parameter, early return, one Description sentence each)
- Modify: every existing test that constructs those two tools: pass `TestGuards.AllowAll()`
- Test: `tests/.../PermissionLookupGuardTests.cs`; gate cases in `ExplainAccessToolTests.cs`, `AuditPermissionsToolTests.cs`

**Interfaces:**
- Produces: `public interface IPermissionLookupGuard { bool CanInspectOthers(); }` and `public sealed class PermissionLookupGuard(IBackOfficeSecurityAccessor backOfficeSecurityAccessor)`, true iff `CurrentUser?.AllowedSections` contains `Constants.Applications.Users` (case-insensitive), the base's `SectionAccessUsers` rule. Constants, exact copy:
  - `OtherSubjectRefusal = "Looking up the permissions of other users or user groups needs access to the Users section. You can still ask about your own access."`
  - `AuditRefusal = "Auditing permissions needs access to the Users section."`
- `TestGuards.AllowAll()`: a substitute whose `CanInspectOthers()` returns true.

- [ ] **Step 1: Write the failing tests.**
  - `PermissionLookupGuardTests`: `UserWithUsersSection_CanInspect`, `UserWithoutUsersSection_CannotInspect`, `NoCurrentUser_CannotInspect`.
  - `ExplainAccessToolTests`: `[Theory]` over `User`, `UserGroup`, `AllUserGroups` with the guard false → `AccessError` with `OtherSubjectRefusal`, and `permissions` received no `Resolve*` call; same for `aspect: type-create`. `CurrentUser_WithoutUsersSection_StillAnswers` → an `AccessVerdict`.
  - `AuditPermissionsToolTests.WithoutUsersSection_ReturnsError` → `AccessError` with `AuditRefusal`, repository not read.
  - Description tests: both Descriptions mention "Users section".
- [ ] **Step 2: Watch them fail.** `dotnet test --filter "FullyQualifiedName~PermissionLookupGuard|FullyQualifiedName~WithoutUsersSection"`. Expected: compile failure.
- [ ] **Step 3: Implement.** Port from 74f9359: the guard; `ExecuteAsync` checks it first for every subject except `CurrentUser`; the audit checks it always. Description sentences as in 74f9359 (`ExplainAccessTool`: "Looking up another user, a user group, or every user group needs access to the Users section (the base package's Access Viewer has the same rule); asking about your own access (`current-user`) never does."; audit: "Needs access to the Users section, like the base package's own viewers.").
- [ ] **Step 4: Run all tests.** Expected: PASS, including `ToolDescriptionTerminologyTests`.

### Task 3: User group names (`UserGroupNameResolver`)

**Files:**
- Create: `src/.../Services/UserGroupNameResolver.cs` (port of 74f9359, unchanged)
- Modify: `Tools/ExplainAccessTool.cs`, `Tools/AuditPermissionsTool.cs` (resolve first, after the gate)
- Modify: `Models/AiToolArgs.cs` (both `UserGroupAlias` descriptions)
- Test: `tests/.../UserGroupNameResolverTests.cs`; tool cases in `ExplainAccessToolTests.cs`, `AuditPermissionsToolTests.cs`

**Interfaces:**
- Produces: `public sealed record UserGroupMatch(string? Alias, string? Error);` and `public sealed class UserGroupNameResolver(IUserGroupService userGroupService)` with `Task<UserGroupMatch> ResolveAsync(string aliasOrName)`. Matches alias or name case-insensitively, pages through `GetAllAsync(skip, 100)`; "$everyone", "All Users" and "everyone" → `$everyone`; unknown → `Error = "There is no user group called '{wanted}'. The user groups are: {names}."` (names start with "All Users"). `UserGroupMatch` is a public type in `Services`, not `Models`, so the Task 1 guard does not see it (as on v18).
- Arg description, exact: `"The user group to evaluate: its name as shown (e.g. 'Editors') or its alias, or 'All Users'. Required when subject is 'user-group'."`; the audit's: `"The user group whose stored permission entries to audit: its name as shown (e.g. 'Editors') or its alias, or 'All Users'. Required when scope is 'user-group'."`

- [ ] **Step 1: Write the failing tests.** Port `UserGroupNameResolverTests` from 74f9359 (`Alias_ResolvesToItself`, `DisplayName_ResolvesToAlias`, `MatchIsCaseInsensitive`, `AllUsersNames_ResolveToEveryone`, `Unknown_IsRefusedWithTheRealGroups`, `SecondPage_IsSearched`; use 74f9359's names where they differ). Tool level: `UserGroupByDisplayName_ResolvesBeforeLookup` (the resolver is called with the alias, not the name), `UnknownUserGroup_ReturnsErrorWithoutLookup`, and the audit equivalent.
- [ ] **Step 2: Watch them fail.** `dotnet test --filter "FullyQualifiedName~UserGroupName|FullyQualifiedName~UnknownUserGroup|FullyQualifiedName~ByDisplayName"`. Expected: compile failure.
- [ ] **Step 3: Implement.** Port the resolver; in each tool replace `args.UserGroupAlias` with the resolved alias (`args = args with { ... }`) for `subject: user-group` (both aspects) and `scope: user-group`. The schema guard has `"$everyone"` banned in model-visible text: the descriptions say "All Users".
- [ ] **Step 4: Run all tests.** Expected: PASS.

### Task 4: Start-node reach resolver (A1, v17 shape)

**Files:**
- Create: `src/.../Models/StartNodeReach.cs` (port of 74f9359, unchanged: `StartNodeRef`, `StartNodeReach` with `BlockedResult`, `PathOnlyAccess`, `NotVisibleAccess`, `RosterNote`)
- Create: `src/.../Services/IStartNodeReachResolver.cs`, `src/.../Services/StartNodeReachResolver.cs`
- Modify: `Composing/AdvancedPermissionsAiComposer.cs` (singleton); `tests/.../ToolSchemaTerminologyTests.cs` (add `StartNodeReach`, `StartNodeRef`); `tests/.../TestGuards.cs` (`NeverBlocked()`)
- Test: `tests/.../StartNodeReachResolverTests.cs`

**Interfaces:**
- Consumes (base v17.5.0): `IStartNodeAccessService.GetForUserAsync(Guid)`, `GetForUserGroupAsync(string)`, `Classify(StartNodeSnapshot, string)`; `IEntityService.Get(Guid, UmbracoObjectTypes.Document)`.
- Produces:
  ```csharp
  public interface IStartNodeReachResolver
  {
      Task<StartNodeReach?> ForUserAsync(Guid userKey, Guid nodeKey);
      Task<StartNodeReach?> ForUserGroupAsync(string userGroupAlias, Guid nodeKey);
  }
  public sealed class StartNodeReachResolver(IStartNodeAccessService startNodes, IEntityService entityService) : IStartNodeReachResolver;
  ```
- **Contract** (`null` = not blocked, or cannot tell):
  - `null` when: `Accessible`; snapshot `null` (unknown user or group); alias is All Users (checked before any call, case-insensitive); node path unreadable (entity missing or empty path). Note the base controllers report an unreadable path as NotVisible; the AI deliberately does not claim a block it cannot verify (v18 rule).
  - Path: `AdvancedPermissionsConstants.VirtualRootNodeKey` → `"-1"` (blocked for restricted subjects, so creating at the root is blocked for them); otherwise the document's `Path`.
  - One snapshot per call, used for both `Classify` and the start-node list.
  - Copy, identical to v18's content strings (base `en.ts` keys in the v18 resolver's comments): `Access` from `startNodes_badgePathOnly`/`startNodes_badgeNotVisible`; `Explanation` from `startNodes_badgePathOnlyTitle`/`startNodes_badgeNotVisibleTitle`, or `startNodes_introNoAccessUser`/`startNodes_introNoAccessRole` when `State == NoAccess`; `SetBy` from `startNodes_sourceUser`/`startNodes_sourceGroups`/`startNodes_sourceUserAndGroups`, only `InUse` sources, grouped per start node, a node with no name reads "the root"; `Note` = v18's `GroupNote` for `ForUserGroupAsync` only; `HowToFix` = v18's `ContentFix`.

- [ ] **Step 1: Write the failing tests** (NSubstitute fakes for `IStartNodeAccessService` and `IEntityService`; port from `git show 74f9359:tests/.../StartNodeReachResolverTests.cs` and drop the Library cases):
  - `ForUser_Accessible_ReturnsNull`
  - `ForUser_PathOnly_ReturnsBrowseOnlyWithStartNodes`: `Access == "Browse only (start nodes)"`, `StartNodes` holds `("News", "from Editors")`
  - `ForUser_NotVisible_ReturnsNotVisible`
  - `ForUser_NoAccess_UsesNoAccessExplanation`: `Explanation` equals the `startNodes_introNoAccessUser` text
  - `ForUser_StartNodeSetOnUserAndGroup_SaysBoth`: `SetBy == "set on the user and from Editors"`
  - `ForUser_OnlyInUseSourcesListed` (a `CoveredBy` source is left out)
  - `ForUserGroup_Everyone_ReturnsNull` (and the service is never called)
  - `ForUserGroup_Blocked_CarriesGroupNote`
  - `ForUser_UnknownUser_ReturnsNull`
  - `ForUser_UnreadablePath_ReturnsNull`
  - `VirtualRoot_ClassifiesPathMinusOne`: `Classify` received `"-1"`
- [ ] **Step 2: Watch them fail.** `dotnet test --filter FullyQualifiedName~StartNodeReachResolverTests`. Expected: compile failure.
- [ ] **Step 3: Implement** the resolver and register it. Add `TestGuards.NeverBlocked()` (both methods `ReturnsForAnyArgs(null)`).
- [ ] **Step 4: Run all tests.** Expected: PASS, including `ModelVisibleTypes_ListIsComplete`.

### Task 5: Start nodes in `uap_explain_access`, node aspect (A1)

**Files:**
- Modify: `Models/AccessExplanation.cs` (`AccessVerdict`: `string? IfInsideStartNodes = null`, `StartNodeReach? StartNodes = null`; `AccessExplanation`: `StartNodeReach? StartNodes = null`)
- Modify: `Models/AccessRoster.cs` (`AccessRoster`: `IReadOnlyList<string>? BlockedUserGroups = null`, `string? StartNodeNote = null`)
- Modify: `Tools/ExplainAccessTool.cs`; existing tool tests pass `TestGuards.NeverBlocked()`
- Test: `tests/.../ExplainAccessStartNodeTests.cs` (port of 74f9359, node-aspect part)

**Interfaces:**
- Consumes Task 4's resolver.
- Produces the blocked convention Task 6 reuses: `Result = StartNodeReach.BlockedResult`, `IfInsideStartNodes` = the permission-only result, `Reasons` kept, `Remediations = null`. In an all-verbs explanation the reach sits once on `AccessExplanation.StartNodes`, not on each verdict. Roster: a flagged group stays in its allowed/denied list, is also in `BlockedUserGroups`, and `StartNodeNote = StartNodeReach.RosterNote`; both `null` when nobody is flagged.

- [ ] **Step 1: Write the failing tests.**
  - `CurrentUser_OutsideStartNodes_IsBlockedNotAllowed`: resolver Allowed, reach PathOnly → `Result == "Blocked (start nodes)"`, `IfInsideStartNodes == "Allowed"`, `StartNodes.Access == "Browse only (start nodes)"`.
  - `User_AllVerbs_Blocked_MarksEveryVerdictAndExplanation`.
  - `UserGroup_Blocked_CarriesGroupNote`.
  - `RootAccess_LeavesVerdictUnchanged`: reach null → verdict equals today's, both new fields null.
  - `SuggestFix_WhenBlocked_ReturnsNoRemediations`: `remediation.SuggestAsync` not received.
  - `AllUserGroups_BlockedGroupsFlaggedButKeepTheirResult`: Editors blocked, Writers not, Allowed and Denied mixed → Editors in its allowed/denied list AND in `BlockedUserGroups`; `StartNodeNote == RosterNote`; All Users never in `BlockedUserGroups`; no group blocked → both null.
- [ ] **Step 2: Watch them fail.** `dotnet test --filter FullyQualifiedName~ExplainAccessStartNodeTests`. Expected: compile failure.
- [ ] **Step 3: Implement** by porting 74f9359's `ExplainAccessTool` changes for the node aspect: reach once per subject (`ForUserAsync` for user/current user, `ForUserGroupAsync` for a group and per roster group), the two private `Block` helpers, skip `AttachRemediationAsync` when blocked, roster `Blocked` bucket. Description: replace the "A Deny entry is a common, invisible cause — don't attribute a block to a structural reason…" sentence and add the `Blocked (start nodes)` sentence, both exactly as in 74f9359.
- [ ] **Step 4: Run all tests.** Expected: PASS.

### Task 6: Start nodes in `uap_explain_access`, type-create aspect (A1)

**Files:**
- Modify: `Models/TypeCreateExplanation.cs` (`TypeCreateVerdict`: `string? IfInsideStartNodes = null`, `StartNodeReach? StartNodes = null`; `TypeCreateExplanation`: `StartNodeReach? StartNodes = null`; `TypeCreateRoster`: `IReadOnlyList<string>? BlockedUserGroups = null`, `string? StartNodeNote = null`)
- Modify: `Tools/ExplainAccessTool.cs` (type-create methods)
- Test: `tests/.../ExplainAccessTypeCreateTests.cs` (new cases, as in 74f9359)

**Interfaces:** classifies the **parent** (`args.NodeKey`), base rule "Creating under a node needs access to that node" (`insert-options-viewer.md`). Port 74f9359's `BlockTypeCreate` helper: a type that is structurally not an allowed child stays "Not applicable".

- [ ] **Step 1: Write the failing tests.** `ParentOutsideStartNodes_ApplicableTypesBlocked_NotApplicableStays`, `CreateAtRoot_RestrictedUser_Blocked` (parent = virtual root), `UserGroup_FocusedType_BlockedCarriesReach`, `Roster_BlockedGroupsFlaggedButKeepTheirResult`, `ParentAccessible_Unchanged`.
- [ ] **Step 2: Watch them fail.** `dotnet test --filter FullyQualifiedName~ExplainAccessTypeCreateTests`. Expected: FAIL.
- [ ] **Step 3: Implement** as in 74f9359, keeping v17's 4-argument `ResolveCreateForRolesAsync`.
- [ ] **Step 4: Run all tests.** Expected: PASS.

### Task 7: "Default permissions" label (A5)

**Files:**
- Modify: `Services/PermissionPresenter.cs:31` (`VirtualRootLabel`), the doc comment at `IPermissionPresenter.cs:48`
- Test: `PermissionPresenterTests.cs:154,238,313`, `AuditPermissionsToolTests.cs:124`

- [ ] **Step 1: Change the expected strings** to `"Default permissions"` (base `en.ts` `contentRoot`).
- [ ] **Step 2: Watch them fail.** `dotnet test --filter "FullyQualifiedName~PermissionPresenterTests|FullyQualifiedName~AuditPermissionsToolTests"`. Expected: FAIL.
- [ ] **Step 3: Change the constant and the comment.**
- [ ] **Step 4: Run all tests.** Expected: PASS.

### Task 8: `document-types` audit domain (C1)

**Files:**
- Modify: `Models/AiToolArgs.cs` (`public enum AuditDomain { Content, DocumentTypes }` with v18's XML docs minus Library, serialized like the other arg enums as `content`/`document-types`; `AuditPermissionsArgs.Domain = AuditDomain.Content` as the last parameter, with v18's description minus Library)
- Modify: `Models/PermissionAuditModels.cs` (`AuditFinding`: `Guid? ContentTypeKey = null`; `FriendlyAuditFinding`: `string? ContentType = null`)
- Modify: `Services/IPermissionAuditAnalyzer.cs`, `Services/PermissionAuditAnalyzer.cs` (`AuditReport Analyze(IReadOnlyList<AdvancedPermissionEntry> entries, AuditDomain domain = AuditDomain.Content)`)
- Modify: `Services/PermissionPresenter.cs` (`ToFriendlyAuditAsync` names the content type; `BuildFriendlyMessage` gets the `everyone-broad-create-deny` sentence and the "on {contentType}" suffixes, from `git show 2074e0b:src/.../Services/PermissionPresenter.cs` minus Library)
- Modify: `Tools/AuditPermissionsTool.cs` (constructor gains `IDocTypePermissionRepository docTypeRepository` and `IUserGroupService userGroupService`, before the guard; `AnalyzeCreateFilterAsync`; Description)
- Modify: `tests/.../ToolSchemaTerminologyTests.cs` (add `AuditDomain`)
- Test: `tests/.../PermissionAuditAnalyzerCreateFilterTests.cs`, `tests/.../AuditPermissionsCreateFilterTests.cs`, `tests/.../AuditPermissionsDomainTests.cs` (ports of 2074e0b/74f9359, content and document-types rows only)

**Interfaces:**
- Analyzer (port of 2074e0b, Library removed): `everyone-broad-write` fires only for `Content` (read verb `VerbRead` excluded); for `DocumentTypes`, `everyone-broad-create-deny` (Risk, `"Everyone is denied '{verb}' across the whole tree from the root."`) fires for an All Users **Deny** at the virtual root with `ThisNodeAndDescendants`; a broad Allow there is not reported. The domain-independent rules (conflict, manage-permissions, priority override) run in both.
- Tool (port of 2074e0b `AnalyzeCreateFilterAsync`, Library removed):
  - Entries come from `docTypeRepository.GetByRoleAsync(alias)`: the named group for `scope: user-group`; for `subtree`/`all`, every group from `userGroupService.GetAllAsync` paged by 100 plus All Users. **Never inferred from the All Users entries.** Keep only `Verb == AdvancedPermissionsConstants.VerbCreateOfType`; for `subtree`, only entries on the subtree's node keys (virtual root included for `all`).
  - Analyze **one content type at a time** (two types at one node are not a conflict), map each `DocTypePermissionEntry` to an `AdvancedPermissionEntry` for the analyzer, and set `ContentTypeKey` on its findings.
  - Description: port 2074e0b's domain paragraph with only `content` and `document-types`; drop README's "covers node permission entries only" sentence.

- [ ] **Step 1: Write the failing tests.**
  - Analyzer: `Analyze_CreateFilter_EveryoneAllowAcrossTheTree_IsNotARisk`, `Analyze_CreateFilter_EveryoneDenyAcrossTheTree_IsARisk`, `Analyze_CreateFilter_NarrowlyScopedDeny_IsNotABroadRisk`, `Analyze_NodePermissions_BroadDeny_DoesNotFireTheCreateFilterRule`, `Analyze_CreateFilter_DomainIndependentRulesStillFire`, `Analyze_DomainOmitted_StillDefaultsToContent`.
  - Tool: `DocumentTypes_UserGroupScope_AuditsInsertOptionEntries`, `DocumentTypes_SubtreeScope_IsSupported`, `DocumentTypes_AllScope_FindsEntriesBelongingOnlyToANamedGroup`, `DifferentContentTypesAtTheSameNode_AreNotReportedAsAConflict`, `SameContentTypeWithAllowAndDeny_IsStillReportedAsAConflict`, `CreateFilterFinding_NamesTheContentType`, `CreateFilterFindings_NeverLeakRawIdentifiers`, `ContentDomain_ReadsNoDocTypeEntries`.
- [ ] **Step 2: Watch them fail.** `dotnet test --filter "FullyQualifiedName~CreateFilter|FullyQualifiedName~AuditPermissionsDomain"`. Expected: compile failure.
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run all tests.** Expected: PASS, including `ModelVisibleTypes_ListIsComplete` and `ToolDescriptionTerminologyTests`.

### Task 9: `uap_explain_editors` for v17's four surfaces (C2, fixes A3)

**Files:**
- Create: `src/.../Models/EditorGuide.cs`: `EditorSurface(Name, Purpose, UseWhen, NotThis, Navigation)` as v18; `EditorGuide(string HowToChoose, string EditorVersusViewer, string PermissionsVersusCreateFilters, IReadOnlyList<EditorSurface> Surfaces, string Navigation)` (v18's `PerNodeVersusSectionWide` is dropped: all four v17 surfaces are per node). `ExplainEditorsArgs` (empty record) in `Models/AiToolArgs.cs` as v18.
- Create: `src/.../Tools/ExplainEditorsTool.cs` (`[AITool("uap_explain_editors", "Explain the permission editors", ScopeId = "advanced-permissions:read")]`)
- Modify: `Tools/ExplainConceptsTool.cs` (`HowToChange`), `lang/en.js`, `lang/nl.js` (`uapExplainEditorsLabel`/`Description`), `tests/.../ToolSchemaTerminologyTests.cs` (`ExplainEditorsArgs`, `EditorGuide`, `EditorSurface`)
- Test: `tests/.../ExplainEditorsToolTests.cs` (port of 74f9359, for four surfaces); `tests/.../ExplainConceptsToolTests.cs:112`

**Content** (v18's structure and wording, re-cut for one tree and v17 names, sourced from base v17 `help-docs/en/*.md`):
- `HowToChoose`: two questions, in order: which mechanism (permissions on nodes, or the create filter for document types), then change it or just see it (Editor vs Viewer). Two × two = four.
- `EditorVersusViewer`, `PermissionsVersusCreateFilters`: v18's text without Library mentions. `PermissionsVersusCreateFilters` carries B4: "an Allow there only KEEPS a type that is already offered; it can never make a type appear that Umbraco does not already permit. A Deny takes it away."
- Surfaces and navigation, exact names:

  | Name | Navigation |
  |---|---|
  | Content Permissions Editor | Users section → Editors → Content Permissions Editor |
  | Access Viewer | Users section → Viewers → Access Viewer |
  | Document Type Permissions Editor | Users section → Editors → Document Type Permissions Editor |
  | Insert Options Viewer | Users section → Viewers → Insert Options Viewer |

  Each `NotThis` names another surface as the alternative. The two viewers carry the start-node text from v18's Content Access Viewer / Document Type Access Viewer surfaces (blocked cells, start-node card, click a blocked cell, user-group mode caveat; "Creating under a node needs access to that node").
- `Navigation`: "All four are in the backoffice's Users section. Its sidebar has two groups: 'Editors' (Content Permissions Editor, Document Type Permissions Editor), where permissions are changed, and 'Viewers' (Access Viewer, Insert Options Viewer), which are read-only. Only people with access to the Users section can open them."
- `ExplainConceptsTool.HowToChange` becomes: Users section → Editors → Content Permissions Editor; pick the user group, find the node (or the Default permissions row), click the cell, set Allow or Deny with a scope, Save; the All Users single-node example stays; document-type creation is the Document Type Permissions Editor in the same group; "For which of the four screens answers a particular question, call uap_explain_editors."

- [ ] **Step 1: Write the failing tests.**
  - Port from 74f9359 with v17 data: `Guide_CoversExactlyTheFourSurfaces`, `Guide_IncludesSurface` (theory over the four names), `EverySurface_StatesWhatItIsNotAndNamesAnAlternative`, `EverySurface_HasPurposeUseWhenAndNavigation`, `EverySurface_NavigationMatchesTheRealSidebar` (theory: name, group "Editors"/"Viewers", menu item), `Guide_ExplainsEditorVersusViewer`, `Guide_ExplainsPermissionsVersusCreateFilters` (asserts "only KEEPS" / "never make a type appear"), `Guide_HowToChoose_AsksBothQuestions`, `Guide_Navigation_NamesBothSidebarGroups`, `Description_RoutesWhereQuestionsHereAndHandsOffTheRest`, `Guide_NeverLeaksRawIdentifiers`, `ViewerSurfaces_MentionStartNodeBlocks`, `EditorVersusViewer_ExplainsBlockedCells`, `Guide_NeverMentionsTheLibrary`.
  - `ExplainConceptsToolTests.HowToChange_NamesTheRealBackofficeNavigation`: asserts "Users section", "Editors", "Content Permissions Editor", and `DoesNotContain("'Content Permissions' in the sidebar")`.
- [ ] **Step 2: Watch them fail.** `dotnet test --filter "FullyQualifiedName~ExplainEditors|FullyQualifiedName~HowToChange|FullyQualifiedName~ToolLocalization"`. Expected: FAIL (compile failure for the editors tests; the concepts test fails on "Editors").
- [ ] **Step 3: Write the tool and the copy.**
- [ ] **Step 4: Run all tests.** Expected: PASS, including `ToolLocalizationTests` (four tools now) and `ModelVisibleTypes_ListIsComplete`.

### Task 10: Concepts and grounding text (B1, B3, B4, A2 grounding)

**Files:**
- Modify: `Models/PermissionConcepts.cs` (add `string StartNodes` after `InsertOptions`, with XML doc)
- Modify: `Tools/ExplainConceptsTool.cs` (`StartNodes` text; `InsertOptions` nuance; Description adds "start nodes" and "why a page is missing from the tree")
- Modify: `Context/AdvancedPermissionsGroundingContributor.cs` (`ConceptsGrounding`, `DocumentGrounding`, class doc comment at line 19)
- Modify: `lang/en.js`, `lang/nl.js` (`uapExplainConceptsDescription` mentions start nodes)
- Test: `ExplainConceptsToolTests.cs`, `AdvancedPermissionsGroundingContributorTests.cs`

**Copy:**
- `StartNodes`: v18's text minus the Library and Umbraco 18 sentences, covering the seven v17 `concepts.md` bullets: checked before any permission; one start node, root, or none (none = no content access); groups combine, a higher start node covers a lower one; start nodes and permissions are separate; a user-account start node replaces group start nodes on the same branch, even root, and adds access elsewhere; Administrators only via root access; deleted or trashed ones ignored, user falls back to groups; pickers/search differ. Plus the viewers' blocked-cell labels, and "No permission entry can change that; start nodes are set in the Users section, on the user group or on the user account."
- `InsertOptions` (B4 + start nodes), appended: "An Allow entry never adds a type that Umbraco does not already permit as a child; it only keeps it available. A Deny entry takes it away. Creating under a node also needs access to that node through start nodes."
- Read controls tree visibility (B3), in `StartNodes` or a closing sentence of `Model`: "A page is only shown in the content tree to users who have the Read permission on it and can reach it through their start nodes. A page missing from the tree, or an empty tree, means Read is denied there or the page is outside the user's start nodes."
- `ConceptsGrounding`: add "start nodes" to the trigger list; add v18's two Users-section sentences ("All of those editors and viewers are in the Users section…never send anyone else there…"), reworded for v17's four screens, plus one pointer: "For WHERE to go or WHICH screen to use, call uap_explain_editors." If the existing always-on size test would fail, keep the new text in `DocumentGrounding` instead and say so in the report.
- `DocumentGrounding`: replace the first sentence with v18's ("If a user cannot perform an action, cannot see a page, or the editor appears read-only, a Deny entry or the user's start nodes are the likely causes…" through "…on the user group or the user account.").

- [ ] **Step 1: Write the failing tests.**
  - `Concepts_StartNodes_CoversTheBaseBullets`: contains "before any permission", "no content access", "combine", "user account", "Administrators", "ignored".
  - `Concepts_StartNodes_NeverMentionsTheLibrary`.
  - `Concepts_InsertOptions_AllowOnlyKeepsAType` ("never adds", "takes it away") and `Concepts_InsertOptions_MentionsStartNodes`.
  - `Concepts_ExplainsMissingFromTree` ("missing from the tree", "Read").
  - `Grounding_Concepts_SendsNobodyToScreensTheyCannotOpen`: always-on grounding contains "Users section" and "uap_explain_editors".
  - `Grounding_Document_NamesStartNodesAsCause`: contains "start nodes" and "cannot see a page", no longer "before concluding the cause is structural".
  - `Grounding_NeverSuggestEntryForStartNodeBlock`.
  - Description test: concepts Description mentions "start nodes".
- [ ] **Step 2: Watch them fail.** `dotnet test --filter "FullyQualifiedName~ExplainConcepts|FullyQualifiedName~Grounding"`. Expected: FAIL.
- [ ] **Step 3: Write the copy.**
- [ ] **Step 4: Run all tests.** Expected: PASS, including `ToolDescriptionTerminologyTests`, `ToolLocalizationTests` and the "definitions stay in the tool" test.

### Task 11: Shared `build-package` CI action (C4)

**Files:**
- Create: `.github/actions/build-package/action.yml` (copy of `git show 74f9359:.github/actions/build-package/action.yml`, unchanged; nothing in it is v18-specific)
- Modify: `.github/workflows/ci.yml`, `.github/workflows/publish.yml` (as at 74f9359: `package` job using the action plus a separate `solution` job; publish uses the action)

- [ ] **Step 1: Copy the action and the two workflows' job bodies.** Keep v17's `publish.yml` trigger and NuGet login steps.
- [ ] **Step 2: Dry-run the action's commands locally** (restore and build the two projects, `dotnet test` by test csproj, `dotnet pack --no-build -o ./artifacts`, then the two shell checks via the Bash tool on the produced `.nupkg`). Expected: one manifest, two lang files, dependencies `Umbraco.Community.AdvancedPermissions -> [17.5.0, 18.0.0)` and `Umbraco.AI.Startup -> [17.0.0, 18.0.0)`, no prerelease marker. Delete `./artifacts` afterwards.

### Task 12: Docs (C5)

**Files:**
- Modify: `CLAUDE.md`
  - Read the base via git, never its working tree (`git -C C:/GitHub/UmbracoAdvancedSecurity show origin/v17/main:<path>`).
  - New parameters go before the `CancellationToken`.
  - Reference source: `D:\github\UmbracoVersions\v17\Umbraco-CMS\…` (not `C:\GitHub\UmbracoVersions\v17\…`).
  - Grounding-mirrors-UI bullet: the real navigation is Users section → Editors / Viewers, not "Content Permissions".
  - Port v18's gotchas, without Library: start nodes are applied by the tools (blocked never carries a fix; structurally N/A wins over blocked; All Users never blocked; unknown = not claimed; rosters flag but keep), the Users-section gate, user group names resolved never trusted, the model-visible type list guard, and the editors guide's `NotThis` rule. Add: v17's `IStartNodeAccessService` has no scope cache, so the resolver loads one snapshot per call.
  - The tool count and `uap_explain_editors` in the concepts bullet.
- Modify: `README.md` (four tools, the Users-section rule, start-node answers, the `document-types` audit domain, Requirements)
- Modify: `docs/architecture.md` (tool box: four AITool classes; services list adds the guard, the name resolver and the start-node resolver)

- [ ] **Step 1: Update the three files.**
- [ ] **Step 2: Check.** `dotnet test` still green (description/README-linked tests, if any).

### Task 13: Mutation checks and final verification

- [ ] **Step 1: Mutation checks.** For each mutation: make it, run the named filter, confirm at least one test FAILS, revert. Record each result in the report.

  | Rule | Mutation | Filter |
  |---|---|---|
  | Blocked when not Accessible | `access == Accessible ? null : …` → always `null` | `StartNodeReachResolverTests` |
  | All Users never blocked | delete the `IsEveryone` early return | `StartNodeReachResolverTests` |
  | Unknown subject not claimed | treat a `null` snapshot as NotVisible | `StartNodeReachResolverTests` |
  | NoAccess explanation | drop the `State == NoAccess` branch | `StartNodeReachResolverTests` |
  | Virtual root = "-1" | look the virtual root up as a document | `StartNodeReachResolverTests` |
  | Blocked verdict has no fix | call `AttachRemediationAsync` when blocked | `ExplainAccessStartNodeTests` |
  | Roster keeps the result | skip adding a flagged group to allowed/denied | `ExplainAccessStartNodeTests` |
  | N/A wins over blocked | remove `!isApplicable` from `BlockTypeCreate` | `ExplainAccessTypeCreateTests` |
  | Gate rule | `CanInspectOthers` returns `true` | `PermissionLookupGuardTests` |
  | Current user bypasses the gate | gate `CurrentUser` too | `ExplainAccessToolTests` |
  | Gate applies to others | drop the gate from `ExecuteAsync` | `ExplainAccessToolTests`, `AuditPermissionsToolTests` |
  | Names resolve | match alias only, not name | `UserGroupNameResolverTests` |
  | Unknown refused | return the input as alias when unmatched | `UserGroupNameResolverTests`, `ExplainAccessToolTests` |
  | All Users names | remove "All Users" from the everyone names | `UserGroupNameResolverTests` |

- [ ] **Step 2: Full run.** `dotnet build`, `dotnet test`, `dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/`. Expected: all green, no warnings; nuspec dependency reads `Umbraco.Community.AdvancedPermissions [17.5.0, 18.0.0)`.
- [ ] **Step 3: Live check** (needs an Umbraco AI connection; see the choices). Run the v17 AI TestSite from this worktree (`dotnet run --project tests/Umbraco.Community.AdvancedPermissions.AI.TestSite --urls http://localhost:5000`; fresh SQLite DB via unattended install, starter kit content). Log in with the repo's test credentials. Create test data: a user group with a content start node on one branch and Read/Update/Delete; a user in only that group, with the Content section but not the Users section; a second user with a start node and the Users section. For each case, compare the copilot's answer with the base Access Viewer / Insert Options Viewer for the same subject and node:
  - Administrator: "Who can delete <node>?", "What can <group> do on <node>?" (by display name). Expect results matching the Access Viewer, the flagged group listed with the start-node note.
  - User with a start node: "Can I delete <node outside branch>?" → Blocked by start nodes, who set them, no entry suggested. "Can I delete <node inside branch>?" → matches the viewer. "What can I create under <node outside>?" → blocked.
  - User without the Users section: "What can Editors do here?" → the refusal; "Can I publish this?" (own access) → answered. "How do I change a permission?" → no instruction to open the Users section; says an administrator can.
- [ ] **Step 4: Restore and report.** `git checkout -- src/Umbraco.Community.AdvancedPermissions.AI/wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/umbraco-package.json`. Stop the TestSite. Report changed files, test counts, mutation results, live-check results, and anything not verified. Leave everything uncommitted.

---

## In plain language

- **Start nodes:** today the copilot can say "yes, you can delete that" for a page the user can't even reach, and suggest a permission change that wouldn't help. After this, it checks the user's start nodes first, says "blocked by start nodes, set by X", and never suggests a permission as the fix.
- **Privacy:** today any copilot user can look up anyone's permissions. After this, only people with access to the Users section can, as in the base package; everyone can still ask about their own access.
- **Group names:** the copilot will understand "Writers" as the Writers group instead of quietly treating it as an empty group.
- **Correct directions:** the copilot currently points to a "Content Permissions" menu that doesn't exist on v17. It will name the real screens (Users section → Editors / Viewers), and a new "which screen do I need?" tool explains the four of them.
- **Catch-up from v18:** the audit can also check who may create which document types; it explains why a page is missing from the tree and how Insert Options really work; and the test and release checks v18 has come along.
- **Requires** base package 17.5.0 or newer. Everything is built test-first, then checked by deliberately breaking the key rules, and finally tried in the real copilot.

**Choices for you:**
- Live check: you add an Umbraco AI connection (provider and key) in the v17 test site when I ask, since I can't enter API keys myself. Or skip the live check and rely on the tests.
- B3's optional audit rule ("All Users denied Read across the whole site", which hides content from everyone): leave it out to keep v17 and v18 in step (recommended, can be a follow-up for both), or add it on v17 now.
