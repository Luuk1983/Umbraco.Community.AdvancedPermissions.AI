# Design — Reference tools become a knowledge set (issue #9)

Date: 2026-11-27 · Line: v18 (`main`) · Size: small change

## Intent

`uap_explain_concepts` and `uap_explain_editors` return fixed reference text: the same answer on every
site, node and user. They are knowledge, not tools. As tools they only help after an admin has ticked
the "Advanced Permissions (read)" scope on the chat agent, and only when the model decides to call them.
Umbraco AI knowledge sets are the platform's mechanism for exactly this content.

**Rule this establishes:** if the answer depends on live data it is a tool; if it does not, it is
knowledge. `uap_explain_access`, `uap_explain_library_access` and `uap_audit_permissions` stay tools.

## How knowledge sets behave (researched, see side-session notes)

- In `Umbraco.AI.Core` from **18.2.0** (17.2.0 on v17). Same API on both lines.
- A package ships one by dropping a class deriving `AIKnowledgeSetBase` with `[AIKnowledgeSet(id, name)]`.
  It is auto-discovered and **always active**. No composer code, no admin toggle, no tool scope.
- Each item is listed in the system prompt of **every** Umbraco AI chat request under "Available
  On-Demand Context Resources" (name, one-line description, id). The model fetches the full text with
  the built-in `get_context_resource` tool, which every agent always has, whatever its scopes.
- The model still decides when to fetch, so the grounding pointer and the item descriptions stay
  load-bearing.

## Goals

- Both reference texts reach the copilot without any agent setup.
- The always-on grounding keeps pointing at them, so the model still knows it must look them up.
- All existing content guarantees survive: base-package sourcing, terminology, "every surface names
  another surface", real sidebar navigation.

## Non-goals

- No change to the content itself beyond replacing tool-name cross-references.
- No change to the three live-data tools beyond their descriptions' cross-references.
- No v17 backport now (notes below).

## Anti-goals

- Definitions creeping back into the always-on prompt.
- Splitting the content so finely that the model fetches half the rules and answers wrongly.

## Approaches considered

- **A. Two items, mirroring today's two tools (picked).** "Permission concepts" and "Permission editors
  and viewers". Adds two lines to every request. One fetch returns the whole topic, which suits concept
  questions that touch several sections at once. Same split the base package's help docs use.
- B. One item per section (~13). Sharper descriptions, smaller fetches, but ~13 lines on every AI
  request site-wide, and the model must guess which pieces a question needs.
- C. A handful of grouped items. Between the two on cost, still has B's wrong-piece risk.

## Design

1. **`AdvancedPermissionsKnowledgeSet`** (new folder `Knowledge/`): `[AIKnowledgeSet("advanced-permissions",
   "Advanced Permissions", Description = …, Icon = …)]`, derives `AIKnowledgeSetBase`, returns two items
   via `AIKnowledgeSetItem.FromContent`:
   - key `concepts`, name "Permission concepts". Description lists the topics (precedence, inheritance,
     unset permissions, scopes, Priority Override, Insert Options, start nodes, how to change a
     permission) and says to read it before answering, not from general knowledge.
   - key `editors`, name "Permission editors and viewers". Description: which of the eight screens
     answers a question, how they differ, where to find them.
   Keys are stable and URL-safe: Umbraco derives the resource id from them.
2. **Content source stays structured.** `PermissionConcepts`, `EditorGuide` and `EditorSurface` remain the
   source records, now `internal`, and are rendered to markdown (one heading per section, one sub-heading
   per surface). The existing content tests keep asserting on the records; new tests assert that the
   rendered markdown carries every section.
3. **Cross-references** inside the content ("call uap_explain_editors", "use uap_explain_concepts") and in
   the three remaining tools' descriptions now name the knowledge item instead.
4. **Remove** `ExplainConceptsTool`, `ExplainEditorsTool`, `ExplainConceptsArgs`, `ExplainEditorsArgs`,
   their localization keys in `lang/en.js` and `lang/nl.js`, and their test entries.
5. **Grounding** (`ConceptsGrounding`): the two "call uap_explain_…" pointers become "read the 'Permission
   concepts' / 'Permission editors and viewers' resource (get_context_resource) and answer only from it".
   The "definitions stay out of the prompt" tests are kept and re-pointed deliberately, not deleted.
6. **Terminology guard** extends to model-visible knowledge text: set description, both item
   descriptions and both rendered bodies are checked against `BannedPhrases`. The two records leave the
   model-visible type list, because they are no longer serialized to the model and are no longer public.
7. **Floor bump:** `Umbraco.AI.Startup` `[18.0.0,19.0.0)` → `[18.2.0,19.0.0)`. The effective CMS floor
   stays 18.0.0 (Startup 18.2.0 still requires `Umbraco.Cms.* [18.0.0, 18.999.999)`).
8. **Docs:** `CLAUDE.md` (rule becomes "Definitions live in the knowledge set; only style lives in the
   prompt"; tool count five → three; dependency floor), `README.md` (three tools plus a knowledge set
   that needs no scope; Requirements), `docs/architecture.md`, `RELEASE.md`.

## Behaviour changes worth knowing

- The reference text now reaches **every** Umbraco AI agent and prompt on the site, including ones
  without our scope. Per-user-group tool governance no longer gates it. It is public product
  documentation, so that is acceptable.
- Two short lines are added to every AI chat request on the site.
- Two tools disappear and a dependency floor rises. Whether that is a minor or a major release is the
  maintainer's call at release time.

## Left to the builder

- Exact item descriptions and set description wording (subject to the terminology tests).
- Icon alias.
- Markdown layout of the rendered bodies.

## v17 backport (later, on `v17/main`)

- Same code. Floor `Umbraco.AI.Startup` `[17.0.0,18.0.0)` → `[17.2.0,18.0.0)`. The CMS floor is
  unaffected: Startup 17.0.0 and 17.2.0 both require `Umbraco.Cms.* [17.4.0, 17.999.999)`.
- v17 already has both tools, with no Library and fewer surfaces, so the editors item renders that
  line's smaller `EditorGuide` and the concepts item has no Library start-node bullets.
- Same grounding, terminology-test and docs changes.
