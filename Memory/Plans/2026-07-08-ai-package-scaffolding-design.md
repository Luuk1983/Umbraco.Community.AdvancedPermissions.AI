# Design — Scaffolding for Umbraco.Community.AdvancedPermissions.AI

- **Date:** 2026-07-08
- **Status:** Approved (design); scaffolding not yet implemented
- **Repo:** `D:\github\Umbraco.Community.AdvancedPermissions.AI` (remote: `Luuk1983/Umbraco.Community.AdvancedPermissions.AI`, branch `main`)

## Context

`Umbraco.Community.AdvancedPermissions.AI` is an **optional AI companion** for the existing
`Umbraco.Community.AdvancedPermissions` package. It exposes native C# Umbraco AI copilot
tools (`[AITool]`s via `Umbraco.AI`) that explain effective permissions, find which user
groups can access a node, and audit permission configuration.

The package is **Umbraco v17-first** (targets `net10.0`, pins Umbraco to the 17 line). The
tooling/structure conventions are mirrored from the v18 `Umbraco.Community.AdvancedPermissions`
repo (the code base the maintainer prefers), but the Umbraco dependency is pinned to v17.

The actual feature code already exists on the base repo's `v17/feature/umbraco-ai-integration`
branch (`src/Umbraco.Community.AdvancedPermissions.AI` + `.AI.Tests`). It was initially planned
as a project inside the base repo but has been promoted to its own package/repo. **This session
scaffolds the new repo; migrating that feature code is a later session.**

**Guiding principle — canonical reference for "how" vs "what version":** The v18
`Umbraco.Community.AdvancedPermissions` repo (`C:\GitHub\UmbracoAdvancedSecurity_v18`) is the
most up-to-date code base and is the **canonical reference for *how* to do things** — csproj
structure, MSBuild targets, workflow layout, `Directory.Build.props`, `.editorconfig`, packaging
conventions, etc. Only the **targeted versions** differ: anything tied to the Umbraco line is
pinned to v17; everything else (build/test tooling and general approach) follows the v18 repo.
Use the v17 feature branch only for AI-package-specific *content*, not for conventions.

## Goal of this session (scope boundary)

Produce a **buildable skeleton** — `dotnet build` succeeds and `dotnet test` is green — with all
tooling, packaging, pipelines and repo files in place. **Do NOT** migrate the feature-branch
tool/service/model code. Concretely:

- Package project ships packaging plumbing + a placeholder
  `wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/umbraco-package.json`
  (version `0.0.0`, no extensions yet). No `.cs` tool code.
- Test project gets one trivial sanity test so CI passes.
- TestSite boots Umbraco 17 with the package + AI copilot referenced, for future manual testing.

## Naming

- PackageId / assembly / root namespace: `Umbraco.Community.AdvancedPermissions.AI` (uppercase
  `.AI`, matching the repo name and `Umbraco.AI`'s own casing).

## Project structure (3 projects, `.slnx` solution)

| Project | SDK | Purpose |
|---|---|---|
| `src/Umbraco.Community.AdvancedPermissions.AI` | `Microsoft.NET.Sdk.Razor` | The package. `StaticWebAssetBasePath=/`, MinVer, SourceLink, `InternalsVisibleTo` the test project, `SyncUmbracoPackageVersion` target. |
| `tests/Umbraco.Community.AdvancedPermissions.AI.Tests` | `Microsoft.NET.Sdk` | xUnit + NSubstitute. References the package project. |
| `tests/Umbraco.Community.AdvancedPermissions.AI.TestSite` | `Microsoft.NET.Sdk.Web` | Bootable Umbraco 17 site referencing the package + `Umbraco.AI.Agent.Copilot` for manual verification. |

No Core, no Data/EF layer, no Vite/Lit Client project — the "frontend" is hand-written static
`App_Plugins` assets (`umbraco-package.json` + `lang/*.js`), not a build output.

## Cross-package dependency: reference the full base package (not just Abstractions)

**Decision:** the package takes a single cross-package NuGet dependency on
`Umbraco.Community.AdvancedPermissions`, pinned `[17.2.0, 18.0.0-0)`.

Rationale (verified against the v17 main-package csproj):

- The base package declares `Abstractions` as a **real, non-private** NuGet dependency and
  **bundles** the Core/Data *implementations* inside itself.
- The AI tools resolve the base package's **service implementations** from DI at runtime
  (`IAdvancedPermissionService`, `IDocTypePermissionService`, `IPermissionResolver`). Depending
  only on `Abstractions` would allow an install without the implementations → runtime DI failure
  (Abstractions' own description says "Not functional on its own").
- Depending on the base package makes a broken install impossible **and** still lets us
  "compile against abstractions" — the base package flows `Abstractions` transitively, so one
  dependency covers both the compile-time contract and the runtime guarantee.
- This is the standard idiom for an add-on that needs its host's services at runtime, and matches
  what the mono-repo feature branch actually did (real dependency on the base package).
- Floor `17.2.0` = the current released base/Abstractions version; `-0` upper bound excludes v18
  prereleases. We do **not** carry the mono-repo's `PinBaseDependencyRange` target — that existed
  only to convert an intra-repo `ProjectReference` into a package range; a plain `PackageReference`
  with a CPM range has no such problem.

Migration-time note: the base package flows `Abstractions`, whose domain types still live under the
`Umbraco.Community.AdvancedPermissions.Core.*` namespace (a deferred v19 rename). The migrated
`using` directives resolve unchanged.

## Version sync: backoffice version == NuGet version

Mirror the base package's mechanism exactly. An inline `RoslynCodeTaskFactory` task
`SetJsonVersion` regex-replaces the `"version"` field in `umbraco-package.json`; a target
`SyncUmbracoPackageVersion` (`AfterTargets="MinVer"`, `Condition="'$(MinVerVersion)' != ''"`)
stamps `$(MinVerVersion)` into
`wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/umbraco-package.json`.

Because the NuGet package version is **also** `$(MinVerVersion)`, both the backoffice version and
the NuGet version derive from the **same** MinVer git-tag source and are guaranteed identical. The
committed source file holds a `0.0.0` placeholder, overwritten on every build.

## Dependencies (CPM — Directory.Packages.props)

The **package project** references only two NuGet packages directly — `Umbraco.AI` and
`Umbraco.Community.AdvancedPermissions` (plus the private build tools MinVer + SourceLink). It
declares **no** direct `Umbraco.Cms.*` references: both `Umbraco.AI` and the base package bring the
CMS assemblies in transitively (non-private references), so `IContent`, user-group services, etc.
are available at compile time without restating them. The effective `Umbraco.Cms` floor (17.4.2) is
therefore **implicit** — enforced by `Umbraco.AI` 17.0.0's own transitive constraint — and is not
restated in the AI package or its `.nuspec`. (The base package declares the CMS directly only
because its sole other dependency, `Abstractions`, is a pure contract package that carries no
Umbraco dependency; the AI package sits downstream of packages that already provide the CMS.)

Only packages that are *directly* referenced somewhere need a CPM entry. Per the guiding
principle, **Umbraco-line versions are pinned to v17; build/test tooling tracks the v18 repo**
(verify each exact version against `C:\GitHub\UmbracoAdvancedSecurity_v18\Directory.Packages.props`
at implementation time — the numbers below are the current v18 values).

Umbraco-targeted (v17 line):

- **Base package (package project):** `Umbraco.Community.AdvancedPermissions` = `[17.2.0, 18.0.0-0)`.
- **AI runtime (package project):** `Umbraco.AI` = `17.0.0`.
- **CMS meta (TestSite only):** `Umbraco.Cms` = `[17.4.2, 18.0.0)`. The one place the 17.4.2 floor
  is stated explicitly — the TestSite references the `Umbraco.Cms` meta-package directly and CPM
  requires a version for direct references. Pinned to 17.4.2 to match `Umbraco.AI`.
- **AI copilot/providers (TestSite only, manual verification):** `Umbraco.AI.Agent`,
  `Umbraco.AI.Anthropic`, `Umbraco.AI.OpenAI`, `Umbraco.AI.Agent.Copilot` = `17.0.0`.
- **TestSite infra:** `Microsoft.EntityFrameworkCore.Sqlite` = `10.0.6` (host-driven — matches what
  Umbraco 17.4.2 requires); `uSync` = `[17.0.4, 18.0.0)` (optional — only to seed test content).
- **Transient vuln overrides (only if TestSite restore surfaces them; the v18 repo dropped these,
  but the v17 dependency graph may still need them):** `MailKit` 4.16.0,
  `System.Security.Cryptography.Xml` 10.0.6.

Tooling (latest, tracking the v18 repo — NOT the v17 line):

- **Build/versioning:** `MinVer` = `7.0.0`, `Microsoft.SourceLink.GitHub` = `10.0.300`.
- **Testing:** `coverlet.collector` 10.0.1, `Microsoft.NET.Test.Sdk` 18.6.0, `NSubstitute` 5.3.0,
  `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.5.

No `Umbraco.Cms.Core/Infrastructure/Api.Management` entries — nothing references them directly.
The package project's direct references are final (not "refined later"): `Umbraco.AI` +
`Umbraco.Community.AdvancedPermissions`.

## Root tooling & repo files (mirrored from the base repo, adapted)

`Directory.Build.props` (EnforceCodeStyleInBuild + NuGetAuditMode=direct), `Directory.Packages.props`
(above), `NuGet.config`, `.editorconfig`, `.gitignore`, `.gitattributes`, `.github/CODEOWNERS`,
`CLAUDE.md` (rewritten for this package's structure — **must state the guiding principle: the v18
base repo is the canonical reference for conventions; only Umbraco-line versions are pinned to
v17**), `README.md` (first draft based on the
feature-branch AI README, adapted for a standalone repo — install instructions reference both
packages), `RELEASE.md`, `umbraco-marketplace.json`, and `Memory/` + `docs/` directories.
`LICENSE` already present. `.slnx` lists the 3 projects in `src/`+`tests/` folders.

## Icon

Copy the base package's `package_logo_128x128.png` into the package project as a placeholder
(swap for an AI-specific icon later). Referenced by `PackageIcon` and packed at the package root.

## CI/CD (`.github/workflows/`)

Copy `ci.yml` and `publish.yml` from the base repo, adapted:

- **Drop** the Node.js setup + `npm ci`/`npm run build` steps — there is no Vite build; frontend
  assets are committed static files.
- Point `dotnet pack` at `src/Umbraco.Community.AdvancedPermissions.AI/`.
- `ci.yml`: restore → build (Release) → test → pack (verify) → upload artifact, on PRs to `main`.
- `publish.yml`: on `v*.*.*` tags → restore → build → test → pack → NuGet trusted publishing →
  GitHub release. Prerelease flag when the tag contains `-`.

## Versioning

MinVer with tag prefix `v` (standalone repo — no `ai-v` disambiguation needed), so `v*.*.*` tags
drive both the NuGet and backoffice versions and the base repo's `publish.yml` trigger works
unchanged. `MinVerMinimumMajorMinor` = `17.0`; `MinVerAutoIncrement` = `minor`.

## Final step (separate, after the scaffold is pushed)

Enable `main` branch protection + required pull requests via `gh api`, mirroring the base repo.
This runs only once there is content on `main` to protect. Per the maintainer's standing
preference, commits/pushes happen only on explicit request.

## Deferred / later sessions

- Migrate the feature-branch tool/service/model code (`Tools/`, `Services/`, `Models/`, `Scopes/`,
  `lang/*.js`, real `umbraco-package.json` extensions) and its tests.
- Verify all domain types the tools use are available via the base package's transitive
  `Abstractions` (namespace `...Core.*`).
- Replace the placeholder icon with an AI-specific one.
