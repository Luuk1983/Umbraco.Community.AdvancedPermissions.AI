# AI companion catch-up: gap analysis (2026-11-05)

Compares the AI companion against the base package as it is now.

- **v17:** AI `origin/v17/main` (feature content from b5dbdc0, 2026-08-10) against base `origin/v17/main` (97be43d, v17.5.0).
- **v18:** AI `origin/main` (06c5f8a, built against base v18.1.0) against base `origin/v18/main` (749850d, v18.3.0).

Base `main` is now v19 and is out of scope.

## What changed in the base since the AI was built

| Base change | v17 | v18 | AI impact |
|---|---|---|---|
| #66 Start nodes shown in the viewers | 17.5.0 | 18.3.0 | **Major.** Wrong "allowed" answers and fixes (A1) |
| #41 Endpoints gated on Users-section access | 17.3.0 | 18.0.1 | **Security.** AI tools bypass the gate (A2) |
| #64 Content tree filtered by Advanced Permissions Read | 17.4.1 | n/a | New symptom to explain (B3) |
| #58 / #61 Read-only after first save; Save and publish | 17.4.0 | 18.2.0 | Dependency floor (A4) |
| #56 / #57 Real-time updates, conflict dialog | 17.4.0 | 18.2.0 | Nice-to-have wording only |
| #48, #50, #40 | | | None (compile-compatible) |

## A. Must fix: the copilot gives wrong answers today (both lines unless marked)

1. **Start nodes are ignored.** Base `concepts.md` says Umbraco checks start nodes *before* any permission. The AI calls the resolver directly, so:
   - `uap_explain_access` (and `uap_explain_library_access` on v18) can say "allowed" for a node the user cannot reach. The same goes for the all-user-groups roster.
   - `PermissionRemediator` can suggest an Allow entry that changes nothing.
   - The grounding actively steers away from structural causes. See the "a Deny entry is a likely cause…" text in `AdvancedPermissionsGroundingContributor.cs`, and `ExplainAccessTool.cs` around line 61 ("don't attribute a block to a structural reason…").
   - The type-create aspects have the same gap ("Creating under a node needs access to that node"). On v18 this also covers Library element-type creation: no Library start node means blocked. This is the upgrade trap, where after an upgrade only Administrators can reach the Library.
   - Fix: add a start-node verdict (`Accessible` / `Browse only` / `Not visible`, plus who set it) to `AccessExplanation`/`AccessVerdict`. Check it before resolving. Treat start nodes as a second structural cause in the grounding.
     - All Users gets no start-node claim.
     - User-group mode carries the caveat that a user gets the combined start nodes of all their groups.
   - Use the base's public `IStartNodeAccessService` (`GetForUserAsync`, `GetForUserGroupAsync`, `Classify`). It lives in the main package, not Abstractions, and needs base floor **17.5.0 / 18.3.0**.
2. **No Users-section gate (security).** The base deliberately gates the Access Viewer, `/effective/by-role`, the tree and the audit behind Users-section access, so that a user cannot look up another user's permissions. The AI tools check nothing (`AdvancedPermissionsReadScope` is only a tag), so any copilot user can run `subject: user`, `all-user-groups` and `uap_audit_permissions`. Fix: require Users-section access for everything except `current-user`, and say so in the tool descriptions.
3. **(v17 only) Wrong navigation.** `ExplainConceptsTool.cs:71` sends editors to "'Content Permissions' in the sidebar". v17 has no such group. The real layout is Users section, then **Editors** (Content Permissions Editor, Document Type Permissions Editor) and **Viewers** (Access Viewer, Insert Options Viewer). This was wrong at release. The wrong text is also pinned by `ExplainConceptsToolTests.cs:112` and by the v17 CLAUDE.md.
4. **Dependency floor admits buggy base versions.**
   - v17 pins base `[17.2.0,18)`. NuGet resolves the lowest version, so a fresh install gets 17.2.0, which still has #41, #58, #61 and #64. Those versions show "read-only" and "Save and publish disabled" symptoms that contradict what the copilot reports.
   - v18 pins `[18.1.0,19)`, which still has the #61 bug, and the AI test site runs it.
   - A1 needs 17.5.0 / 18.3.0 anyway. Both keep the computed CMS floor unchanged (17.4.0 / 18.0.0).
5. **"All content (root-level default)"** (`PermissionPresenter.cs`). The UI calls this row **"Default permissions"** (en.ts `contentRoot`, concepts.md), so editors cannot find "All content" on screen.

## B. Gaps: base features the AI does not know about

1. **Start-nodes concept** in `uap_explain_concepts` (`PermissionConcepts` + tool). Mirror the concepts.md "Start nodes" section:
   - per group / root / none
   - groups combine
   - start nodes are separate from permissions
   - a user's own start node replaces the group start nodes on the same branch
   - no special exception for Administrators
   - deleted start nodes are ignored
   - pickers and search can differ
   - v18 only: the Library bullets and the Umbraco 18 upgrade note

   Add "start nodes" to the tool description, the grounding trigger list and `lang/*.js`.
2. **Start nodes in `uap_explain_editors`** (v18). Add the start-node card and the blocked cells ("Browse only" / "Not visible") to the four viewer surfaces. A blocked cell is neither Allow nor Deny. On the Library viewer, N/A takes precedence over blocked.
3. **(v17) Read controls tree visibility** (#64). Add "page missing from the tree / empty tree" to the symptom list. The cause is Read denied or outside start nodes. Optional audit rule: an All Users Deny on Read at a broad level, which hides content from everyone.
4. **(v17) Insert Options nuance.** An Allow never adds a type Umbraco does not already permit; it only keeps it. A Deny takes it away.
5. **Optional audit rule (v18):** "user group has Library permission entries but no Library start node" (the upgrade trap).

## C. Backport v18 AI improvements to v17 (general, not Library)

1. **`document-types` audit domain.** v17 audits content entries only, although base v17 has had Insert Options since 17.1.0. Port `AuditDomain` (Content, DocumentTypes), `AnalyzeCreateFilterAsync` (one content type at a time), the `everyone-broad-create-deny` rule, and per-group `GetByRoleAsync` enumeration. Never infer the entries from the All Users entries. About 250 lines plus tests.
2. **`uap_explain_editors` for v17's four surfaces.** Use the boundaries-first structure, the editor-vs-viewer and permissions-vs-create-filter contrasts, and the `NotThis` invariant test. It also fixes A3 at the root.
3. **Test guards:** `ToolSchemaTerminologyTests.ModelVisibleTypes_ListIsComplete`, `ToolLocalizationTests`.
4. **CI:** the shared `build-package` composite action (checks shipped assets and dependency bounds).
5. **v17 CLAUDE.md:**
   - add "read the base via git, never the working tree"
   - add "new parameters before the CancellationToken"
   - fix the reference-source path (`D:\github\UmbracoVersions\v17\Umbraco-CMS\…`)
   - fix the "Content Permissions" claim

Not to backport: everything Library / `PermissionDomain` / element types (v18-only).

## D. Housekeeping on the v18 AI CLAUDE.md

- It says the v18 base branch is `main`. That is now v19; the v18 base branch is `v18/main`.
- It says `concepts.md` is "byte-identical between v17 and v18". That is no longer true: v18 has extra Library start-node bullets.

## Nice-to-have (both)

- One line in the editors/concepts text: editors update live and warn before overwriting someone else's change (#56/#57).

## Suggested order

1. A2 (security) and A1 (start nodes) on **both** lines, with the floor bump (A4). This is the same design twice; build it on v18 first, then port it.
2. B1, B2, A5, and the v17-only fixes A3 and B3.
3. The C backports to v17, then D.
