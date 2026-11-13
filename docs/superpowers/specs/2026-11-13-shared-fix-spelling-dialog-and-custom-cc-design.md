# Shared Fix Spelling dialog, and keeping custom CC rules

Date: 2026-11-13. Spans two repos: `encoding-converters-core` (SpellingFixer30) and `silconverters` (SpellFixerPluginForParatext).

## Why

- Users want the code-point panes of SpellingFixer30's `QueryGoodSpelling` dialog when adding a correction in the Paratext plugin. In Devanagari, two words can look identical but differ by e.g. a ZWJ.
- The plugin's own `FixSpellingForm` has a **Similar words** list that every SpellingFixer30 client with a Consistent Spelling Fixer (CSC) project would benefit from.
- So: one dialog, in core, with both. The plugin uses it non-modally and drops `FixSpellingForm`.
- Separately, any rule a user types between the `start custom changes` / `end custom changes` lines of a CSC project's `.cct` is lost the next time a correction is saved, from any client (not just the plugin).

## Decisions (with the user)

| Topic | Decision |
|---|---|
| Approach | **A**: make `QueryGoodSpelling` public and able to run non-modally; the plugin hosts it. (B, a core-owned flow with plugin callbacks, and C, a shared UserControl in two dialogs, were rejected.) |
| Similar words | `CscProject.GetAmbiguousWords(bad)` as is: known-good and words-to-check matches, no distinction. An empty/missing words-to-check list is fine (`WordsToCheck.LoadTable` returns an empty map). |
| Custom CC code | The existing `.cct` is the source of truth for the custom section. `CustomCcCode` in the `.csf` isn't needed. |
| NuGet | Stays at 1.0.4 (not yet pushed to nuget.org). |

## Core: custom CC section (bug fix)

- `Bad2GoodMap.SaveTable` (static), before `CscProject.BackupFile` moves the file away, reads the lines strictly between the start and end markers of the existing `.cct`.
- If the file exists and has both markers, those lines (even if none) are written back as the custom section. Otherwise it falls back to the `strCustomCode` passed in (from `SpellFixerCustomCcCode`).
- `CscProjectStore` keeps *reading* `CustomCcCode` (old `.csf` files, used only as that fallback) but stops *writing* it.

## Core: `QueryGoodSpelling`

- `public` class. The existing constructor `(Font)` keeps working; a new one takes `(Font font, Func<string, SpellFixerWords> getSimilarWords)`.
- **Similar words** list, to the right of the text boxes and code-point panes, with a "Similar words:" label. Items show `word (count)`.
  - Filled from `getSimilarWords(bad)` whenever the bad spelling changes. Hidden when there's no provider or no result; exceptions from the provider are swallowed (it runs per keystroke).
  - Click puts the word in Replacement; double-click also does OK.
  - The dialog is widened only when a provider was given.
- `LoadWord(bad, replacement, originalWord, showDelete, showSwapWords)`: the setup `ShowDialog(...)` does today, made public so an open dialog can be reloaded. `ShowDialog(...)` calls it.
- `event EventHandler<QueryGoodSpellingAcceptingEventArgs> Accepting`, raised on OK with `BadSpelling`/`GoodSpelling`; setting `Cancel` keeps the dialog open (and, when modal, sets `DialogResult = None`). Nobody in core subscribes, so modal callers behave as before.
- `RightToLeft` support for the text boxes and list: `bool VernacularRightToLeft` property.
- `BadWordTextBox` / `ReplacementTextBox` exposed read-only (`TextBox`) so a host can hook `Enter` (keyboard switching).
- Where the list is turned on in core: `CscProject.AssignCorrectSpelling(string)` and `FindReplacementRule`. Not in the Bulk Picker, CorrectSpellingPicker or ViewBadGoodPairsDlg, which already show the bundle or are edits of existing rules.
- New `public bool CscProject.TryAssignCorrectSpelling(bad, good, bNoUI, context, dontSave=false)`: the existing method's body, returning whether the rule is now in place. The existing `void` method calls it (no binary break).

## Plugin

- `FixSpellingForm` (+ Designer) deleted.
- `SpellFixerForm` creates `QueryGoodSpelling(font, word => cscProject?.GetAmbiguousWords(word))`, sets `VernacularRightToLeft`, title `SpellFixer: Fix Spelling — <verse> (<project>)`, shows it with `Show(this)`, reloads it with `LoadWord(word, Convert(word), null, false, false)` (no "while fixing:" row; it would just repeat the bad word).
- `Accepting` handler = today's `ApplyFix(bad, good)`; `e.Cancel = !ApplyFix(...)`. Same checks as before (empty / unchanged replacement → message, stay open).
- Keyboard: `Enter` of both exposed text boxes → vernacular keyboard; form `Activated` / `Deactivate` as today.
- `SpellFixerProject.AssignCorrectSpelling(bad, good, context)` uses `TryAssignCorrectSpelling` for CSC projects instead of inferring from `Convert`.
- Legacy projects: the list is hidden (no provider) — same as today.

## Non-goals

- No change to the Bulk Picker / check-run dialogs.
- No colouring of similar words; no "known good vs. to check" distinction.
- No change to legacy (non-CSC) projects' `.cct` writing; they already preserve the header.

## Left to the builder

- Exact layout sizes; the list's mnemonic (must not clash with `&Swap Words`).
- Editing the bad spelling only refreshes the list (the Replacement box isn't re-derived, matching the current core dialog).
