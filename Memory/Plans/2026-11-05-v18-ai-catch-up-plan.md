# v18 AI Companion Catch-up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the v18 AI companion (`origin/main`) in line with base Advanced Permissions v18.3.0.
- Start nodes become part of every access answer.
- Lookups of other people's access require the Users section, as in the base.
- Concepts, screen guide, labels and grounding match the base's current help docs.

**Architecture:** Two small new services, each consumed by the existing tools.
- `IStartNodeReachResolver` wraps the base's public `IStartNodeAccessService` and returns a friendly `StartNodeReach` only when start nodes block the subject.
- `IPermissionLookupGuard` answers "may the current user inspect others?".

The tools mark blocked results as "Blocked (start nodes)", keep the permission-only result in `IfInsideStartNodes`, and skip fix suggestions for blocked nodes. The text changes are copy edits sourced from base `origin/v18/main`.

**Tech Stack:** .NET 10, Umbraco 18, Umbraco.AI 18 (`[AITool]`), xUnit + NSubstitute.

**Spec:** `Memory/Plans/2026-11-05-ai-catch-up-gap-analysis.md`. This plan covers its v18 items: A1, A2, A4, A5, B1, B2, B5 (optional), D.

## Global Constraints

- **Work and branch.**
  - Work in `D:\github\Umbraco.Community.AdvancedPermissions.AI` on a new branch `feature/v18_ai_catch_up` cut from `origin/main`. The local checkout currently sits on the stale `feature/v18_update`.
  - **No commits, pushes or PRs.** Leave everything as uncommitted changes (maintainer rule; it overrides the skill's commit steps).
- **Test-first:** write each test, watch it fail, then implement.
- **Base dependency:** `Umbraco.Community.AdvancedPermissions` `[18.3.0,19.0.0)`. 18.3.0 is the first base release that registers `IStartNodeAccessService`. The computed CMS floor stays 18.0.0 (re-derive it from the nuspecs; do not restate it).
- **Read base source via git only:** `git -C C:/GitHub/UmbracoAdvancedSecurity show origin/v18/main:<path>`. Base `main` is v19, so never read it for this work.
- **Copy rules.**
  - Every editor-facing string must be copied from base `localization/en.ts` or `help-docs/en/*.md` on `origin/v18/main`, or follow existing AI terminology ("entry", "user group", "the X permission").
  - No raw ids, aliases or enum names in model-visible output.
- **Model-visible schema:** every new public model type must be added to the terminology-checked list in `ToolSchemaTerminologyTests` (`ModelVisibleTypes_ListIsComplete` fails otherwise).
- **XML docs and conventions:** XML docs on every member (private too), primary constructors, `record`, `var`.
- **Namespace collision:** keep `Umbraco.Cms.*` in `using` directives only.

## Review Focus

1. **A user whose only group has a start node on a sibling branch asks "can I delete this?" about a node outside it.** Expect a Blocked result, never Allowed. Pinned in Task 3: `CurrentUser_OutsideStartNodes_IsBlockedNotAllowed`.
2. **An Administrator, or any subject with root access.** Expect output byte-identical to today: no `StartNodes`, no `IfInsideStartNodes`. Pinned in Task 3: `RootAccess_LeavesVerdictUnchanged`.
3. **The All Users group as subject, or in a roster.** Start nodes don't apply to it, so it is never reported as blocked. Pinned in Task 2: `ForUserGroup_Everyone_ReturnsNull`, and in Task 3: the roster test.
4. **A Library folder asked about Publish by a user outside their Library start nodes.** Expect "Not applicable", not Blocked, because N/A wins (base `library-access-viewer.md`). Pinned in Task 5: `Folder_NotApplicableVerb_StaysNotApplicableWhenBlocked`.
5. **A copilot user without the Users section asks about their own access.** Expect it to keep working. Only other subjects and the audit are refused. Pinned in Task 1: `CurrentUser_WithoutUsersSection_StillAnswers`.

---

### Task 0: Branch and dependency floor

**Files:**
- Modify: `Directory.Packages.props` (the base `PackageVersion` line)
- Modify: `README.md:101-102` (Requirements)
- Modify: `CLAUDE.md`, the Dependencies bullet

**Steps:**

- [ ] **Step 1: Create the branch.** Run `git fetch origin && git switch -c feature/v18_ai_catch_up origin/main`. The untracked `Memory/Plans/*.md` files come along.
- [ ] **Step 2: Bump the floor.** Set the base package range to `[18.3.0,19.0.0)`. In the README and CLAUDE.md, replace the "18.1.0 floor is what brings the Library permission APIs" explanation with: 18.3.0 is the floor because it brings `IStartNodeAccessService`, which the tools use to account for start nodes.
- [ ] **Step 3: Verify.** Run `dotnet build` and `dotnet test`. Expected: build succeeds, all existing tests pass, and `obj/project.assets.json` resolves `Umbraco.Community.AdvancedPermissions/18.3.0` (or newer 18.x).

### Task 1: Users-section gate

**Files:**
- Create: `src/.../Services/IPermissionLookupGuard.cs`, `src/.../Services/PermissionLookupGuard.cs`
- Modify: `Composing/AdvancedPermissionsAiComposer.cs` (register as singleton)
- Modify: `Tools/ExplainAccessTool.cs`, `Tools/ExplainLibraryAccessTool.cs`, `Tools/AuditPermissionsTool.cs` (new constructor dependency, early return, one sentence in each Description)
- Modify: `lang/en.js`, `lang/nl.js` (if these copy the descriptions; keep `ToolLocalizationTests` green)
- Test: `tests/.../PermissionLookupGuardTests.cs`, plus gate cases in `ExplainAccessToolTests.cs`, `ExplainLibraryAccessToolTests.cs`, `AuditPermissionsToolTests.cs`

**Interfaces:**
- Produces:
  - `public interface IPermissionLookupGuard { bool CanInspectOthers(); }`
  - The implementation is `PermissionLookupGuard(IBackOfficeSecurityAccessor accessor)`. It returns `true` iff `CurrentUser?.AllowedSections` contains `Constants.Applications.Users`, the same rule as the base's `AuthorizationPolicies.SectionAccessUsers`.
- Error copy, exact:
  - **Explain tools:** `"Looking up the permissions of other users or user groups needs access to the Users section. You can still ask about your own access."`
  - **Audit:** `"Auditing permissions needs access to the Users section."`

**Steps:**

- [ ] **Step 1: Write the failing tests.**
  - `PermissionLookupGuardTests`: `UserWithUsersSection_CanInspect` (true), `UserWithoutUsersSection_CannotInspect` (false), `NoCurrentUser_CannotInspect` (false).
  - `ExplainAccessToolTests`:
    - `[Theory]` over `User`, `UserGroup` and `AllUserGroups` with the guard returning false. Assert the result is an `AccessError` with the exact copy above, and that `permissions` received no `Resolve*` call.
    - `CurrentUser_WithoutUsersSection_StillAnswers`: the guard returns false and the subject is `CurrentUser`. Assert an `AccessVerdict` comes back.
  - Mirror both cases in `ExplainLibraryAccessToolTests`.
  - `AuditPermissionsToolTests.WithoutUsersSection_ReturnsError`: the audit returns an `AccessError` with the audit copy, and no repository was read.
- [ ] **Step 2: Watch them fail.** Run `dotnet test --filter "FullyQualifiedName~PermissionLookupGuard|FullyQualifiedName~WithoutUsersSection"`. Expected: compile failure or FAIL.
- [ ] **Step 3: Implement.**
  - Add the guard and register it.
  - Check it first in `ExecuteAsync` for every subject except `CurrentUser`; the audit tool checks it always.
  - Add one sentence to each Description: other users, user groups and the audit need Users-section access.
  - Existing tests that construct these tools pass a guard substitute returning `true`, so their behaviour is unchanged.
- [ ] **Step 4: Run all tests.** Expected: PASS, including `ToolDescriptionTerminologyTests` and `ToolLocalizationTests`.

### Task 2: Start-node reach resolver

**Files:**
- Create: `src/.../Models/StartNodeReach.cs`, `src/.../Services/IStartNodeReachResolver.cs`, `src/.../Services/StartNodeReachResolver.cs`
- Modify: `Composing/AdvancedPermissionsAiComposer.cs` (register as singleton)
- Modify: `tests/.../ToolSchemaTerminologyTests.cs` (add `StartNodeReach`, `StartNodeRef` to the checked list)
- Test: `tests/.../StartNodeReachResolverTests.cs`

**Interfaces:**
- Consumes the base public API, namespace `Umbraco.Community.AdvancedPermissions.Services.StartNodes`:
  - `IStartNodeAccessService.GetScopeForUserAsync(Guid, StartNodeTree)`
  - `GetScopeForUserGroupAsync(string, StartNodeTree)`
  - `GetForUserAsync`, `GetForUserGroupAsync` (snapshot, for names and attribution)
  - `Classify(StartNodeScope, string nodePath)`
- Produces:
  ```csharp
  public sealed record StartNodeRef(string Name, string SetBy);
  public sealed record StartNodeReach(
      string Access,                      // "Browse only (start nodes)" | "Not visible (start nodes)"
      string Explanation,
      IReadOnlyList<StartNodeRef> StartNodes,
      string HowToFix,
      string? Note = null);

  public interface IStartNodeReachResolver
  {
      Task<StartNodeReach?> ForUserAsync(Guid userKey, Guid nodeKey, PermissionDomain domain);
      Task<StartNodeReach?> ForUserGroupAsync(string userGroupAlias, Guid nodeKey, PermissionDomain domain);
      Task<StartNodeReach?> ForLibraryCreateAsync(Guid? userKey, string? userGroupAlias);
  }
  ```
- **Contract:**
  - `null` means "not blocked, or cannot verify". Covered cases: `Accessible`; an unknown user or group (scope `null`); the All Users alias; a node whose path cannot be read.
  - Trees: `PermissionDomain.Content` maps to `StartNodeTree.Content`, and `Library` to `StartNodeTree.Library`.
  - Node path:
    - `AdvancedPermissionsConstants.VirtualRootNodeKey` has path `"-1"`. The base classifies that as `PathOnly` for restricted subjects, so creating at the root is blocked for them.
    - A content node's path is `IEntityService.Get(key, UmbracoObjectTypes.Document)?.Path`.
    - Library nodes use the element and element-folder object types, as `ElementTreeResolver` does.
  - `ForLibraryCreateAsync` is blocked only when the scope `State == StartNodeSubjectState.NoAccess`. That is the base's section-wide rule (`DocTypePermissionsController`, element-type audit).
- **Copy, from base `en.ts` on `origin/v18/main`:**
  - **`Access`:**
    - `startNodes_badgePathOnly`
    - `startNodes_badgeNotVisible`
  - **`Explanation`:**
    - `startNodes_badgePathOnlyTitle` and `startNodes_badgeNotVisibleTitle`.
    - When `State == NoAccess`, use instead:
      - `startNodes_introNoAccessUser` / `startNodes_introNoAccessRole`
      - their `startNodes_library_*` variants for Library.
  - **`SetBy`:**
    - `startNodes_sourceUser` ("set on the user")
    - `startNodes_sourceGroups` ("from {groups}")
    - Use only sources with status `InUse`.
  - **`Note`:**
    - `startNodes_roleModeNote`, for `ForUserGroupAsync` only.
  - **`HowToFix`**, AI copy:
    - Content: `"Start nodes are set in the Users section, on the user group or on the user account. No permission entry can change this."`
    - Library: `"Library start nodes are set in the Users section, on the user group or on the user account. No permission entry can change this."`

**Steps:**

- [ ] **Step 1: Write the failing tests** (NSubstitute fakes for `IStartNodeAccessService` and `IEntityService`):
  - `ForUser_Accessible_ReturnsNull`
  - `ForUser_PathOnly_ReturnsBrowseOnlyWithStartNodes`. Assert `Access == "Browse only (start nodes)"`, and that `StartNodes` holds `("News", "from Editors")`.
  - `ForUser_NotVisible_ReturnsNotVisible`
  - `ForUser_NoAccess_UsesNoAccessExplanation`. Assert `Explanation` equals the `startNodes_introNoAccessUser` text.
  - `ForUserGroup_Everyone_ReturnsNull`
  - `ForUserGroup_Blocked_CarriesRoleModeNote`
  - `ForUser_UnknownUser_ReturnsNull` (scope `null`)
  - `VirtualRoot_ClassifiesPathMinusOne`. Assert `Classify` received `"-1"`.
  - `Library_UsesLibraryTreeAndCopy`
  - `ForLibraryCreate_Restricted_ReturnsNull`
  - `ForLibraryCreate_NoAccess_ReturnsBlocked`
- [ ] **Step 2: Watch them fail.** Run `dotnet test --filter FullyQualifiedName~StartNodeReachResolverTests`. Expected: FAIL.
- [ ] **Step 3: Implement.** Classify against the cached scope first. Load the snapshot (`GetFor*Async`) only when blocked, to fill `StartNodes`.
- [ ] **Step 4: Run all tests.** Expected: PASS, including `ModelVisibleTypes_ListIsComplete`.

### Task 3: Start nodes in `uap_explain_access`, node aspect

**Files:**
- Modify: `Models/AccessExplanation.cs`
  - Add to `AccessVerdict`: `string? IfInsideStartNodes = null` and `StartNodeReach? StartNodes = null`.
  - Add to `AccessExplanation`: `StartNodeReach? StartNodes = null`.
- Modify: `Models/AccessRoster.cs`. Add to `AccessRoster`: `IReadOnlyList<string>? BlockedUserGroups = null`.
- Modify: `Tools/ExplainAccessTool.cs`
- Test: `tests/.../ExplainAccessStartNodeTests.cs`

**Interfaces:**
- Consumes `IStartNodeReachResolver` (Task 2).
- Produces the blocked-verdict shape that Tasks 4 and 5 reuse:
  - `Result = "Blocked (start nodes)"`
  - `IfInsideStartNodes` = the result the permissions alone give ("Allowed" or "Denied")
  - `Reasons` kept as the would-be reasons
  - `Remediations = null`

**Steps:**

- [ ] **Step 1: Write the failing tests.**
  - `CurrentUser_OutsideStartNodes_IsBlockedNotAllowed`. The resolver says Allowed and the reach resolver returns PathOnly. Assert `Result == "Blocked (start nodes)"`, `IfInsideStartNodes == "Allowed"` and `StartNodes.Access == "Browse only (start nodes)"`.
  - `User_AllVerbs_Blocked_MarksEveryVerdictAndExplanation`. Every verdict is Blocked, and `AccessExplanation.StartNodes` is set once, not repeated on the nested verdicts.
  - `UserGroup_Blocked_CarriesRoleModeNote`
  - `RootAccess_LeavesVerdictUnchanged`. The reach resolver returns null. Assert the verdict equals the verdict computed before this change, with `IfInsideStartNodes` and `StartNodes` both null.
  - `SuggestFix_WhenBlocked_ReturnsNoRemediations`. Assert `remediation.SuggestAsync` was not called.
  - `AllUserGroups_BlockedGroupsListedSeparately`.
    - Editors is blocked and Writers is accessible, with Allowed and Denied mixed.
    - Assert Editors appears in `BlockedUserGroups` and in neither of the other two lists.
    - Assert All Users is never in `BlockedUserGroups`.
    - When no group is blocked, `BlockedUserGroups` is null.
- [ ] **Step 2: Watch them fail.** Run `dotnet test --filter FullyQualifiedName~ExplainAccessStartNodeTests`. Expected: FAIL.
- [ ] **Step 3: Implement.**
  - Resolve reach once per call, per subject: user, current user, user group, and each group in the roster.
  - Apply one private `Block(AccessVerdict, StartNodeReach)` helper. Skip `AttachRemediationAsync` when blocked.
  - In the Description:
    - Replace "A Deny entry is a common, invisible cause — don't attribute a block to a structural reason (root node, content type) before checking this." with "A Deny entry or the user's start nodes are the common, invisible causes — check this before attributing a block to anything else."
    - Add one sentence: "A `Blocked (start nodes)` result means Umbraco blocks the node before any permission is checked; relay its `startNodes` and never suggest a permission entry as the fix."
- [ ] **Step 4: Run all tests.** Expected: PASS, including the existing `ExplainAccessToolTests`, which use a reach resolver substitute returning null by default.

### Task 4: Start nodes in `uap_explain_access`, type-create aspect

**Files:**
- Modify: `Models/TypeCreateExplanation.cs`
  - `TypeCreateVerdict`: add `string? IfInsideStartNodes = null`.
  - `TypeCreateExplanation`: add `StartNodeReach? StartNodes = null`.
  - `TypeCreateRoster`: add `IReadOnlyList<string>? BlockedUserGroups = null`.
- Modify: `Tools/ExplainAccessTool.cs` (type-create methods)
- Test: `tests/.../ExplainAccessTypeCreateStartNodeTests.cs`

**Interfaces:**
- Consumes the Task 2 resolver, classifying the **parent** node (`args.NodeKey`), and the Task 3 blocked-verdict convention.
- Base rule: "Creating under a node needs access to that node" (`insert-options-viewer.md`).

**Steps:**

- [ ] **Step 1: Write the failing tests.**
  - `ParentOutsideStartNodes_AllTypesBlocked`. Applicable types get `Result == "Blocked (start nodes)"` with `IfInsideStartNodes` set. A structurally "Not applicable" type stays "Not applicable".
  - `CreateAtRoot_RestrictedUser_Blocked`. The parent is the virtual root.
  - `Roster_BlockedGroupsListedSeparately`
  - `ParentAccessible_Unchanged`
- [ ] **Step 2: Watch them fail.** Run `dotnet test --filter FullyQualifiedName~ExplainAccessTypeCreateStartNodeTests`. Expected: FAIL.
- [ ] **Step 3: Implement** with the same helper approach as Task 3.
- [ ] **Step 4: Run all tests.** Expected: PASS.

### Task 5: Start nodes in `uap_explain_library_access`

**Files:**
- Modify: `Tools/ExplainLibraryAccessTool.cs`
- Modify: `Models/ElementTypeCreateExplanation.cs`
  - `ElementTypeCreateVerdict`: add `string? IfInsideStartNodes = null`.
  - `ElementTypeCreateExplanation`: add `StartNodeReach? StartNodes = null`.
  - `ElementTypeCreateRoster`: add `IReadOnlyList<string>? BlockedUserGroups = null`.
- Test: `tests/.../ExplainLibraryAccessStartNodeTests.cs`

**Interfaces:**
- Consumes the Task 2 resolver: `ForUser*Async(..., PermissionDomain.Library)` and `ForLibraryCreateAsync`.
- Consumes the Task 3 convention and models.

**Steps:**

- [ ] **Step 1: Write the failing tests.**
  - `Item_OutsideLibraryStartNodes_Blocked`
  - `Folder_NotApplicableVerb_StaysNotApplicableWhenBlocked`. Use Publish on a folder; N/A takes precedence over blocked (base `library-access-viewer.md`).
  - `ElementTypeCreate_NoLibraryStartNode_AllBlocked`. This is the "after an upgrade to Umbraco 18" case. Assert `StartNodes.Explanation` equals the `startNodes_library_introNoAccessUser` text.
  - `ElementTypeCreate_RestrictedButHasStartNode_NotBlocked`
  - `ElementTypeCreate_Roster_GroupsWithoutLibraryStartNodeBlocked`
  - `SuggestFix_WhenBlocked_ReturnsNoRemediations`
- [ ] **Step 2: Watch them fail.** Run `dotnet test --filter FullyQualifiedName~ExplainLibraryAccessStartNodeTests`. Expected: FAIL.
- [ ] **Step 3: Implement.** Check applicability first, as now. Apply the start-node block only to applicable verdicts. Update the Description the same way as Task 3.
- [ ] **Step 4: Run all tests.** Expected: PASS.

### Task 6: "Default permissions" label

**Files:**
- Modify: `Services/PermissionPresenter.cs:44,51`, and the doc comments that quote the old labels (`IPermissionPresenter.cs:77`, `PermissionPresenter.cs:48`)
- Test: update `PermissionPresenterTests.cs:154,238,313`, `AuditPermissionsToolTests.cs:152`, and `PermissionPresenterDomainTests` (the root-label case)

**Steps:**

- [ ] **Step 1: Change the expected strings in the tests.**
  - Content root: `"Default permissions"`, matching base `en.ts` `contentRoot`.
  - Library root: `"Default permissions (Library)"`. The UI label is "Default permissions" (`library_root`). The suffix keeps the existing domain test's guarantee that a Library answer never borrows the content wording.
- [ ] **Step 2: Watch them fail.** Run `dotnet test --filter "FullyQualifiedName~PermissionPresenter|FullyQualifiedName~AuditPermissionsToolTests"`. Expected: FAIL.
- [ ] **Step 3: Change the two constants and the comments.**
- [ ] **Step 4: Run all tests.** Expected: PASS.

### Task 7: Concepts, screen guide and grounding text

**Files:**
- Modify: `Models/PermissionConcepts.cs`. Add `string StartNodes` after `InsertOptions`, with XML doc.
- Modify: `Tools/ExplainConceptsTool.cs`
  - Add the `StartNodes` text.
  - Append to `InsertOptions`: "Creating under a node also needs access to that node through start nodes."
  - Add "start nodes" to the Description's question list.
- Modify: `Tools/ExplainEditorsTool.cs`
  - Content Access Viewer, Library Access Viewer, Document Type Access Viewer, and Library Element Type Access Viewer surfaces.
  - `EditorVersusViewer`.
  - The "verified against v18.1.0" comment becomes v18.3.0.
- Modify: `Context/AdvancedPermissionsGroundingContributor.cs`: `ConceptsGrounding`, `DocumentGrounding`, `LibraryGrounding`.
- Modify: `lang/en.js`, `lang/nl.js` (description keys for concepts, explain and library explain, if mirrored)
- Test: `ExplainConceptsToolTests.cs`, `ExplainEditorsToolTests.cs`, `AdvancedPermissionsGroundingContributorTests.cs`, `GroundingContributorLibraryTests.cs`

**Copy sources (base `origin/v18/main`):**
- `concepts.md` "Start nodes" section: all nine bullets, paraphrased tightly, with the Library bullet and the Umbraco 18 upgrade note.
- `access-viewer.md` and `library-access-viewer.md` "Start nodes" sections: start-node card, blocked cells, "click a blocked cell", user-group-mode caveat, N/A stays N/A.
- `insert-options-viewer.md` and `library-insert-viewer.md`: the one added line each.

**Steps:**

- [ ] **Step 1: Write the failing tests.**
  - `Concepts_StartNodes_CoversTheBaseBullets`. Asserts `StartNodes` contains "before any permission", "no content access", "combine", "user account", "Administrators", "Library" and "Umbraco 18".
  - `Concepts_InsertOptions_MentionsStartNodes`
  - `Editors_ViewerSurfaces_MentionBlockedCells`. Each of the four viewer surfaces mentions "start node".
  - `Editors_LibraryAccessViewer_NotApplicableWinsOverBlocked`
  - `Grounding_Document_NamesStartNodesAsCause`. `DocumentGrounding` contains "start nodes", and no longer contains "before concluding the cause is structural".
  - `Grounding_Library_MentionsUpgradeTrap`. `LibraryGrounding` names "start node" next to "element types are creatable by default".
  - `Grounding_NeverSuggestEntryForStartNodeBlock`. The grounding says a start-node block is not fixed by a permission entry.
  - Keep any existing grounding size budget test green. If the always-on half grows past its budget, put the new text in the domain halves instead.
- [ ] **Step 2: Watch them fail.** Run `dotnet test --filter "FullyQualifiedName~ExplainConcepts|FullyQualifiedName~ExplainEditors|FullyQualifiedName~Grounding"`. Expected: FAIL.
- [ ] **Step 3: Write the copy.**
- [ ] **Step 4: Run all tests.** Expected: PASS, including `ToolDescriptionTerminologyTests` and `ToolLocalizationTests`.

### Task 8 (optional, see choices): audit rule for user groups without a Library start node

**Files:**
- Modify: `Services/PermissionAuditAnalyzer.cs` or `Tools/AuditPermissionsTool.cs` (Library domain only), and the Description's rule list.
- Test: `tests/.../AuditLibraryStartNodeTests.cs`

**Behaviour:**
- New finding kind `library-no-start-node`, severity Warning, for each named user group that has at least one Library **Allow** entry but whose `GetScopeForUserGroupAsync(alias, StartNodeTree.Library)` state is `NoAccess`.
- The friendly message says the entries have no effect until the group gets a Library start node, and that this is the usual state after an upgrade to Umbraco 18.
- Never raised for All Users.

**Steps:**

- [ ] **Step 1: Write the failing tests.** `GroupWithLibraryAllowButNoStartNode_Flagged`, `GroupWithStartNode_NotFlagged`, `Everyone_NeverFlagged`, `ContentDomain_NeverFlagged`.
- [ ] **Step 2: Watch them fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run all tests.** Expected: PASS.

### Task 9: Docs and final verification

**Files:**
- Modify: `CLAUDE.md`
  - The base v18 branch is `v18/main`, not `main` (that is v19).
  - Remove "`concepts.md` is byte-identical between the v17 and v18 lines".
  - New gotcha: start nodes are applied by the tools, via `IStartNodeReachResolver`, not by the resolver; blocked results never carry fixes; N/A wins over blocked.
  - The Users-section gate.
- Modify: `README.md` (tool descriptions and Requirements)
- Modify: `docs/architecture.md` (the "5 AITool classes" box and the services list)

**Steps:**

- [ ] **Step 1: Update the docs** as listed.
- [ ] **Step 2: Run** `dotnet build`, `dotnet test` and `dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/`. Expected: all green. The nuspec dependency reads `Umbraco.Community.AdvancedPermissions [18.3.0, 19.0.0)`.
- [ ] **Step 3: Manual check (needs an LLM key in the TestSite).** On the AI TestSite, with a user restricted to one branch, ask the copilot:
  - "Can I delete <node outside branch>?" Expect: blocked by start nodes, no entry suggested.
  - "Why can't I see the Library?", for a non-admin group. Expect the upgrade explanation.
  - As an editor without the Users section, "what can Editors do here?". Expect the refusal.
- [ ] **Step 4: Report.** List changed files, test counts and anything not verified. Leave everything uncommitted.
