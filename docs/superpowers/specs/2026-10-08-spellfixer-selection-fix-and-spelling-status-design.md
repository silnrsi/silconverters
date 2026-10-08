# SpellFixer plugin: selection-based Fix Spelling dialog + SpellingStatus.xml update — Design

Date: 2026-10-08
Status: Draft for review
Builds on: `2026-10-07-spellfixer-plugin-for-paratext-design.md` (branch `feature/spellfixer-plugin-for-paratext`)

## Goal

Make it quicker to add a spelling rule while working in Paratext. The workflow:

1. Copy the correct form, from the text pane or from Paratext's similar-words menu.
2. Select the misspelled word in the text.
3. Click **Assign Correct Spelling (selection)** in the SpellFixer window.

A **non-modal** Fix Spelling dialog opens with the selected word as the bad form. While it is open the user can
still search Paratext. Pressing Ctrl+V pastes the correct form, or the user can click a similar word from the
CSC project's bundle. Clicking OK does three things: saves the rule, replaces the selected word in Paratext, and
records the fix. The recorded fixes are merged into Paratext's `SpellingStatus.xml` by a small helper exe after
Paratext exits, because the plugin API has no way to update spelling status while Paratext is running.

## Decisions (from review with the user)

| Topic | Decision |
|---|---|
| Bad form source | `IScriptureTextSelection.SelectedText` of the current project (not the clipboard). |
| Paste | No Paste button; Ctrl+V in the Replacement box. |
| Modality | Only the Assign Correct Spelling dialog becomes non-modal. The check-run query dialog stays modal. |
| Similar words | `CscProject.GetAmbiguousWords(bad)` as is, listed in count order with **no colouring**, CSC projects only. No encoding-converters-core changes. |
| After OK | Automatically replace the selected occurrence in Paratext. |
| Context | Pass the selection's context to `CscProject.AssignCorrectSpelling`. |
| Keyboard | `IProject.VernacularKeyboard.Activate()` when the dialog's text boxes get focus. |
| SpellingStatus | No API exists. Record fixes per project in `<projects folder>\<ShortName>\local\SpellFixer\PendingSpellingStatus.xml`. On `ShuttingDown`, launch a helper exe that waits for Paratext to exit and then merges them into `SpellingStatus.xml`. |
| Dropped | Non-modal check run; Paste button; good/bad colouring; any encoding-converters-core change. |

## Research findings this design relies on

- **Selection.**
  - `IParatextChildState.Selections` is an `IReadOnlyList<ISelection>`. It may be empty or null.
  - `IPluginHost.ActiveWindowSelectionChanged(host, activeWindowState, currentSelections)` is raised for any window. Check `activeWindowState.Project`.
  - `IScriptureTextSelection` has the following members:
    - `SelectedText`: raw USFM, possibly empty.
    - `VerseRefStart` / `VerseRefEnd`.
    - `Offset`: the character offset in the verse's raw USFM, counted from the `\v`.
    - `BeforeContext` / `AfterContext`: raw USFM for the rest of the verse on either side of the selection.
- **Editing.** There is no API that edits a selection. Text can only change through `RequestWriteLock` followed by `PutUSFM`, `PutUSFMTokens` or `PutUSX`. `GetUSFM(book, chapter[, verse])` reads text.
- **Spelling.** No plugin API reads or writes spelling status, in any version up to 2.0.100. `DataType.SpellingStatus` is only a permission for `IProject.CanEdit`.
  - Paratext does not watch `SpellingStatus.xml`; there is no FileSystemWatcher in Paratext.exe or ParatextData.dll.
  - The file is tracked by Send/Receive. `local/**` is in `.hgignore`.
  - Schema:
    ```xml
    <SpellingStatus>
      <Status Word="good" State="R" />
      <Status Word="bad" State="W"><Correction>good</Correction></Status>
    </SpellingStatus>
    ```
- **Keyboard.** `IKeyboard.Activate()`; `IProject.VernacularKeyboard` (which may be null); `IPluginHost.DefaultKeyboard`. The API cannot report which keyboard is currently active.
- **Lifecycle.** `IPluginHost.ShuttingDown` is a `CancelEventHandler`, raised when Paratext starts shutting down.
- **SpellingFixer30.**
  - `CscProject.AssignCorrectSpelling(string bad)` uses its internal `QueryGoodSpelling` window with `ShowDialog()`, so it cannot be made non-modal. Instead we call `CscProject.AssignCorrectSpelling(string bad, string good, bool bNoUI, List<string> context, …)`, which is public and shows no UI (CscProject.cs:1100).
  - Legacy projects: `SpellingFixer.AssignCorrectSpelling(string bad, string good)`. It may show its own Abort/Retry/Ignore box if an existing rule conflicts.
  - `FindReplaceHelper` keeps `m_cscProject` and `m_aSpellFixerLegacy` as `protected` fields.
  - `GetAmbiguousWords(word)` is public. It searches the known-good list and the words-to-check list, excludes the word itself, sorts by count with the most frequent first, and returns null if nothing matches.
  - `WordsInContext` controls how many words on each side make up a context string. BulkSFMConverter builds its contexts this way (SCConvForm.cs `CscAddToCheckList`).

## 1. Selection-based, non-modal Fix Spelling dialog

### 1.1 Tracking the selection

When the user clicks the SpellFixer window's button, that window is the active one, so Paratext no longer
reports the text pane's selection. `SpellFixerForm` therefore subscribes to `host.ActiveWindowSelectionChanged`
and keeps the most recent `IScriptureTextSelection` whose `activeWindowState.Project.ShortName` equals the
plugin's project. It unsubscribes when the form closes.

`SelectionReplacer.ValidateSelection(selection)` accepts a selection only if:
- `SelectedText.Trim()` is non-empty;
- `VerseRefStart` equals `VerseRefEnd`;
- the text has no `\` (no markers);
- the text has no line breaks.

If the selection fails these checks, the plugin shows "Select the misspelled word in the {project} text window
first (one word, within one verse)." The bad form is `SelectedText` with surrounding whitespace trimmed, and
`Offset` and the contexts are adjusted to match the trim.

### 1.2 Main window changes

- **Assign Correct Spelling (selection):** the label changes, and so does the tooltip: "Select the misspelled word in the Paratext text window, then click here. Paste the correct spelling into the Replacement box (Ctrl+V), or click one of the similar words."
- **Find Replacement Rule:** uses the tracked selection if it is valid, and otherwise the clipboard (as now). The label becomes "Find Replacement Rule".
- **Edit Spelling Fixes:** no change.

### 1.3 `FixSpellingForm` (new, non-modal)

- **Showing it:**
  - It is opened with `Show(owner: SpellFixerForm)`.
  - There is only one instance. Clicking the button again while it is open loads the new selection into it and activates it.
  - `TopMost` follows the main window's pin.
  - The SpellFixer window's "Check from Current Verse" is disabled while the dialog is open, and the dialog cannot be opened during a check run.
- **Controls:**
  - **Bad form** (EcTextBox): pre-filled from the selection and editable. Changing it re-queries the replacement and the similar words.
  - **Replacement** (EcTextBox): pre-filled with `Converter.Convert(bad)`, which is the existing rule's output, or the bad form itself if there is no rule. The text is selected so Ctrl+V overwrites it.
  - **Similar words** (ListBox, to the right of the two text boxes):
    - It holds `GetAmbiguousWords(bad)`, in returned order, with each item shown as `word (count)`.
    - Clicking an item puts that word in the Replacement box.
    - Double-clicking also clicks OK.
    - The list is hidden for legacy projects, and when the result is null or empty.
  - **Verse label:** shows the selection's `VerseRefStart`.
  - **Buttons:** OK (AcceptButton) and Cancel (CancelButton).
- **Fonts and direction:** the project font and right-to-left setting, as in the query dialog.
- **Keyboard:**
  - On `Enter` of either text box: `project.VernacularKeyboard?.Activate()`.
  - On the form's `Deactivate`: `host.DefaultKeyboard?.Activate()`. Paratext switches back to its own keyboard when its text window gets focus.
- **OK:**
  - If the bad and replacement forms are equal, or the replacement is empty, the plugin says nothing would change and leaves the dialog open.
  - Otherwise it runs §1.4 and closes the dialog.
- **Cancel:** closes the dialog and changes nothing.

### 1.4 What OK does

1. **Save the rule.**
   - The plugin gets the underlying objects through `PluginFindReplaceHelper : FindReplaceHelper`, a plugin-side subclass that exposes the protected `m_cscProject` and `m_aSpellFixerLegacy`. `SpellFixerProject` is changed to create this subclass in both `QueryUser` and `FromConverterName`, so the plugin keeps one in-memory copy of the project. `QueryUser` becomes `new PluginFindReplaceHelper(); helper.QuerySpellFixProjectType();` and then checks `IsSpellFixerProject`, which is the same logic as `FindReplaceHelper.GetFindReplaceHelper`. The plan's first task must confirm that the shipped 1.0.2 `BackTranslationHelper.dll` is the `!UseReflection` build, i.e. that it has these protected members and `QuerySpellFixProjectType`. If it isn't, stop and report.
   - **CSC:** `cscProject.AssignCorrectSpelling(bad, good, bNoUI: true, context)`. The context is built as in §1.5. Any further optional parameters keep their defaults, so the rules are saved immediately.
   - **Legacy:** `spellingFixer.AssignCorrectSpelling(bad, good)`.
   - Then `SpellFixerProject.ReloadConverter()`.
   - If this step throws, the plugin shows the error and stops. Nothing is replaced and nothing is recorded.
2. **Replace the selected occurrence in Paratext** (`SelectionReplacer`; see §1.6). If replacement fails, the plugin shows why; the rule stays saved.
3. **Record the fix** for SpellingStatus (§2.1).

### 1.5 Context string

- Take `BeforeContext + bad + AfterContext` and remove USFM markers with `\\[^\s\\*]+\*?\s?`. Keep the text of the remaining text runs.
- Split the result on whitespace, find the bad word's position, and take up to `cscProject.WordsInContext` words on each side.
- Join them with single spaces. The context is `new List<string> { thatString }`.
- The verse reference is not included, to match BulkSFMConverter's context format.
- This is implemented as a pure function, `SelectionReplacer.BuildContext(before, word, after, wordsEachSide)`.

### 1.6 Replacing the selected occurrence

`SelectionReplacer.TryReplaceInVerse(currentVerseUsfm, selection, bad, good, out newVerseUsfm, out reason)` is
a pure function. It succeeds only if both of these hold:
- `currentVerseUsfm == selection.BeforeContext + selection.SelectedText + selection.AfterContext`, i.e. the verse is unchanged since the selection was made;
- `currentVerseUsfm.Substring(trimmedOffset, bad.Length) == bad`.

The new verse is the old verse with that range replaced by `good`.

If `BeforeContext.Length != Offset`, the plugin treats the selection as having only partial context. It then
requires only that `currentVerseUsfm` has `bad` at `trimmedOffset`, with the 20 characters before and after
equal to the matching ends of `BeforeContext` and `AfterContext`.

Steps in the form:
1. Request the write lock for the selection's book and chapter. If it is null, show the no-edit-privilege message.
2. Read `chapterUsfm = GetUSFM(book, chapter)` and `verseUsfm = GetUSFM(book, chapter, verse)`.
3. `verseUsfm` must occur exactly once in `chapterUsfm`; otherwise fail with "couldn't locate the verse".
4. Call `TryReplaceInVerse`.
5. Splice the new verse into `chapterUsfm` with `SelectionReplacer.ReplaceVerseInChapter(chapterUsfm, verseUsfm, newVerseUsfm)`.
6. `PutUSFM(lock, newChapterUsfm, book)`.
7. Dispose the lock in a `finally` block.

Whatever happens, the plugin then calls `setSyncReference(VerseRefStart)`.

### 1.7 Rules added from the check-run query dialog

`SpellFixerQueryForm`'s "Add Rule" button keeps using the modal `FindReplaceHelper.AssignCorrectSpelling(word)`.
Afterwards the plugin compares `Converter.Convert(word)` from before and after the call. If the output changed
and differs from `word`, it records `word` → new output (§2.1), with no Paratext replacement. "View Rule" is not
recorded.

## 2. SpellingStatus.xml update after Paratext exits

### 2.1 Recording

- **Project folder.** `ParatextProjectFolder.Find(project)`:
  - reads the projects directory from the registry value `Settings_Directory`, under `HKLM\SOFTWARE\WOW6432Node\Paratext\8` or else `HKLM\SOFTWARE\Paratext\8`;
  - forms `<dir>\<ShortName>`;
  - confirms that `<dir>\<ShortName>\Settings.xml` exists and that its `<Guid>` equals `project.ID` (case-insensitive; if Settings.xml has no Guid, the folder is accepted).

  If the folder can't be found, the plugin logs it, shows a message once per session, and records nothing.
- **Permission.** If `project.CanEdit(plugin, DataType.SpellingStatus)` is false, the plugin records nothing and shows a message once per session.
- **Pending file:** `<projectFolder>\local\SpellFixer\PendingSpellingStatus.xml`. The folder is created if missing.
  ```xml
  <PendingSpellingStatus>
    <Fix Bad="bad" Good="good" When="2026-10-08T14:03:00Z" />
  </PendingSpellingStatus>
  ```
  - Entries are appended in order.
  - Reading and writing go through `PendingSpellingStatusStore`, which writes a temp file and renames it.
  - `PendingSpellingStatusStore` and `SpellingStatusMerger` are shared source files, linked into both the plugin and the helper.

### 2.2 Starting the helper

- `SpellFixerPlugin` subscribes to `host.ShuttingDown` once, the first time `Run` is called.
- On shutdown, if any project the plugin recorded in this session has a non-empty pending file, it starts `SpellingStatusUpdater.exe`:
  - the exe is in the same folder as the plugin assembly;
  - arguments are `--wait-pid <Paratext process id> --projects-dir "<dir>"`;
  - it is started with `UseShellExecute=false` and `CreateNoWindow=true`.
- The plugin never cancels the shutdown.
- Pending files left from a crash are also picked up, because the helper scans every project.

### 2.3 `SpellingStatusUpdater.exe` (new project `src/SpellingStatusUpdater`, net48 WinExe, no UI)

1. Wait for the process `--wait-pid` to exit, for up to 30 minutes. If it is still running after that, exit and leave everything pending.
2. Find every `<projects-dir>\*\local\SpellFixer\PendingSpellingStatus.xml` that has at least one `Fix`.
3. For each project folder:
   1. If `SpellingStatus.xml` exists, copy it to `local\SpellFixer\SpellingStatus.xml.<yyyyMMddHHmmss>.bak`. The backup goes under `local` so it isn't added to Send/Receive.
   2. Load it, or start from an empty `<SpellingStatus/>`.
   3. Apply the pending fixes in order with `SpellingStatusMerger.Apply(doc, bad, good)`.
   4. Save as UTF-8 with an XML declaration and two-space indentation, matching Paratext's own formatting, by writing a temp file and replacing.
   5. Rewrite the pending file without the applied entries, or delete it.
   6. If any I/O error occurs for that project, leave its pending file untouched and go on to the next project.
4. Append one line per project, plus any errors, to `<projects-dir>\<ShortName>\local\SpellFixer\SpellingStatusUpdater.log`.

### 2.4 Merge rules (`SpellingStatusMerger.Apply(XDocument doc, string bad, string good)`)

- **Bad word:** the `Status` element with `Word == bad` (ordinal comparison) becomes `State="W"` with exactly one `<Correction>good</Correction>`. It is created at the end if missing.
- **Good word:** the `Status` element with `Word == good` becomes `State="R"` with no `Correction` child. It is created if missing.
- **Order:** fixes are applied in order, so a later fix wins.
- **Everything else:** other elements, other attributes and the order of existing entries are left as they are.

## 3. Build, installer, tests

- **Solution:** `src/SpellingStatusUpdater/SpellingStatusUpdater.csproj` is added to `SEC VS2019.sln`. Its outputs follow the same `output\x64\{Debug,Release}` scheme. It links `PendingSpellingStatusStore.cs` and `SpellingStatusMerger.cs` from the plugin project.
- **Debug post-build:** the plugin's Debug post-build also copies `SpellingStatusUpdater.exe` into `%ParatextInstallDir%\plugins\SpellFixerPluginForParatext\`.
- **Merge module:** a new component for `SpellingStatusUpdater.exe`, with a new GUID, goes in the `SpellFixerPluginForParatext` directory of `ParatextBackTranslationHelperPlugin_MergeModule.wxs`. The wixproj solution dependency on the new project is added.
- **Unit tests** (TestBwdc, `UnitTest_PtxSpellFixer.cs` or a new `UnitTest_SpellingStatus.cs`):
  - `SelectionReplacer.ValidateSelection`: empty, multi-verse, contains a marker, whitespace-trimmed.
  - `SelectionReplacer.TryReplaceInVerse`:
    - an unchanged verse is replaced at the offset;
    - a changed verse is refused;
    - the bad form is no longer at the offset, so it is refused;
    - the replacement may be longer or shorter than the bad form;
    - a word that occurs twice: only the selected occurrence changes.
  - `SelectionReplacer.ReplaceVerseInChapter`: exactly one occurrence is required.
  - `SelectionReplacer.BuildContext`:
    - markers are stripped;
    - fewer than N words are available on one side;
    - the word is at the start or end of the verse.
  - `SpellingStatusMerger.Apply`:
    - a new bad word;
    - an existing R that becomes W;
    - an existing W whose Correction is replaced;
    - the good word goes from W to R and its Correction is removed;
    - unrelated entries are kept byte-for-byte;
    - a later fix overrides an earlier one.
  - `PendingSpellingStatusStore`: a round trip; appending to an existing file; removing applied entries.
- **Manual checks in Paratext:**
  - the selection workflow;
  - the dialog is non-modal (search in Paratext while it is open);
  - Ctrl+V, and clicking a similar word;
  - the keyboard switches;
  - the selected word is replaced and the rule is saved, for CSC and for legacy;
  - a stale selection is refused;
  - quit Paratext, confirm `SpellingStatus.xml` now has W with the Correction and R entries, the backup is under `local\SpellFixer`, and the log is written, then restart Paratext and see the statuses in the Wordlist.

## Out of scope

- A non-modal check run.
- Good/bad colouring of similar words.
- Changes to encoding-converters-core.
- Updating Paratext's spelling status while it is running.
- Importing Paratext's spelling status into the CSC project (`AddParatextSpellingStatusToCheckLists` exists; a possible later feature).
