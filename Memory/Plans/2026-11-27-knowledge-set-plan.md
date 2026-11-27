# Reference Tools → Knowledge Set Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the `uap_explain_concepts` and `uap_explain_editors` tools with one Umbraco AI knowledge set holding two items, and re-point the always-on grounding at it.

**Architecture:** The static content moves out of the two tools into two internal reference classes that hold the existing records and render them to markdown. A discovered `AIKnowledgeSetBase` subclass exposes them as two on-demand items. The grounding names the items instead of the tools; the tools, their args and their localization keys are deleted.

**Tech Stack:** .NET 10, `Umbraco.AI.Core` ≥ 18.2.0 (`Umbraco.AI.Core.Contexts.KnowledgeSets`), xUnit, NSubstitute.

**Spec:** `Memory/Plans/2026-11-27-knowledge-set-design.md`

## Global Constraints

- **Never commit, push or open a PR.** The maintainer's rule overrides this skill's commit steps: every task ends with the work left uncommitted.
- **Test-first:** write or re-point the test, watch it fail, then change the code.
- `Umbraco.AI.Startup` floor: `[18.2.0,19.0.0)`. Effective CMS floor stays `18.0.0`.
- Knowledge set id `advanced-permissions`, name `Advanced Permissions`. Item keys `concepts` and `editors` (they seed Umbraco's deterministic resource GUIDs, so they never change). Item names `Permission concepts` and `Permission editors and viewers`.
- C# conventions: primary constructors, `var`, `record` over `class` (the knowledge set must be a `class`: it derives `AIKnowledgeSetBase`), XML docs on every public/protected member.
- Terminology rules from `CLAUDE.md` apply to every model-visible string: "entry", "permission", "user group", never "rule"/"role"/bare Allow/Deny nouns.
- Content is sourced from the base package (`git -C C:/GitHub/UmbracoAdvancedSecurity show origin/v18/main:…`). This plan changes no content except cross-references to the removed tools.
- Keep `Umbraco.Cms.*` access in `using` directives (namespace collision).

## Review Focus

1. **Runtime discovery fails silently.** Umbraco finds the set via `TypeLoader.GetTypesWithAttribute<IAIKnowledgeSet, AIKnowledgeSetAttribute>`. A non-public, abstract or attribute-less class, or one with no parameterless constructor, is simply never listed. Pinned in Task 1 (`KnowledgeSet_IsDiscoverable`).
2. **Item key drift.** Renaming a key changes the resource GUID. Pinned in Task 1 (`Items_HaveTheStableKeysAndNames`).
3. **A record field that is never rendered.** Adding a field to `PermissionConcepts`/`EditorGuide`/`EditorSurface` and forgetting the markdown silently drops content. Pinned in Task 1 by a reflection test over every string property.
4. **A removed tool name left in model-visible text** (grounding, the remaining tools' descriptions, the knowledge content). The model would call a tool that no longer exists. Pinned in Task 3 (`ModelVisibleText_NeverNamesARemovedTool`).
5. **Orphaned localization keys** for the removed tools. Covered by `EveryLocale_DeclaresTheSameToolKeys` only if both files are edited; Task 3 removes them from both and greps to confirm.

---

### Task 1: The knowledge set and its two items

**Files:**
- Modify: `Directory.Packages.props` (`Umbraco.AI.Startup` → `[18.2.0,19.0.0)`, and its comment's "18.0.0 targets" sentence)
- Create: `src/Umbraco.Community.AdvancedPermissions.AI/Knowledge/ConceptsReference.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions.AI/Knowledge/EditorsReference.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions.AI/Knowledge/AdvancedPermissionsKnowledgeSet.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions.AI/Models/PermissionConcepts.cs`, `Models/EditorGuide.cs` (`public` → `internal`; doc comments say "knowledge item", not "tool")
- Modify: `src/.../Tools/ExplainConceptsTool.cs`, `Tools/ExplainEditorsTool.cs` (temporarily return `ConceptsReference.Content` / `EditorsReference.Content`; deleted in Task 3)
- Modify: `tests/.../ToolSchemaTerminologyTests.cs` (drop `PermissionConcepts`, `EditorGuide`, `EditorSurface` from `ModelVisibleTypes`: they are now internal, so the completeness test no longer sees them)
- Rename + modify: `tests/.../ExplainConceptsToolTests.cs` → `ConceptsReferenceTests.cs`; `ExplainEditorsToolTests.cs` → `EditorsReferenceTests.cs`
- Create: `tests/.../AdvancedPermissionsKnowledgeSetTests.cs`

**Interfaces:**
- Produces (namespace `Umbraco.Community.AdvancedPermissions.AI.Knowledge`):
  - `internal static class ConceptsReference { PermissionConcepts Content { get; } string ToMarkdown(); }`
  - `internal static class EditorsReference { EditorGuide Content { get; } string ToMarkdown(); }`
  - `[AIKnowledgeSet(AdvancedPermissionsKnowledgeSet.SetId, "Advanced Permissions", Description = …, Icon = "icon-lock")] public sealed class AdvancedPermissionsKnowledgeSet : AIKnowledgeSetBase` with `public const string SetId = "advanced-permissions"`, `internal const string ConceptsKey = "concepts"`, `EditorsKey = "editors"`, `ConceptsItemName = "Permission concepts"`, `EditorsItemName = "Permission editors and viewers"`, and `GetItemsAsync(CancellationToken)` returning the two items in that order via `AIKnowledgeSetItem.FromContent(key, name, markdown, description)`.

- [ ] **Step 1: Bump the floor and confirm the API resolves**

Edit `Directory.Packages.props`. Run: `dotnet restore` then `dotnet build`. Expected: build succeeds (nothing uses the new API yet).

- [ ] **Step 2: Re-point the existing content tests at the reference classes**

In `ConceptsReferenceTests` the helper becomes `private static PermissionConcepts Concepts => ConceptsReference.Content;` (sync; drop `async`/`await`). Delete `Description_TellsModelNotToAnswerFromItsOwnKnowledge` and `Description_MentionsStartNodes`; their replacements are in Step 3. Same for `EditorsReferenceTests` (`EditorsReference.Content`; delete `Description_RoutesWhereQuestionsHereAndHandsOffTheRest`). Add to each:

```csharp
[Fact]
public void Content_NamesNoRemovedTool()
{
    var md = ConceptsReference.ToMarkdown(); // EditorsReference in the other file
    Assert.DoesNotContain("uap_explain_concepts", md, StringComparison.Ordinal);
    Assert.DoesNotContain("uap_explain_editors", md, StringComparison.Ordinal);
}
```

Concepts also asserts `Assert.Contains("Permission editors and viewers", ConceptsReference.Content.HowToChange)`; editors asserts the guide text contains `"Permission concepts"`.

- [ ] **Step 3: Write `AdvancedPermissionsKnowledgeSetTests`**

```csharp
[Fact] KnowledgeSet_IsDiscoverable()
// typeof(AdvancedPermissionsKnowledgeSet): IsPublic, !IsAbstract, has [AIKnowledgeSet],
// has a public parameterless ctor, implements IAIKnowledgeSet; new() does not throw;
// Id == "advanced-permissions", Name == "Advanced Permissions", Description non-empty.

[Fact] async Items_HaveTheStableKeysAndNames()
// keys == ["concepts", "editors"] in order; names == ["Permission concepts",
// "Permission editors and viewers"]; every key matches ^[a-z0-9-]+$.

[Fact] async ConceptsItem_DescriptionSaysToReadItInsteadOfGeneralKnowledge()
// Description contains "Priority Override", "start nodes", "scopes", "general knowledge".

[Fact] async EditorsItem_DescriptionRoutesWhereAndWhichScreenQuestions()
// Description contains "which screen", "Users section".

[Fact] async ItemContent_IsTheRenderedReference()
// await item.GetContentAsync(CancellationToken.None) == ConceptsReference.ToMarkdown() / EditorsReference.ToMarkdown().

[Fact] ConceptsMarkdown_RendersEveryField()
// reflection: every string property value of ConceptsReference.Content appears verbatim in ToMarkdown().

[Fact] EditorsMarkdown_RendersEveryFieldOfEverySurface()
// same over EditorGuide's string properties and every EditorSurface's string properties.

[Fact] Markdown_HasAHeadingPerSection()
// Concepts markdown starts with "# Permission concepts" and has 8 "## " headings;
// editors markdown starts with "# Permission editors and viewers" and has a "### " heading per surface.
```

Run: `dotnet test --filter "FullyQualifiedName~KnowledgeSet|FullyQualifiedName~Reference"`. Expected: compile failure (types missing).

- [ ] **Step 4: Implement the two reference classes**

Move the `Concepts` / `Guide` static data verbatim out of the tools into `ConceptsReference.Content` / `EditorsReference.Content`. Replace the two cross-references: in `HowToChange`, "call uap_explain_editors." → "read the 'Permission editors and viewers' reference."; in the editors content, any "use uap_explain_concepts" → "read the 'Permission concepts' reference". `ToMarkdown()` renders `# {item name}`, then one `## {heading}` plus the field's text per section. Concepts headings, in record order: "How access is stored", "What an entry is made of", "Precedence", "Scopes", "Priority Override", "Insert Options", "Start nodes", "How to change a permission". Editors: "How to choose", "Editor versus viewer", "Permissions versus create filters", "Per-node versus section-wide", "The eight screens" (one `### {surface.Name}` each, then its Purpose, UseWhen, NotThis and Navigation as short paragraphs led by "Use it when", "Not this", "Where"), then "Navigation". Make the records `internal`. Point the two tools at the new classes.

- [ ] **Step 5: Implement `AdvancedPermissionsKnowledgeSet`**

Set description (exact): "Reference for the Advanced Permissions package on this site: how its permission rules work and which screen to use. For the effective permissions on an actual node, use uap_explain_access or uap_explain_library_access instead."

Concepts item description (exact): "How this site's permission rules work: Allow and Deny entries, precedence, inheritance and unset permissions, scopes, Priority Override, Insert Options, start nodes, and how to change a permission. Read it before answering any question about how permissions work: the rules differ from Umbraco's built-in permissions, so an answer from general knowledge sounds right and is wrong."

Editors item description (exact): "Which of the eight permission editors and viewers in the Users section answers a question, how they differ, what each one does and does not do, and where to find it. Read it before telling anyone where to go or which screen to use."

- [ ] **Step 6: Run the tests**

Run: `dotnet test`. Expected: all pass (the two tools still exist and still pass their schema checks).

- [ ] **Step 7: No commit** (maintainer rule).

---

### Task 2: Grounding points at the knowledge items

**Files:**
- Modify: `src/.../Context/AdvancedPermissionsGroundingContributor.cs` (`ConceptsGrounding` and its XML docs)
- Modify: `tests/.../AdvancedPermissionsGroundingContributorTests.cs`, `tests/.../GroundingContributorLibraryTests.cs`

**Interfaces:**
- Consumes: `AdvancedPermissionsKnowledgeSet.ConceptsItemName`, `EditorsItemName` (Task 1). `ConceptsGrounding` stays a `const` built by concatenating these constants.

- [ ] **Step 1: Re-point the pointer tests**

In `Contribute_AnyContext_PointsAtConceptsToolInsteadOfDefining` (rename `…PointsAtConceptsKnowledgeInsteadOfDefining`): replace `Contains("uap_explain_concepts")` with `Contains("'Permission concepts'")`, `Contains("get_context_resource")`, and add `DoesNotContain("uap_explain_concepts")`; replace `Contains("uap_explain_editors")` with `Contains("'Permission editors and viewers'")` and `DoesNotContain("uap_explain_editors")`. Keep every existing `DoesNotContain` on definitions: that rule stays. Same swap in `Contribute_NonDocumentContext_AppendsConceptsOnly`, `Contribute_NoEntityType_AppendsConceptsOnly`, and `GroundingContributorLibraryTests.Contribute_NoEntityType_StillPointsAtBothReferenceTools` (rename `…BothReferenceItems`). Update the XML summaries that say "tool" to "knowledge item".

Run: `dotnet test --filter "FullyQualifiedName~Grounding"`. Expected: those tests FAIL on the new `Contains`.

- [ ] **Step 2: Rewrite the three pointer sentences in `ConceptsGrounding`**

- "…call uap_explain_concepts and answer only from what it returns…" → "…read the '" + ConceptsItemName + "' reference (Advanced Permissions knowledge, fetched with get_context_resource) and answer only from it…"
- "…call uap_explain_editors rather than guessing…" → "…read the '" + EditorsItemName + "' reference rather than guessing…"
- "…call uap_explain_concepts for the exact backoffice navigation…" → "…read the '" + ConceptsItemName + "' reference for the exact backoffice navigation…"

Update the field's and class's XML remarks: definitions live in the knowledge set, the pointer is still load-bearing because the model still decides when to fetch.

- [ ] **Step 3: Run the tests**

Run: `dotnet test`. Expected: all pass.

- [ ] **Step 4: No commit.**

---

### Task 3: Remove the two tools

**Files:**
- Delete: `src/.../Tools/ExplainConceptsTool.cs`, `src/.../Tools/ExplainEditorsTool.cs`
- Modify: `src/.../Models/AiToolArgs.cs` (delete `ExplainConceptsArgs`, `ExplainEditorsArgs`)
- Modify: `src/.../wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/lang/en.js`, `lang/nl.js` (delete both label/description pairs and the two mapping-comment lines)
- Modify: `src/.../Umbraco.Community.AdvancedPermissions.AI.csproj` `<Description>` ("…and explain how the permission model works" → "…and ships a knowledge set that explains how the permission model works")
- Modify: `tests/.../ToolDescriptionTerminologyTests.cs`, `tests/.../ToolSchemaTerminologyTests.cs`

**Interfaces:**
- Consumes: `AdvancedPermissionsKnowledgeSet` (Task 1).

- [ ] **Step 1: Write the guard tests**

In `ToolDescriptionTerminologyTests`: remove the two tools from `AllDescriptions()`. Add:

```csharp
public static TheoryData<string, string> AllKnowledgeText();
// ("set description", set.Description), ("concepts description", …), ("editors description", …),
// ("concepts content", await GetContentAsync), ("editors content", …)

[Theory, MemberData(nameof(AllKnowledgeText))]
public void KnowledgeText_UsesNoBannedTerminology(string source, string text);
// same BannedPhrases check as Description_UsesNoBannedTerminology

[Fact]
public void ModelVisibleText_NeverNamesARemovedTool();
// over every AllDescriptions() text, every AllKnowledgeText() text, and the grounding for entity types
// "document", "element", null: DoesNotContain "uap_explain_concepts" and "uap_explain_editors".
```

In `ToolSchemaTerminologyTests`: remove `ExplainConceptsArgs` and `ExplainEditorsArgs` from `ModelVisibleTypes()`.

Run: `dotnet test --filter "FullyQualifiedName~Terminology"`. Expected: it builds and the new guards pass, because Tasks 1–2 already removed the names. Prove the guard bites: temporarily add "uap_explain_concepts" to one item description, see `ModelVisibleText_NeverNamesARemovedTool` fail, then revert.

- [ ] **Step 2: Delete the tools, args and localization keys; update the csproj description**

- [ ] **Step 3: Verify nothing still names them**

Run: `git grep -n "uap_explain_concepts\|uap_explain_editors\|ExplainConceptsTool\|ExplainEditorsTool\|ExplainConceptsArgs\|ExplainEditorsArgs\|uapExplainConcepts\|uapExplainEditors" -- src tests`. Expected: only the guard test's string literals.

Run: `dotnet test`. Expected: all pass, including `ToolLocalizationTests` and `ModelVisibleTypes_ListIsComplete`.

- [ ] **Step 4: No commit.**

---

### Task 4: Documentation and final verification

**Files:**
- Modify: `CLAUDE.md`, `README.md`, `docs/architecture.md`, `RELEASE.md`

- [ ] **Step 1: `CLAUDE.md`**

- Dependencies: `Umbraco.AI.Startup` `[18.2.0,19.0.0)`; say 18.2.0 is the first with knowledge sets; the effective CMS floor re-derived: Startup 18.2.0 requires `Umbraco.Cms.* [18.0.0, 18.999.999)`, so still 18.0.0.
- Replace the "`uap_explain_editors` is the fifth tool" gotcha: three tools plus the knowledge set; keep its boundaries-first and `EditorSurface.NotThis` rules (now in `EditorsReference`).
- Replace "Definitions live in a tool; only style lives in the prompt" with "Definitions live in the knowledge set; only style lives in the prompt", including the new rule: if the answer depends on live data it is a tool, otherwise knowledge; item keys never change; the pointer stays load-bearing because the model fetches on demand; the item list reaches every AI request on the site.

- [ ] **Step 2: `README.md`**

Requirements: Umbraco AI 18.2.0+. Setup step 5: the scope tick covers the three tools; the reference knowledge needs no setup. "The tools" section: three tools; replace the `uap_explain_concepts` / `uap_explain_editors` subsections with one "Built-in knowledge" subsection describing the two items, keeping their existing "why" prose.

- [ ] **Step 3: `docs/architecture.md` and `RELEASE.md`**

Architecture: the diagram node says 3 AITool classes plus a knowledge set node; lines 109–124 describe pointers to the knowledge items. RELEASE: floor `[18.2.0,19.0.0)`, the tool list at line 86 becomes three tools plus the knowledge set.

- [ ] **Step 4: Final verification**

Run: `dotnet build` (expected: 0 warnings introduced, 0 errors), `dotnet test` (expected: all pass; report the count), `dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/ -o <scratchpad>` and check the nuspec lists `Umbraco.AI.Startup` `[18.2.0, 19.0.0)`.

Manual (maintainer, needs an LLM key): run the TestSite, ask the copilot "what is a Priority Override?" without the scope ticked, and confirm it fetches "Permission concepts".

- [ ] **Step 5: No commit.** Report changed files.
