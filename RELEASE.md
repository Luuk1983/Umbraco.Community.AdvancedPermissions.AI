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

- `Umbraco.Community.AdvancedPermissions` is pinned `[17.2.0,18.0.0)` — keep the floor at a released
  stable base version and keep the upper bound a **plain stable version, never `18.0.0-0`**: a `-0`
  bound packs fine locally but **nuget.org rejects it at push time**
  (`400 BadRequest: invalid Version: '18.0.0-0'`), which fails the release tag, not any test. The
  published base package's own nuspec uses plain `[17.3.0, 18.0.0)` bounds — that form is proven.
- `Umbraco.AI.Core` (the package's only other dependency) is pinned `[17.0.0,18.0.0)` — same plain
  upper bound; Umbraco.AI majors track the CMS major. The `Umbraco.AI.*` TestSite packages are
  pinned to the 17.x line but never flow into the nupkg.
- The package declares **no** direct `Umbraco.Cms.*` — its floor is implicit and **computed** from the
  dependencies' nuspecs. Today that is **17.4.0**: `Umbraco.AI.Core` 17.0.0 requires
  `Umbraco.Cms.* [17.4.0, 17.999.999)` and the base package requires `[17.3.0, 18.0.0)`, so the higher
  floor wins. Re-derive it each release rather than repeating it (it was documented as 17.4.2 for a
  while, which was wrong), and keep the README's Requirements section in step. The only explicit
  `Umbraco.Cms` pin (TestSite) must stay at or above that floor.

## Release notes

`publish.yml` uses `softprops/action-gh-release@v2` with `generate_release_notes: true` — the body
is auto-generated from merged PRs since the previous tag. No `CHANGELOG.md`, no `<PackageReleaseNotes>`.
For a first stable release, curate the body by hand.

## First-publish prerequisites (one-time repo settings)

`publish.yml` uses NuGet **trusted publishing** (`NuGet/login@v1`) and a `production` environment:

- Configure a trusted-publishing policy for `Umbraco.Community.AdvancedPermissions.AI` on nuget.org
  bound to this repo's `publish.yml`. Because the package ID has **never been published**, there is
  no package to attach a policy to — it must be created **in advance** using nuget.org's
  package-ID-**pattern** form, before the first push. Getting this wrong fails the release tag.
- Create a `production` GitHub environment and the `NUGET_USER` secret (both exist).
- Optional but cheap insurance: push a prerelease tag first (e.g. `v17.0.0-rc.1`) to prove the whole
  pipeline — trusted publishing, environment gate, push, GitHub release — before the real
  `v17.0.0`. Remember the stable release notes are auto-generated **since the previous tag**, so
  after an rc the stable body must be hand-curated anyway (already the plan below).

## Documentation to keep in sync

- `README.md` — the copilot tools and example prompts; keep in step with the actual `[AITool]`s
  (currently **three**: `uap_explain_access`, `uap_audit_permissions`, `uap_explain_concepts`).
  Prerequisites must state the correct Umbraco floor (**17.4.2+**) and `Umbraco.AI` version. The
  **Setting up Umbraco AI** section is the most load-bearing part of the README — the tools are
  auto-discovered but do nothing until the chat agent's Governance allows the
  `advanced-permissions:read` scope. Re-check that flow against the AI section's UI each release.
  Keep Mermaid out of the README: it is the packed `PackageReadmeFile`, and NuGet.org renders Mermaid
  as raw code. Diagrams live in `docs/architecture.md`.
- `docs/architecture.md` — layering diagram, request trace, and the grounding split. Update the tool
  count and the grounding token figures when either changes.
- **Backoffice localization** ships as `wwwroot/App_Plugins/.../lang/*.js` (en + nl). **One
  label/description pair per tool** — a new `[AITool]` needs a new pair in *both* files or its raw
  key shows in Umbraco AI's "Select Tools" dialog. Nothing compiles these; nothing fails without them.
- `CONTRIBUTING.md` — house rules (test-first, read-only, terminology, definitions-live-in-the-tool).
- `umbraco-marketplace.json` — review Description / tags / URLs each release; nothing compiles it.

## Screenshot inventory

`docs/screenshots/README.md` is the shot-list, with the prompts to type and the framing for each.
The README and `umbraco-marketplace.json` reference these filenames:

| File | Required | Shows |
|------|----------|-------|
| `copilot_explain_access.jpg` | yes | The copilot answering "Why can't I delete this page?" |
| `agent_tool_scopes.jpg` | yes | "Advanced Permissions (read)" ticked in the chat agent's Governance |
| `copilot_suggest_fix.jpg` | no | A `suggestFix` answer with its GrantedBy clause |

They are served from `raw.githubusercontent.com/.../main/docs/screenshots/`, so they only resolve
**after** the branch is merged to `main`. If a shot is skipped, remove its marketplace entry and its
README image line — a broken image is worse than a missing one.

## Build / test / pack

    dotnet build
    dotnet test
    dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/

When inspecting the `.nupkg`: README + `package_logo_128x128.png` packed; `Umbraco.AI` +
`Umbraco.Community.AdvancedPermissions` declared as dependencies (no bundled DLLs); and
`wwwroot/.../umbraco-package.json` version synced by the MinVer build target.
