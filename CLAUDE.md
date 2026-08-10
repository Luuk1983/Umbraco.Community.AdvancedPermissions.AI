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

- **`Umbraco.Community.AdvancedPermissions`** (`[17.2.0,18.0.0)`) — the base package. A real
  runtime dependency: the tools resolve its services from DI. It flows `Abstractions` transitively,
  so the package compiles against the contract without a separate reference. **No direct
  `Umbraco.Cms.*` references** — they arrive transitively (the CMS floor is implicit; see below).
  Upper bounds are **plain stable versions** — nuget.org rejects a `-0` bound at push time (see RELEASE.md).
- **`Umbraco.AI.Core`** (`[17.0.0,18.0.0)`) — the `[AITool]` authoring contract the package
  references; the full `Umbraco.AI` runtime is a host concern (the TestSite runs `Umbraco.AI` 17.2.0).
  Realigned to the CMS major in 2026.06.
- **The effective Umbraco CMS floor is `17.4.0`**, and it is *computed*, never declared here. Read it off
  the dependencies' own nuspecs: `Umbraco.AI.Core` 17.0.0 requires `Umbraco.Cms.* [17.4.0, 17.999.999)`
  and the base package requires `[17.3.0, 18.0.0)`, so the higher floor wins. Do not restate this number
  from memory (it was documented as 17.4.2 for a while and that was wrong) — re-derive it from the
  nuspecs whenever a dependency version moves, and update the README's Requirements section to match.
  Note `Umbraco.AI.Core`'s `17.999.999` ceiling: the package physically cannot install on Umbraco 18.

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
- **Reference source**: Umbraco v17 backoffice at `C:\GitHub\UmbracoVersions\v17\src\Umbraco.Web.UI.Client`.
  Do not read `node_modules` — use the reference source.
- **Definitions live in a tool; only style lives in the prompt.** `uap_explain_concepts`
  (`Tools/ExplainConceptsTool.cs`, no args, no I/O) returns the conceptual reference — model, precedence,
  scopes, Priority Override, Insert Options, backoffice navigation — so its cost is paid only when asked.
  The always-on `ConceptsGrounding` (~635 tokens) keeps only what a tool *cannot* deliver: terminology and
  readability rules (they govern every sentence, including paraphrases of other tools' output), the
  read-only stance, and **a pointer at the tool**. That pointer is load-bearing — the bug it fixes was the
  model answering confidently and wrongly without realising it needed a reference, so a tool alone would
  not have fired. `DocumentGrounding` (~300 tokens, tool nudges + `suggestFix`/`GrantedBy`/`Caution` rules)
  is *appended* on document conversations, making those a strict superset, contributed as one part.
  When adding a definition, put it in the tool — never back into the prompt (a test enforces this).
- **Grounding text mirrors the base package UI**: the grounding in
  `Context/AdvancedPermissionsGroundingContributor.cs` hard-codes base-package UI labels and navigation
  (e.g. "Users section", "Content Permissions", "Permissions Editor") so the copilot can explain *how* to
  change permissions in the backoffice. Source of truth is the base repo's
  `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/*.md` and `src/.../manifests.ts` (menu/
  section registration). If those labels or the navigation change, update the grounding string and its tests
  to match. The grounding must also **define** the concepts, not just name them — `help-docs/en/concepts.md`
  is the definitional source and settles disputes. Two the copilot got wrong when left undefined: *unset*
  means inherit (there is no `Inherit` state — no entry is stored, and it denies only when nothing up the
  tree allows), and *Priority Override* is a per-entry flag for when a user's **groups** disagree, **not** an
  ancestor-versus-descendant conflict. Insert Options invert the default (creatable unless filtered/denied).
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
