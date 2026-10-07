# SpellFixerPluginForParatext — Design

Date: 2026-10-07
Status: Draft for review

## Goal

Bring the SILConvertersOffice "Check Spelling" feature (`WordApp.CheckSpelling_Click` +
`SpellingFixerWordProcessor`) to Paratext 9 as a plugin. The plugin walks the active Paratext project
word by word from the current verse. It runs each word through the user's SpellFixer EncConverter and
asks the user about every word the converter would change. Accepted fixes are written back to
Paratext once per verse.

## Decisions (from review with the user)

| Topic | Decision |
|---|---|
| Query dialog | Copy (not link or move) `BaseConverterForm` + `SILConverterProcessorForm` into the new project and adapt them; they are mature and rarely change. |
| Paratext project | The project of the active window (`host.ActiveWindowState.Project`). |
| SpellFixer project | Chosen via `FindReplaceHelper.QuerySpellFixProjectType()`, which asks whether to use a whole-word CscProject or a partial-word legacy SpellFixer project. The last one chosen is remembered per Paratext project. A "Choose Project" button switches to a different one (e.g. after finishing a chapter with the other kind). |
| Text checked | All publishable vernacular text tokens (`PtxPluginHelpers.IsPublishableVernacular`), including section headings (`\s`) and footnote text (`\ft`). Metadata (`\id`, `\rem`, …) is skipped. |
| Scope of a run | Starts at the verse currently selected in Paratext (tracked via `VerseRefChanged`) and runs to the end of the chapter. Then asks to continue with the next chapter, and at the end of a book, with the next book. |
| When fixes are written | Collected per verse; one write per verse that has accepted changes. |
| Following along | Paratext's sync group is moved to each verse as it is checked. |
| Shared code | Linked from `ParatextPluginBackTranslationHelper`. Code is moved into separate files there where needed. |
| Clipboard buttons | "Assign Correct Spelling" and "Find Replacement Rule" act on the word on the system clipboard, as ClipboardEC does. |
| Installer | Added to the existing BackTranslationHelper merge module. The feature is retitled so that both plugins install together, selected by default when Paratext 9 is detected. |

## 1. Project and build

New project `src/SpellFixerPluginForParatext/SpellFixerPluginForParatext.csproj`:

- SDK-style, `net48`, `Platforms` AnyCPU;x86;x64, `LangVersion latest`, `GenerateAssemblyInfo false`,
  with the same OutputPath scheme and `UseEcSharedItems` as `ParatextPluginBackTranslationHelper.csproj`.
- RootNamespace `SIL.SpellFixerPluginForParatext`, AssemblyName `SpellFixerPluginForParatext`.
- PackageReferences: `Encoding-Converters-Core` 1.0.2, `ParatextPluginInterfaces` 2.0.23,
  `ParatextEmbeddedUiPluginInterfaces` 2.0.23.
- HintPath reference to `BackTranslationHelper.dll` (for `FindReplaceHelper`) from
  `$(EcDistFilesPath)\win-$(Platform)\native\`, as the sister project does. The SpellingFixer30 types
  `FindReplaceHelper` exposes come from the EC-core package. Check during implementation that they
  resolve without an extra reference.
- Debug post-build event: same as the sister project's, targeting
  `%ParatextInstallDir%\plugins\SpellFixerPluginForParatext\SpellFixerPluginForParatext.ptxplg`.
- Added to `SEC VS2019.sln` with the same configuration/platform mappings as the sister project, so
  the existing GitHub Actions `build.yml` builds it with no workflow change. Check that
  `TeamCityBuild.xml` builds the solution too, rather than listing projects.

### Linked shared files (from `src/ParatextPluginBackTranslationHelper/`)

`TextToken.cs`, `TokenBase.cs`, `MarkerToken.cs`, `VerseRef.cs`, plus:

- **`PtxPluginHelpers.cs`** reads `Properties.Settings.Default.AdditionalMarkersToTranslate`, which
  only exists in the sister project. Move `IsTranslatable` and the `_additionalMarkersToTranslate`
  field into a new sister-project-only partial file (`PtxPluginHelpers.Translatable.cs`). Make
  `PtxPluginHelpers` a `partial` class so the linked part has no settings dependency.
- **New `UsfmChapterTokens.cs`** (in the sister project, linked into the new one): move
  `GetBookChapterKey`, `GetBookChapterVerseRangeKey`, `TriangulateBookChapterVerseKey` and the
  "group chapter tokens by verse into a `SortedDictionary<string, List<IUSFMToken>>`" logic out of
  `BackTranslationHelperForm` into public static methods. `BackTranslationHelperForm` calls them;
  its behavior does not change.

The shared files keep their `SIL.ParatextBackTranslationHelperPlugin` namespace, and the new project
adds a `using` for it. That keeps the sister project untouched apart from the moves above.

### Copied query dialog

From `src/SILConvertersOffice/`: `BaseConverterForm.{cs,designer.cs,resx}` and
`SILConverterProcessorForm.{cs,designer.cs,resx}` become `SpellFixerQueryFormBase` and
`SpellFixerQueryForm`. Changes:

- `FontConverter` is replaced by a `DirectableEncConverter` plus a `Font` for each side; both sides
  use the Paratext project's font (`project.Language.Font`, or the closest equivalent in the plugin
  interfaces) and RTL setting.
- The title is "`Spell Fixer for Paratext: <converter name>`" instead of using `OfficeApp.cstrCaption`.
- The `FormButtons` enum is copied as-is.
- `ForwardString` (the "Replace with" text) is renamed `CorrectedSpelling` (and `InputString` stays as is).
- The View Rule / Add Rule / Refresh / Debug buttons and their handlers are kept. They use the main
  window's `FindReplaceHelper` instead of creating their own.
- Remove Office-only bits that don't compile or don't apply.

## 2. Plugin entry point and main window

### `SpellFixerPluginForParatext : IParatextStandalonePlugin`

- Name "Spell Fixer", publisher "SIL", version 1.0.
- Copies the sister plugin's `AssemblyResolve` handler for `SilEncConverters40.dll` / `ECInterfaces.dll`.
- Menu entry `&Spell Fixer...` at `PluginMenuLocation.ScrTextTools`.
- `Run(host, state)` opens one non-modal `SpellFixerForm`, or activates it if it is already open.

### `SpellFixerForm` (non-modal)

Contents:

- **Paratext project:** the short name of the active window's project.
- **Spell Fixer project:** a label such as `Whole word (CSC): <name>` or
  `Partial word (legacy): <name>`, plus a **Choose Project…** button.
- **Current verse:** the reference that a check will start from (updated by `host.VerseRefChanged`
  while no check is running).
- **Check from Current Verse:** starts the run in section 3.
- **Assign Correct Spelling (clipboard):** tooltip "Assign the correct spelling for the word on the
  system clipboard. Copy the word (e.g. from Paratext) to the clipboard before clicking this button."
  Calls `FindReplaceHelper.AssignCorrectSpelling(clipboardText)`.
- **Find Replacement Rule (clipboard):** tooltip "Find the replacement rule that applies to the word
  on the system clipboard. Copy the word to the clipboard before clicking this button." Calls
  `FindReplaceHelper.FindReplacementRule(clipboardText)`.
- **Edit Spelling Fixes…:** calls `FindReplaceHelper.EditSpellingFixes()`.
- **Pin (stay on top) toggle button:** uses copied `pinup.bmp` / `pindown.bmp` resources and toggles
  `TopMost`. The state is saved in Settings.
- **Status line:** shows progress (e.g. "Checking MAT 5:3…", "Wrote 2 fixes to MAT 5:3").

Clipboard buttons with an empty or non-text clipboard show a message telling the user to copy a word
first.

### Choosing and remembering the SpellFixer project

- **Choose Project…:** creates a new `FindReplaceHelper` and calls `QuerySpellFixProjectType()`. If
  the user picks a project, `SpellFixerEncConverterName` is read; it already carries the CSC or legacy
  prefix (`SpellingFixer.SFConverterPrefixCsc` / `SFConverterPrefix`), so the prefix records which
  kind it is. The name is stored in the user setting `MapPtxProjectToSpellFixerConverter`, a
  `StringCollection` of `ptxShortName=converterName` entries, and saved.
- **On startup:** if the active Paratext project has a stored converter name, restore it with
  `new FindReplaceHelper(storedName)` without asking. If that fails (e.g. the project was deleted),
  remove the entry and ask, as when nothing is stored.
- **The converter used for checking:**
  `new DirectableEncConverter(helper.SpellFixerEncConverterName, bDirectionForward: true, NormalizeFlags.None)`.
  Since `FindReplaceHelper` already exposes `SpellFixerEncConverterName`, **no change to
  encoding-converters-core is needed**.
- **After `AssignCorrectSpelling` or `EditSpellingFixes`:** call
  `DirectableEncConverter.EncConverters.Reinitialize()` (or recreate the converter) so new rules take
  effect, as `SILConverterProcessorForm.TrySpellFixerProjectLogin` does.
- **Settings upgrade:** call `Settings.Default.Upgrade()` on the first run after a Paratext upgrade,
  as the sister project does.

## 3. Checking flow

Run from **Check from Current Verse**. While it runs, the main window's buttons are disabled, apart
from the query dialog's own Cancel.

1. **Load the chapter.** Call `project.GetUSFMTokens(book, chapter)` and group the tokens with
   `UsfmChapterTokens` into an ordered verse-key → tokens map. Find the start key with
   `TriangulateBookChapterVerseKey` for the current reference. Process keys in order from there to
   the end of the chapter.
2. **For each verse key:**
   1. Call `host.SetReferenceForSyncGroup(verseRef, state.SyncReferenceGroup)` so Paratext shows the
      verse being checked.
   2. For each `IUSFMTextToken` in the verse where `IsPublishableVernacular(t, tokens)` is true, split
      `t.Text` into alternating word and separator runs. The separators are runs of space, tab, CR,
      LF and U+00A0; these are the same terminators `OfficeDocument` uses, minus Word's
      footnote/form-feed characters. Joining the runs gives back exactly the original text.
   3. Convert each word with the converter.
      - If the output equals the input, go on to the next word.
      - Else if Replace All is in effect, accept the change.
      - Else if this word is in this run's Replace Every set, accept that change.
      - Otherwise show `SpellFixerQueryForm` with the input and output. The user may edit the
        "Replace with" box.
   4. Handle the dialog result:
      - **Skip:** keep the word.
      - **Replace Once:** use the form's `CorrectedSpelling`.
      - **Replace Every:** use it and add it to the per-run Replace Every set.
      - **Replace All:** use it and turn on Replace All for the rest of the run.
      - **Cancel:** stop the run after writing any fixes already accepted in this verse.
   5. If any word in the token changed, join the runs back together and make a replacement token:
      `new TextToken(original) { Text = rebuilt }`.
   6. If the verse has any replaced tokens:
      1. Get a write lock with `project.RequestWriteLock(plugin, ReleaseRequested, book, chapter)`.
      2. Swap the replaced tokens into the chapter token list in place.
      3. Call `project.PutUSFMTokens(writeLock, allChapterTokens, book)`.
      4. Release the lock.
      5. Reload the chapter tokens (Paratext raises `ScriptureDataChanged`), keep going from the next
         verse key, and update the status line.
3. **At the end of the chapter:** ask "Continue checking with the next chapter?" (Yes/No). At the end
   of the book, ask "Continue checking with the next book (`<book>`)?". Moving on uses
   `IVerseRef.GetNextVerse(project)` (or chapter 1 of the next book in `project.AvailableBooks`), and
   the run continues at step 1.
4. **At the end of the run:** show the number of words fixed. Clear the Replace All flag and the
   Replace Every set. Turn `VerseRefChanged` tracking back on.

### Error handling

- **Write lock refused** (no edit permission, or the user cancelled Paratext's save prompt): show
  "You don't have edit privilege on `<ref>` of `<project>`", then stop the run.
- **Converter throws on a word:** log it with `host.Log`, show it once, and skip that word.
- **`GetUSFMTokens` returns null** (some books, e.g. GLO): skip that chapter.
- **No SpellFixer project selected:** the check, clipboard and edit buttons are disabled until one is
  chosen.

## 4. Installer

- **`Installer/ParatextBackTranslationHelperPlugin64bitMM/ParatextBackTranslationHelperPlugin_MergeModule.wxs`:**
  add a `SpellFixerPluginForParatext` directory under `Paratext9Plugins`. It holds one component,
  with a new GUID, containing the `SpellFixerPluginForParatext.ptxplg` file with source
  `..\..\output\x64\release\SpellFixerPluginForParatext.dll`. It uses the same `Assembly=".net"`
  attributes as the existing plugin. Change the module's `SummaryInformation` description to cover
  both plugins. The merge module and its IDs are **not** renamed.
- **`Installer/SEC Setup 64bit/EcFeatures.wxs`:** keep `Feature Id="Paratext_BackTranslation_Helper_Plugin"`
  so upgrades still work. Change the Title to "SIL Converters Plugins for Paratext" and the
  Description to cover both the Back Translation Helper and the Spell Fixer (launched from Paratext's
  Tools menu). Level stays 4, with 3 when Paratext 9 is detected, so the feature is selected by
  default only when Paratext 9 is installed. The `SpellFixerEc` MergeRef is already there.
- **Build order:** confirm that the wixproj or build.yml builds the solution before harvesting the
  merge module. It already does for the sister plugin, so this is expected to need no change.

## 5. Testing

In `src/SILConvertersWordML/TestBwdc`, which already references the sister plugin project, add a
ProjectReference to `SpellFixerPluginForParatext` (same x64 condition) and a new
`UnitTest_PtxSpellFixer.cs`. Its NUnit tests:

- Splitting a token into word and separator runs and joining them gives the original text exactly,
  including leading and trailing whitespace, double spaces, NBSP and newlines.
- Applying a word → replacement map to a verse's tokens changes only publishable vernacular text
  tokens. It leaves marker tokens and non-publishable text alone and keeps token order.
- Splicing the replaced verse tokens back into the chapter token list leaves every other verse
  unchanged.

To make these testable, the split, rebuild and splice logic lives in a static helper class
(`SpellFixerTokenProcessor`), separate from the form. The word-check callback is passed in as a
`Func<string, string>` that returns the replacement (or the original word to skip).

Manual check in Paratext 9 (Debug post-build copy):

- Check a chapter with a CSC project, and with a legacy project.
- Try each dialog button.
- Continue to the next chapter and to the next book.
- Try a chapter without edit permission.
- Use both clipboard buttons, Edit Spelling Fixes, and the pin.
- Confirm the BackTranslationHelper plugin still works after the shared-code move.

## Out of scope

- Renaming the merge module or Feature Id.
- Moving the query dialog into encoding-converters-core.
- Checking several projects or a range of books in one run, apart from the continue prompts.
