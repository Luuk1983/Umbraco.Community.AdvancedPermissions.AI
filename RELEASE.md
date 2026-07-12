# Release checklist — Advanced Permissions for Umbraco (AI companion)

Repo-specific release notes that complement the generic **`nuget-pre-release`** skill. The skill
owns the phase-by-phase workflow; this file records what only this package knows.

## Release model

- **Personal GitHub package** (owned by Luuk Peters), **not** a Proud Nerds package.
- Published from **GitHub Actions** (`publish.yml`), versioned by **MinVer** from a `v`-prefixed git
  tag (`MinVerTagPrefix=v`, auto-increment minor, minimum `17.0`). Cutting the release = pushing the tag.
- **Cut the release manually in GitHub** by default, for control. Ask before anything tags/pushes.
- **Post-release: N/A.** Do **not** invoke `nuget-post-release` (that is for Proud Nerds packages on
  Azure DevOps). The next version comes from the next tag.
- **Independent cadence** from the base package: this add-on ships its own `v*` tags; its MAJOR
  tracks the shared CMS major (17.x).

## Dependency hygiene

Central versions live in `Directory.Packages.props`:

- `Umbraco.Community.AdvancedPermissions` is pinned `[17.2.0,18.0.0-0)` — keep the floor at a
  released stable base version and keep the `-0` upper bound (excludes v18 prereleases).
- `Umbraco.AI` (and the `Umbraco.AI.*` TestSite packages) pinned to the 17.x line.
- The package declares **no** direct `Umbraco.Cms.*` — its floor is implicit via `Umbraco.AI`. The
  only explicit `Umbraco.Cms` pin (TestSite) must stay `>= 17.4.2` (Umbraco.AI's requirement).

## Release notes

`publish.yml` uses `softprops/action-gh-release@v2` with `generate_release_notes: true` — the body
is auto-generated from merged PRs since the previous tag. No `CHANGELOG.md`, no `<PackageReleaseNotes>`.
For a first stable release, curate the body by hand.

## First-publish prerequisites (one-time repo settings)

`publish.yml` uses NuGet **trusted publishing** (`NuGet/login@v1`) and a `production` environment:

- Configure a trusted-publishing policy for `Umbraco.Community.AdvancedPermissions.AI` on nuget.org
  bound to this repo's `publish.yml`.
- Create a `production` GitHub environment and the `NUGET_USER` secret.

## Documentation to keep in sync

- `README.md` — the copilot tools and example prompts; keep in step with the actual `[AITool]`s.
  Prerequisites must state the correct Umbraco floor (**17.4.2+**) and `Umbraco.AI` version.
- Backoffice localization ships as `wwwroot/App_Plugins/.../lang/*.js` (en + nl) once migrated — keep in sync.
- `umbraco-marketplace.json` — review Description / tags / URLs each release; nothing compiles it.

## Build / test / pack

    dotnet build
    dotnet test
    dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/

When inspecting the `.nupkg`: README + `package_logo_128x128.png` packed; `Umbraco.AI` +
`Umbraco.Community.AdvancedPermissions` declared as dependencies (no bundled DLLs); and
`wwwroot/.../umbraco-package.json` version synced by the MinVer build target.
