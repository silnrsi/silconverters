# SpellFixer Selection-based Fix Spelling + SpellingStatus Update — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a non-modal Fix Spelling dialog to the SpellFixer Paratext plugin. It takes the bad form from the Paratext selection, lists similar words, saves the rule with context, and replaces the selected word in Paratext. It also records each fix so that a small helper exe can merge it into `SpellingStatus.xml` after Paratext exits.

**Architecture:**
- **Pure, unit-tested helpers:**
  - `SelectionReplacer`: validates the selection, swaps the word in the verse and chapter USFM, and builds the context string.
  - `SpellingStatusMerger` and `PendingSpellingStatusStore`: XML merge and the pending-file store. Both are shared source files, linked into the helper exe.
  - `ParatextProjectFolder`: finds the project folder.
  - `SpellingStatusUpdateRunner`: the helper's per-project merge.
- **WinForms and Paratext glue:**
  - `FixSpellingForm`: the non-modal dialog.
  - Additions to `SpellFixerForm`: selection tracking, OK handling, write lock with `PutUSFM`, and recording the fix.
  - `PluginFindReplaceHelper`: exposes `FindReplaceHelper`'s protected `CscProject` and `SpellingFixer` fields.
  - A `ShuttingDown` hook in `SpellFixerPlugin` that launches `SpellingStatusUpdater.exe`.

**Tech Stack:** C# (LangVersion latest), .NET Framework 4.8, WinForms, System.Xml.Linq, Paratext PluginInterfaces 2.0.23, Encoding-Converters-Core 1.0.2 (BackTranslationHelper.dll `FindReplaceHelper`, SpellingFixer30.dll `CscProject`/`SpellingFixer`), NUnit 3 (TestBwdc), WiX 6.

**Spec:** `docs/superpowers/specs/2026-10-08-spellfixer-selection-fix-and-spelling-status-design.md`

## Global Constraints

- Work on the existing branch `feature/spellfixer-plugin-for-paratext`.
- End every commit message with `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.
- **Do not change encoding-converters-core.**
  - The shipped 1.0.2 `BackTranslationHelper.dll` is the `!UseReflection` build. Its `FindReplaceHelper` has protected fields `m_cscProject` and `m_aSpellFixerLegacy`, protected `IsCscProject`, `IsSpellFixerLegacyProject` and `IsSpellFixerProject`, and public `QuerySpellFixProjectType()`. This was verified by reflection on 2026-10-08.
- **Signatures used:**
  - `CscProject.AssignCorrectSpelling(string strBadWord, string strReplacement, bool bNoUI, List<string> ContextForReplacement, bool dontSave = false)`
  - `SpellingFixer.AssignCorrectSpelling(string strBadWord, string strReplacement)`
  - `CscProject.GetAmbiguousWords(string)` returns `SpellFixerWords : List<SpellFixerWord>` (`.Value`, `.Count`), or null.
  - `CscProject.WordsInContext` (int).
  - All of these are in namespace `SpellingFixer30`.
- **Only the Assign Correct Spelling dialog is non-modal.** The check-run query dialog stays modal.
- **No Paste button.** Use Ctrl+V.
- **No colouring** of similar words.
- **Pending file:** `<projectFolder>\local\SpellFixer\PendingSpellingStatus.xml`. Backups and the log also go in `local\SpellFixer`.
- **Merge rule:**
  - The bad form becomes `State="W"` with exactly one `<Correction>good</Correction>`.
  - The good form becomes `State="R"` with no Correction.
  - A later fix wins.
  - Other entries are left as they are.
- **SpellingStatus.xml output:** UTF-8 with BOM, an XML declaration, two-space indentation and CRLF, matching Paratext's own file.
- **Exact UI strings:**
  - "Assign Correct Spelling (selection)", tooltip: "Select the misspelled word in the Paratext text window, then click here. Paste the correct spelling into the Replacement box (Ctrl+V), or click one of the similar words."
  - "Find Replacement Rule", tooltip: "Find the replacement rule for the word selected in the Paratext text window (or, if nothing is selected, the word on the clipboard)."
- **Build and test (PowerShell, from the repo root).**
  - `$env:ParatextInstallDir=''` skips the Debug deploy to Program Files, which needs admin.
  - `$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -find 'MSBuild\Current\Bin\amd64\MSBuild.exe'`
  - `$vstest = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -find '**\vstest.console.exe' | Select-Object -First 1`
  - Build tests: `& $msbuild src\SILConvertersWordML\TestBwdc\TestBwdc.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m`
  - Run tests: `& $vstest src\SILConvertersWordML\TestBwdc\bin\x64\Debug\TestBwdc.dll /Platform:x64 /TestCaseFilter:"<filter>"`
- **TestBwdc** lists its source files explicitly. Each new test file needs a `<Compile Include=... Condition="'$(Platform)' == 'x64'" />` entry. The plugin is referenced with `Aliases="SpellFixer"`. Test files start with `extern alias SpellFixer;` and `using SpellFixer::SIL.SpellFixerPluginForParatext;`.

## Review Focus

1. **The meaning of `IScriptureTextSelection.Offset` compared with `IProject.GetUSFM(book, chapter, verse)`.** If the verse USFM that Paratext returns doesn't start where `Offset` counts from (just before `\v`), every replacement is refused as "the verse has changed". The user should see the reason, and the rule is still saved. This can only be tested by hand. Task 6's manual step checks it, and the fallback, a partial-context comparison, is covered in Task 1.
2. **`IProject.ID` and the `Settings.xml` `<Guid>` may be formatted differently** (40 hex characters were seen in Settings.xml). If they don't match, nothing is recorded. The user should get a one-time message saying so, not silence. → Task 3 test `Find_GuidMismatch_ReturnsNull`, plus the warning path in Task 6.
3. **The user edits the Bad form in the dialog before clicking OK.** The rule is saved for the edited form, the fix is recorded, and Paratext is **not** changed, because the selected text is no longer the bad form. → Task 6 `ApplyFix` logic. Covered by the manual check; the logic is a single equality test.
4. **The same word appears twice in a verse.** Only the selected occurrence, at its offset, is replaced. → Task 1 test `TryReplaceInVerse_WordTwice_ReplacesOnlySelectedOccurrence`.
5. **`SpellingStatus.xml` entries the plugin didn't touch are written back exactly as they were.** Words in non-Latin scripts and entries with Correction children must not be reformatted or reordered. → Task 2 test `Save_KeepsUnrelatedEntriesIdentical`.

---

## File Structure

**Plugin, `src/SpellFixerPluginForParatext/`:**
- Create:
  - `SelectionReplacer.cs`: `SelectionInfo` plus validation, verse/chapter swap and context. Pure.
  - `SpellingStatusMerger.cs`: XML merge, load and save. Pure; shared with the helper.
  - `PendingSpellingStatusStore.cs`: the pending-fixes file. Pure; shared with the helper.
  - `ParatextProjectFolder.cs`: the projects directory from the registry, and folder lookup by ShortName and GUID.
  - `SpellingStatusRecorder.cs`: the permission and folder checks, then append to the pending file.
  - `PluginFindReplaceHelper.cs`: a `FindReplaceHelper` subclass that exposes the CSC and legacy objects.
  - `FixSpellingForm.cs` and `FixSpellingForm.Designer.cs`: the non-modal dialog.
- Modify:
  - `SpellFixerProject.cs`: use `PluginFindReplaceHelper`; add `AssignCorrectSpelling(bad, good, context)`, `GetSimilarWords` and `WordsInContext`.
  - `SpellFixerForm.cs` and `SpellFixerForm.Designer.cs`: selection tracking, the new button behaviour, OK handling and recording.
  - `SpellFixerQueryForm.cs`: record rules added with "Add Rule".
  - `SpellFixerPlugin.cs`: the `ShuttingDown` hook and launching the helper.
  - `SpellFixerPluginForParatext.csproj`: a build-order reference to the helper, and the post-build copy.

**Helper, `src/SpellingStatusUpdater/` (new):** `SpellingStatusUpdater.csproj`, `Program.cs`, `SpellingStatusUpdateRunner.cs`, `Properties/AssemblyInfo.cs`.

**Tests, `src/SILConvertersWordML/TestBwdc/`:** new `UnitTest_PtxSpellFixerSpelling.cs`, and changes to `TestBwdc.csproj`.

**Solution and installer:** `SEC VS2019.sln` and `Installer/ParatextBackTranslationHelperPlugin64bitMM/ParatextBackTranslationHelperPlugin_MergeModule.wxs`.

---

### Task 1: SelectionReplacer (pure)

**Files:**
- Create: `src/SpellFixerPluginForParatext/SelectionReplacer.cs`
- Create test: `src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixerSpelling.cs`
- Modify: `src/SILConvertersWordML/TestBwdc/TestBwdc.csproj` (Compile item)

**Interfaces:**
- Produces, in namespace `SIL.SpellFixerPluginForParatext`:
  - `public class SelectionInfo`, with properties `string SelectedText`, `string BeforeContext`, `string AfterContext`, `int Offset`, `IVerseRef VerseRefStart` and `IVerseRef VerseRefEnd`, and a factory `static SelectionInfo FromSelection(IScriptureTextSelection)`.
  - `public static class SelectionReplacer`, with:
    - `bool ValidateSelection(SelectionInfo s, out string word, out int wordOffset, out string reason)`
    - `bool TryReplaceInVerse(string currentVerseUsfm, SelectionInfo s, string bad, int badOffset, string good, out string newVerseUsfm, out string reason)`
    - `bool ReplaceVerseInChapter(string chapterUsfm, string oldVerseUsfm, string newVerseUsfm, out string newChapterUsfm)`
    - `string StripMarkers(string usfm)`
    - `string BuildContext(string before, string word, string after, int wordsEachSide)`
    - `string BuildContext(SelectionInfo s, string word, int wordOffset, int wordsEachSide)`

- [ ] **Step 1: Write the failing tests**

Create `UnitTest_PtxSpellFixerSpelling.cs`:

```csharp
extern alias SpellFixer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using SpellFixer::SIL.SpellFixerPluginForParatext;
using static TestBwdc.PtxTestTokens;

namespace TestBwdc
{
    [TestFixture]
    public class UnitTest_PtxSpellFixerSpelling
    {
        private static SelectionInfo Sel(string before, string selected, string after, int? offset = null)
        {
            return new SelectionInfo
            {
                BeforeContext = before,
                SelectedText = selected,
                AfterContext = after,
                Offset = offset ?? before.Length,
                VerseRefStart = Vref(5, 1),
                VerseRefEnd = Vref(5, 1),
            };
        }

        #region SelectionReplacer.ValidateSelection

        [Test]
        public void ValidateSelection_TrimsWhitespaceAndAdjustsOffset()
        {
            var s = Sel("\\v 1 the ", " teh ", "cat");
            Assert.IsTrue(SelectionReplacer.ValidateSelection(s, out string word, out int wordOffset, out _));
            Assert.AreEqual("teh", word);
            Assert.AreEqual(s.Offset + 1, wordOffset);
        }

        [Test]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("teh\\nd cat")]
        [TestCase("teh\r\ncat")]
        public void ValidateSelection_RejectsEmptyMarkersAndLineBreaks(string selected)
        {
            Assert.IsFalse(SelectionReplacer.ValidateSelection(Sel("\\v 1 ", selected, " end"), out _, out _, out string reason));
            Assert.IsFalse(string.IsNullOrEmpty(reason));
        }

        [Test]
        public void ValidateSelection_RejectsMultiVerseSelection()
        {
            var s = Sel("\\v 1 ", "teh", " end");
            s.VerseRefEnd = Vref(5, 2);
            Assert.IsFalse(SelectionReplacer.ValidateSelection(s, out _, out _, out _));
        }

        [Test]
        public void ValidateSelection_RejectsNull()
        {
            Assert.IsFalse(SelectionReplacer.ValidateSelection(null, out _, out _, out _));
        }

        #endregion

        #region SelectionReplacer.TryReplaceInVerse

        [Test]
        public void TryReplaceInVerse_UnchangedVerse_ReplacesAtOffset()
        {
            var s = Sel("\\v 1 the cat and ", "teh", " dog");
            var verse = s.BeforeContext + s.SelectedText + s.AfterContext;
            Assert.IsTrue(SelectionReplacer.TryReplaceInVerse(verse, s, "teh", s.Offset, "the", out string newVerse, out _));
            Assert.AreEqual("\\v 1 the cat and the dog", newVerse);
        }

        [Test]
        public void TryReplaceInVerse_WordTwice_ReplacesOnlySelectedOccurrence()
        {
            var s = Sel("\\v 1 teh cat and ", "teh", " dog");
            var verse = s.BeforeContext + s.SelectedText + s.AfterContext;
            Assert.IsTrue(SelectionReplacer.TryReplaceInVerse(verse, s, "teh", s.Offset, "the", out string newVerse, out _));
            Assert.AreEqual("\\v 1 teh cat and the dog", newVerse);
        }

        [Test]
        [TestCase("a")]
        [TestCase("a much longer replacement")]
        public void TryReplaceInVerse_ReplacementOfAnyLength(string good)
        {
            var s = Sel("\\v 1 x ", "teh", " y");
            var verse = s.BeforeContext + s.SelectedText + s.AfterContext;
            Assert.IsTrue(SelectionReplacer.TryReplaceInVerse(verse, s, "teh", s.Offset, good, out string newVerse, out _));
            Assert.AreEqual("\\v 1 x " + good + " y", newVerse);
        }

        [Test]
        public void TryReplaceInVerse_VerseChanged_IsRefused()
        {
            var s = Sel("\\v 1 the cat and ", "teh", " dog");
            Assert.IsFalse(SelectionReplacer.TryReplaceInVerse("\\v 1 the cat and teh dogs", s, "teh", s.Offset, "the", out _, out string reason));
            Assert.IsFalse(string.IsNullOrEmpty(reason));
        }

        [Test]
        public void TryReplaceInVerse_BadFormNotAtOffset_IsRefused()
        {
            var s = Sel("\\v 1 the cat and ", "teh", " dog");
            var verse = s.BeforeContext + s.SelectedText + s.AfterContext;
            Assert.IsFalse(SelectionReplacer.TryReplaceInVerse(verse, s, "cat", s.Offset, "the", out _, out _));
        }

        [Test]
        public void TryReplaceInVerse_PartialContext_ChecksNeighbourhood()
        {
            // BeforeContext only holds the tail of the text before the selection (its length != Offset)
            var verse = "\\v 1 a long beginning of the verse and teh dog";
            var offset = verse.IndexOf("teh", StringComparison.Ordinal);
            var s = Sel("verse and ", "teh", " dog", offset);

            Assert.IsTrue(SelectionReplacer.TryReplaceInVerse(verse, s, "teh", offset, "the", out string newVerse, out _));
            Assert.AreEqual("\\v 1 a long beginning of the verse and the dog", newVerse);

            var changedNearby = verse.Replace("verse and", "verse but");
            Assert.IsFalse(SelectionReplacer.TryReplaceInVerse(changedNearby, s, "teh", offset, "the", out _, out _));
        }

        #endregion

        #region SelectionReplacer.ReplaceVerseInChapter

        [Test]
        public void ReplaceVerseInChapter_ReplacesTheSingleOccurrence()
        {
            var chapter = "\\c 5\r\n\\p\r\n\\v 1 one\r\n\\v 2 teh two\r\n";
            Assert.IsTrue(SelectionReplacer.ReplaceVerseInChapter(chapter, "\\v 2 teh two\r\n", "\\v 2 the two\r\n", out string newChapter));
            Assert.AreEqual("\\c 5\r\n\\p\r\n\\v 1 one\r\n\\v 2 the two\r\n", newChapter);
        }

        [Test]
        public void ReplaceVerseInChapter_MissingOrRepeated_IsRefused()
        {
            Assert.IsFalse(SelectionReplacer.ReplaceVerseInChapter("\\v 1 one", "\\v 2 two", "x", out _));
            Assert.IsFalse(SelectionReplacer.ReplaceVerseInChapter("ab ab", "ab", "x", out _));
        }

        #endregion

        #region SelectionReplacer.BuildContext

        [Test]
        public void BuildContext_TakesWordsEachSide()
        {
            Assert.AreEqual("two three teh four five",
                SelectionReplacer.BuildContext("\\v 3 one two three ", "teh", " four five six", 2));
        }

        [Test]
        public void BuildContext_StripsMarkers()
        {
            Assert.AreEqual("a b c teh",
                SelectionReplacer.BuildContext("\\p \\v 1 a \\nd b\\nd* c ", "teh", "", 3));
        }

        [Test]
        public void BuildContext_WordAtStartOrFewerWords()
        {
            Assert.AreEqual("Teh cat sat", SelectionReplacer.BuildContext("\\v 1 ", "Teh", " cat sat on", 2));
            Assert.AreEqual("one teh", SelectionReplacer.BuildContext("one ", "teh", "", 3));
        }

        [Test]
        public void BuildContext_FromSelection_UsesTrimmedWord()
        {
            var s = Sel("\\v 1 the cat ", " teh ", "dog ran");
            SelectionReplacer.ValidateSelection(s, out string word, out int wordOffset, out _);
            Assert.AreEqual("the cat teh dog ran", SelectionReplacer.BuildContext(s, word, wordOffset, 2));
        }

        #endregion
    }
}
```

Add to `TestBwdc.csproj`, after the `UnitTest_PtxSpellFixer.cs` Compile item:

```xml
    <Compile Include="UnitTest_PtxSpellFixerSpelling.cs" Condition="'$(Platform)' == 'x64'" />
```

- [ ] **Step 2: Build to confirm it fails**

Run the TestBwdc build command. Expected: `CS0246` errors, because `SelectionInfo` and `SelectionReplacer` don't exist yet.

- [ ] **Step 3: Implement `SelectionReplacer.cs`**

```csharp
using Paratext.PluginInterfaces;
using System;
using System.Text.RegularExpressions;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// A copy of the (relevant) data of an IScriptureTextSelection (so it can be kept and tested)
    /// </summary>
    public class SelectionInfo
    {
        public string SelectedText { get; set; }
        public string BeforeContext { get; set; }
        public string AfterContext { get; set; }
        public int Offset { get; set; }
        public IVerseRef VerseRefStart { get; set; }
        public IVerseRef VerseRefEnd { get; set; }

        public static SelectionInfo FromSelection(IScriptureTextSelection selection)
        {
            return (selection == null)
                    ? null
                    : new SelectionInfo
                    {
                        SelectedText = selection.SelectedText,
                        BeforeContext = selection.BeforeContext,
                        AfterContext = selection.AfterContext,
                        Offset = selection.Offset,
                        VerseRefStart = selection.VerseRefStart,
                        VerseRefEnd = selection.VerseRefEnd,
                    };
        }
    }

    /// <summary>
    /// The (Paratext UI-independent) bits of using a Paratext text selection as the 'bad' word: validating it,
    /// replacing it in the verse/chapter USFM, and building the context string for the CscProject
    /// </summary>
    public static class SelectionReplacer
    {
        private const int NeighbourhoodLength = 20;

        // \v 3 and \c 5 (with their numbers), and then any other start (\nd ) or end (\nd*) marker
        private static readonly Regex VerseOrChapterMarker = new Regex(@"\\(?:v|c)\s+\S+\s?");
        private static readonly Regex OtherMarker = new Regex(@"\\[^\s\\*]+(?:\*|\s?)");
        private static readonly Regex Whitespace = new Regex(@"\s+");

        public static bool ValidateSelection(SelectionInfo s, out string word, out int wordOffset, out string reason)
        {
            word = null;
            wordOffset = -1;
            reason = null;

            var text = s?.SelectedText ?? String.Empty;
            var trimmed = text.Trim();
            if (trimmed.Length == 0)
                reason = "nothing is selected";
            else if (trimmed.IndexOf('\\') >= 0)
                reason = "the selection includes a USFM marker";
            else if (trimmed.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                reason = "the selection spans more than one line";
            else if (!IsSameVerse(s.VerseRefStart, s.VerseRefEnd))
                reason = "the selection spans more than one verse";

            if (reason != null)
                return false;

            word = trimmed;
            wordOffset = s.Offset + (text.Length - text.TrimStart().Length);
            return true;
        }

        private static bool IsSameVerse(IVerseRef start, IVerseRef end)
        {
            return (end == null) ||
                   ((start != null) && (start.BookNum == end.BookNum) && (start.ChapterNum == end.ChapterNum) && (start.VerseNum == end.VerseNum));
        }

        /// <summary>
        /// Replaces 'bad' (at badOffset) with 'good' in the verse's current USFM, but only if the verse still looks
        /// the way it did when the selection was made
        /// </summary>
        public static bool TryReplaceInVerse(string currentVerseUsfm, SelectionInfo s, string bad, int badOffset, string good,
                                             out string newVerseUsfm, out string reason)
        {
            newVerseUsfm = null;
            reason = null;
            currentVerseUsfm ??= String.Empty;
            var before = s.BeforeContext ?? String.Empty;
            var selected = s.SelectedText ?? String.Empty;
            var after = s.AfterContext ?? String.Empty;

            if (before.Length == s.Offset)
            {
                if (currentVerseUsfm != before + selected + after)
                    reason = "the verse has changed since the word was selected";
            }
            else if (!NeighbourhoodMatches(currentVerseUsfm, s.Offset, before, selected, after))
            {
                reason = "the text around the selection has changed since the word was selected";
            }

            if ((reason == null) &&
                ((badOffset < 0) || (badOffset + bad.Length > currentVerseUsfm.Length) ||
                 (String.CompareOrdinal(currentVerseUsfm, badOffset, bad, 0, bad.Length) != 0)))
            {
                reason = $"'{bad}' is no longer where it was selected";
            }

            if (reason != null)
                return false;

            newVerseUsfm = currentVerseUsfm.Substring(0, badOffset) + good + currentVerseUsfm.Substring(badOffset + bad.Length);
            return true;
        }

        // when the contexts are partial, compare the selection itself and up to 20 characters on either side of it
        private static bool NeighbourhoodMatches(string verse, int offset, string before, string selected, string after)
        {
            var selectionEnd = offset + selected.Length;
            if ((offset < 0) || (selectionEnd > verse.Length) || (String.CompareOrdinal(verse, offset, selected, 0, selected.Length) != 0))
                return false;

            var k = Math.Min(NeighbourhoodLength, Math.Min(before.Length, offset));
            if (String.CompareOrdinal(verse, offset - k, before, before.Length - k, k) != 0)
                return false;

            var j = Math.Min(NeighbourhoodLength, Math.Min(after.Length, verse.Length - selectionEnd));
            return String.CompareOrdinal(verse, selectionEnd, after, 0, j) == 0;
        }

        /// <summary>
        /// Puts the new verse USFM into the chapter USFM (in place of the old one, which must occur exactly once)
        /// </summary>
        public static bool ReplaceVerseInChapter(string chapterUsfm, string oldVerseUsfm, string newVerseUsfm, out string newChapterUsfm)
        {
            newChapterUsfm = null;
            if (String.IsNullOrEmpty(chapterUsfm) || String.IsNullOrEmpty(oldVerseUsfm))
                return false;

            var index = chapterUsfm.IndexOf(oldVerseUsfm, StringComparison.Ordinal);
            if ((index < 0) || (chapterUsfm.LastIndexOf(oldVerseUsfm, StringComparison.Ordinal) != index))
                return false;

            newChapterUsfm = chapterUsfm.Substring(0, index) + newVerseUsfm + chapterUsfm.Substring(index + oldVerseUsfm.Length);
            return true;
        }

        public static string StripMarkers(string usfm)
        {
            return OtherMarker.Replace(VerseOrChapterMarker.Replace(usfm ?? String.Empty, " "), String.Empty);
        }

        /// <summary>
        /// The word with up to wordsEachSide words before and after it (from the marker-less text), like the
        /// context strings BulkSFMConverter gives the CscProject
        /// </summary>
        public static string BuildContext(string before, string word, string after, int wordsEachSide)
        {
            var left = StripMarkers(before);
            var full = left + word + StripMarkers(after);
            var start = MoveLeftOverWords(full, left.Length, wordsEachSide);
            var end = MoveRightOverWords(full, left.Length + word.Length, wordsEachSide);
            return Whitespace.Replace(full.Substring(start, end - start), " ").Trim();
        }

        public static string BuildContext(SelectionInfo s, string word, int wordOffset, int wordsEachSide)
        {
            var verse = (s.BeforeContext ?? String.Empty) + (s.SelectedText ?? String.Empty) + (s.AfterContext ?? String.Empty);
            var wordIndex = (s.BeforeContext ?? String.Empty).Length + (wordOffset - s.Offset);
            return BuildContext(verse.Substring(0, wordIndex), word, verse.Substring(wordIndex + word.Length), wordsEachSide);
        }

        private static int MoveLeftOverWords(string s, int i, int count)
        {
            while ((i > 0) && !Char.IsWhiteSpace(s[i - 1]))  // anything glued to the front of the word is part of it
                i--;
            for (var n = 0; (n < count) && (i > 0); n++)
            {
                while ((i > 0) && Char.IsWhiteSpace(s[i - 1]))
                    i--;
                while ((i > 0) && !Char.IsWhiteSpace(s[i - 1]))
                    i--;
            }
            return i;
        }

        private static int MoveRightOverWords(string s, int i, int count)
        {
            while ((i < s.Length) && !Char.IsWhiteSpace(s[i]))   // anything glued to the end of the word is part of it
                i++;
            for (var n = 0; (n < count) && (i < s.Length); n++)
            {
                while ((i < s.Length) && Char.IsWhiteSpace(s[i]))
                    i++;
                while ((i < s.Length) && !Char.IsWhiteSpace(s[i]))
                    i++;
            }
            return i;
        }
    }
}
```

- [ ] **Step 4: Build and run the tests**

Filter: `FullyQualifiedName~UnitTest_PtxSpellFixerSpelling`. Expected: all pass.

- [ ] **Step 5: Commit**

```powershell
git add src/SpellFixerPluginForParatext/SelectionReplacer.cs src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixerSpelling.cs src/SILConvertersWordML/TestBwdc/TestBwdc.csproj
git commit -m "feat(SpellFixerPluginForParatext): SelectionReplacer to validate/replace a Paratext selection and build its context`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: SpellingStatusMerger + PendingSpellingStatusStore (pure, shared)

**Files:**
- Create: `src/SpellFixerPluginForParatext/SpellingStatusMerger.cs`
- Create: `src/SpellFixerPluginForParatext/PendingSpellingStatusStore.cs`
- Modify test: `src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixerSpelling.cs`

**Interfaces:**
- Produces, in namespace `SIL.SpellFixerPluginForParatext` (all public; these files are also linked into the helper in Task 7):
  - `public class PendingFix { string Bad; string Good; DateTime WhenUtc; }` (properties).
  - `public static class PendingSpellingStatusStore`, with:
    - constants `FileName = "PendingSpellingStatus.xml"` and `SubFolder = @"local\SpellFixer"`
    - `string FolderFor(string projectFolder)`
    - `string PathFor(string projectFolder)`
    - `List<PendingFix> Load(string path)`, which returns an empty list if the file is missing
    - `void Append(string path, string bad, string good, DateTime whenUtc)`
    - `void Save(string path, IEnumerable<PendingFix> fixes)`, which deletes the file when the list is empty
  - `public static class SpellingStatusMerger`, with:
    - `XDocument LoadOrCreate(string path)`
    - `void Apply(XDocument doc, string bad, string good)`
    - `void Save(XDocument doc, string path)`
  - Both stores write atomically: they write `path + ".tmp"`, then use `File.Replace` or `File.Move`.

- [ ] **Step 1: Write the failing tests**

Append inside the `UnitTest_PtxSpellFixerSpelling` class:

```csharp
        #region SpellingStatusMerger / PendingSpellingStatusStore

        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SpellFixerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private static XElement StatusOf(XDocument doc, string word) =>
            doc.Root.Elements("Status").Single(e => (string)e.Attribute("Word") == word);

        [Test]
        public void Apply_NewWords_AddsWrongWithCorrectionAndRight()
        {
            var doc = new XDocument(new XElement("SpellingStatus"));
            SpellingStatusMerger.Apply(doc, "teh", "the");

            Assert.AreEqual("W", (string)StatusOf(doc, "teh").Attribute("State"));
            Assert.AreEqual("the", StatusOf(doc, "teh").Element("Correction").Value);
            Assert.AreEqual("R", (string)StatusOf(doc, "the").Attribute("State"));
            Assert.IsNull(StatusOf(doc, "the").Element("Correction"));
        }

        [Test]
        public void Apply_ExistingEntries_AreUpdatedInPlace()
        {
            var doc = XDocument.Parse(
                "<SpellingStatus>" +
                "<Status Word=\"teh\" State=\"R\" />" +
                "<Status Word=\"the\" State=\"W\"><Correction>thee</Correction></Status>" +
                "<Status Word=\"adn\" State=\"W\"><Correction>an</Correction></Status>" +
                "</SpellingStatus>");

            SpellingStatusMerger.Apply(doc, "teh", "the");
            SpellingStatusMerger.Apply(doc, "adn", "and");

            Assert.AreEqual(4, doc.Root.Elements("Status").Count(), "teh, the, adn + and (new), with no duplicates");
            Assert.AreEqual("W", (string)StatusOf(doc, "teh").Attribute("State"));
            Assert.AreEqual(1, StatusOf(doc, "teh").Elements("Correction").Count());
            Assert.AreEqual("R", (string)StatusOf(doc, "the").Attribute("State"));
            Assert.IsNull(StatusOf(doc, "the").Element("Correction"));
            Assert.AreEqual("and", StatusOf(doc, "adn").Element("Correction").Value);
            CollectionAssert.AreEqual(new[] { "teh", "the", "adn", "and" },
                                      doc.Root.Elements("Status").Select(e => (string)e.Attribute("Word")).Distinct().ToList());
        }

        [Test]
        public void Apply_LaterFixWins()
        {
            var doc = new XDocument(new XElement("SpellingStatus"));
            SpellingStatusMerger.Apply(doc, "colour", "color");
            SpellingStatusMerger.Apply(doc, "color", "colour");

            Assert.AreEqual("R", (string)StatusOf(doc, "colour").Attribute("State"));
            Assert.AreEqual("W", (string)StatusOf(doc, "color").Attribute("State"));
            Assert.AreEqual("colour", StatusOf(doc, "color").Element("Correction").Value);
        }

        [Test]
        public void Save_KeepsUnrelatedEntriesIdentical()
        {
            var original =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<SpellingStatus>\r\n" +
                "  <Status Word=\"किताब\" State=\"R\" />\r\n" +
                "  <Status Word=\"कीताब\" State=\"W\">\r\n" +
                "    <Correction>किताब</Correction>\r\n" +
                "  </Status>\r\n" +
                "</SpellingStatus>";
            var path = Path.Combine(_tempDir, "SpellingStatus.xml");
            File.WriteAllText(path, original, new System.Text.UTF8Encoding(true));

            var doc = SpellingStatusMerger.LoadOrCreate(path);
            SpellingStatusMerger.Apply(doc, "teh", "the");
            SpellingStatusMerger.Save(doc, path);

            var bytes = File.ReadAllBytes(path);
            CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray(), "UTF-8 BOM");
            var saved = File.ReadAllText(path);
            StringAssert.StartsWith(original.Substring(0, original.IndexOf("</SpellingStatus>", StringComparison.Ordinal)), saved);
            StringAssert.Contains("  <Status Word=\"teh\" State=\"W\">\r\n    <Correction>the</Correction>\r\n  </Status>\r\n", saved);
            StringAssert.Contains("  <Status Word=\"the\" State=\"R\" />\r\n", saved);
        }

        [Test]
        public void LoadOrCreate_MissingFile_GivesEmptyRoot()
        {
            var doc = SpellingStatusMerger.LoadOrCreate(Path.Combine(_tempDir, "nope.xml"));
            Assert.AreEqual("SpellingStatus", doc.Root.Name.LocalName);
            Assert.IsEmpty(doc.Root.Elements());
        }

        [Test]
        public void PendingStore_AppendLoadAndClear()
        {
            var projectFolder = Path.Combine(_tempDir, "Dog");
            var path = PendingSpellingStatusStore.PathFor(projectFolder);
            StringAssert.EndsWith(@"Dog\local\SpellFixer\PendingSpellingStatus.xml", path);

            CollectionAssert.IsEmpty(PendingSpellingStatusStore.Load(path));

            var when = new DateTime(2026, 10, 8, 14, 3, 0, DateTimeKind.Utc);
            PendingSpellingStatusStore.Append(path, "teh", "the", when);
            PendingSpellingStatusStore.Append(path, "adn", "and", when.AddMinutes(1));

            var fixes = PendingSpellingStatusStore.Load(path);
            CollectionAssert.AreEqual(new[] { "teh", "adn" }, fixes.Select(f => f.Bad).ToList());
            CollectionAssert.AreEqual(new[] { "the", "and" }, fixes.Select(f => f.Good).ToList());
            Assert.AreEqual(when, fixes[0].WhenUtc);

            PendingSpellingStatusStore.Save(path, fixes.Skip(1));
            Assert.AreEqual(1, PendingSpellingStatusStore.Load(path).Count);

            PendingSpellingStatusStore.Save(path, Enumerable.Empty<PendingFix>());
            Assert.IsFalse(File.Exists(path));
        }

        #endregion
```

- [ ] **Step 2: Build to confirm it fails**

Expected: `CS0103` and `CS0246` errors for `SpellingStatusMerger`, `PendingSpellingStatusStore` and `PendingFix`.

- [ ] **Step 3: Implement `SpellingStatusMerger.cs`**

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Merges SpellFixer fixes into a Paratext project's SpellingStatus.xml (also linked into SpellingStatusUpdater.exe)
    ///   <SpellingStatus>
    ///     <Status Word="the" State="R" />
    ///     <Status Word="teh" State="W"><Correction>the</Correction></Status>
    ///   </SpellingStatus>
    /// </summary>
    public static class SpellingStatusMerger
    {
        private const string RootName = "SpellingStatus";
        private const string StatusName = "Status";
        private const string CorrectionName = "Correction";

        public static XDocument LoadOrCreate(string path)
        {
            var doc = File.Exists(path)
                        ? XDocument.Load(path)
                        : new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(RootName));
            if (doc.Root == null)
                doc.Add(new XElement(RootName));
            return doc;
        }

        /// <summary>
        /// 'bad' becomes a wrong word with 'good' as its correction, and 'good' becomes a right word
        /// </summary>
        public static void Apply(XDocument doc, string bad, string good)
        {
            SetStatus(doc.Root, bad, "W", good);
            SetStatus(doc.Root, good, "R", null);
        }

        private static void SetStatus(XElement root, string word, string state, string correction)
        {
            var status = root.Elements(StatusName).FirstOrDefault(e => String.Equals((string)e.Attribute("Word"), word, StringComparison.Ordinal));
            if (status == null)
            {
                status = new XElement(StatusName, new XAttribute("Word", word));
                root.Add(status);
            }

            status.SetAttributeValue("State", state);
            status.Elements(CorrectionName).Remove();
            if (correction != null)
                status.Add(new XElement(CorrectionName, correction));
        }

        /// <summary>
        /// Saves the way Paratext does (UTF-8 w/ BOM, 2-space indent, CRLF), via a temp file
        /// </summary>
        public static void Save(XDocument doc, string path)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(true),
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace,
            };

            AtomicFile.Write(path, tempPath =>
            {
                using (var writer = XmlWriter.Create(tempPath, settings))
                    doc.Save(writer);
            });
        }
    }

    /// <summary>
    /// Writes a file by writing a temp file and then replacing the original with it
    /// </summary>
    public static class AtomicFile
    {
        public static void Write(string path, Action<string> writeTempFile)
        {
            var folder = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var tempPath = path + ".tmp";
            writeTempFile(tempPath);
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
    }
}
```

- [ ] **Step 4: Implement `PendingSpellingStatusStore.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SIL.SpellFixerPluginForParatext
{
    public class PendingFix
    {
        public string Bad { get; set; }
        public string Good { get; set; }
        public DateTime WhenUtc { get; set; }
    }

    /// <summary>
    /// The fixes made in the SpellFixer plugin that still need to go into the project's SpellingStatus.xml (after
    /// Paratext exits). Kept in &lt;projectFolder&gt;\local\SpellFixer (which isn't Send/Received). Also linked into
    /// SpellingStatusUpdater.exe
    ///   <PendingSpellingStatus>
    ///     <Fix Bad="teh" Good="the" When="2026-10-08T14:03:00Z" />
    ///   </PendingSpellingStatus>
    /// </summary>
    public static class PendingSpellingStatusStore
    {
        public const string FileName = "PendingSpellingStatus.xml";
        public const string SubFolder = @"local\SpellFixer";

        public static string FolderFor(string projectFolder) => Path.Combine(projectFolder, SubFolder);

        public static string PathFor(string projectFolder) => Path.Combine(FolderFor(projectFolder), FileName);

        public static List<PendingFix> Load(string path)
        {
            if (!File.Exists(path))
                return new List<PendingFix>();

            return XDocument.Load(path).Root?
                            .Elements("Fix")
                            .Select(e => new PendingFix
                            {
                                Bad = (string)e.Attribute("Bad"),
                                Good = (string)e.Attribute("Good"),
                                WhenUtc = DateTime.Parse((string)e.Attribute("When") ?? "2000-01-01T00:00:00Z", CultureInfo.InvariantCulture,
                                                         DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
                            })
                            .Where(f => !String.IsNullOrEmpty(f.Bad) && (f.Good != null))
                            .ToList()
                   ?? new List<PendingFix>();
        }

        public static void Append(string path, string bad, string good, DateTime whenUtc)
        {
            var fixes = Load(path);
            fixes.Add(new PendingFix { Bad = bad, Good = good, WhenUtc = whenUtc });
            Save(path, fixes);
        }

        public static void Save(string path, IEnumerable<PendingFix> fixes)
        {
            var list = fixes.ToList();
            if (!list.Any())
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            var doc = new XDocument(new XElement("PendingSpellingStatus",
                list.Select(f => new XElement("Fix",
                                              new XAttribute("Bad", f.Bad),
                                              new XAttribute("Good", f.Good),
                                              new XAttribute("When", f.WhenUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))))));
            AtomicFile.Write(path, tempPath => doc.Save(tempPath));
        }
    }
}
```

- [ ] **Step 5: Build and run the tests**

Filter: `FullyQualifiedName~UnitTest_PtxSpellFixerSpelling`. Expected: all pass.

If `Save_KeepsUnrelatedEntriesIdentical` fails only because XmlWriter writes the declaration differently (e.g. `encoding="utf-8"` vs `"UTF-8"`), compare against the real `C:\My Paratext 9 Projects\Dog\SpellingStatus.xml` header bytes. Make the writer output whatever Paratext writes, by writing the declaration manually if necessary. Record the decision as a ruling.

- [ ] **Step 6: Commit**

```powershell
git add src/SpellFixerPluginForParatext/SpellingStatusMerger.cs src/SpellFixerPluginForParatext/PendingSpellingStatusStore.cs src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixerSpelling.cs
git commit -m "feat(SpellFixerPluginForParatext): SpellingStatus.xml merger and pending-fixes store`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: ParatextProjectFolder + SpellingStatusRecorder

**Files:**
- Create: `src/SpellFixerPluginForParatext/ParatextProjectFolder.cs`
- Create: `src/SpellFixerPluginForParatext/SpellingStatusRecorder.cs`
- Modify test: `src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixerSpelling.cs`

**Interfaces:**
- Consumes: `PendingSpellingStatusStore` (Task 2).
- Produces:
  - `public static class ParatextProjectFolder`, with `string GetProjectsDirectory()` (from the registry; null if not found) and `string Find(string projectsDirectory, string shortName, string projectId)` (null if not found or if the GUID doesn't match).
  - `internal static class SpellingStatusRecorder`, with:
    - `bool HasRecordedThisSession { get; }`
    - `void Record(IProject project, IPluginObject plugin, string bad, string good, Action<string> warnUser)`. Each kind of warning is shown at most once per project per session.

- [ ] **Step 1: Write the failing tests**

Append inside the test class:

```csharp
        #region ParatextProjectFolder

        private string MakeProject(string shortName, string guid)
        {
            var folder = Path.Combine(_tempDir, shortName);
            Directory.CreateDirectory(folder);
            var settings = (guid == null)
                            ? "<ScriptureText><Name>" + shortName + "</Name></ScriptureText>"
                            : "<ScriptureText><Guid>" + guid + "</Guid><Name>" + shortName + "</Name></ScriptureText>";
            File.WriteAllText(Path.Combine(folder, "Settings.xml"), settings);
            return folder;
        }

        [Test]
        public void Find_MatchingGuid_ReturnsFolder()
        {
            var folder = MakeProject("Dog", "580567e691300a47f556b191275ab1791777ef21");
            Assert.AreEqual(folder, ParatextProjectFolder.Find(_tempDir, "Dog", "580567E691300A47F556B191275AB1791777EF21"));
        }

        [Test]
        public void Find_GuidMismatch_ReturnsNull()
        {
            MakeProject("Dog", "580567e691300a47f556b191275ab1791777ef21");
            Assert.IsNull(ParatextProjectFolder.Find(_tempDir, "Dog", "0000000000000000000000000000000000000000"));
        }

        [Test]
        public void Find_NoGuidInSettings_AcceptsFolder()
        {
            var folder = MakeProject("Dog", null);
            Assert.AreEqual(folder, ParatextProjectFolder.Find(_tempDir, "Dog", "anything"));
        }

        [Test]
        public void Find_MissingProjectOrDirectory_ReturnsNull()
        {
            Assert.IsNull(ParatextProjectFolder.Find(_tempDir, "Cat", "x"));
            Assert.IsNull(ParatextProjectFolder.Find(null, "Dog", "x"));
        }

        #endregion
```

- [ ] **Step 2: Build to confirm it fails**

Expected: `CS0103` errors for `ParatextProjectFolder`.

- [ ] **Step 3: Implement `ParatextProjectFolder.cs`**

```csharp
using Microsoft.Win32;
using System;
using System.IO;
using System.Xml.Linq;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Finds a Paratext project's folder (e.g. C:\My Paratext 9 Projects\Dog), which the plugin API doesn't expose
    /// </summary>
    public static class ParatextProjectFolder
    {
        private static readonly string[] SettingsKeys = { @"SOFTWARE\WOW6432Node\Paratext\8", @"SOFTWARE\Paratext\8" };

        public static string GetProjectsDirectory()
        {
            foreach (var keyName in SettingsKeys)
            {
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyName))
                    {
                        if ((key?.GetValue("Settings_Directory") is string directory) && Directory.Exists(directory))
                            return directory;
                    }
                }
                catch
                {
                    // try the next one
                }
            }
            return null;
        }

        /// <summary>
        /// Returns projectsDirectory\shortName if it has a Settings.xml whose Guid (if any) is the project's ID
        /// </summary>
        public static string Find(string projectsDirectory, string shortName, string projectId)
        {
            if (String.IsNullOrEmpty(projectsDirectory) || String.IsNullOrEmpty(shortName))
                return null;

            var folder = Path.Combine(projectsDirectory, shortName);
            var settingsPath = Path.Combine(folder, "Settings.xml");
            if (!File.Exists(settingsPath))
                return null;

            try
            {
                var guid = XDocument.Load(settingsPath).Root?.Element("Guid")?.Value?.Trim();
                if (!String.IsNullOrEmpty(guid) && !String.Equals(guid, projectId?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return null;
            }
            catch
            {
                return null;
            }

            return folder;
        }
    }
}
```

- [ ] **Step 4: Implement `SpellingStatusRecorder.cs`**

```csharp
using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Records the spelling fixes made in the plugin in the project's pending file, so SpellingStatusUpdater.exe
    /// can put them into SpellingStatus.xml after Paratext exits (there's no plugin API to change spelling status)
    /// </summary>
    internal static class SpellingStatusRecorder
    {
        private static readonly HashSet<string> _warningsShown = new HashSet<string>();

        public static bool HasRecordedThisSession { get; private set; }

        public static void Record(IProject project, IPluginObject plugin, string bad, string good, Action<string> warnUser)
        {
            try
            {
                if (!project.CanEdit(plugin, DataType.SpellingStatus))
                {
                    WarnOnce(project, "permission", $"You don't have permission to change the spelling status of the {project.ShortName} project, so the fixes made here won't be added to Paratext's spelling list.", warnUser);
                    return;
                }

                var projectFolder = ParatextProjectFolder.Find(ParatextProjectFolder.GetProjectsDirectory(), project.ShortName, project.ID);
                if (projectFolder == null)
                {
                    WarnOnce(project, "folder", $"Couldn't find the folder of the {project.ShortName} project, so the fixes made here won't be added to Paratext's spelling list.", warnUser);
                    return;
                }

                PendingSpellingStatusStore.Append(PendingSpellingStatusStore.PathFor(projectFolder), bad, good, DateTime.UtcNow);
                HasRecordedThisSession = true;
            }
            catch (Exception ex)
            {
                WarnOnce(project, "error", $"Unable to record the fix for Paratext's spelling list: {ex.Message}", warnUser);
            }
        }

        private static void WarnOnce(IProject project, string kind, string message, Action<string> warnUser)
        {
            if (_warningsShown.Add($"{project.ShortName}|{kind}"))
                warnUser?.Invoke(message);
        }
    }
}
```

- [ ] **Step 5: Build and run the tests**

Filter: `FullyQualifiedName~UnitTest_PtxSpellFixerSpelling`. Expected: all pass.

- [ ] **Step 6: Commit**

```powershell
git add src/SpellFixerPluginForParatext/ParatextProjectFolder.cs src/SpellFixerPluginForParatext/SpellingStatusRecorder.cs src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixerSpelling.cs
git commit -m "feat(SpellFixerPluginForParatext): find the Paratext project folder and record fixes for SpellingStatus`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: PluginFindReplaceHelper + SpellFixerProject additions

**Files:**
- Create: `src/SpellFixerPluginForParatext/PluginFindReplaceHelper.cs`
- Modify: `src/SpellFixerPluginForParatext/SpellFixerProject.cs`

**Interfaces:**
- Produces:
  - `internal class PluginFindReplaceHelper : FindReplaceHelper`, with `CscProject CscProject`, `SpellingFixer LegacySpellingFixer` and `bool HasProject`.
  - `public class SimilarWord { string Word; int Count; ToString() => "Word (Count)" }`.
  - On `SpellFixerProject`:
    - `void AssignCorrectSpelling(string bad, string good, List<string> context)`, which throws on failure and reloads the converter.
    - `List<SimilarWord> GetSimilarWords(string word)`, which is empty for legacy projects or when there are no matches.
    - `int WordsInContext`, which is 0 for legacy projects.
  - Existing members are unchanged.

- [ ] **Step 1: Create `PluginFindReplaceHelper.cs`**

```csharp
using BackTranslationHelper;
using SpellingFixer30;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Gives the plugin access to the CscProject/SpellingFixer that FindReplaceHelper loaded (so we can add rules
    /// without its modal UI, and get the similar words), while keeping a single in-memory copy of the project
    /// </summary>
    internal class PluginFindReplaceHelper : FindReplaceHelper
    {
        public PluginFindReplaceHelper()
        {
        }

        public PluginFindReplaceHelper(string projectName)
            : base(projectName)
        {
        }

        public CscProject CscProject => IsCscProject ? m_cscProject : null;

        public SpellingFixer LegacySpellingFixer => IsSpellFixerLegacyProject ? m_aSpellFixerLegacy : null;

        public bool HasProject => IsSpellFixerProject;
    }
}
```

- [ ] **Step 2: Update `SpellFixerProject.cs`**

Make these changes:
- The field `private readonly FindReplaceHelper _findReplaceHelper;` becomes `private readonly PluginFindReplaceHelper _findReplaceHelper;`, and the private ctor parameter becomes the same type.
- Replace `QueryUser` and `FromConverterName` with:

```csharp
        public static SpellFixerProject QueryUser()
        {
            // same as FindReplaceHelper.GetFindReplaceHelper, but with our subclass
            var findReplaceHelper = new PluginFindReplaceHelper();
            findReplaceHelper.QuerySpellFixProjectType();
            return findReplaceHelper.HasProject ? new SpellFixerProject(findReplaceHelper) : null;
        }

        public static SpellFixerProject FromConverterName(string converterName)
        {
            return new SpellFixerProject(new PluginFindReplaceHelper(converterName));
        }
```

- Add, after `EditSpellingFixes`:

```csharp
        public int WordsInContext => _findReplaceHelper.CscProject?.WordsInContext ?? 0;

        /// <summary>
        /// Adds the bad -> good rule without any UI (throws if it can't). For a CSC project, 'context' is stored
        /// with the good word (e.g. for the tooltips in its dialogs)
        /// </summary>
        public void AssignCorrectSpelling(string bad, string good, List<string> context)
        {
            var cscProject = _findReplaceHelper.CscProject;
            if (cscProject != null)
                cscProject.AssignCorrectSpelling(bad, good, bNoUI: true, context);
            else
                _findReplaceHelper.LegacySpellingFixer.AssignCorrectSpelling(bad, good);

            ReloadConverter();
        }

        /// <summary>
        /// The words the CscProject considers ambiguous with 'word' (its 'bundle'), most frequent first; empty for
        /// legacy projects (which don't have the notion)
        /// </summary>
        public List<SimilarWord> GetSimilarWords(string word)
        {
            var words = String.IsNullOrEmpty(word) ? null : _findReplaceHelper.CscProject?.GetAmbiguousWords(word);
            return words?.Select(w => new SimilarWord { Word = w.Value, Count = w.Count }).ToList()
                   ?? new List<SimilarWord>();
        }
```

- Add `using System;`, `using System.Collections.Generic;` and `using System.Linq;`, and append this class to the file, inside the namespace:

```csharp
    public class SimilarWord
    {
        public string Word { get; set; }
        public int Count { get; set; }
        public override string ToString() => $"{Word} ({Count})";
    }
```

- [ ] **Step 3: Build the plugin**

```powershell
$env:ParatextInstallDir=''; & $msbuild src\SpellFixerPluginForParatext\SpellFixerPluginForParatext.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
```
Expected: 0 errors.
- If `bNoUI:` named-argument binding fails because the parameter name differs, pass the arguments positionally: `(bad, good, true, context)`.

- [ ] **Step 4: Run the existing spell-fixer tests (regression)**

Filter: `FullyQualifiedName~UnitTest_PtxSpellFixer`. Expected: all pass. This filter also matches `UnitTest_PtxSpellFixerSpelling`.

- [ ] **Step 5: Commit**

```powershell
git add src/SpellFixerPluginForParatext/PluginFindReplaceHelper.cs src/SpellFixerPluginForParatext/SpellFixerProject.cs
git commit -m "feat(SpellFixerPluginForParatext): direct (no UI) AssignCorrectSpelling with context and similar words via FindReplaceHelper subclass`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: FixSpellingForm (non-modal dialog)

**Files:**
- Create: `src/SpellFixerPluginForParatext/FixSpellingForm.cs`
- Create: `src/SpellFixerPluginForParatext/FixSpellingForm.Designer.cs`

**Interfaces:**
- Consumes: `SpellFixerProject.Convert`, `GetSimilarWords` and `SimilarWord` (Task 4); `IKeyboard`.
- Produces: `internal partial class FixSpellingForm : Form`, with:
  - ctor `(SpellFixerProject project, Font font, bool rightToLeft, IKeyboard vernacularKeyboard, IKeyboard defaultKeyboard, Func<string, string, bool> applyFix)`. `applyFix(bad, good)` returns true to close the dialog.
  - `void LoadWord(string badForm, string verseReference)`.

- [ ] **Step 1: `FixSpellingForm.Designer.cs`**

```csharp
namespace SIL.SpellFixerPluginForParatext
{
    partial class FixSpellingForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.labelVerseCaption = new System.Windows.Forms.Label();
            this.labelVerse = new System.Windows.Forms.Label();
            this.labelSimilarWords = new System.Windows.Forms.Label();
            this.labelBadForm = new System.Windows.Forms.Label();
            this.textBoxBadForm = new SilEncConverters40.EcTextBox();
            this.listBoxSimilarWords = new System.Windows.Forms.ListBox();
            this.labelReplacement = new System.Windows.Forms.Label();
            this.textBoxReplacement = new SilEncConverters40.EcTextBox();
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOk = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel.SuspendLayout();
            this.flowLayoutPanelButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanel
            //
            this.tableLayoutPanel.ColumnCount = 3;
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanel.Controls.Add(this.labelVerseCaption, 0, 0);
            this.tableLayoutPanel.Controls.Add(this.labelVerse, 1, 0);
            this.tableLayoutPanel.Controls.Add(this.labelSimilarWords, 2, 0);
            this.tableLayoutPanel.Controls.Add(this.labelBadForm, 0, 1);
            this.tableLayoutPanel.Controls.Add(this.textBoxBadForm, 1, 1);
            this.tableLayoutPanel.Controls.Add(this.listBoxSimilarWords, 2, 1);
            this.tableLayoutPanel.Controls.Add(this.labelReplacement, 0, 2);
            this.tableLayoutPanel.Controls.Add(this.textBoxReplacement, 1, 2);
            this.tableLayoutPanel.Controls.Add(this.flowLayoutPanelButtons, 0, 3);
            this.tableLayoutPanel.SetRowSpan(this.listBoxSimilarWords, 3);
            this.tableLayoutPanel.SetColumnSpan(this.flowLayoutPanelButtons, 2);
            this.tableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel.Name = "tableLayoutPanel";
            this.tableLayoutPanel.Padding = new System.Windows.Forms.Padding(6);
            this.tableLayoutPanel.RowCount = 4;
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            //
            // labels
            //
            this.labelVerseCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelVerseCaption.AutoSize = true;
            this.labelVerseCaption.Name = "labelVerseCaption";
            this.labelVerseCaption.Text = "Selected in:";
            this.labelVerse.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelVerse.AutoSize = true;
            this.labelVerse.Name = "labelVerse";
            this.labelSimilarWords.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelSimilarWords.AutoSize = true;
            this.labelSimilarWords.Name = "labelSimilarWords";
            this.labelSimilarWords.Text = "&Similar words:";
            this.labelBadForm.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelBadForm.AutoSize = true;
            this.labelBadForm.Name = "labelBadForm";
            this.labelBadForm.Text = "&Bad form:";
            this.labelReplacement.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelReplacement.AutoSize = true;
            this.labelReplacement.Name = "labelReplacement";
            this.labelReplacement.Text = "&Replacement:";
            //
            // textBoxBadForm
            //
            this.textBoxBadForm.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxBadForm.Name = "textBoxBadForm";
            this.toolTip.SetToolTip(this.textBoxBadForm, "The misspelled word (from the selection in Paratext)");
            this.textBoxBadForm.TextChanged += new System.EventHandler(this.TextBoxBadForm_TextChanged);
            this.textBoxBadForm.Enter += new System.EventHandler(this.TextBox_Enter);
            //
            // textBoxReplacement
            //
            this.textBoxReplacement.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxReplacement.Name = "textBoxReplacement";
            this.toolTip.SetToolTip(this.textBoxReplacement, "The correct spelling (paste it with Ctrl+V, or click one of the similar words)");
            this.textBoxReplacement.Enter += new System.EventHandler(this.TextBox_Enter);
            //
            // listBoxSimilarWords
            //
            this.listBoxSimilarWords.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxSimilarWords.IntegralHeight = false;
            this.listBoxSimilarWords.Name = "listBoxSimilarWords";
            this.toolTip.SetToolTip(this.listBoxSimilarWords, "Words the Spell Fixer project considers similar (most frequent first). Click one to use it as the replacement; double-click to use it and click OK");
            this.listBoxSimilarWords.SelectedIndexChanged += new System.EventHandler(this.ListBoxSimilarWords_SelectedIndexChanged);
            this.listBoxSimilarWords.DoubleClick += new System.EventHandler(this.ListBoxSimilarWords_DoubleClick);
            //
            // flowLayoutPanelButtons
            //
            this.flowLayoutPanelButtons.AutoSize = true;
            this.flowLayoutPanelButtons.Controls.Add(this.buttonCancel);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonOk);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);
            this.buttonOk.Name = "buttonOk";
            this.buttonOk.Text = "OK";
            this.toolTip.SetToolTip(this.buttonOk, "Add the rule, replace the selected word in Paratext, and remember the fix for Paratext's spelling list");
            this.buttonOk.Click += new System.EventHandler(this.ButtonOk_Click);
            //
            // FixSpellingForm
            //
            this.AcceptButton = this.buttonOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(560, 180);
            this.Controls.Add(this.tableLayoutPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.MinimumSize = new System.Drawing.Size(420, 160);
            this.Name = "FixSpellingForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Fix Spelling";
            this.Deactivate += new System.EventHandler(this.FixSpellingForm_Deactivate);
            this.tableLayoutPanel.ResumeLayout(false);
            this.tableLayoutPanel.PerformLayout();
            this.flowLayoutPanelButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Label labelVerseCaption;
        private System.Windows.Forms.Label labelVerse;
        private System.Windows.Forms.Label labelSimilarWords;
        private System.Windows.Forms.Label labelBadForm;
        private SilEncConverters40.EcTextBox textBoxBadForm;
        private System.Windows.Forms.ListBox listBoxSimilarWords;
        private System.Windows.Forms.Label labelReplacement;
        private SilEncConverters40.EcTextBox textBoxReplacement;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonOk;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
```

- [ ] **Step 2: `FixSpellingForm.cs`**

```csharp
using Paratext.PluginInterfaces;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Non-modal dialog to add a bad -> good spelling rule for the word selected in Paratext (so the user can
    /// still search, copy, etc. in Paratext while it's up)
    /// </summary>
    internal partial class FixSpellingForm : Form
    {
        private readonly SpellFixerProject _spellFixerProject;
        private readonly IKeyboard _vernacularKeyboard;
        private readonly IKeyboard _defaultKeyboard;
        private readonly Func<string, string, bool> _applyFix;

        public FixSpellingForm(SpellFixerProject spellFixerProject, Font font, bool rightToLeft, IKeyboard vernacularKeyboard,
                               IKeyboard defaultKeyboard, Func<string, string, bool> applyFix)
        {
            InitializeComponent();
            _spellFixerProject = spellFixerProject;
            _vernacularKeyboard = vernacularKeyboard;
            _defaultKeyboard = defaultKeyboard;
            _applyFix = applyFix;

            Text = $"{SpellFixerPlugin.PluginName}: Fix Spelling ({spellFixerProject.DisplayName})";
            if (font != null)
                textBoxBadForm.Font = textBoxReplacement.Font = listBoxSimilarWords.Font = font;
            textBoxBadForm.RightToLeft = textBoxReplacement.RightToLeft = listBoxSimilarWords.RightToLeft =
                rightToLeft ? RightToLeft.Yes : RightToLeft.No;
        }

        public void LoadWord(string badForm, string verseReference)
        {
            labelVerse.Text = verseReference;
            textBoxBadForm.Text = badForm;      // TextChanged fills in the replacement and similar words
            RefreshSuggestions();               // ... even if it's the same word as before
            textBoxReplacement.Focus();
            textBoxReplacement.SelectAll();
        }

        private void RefreshSuggestions()
        {
            var bad = textBoxBadForm.Text.Trim();

            var replacement = bad;
            try
            {
                replacement = _spellFixerProject.Convert(bad);
            }
            catch
            {
                // leave it as the bad form
            }
            textBoxReplacement.Text = replacement;
            textBoxReplacement.SelectAll();

            var similarWords = _spellFixerProject.GetSimilarWords(bad);
            listBoxSimilarWords.BeginUpdate();
            listBoxSimilarWords.Items.Clear();
            listBoxSimilarWords.Items.AddRange(similarWords.Cast<object>().ToArray());
            listBoxSimilarWords.EndUpdate();
            listBoxSimilarWords.Visible = labelSimilarWords.Visible = similarWords.Any();
        }

        private void TextBoxBadForm_TextChanged(object sender, EventArgs e)
        {
            RefreshSuggestions();
        }

        private void ListBoxSimilarWords_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxSimilarWords.SelectedItem is SimilarWord similarWord)
                textBoxReplacement.Text = similarWord.Word;
        }

        private void ListBoxSimilarWords_DoubleClick(object sender, EventArgs e)
        {
            if (listBoxSimilarWords.SelectedItem is SimilarWord)
                ButtonOk_Click(sender, e);
        }

        private void TextBox_Enter(object sender, EventArgs e)
        {
            ActivateKeyboard(_vernacularKeyboard);
        }

        private void FixSpellingForm_Deactivate(object sender, EventArgs e)
        {
            ActivateKeyboard(_defaultKeyboard);
        }

        private static void ActivateKeyboard(IKeyboard keyboard)
        {
            try
            {
                keyboard?.Activate();
            }
            catch
            {
                // not worth bothering the user about
            }
        }

        private void ButtonOk_Click(object sender, EventArgs e)
        {
            var bad = textBoxBadForm.Text.Trim();
            var good = textBoxReplacement.Text.Trim();
            if (String.IsNullOrEmpty(bad) || String.IsNullOrEmpty(good) || (bad == good))
            {
                MessageBox.Show(this, "Put the correct spelling in the Replacement box first (e.g. paste it with Ctrl+V, or click one of the similar words).",
                                SpellFixerPlugin.PluginName);
                return;
            }

            if (_applyFix(bad, good))
                Close();
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
```

- [ ] **Step 3: Build the plugin**

Use the Task 4 Step 3 command. Expected: 0 errors.

- [ ] **Step 4: Commit**

```powershell
git add src/SpellFixerPluginForParatext/FixSpellingForm.cs src/SpellFixerPluginForParatext/FixSpellingForm.Designer.cs
git commit -m "feat(SpellFixerPluginForParatext): non-modal Fix Spelling dialog with similar words and keyboard switching`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Wire it into SpellFixerForm (selection, OK → rule + replace + record) and the check-run "Add Rule"

**Files:**
- Modify: `src/SpellFixerPluginForParatext/SpellFixerForm.cs`
- Modify: `src/SpellFixerPluginForParatext/SpellFixerForm.Designer.cs`
- Modify: `src/SpellFixerPluginForParatext/SpellFixerQueryForm.cs`

**Interfaces:**
- Consumes:
  - `SelectionInfo` and `SelectionReplacer` (Task 1).
  - `SpellingStatusRecorder.Record` (Task 3).
  - `SpellFixerProject.AssignCorrectSpelling(bad, good, context)` and `WordsInContext` (Task 4).
  - `FixSpellingForm` (Task 5).
  - `IProject.GetUSFM(int, int)`, `GetUSFM(int, int, int)` and `PutUSFM(IWriteLock, string, int)`.
  - `IPluginHost.ActiveWindowSelectionChanged`, `ActiveWindowState` and `DefaultKeyboard`.
  - `IProject.VernacularKeyboard`.
- Produces: the `SpellFixerQueryForm` ctor becomes `(SpellFixerProject project, Action<string, string> ruleAdded)`.

- [ ] **Step 1: Designer text changes (`SpellFixerForm.Designer.cs`)**

- `buttonAssignCorrectSpelling.Text` becomes `"&Assign Correct Spelling (selection)"`. Its tooltip becomes "Select the misspelled word in the Paratext text window, then click here. Paste the correct spelling into the Replacement box (Ctrl+V), or click one of the similar words."
- `buttonFindReplacementRule.Text` becomes `"&Find Replacement Rule"`. Its tooltip becomes "Find the replacement rule for the word selected in the Paratext text window (or, if nothing is selected, the word on the clipboard)."

- [ ] **Step 2: Selection tracking (`SpellFixerForm.cs`)**

Add these fields and methods, and wire them up as described:

```csharp
        private SelectionInfo _lastSelection;

        private void Host_ActiveWindowSelectionChanged(IPluginHost sender, IParatextChildState activeWindowState, IReadOnlyList<ISelection> currentSelections)
        {
            // only keep selections in this project's (Scripture text) windows; moving to our own window must not lose it
            if (activeWindowState?.Project?.ShortName != _project.ShortName)
                return;

            var selection = currentSelections?.OfType<IScriptureTextSelection>().FirstOrDefault();
            if (selection != null)
                _lastSelection = SelectionInfo.FromSelection(selection);
        }

        private SelectionInfo CurrentSelection
        {
            get
            {
                var state = _host.ActiveWindowState;
                if (state?.Project?.ShortName == _project.ShortName)
                {
                    var selection = state.Selections?.OfType<IScriptureTextSelection>().FirstOrDefault();
                    if (selection != null)
                        return SelectionInfo.FromSelection(selection);
                }
                return _lastSelection;
            }
        }
```

- In the ctor, after `_host.VerseRefChanged += Host_VerseRefChanged;`, add `_host.ActiveWindowSelectionChanged += Host_ActiveWindowSelectionChanged;` followed by `_lastSelection = CurrentSelection;`.
- In `SpellFixerForm_FormClosing`, after the `_isChecking` check:
  - add `_host.ActiveWindowSelectionChanged -= Host_ActiveWindowSelectionChanged;`;
  - add `_fixSpellingForm?.Close();`.

- [ ] **Step 3: Assign Correct Spelling opens FixSpellingForm, and OK applies the fix**

Replace `ButtonAssignCorrectSpelling_Click` and `ButtonFindReplacementRule_Click` with the code below, and add the new members:

```csharp
        private FixSpellingForm _fixSpellingForm;

        // the selection (and the word in it) the Fix Spelling dialog was opened for
        private SelectionInfo _fixSelection;
        private string _fixWord;
        private int _fixWordOffset;

        private void ButtonAssignCorrectSpelling_Click(object sender, EventArgs e)
        {
            var selection = CurrentSelection;
            if (!SelectionReplacer.ValidateSelection(selection, out string word, out int wordOffset, out string reason))
            {
                MessageBox.Show($"Select the misspelled word in the {_project.ShortName} text window first (one word, within one verse): {reason}.",
                                SpellFixerPlugin.PluginName);
                return;
            }

            _fixSelection = selection;
            _fixWord = word;
            _fixWordOffset = wordOffset;

            if ((_fixSpellingForm == null) || _fixSpellingForm.IsDisposed)
            {
                _projectFont ??= GetProjectFont();
                _fixSpellingForm = new FixSpellingForm(_spellFixerProject, _projectFont, _project.Language?.IsRtoL ?? false,
                                                       _project.VernacularKeyboard, _host.DefaultKeyboard, ApplyFix);
                _fixSpellingForm.FormClosed += (s, args) =>
                {
                    _fixSpellingForm = null;
                    UpdateButtonStates();
                };
            }

            _fixSpellingForm.TopMost = TopMost;
            _fixSpellingForm.LoadWord(word, selection.VerseRefStart?.ToString());
            if (!_fixSpellingForm.Visible)
                _fixSpellingForm.Show(this);
            _fixSpellingForm.Activate();
            UpdateButtonStates();
        }

        private void ButtonFindReplacementRule_Click(object sender, EventArgs e)
        {
            if (SelectionReplacer.ValidateSelection(CurrentSelection, out string word, out _, out _))
                TryAction(() => _spellFixerProject.FindReplacementRule(word));
            else
                DoWithClipboardWord(clipboardWord => _spellFixerProject.FindReplacementRule(clipboardWord));
        }

        /// <summary>
        /// Called when OK is clicked in the Fix Spelling dialog: add the rule, replace the selected word in Paratext,
        /// and record the fix for Paratext's spelling status. Returns true to close the dialog.
        /// </summary>
        private bool ApplyFix(string bad, string good)
        {
            // only if the dialog's bad form is still the selected word do we know where it is (for the context
            //  and the replacement in Paratext)
            var isSelectedWord = (_fixSelection != null) && (bad == _fixWord);

            try
            {
                var context = isSelectedWord && (_spellFixerProject.WordsInContext > 0)
                                ? new List<string> { SelectionReplacer.BuildContext(_fixSelection, _fixWord, _fixWordOffset, _spellFixerProject.WordsInContext) }
                                : null;
                _spellFixerProject.AssignCorrectSpelling(bad, good, context);
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: AssignCorrectSpelling: {ex}");
                MessageBox.Show(_fixSpellingForm, $"Unable to add the rule '{bad}' → '{good}':{Environment.NewLine}{ex.Message}", SpellFixerPlugin.PluginName);
                return false;
            }

            var problem = isSelectedWord
                            ? ReplaceSelectedWord(_fixSelection, _fixWord, _fixWordOffset, good)
                            : $"The rule was added, but the text in Paratext wasn't changed because the bad form ('{bad}') isn't the selected word ('{_fixWord}').";

            SpellingStatusRecorder.Record(_project, _plugin, bad, good, message => MessageBox.Show(message, SpellFixerPlugin.PluginName));

            SetStatus($"Added the rule '{bad}' → '{good}'" + ((problem == null) ? $" and fixed it in {_fixSelection.VerseRefStart}." : "."));
            if (problem != null)
                MessageBox.Show(_fixSpellingForm, problem, SpellFixerPlugin.PluginName);

            _fixSelection = null;
            return true;
        }

        /// <summary>
        /// Replaces the selected occurrence of the bad word with the good one (if the verse hasn't changed since
        /// it was selected). Returns null if it worked, or else the reason it didn't.
        /// </summary>
        private string ReplaceSelectedWord(SelectionInfo selection, string bad, int badOffset, string good)
        {
            var verseReference = selection.VerseRefStart;
            IWriteLock writeLock = null;
            try
            {
                writeLock = _project.RequestWriteLock(_plugin, ReleaseRequested, verseReference.BookNum, verseReference.ChapterNum);
                if (writeLock == null)
                    return $"The rule was added, but you don't have edit privilege on {verseReference.BookCode} {verseReference.ChapterNum} of the {_project.ShortName} project, so the text wasn't changed.";

                var chapterUsfm = _project.GetUSFM(verseReference.BookNum, verseReference.ChapterNum);
                var verseUsfm = _project.GetUSFM(verseReference.BookNum, verseReference.ChapterNum, verseReference.VerseNum);

                if (!SelectionReplacer.TryReplaceInVerse(verseUsfm, selection, bad, badOffset, good, out string newVerseUsfm, out string reason))
                    return $"The rule was added, but {verseReference} wasn't changed because {reason}. Fix it by hand (or select it again).";

                if (!SelectionReplacer.ReplaceVerseInChapter(chapterUsfm, verseUsfm, newVerseUsfm, out string newChapterUsfm))
                    return $"The rule was added, but {verseReference} wasn't changed because the verse couldn't be located in the chapter.";

                _project.PutUSFM(writeLock, newChapterUsfm, verseReference.BookNum);
                return null;
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: ReplaceSelectedWord: {ex}");
                return $"The rule was added, but changing {verseReference} failed:{Environment.NewLine}{ex.Message}";
            }
            finally
            {
                writeLock?.Dispose();
                _setSyncReference(verseReference);
            }
        }
```

- In `UpdateButtonStates`, require that the Fix Spelling dialog isn't open before a check can start, by changing the `buttonCheck` line to:

```csharp
            buttonCheck.Enabled = haveProject && !_isChecking && ((_fixSpellingForm == null) || _fixSpellingForm.IsDisposed);
```

- In `SetSpellFixerProject`, close the dialog when the project changes: add `_fixSpellingForm?.Close();` next to the `_queryForm` disposal.

- [ ] **Step 4: Record rules added with the check run's "Add Rule" (`SpellFixerQueryForm.cs`)**

Change the ctor and `buttonAddRule_Click` as follows:

```csharp
        private readonly Action<string, string> _ruleAdded;

        public SpellFixerQueryForm(SpellFixerProject spellFixerProject, Action<string, string> ruleAdded)
        {
            InitializeComponent();
            _spellFixerProject = spellFixerProject;
            _ruleAdded = ruleAdded;
        }
```

```csharp
        private void buttonAddRule_Click(object sender, EventArgs e)
        {
            var word = textBoxInput.Text;
            var before = SafeConvert(word);
            TryAction(() => _spellFixerProject.AssignCorrectSpelling(word));

            // FindReplaceHelper doesn't tell us what the user chose, so see what the converter does with it now
            var after = SafeConvert(word);
            if ((after != null) && (after != before) && (after != word))
                _ruleAdded?.Invoke(word, after);
        }

        private string SafeConvert(string word)
        {
            try
            {
                return _spellFixerProject.Convert(word);
            }
            catch
            {
                return null;
            }
        }
```

In `SpellFixerForm.AskUser`, change the construction to:

```csharp
            _queryForm ??= new SpellFixerQueryForm(_spellFixerProject,
                (bad, good) => SpellingStatusRecorder.Record(_project, _plugin, bad, good, message => MessageBox.Show(message, SpellFixerPlugin.PluginName)));
```

- [ ] **Step 5: Build, then run the regression tests**

Build the plugin as in Task 4 Step 3. Expected: 0 errors and no new warnings. Then run the TestBwdc filter `FullyQualifiedName~UnitTest_PtxSpellFixer`. Expected: all pass.

- [ ] **Step 6: Manual check in Paratext (by the user; Debug build in VS)**

1. Select a misspelled word in the text, copy the correct form, and click **Assign Correct Spelling (selection)**.
   - The dialog shows the bad form, the existing replacement and the similar words.
   - Paratext is still usable, e.g. Find works while the dialog is open.
2. Press Ctrl+V and click OK.
   - The selected occurrence changes in Paratext.
   - The rule exists (check with Edit Spelling Fixes).
   - `<projects>\<ShortName>\local\SpellFixer\PendingSpellingStatus.xml` has the fix.
   - **This also checks Review Focus #1:** if you get "the verse has changed since the word was selected" on an unedited verse, `Offset` and `GetUSFM(b, c, v)` disagree. Report it.
3. Click a similar word, then OK.
4. Edit the bad form, then OK. The rule is added, Paratext is not changed, and a message explains why.
5. Keyboard: the vernacular keyboard comes on when you click into the boxes.
6. Check run → **Add Rule** in the query dialog → the fix is recorded in the pending file.
7. With a legacy project: the similar words list is hidden, and OK adds the rule.

- [ ] **Step 7: Commit**

```powershell
git add src/SpellFixerPluginForParatext/SpellFixerForm.cs src/SpellFixerPluginForParatext/SpellFixerForm.Designer.cs src/SpellFixerPluginForParatext/SpellFixerQueryForm.cs
git commit -m "feat(SpellFixerPluginForParatext): Assign Correct Spelling uses the Paratext selection, replaces it and records the fix`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: SpellingStatusUpdater.exe + ShuttingDown launch

**Files:**
- Create:
  - `src/SpellingStatusUpdater/SpellingStatusUpdater.csproj`
  - `src/SpellingStatusUpdater/Program.cs`
  - `src/SpellingStatusUpdater/SpellingStatusUpdateRunner.cs`
  - `src/SpellingStatusUpdater/Properties/AssemblyInfo.cs`
- Modify:
  - `src/SpellFixerPluginForParatext/SpellFixerPlugin.cs`
  - `src/SpellFixerPluginForParatext/SpellFixerPluginForParatext.csproj`
  - `SEC VS2019.sln`
  - `src/SILConvertersWordML/TestBwdc/TestBwdc.csproj`
  - `src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixerSpelling.cs`

**Interfaces:**
- Consumes: `PendingSpellingStatusStore`, `SpellingStatusMerger`, `AtomicFile` (Task 2, linked); `SpellingStatusRecorder.HasRecordedThisSession` and `ParatextProjectFolder.GetProjectsDirectory` (Task 3).
- Produces: `public static class SIL.SpellingStatusUpdater.SpellingStatusUpdateRunner`, with:
  - `List<string> ProcessProjectsDirectory(string projectsDirectory, DateTime now)`
  - `string ProcessProject(string projectFolder, DateTime now)`, which returns a log line or null.

- [ ] **Step 1: Create the project**

`src/SpellingStatusUpdater/SpellingStatusUpdater.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <Platforms>AnyCPU;x86;x64</Platforms>
    <ProjectGuid>{B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74}</ProjectGuid>
    <OutputType>WinExe</OutputType>
    <RootNamespace>SIL.SpellingStatusUpdater</RootNamespace>
    <AssemblyName>SpellingStatusUpdater</AssemblyName>
    <LangVersion>latest</LangVersion>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Platform)'=='x64'">
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Platform)'=='x86'">
    <PlatformTarget>x86</PlatformTarget>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|x64'">
    <OutputPath>..\..\output\x64\Debug\</OutputPath>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <DefineConstants>TRACE;DEBUG</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x64'">
    <OutputPath>..\..\output\x64\Release\</OutputPath>
    <Optimize>true</Optimize>
    <DebugType>pdbonly</DebugType>
    <DefineConstants>TRACE</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|x86'">
    <OutputPath>bin\x86\Debug\</OutputPath>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x86'">
    <OutputPath>bin\x86\Release\</OutputPath>
    <Optimize>true</Optimize>
    <DebugType>pdbonly</DebugType>
    <DefineConstants>TRACE</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'">
    <OutputPath>..\..\output\x86\Debug\</OutputPath>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|AnyCPU'">
    <OutputPath>..\..\output\x86\Release\</OutputPath>
    <Optimize>true</Optimize>
    <DebugType>pdbonly</DebugType>
    <DefineConstants>TRACE</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <!-- shared with (linked from) the SpellFixerPluginForParatext project -->
    <Compile Include="..\SpellFixerPluginForParatext\SpellingStatusMerger.cs" Link="Shared\SpellingStatusMerger.cs" />
    <Compile Include="..\SpellFixerPluginForParatext\PendingSpellingStatusStore.cs" Link="Shared\PendingSpellingStatusStore.cs" />
  </ItemGroup>
</Project>
```

`src/SpellingStatusUpdater/Properties/AssemblyInfo.cs`:

```csharp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("SpellingStatusUpdater")]
[assembly: AssemblyDescription("Merges the spelling fixes made in the SpellFixer Paratext plugin into the projects' SpellingStatus.xml after Paratext exits")]
[assembly: AssemblyCompany("SIL")]
[assembly: AssemblyProduct("SpellingStatusUpdater")]
[assembly: AssemblyCopyright("Copyright © 2026 SIL. All rights reserved.")]
[assembly: ComVisible(false)]
[assembly: Guid("b3f6d2a1-7c4e-4e8b-a1d5-9f2c6e3b8d74")]
[assembly: AssemblyVersion("4.0.0.0")]
[assembly: AssemblyFileVersion("4.1.0.0")]
```

- [ ] **Step 2: Write the failing runner tests**

Make these changes in `TestBwdc.csproj`:
- Next to the SpellFixer ProjectReference, add:
  ```xml
      <ProjectReference Include="..\..\SpellingStatusUpdater\SpellingStatusUpdater.csproj" Condition="('$(Platform)' == 'x64') and ('$(OS)'=='Windows_NT')" Aliases="Updater" />
  ```
- At the top of `UnitTest_PtxSpellFixerSpelling.cs`, add `extern alias Updater;` after `extern alias SpellFixer;`.
- Add `using SpellingStatusUpdateRunner = Updater::SIL.SpellingStatusUpdater.SpellingStatusUpdateRunner;` after the other usings.

Then append inside the test class:

```csharp
        #region SpellingStatusUpdateRunner

        [Test]
        public void Runner_MergesPendingFixes_BacksUpAndClearsPending()
        {
            var projectFolder = MakeProject("Dog", null);
            var statusPath = Path.Combine(projectFolder, "SpellingStatus.xml");
            File.WriteAllText(statusPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<SpellingStatus>\r\n  <Status Word=\"cat\" State=\"R\" />\r\n</SpellingStatus>");
            var pendingPath = PendingSpellingStatusStore.PathFor(projectFolder);
            PendingSpellingStatusStore.Append(pendingPath, "teh", "the", DateTime.UtcNow);
            MakeProject("Cat", null);   // a project with nothing pending

            var now = new DateTime(2026, 10, 8, 15, 4, 5);
            var lines = SpellingStatusUpdateRunner.ProcessProjectsDirectory(_tempDir, now);

            Assert.AreEqual(1, lines.Count);
            StringAssert.Contains("Dog", lines[0]);
            Assert.IsFalse(File.Exists(pendingPath), "pending fixes cleared");
            Assert.IsTrue(File.Exists(Path.Combine(PendingSpellingStatusStore.FolderFor(projectFolder), "SpellingStatus.xml.20261008150405.bak")));
            Assert.IsTrue(File.Exists(Path.Combine(PendingSpellingStatusStore.FolderFor(projectFolder), "SpellingStatusUpdater.log")));

            var doc = XDocument.Load(statusPath);
            Assert.AreEqual("W", (string)StatusOf(doc, "teh").Attribute("State"));
            Assert.AreEqual("R", (string)StatusOf(doc, "the").Attribute("State"));
            Assert.AreEqual("R", (string)StatusOf(doc, "cat").Attribute("State"));
        }

        [Test]
        public void Runner_NoSpellingStatusYet_CreatesIt()
        {
            var projectFolder = MakeProject("Dog", null);
            PendingSpellingStatusStore.Append(PendingSpellingStatusStore.PathFor(projectFolder), "teh", "the", DateTime.UtcNow);

            SpellingStatusUpdateRunner.ProcessProjectsDirectory(_tempDir, DateTime.Now);

            var doc = XDocument.Load(Path.Combine(projectFolder, "SpellingStatus.xml"));
            Assert.AreEqual("W", (string)StatusOf(doc, "teh").Attribute("State"));
        }

        [Test]
        public void Runner_LockedSpellingStatus_LeavesFixesPending()
        {
            var projectFolder = MakeProject("Dog", null);
            var statusPath = Path.Combine(projectFolder, "SpellingStatus.xml");
            File.WriteAllText(statusPath, "<SpellingStatus />");
            var pendingPath = PendingSpellingStatusStore.PathFor(projectFolder);
            PendingSpellingStatusStore.Append(pendingPath, "teh", "the", DateTime.UtcNow);

            using (new FileStream(statusPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var lines = SpellingStatusUpdateRunner.ProcessProjectsDirectory(_tempDir, DateTime.Now);
                StringAssert.Contains("ERROR", lines.Single());
            }

            Assert.AreEqual(1, PendingSpellingStatusStore.Load(pendingPath).Count, "still pending");
        }

        #endregion
```

Build: expected `CS0234`/`CS0246` errors, because `Updater::SIL.SpellingStatusUpdater` doesn't exist yet. Restore will also fail until the project file exists, and it does exist after Step 1.

- [ ] **Step 3: Implement `SpellingStatusUpdateRunner.cs` and `Program.cs`**

`src/SpellingStatusUpdater/SpellingStatusUpdateRunner.cs`:

```csharp
using SIL.SpellFixerPluginForParatext;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SIL.SpellingStatusUpdater
{
    /// <summary>
    /// Merges each project's pending SpellFixer fixes (local\SpellFixer\PendingSpellingStatus.xml) into its
    /// SpellingStatus.xml. Only run when Paratext isn't (since Paratext keeps its own copy in memory)
    /// </summary>
    public static class SpellingStatusUpdateRunner
    {
        public const string LogFileName = "SpellingStatusUpdater.log";

        public static List<string> ProcessProjectsDirectory(string projectsDirectory, DateTime now)
        {
            return Directory.EnumerateDirectories(projectsDirectory)
                            .Where(projectFolder => File.Exists(PendingSpellingStatusStore.PathFor(projectFolder)))
                            .Select(projectFolder => ProcessProject(projectFolder, now))
                            .Where(line => line != null)
                            .ToList();
        }

        public static string ProcessProject(string projectFolder, DateTime now)
        {
            var projectName = Path.GetFileName(projectFolder);
            var spellFixerFolder = PendingSpellingStatusStore.FolderFor(projectFolder);
            var pendingPath = PendingSpellingStatusStore.PathFor(projectFolder);
            string line;
            try
            {
                var fixes = PendingSpellingStatusStore.Load(pendingPath);
                if (!fixes.Any())
                {
                    PendingSpellingStatusStore.Save(pendingPath, fixes);  // i.e. delete it
                    return null;
                }

                var statusPath = Path.Combine(projectFolder, "SpellingStatus.xml");
                if (File.Exists(statusPath))
                    File.Copy(statusPath, Path.Combine(spellFixerFolder, $"SpellingStatus.xml.{now:yyyyMMddHHmmss}.bak"), true);

                var doc = SpellingStatusMerger.LoadOrCreate(statusPath);
                foreach (var fix in fixes)
                    SpellingStatusMerger.Apply(doc, fix.Bad, fix.Good);
                SpellingStatusMerger.Save(doc, statusPath);

                PendingSpellingStatusStore.Save(pendingPath, Enumerable.Empty<PendingFix>());
                line = $"{now:s} {projectName}: merged {fixes.Count} fix(es) into SpellingStatus.xml";
            }
            catch (Exception ex)
            {
                line = $"{now:s} {projectName}: ERROR (the fixes are still pending): {ex.Message}";
            }

            try
            {
                Directory.CreateDirectory(spellFixerFolder);
                File.AppendAllText(Path.Combine(spellFixerFolder, LogFileName), line + Environment.NewLine);
            }
            catch
            {
                // nowhere else to report it
            }

            return line;
        }
    }
}
```

`src/SpellingStatusUpdater/Program.cs`:

```csharp
using System;
using System.Diagnostics;
using System.IO;

namespace SIL.SpellingStatusUpdater
{
    /// <summary>
    /// Started by the SpellFixer Paratext plugin as Paratext shuts down:
    ///   SpellingStatusUpdater.exe --wait-pid {Paratext's process id} --projects-dir "{e.g. C:\My Paratext 9 Projects}"
    /// Waits for Paratext to exit and then merges the pending spelling fixes into each project's SpellingStatus.xml
    /// </summary>
    internal static class Program
    {
        private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(30);

        [STAThread]
        private static int Main(string[] args)
        {
            int? waitPid = null;
            string projectsDirectory = null;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--wait-pid" && int.TryParse(args[i + 1], out int pid))
                    waitPid = pid;
                else if (args[i] == "--projects-dir")
                    projectsDirectory = args[i + 1];
            }

            if (String.IsNullOrEmpty(projectsDirectory) || !Directory.Exists(projectsDirectory))
                return 1;

            if (waitPid.HasValue && !WaitForExit(waitPid.Value))
                return 2;   // Paratext is still running; leave everything pending for next time

            SpellingStatusUpdateRunner.ProcessProjectsDirectory(projectsDirectory, DateTime.Now);
            return 0;
        }

        private static bool WaitForExit(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                    return process.WaitForExit((int)MaxWait.TotalMilliseconds);
            }
            catch (ArgumentException)
            {
                return true;    // it has already exited
            }
        }
    }
}
```

- [ ] **Step 4: Add the project to the solution, and make the plugin depend on it**

`SEC VS2019.sln`:
- After the `SpellFixerPluginForParatext` `EndProject`, insert:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SpellingStatusUpdater", "src\SpellingStatusUpdater\SpellingStatusUpdater.csproj", "{B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74}"
EndProject
```

- After the `{6C1E5B2A-...}.Release|x86.ActiveCfg` line, insert:

```
		{B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74}.Debug|x64.ActiveCfg = Debug|x64
		{B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74}.Debug|x64.Build.0 = Debug|x64
		{B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74}.Debug|x86.ActiveCfg = Debug|x86
		{B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74}.Release|x64.ActiveCfg = Release|x64
		{B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74}.Release|x64.Build.0 = Release|x64
		{B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74}.Release|x86.ActiveCfg = Release|x86
```

- In the `ParatextBackTranslationHelperPlugin64bitMM` ProjectDependencies section, add `{B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74} = {B3F6D2A1-7C4E-4E8B-A1D5-9F2C6E3B8D74}`.

`SpellFixerPluginForParatext.csproj`:
- Add a build-order-only reference:

```xml
  <ItemGroup>
    <ProjectReference Include="..\SpellingStatusUpdater\SpellingStatusUpdater.csproj" ReferenceOutputAssembly="false" />
  </ItemGroup>
```

- In the PostBuildEvent, add these lines just before `:nocopy`:

```
@echo Copying '$(SolutionDir)output\x64\Debug\SpellingStatusUpdater.exe' to: '%ParatextInstallDir%\plugins\$(MSBuildProjectName)'
copy "$(SolutionDir)output\x64\Debug\SpellingStatusUpdater.exe" "%ParatextInstallDir%\plugins\$(MSBuildProjectName)\SpellingStatusUpdater.exe"
```

- [ ] **Step 5: Launch the helper on ShuttingDown (`SpellFixerPlugin.cs`)**

Add `using System.ComponentModel;`, `using System.Diagnostics;`, `using System.IO;` and `using System.Reflection;`. Then:

```csharp
        private static bool _isShutdownHooked;

        private static string PluginFolder
        {
            get
            {
                var location = Assembly.GetExecutingAssembly().Location;
                return !String.IsNullOrEmpty(location)
                        ? Path.GetDirectoryName(location)
                        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "SpellFixerPluginForParatext");
            }
        }

        private static void Host_ShuttingDown(object sender, CancelEventArgs e)
        {
            try
            {
                if (!SpellingStatusRecorder.HasRecordedThisSession)
                    return;

                var projectsDirectory = ParatextProjectFolder.GetProjectsDirectory();
                var updaterPath = Path.Combine(PluginFolder, "SpellingStatusUpdater.exe");
                if ((projectsDirectory == null) || !File.Exists(updaterPath))
                {
                    _host?.Log(_this, $"SpellFixer: can't update SpellingStatus.xml (projects folder: '{projectsDirectory}', updater: '{updaterPath}')");
                    return;
                }

                var arguments = $"--wait-pid {Process.GetCurrentProcess().Id} --projects-dir \"{projectsDirectory.TrimEnd('\\')}\"";
                Process.Start(new ProcessStartInfo(updaterPath, arguments) { UseShellExecute = false, CreateNoWindow = true });
            }
            catch (Exception ex)
            {
                _host?.Log(_this, $"SpellFixer: unable to start SpellingStatusUpdater: {ex.Message}");
            }
        }
```

In `Run`, after `_host = host;`, add:

```csharp
            if (!_isShutdownHooked)
            {
                host.ShuttingDown += Host_ShuttingDown;
                _isShutdownHooked = true;
            }
```

- [ ] **Step 6: Build and run the tests**

```powershell
$env:ParatextInstallDir=''; & $msbuild src\SILConvertersWordML\TestBwdc\TestBwdc.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
& $vstest src\SILConvertersWordML\TestBwdc\bin\x64\Debug\TestBwdc.dll /Platform:x64 /TestCaseFilter:"FullyQualifiedName~UnitTest_PtxSpellFixer"
```
Expected: 0 build errors; all tests pass, including the three runner tests. `output\x64\Debug\SpellingStatusUpdater.exe` exists.

- [ ] **Step 7: Commit**

```powershell
git add src/SpellingStatusUpdater src/SpellFixerPluginForParatext/SpellFixerPlugin.cs src/SpellFixerPluginForParatext/SpellFixerPluginForParatext.csproj "SEC VS2019.sln" src/SILConvertersWordML/TestBwdc/TestBwdc.csproj src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixerSpelling.cs
git commit -m "feat(SpellingStatusUpdater): merge recorded SpellFixer fixes into SpellingStatus.xml after Paratext exits`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: Installer, full build, full suite

**Files:**
- Modify: `Installer/ParatextBackTranslationHelperPlugin64bitMM/ParatextBackTranslationHelperPlugin_MergeModule.wxs`

- [ ] **Step 1: Add the helper to the merge module**

Inside `<Directory Id="SpellFixerPluginForParatext" ...>`, after the `.ptxplg` component, add:

```xml
            <Component Id="SpellingStatusUpdater.exe" Guid="{4E2D8A63-1B7C-4F59-9D3A-6C8E2F1B7A45}">
              <File Id="SpellingStatusUpdater.exe" Name="SpellingStatusUpdater.exe" KeyPath="yes" Source="..\..\output\x64\release\SpellingStatusUpdater.exe" />
            </Component>
```

- [ ] **Step 2: Build the whole solution, Release x64**

```powershell
$env:ParatextInstallDir=''; & $msbuild "SEC VS2019.sln" -restore -p:Configuration=Release -p:Platform=x64 -v:m
```
This takes more than 10 minutes, so run it in the background. Expected: 0 errors. The merge module, MSI and bundle all build, and the `.msm` contains `SpellingStatusUpdater.exe` (check with a byte search, as before).

- [ ] **Step 3: Run the whole TestBwdc suite**

```powershell
& $vstest src\SILConvertersWordML\TestBwdc\bin\x64\Debug\TestBwdc.dll /Platform:x64
```
Expected: all pass. Rebuild Debug first if the Release build replaced the Debug output.

- [ ] **Step 4: Manual end-to-end (by the user)**

1. Make one or two fixes with the Fix Spelling dialog, then quit Paratext.
2. Within a few seconds:
   - `SpellingStatus.xml` has the new W (with Correction) and R entries;
   - `local\SpellFixer\SpellingStatus.xml.<timestamp>.bak` and `SpellingStatusUpdater.log` exist;
   - the pending file is gone.
3. Restart Paratext and check that the Wordlist shows the new spelling statuses.
4. If nothing happens, check whether `SpellingStatusUpdater.exe` was still running after Paratext exited (Task Manager). If it wasn't, Paratext may be killing child processes, and the launch needs a detached start (e.g. via `cmd /c start`).

- [ ] **Step 5: Commit**

```powershell
git add Installer/ParatextBackTranslationHelperPlugin64bitMM/ParatextBackTranslationHelperPlugin_MergeModule.wxs
git commit -m "installer: add SpellingStatusUpdater.exe to the SpellFixer plugin folder`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```
