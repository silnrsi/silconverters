# Shared Fix Spelling dialog + custom CC rules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep custom CC rules when CSC corrections are saved, add a Similar words list to SpellingFixer30's `QueryGoodSpelling`, and have the Paratext plugin use that dialog non-modally instead of `FixSpellingForm`.

**Architecture:** Core (`encoding-converters-core/src/SpellingFixer30`) gets the bug fix, a public reusable `QueryGoodSpelling`, and `CscProject.TryAssignCorrectSpelling`. The plugin (`silconverters/src/SpellFixerPluginForParatext`) hosts the dialog via `Show(owner)` and handles its `Accepting` event.

**Tech Stack:** C# WinForms, .NET Framework 4.8 (core multi-targets), MSBuild (VS 18 Insiders), local NuGet feed `C:\btmp\LocalNuget`.

**Spec:** `docs/superpowers/specs/2026-11-13-shared-fix-spelling-dialog-and-custom-cc-design.md`

## Global Constraints

- NuGet version stays **1.0.4**.
- No binary-breaking change to existing public SpellingFixer30 members (the `void AssignCorrectSpelling(...)` overloads keep their signatures).
- Core's existing modal callers of `QueryGoodSpelling` must behave as before.
- MSBuild: `C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe`; core builds via `"EncConverters 2019.sln" /restore /t:SpellingFixer30 /p:Platform=x64` (and Win32).
- Neither repo has a test project for this code: verification is builds plus a scratch PowerShell harness (in the session scratchpad) that drives internals via reflection.

## Review Focus

- `.cct` hand-edited with Windows line endings / blank lines inside the custom section → preserved byte-for-line.
- `.cct` with only one marker (user deleted the end marker) → fall back to the stored custom code, don't eat rules.
- `.cct` missing (first save) → header created as before.
- Dialog reloaded (`LoadWord`) while open with a word that has no similar words → list hides, no stale items.
- Modal caller presses Enter (AcceptButton) with no `Accepting` subscriber → closes with OK as before.

---

### Task 1: Keep the `.cct` custom section on save (core)

**Files:**
- Modify: `src/SpellingFixer30/Bad2GoodMap.cs` (static `SaveTable`, new helper)
- Modify: `src/SpellingFixer30/CscProjectStore.cs:180` (stop writing `CustomCcCode`)

**Interfaces:**
- Produces: `internal static string Bad2GoodMap.ReadCustomSection(string strFileSpec)` → lines strictly between `c +----------start custom changes----------+` and `CustomRuleEndComment`, joined with `Environment.NewLine`; `""` if both markers exist with nothing between; `null` if the file is missing or either marker is absent.

- [ ] Step 1: Scratch harness `test_custom_section.ps1` loads the built `SpellingFixer30.dll`, writes sample `.cct` files and asserts via reflection: two custom lines (incl. a blank one) → returned exactly; markers with nothing between → `""`; end marker missing → `$null`; no file → `$null`. Then calls `Bad2GoodMap.SaveTable(map, path, "SpellFixer_X", "' '", $null)` on a file with custom rules and asserts the rules are still between the markers and the corrections are below the last header line.
- [ ] Step 2: Run against the current build → custom-rule assertion fails.
- [ ] Step 3: Implement `ReadCustomSection`; in static `SaveTable`, call it *before* `CscProject.BackupFile` and pass `existing ?? strCustomCode` on. `CreateCCTableCsc` writes nothing when given `""`.
- [ ] Step 4: In `CscProjectStore` save mapping, write `CustomCcCode = null` (reading stays).
- [ ] Step 5: Build x64; run harness → all pass.
- [ ] Step 6: Commit in core.

### Task 2: `TryAssignCorrectSpelling` (core)

**Files:** Modify `src/SpellingFixer30/CscProject.cs:1414`

**Interfaces:**
- Produces: `public bool TryAssignCorrectSpelling(string strBadWord, string strReplacement, bool bNoUI, List<string> ContextForReplacement, bool dontSave = false)` — false iff the user answered No/Cancel to one of its questions; true when the mapping is (now or already) `bad → good`. The existing `void AssignCorrectSpelling(...)` with the same parameters calls it.

- [ ] Step 1: Move the body; each `return` after a declined question → `return false`; "exact match already there" and the end → `return true`.
- [ ] Step 2: Build; commit with Task 3.

### Task 3: Public, non-modal-capable `QueryGoodSpelling` with Similar words (core)

**Files:**
- Modify: `src/SpellingFixer30/QueryGoodSpelling.cs`, `QueryGoodSpelling.Designer.cs`
- Create: `src/SpellingFixer30/QueryGoodSpellingAcceptingEventArgs.cs` (add `Compile` item only if the csproj isn't SDK-globbing)
- Modify: `src/SpellingFixer30/CscProject.cs` (`AssignCorrectSpelling(string)`, `FindReplacementRule`)

**Interfaces:**
- Produces (all `public`):
  - `class QueryGoodSpelling : Form`; ctors `(Font font)` and `(Font font, Func<string, SpellFixerWords> getSimilarWords)`.
  - `void LoadWord(string strBadWord, string strReplacement, string strOriginalWord, bool bShowDelete, bool bShowSwapWords)`.
  - `DialogResult ShowDialog(string, string, string, bool, bool)` (unchanged signature; calls `LoadWord`).
  - `event EventHandler<QueryGoodSpellingAcceptingEventArgs> Accepting`; args: `string BadSpelling`, `string GoodSpelling`, `bool Cancel { get; set; }`.
  - `bool VernacularRightToLeft { get; set; }` (text boxes + list).
  - `TextBox BadWordTextBox { get; }`, `TextBox ReplacementTextBox { get; }`.
  - `string GoodSpelling`, `string BadSpelling` (unchanged).
- Layout: a 6th AutoSize column holding a 170px-wide panel (Label "Si&milar words:" docked top + `ListBox listBoxSimilarWords` fill, `IntegralHeight=false`) spanning rows 0–3. Panel hidden when no provider/no result; form widened by the panel width only when a provider is given.
- Behaviour: `textBoxBadWord_TextChanged` also refreshes the list (provider exceptions swallowed; items formatted `"{Value} ({Count})"`); click → Replacement text; double-click → `buttonOK.PerformClick()`. OK: set `m_strBadWord`/`m_strGoodWord` from the boxes, raise `Accepting`; if cancelled set `DialogResult = DialogResult.None` and return; else `Close()`.
- `CscProject.AssignCorrectSpelling(string)` and `FindReplacementRule` construct with `GetAmbiguousWords`.

- [ ] Step 1: Implement; build x64 and Win32.
- [ ] Step 2: Scratch harness `test_dialog.ps1`: construct with a provider returning two words, `LoadWord`, `Show()`, assert list visible with 2 items; reload with a word for which the provider returns `$null` → list hidden; subscribe `Accepting` with `Cancel=true`, click OK → form still visible; no subscriber, modal path covered by code review (`ShowDialog` unchanged).
- [ ] Step 3: Commit in core.

### Task 4: Refresh the local 1.0.4 package

- [ ] Step 1: Copy built `SpellingFixer30.dll` (x64 and Win32) into `%USERPROFILE%\.nuget\packages\encoding-converters-core\1.0.4\runtimes\EcDistFiles\win-<plat>\native\` (and `lib\` copies if present) so silconverters builds against it. Repacking the nupkg is left to the user's normal release step.

### Task 5: Plugin uses `QueryGoodSpelling` (silconverters)

**Files:**
- Delete: `src/SpellFixerPluginForParatext/FixSpellingForm.cs`, `FixSpellingForm.Designer.cs` (+ csproj entries if listed)
- Modify: `SpellFixerForm.cs` (≈241–355), `SpellFixerProject.cs:123-144`

**Interfaces:**
- Consumes: Task 2 and Task 3 signatures.
- `SpellFixerProject.AssignCorrectSpelling(bad, good, context)` → CSC: `return cscProject.TryAssignCorrectSpelling(bad, good, bNoUI: false, context)` (then `ReloadConverter`); legacy unchanged.
- `SpellFixerProject.GetSimilarWordsProvider` → `Func<string, SpellFixerWords>` or `null` for legacy (replaces `GetSimilarWords`/`SimilarWord` if no other user).

- [ ] Step 1: Replace the `FixSpellingForm` field/creation with `QueryGoodSpelling`; title `"{PluginName}: Fix Spelling — {verse} ({DisplayName})"` set after each `LoadWord(word, Convert(word) or word, word, false, false)`; `Accepting` → empty/unchanged check (message, `Cancel=true`) else `Cancel = !ApplyFix(bad, good)`; keyboard via `BadWordTextBox.Enter`, `ReplacementTextBox.Enter`, form `Activated`/`Deactivate`.
- [ ] Step 2: Build plugin x64 → 0 errors.
- [ ] Step 3: Commit in silconverters.
