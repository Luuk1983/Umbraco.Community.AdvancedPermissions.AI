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

- **`Umbraco.Community.AdvancedPermissions`** (`[17.2.0,18.0.0-0)`) — the base package. A real
  runtime dependency: the tools resolve its services from DI. It flows `Abstractions` transitively,
  so the package compiles against the contract without a separate reference. **No direct
  `Umbraco.Cms.*` references** — they arrive transitively (the 17.4.2 floor is implicit via `Umbraco.AI`).
- **`Umbraco.AI`** (`17.0.0`) — the `[AITool]` runtime. Realigned to the CMS major in 2026.06;
  17.0.0 requires `Umbraco.Cms >= 17.4.2`.

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
- **Grounding text mirrors the base package UI**: the grounding line in
  `Context/AdvancedPermissionsGroundingContributor.cs` hard-codes base-package UI labels and navigation
  (e.g. "Users section", "Content Permissions", "Permissions Editor") so the copilot can explain *how* to
  change permissions in the backoffice. Source of truth is the base repo's
  `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/*.md` and `src/.../manifests.ts` (menu/
  section registration). If those labels or the navigation change, update the grounding string and its tests
  to match.
- **Editor-facing terminology**: the copilot must talk like an editor, not the data model. A record is an
  **"entry"** (never a bare "Allow"/"Deny" noun — say "a Deny entry", or verb it: "deleting is denied"); an
  action is a **"permission"** ("the Delete permission", not bare "Delete"); the collections are **"user
  groups"** (never "roles"); **"node"** stays as the generic content word. "entry" beats "rule" because the
  base package's `concepts.md` uses "entry" and not "rule". Enforced in the grounding string
  (`AdvancedPermissionsGroundingContributor`) and the baked sentences in `PermissionPresenter`
  (`ToRemediationAsync`, `BuildFriendlyMessage`, `GroupsText`) — keep both in step, with tests.
