# Umbraco.Community.AdvancedPermissions.AI — Claude Code Guide

## Project Overview

Optional **AI companion** for `Umbraco.Community.AdvancedPermissions`. Ships native C# Umbraco AI
copilot tools (`[AITool]`s via `Umbraco.AI`) that explain effective permissions, find which user
groups can access a node, and audit permission configuration — read-only, grounded in the base
package's permission-resolution engine. **Umbraco v18-first** (targets `net10.0`). The v17 line is
maintained on the `v17/main` branch; `main` is v18.

## Guiding principle — where "how" comes from

The **v18 `Umbraco.Community.AdvancedPermissions` repo** (`C:\GitHub\UmbracoAdvancedSecurity`,
branch `main`) is the canonical reference for *how* to do things — csproj/MSBuild structure, workflows,
`Directory.Build.props`, `.editorconfig`, packaging conventions — **and** for what the
product actually does, since every string this package shows an editor is sourced from it.

**Read it via git, never the working tree.** `git -C C:/GitHub/UmbracoAdvancedSecurity show main:<path>`
(or a tag such as `v18.1.0`). That clone's working tree has been observed holding a stale `manifests.ts`
that disagreed with `HEAD` — it showed 4 menu items where the released package has 8 — exactly the kind
of thing that gets hard-coded into a grounding string and shipped wrong. Note also that the Bash tool's
cwd resets between calls, so a relative-path grep can silently read the wrong repo.

## Solution structure

    src/
      Umbraco.Community.AdvancedPermissions.AI/          # The package (Razor SDK): Tools, Services, Models, Scopes, wwwroot/App_Plugins
    tests/
      Umbraco.Community.AdvancedPermissions.AI.Tests/    # xUnit + NSubstitute unit tests
      Umbraco.Community.AdvancedPermissions.AI.TestSite/ # Minimal bootable Umbraco 18 site + AI copilot for manual verification
    Memory/Plans/                                        # Design + implementation plans

No Core/Data/Client projects: the package has no persistence and no Vite/Lit frontend — its
"frontend" is hand-written static `App_Plugins` assets (`umbraco-package.json` + `lang/*.js`).

## Dependencies

- **`Umbraco.Community.AdvancedPermissions`** (`[18.1.0,19.0.0)`) — the base package. The floor is
  **18.1.0**, not 18.0.0: the Library APIs this package reads (`IElementNodePermissionService`,
  `ElementVerbs`, `VerbElementCreateOfType`) start there. A real
  runtime dependency: the tools resolve its services from DI. It flows `Abstractions` transitively,
  so the package compiles against the contract without a separate reference. **No direct
  `Umbraco.Cms.*` references** — they arrive transitively (the CMS floor is implicit; see below).
  Upper bounds are **plain stable versions** — nuget.org rejects a `-0` bound at push time (see RELEASE.md).
- **`Umbraco.AI.Core`** (`[18.0.0,19.0.0)`) — the `[AITool]` authoring contract the package
  references; the full `Umbraco.AI` runtime is a host concern (the TestSite runs `Umbraco.AI` 18.3.1).
  Umbraco.AI majors track the CMS major.
- **The effective Umbraco CMS floor is `18.0.0`**, and it is *computed*, never declared here. Read it off
  the dependencies' own nuspecs: `Umbraco.AI.Core` 18.3.1 requires `Umbraco.Cms.* [18.0.0, 18.999.999)`
  and the base package requires `[18.0.0, 19.0.0)`, so they agree at 18.0.0. Do not restate this number
  from memory (on the v17 line it was documented as 17.4.2 for a while and that was wrong) — re-derive it
  from the nuspecs whenever a dependency version moves, and update the README's Requirements section to
  match. Note `Umbraco.AI.Core`'s `18.999.999` ceiling: the package physically cannot install on
  Umbraco 19, whatever the base-package bound says.

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

- **Two trees, one spine.** v18 added the **Library** (elements and element folders) as a second
  permission tree alongside content. Same machinery — Allow/Deny, scopes, inheritance, All Users,
  Priority Override — but separate tables, separate Umbraco object types, separate verbs, so an answer
  from the wrong tree is about the wrong thing. `PermissionDomain` (`Content` | `Library`) is the single
  concept threaded through the shared services, and how it is threaded differs on purpose: the
  nine-method `IPermissionPresenter` is **bound** once via `For(PermissionDomain)` (so no signature or
  call site carries it), while the single-method remediator and analyzer take a plain `domain` argument.
  All default to `Content`, so v17 call sites keep working. `PermissionDomain` is **internal plumbing and
  never a tool argument** — the model picks a tool by name, which is a clearer choice than a flag; a test
  enforces that it stays out of the model-visible schema.
- **Explain got a sibling tool; audit got an argument.** Deliberate asymmetry, and worth preserving.
  `uap_explain_library_access` is separate from `uap_explain_access` because they differ in *arguments
  and reasoning* — library element-type creation takes **no node at all**. `uap_audit_permissions` just
  takes a `domain` (four of them: `content`, `library`, `document-types`, `library-element-types`),
  because those paths differ mainly in *which store to read* and all yield the same entry shape.
- **The audit's broad-risk rule points in OPPOSITE directions per domain.** Node permissions are denied
  unless allowed, so the risk is a blanket All Users **Allow** of a write permission. The create filters
  are allowed unless denied and can only narrow, so a blanket Allow there grants nothing (false positive)
  and the real risk is a blanket All Users **Deny**, which hides a type from everyone (false negative for
  the Allow-only rule). `everyone-broad-write` vs `everyone-broad-create-deny`. Do not unify them.
- **Create-filter entries must be analyzed ONE CONTENT TYPE AT A TIME.** They are keyed on (node, group,
  content type, verb) while the analyzer reasons over (node, group, verb), so two *different* document
  types with an Allow and a Deny at the same node for the same group share every key it looks at without
  being in conflict. `AnalyzeCreateFilterAsync` groups by content type, analyzes each group, and stamps
  each finding with `AuditFinding.ContentTypeKey` — which the presenter renders, because a finding that
  cannot name its type is useless to a reviewer.
- **Never infer "all entries" from the All Users entries.** The create-filter repository has no
  "everything" read. Discovering content types from `$everyone` entries misses any type configured only
  for a named group — a silent under-report in something read as a risk assessment. Enumerate the user
  groups and union `GetByRoleAsync` per group instead (each entry belongs to exactly one group, so no
  de-duplication is needed). This was a real defect, caught only when the document-type domain was added.
- **`ContextKeys.EntityType`, never `ContextKeys.ElementType`.** `Umbraco.AI.Core` has both, and
  `ElementType` means a **block-editor** element type — nothing to do with the Library. The grounding's
  Library gate reads `EntityType` and matches `element` / `element-folder` (values taken from the base
  package's own condition classes). Gating on the wrong key would silently never fire; a test pins it.
- **Applicability is checked before resolving, not after.** The Library shows a hatched **N/A** cell for
  combinations with no meaning — `Create` on a single item, and `Publish`/`Unpublish`/`Duplicate`/
  `Rollback` on a folder. The resolver returns an ordinary Allow/Deny for those anyway, and relaying it
  would have the copilot assert "Publish is denied on this folder" and send someone hunting for an entry
  that is not there. `LibraryVerbApplicability` owns the rule (sourced from `library-permissions.md`);
  `LibraryNodeKind.Unknown` makes **no** applicability claim, which is the safe failure.
- **Element-type entries share a table with document-type entries**, distinguished *only* by the verb
  (`Umb.Element.CreateOfType` vs `Umb.Document.CreateOfType`). Every read of that store must filter by
  verb, or a document-type finding gets reported as a Library one. Both create-filter call sites pass the
  verb explicitly rather than relying on the default — which also removes the positional-binding trap
  that `ResolveCreateForRolesAsync` sprang when it gained a defaulted `verb` parameter *before*
  `cancellationToken`. When adding a parameter to a service of our own, put it **before** the token so
  existing positional calls break loudly instead of silently re-binding.
- **`uap_explain_editors` is the fifth tool, and its job is the boundaries.** Five tools now:
  `uap_explain_access`, `uap_explain_library_access`, `uap_audit_permissions`, `uap_explain_concepts`,
  `uap_explain_editors`. The last one mirrors the base package's nine per-surface help docs the way
  `uap_explain_concepts` mirrors `concepts.md` — a split that follows the base package's own, since
  `concepts.md` is byte-identical between the v17 and v18 lines while the per-surface docs went 5 → 9.
  Its content is ordered **boundaries-first** (how to choose, then the three contrast pairs, then the
  eight surfaces) because the failure to prevent is not "cannot describe a screen" but "confidently
  describes the wrong one of eight similarly-named screens". `EditorSurface.NotThis` is load-bearing:
  a test asserts every surface names *another* surface as the alternative. Keep it that way — do not let
  it decay into eight blurbs.
- **The model-visible type list is guarded.** `ToolSchemaTerminologyTests.ModelVisibleTypes_ListIsComplete`
  reflects over the models namespace and fails if a public type is neither terminology-checked nor
  explicitly excluded with a reason. Adding a returned type therefore forces a decision. This was added
  after the list had quietly fallen behind.

- **Namespace collision with `Umbraco.Cms`**: the project namespace is
  `Umbraco.Community.AdvancedPermissions.AI`, so the compiler sees `Umbraco` as a parent namespace.
  Keep all `Umbraco.Cms.*` access in `using` directives (or use `global::Umbraco.Cms...`), never
  inline fully-qualified inside a class declared in this namespace.
- **Versioning**: MinVer, `v`-prefixed tags (`v18.x.x` on `main`, `v17.x.x` on `v17/main`). Cutting a release = pushing a tag;
  `publish.yml` does the rest. Ask before tagging/pushing (maintainer preference; no auto-commit).
- **Reference source**: there is currently **no** local Umbraco backoffice clone on this machine — the
  `C:\GitHub\UmbracoVersions\...` path this file used to name does not exist. For backoffice internals,
  read the `Umbraco.Cms.Core` XML docs in the NuGet cache
  (`~/.nuget/packages/umbraco.cms.core/<ver>/lib/net10.0/Umbraco.Core.xml`) — that is how the
  `IEntityService` overloads used by `ElementTreeResolver` were confirmed. Never read `node_modules`.
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
