# v18 update — working plan

Status: in progress. Started 2026-09-02.

Branch: `feature/v18_update` → PR → `main`. `origin/v17/main` already exists at `b5dbdc0`
(frozen v17 maintenance line — do NOT touch it). `ci.yml` fires on PRs to `main` and `*/main`;
`publish.yml` fires on any `v*.*.*` tag, so v17 patches still publish from `v17/main`.

## Settled decisions (do not re-litigate)

1. **Full parity** for the Library domain — the copilot resolves library permissions for real,
   not just describes them.
2. **Sibling tool** `uap_explain_library_access`, not a 4-value aspect on `uap_explain_access`.
   The two differ in *arguments and reasoning* (library element-type-create takes no node at all).
3. **Audit gets a `domain` argument**, not a sibling tool. Those paths differ only in *which
   table to read* — the analyzer is domain-agnostic. This asymmetry with (2) is deliberate.
4. **New tool `uap_explain_editors`** for the 8 editors/viewers. `uap_explain_concepts` stays
   anchored to `concepts.md` (byte-identical between v17 and v18 — the permission *model* did
   not change; the per-editor docs went 5 → 9).
   Maintainer emphasis: **the differences BETWEEN the editors are the point.** Required structure:
   disambiguation table first (which tree / which mechanism / change-vs-see), then the three
   contrast pairs stated explicitly, then per-surface entries that each say what it is NOT and
   name the neighbour to use instead, then navigation.
5. **One cross-cutting `PermissionDomain` enum (`Content` | `Library`)** threaded through the
   shared services — instead of three ad-hoc per-service fixes.
6. Prose is sourced verbatim-in-substance from the base repo's `help-docs/en/*.md` and
   `localization/en.ts`. Never invented. Same rule as the v17 grounding.

## Verified facts (re-derive only if a dependency version moves)

- Computed **CMS floor = 18.0.0**. `Umbraco.AI.Core` 18.3.1 → `Umbraco.Cms.* [18.0.0, 18.999.999)`;
  base package 18.1.0 → `[18.0.0, 19.0.0)`. AI.Core's `18.999.999` ceiling means the package
  **cannot install on Umbraco 19**.
- Base package floor is **18.1.0**, not 18.0.0: the Library element APIs
  (`IElementNodePermissionService`, `ElementVerbs`, `VerbElementCreateOfType`) start there.
- `Umbraco.AI.Core` 18.x API surface used by this package is intact: `AIToolBase<T>`,
  `AIToolAttribute`, `IAIRuntimeContextContributor`, `AIRuntimeContext.SystemMessageParts`,
  `Constants.ContextKeys.EntityType`.
- Base package namespace stays `Umbraco.Community.AdvancedPermissions.Core.*` even though the
  contracts now ship in a separate `.Abstractions` package. It flows transitively — **no new
  direct reference needed**.
- Library entity-type strings are **`element`** and **`element-folder`** (from the base package's
  own condition classes, `models/element-permission.models.ts`).
- **TRAP:** `Umbraco.AI.Core` has both `ContextKeys.EntityType` and `ContextKeys.ElementType`.
  The latter means a *block-editor* element type — nothing to do with Library elements. The
  grounding gate must read `EntityType`. Needs a code comment + a test.
- `ElementTreePathResolver` in the base package is `internal static` → must be re-implemented
  here (same as `ContentPathResolver` already mirrors the base controllers).
- Element types are `IContentTypeService.GetAll().Where(ct => ct.IsElement && ct.AllowedInLibrary)`.
  `AllowedInLibrary` is an Umbraco 18 CMS property — an independent reason the v18 floor is real.
- v18 navigation: *Users* section → one of *Content Permissions* / *Document Type Permissions* /
  *Library Permissions* / *Library Element Type Permissions* → *Permissions Editor* or
  *Access Viewer*. The v17 grounding's single hard-coded menu path is now **wrong**.

## Assumed, to verify in the TestSite

- Whether Umbraco AI's copilot attaches to the **Library workspace** in 18.x. Safe either way:
  if it does not, `LibraryGrounding` never fires and the always-on pointers still route the
  model to the library tools.

## Phases

- [x] **P1 — Migration (done).** Version pins, `MinVerMinimumMajorMinor` → 18.0. Build + test green on
      18 with **no** new features. Pins: base `[18.1.0,19.0.0)`, `Umbraco.AI.Core` `[18.0.0,19.0.0)`;
      TestSite `Umbraco.Cms` 18.1.1, `Umbraco.AI` 18.3.1, `.Agent` 18.1.4, `.Agent.Copilot` 18.0.5,
      `.Anthropic` 18.1.0, `.OpenAI` 18.2.0, `TheStarterKit` 18.0.0. Build/test tooling unchanged.
      Actual breakages found and fixed (all from the v18 abstraction split, none behavioural):
      (a) `Microsoft.EntityFrameworkCore.Sqlite` had to go 10.0.6 → 10.0.10 to match what
      Umbraco.Cms 18.1.1 references (NU1605 downgrade error under TreatWarningsAsErrors).
      (b) `IDocTypePermissionService.ResolveCreateForRolesAsync` gained a DEFAULTED `verb`
      parameter *before* `cancellationToken`, so the positional token silently bound to `verb`.
      Fixed by passing `AdvancedPermissionsConstants.VerbCreateOfType` explicitly at all 3 call
      sites (better than naming the token: it kills the trap and pre-figures the library
      parallel). Test mocks match the verb explicitly too, so a wrong verb cannot pass silently.
      (c) 7 `cref`s pointed at members of `IAdvancedPermissionRepository` /
      `IAdvancedPermissionService`, which are now member-less markers — retargeted to
      `INodePermissionRepository` / `INodePermissionService`.
      Verified: build clean (zero warnings), 148/148 tests pass, `dotnet pack` emits the correct
      `[18.1.0, 19.0.0)` and `[18.0.0, 19.0.0)` ranges.
      NOTE: pack currently stamps 17.0.0 because HEAD sits exactly on the `v17.0.0` tag — MinVer
      uses an exact tag verbatim and ignores MinVerMinimumMajorMinor. Re-verify after the first
      v18 commit; expect `18.0.0-alpha.0.N`.
- [x] **P2 — `PermissionDomain` spine (done).** Enum; `IElementPathResolver`;
      `PermissionPresenter.GetNodeName(key, domain)` + element/container verb labels;
      `PermissionRemediator` selecting `INodePermissionService` on domain;
      `PermissionAuditAnalyzer` domain-aware read-verb (`VerbRead` vs `VerbElementRead`).
      `manage-permissions-descendants` needs nothing — no `Umb.Element.Permissions` verb exists.
      How the domain is threaded, and why it differs per service (deliberate, not inconsistent):
      the nine-method `IPermissionPresenter` is BOUND once via `For(PermissionDomain)` so no signature
      or call site carries it; the single-method remediator and analyzer take a plain `domain`
      argument, where a binding step would be ceremony. All default to `Content`, so every v17 call
      site keeps working. `PermissionDomain` is internal plumbing and NEVER a tool argument — the
      model picks a tool by name instead.
      `GetVerbDisplayName` deliberately takes NO domain: the verb string already says which tree it
      belongs to, and the generic "text after the last dot" is already correct for `Umb.Document.*`,
      `Umb.Element.*` and `Umb.ElementContainer.*`. Only the two create verbs are special-cased —
      "Insert" vs "Create in Library" (the latter from `elementTypePermissions_verbCreate`), kept
      distinct because the per-node and section-wide create filters must not read alike.
      New labels: `UnresolvedLibraryNodeLabel` = "this library item" (the base package's column
      header is "Library Item", so "item" not "node"), and `LibraryVirtualRootLabel` =
      "All library items (root-level default)".
      Remediator now injects BOTH repositories and picks on domain; the pure `IPermissionResolver`
      is shared unchanged, which is why the whole simulate-and-confirm approach carried over free.
      `SuggestAsync`'s new `domain` parameter sits BEFORE `cancellationToken` on purpose: that makes
      every existing positional call a loud compile error instead of a silent mis-bind — the exact
      trap the base package's own `ResolveCreateForRolesAsync` change sprang on us in P1.
      Verified: 189/189 tests pass (41 new), build clean with zero warnings.
      Test-setup gotcha worth remembering: a helper that calls `.Returns()` internally (e.g. building
      a stub entity) clobbers NSubstitute's pending last-call slot, so stubs must be materialised into
      a local BEFORE being passed to an outer `.Returns(...)`.
- [x] **P3 — `uap_explain_library_access` (done).** 4 subjects, Concise/Detailed, `suggestFix`.
      `LibraryAspect.Node` (ElementVerbs via `IElementNodePermissionService`; honour the N/A
      cases — `Create` on a leaf item, `Publish`/`Unpublish`/`Duplicate`/`Rollback` on a folder —
      rather than reporting a resolved Deny where the base package shows a hatched N/A cell) and
      `LibraryAspect.ElementTypeCreate` (**no nodeKey**; section-wide; via
      `IDocTypePermissionService.ResolveCreateForRolesAsync(..., VerbElementCreateOfType)`).
      Built: `LibraryAspect` + `ExplainLibraryAccessArgs`; `ExplainLibraryAccessTool`; the four
      `ElementTypeCreate*` models (separate from the document ones because the noun differs —
      `ElementType` not `DocumentType` — AND because there is no node and no allowed-children check,
      so those fields would be permanently meaningless); `ILibraryElementTypeProvider`;
      `LibraryVerbApplicability`; `LibraryNodeKind`.
      `IElementPathResolver` was RENAMED to `IElementTreeResolver` mid-phase: it also has to answer
      "item or folder?", which needs the same two-object-type expertise, so a third component would
      have duplicated it. `GetNodeKind` uses `IEntityService.GetObjectType(Guid)` — one call, and safe
      for element containers because it materializes no entity.
      Applicability is checked BEFORE resolving, not after: the resolver returns an ordinary Allow/Deny
      for an N/A combination and relaying it is precisely the bug. `Unknown` kind makes NO
      applicability claim (safe failure). Presenter gained `ToNotApplicableVerdict` so the
      "Not applicable" label lives in one place.
      Also added `ToolSchemaTerminologyTests.ModelVisibleTypes_ListIsComplete` — the model-visible
      list is hand-maintained and was silently falling behind; a new model now forces a deliberate
      choice between "terminology-checked" and "excluded, with a reason".
      Verified: 216/216 tests pass, build clean with zero warnings.
- [x] **P4 — Audit `domain` (done).** `content` | `library` | `library-element-types`. Repository swap
      `IAdvancedPermissionRepository` ↔ `IElementPermissionRepository`; `library-element-types`
      reads `IDocTypePermissionRepository` filtered to `Umb.Element.CreateOfType` (element-type
      entries share that table with doc-type entries, distinguished only by verb). Reject
      `scope=subtree` for `library-element-types` — it has no tree.
      Built as `AuditDomain { Content, Library, LibraryElementTypes }`. The element-type path also needed
      Library subtree/all key enumeration (`EnumerateLibraryDescendantKeys` sweeps BOTH Library object
      types — folders carry entries too) and a projection from `DocTypePermissionEntry` onto the shared
      `AdvancedPermissionEntry` shape the analyzer works on.
      `AuditPermissionsDomainTests` covers the trap end-to-end: an unfiltered read of the shared
      create-filter store would report a document-type override as a Library element-type finding.
- [x] **P5 — `uap_explain_editors` + grounding (done).** New tool per decision (4). Grounding goes
      two-part → three: `ConceptsGrounding` (always; +~70 tokens for the editors-tool pointer,
      the two-trees line, and terminology for the new nouns — a library node is an "item" or
      "folder", per the base package's "Library Item" header; the create filter controls
      "element types"), `DocumentGrounding` (gate `document`, substance unchanged),
      `LibraryGrounding` (gate `element` / `element-folder`, mirrors the document half).
      `uap_explain_concepts` gains only two model-level additions: the same machinery governs a
      second tree, and `HowToChange` names the four sidebar groups.
      IMPORTANT correction made during this phase: the base repo working tree at
      `C:\GitHub\UmbracoAdvancedSecurity` has a STALE `manifests.ts` (4 menu items under
      Editors/Viewers). The committed `main` AND the released `v18.1.0` tag both have the 8-item
      structure — 4 sidebar groups (`#uap_group_content`, `_documentTypes`, `_library`,
      `_libraryElementTypes`) each holding `#uap_menuItem_permissionsEditor` and
      `#uap_menuItem_accessViewer`. ALWAYS read the base repo via `git show main:<path>` or a tag, never
      the working tree, and never trust a relative-path grep (the shell cwd resets between Bash calls).
      Surface titles come from committed `en.ts`: Content Permissions Editor / Content Access Viewer /
      Document Type Permissions Editor / Document Type Access Viewer / Library Permissions Editor /
      Library Access Viewer / Library Element Type Permissions Editor / Library Element Type Access Viewer.
      `EditorGuide` is ordered boundaries-first (HowToChoose, then the three contrast pairs, then the
      eight surfaces) because the failure to prevent is answering about the WRONG screen, and
      `EditorSurface.NotThis` is the load-bearing field — a test asserts every surface names another
      surface as the alternative.
      Verified: 289/289 tests pass, build clean with zero warnings.
- [x] **P6 — Docs (done).** README (Requirements → 18.0.0, can't-install-on-19, tool list 3→5),
      `CLAUDE.md` (versions/floor/gotchas + `PermissionDomain` + the `EntityType` trap),
      `docs/architecture.md`, `RELEASE.md`, `umbraco-marketplace.json`.
      TWO things nearly missed, both silent:
      (a) **Backoffice localization.** `wwwroot/.../lang/{en,nl}.js` need one label+description pair per
      `[AITool]`; nothing compiles them and nothing fails at run time — the only symptom is a raw key in
      Umbraco AI's "Select Tools" dialog. Added pairs for both new tools AND a new
      `ToolLocalizationTests` that discovers tools by reflection and locales by directory listing, so
      this can never be a silent manual step again.
      (b) **`umbraco-package.json` is a tracked file the build STAMPS.** Committed value must stay the
      `0.0.0` placeholder; a build rewrites it to the MinVer version. Check `git diff` on it before
      committing and revert it — do not commit a stamped version.
      Also corrected a stale CLAUDE.md gotcha: it named `C:\GitHub\UmbracoVersions\v17\...` as the
      backoffice reference source, but no Umbraco clone exists on this machine. Use the
      `Umbraco.Cms.Core` XML docs in the NuGet cache instead.

- [x] **P7 — Close the doc-type Insert Options audit gap (done, follow-up request).**
      Added `AuditDomain.DocumentTypes`, so all four stored configurations can now be audited. Doing it
      properly surfaced THREE things beyond the obvious:
      (a) **A defect in P4.** `scope=all` for the create-filter store discovered content types from the
      `$everyone` entries only, so a type configured just for a named group was invisible — a silent
      under-report in something read as a risk assessment. Replaced with role enumeration
      (`LoadCreateFilterEntriesForEveryRoleAsync`): union `GetByRoleAsync` over every group. Each entry
      belongs to exactly one group, so no de-duplication.
      (b) **False conflicts.** Create-filter entries are keyed on (node, group, content type, verb) while
      the analyzer reasons over (node, group, verb), so two DIFFERENT document types with an Allow and a
      Deny at one node for one group looked like an Allow/Deny conflict. Fixed by grouping on content
      type and analyzing one type at a time (`AnalyzeCreateFilterAsync`), stamping each finding with
      `AuditFinding.ContentTypeKey`, which the presenter renders. A finding that cannot name its type is
      useless to a reviewer, so the field is not optional in spirit.
      (c) **The broad-risk rule pointed the wrong way.** `everyone-broad-write` fires on a blanket All
      Users ALLOW — correct for node permissions (denied unless allowed), but for a create filter an
      Allow grants nothing, so it was a false positive; and the real risk there is the mirror image, a
      blanket DENY hiding a type from everyone, which an Allow-only rule missed entirely. Added
      `everyone-broad-create-deny` for the create-filter domains and suppressed the write rule there.
      The analyzer's parameter changed from `PermissionDomain` to `AuditDomain` — it now needs to know
      the mechanism, not just the tree. `PermissionDomain` still drives the presenter/remediator.
      Subtree scope: valid for `document-types` (per node), still refused for `library-element-types`
      (section-wide). Tool description rewritten — the rule set is domain-dependent now, so "four checks"
      was no longer true.
      Verified: 323/323 tests pass, build clean with zero warnings, pack clean.

## Remaining / handover

- **Nothing is committed** — the maintainer commits. Working tree holds the whole v18 change.
- **MinVer version is still reported as 17.0.0** because HEAD sits exactly on the `v17.0.0` tag and
  MinVer uses an exact tag verbatim. This is expected, not a bug, but it is UNVERIFIED until the first
  v18 commit; after committing, confirm `dotnet pack` stamps `18.0.0-alpha.0.N`.
- Doc-type Insert Options audit is now covered; there is no known audit gap left.
- **Manual TestSite verification not yet run** (needs an LLM provider + key). Two things to check there:
  (1) whether Umbraco AI's copilot attaches to the Library workspace at all in 18.x — if it does not,
  `LibraryGrounding` never fires and the always-on tool pointers carry the routing instead (safe either
  way); (2) that the copilot picks the right tool of the five, especially content vs Library.

## Test-first (maintainer's global rule — failing tests before each slice)

Extend: `ToolSchemaTerminologyTests` (cover `LibraryAspect`, `PermissionDomain`, audit `domain`;
the v17 boundary rule is unchanged — model-visible says "user group"/"item"/"element type",
internals carrying a base-package value keep `RoleAlias`), `ToolDescriptionTerminologyTests`
(both new tools), `AdvancedPermissionsGroundingContributorTests` (library gate, three-part
composition as a strict superset, and the existing definitions-live-in-a-tool guard now covers
`uap_explain_editors`).

New: `ExplainLibraryAccessToolTests`, `ElementPathResolverTests`, `ExplainEditorsToolTests`,
plus library cases in `PermissionPresenterTests` / `PermissionRemediatorTests` /
`AuditPermissionsToolTests`.

`ExplainEditorsToolTests` must assert decision (4) structurally: all 8 surfaces present, each
naming what it is not plus the neighbour to use instead, and all three contrast pairs stated.

Gates: `dotnet build` → `dotnet test` → `dotnet pack` → manual TestSite run against a real
provider (confirm the copilot answers library questions and picks the right tool of the five).
