# Umbraco.Community.AdvancedPermissions.AI — Claude Code Guide

## Project Overview

Optional **AI companion** for `Umbraco.Community.AdvancedPermissions`. Ships native C# Umbraco AI
copilot tools (`[AITool]`s via `Umbraco.AI`) that explain effective permissions, find which user
groups can access a node, and audit permission configuration — read-only, grounded in the base
package's permission-resolution engine. **Umbraco v17-first** (targets `net10.0`).

## Guiding principle — where "how" comes from

The **v18 `Umbraco.Community.AdvancedPermissions` repo** (`C:\GitHub\UmbracoAdvancedSecurity_v18`)
is the canonical reference for *how* to do things — csproj/MSBuild structure, workflows,
`Directory.Build.props`, `.editorconfig`, packaging conventions. Only the **targeted versions**
differ: Umbraco-line packages are pinned to v17; build/test tooling tracks the v18 repo. Use the
base repo's `v17/feature/umbraco-ai-integration` branch only for AI-package *content*, not conventions.

## Solution structure

    src/
      Umbraco.Community.AdvancedPermissions.AI/          # The package (Razor SDK): Tools, Services, Models, Scopes, wwwroot/App_Plugins
    tests/
      Umbraco.Community.AdvancedPermissions.AI.Tests/    # xUnit + NSubstitute unit tests
      Umbraco.Community.AdvancedPermissions.AI.TestSite/ # Minimal bootable Umbraco 17 site + AI copilot for manual verification
    Memory/Plans/                                        # Design + implementation plans

No Core/Data/Client projects: the package has no persistence and no Vite/Lit frontend — its
"frontend" is hand-written static `App_Plugins` assets (`umbraco-package.json` + `lang/*.js`).

## Dependencies

- **`Umbraco.Community.AdvancedPermissions`** (`[17.5.0,18.0.0)`) — the base package. A real
  runtime dependency: the tools resolve its services from DI. **17.5.0 is the floor** because it is the
  first release with the public `IStartNodeAccessService`, which the tools use to account for start
  nodes; it also keeps out the base versions with the #41/#58/#61/#64 bugs, whose symptoms the copilot
  would otherwise contradict (NuGet installs the lowest matching version). It flows `Abstractions` transitively,
  so the package compiles against the contract without a separate reference. **No direct
  `Umbraco.Cms.*` references** — they arrive transitively (the CMS floor is implicit; see below).
  Upper bounds are **plain stable versions** — nuget.org rejects a `-0` bound at push time (see RELEASE.md).
- **`Umbraco.AI.Startup`** (`[17.0.0,18.0.0)`) — the Umbraco AI runtime (its composer registers the
  services). It flows `Umbraco.AI.Core`, the `[AITool]` authoring contract the code compiles against.
  **Never go back to `Umbraco.AI.Core` alone**: Core ships `AIContextPickerPropertyValueConverter`,
  which Umbraco type-scans and registers, and it needs `IAIContextService` — registered only by Startup.
  A site with this package but no runtime then fails to boot in Development ("Unable to resolve service
  for type 'Umbraco.AI.Core.Contexts.IAIContextService'"); Production boots only because nothing resolves
  the converter. `Umbraco.AI.Agent` depends on Startup the same way. **Not the `Umbraco.AI` meta package
  either**: it also pins `Umbraco.AI.Web.StaticAssets`, and with lowest-applicable resolution our 17.0.0
  floor would hold the backoffice UI there while another package lifts the runtime. The UI stays a host
  concern (the TestSite runs `Umbraco.AI` 17.2.0). Realigned to the CMS major in 2026.06.
- **The effective Umbraco CMS floor is `17.4.0`**, and it is *computed*, never declared here. Read it off
  the dependencies' own nuspecs at the version our floor resolves to: `Umbraco.AI.Startup` 17.0.0 (and
  the `Umbraco.AI.Core`/`Persistence`/`Web` 17.0.0 it pins) requires `Umbraco.Cms.* [17.4.0, 17.999.999)`
  and the base package 17.5.0 requires `[17.4.0, 18.0.0)`, so the floor is 17.4.0. Do not restate this number
  from memory (it was documented as 17.4.2 for a while and that was wrong) — re-derive it from the
  nuspecs whenever a dependency version moves, and update the README's Requirements section to match.
  Note Umbraco AI's `17.999.999` ceiling: the package physically cannot install on Umbraco 18.

## Version sync (backoffice == NuGet)

`SyncUmbracoPackageVersion` (a `SetJsonVersion` RoslynCodeTaskFactory task, `AfterTargets="MinVer"`)
stamps `$(MinVerVersion)` into `wwwroot/App_Plugins/.../umbraco-package.json`. The NuGet version is
the same MinVer value, so the two never drift. The committed source keeps a `0.0.0` placeholder.

## Build & run

    dotnet build
    dotnet test
    dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/

    # Manual AI verification (needs your own LLM provider + key — see README):
    dotnet run --project tests/Umbraco.Community.AdvancedPermissions.AI.TestSite --urls http://localhost:5000

## Gotchas

- **Namespace collision with `Umbraco.Cms`**: the project namespace is
  `Umbraco.Community.AdvancedPermissions.AI`, so the compiler sees `Umbraco` as a parent namespace.
  Keep all `Umbraco.Cms.*` access in `using` directives (or use `global::Umbraco.Cms...`), never
  inline fully-qualified inside a class declared in this namespace.
- **Versioning**: MinVer, `v`-prefixed tags (`v17.x.x`). Cutting a release = pushing a tag;
  `publish.yml` does the rest. Ask before tagging/pushing (maintainer preference; no auto-commit).
- **Reference source**: the Umbraco v17 clone is on the **D:** drive —
  `D:\github\UmbracoVersions\v17\Umbraco-CMS\src\Umbraco.Web.UI.Client` (backoffice) and its sibling
  `src\Umbraco.Core` / `Umbraco.Cms.*` projects (backend). An earlier version of this file pointed at a
  `C:\GitHub\UmbracoVersions\v17\…` path that does not exist. Do not read `node_modules`.
- **Read the base package through git, never its working tree.** The base repo is
  `C:\GitHub\UmbracoAdvancedSecurity`; its checkout is usually on some unrelated branch, so read the v17
  line as `git -C C:/GitHub/UmbracoAdvancedSecurity show origin/v17/main:<path>` (after a `fetch`). The v18
  line lives in `C:\GitHub\UmbracoAdvancedSecurity_v18` (`origin/v18/main`); base `main` is v19.
- **New parameters go before the `CancellationToken`.** A method's `CancellationToken` stays its last
  parameter, so callers can keep passing it positionally.
- **Definitions live in a tool; only style lives in the prompt.** `uap_explain_concepts`
  (`Tools/ExplainConceptsTool.cs`, no args, no I/O) returns the conceptual reference — model, precedence,
  scopes, Priority Override, Insert Options, start nodes, backoffice navigation — so its cost is paid only
  when asked. Its sibling `uap_explain_editors` (`Tools/ExplainEditorsTool.cs`) is the guide to the base
  package's four screens. The always-on `ConceptsGrounding` (~750 tokens) keeps only what a tool *cannot*
  deliver: terminology and
  readability rules (they govern every sentence, including paraphrases of other tools' output), the
  read-only stance, the Users-section rule, and **pointers at the two tools**. That pointer is load-bearing — the bug it fixes was the
  model answering confidently and wrongly without realising it needed a reference, so a tool alone would
  not have fired. `DocumentGrounding` (~370 tokens, tool nudges + `suggestFix`/`GrantedBy`/`Caution` rules)
  is *appended* on document conversations, making those a strict superset, contributed as one part.
  When adding a definition, put it in the tool — never back into the prompt (a test enforces this).
- **Navigation text mirrors the base package UI**: `ExplainEditorsTool` and `ExplainConceptsTool.HowToChange`
  hard-code base-package UI labels and navigation so the copilot can explain *how* to change permissions in
  the backoffice. On v17 the Users section sidebar has two groups: **Editors** (Content Permissions Editor,
  Document Type Permissions Editor) and **Viewers** (Access Viewer, Insert Options Viewer). There is **no
  "Content Permissions" group** on v17 — that is the v18 layout, and the first v17 release named it anyway
  (fixed in the catch-up; `Guide_NeverNamesTheInventedSidebarGroup` and the HowToChange test hold it). The
  grounding itself names no screen; it points at the tools. Source of truth is the base repo's
  `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/*.md` and `src/.../manifests.ts` (menu/
  section registration). If those labels or the navigation change, update the grounding string and its tests
  to match. The grounding must also **define** the concepts, not just name them — `help-docs/en/concepts.md`
  is the definitional source and settles disputes. Two the copilot got wrong when left undefined: *unset*
  means inherit (there is no `Inherit` state — no entry is stored, and it denies only when nothing up the
  tree allows), and *Priority Override* is a per-entry flag for when a user's **groups** disagree, **not** an
  ancestor-versus-descendant conflict. Insert Options invert the default (creatable unless filtered/denied).
- **The editors guide is boundaries-first.** `uap_explain_editors` exists because the four screens' names
  are easy to confuse. `EditorSurface.NotThis` is load-bearing: a test asserts every surface names *another*
  surface as the alternative. Keep it that way — do not let it decay into four blurbs.
- **Start nodes are applied by the tools, not by the resolver.** Umbraco checks start nodes *before* any
  permission, but the base package's resolvers (`ResolveAsync`, `ResolveForRoleAsync`, `ResolveCreate*`)
  answer from entries alone — the base applies start nodes only in its viewer controllers. So the explain
  tool asks `IStartNodeReachResolver` (over the base's public `IStartNodeAccessService`, 17.5.0+) and, when
  it returns a reach, reports `Blocked (start nodes)` with the permission-only result in
  `IfInsideStartNodes` and the reach in `StartNodes`. Rules worth keeping:
  - A blocked answer **never carries a fix**: no permission entry can change a start-node block, so
    `suggestFix` is skipped.
  - **Structurally "Not applicable" wins over blocked** for document types, mirroring the Insert Options Viewer.
  - **The All Users group is never blocked**, and an unknown subject or unreadable node path is never
    claimed as blocked (`null` = not blocked *or* cannot tell). The base controllers show an unreadable
    path as not visible; the copilot does not claim what it cannot verify.
  - **A group's own start node is not the whole story.** Users combine the start nodes of all their groups,
    and a permission from one group also applies inside a start node another group provides. So rosters
    keep a flagged group in its allowed/denied list, ALSO list it in `BlockedUserGroups`, and attach
    `StartNodeNote` (`StartNodeReach.RosterNote`); both are `null` when nobody is flagged, so unaffected
    answers read exactly as before.
  - **A group with no start node at all is flagged apart.** It is blocked on every node by design (the stock
    "Sensitive data" group only adds rights), so the full caveat on every roster was noise. A blocked reach
    with no start nodes in effect goes to `NoStartNodeUserGroups` with the shorter
    `StartNodeReach.NoStartNodeRosterNote`, which tells the model to mention it only when the question is
    about one of those groups; `BlockedUserGroups` keeps only groups whose start node is elsewhere.
  - **v17's base service is the older shape**: `GetForUserAsync(Guid)`, `GetForUserGroupAsync(string)`,
    `Classify(StartNodeSnapshot, string)` — no `StartNodeScope`, `StartNodeTree` or scope cache (v18 added
    those). The resolver therefore loads one snapshot per lookup and uses it both to classify and to list
    the start nodes in effect.
- **Lookups of others need the Users section.** The base gates its Access Viewer, by-role and audit
  endpoints behind `SectionAccessUsers` so a user cannot inspect someone else's permissions; the tools call
  the services directly, so `IPermissionLookupGuard` re-applies that gate. `current-user` is always allowed;
  `user`, `user-group`, `all-user-groups` and the whole audit are refused without Users-section access. The
  grounding also tells the copilot never to send someone without that section to the editors and viewers.
- **User group arguments are resolved by name, never trusted as an alias.** The model only ever sees user
  groups by display name ("Writers"), so it passes that as `userGroupAlias`. The base package treats an
  unknown alias as a group with no entries, so the call silently resolved as "nothing allows it" (found in
  the v18 live check). `UserGroupNameResolver` maps an alias or display name (case-insensitive) to the real
  alias, in this order: "All Users" or `$everyone` → `$everyone`; a real group's alias (unique, so it
  settles any ambiguity); a display name, **refused with the candidates and their aliases when more than
  one group has it** (never the first match); "everyone" → `$everyone` **only when no real group answers to
  it**, so the shorthand never hides a real "Everyone" group; anything else is refused with the list of real
  groups. Every tool that takes a user group runs it first, after the Users-section gate.
- **The `document-types` audit domain reads per group, and per type.** Insert Options entries are keyed per
  user group, so `all`/`subtree` read every group's entries (`GetByRoleAsync` for All Users plus each
  group) — never inferred from the All Users entries, which would miss types configured only for a named
  group. They are analyzed one document type at a time, because two types at one node share (node, group,
  verb) without being in conflict. The broad rule is mirrored: a broad All Users **Deny** is the risk
  (`everyone-broad-create-deny`), a broad Allow grants nothing — and it is phrased as "unless a more
  specific Allow entry exists further down or a Priority Override Allow entry outranks it", because Insert
  Options resolve by nearest node (an explicit entry beats an inherited one) and, within one level, a Deny
  from any group wins unless an Allow is a Priority Override. `all` keeps
  only entries on live nodes plus the virtual root (the same node set as the content sweep), so the
  description's "deleted nodes are left out" holds in both domains. The manage-permissions rule can never
  fire here (only create entries are read), so the description ties it to `content`; keep the description's
  per-domain check list in step with `PermissionAuditAnalyzer`.
- **The virtual root is "Default permissions" as a label, "the Default permissions row" in a sentence.**
  Fields that name the row (`Node`, `SetOn`) keep the bare label; the presenter's sentences use
  `GetNodeNameInSentence`, decided on the key so a real node with that name keeps its own name.
- **The model-visible type list is guarded.** `ToolSchemaTerminologyTests.ModelVisibleTypes_ListIsComplete`
  reflects over the models namespace and fails if a public type is neither terminology-checked nor
  explicitly excluded with a reason, so adding a returned type forces a decision. `ToolLocalizationTests`
  fails when a tool has no label/description in every `lang/*.js`, and the shared
  `.github/actions/build-package` checks the packed `.nupkg` ships those assets and plain dependency bounds.
  Its Test step also reads the TRX results and fails unless more than zero tests executed, because a
  `dotnet test` that runs nothing reports success.
- **Editor-facing terminology**: the copilot must talk like an editor, not the data model. A record is an
  **"entry"** (never a bare "Allow"/"Deny" noun — say "a Deny entry", or verb it: "deleting is denied"); an
  action is a **"permission"** ("the Delete permission", not bare "Delete"); the collections are **"user
  groups"** (never "roles"); **"node"** stays as the generic content word. "entry" beats "rule" because the
  base package's `concepts.md` uses "entry" and not "rule". Enforced in the grounding string
  (`AdvancedPermissionsGroundingContributor`) and the baked sentences in `PermissionPresenter`
  (`ToRemediationAsync`, `BuildFriendlyMessage`, `GroupsText`) — keep both in step, with tests.
- **Terminology reaches the schema, and stops at the base package.** Before 17.0.0 the rule covered only
  prose; the *values* said `role`. That was the same fluent counter-example the prose rule exists to
  prevent — the model was ordered never to say "roles" while being made to type `subject: "role"` and
  `roleAlias` — so the model-visible schema was renamed: `ExplainSubject.UserGroup`/`AllUserGroups`,
  `AuditScope.UserGroup`, `UserGroupAlias`, and the returned `UserGroup` / `AllowedUserGroups` /
  `DeniedUserGroups` fields. `ToolSchemaTerminologyTests` reflects over every model-visible type to hold
  it, and `roleAlias`/`all-roles`/`scope=role` are banned phrases in the descriptions.
  **The boundary is deliberate**: the base package's API is role-named throughout
  (`AdvancedPermissionEntry.RoleAlias`, `ResolveForRoleAsync`, `ContributingRole`, `EveryoneRoleAlias`),
  so internals that carry one of its values keep the name — `AuditFinding.RoleAlias`,
  `RemediationOption.RoleAlias`, local `roleAliases`, the private `ExplainRoleAsync`/`TypeCreateAllRolesAsync`.
  Neither `AuditFinding` nor `RemediationOption` is ever serialized; the presenter converts them first.
  Renaming those would hide where the value came from. When adding a tool arg or a returned field, use
  "user group"; when calling the base package, "role" is correct.
- **Scope strings are label + meaning, both sourced**: `GetScopeText` pairs the Permissions Editor's own
  dropdown label (base repo `src/.../localization/en.ts` → `scope_thisNodeOnly` etc.) with the plain-English
  meaning taken verbatim from the base repo's `help-docs/en/concepts.md` — e.g. "This node only (the node
  itself, not its children)". The label anchors the copilot to what the editor sees on screen; the meaning
  spares them from knowing what it reaches. Never invent either half. `GetScopeLabel`/`GetScopeGloss` are
  the private halves; the grounding carries the same three meanings.
- **A remediation must explain itself**: removing a Deny entry only grants access because something *else*
  already allows it (an unset permission inherits, and denies only when nothing up the tree allows it), so
  `PermissionRemediator` captures the deciding Allow from the
  confirming re-resolution into `RemediationOption.GrantedBy`, and the presenter renders it as the
  "…because the X user group has an Allow entry …" clause. Never let the copilot assert a removal "would
  then be allowed" without that clause. `AccessRemediation.Caution` carries the Priority Override warning
  (it wins even over a Deny entry, so reviewers see a Deny that is silently not in effect) — the grounding
  requires relaying it and treating removal as the preferred fix.
