# SpellFixerPluginForParatext Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Paratext 9 plugin that walks the active project word by word from the current verse, runs each word through the user's SpellFixer EncConverter, and asks the user about each change. Accepted fixes are written back once per verse.

**Architecture:** A new SDK-style net48 plugin project, `src/SpellFixerPluginForParatext`, sits beside `src/ParatextPluginBackTranslationHelper`. It links that project's token classes and a newly extracted `UsfmChapterTokens` helper. The pure logic, which is unit-tested, lives in two classes:
- `SpellFixerTokenProcessor`: splits text into words and puts it back together, fixes a verse's tokens, and splices the verse back into the chapter.
- `SpellFixerSession`: the Skip / Replace Once / Replace Every / Replace All / Cancel state machine.

The Paratext/WinForms glue lives in `SpellFixerForm`, a non-modal window. It also uses `SpellFixerQueryForm`, a copy of the Office `SILConverterProcessorForm`, and `SpellFixerProject`, a wrapper around `BackTranslationHelper.FindReplaceHelper`. Installation goes through the existing BackTranslationHelper merge module.

**Tech Stack:** C# (LangVersion latest), .NET Framework 4.8, WinForms, Paratext PluginInterfaces 2.0.23, Encoding-Converters-Core 1.0.2 (SilEncConverters40, BackTranslationHelper.dll, SpellingFixer30.dll), NUnit 3 (TestBwdc project), WiX 6.

**Spec:** `docs/superpowers/specs/2026-10-07-spellfixer-plugin-for-paratext-design.md`

## Global Constraints

- **Project:** `src/SpellFixerPluginForParatext/SpellFixerPluginForParatext.csproj`; RootNamespace `SIL.SpellFixerPluginForParatext`; AssemblyName `SpellFixerPluginForParatext`; `net48`; x64 is the platform that ships.
- **Package versions:** `Encoding-Converters-Core` 1.0.2, `ParatextPluginInterfaces` 2.0.23, `ParatextEmbeddedUiPluginInterfaces` 2.0.23.
- **Paratext project:** the active window's project. **SpellFixer project:** chosen through `FindReplaceHelper.GetFindReplaceHelper()` (which calls `QuerySpellFixProjectType()`). The last one chosen is remembered per Paratext project.
- **Text checked:** all publishable vernacular text tokens (`PtxPluginHelpers.IsPublishableVernacular`), including `\s` and `\ft`.
- **Run scope:** from the verse currently selected in Paratext to the end of the chapter, then a prompt to continue with the next chapter, or the next book at the end of a book.
- **Writes:** one `PutUSFMTokens` per verse that has accepted fixes.
- **Sync:** Paratext's sync group follows the verse being checked.
- **The query dialog's "Replace with" property is named `CorrectedSpelling`** (it was `ForwardString` in the Office original).
- **Clipboard button labels and tooltips, verbatim:**
  - "Assign Correct Spelling (clipboard)", tooltip: "Assign the correct spelling for the word on the system clipboard. Copy the word (e.g. from Paratext) to the clipboard before clicking this button."
  - "Find Replacement Rule (clipboard)", tooltip: "Find the replacement rule that applies to the word on the system clipboard. Copy the word to the clipboard before clicking this button."
- **Installer:** the plugin goes into the existing `ParatextBackTranslationHelperPlugin64bitMM` merge module, with no rename. In `EcFeatures.wxs`, keep `Feature Id="Paratext_BackTranslation_Helper_Plugin"` and set Title to "SIL Converters Plugins for Paratext". It stays Level 4, or 3 when `COMPONENTEXISTS_PARATEXT_9`.
- **Do not change encoding-converters-core.**
- **Build commands (PowerShell, from the repo root):**
  - `$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -find 'MSBuild\Current\Bin\amd64\MSBuild.exe'`
  - `$vstest = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -find '**\vstest.console.exe' | Select-Object -First 1`
  - Close Paratext before building Debug: the post-build step copies into `%ParatextInstallDir%`, and a running Paratext locks the files.
- **Tests:** NUnit, in `src/SILConvertersWordML/TestBwdc`. That project lists its sources explicitly (`<Compile Include=...>`), so every new test file needs its own entry with `Condition="'$(Platform)' == 'x64'"`.
- **Commits:** on a feature branch (`feature/spellfixer-plugin-for-paratext`), never on `master`. End every commit message with `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.

## Review Focus

1. **Words with attached punctuation** (`"word,"`, `"(word"`): the whole whitespace-delimited run goes to the converter unchanged, and the result is put back exactly. The CC table's own word-boundary rules decide what to do with the punctuation. → Task 2 test `FixWords_PassesPunctuatedRunWhole`.
2. **The user empties the "Replace with" box** (deletes the word): `""` is a valid correction, not a cancel. Only `null` cancels. → Task 3 test `FixWord_EmptyCorrection_IsAReplacementNotACancel`.
3. **The user edits the suggestion and picks Replace Every:** later occurrences get the user's edited text, not the converter's suggestion, without asking again. → Task 3 test `FixWord_ReplaceEvery_ReusesUsersEditedSpellingWithoutAsking`.
4. **Paratext points at a single verse inside a combined verse** (e.g. MAT 5:4 when the text has `\v 3-4`): the run starts at the combined `40_005_003-004` group. → Task 1 test `StartVerseKey_SingleVerseInsideCombinedVerse_FindsCombinedKey`.
5. **The verse changes in Paratext between being read and being written** (e.g. unsaved edits saved during `RequestWriteLock`): the verse is not overwritten with stale tokens. The run stops with a message instead. → Task 2 test `VerseText_DiffersWhenATextTokenChanges`, and the check in Task 6's `WriteVerse`.

---

## File Structure

**ParatextPluginBackTranslationHelper (existing; shared code split out):**
- Create `UsfmChapterTokens.cs`: verse keys, grouping a chapter by verse, start and next verse keys. Linked into the new project.
- Create `PtxPluginHelpers.Translatable.cs`: the settings-dependent `IsTranslatable` part of `PtxPluginHelpers`. Stays in the sister project only.
- Create `PluginAssemblyResolver.cs`: the `AssemblyResolve` hook taken out of the plugin class. Linked.
- Modify `PtxPluginHelpers.cs` (becomes `partial`, no settings use), `BackTranslationHelperForm.cs` (uses `UsfmChapterTokens`) and `ParatextBackTranslationHelperPlugin.cs` (uses `PluginAssemblyResolver`).

**SpellFixerPluginForParatext (new):**
- `SpellFixerPluginForParatext.csproj`, `Properties/AssemblyInfo.cs`, `Properties/Settings.settings`, `Properties/Settings.Designer.cs`
- `SpellFixerTokenProcessor.cs`: word splitting and rebuilding, verse token fixing, chapter splice, verse text. Pure.
- `SpellFixerSession.cs`: `FormButtons`, the `AskUser` delegate, and `SpellFixerSession` (the per-run decision state). Pure.
- `SpellFixerProject.cs`: wraps `FindReplaceHelper` plus the `DirectableEncConverter`.
- `SpellFixerQueryFormBase.{cs,Designer.cs,resx}` and `SpellFixerQueryForm.{cs,Designer.cs,resx}`: copied from SILConvertersOffice.
- `SpellFixerForm.{cs,Designer.cs}`: the non-modal main window and the checking loop.
- `SpellFixerPlugin.cs`: the `IParatextStandalonePlugin` entry point.

**Tests (TestBwdc):** `PtxTestTokens.cs` (token builders), `UnitTest_UsfmChapterTokens.cs`, `UnitTest_PtxSpellFixer.cs`.

**Installer:** `Installer/ParatextBackTranslationHelperPlugin64bitMM/ParatextBackTranslationHelperPlugin_MergeModule.wxs` and `Installer/SEC Setup 64bit/EcFeatures.wxs`.

**Solution:** `SEC VS2019.sln`.

---

### Task 0: Branch

- [ ] **Step 1: Create the feature branch and commit the spec and plan**

```powershell
git checkout -b feature/spellfixer-plugin-for-paratext
git add docs/superpowers/specs/2026-10-07-spellfixer-plugin-for-paratext-design.md docs/superpowers/plans/2026-10-07-spellfixer-plugin-for-paratext.md
git commit -m "docs: spec and plan for SpellFixerPluginForParatext`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 1: Extract shared code from ParatextPluginBackTranslationHelper

**Files:**
- Create: `src/ParatextPluginBackTranslationHelper/UsfmChapterTokens.cs`
- Create: `src/ParatextPluginBackTranslationHelper/PtxPluginHelpers.Translatable.cs`
- Create: `src/ParatextPluginBackTranslationHelper/PluginAssemblyResolver.cs`
- Modify: `src/ParatextPluginBackTranslationHelper/PtxPluginHelpers.cs`
- Modify: `src/ParatextPluginBackTranslationHelper/BackTranslationHelperForm.cs` (`CurrentTargetData` getter ~L888-903; private key helpers ~L938-959)
- Modify: `src/ParatextPluginBackTranslationHelper/ParatextBackTranslationHelperPlugin.cs` (ctor and `CurrentDomain_AssemblyResolve`)
- Create test: `src/SILConvertersWordML/TestBwdc/PtxTestTokens.cs`
- Create test: `src/SILConvertersWordML/TestBwdc/UnitTest_UsfmChapterTokens.cs`
- Modify: `src/SILConvertersWordML/TestBwdc/TestBwdc.csproj` (Compile items)

**Interfaces:**
- Produces (namespace `SIL.ParatextBackTranslationHelperPlugin`, `public static class UsfmChapterTokens`):
  - `string GetBookChapterKey(IVerseRef)` returns e.g. `"40_005"`.
  - `string GetBookChapterVerseRangeKey(IVerseRef)` returns e.g. `"40_005_003"` or `"40_005_003-004"`.
  - `string TriangulateBookChapterVerseKey(string bookChapterVerseKey, SortedDictionary<string, List<IUSFMToken>> vrefTokens)`
  - `SortedDictionary<string, List<IUSFMToken>> GroupByVerse(IEnumerable<IUSFMToken> chapterTokens)`
  - `string StartVerseKey(SortedDictionary<string, List<IUSFMToken>> vrefTokens, IVerseRef verseReference)` returns null when nothing is at or after the reference.
  - `string NextVerseKey(SortedDictionary<string, List<IUSFMToken>> vrefTokens, string currentKey)` returns null at the end of the chapter.
- Produces: `public static class PluginAssemblyResolver { public static void Register(Action<string> log); }`
- Produces (TestBwdc, namespace `TestBwdc`): `PtxTestTokens.Vref(int chapter, int verse, int? lastVerse = null)`, `PtxTestTokens.Text(IVerseRef, string, bool publishable = true)`, `PtxTestTokens.Marker(IVerseRef, string marker, MarkerType type)`. All refs are in MAT (book 40).

- [ ] **Step 1: Write the test token builders**

`src/SILConvertersWordML/TestBwdc/PtxTestTokens.cs`:

```csharp
using System.Collections.Generic;
using Paratext.PluginInterfaces;
using SIL.ParatextBackTranslationHelperPlugin;

namespace TestBwdc
{
    /// <summary>
    /// Builders for Paratext USFM tokens to use in tests (all references are in MAT)
    /// </summary>
    internal static class PtxTestTokens
    {
        public static IVerseRef Vref(int chapter, int verse, int? lastVerse = null)
        {
            var vref = new TestVerseReference
            {
                BookCode = "MAT",
                BookNum = 40,
                ChapterNum = chapter,
                VerseNum = verse,
                BBBCCCVVV = (40 * 1000000) + (chapter * 1000) + verse,
                RepresentsMultipleVerses = lastVerse.HasValue,
            };

            var allVerses = new List<IVerseRef>();
            if (lastVerse.HasValue)
            {
                for (var v = verse; v <= lastVerse.Value; v++)
                    allVerses.Add(Vref(chapter, v));
            }
            else
                allVerses.Add(vref);

            vref.AllVerses = allVerses;
            return vref;
        }

        public static IUSFMTextToken Text(IVerseRef vref, string text, bool publishable = true)
        {
            return new TestTextToken(vref, text, publishable);
        }

        public static IUSFMMarkerToken Marker(IVerseRef vref, string marker, MarkerType type)
        {
            return new MarkerToken(vref, isScripture: true, isPublishableVernacular: true, verseOffset: 0)
            {
                Marker = marker,
                Type = type,
            };
        }
    }

    internal class TestTextToken : TokenBase, IUSFMTextToken
    {
        public TestTextToken(IVerseRef vref, string text, bool publishable)
            : base(vref, false, false, false, true, !publishable, publishable)
        {
            Text = text;
        }

        public string Text { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}
```

- [ ] **Step 2: Write the failing UsfmChapterTokens tests**

`src/SILConvertersWordML/TestBwdc/UnitTest_UsfmChapterTokens.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Paratext.PluginInterfaces;
using SIL.ParatextBackTranslationHelperPlugin;
using static TestBwdc.PtxTestTokens;

namespace TestBwdc
{
    [TestFixture]
    public class UnitTest_UsfmChapterTokens
    {
        private static List<IUSFMToken> Chapter5()
        {
            var v0 = Vref(5, 0);
            var v1 = Vref(5, 1);
            var v2 = Vref(5, 2);
            var v34 = Vref(5, 3, 4);
            return new List<IUSFMToken>
            {
                Marker(v0, "c", MarkerType.Chapter), Marker(v0, "s", MarkerType.Paragraph), Text(v0, "Heading"),
                Marker(v1, "p", MarkerType.Paragraph), Marker(v1, "v", MarkerType.Verse), Text(v1, "verse one"),
                Marker(v2, "v", MarkerType.Verse), Text(v2, "verse two"),
                Marker(v34, "v", MarkerType.Verse), Text(v34, "verses three and four"),
            };
        }

        [Test]
        public void GroupByVerse_KeysEachVerseRangeInOrder()
        {
            var vrefTokens = UsfmChapterTokens.GroupByVerse(Chapter5());

            CollectionAssert.AreEqual(new[] { "40_005_000", "40_005_001", "40_005_002", "40_005_003-004" }, vrefTokens.Keys.ToList());
            Assert.AreEqual(2, vrefTokens["40_005_002"].Count);
            Assert.AreEqual("verse two", ((IUSFMTextToken)vrefTokens["40_005_002"][1]).Text);
        }

        [Test]
        public void StartVerseKey_ExactVerse_ReturnsItsKey()
        {
            var vrefTokens = UsfmChapterTokens.GroupByVerse(Chapter5());
            Assert.AreEqual("40_005_002", UsfmChapterTokens.StartVerseKey(vrefTokens, Vref(5, 2)));
        }

        [Test]
        public void StartVerseKey_SingleVerseInsideCombinedVerse_FindsCombinedKey()
        {
            var vrefTokens = UsfmChapterTokens.GroupByVerse(Chapter5());
            Assert.AreEqual("40_005_003-004", UsfmChapterTokens.StartVerseKey(vrefTokens, Vref(5, 4)));
        }

        [Test]
        public void StartVerseKey_VerseMissingFromChapter_ReturnsNextKeyOrNull()
        {
            var vrefTokens = UsfmChapterTokens.GroupByVerse(Chapter5());
            vrefTokens.Remove("40_005_002");

            Assert.AreEqual("40_005_003-004", UsfmChapterTokens.StartVerseKey(vrefTokens, Vref(5, 2)));
            Assert.IsNull(UsfmChapterTokens.StartVerseKey(vrefTokens, Vref(5, 9)));
        }

        [Test]
        public void NextVerseKey_ReturnsFollowingKeyOrNullAtEnd()
        {
            var vrefTokens = UsfmChapterTokens.GroupByVerse(Chapter5());

            Assert.AreEqual("40_005_002", UsfmChapterTokens.NextVerseKey(vrefTokens, "40_005_001"));
            Assert.IsNull(UsfmChapterTokens.NextVerseKey(vrefTokens, "40_005_003-004"));
        }
    }
}
```

Register both files in `TestBwdc.csproj`, after the existing `UnitTest_PtxBackTrHelper.cs` entry:

```xml
    <Compile Include="PtxTestTokens.cs" Condition="'$(Platform)' == 'x64'" />
    <Compile Include="UnitTest_UsfmChapterTokens.cs" Condition="'$(Platform)' == 'x64'" />
```

- [ ] **Step 3: Build TestBwdc to confirm it fails**

```powershell
& $msbuild src\SILConvertersWordML\TestBwdc\TestBwdc.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
```
Expected: compile error `CS0103: The name 'UsfmChapterTokens' does not exist`.

- [ ] **Step 4: Create `UsfmChapterTokens.cs`**

`src/ParatextPluginBackTranslationHelper/UsfmChapterTokens.cs`. The bodies of the first three methods are moved unchanged from `BackTranslationHelperForm`. `GroupByVerse` groups by the computed key string rather than by `IVerseRef`. Two `IVerseRef`s that are equal by value produce the same key, and grouping on the string avoids depending on `IVerseRef` equality (the test `TestVerseReference.Equals` throws).

```csharp
using Paratext.PluginInterfaces;
using System.Collections.Generic;
using System.Linq;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    /// <summary>
    /// Helpers for keying and grouping the USFM tokens of a chapter by verse (also linked into the
    /// SpellFixerPluginForParatext project).
    /// </summary>
    public static class UsfmChapterTokens
    {
        public static string GetBookChapterKey(IVerseRef verseReference)
        {
            // get the key, which for the target data is the entire chapter (we have to Put as a whole chapter)
            return $"{verseReference.BookNum:D2}_{verseReference.ChapterNum:D3}";
        }

        public static string GetBookChapterVerseRangeKey(IVerseRef verseReference)
        {
            // get the key to see if we already have this data (TODO: add a 'it was changed in Ptx', so we can remove it from this collection)
            var bookChapterFirstVerse = $"{verseReference.BookNum:D2}_{verseReference.ChapterNum:D3}_{verseReference.VerseNum:D3}";
            if (verseReference.RepresentsMultipleVerses)
                bookChapterFirstVerse += $"-{verseReference.AllVerses.Last().VerseNum:D3}";
            return bookChapterFirstVerse;
        }

        public static string TriangulateBookChapterVerseKey(string bookChapterVerseKey, SortedDictionary<string, List<IUSFMToken>> vrefTokens)
        {
            if (vrefTokens.ContainsKey(bookChapterVerseKey))
                return bookChapterVerseKey;

            var vrefTokenKey = vrefTokens.FirstOrDefault(t => t.Value.Any(v => v.VerseRef.AllVerses.Any(sv => GetBookChapterVerseRangeKey(sv) == bookChapterVerseKey))).Key;
            return vrefTokenKey;
        }

        /// <summary>
        /// Groups the tokens of a chapter (from IProject.GetUSFMTokens(book, chapter)) by verse (range) key, in verse order
        /// </summary>
        public static SortedDictionary<string, List<IUSFMToken>> GroupByVerse(IEnumerable<IUSFMToken> chapterTokens)
        {
            var dict = chapterTokens.GroupBy(t => GetBookChapterVerseRangeKey(t.VerseRef))
                                    .ToDictionary(g => g.Key, g => g.ToList());
            return new SortedDictionary<string, List<IUSFMToken>>(dict);
        }

        /// <summary>
        /// Returns the key of the verse (range) containing verseReference, or if there isn't one, the key of the
        /// next verse after it (null if there's nothing at or after it in the chapter)
        /// </summary>
        public static string StartVerseKey(SortedDictionary<string, List<IUSFMToken>> vrefTokens, IVerseRef verseReference)
        {
            var bookChapterVerseKey = GetBookChapterVerseRangeKey(verseReference);
            return TriangulateBookChapterVerseKey(bookChapterVerseKey, vrefTokens)
                   ?? NextVerseKey(vrefTokens, bookChapterVerseKey);
        }

        /// <summary>
        /// Returns the key that follows currentKey (null at the end of the chapter)
        /// </summary>
        public static string NextVerseKey(SortedDictionary<string, List<IUSFMToken>> vrefTokens, string currentKey)
        {
            return vrefTokens.Keys.FirstOrDefault(k => string.CompareOrdinal(k, currentKey) > 0);
        }
    }
}
```

- [ ] **Step 5: Make `BackTranslationHelperForm` use it**

In `BackTranslationHelperForm.cs`:
1. Add `using static SIL.ParatextBackTranslationHelperPlugin.UsfmChapterTokens;` after the other `using`s. The existing unqualified calls to `GetBookChapterKey`, `GetBookChapterVerseRangeKey` and `TriangulateBookChapterVerseKey` then bind to the static class.
2. Delete the three private methods `TriangulateBookChapterVerseKey`, `GetBookChapterKey` and `GetBookChapterVerseRangeKey` (~L938-959).
3. In the `CurrentTargetData` getter, replace

```csharp
                    var dict = chapterTokens.GroupBy(t => t.VerseRef, t => t, (key, g) => new { VerseRef = key, USFMTokens = g.ToList() })
                                            .ToDictionary(t => GetBookChapterVerseRangeKey(t.VerseRef), t => t.USFMTokens);
                    vrefTokens = new SortedDictionary<string, List<IUSFMToken>>(dict);
```
with
```csharp
                    vrefTokens = GroupByVerse(chapterTokens);
```

- [ ] **Step 6: Split the settings-dependent part out of `PtxPluginHelpers`**

In `PtxPluginHelpers.cs`, change `internal class PtxPluginHelpers` to `internal partial class PtxPluginHelpers`. Then cut the `_additionalMarkersToTranslate` field and the `IsTranslatable` method (with its comment) and put them in a new file, `src/ParatextPluginBackTranslationHelper/PtxPluginHelpers.Translatable.cs`:

```csharp
using Paratext.PluginInterfaces;
using System.Collections.Generic;
using System.Linq;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    // this part depends on this project's Properties.Settings, so it's kept out of PtxPluginHelpers.cs, which is
    //  also linked into the SpellFixerPluginForParatext project
    internal partial class PtxPluginHelpers
    {
        private static readonly List<string> _additionalMarkersToTranslate = Properties.Settings.Default.AdditionalMarkersToTranslate.Cast<string>().ToList();

        // this would return true for both regular scripture text (i.e.  text after any of these markers:
        // \v, \q[1-3], \m, \pc, etc) and footnote text that is translatable (i.e. \ft)
        public static bool IsTranslatable(IUSFMTextToken token, List<IUSFMToken> tokens)
        {
            PtxPluginHelpers.PreviousToken(token, tokens, out IUSFMMarkerToken mt);
            return (IsScriptureText(token) && (mt.Marker != "va")) ||
                    _additionalMarkersToTranslate.Contains(mt.Marker);
        }
    }
}
```

- [ ] **Step 7: Move the AssemblyResolve hook into `PluginAssemblyResolver.cs`**

`src/ParatextPluginBackTranslationHelper/PluginAssemblyResolver.cs`. The body is the existing `CurrentDomain_AssemblyResolve`, unchanged, except that it logs through a delegate:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    /// <summary>
    /// Hooks AppDomain.AssemblyResolve to load the EncConverters assemblies (also linked into the
    /// SpellFixerPluginForParatext project)
    /// </summary>
    public static class PluginAssemblyResolver
    {
        private static readonly List<string> _assembliesToFindInPluginFolder = new List<string>
        {
            "SilEncConverters40.dll",
            "ECInterfaces.dll",
        };

        private static Action<string> _log;
        private static bool _isRegistered;

        public static void Register(Action<string> log)
        {
            _log = log;
            if (_isRegistered)
                return;

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            _isRegistered = true;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Ignore missing resources
            if (!_assembliesToFindInPluginFolder.Any(s => args.Name.Contains(s)))
                return null;

            try
            {
                var pathToPluginFolder = Assembly.GetExecutingAssembly().Location;
                pathToPluginFolder = Path.Combine(Path.GetDirectoryName(pathToPluginFolder), "SilEncConverters40.dll");
                var asm = Assembly.LoadFrom(pathToPluginFolder);
                var types = asm.GetTypes();

                foreach (var type in types)
                {
                    try
                    {
                        Activator.CreateInstance(type);
                    }
                    catch   // ignore errors
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = $"Unable to load add-in assembly: {Path.GetFileNameWithoutExtension(args.Name)}: {ex.Message}";
                _log?.Invoke(msg);
            }

            return null;
        }
    }
}
```

In `ParatextBackTranslationHelperPlugin.cs`:
- Replace the constructor body with:

```csharp
			_this = this;

			PluginAssemblyResolver.Register(msg => _host?.Log(_this, msg));
```
- Delete the `_assembliesToFindInPluginFolder` field and the `CurrentDomain_AssemblyResolve` method.
- Remove `using` lines that are now unused only if the compiler warns about them; otherwise leave them.

- [ ] **Step 8: Build and run the tests**

```powershell
& $msbuild src\SILConvertersWordML\TestBwdc\TestBwdc.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
& $vstest src\SILConvertersWordML\TestBwdc\bin\x64\Debug\TestBwdc.dll /Platform:x64 /TestCaseFilter:"FullyQualifiedName~UnitTest_UsfmChapterTokens|FullyQualifiedName~UnitTest_PtxBackTrHelper"
```
Expected: all `UnitTest_UsfmChapterTokens` tests pass. `UnitTest_PtxBackTrHelper` gives the same results as before the change: run that filter on `master` first if you're unsure, and compare.

- [ ] **Step 9: Commit**

```powershell
git add src/ParatextPluginBackTranslationHelper src/SILConvertersWordML/TestBwdc/PtxTestTokens.cs src/SILConvertersWordML/TestBwdc/UnitTest_UsfmChapterTokens.cs src/SILConvertersWordML/TestBwdc/TestBwdc.csproj
git commit -m "refactor(PtxBTH): extract UsfmChapterTokens, PluginAssemblyResolver and settings-free PtxPluginHelpers for sharing`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: New project scaffold + SpellFixerTokenProcessor

**Files:**
- Create: `src/SpellFixerPluginForParatext/SpellFixerPluginForParatext.csproj`
- Create: `src/SpellFixerPluginForParatext/Properties/AssemblyInfo.cs`
- Create: `src/SpellFixerPluginForParatext/SpellFixerTokenProcessor.cs`
- Modify: `SEC VS2019.sln`
- Modify: `src/SILConvertersWordML/TestBwdc/TestBwdc.csproj`
- Create test: `src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixer.cs`

**Interfaces:**
- Consumes: `PtxPluginHelpers.IsPublishableVernacular(IUSFMTextToken, List<IUSFMToken>)` and `TextToken(IUSFMTextToken)` (both linked).
- Produces (namespace `SIL.SpellFixerPluginForParatext`):
  - `public delegate string WordFixer(string word);` returns the replacement, or `word` to keep it, or `null` to cancel the run.
  - `public static class SpellFixerTokenProcessor`, with:
    - `char[] WordTerminators`
    - `List<string> SplitIntoRuns(string text)`
    - `bool IsSeparatorRun(string run)`
    - `string FixWords(string text, WordFixer fixWord, ref bool cancelled, out int wordsFixed)`
    - `List<IUSFMToken> FixVerseTokens(List<IUSFMToken> verseTokens, WordFixer fixWord, ref bool cancelled, out int wordsFixed)`
    - `List<IUSFMToken> SpliceVerse(SortedDictionary<string, List<IUSFMToken>> vrefTokens, string verseKey, List<IUSFMToken> newVerseTokens)`
    - `string VerseText(IEnumerable<IUSFMToken> verseTokens)`

- [ ] **Step 1: Create the csproj**

`src/SpellFixerPluginForParatext/SpellFixerPluginForParatext.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <Platforms>AnyCPU;x86;x64</Platforms>
    <ProjectGuid>{6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58}</ProjectGuid>
    <OutputType>Library</OutputType>
    <RootNamespace>SIL.SpellFixerPluginForParatext</RootNamespace>
    <AssemblyName>SpellFixerPluginForParatext</AssemblyName>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <LangVersion>latest</LangVersion>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <UseWindowsForms>true</UseWindowsForms>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Platform)'=='x64'">
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Platform)'=='x86'">
    <PlatformTarget>x86</PlatformTarget>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'">
    <OutputPath>..\..\output\x86\Debug\</OutputPath>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|AnyCPU'">
    <OutputPath>..\..\output\x86\Release\</OutputPath>
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <DefineConstants>TRACE</DefineConstants>
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
  <ItemGroup>
    <!-- nuget doesn't add the references to the csproj files properly for these; manually HintPath'd instead. -->
    <Reference Include="BackTranslationHelper">
      <HintPath>$(EcDistFilesPath)\win-$(Platform)\native\BackTranslationHelper.dll</HintPath>
    </Reference>
    <Reference Include="SpellingFixer30">
      <HintPath>$(EcDistFilesPath)\win-$(Platform)\native\SpellingFixer30.dll</HintPath>
    </Reference>
    <Reference Include="System.Configuration" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.Windows.Forms" />
    <Reference Include="Microsoft.CSharp" />
  </ItemGroup>
  <ItemGroup>
    <!-- shared with (linked from) the sister ParatextPluginBackTranslationHelper project -->
    <Compile Include="..\ParatextPluginBackTranslationHelper\TextToken.cs" Link="Shared\TextToken.cs" />
    <Compile Include="..\ParatextPluginBackTranslationHelper\TokenBase.cs" Link="Shared\TokenBase.cs" />
    <Compile Include="..\ParatextPluginBackTranslationHelper\MarkerToken.cs" Link="Shared\MarkerToken.cs" />
    <Compile Include="..\ParatextPluginBackTranslationHelper\VerseRef.cs" Link="Shared\VerseRef.cs" />
    <Compile Include="..\ParatextPluginBackTranslationHelper\PtxPluginHelpers.cs" Link="Shared\PtxPluginHelpers.cs" />
    <Compile Include="..\ParatextPluginBackTranslationHelper\UsfmChapterTokens.cs" Link="Shared\UsfmChapterTokens.cs" />
    <Compile Include="..\ParatextPluginBackTranslationHelper\PluginAssemblyResolver.cs" Link="Shared\PluginAssemblyResolver.cs" />
    <EmbeddedResource Include="..\ParatextPluginBackTranslationHelper\Resources\pinup.bmp" Link="Resources\pinup.bmp" LogicalName="SIL.SpellFixerPluginForParatext.pinup.bmp" />
    <EmbeddedResource Include="..\ParatextPluginBackTranslationHelper\Resources\pindown.bmp" Link="Resources\pindown.bmp" LogicalName="SIL.SpellFixerPluginForParatext.pindown.bmp" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Encoding-Converters-Core" Version="1.0.2" />
    <PackageReference Include="ParatextEmbeddedUiPluginInterfaces" Version="2.0.23" />
    <PackageReference Include="ParatextPluginInterfaces" Version="2.0.23" />
  </ItemGroup>
  <PropertyGroup>
    <UseEcSharedItems>true</UseEcSharedItems>
  </PropertyGroup>
  <PropertyGroup>
    <PostBuildEvent>IF NOT "$(Configuration)" == "Debug" goto nocopy
IF "%ParatextInstallDir%" == "" goto nocopy
IF NOT EXIST "%ParatextInstallDir%" goto nocopy

IF EXIST "%ParatextInstallDir%\plugins" goto checkMakePluginDir
md "%ParatextInstallDir%\plugins"

:checkMakePluginDir
IF EXIST "%ParatextInstallDir%\plugins\$(MSBuildProjectName)" goto checkdelete
md "%ParatextInstallDir%\plugins\$(MSBuildProjectName)"

:checkdelete
IF NOT EXIST "%ParatextInstallDir%\plugins\$(MSBuildProjectName)\$(MSBuildProjectName).ptxplg" goto nodelete
@echo Deleting '%ParatextInstallDir%\plugins\$(MSBuildProjectName)\$(MSBuildProjectName).ptxplg'
del "%ParatextInstallDir%\plugins\$(MSBuildProjectName)\$(MSBuildProjectName).ptxplg"
:nodelete

@echo Copying files to '%ParatextInstallDir%\Encoding Core' folder
xcopy "$(SolutionDir)output\x64\Debug\*.*" "%ParatextInstallDir%\Encoding Core" /y /i /c
@echo Copying '$(SolutionDir)output\x64\Debug\$(AssemblyName).dll' to: '%ParatextInstallDir%\plugins\$(MSBuildProjectName)\$(MSBuildProjectName).ptxplg'
copy "$(SolutionDir)output\x64\Debug\$(AssemblyName).dll" "%ParatextInstallDir%\plugins\$(MSBuildProjectName)\$(MSBuildProjectName).ptxplg"

:nocopy</PostBuildEvent>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create `Properties/AssemblyInfo.cs`**

```csharp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("SpellFixerPluginForParatext")]
[assembly: AssemblyDescription("Paratext Plug-in to check and fix the spelling of a Paratext project using a SpellFixer (Consistent Spelling Checker or legacy SpellFixer) project")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("SIL")]
[assembly: AssemblyProduct("SpellFixerPluginForParatext")]
[assembly: AssemblyCopyright("Copyright © 2026 SIL. All rights reserved.")]
[assembly: AssemblyTrademark("Copyright © 2026 SIL. All rights reserved.")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: Guid("6c1e5b2a-8f3d-4a7b-9c2e-1d4f7a9b3e58")]

[assembly: AssemblyVersion("4.0.0.0")]
[assembly: AssemblyFileVersion("4.1.0.0")]
```

- [ ] **Step 3: Add the project to `SEC VS2019.sln`**

Insert this right after the `ParatextPluginBackTranslationHelper` `EndProject` line (~L463):

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SpellFixerPluginForParatext", "src\SpellFixerPluginForParatext\SpellFixerPluginForParatext.csproj", "{6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58}"
	ProjectSection(ProjectDependencies) = postProject
		{3998257F-C3E2-4508-9579-70305BDD26F7} = {3998257F-C3E2-4508-9579-70305BDD26F7}
	EndProjectSection
EndProject
```

In the `ParatextBackTranslationHelperPlugin64bitMM` project's `ProjectDependencies` section, add this line next to the `{0BFFA5D9-...}` line:

```
		{6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58} = {6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58}
```

In `GlobalSection(ProjectConfigurationPlatforms)`, add these after the `{0BFFA5D9-...}.Release|x86.ActiveCfg` line:

```
		{6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58}.Debug|x64.ActiveCfg = Debug|x64
		{6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58}.Debug|x64.Build.0 = Debug|x64
		{6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58}.Debug|x86.ActiveCfg = Debug|x86
		{6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58}.Release|x64.ActiveCfg = Release|x64
		{6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58}.Release|x64.Build.0 = Release|x64
		{6C1E5B2A-8F3D-4A7B-9C2E-1D4F7A9B3E58}.Release|x86.ActiveCfg = Release|x86
```

- [ ] **Step 4: Reference the new project from TestBwdc and write the failing tests**

In `TestBwdc.csproj`, next to the existing `ParatextPluginBackTranslationHelper` ProjectReference, add a reference with an extern alias. The alias is needed because the new assembly also contains the linked public `TextToken`, `UsfmChapterTokens` and similar types, in the same namespace as the sister assembly's:

```xml
    <ProjectReference Include="..\..\SpellFixerPluginForParatext\SpellFixerPluginForParatext.csproj" Condition="('$(Platform)' == 'x64') and ('$(OS)'=='Windows_NT')" Aliases="SpellFixer" />
```
and next to the other Compile items:
```xml
    <Compile Include="UnitTest_PtxSpellFixer.cs" Condition="'$(Platform)' == 'x64'" />
```

`src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixer.cs`:

```csharp
extern alias SpellFixer;

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Paratext.PluginInterfaces;
using SIL.ParatextBackTranslationHelperPlugin;
using SpellFixer::SIL.SpellFixerPluginForParatext;
using static TestBwdc.PtxTestTokens;

namespace TestBwdc
{
    [TestFixture]
    public class UnitTest_PtxSpellFixer
    {
        private static string Rebuild(IEnumerable<string> runs) => string.Concat(runs);

        [Test]
        [TestCase("")]
        [TestCase("word")]
        [TestCase(" leading")]
        [TestCase("trailing ")]
        [TestCase("two  spaces")]
        [TestCase("tab\there")]
        [TestCase("no break")]
        [TestCase("line1\r\nline2")]
        [TestCase("  ")]
        public void SplitIntoRuns_RebuildsOriginalExactly(string text)
        {
            Assert.AreEqual(text, Rebuild(SpellFixerTokenProcessor.SplitIntoRuns(text)));
        }

        [Test]
        public void SplitIntoRuns_AlternatesWordsAndSeparators()
        {
            var runs = SpellFixerTokenProcessor.SplitIntoRuns(" a  bc d");
            CollectionAssert.AreEqual(new[] { " ", "a", "  ", "bc", " ", "d" }, runs);
            CollectionAssert.AreEqual(new[] { true, false, true, false, true, false }, runs.Select(SpellFixerTokenProcessor.IsSeparatorRun).ToList());
        }

        [Test]
        public void FixWords_CallsFixerOnlyForWords_AndRebuilds()
        {
            var seen = new List<string>();
            var cancelled = false;
            var result = SpellFixerTokenProcessor.FixWords("Teh  cat sat", w => { seen.Add(w); return w == "Teh" ? "The" : w; }, ref cancelled, out int wordsFixed);

            Assert.AreEqual("The  cat sat", result);
            CollectionAssert.AreEqual(new[] { "Teh", "cat", "sat" }, seen);
            Assert.AreEqual(1, wordsFixed);
            Assert.IsFalse(cancelled);
        }

        [Test]
        public void FixWords_PassesPunctuatedRunWhole()
        {
            var seen = new List<string>();
            var cancelled = false;
            var result = SpellFixerTokenProcessor.FixWords("(teh, cat.", w => { seen.Add(w); return w == "(teh," ? "(the," : w; }, ref cancelled, out _);

            CollectionAssert.AreEqual(new[] { "(teh,", "cat." }, seen);
            Assert.AreEqual("(the, cat.", result);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \r\n ")]
        public void FixWords_NoWords_NeverCallsFixer(string text)
        {
            var cancelled = false;
            var result = SpellFixerTokenProcessor.FixWords(text, w => { Assert.Fail("should not be called"); return w; }, ref cancelled, out int wordsFixed);
            Assert.AreEqual(text, result);
            Assert.AreEqual(0, wordsFixed);
        }

        [Test]
        public void FixWords_NullFromFixer_CancelsAndLeavesRestUnchanged()
        {
            var calls = 0;
            var cancelled = false;
            var result = SpellFixerTokenProcessor.FixWords("teh cat teh", w => { calls++; return calls == 1 ? "the" : null; }, ref cancelled, out int wordsFixed);

            Assert.AreEqual("the cat teh", result);
            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, wordsFixed);
            Assert.IsTrue(cancelled);
        }

        [Test]
        public void FixVerseTokens_ChangesOnlyPublishableTextTokens_KeepingOrderAndInstances()
        {
            var v1 = Vref(5, 1);
            var marker = Marker(v1, "v", MarkerType.Verse);
            var text = Text(v1, "teh cat");
            var footnoteMarker = Marker(v1, "f", MarkerType.Note);
            var notPublishable = Text(v1, "teh note", publishable: false);
            var verseTokens = new List<IUSFMToken> { marker, text, footnoteMarker, notPublishable };

            var cancelled = false;
            var result = SpellFixerTokenProcessor.FixVerseTokens(verseTokens, w => w == "teh" ? "the" : w, ref cancelled, out int wordsFixed);

            Assert.AreEqual(4, result.Count);
            Assert.AreSame(marker, result[0]);
            Assert.AreNotSame(text, result[1]);
            Assert.AreEqual("the cat", ((IUSFMTextToken)result[1]).Text);
            Assert.AreSame(v1, result[1].VerseRef);
            Assert.AreSame(footnoteMarker, result[2]);
            Assert.AreSame(notPublishable, result[3]);
            Assert.AreEqual(1, wordsFixed);
            Assert.AreEqual("teh cat", text.Text, "the original token must not be modified");
        }

        [Test]
        public void FixVerseTokens_NoChanges_ReturnsSameInstances()
        {
            var v1 = Vref(5, 1);
            var verseTokens = new List<IUSFMToken> { Marker(v1, "v", MarkerType.Verse), Text(v1, "the cat") };
            var cancelled = false;
            var result = SpellFixerTokenProcessor.FixVerseTokens(verseTokens, w => w, ref cancelled, out int wordsFixed);

            Assert.AreEqual(0, wordsFixed);
            Assert.AreSame(verseTokens[0], result[0]);
            Assert.AreSame(verseTokens[1], result[1]);
        }

        [Test]
        public void SpliceVerse_ReplacesOnlyThatVerse()
        {
            var v1 = Vref(5, 1);
            var v2 = Vref(5, 2);
            var chapter = new List<IUSFMToken>
            {
                Marker(v1, "v", MarkerType.Verse), Text(v1, "one"),
                Marker(v2, "v", MarkerType.Verse), Text(v2, "teh two"),
            };
            var vrefTokens = UsfmChapterTokens.GroupByVerse(chapter);
            var newVerse2 = new List<IUSFMToken> { chapter[2], Text(v2, "the two") };

            var result = SpellFixerTokenProcessor.SpliceVerse(vrefTokens, "40_005_002", newVerse2);

            Assert.AreEqual(4, result.Count);
            Assert.AreSame(chapter[0], result[0]);
            Assert.AreSame(chapter[1], result[1]);
            Assert.AreSame(chapter[2], result[2]);
            Assert.AreEqual("the two", ((IUSFMTextToken)result[3]).Text);
        }

        [Test]
        public void VerseText_DiffersWhenATextTokenChanges()
        {
            var v1 = Vref(5, 1);
            var before = new List<IUSFMToken> { Marker(v1, "v", MarkerType.Verse), Text(v1, "teh cat") };
            var same = new List<IUSFMToken> { Marker(v1, "v", MarkerType.Verse), Text(v1, "teh cat") };
            var edited = new List<IUSFMToken> { Marker(v1, "v", MarkerType.Verse), Text(v1, "teh dog") };

            Assert.AreEqual(SpellFixerTokenProcessor.VerseText(before), SpellFixerTokenProcessor.VerseText(same));
            Assert.AreNotEqual(SpellFixerTokenProcessor.VerseText(before), SpellFixerTokenProcessor.VerseText(edited));
        }
    }
}
```

- [ ] **Step 5: Build to confirm the tests fail**

```powershell
& $msbuild src\SILConvertersWordML\TestBwdc\TestBwdc.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
```
Expected: compile errors saying `SpellFixerTokenProcessor` does not exist in `SpellFixer::SIL.SpellFixerPluginForParatext`.

- [ ] **Step 6: Implement `SpellFixerTokenProcessor.cs`**

`src/SpellFixerPluginForParatext/SpellFixerTokenProcessor.cs`:

```csharp
using Paratext.PluginInterfaces;
using SIL.ParatextBackTranslationHelperPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Returns the replacement for 'word' (or 'word' itself to leave it alone), or null to cancel the rest of the run
    /// </summary>
    public delegate string WordFixer(string word);

    /// <summary>
    /// The (Paratext UI-independent) bits of splitting text tokens into words, fixing them, and putting the verse
    /// back into the chapter
    /// </summary>
    public static class SpellFixerTokenProcessor
    {
        // same as the word terminators the SILConvertersOffice word-by-word processing uses (minus the Word-specific
        //  footnote and form feed characters)
        public static readonly char[] WordTerminators = { ' ', '\t', '\r', '\n', ' ' };

        /// <summary>
        /// Splits text into alternating runs of word characters and word terminators, such that concatenating the
        /// runs gives back the original text exactly
        /// </summary>
        public static List<string> SplitIntoRuns(string text)
        {
            var runs = new List<string>();
            if (String.IsNullOrEmpty(text))
                return runs;

            var start = 0;
            var inSeparator = IsTerminator(text[0]);
            for (var i = 1; i < text.Length; i++)
            {
                var isSeparator = IsTerminator(text[i]);
                if (isSeparator == inSeparator)
                    continue;

                runs.Add(text.Substring(start, i - start));
                start = i;
                inSeparator = isSeparator;
            }

            runs.Add(text.Substring(start));
            return runs;
        }

        public static bool IsSeparatorRun(string run)
        {
            return !String.IsNullOrEmpty(run) && IsTerminator(run[0]);
        }

        private static bool IsTerminator(char ch)
        {
            return Array.IndexOf(WordTerminators, ch) >= 0;
        }

        /// <summary>
        /// Calls fixWord for each word in text and returns the rebuilt text. If fixWord returns null, 'cancelled'
        /// is set and the rest of the text is left as is.
        /// </summary>
        public static string FixWords(string text, WordFixer fixWord, ref bool cancelled, out int wordsFixed)
        {
            wordsFixed = 0;
            if (cancelled || String.IsNullOrEmpty(text))
                return text;

            var sb = new StringBuilder(text.Length);
            foreach (var run in SplitIntoRuns(text))
            {
                if (cancelled || IsSeparatorRun(run))
                {
                    sb.Append(run);
                    continue;
                }

                var fixedWord = fixWord(run);
                if (fixedWord == null)
                {
                    cancelled = true;
                    sb.Append(run);
                    continue;
                }

                if (fixedWord != run)
                    wordsFixed++;
                sb.Append(fixedWord);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns a new list of the verse's tokens in which each publishable vernacular text token that had a word
        /// fixed is replaced by a new TextToken (all other tokens are the same instances)
        /// </summary>
        public static List<IUSFMToken> FixVerseTokens(List<IUSFMToken> verseTokens, WordFixer fixWord, ref bool cancelled, out int wordsFixed)
        {
            wordsFixed = 0;
            var result = new List<IUSFMToken>(verseTokens.Count);
            foreach (var token in verseTokens)
            {
                if (!cancelled && (token is IUSFMTextToken textToken) && PtxPluginHelpers.IsPublishableVernacular(textToken, verseTokens))
                {
                    var newText = FixWords(textToken.Text, fixWord, ref cancelled, out int wordsFixedInToken);
                    if (newText != textToken.Text)
                    {
                        result.Add(new TextToken(textToken) { Text = newText });
                        wordsFixed += wordsFixedInToken;
                        continue;
                    }
                }

                result.Add(token);
            }

            return result;
        }

        /// <summary>
        /// Returns all of the chapter's tokens (in order) with the tokens for verseKey replaced by newVerseTokens
        /// (i.e. what to pass to IProject.PutUSFMTokens)
        /// </summary>
        public static List<IUSFMToken> SpliceVerse(SortedDictionary<string, List<IUSFMToken>> vrefTokens, string verseKey, List<IUSFMToken> newVerseTokens)
        {
            return vrefTokens.SelectMany(kvp => (kvp.Key == verseKey) ? newVerseTokens : kvp.Value)
                             .ToList();
        }

        /// <summary>
        /// The text of all the text tokens of a verse (to detect whether it was changed in Paratext since we read it)
        /// </summary>
        public static string VerseText(IEnumerable<IUSFMToken> verseTokens)
        {
            return String.Join("\n", verseTokens.OfType<IUSFMTextToken>().Select(t => t.Text));
        }
    }
}
```

- [ ] **Step 7: Build and run the tests**

```powershell
& $msbuild src\SpellFixerPluginForParatext\SpellFixerPluginForParatext.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
& $msbuild src\SILConvertersWordML\TestBwdc\TestBwdc.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
& $vstest src\SILConvertersWordML\TestBwdc\bin\x64\Debug\TestBwdc.dll /Platform:x64 /TestCaseFilter:"FullyQualifiedName~UnitTest_PtxSpellFixer"
```
Expected: the new project builds with 0 errors, and every `UnitTest_PtxSpellFixer` test passes.

- [ ] **Step 8: Commit**

```powershell
git add src/SpellFixerPluginForParatext "SEC VS2019.sln" src/SILConvertersWordML/TestBwdc/TestBwdc.csproj src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixer.cs
git commit -m "feat(SpellFixerPluginForParatext): new plugin project with word split/fix/splice token processor`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: SpellFixerSession (decision state machine)

**Files:**
- Create: `src/SpellFixerPluginForParatext/SpellFixerSession.cs`
- Modify test: `src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixer.cs`

**Interfaces:**
- Produces (namespace `SIL.SpellFixerPluginForParatext`):
  - `public enum FormButtons { None, Next, ReplaceOnce, ReplaceEvery, ReplaceAll, Cancel, Redo, Copy }`. Copied from SILConvertersOffice; `Next` means Skip.
  - `public delegate FormButtons AskUser(string word, string suggestion, out string correctedSpelling);`
  - `public class SpellFixerSession`, with:
    - ctor `(Func<string, string> convert, AskUser askUser)`
    - `string FixWord(string word)`, which matches `WordFixer`
    - `bool IsReplaceAll { get; }`

- [ ] **Step 1: Write the failing tests**

Add this nested helper and these tests inside `UnitTest_PtxSpellFixer`:

```csharp
        private class FakeUser
        {
            private readonly Queue<(FormButtons Button, string Corrected)> _answers = new Queue<(FormButtons, string)>();
            public List<(string Word, string Suggestion)> Asked { get; } = new List<(string, string)>();

            public FakeUser Then(FormButtons button, string corrected)
            {
                _answers.Enqueue((button, corrected));
                return this;
            }

            public FormButtons Ask(string word, string suggestion, out string correctedSpelling)
            {
                Asked.Add((word, suggestion));
                var (button, corrected) = _answers.Dequeue();
                correctedSpelling = corrected;
                return button;
            }
        }

        private static string FixTeh(string word) => (word == "teh") ? "the" : word;

        [Test]
        public void FixWord_UnchangedByConverter_DoesNotAsk()
        {
            var user = new FakeUser();
            var session = new SpellFixerSession(FixTeh, user.Ask);
            Assert.AreEqual("cat", session.FixWord("cat"));
            Assert.IsEmpty(user.Asked);
        }

        [Test]
        public void FixWord_Skip_KeepsWord()
        {
            var user = new FakeUser().Then(FormButtons.Next, "the");
            var session = new SpellFixerSession(FixTeh, user.Ask);
            Assert.AreEqual("teh", session.FixWord("teh"));
            CollectionAssert.AreEqual(new[] { ("teh", "the") }, user.Asked);
        }

        [Test]
        public void FixWord_ReplaceOnce_UsesCorrectedSpellingAndAsksAgainNextTime()
        {
            var user = new FakeUser().Then(FormButtons.ReplaceOnce, "thee").Then(FormButtons.ReplaceOnce, "the");
            var session = new SpellFixerSession(FixTeh, user.Ask);
            Assert.AreEqual("thee", session.FixWord("teh"));
            Assert.AreEqual("the", session.FixWord("teh"));
            Assert.AreEqual(2, user.Asked.Count);
        }

        [Test]
        public void FixWord_ReplaceEvery_ReusesUsersEditedSpellingWithoutAsking()
        {
            var user = new FakeUser().Then(FormButtons.ReplaceEvery, "thee");
            var session = new SpellFixerSession(FixTeh, user.Ask);
            Assert.AreEqual("thee", session.FixWord("teh"));
            Assert.AreEqual("thee", session.FixWord("teh"));
            Assert.AreEqual(1, user.Asked.Count);
        }

        [Test]
        public void FixWord_ReplaceAll_StopsAskingForOtherWordsToo()
        {
            var user = new FakeUser().Then(FormButtons.ReplaceAll, "the");
            var session = new SpellFixerSession(w => w == "teh" ? "the" : (w == "adn" ? "and" : w), user.Ask);
            Assert.AreEqual("the", session.FixWord("teh"));
            Assert.AreEqual("and", session.FixWord("adn"));
            Assert.IsTrue(session.IsReplaceAll);
            Assert.AreEqual(1, user.Asked.Count);
        }

        [Test]
        [TestCase(FormButtons.Cancel)]
        [TestCase(FormButtons.None)]   // e.g. closed with the X
        public void FixWord_CancelOrClosed_ReturnsNull(FormButtons button)
        {
            var user = new FakeUser().Then(button, "the");
            var session = new SpellFixerSession(FixTeh, user.Ask);
            Assert.IsNull(session.FixWord("teh"));
        }

        [Test]
        public void FixWord_EmptyCorrection_IsAReplacementNotACancel()
        {
            var user = new FakeUser().Then(FormButtons.ReplaceOnce, "");
            var session = new SpellFixerSession(FixTeh, user.Ask);
            Assert.AreEqual("", session.FixWord("teh"));
        }

        [Test]
        public void FixWord_NullCorrection_FallsBackToSuggestion()
        {
            var user = new FakeUser().Then(FormButtons.ReplaceOnce, null);
            var session = new SpellFixerSession(FixTeh, user.Ask);
            Assert.AreEqual("the", session.FixWord("teh"));
        }

        [Test]
        public void Session_DrivesFixWordsEndToEnd()
        {
            var user = new FakeUser().Then(FormButtons.ReplaceEvery, "the");
            var session = new SpellFixerSession(FixTeh, user.Ask);
            var cancelled = false;
            var result = SpellFixerTokenProcessor.FixWords("teh cat and teh dog", session.FixWord, ref cancelled, out int wordsFixed);
            Assert.AreEqual("the cat and the dog", result);
            Assert.AreEqual(2, wordsFixed);
        }
```

- [ ] **Step 2: Build to confirm it fails**

```powershell
& $msbuild src\SILConvertersWordML\TestBwdc\TestBwdc.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
```
Expected: compile errors saying `SpellFixerSession` and `FormButtons` don't exist.

- [ ] **Step 3: Implement `SpellFixerSession.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace SIL.SpellFixerPluginForParatext
{
    // copied from SILConvertersOffice (BaseConverterForm.cs). 'Next' is the "Skip" button
    public enum FormButtons
    {
        None,
        Next,
        ReplaceOnce,
        ReplaceEvery,
        ReplaceAll,
        Cancel,
        Redo,
        Copy
    }

    /// <summary>
    /// Asks the user what to do about 'word', which the SpellFixer would change to 'suggestion'. 'correctedSpelling'
    /// is what's in the "Replace with" box when they click a button (they may have edited it)
    /// </summary>
    public delegate FormButtons AskUser(string word, string suggestion, out string correctedSpelling);

    /// <summary>
    /// Keeps track of the user's decisions during one checking run (e.g. Replace Every and Replace All)
    /// </summary>
    public class SpellFixerSession
    {
        private readonly Func<string, string> _convert;
        private readonly AskUser _askUser;
        private readonly Dictionary<string, string> _replaceEvery = new Dictionary<string, string>();

        public SpellFixerSession(Func<string, string> convert, AskUser askUser)
        {
            _convert = convert;
            _askUser = askUser;
        }

        public bool IsReplaceAll { get; private set; }

        /// <summary>
        /// Returns what 'word' should become (itself if unchanged), or null if the user cancelled
        /// </summary>
        public string FixWord(string word)
        {
            if (_replaceEvery.TryGetValue(word, out string everyReplacement))
                return everyReplacement;

            var suggestion = _convert(word);
            if (suggestion == word)
                return word;

            if (IsReplaceAll)
                return suggestion;

            var button = _askUser(word, suggestion, out string correctedSpelling);
            correctedSpelling ??= suggestion;
            switch (button)
            {
                case FormButtons.ReplaceOnce:
                    return correctedSpelling;

                case FormButtons.ReplaceEvery:
                    _replaceEvery[word] = correctedSpelling;
                    return correctedSpelling;

                case FormButtons.ReplaceAll:
                    IsReplaceAll = true;
                    return correctedSpelling;

                case FormButtons.Cancel:
                case FormButtons.None:
                    return null;

                default:    // Next (i.e. Skip)
                    return word;
            }
        }
    }
}
```

- [ ] **Step 4: Build and run the tests**

```powershell
& $msbuild src\SILConvertersWordML\TestBwdc\TestBwdc.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
& $vstest src\SILConvertersWordML\TestBwdc\bin\x64\Debug\TestBwdc.dll /Platform:x64 /TestCaseFilter:"FullyQualifiedName~UnitTest_PtxSpellFixer"
```
Expected: all pass.

- [ ] **Step 5: Commit**

```powershell
git add src/SpellFixerPluginForParatext/SpellFixerSession.cs src/SILConvertersWordML/TestBwdc/UnitTest_PtxSpellFixer.cs
git commit -m "feat(SpellFixerPluginForParatext): SpellFixerSession for Skip/Replace Once/Every/All/Cancel decisions`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: SpellFixerProject wrapper + query dialog (copied from SILConvertersOffice)

**Files:**
- Create: `src/SpellFixerPluginForParatext/SpellFixerProject.cs`
- Create (copy, then edit): `src/SpellFixerPluginForParatext/SpellFixerQueryFormBase.cs`, `.Designer.cs`, `.resx` (from `src/SILConvertersOffice/BaseConverterForm.*`)
- Create (copy, then edit): `src/SpellFixerPluginForParatext/SpellFixerQueryForm.cs`, `.Designer.cs`, `.resx` (from `src/SILConvertersOffice/SILConverterProcessorForm.*`)

**Interfaces:**
- Consumes: `FormButtons` (Task 3); `BackTranslationHelper.FindReplaceHelper` (EC core): `GetFindReplaceHelper()`, `FindReplaceHelper(string)`, `SpellFixerEncConverterName`, `AssignCorrectSpelling(string)`, `FindReplacementRule(string)`, `EditSpellingFixes()`.
- Produces:
  - `internal class SpellFixerProject`:
    - `static SpellFixerProject QueryUser()` (null if none chosen) and `static SpellFixerProject FromConverterName(string)` (throws if it can't be loaded).
    - `string ConverterName`, `DirectableEncConverter Converter`, `bool IsWholeWord`, `string DisplayName`.
    - `string Convert(string)`.
    - `void AssignCorrectSpelling(string)`, `void FindReplacementRule(string)`, `void EditSpellingFixes()`. Each reloads the converter afterwards.
  - `internal partial class SpellFixerQueryForm : SpellFixerQueryFormBase`:
    - ctor `(SpellFixerProject project)`.
    - `FormButtons Show(IWin32Window owner, Font font, bool rightToLeft, string word, string suggestion)`.
    - `string CorrectedSpelling { get; }`.

- [ ] **Step 1: Write `SpellFixerProject.cs`**

```csharp
using BackTranslationHelper;
using ECInterfaces;
using SilEncConverters40;
using SpellingFixer30;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// The SpellFixer project (either a whole word Consistent Spelling Checker project or a (partial word) legacy
    /// SpellFixer project) the user chose, and the EncConverter used to check words with it
    /// </summary>
    internal class SpellFixerProject
    {
        private readonly FindReplaceHelper _findReplaceHelper;

        private SpellFixerProject(FindReplaceHelper findReplaceHelper)
        {
            _findReplaceHelper = findReplaceHelper;
            ConverterName = findReplaceHelper.SpellFixerEncConverterName;
            ReloadConverter();
        }

        /// <summary>
        /// Asks the user which kind of project to use (whole word or partial word) and which project. Returns null if
        /// they didn't choose one.
        /// </summary>
        public static SpellFixerProject QueryUser()
        {
            var findReplaceHelper = FindReplaceHelper.GetFindReplaceHelper();
            return (findReplaceHelper == null) ? null : new SpellFixerProject(findReplaceHelper);
        }

        /// <summary>
        /// Loads a previously chosen project by its EncConverter name (e.g. "Consistent Spelling for xyz"); throws if
        /// it can't be loaded
        /// </summary>
        public static SpellFixerProject FromConverterName(string converterName)
        {
            return new SpellFixerProject(new FindReplaceHelper(converterName));
        }

        public string ConverterName { get; }

        public DirectableEncConverter Converter { get; private set; }

        public bool IsWholeWord => ConverterName.StartsWith(SpellingFixer.SFConverterPrefixCsc);

        public string DisplayName => IsWholeWord
                                        ? $"Whole word (CSC): {ConverterName.Substring(SpellingFixer.SFConverterPrefixCsc.Length)}"
                                        : $"Partial word (legacy): {ConverterName.Substring(SpellingFixer.SFConverterPrefix.Length)}";

        public string Convert(string word)
        {
            return Converter.Convert(word);
        }

        public void AssignCorrectSpelling(string word)
        {
            _findReplaceHelper.AssignCorrectSpelling(word);
            ReloadConverter();
        }

        public void FindReplacementRule(string word)
        {
            _findReplaceHelper.FindReplacementRule(word);
            ReloadConverter();
        }

        public void EditSpellingFixes()
        {
            _findReplaceHelper.EditSpellingFixes();
            ReloadConverter();
        }

        // so that rules just added or edited are used for the next word checked
        public void ReloadConverter()
        {
            DirectableEncConverter.EncConverters.Reinitialize();
            Converter = new DirectableEncConverter(ConverterName, bDirectionForward: true, NormalizeFlags.None);
        }
    }
}
```

- [ ] **Step 2: Copy the Office form files**

```powershell
Copy-Item src\SILConvertersOffice\BaseConverterForm.designer.cs src\SpellFixerPluginForParatext\SpellFixerQueryFormBase.Designer.cs
Copy-Item src\SILConvertersOffice\BaseConverterForm.resx src\SpellFixerPluginForParatext\SpellFixerQueryFormBase.resx
Copy-Item src\SILConvertersOffice\SILConverterProcessorForm.designer.cs src\SpellFixerPluginForParatext\SpellFixerQueryForm.Designer.cs
Copy-Item src\SILConvertersOffice\SILConverterProcessorForm.resx src\SpellFixerPluginForParatext\SpellFixerQueryForm.resx
```

- [ ] **Step 3: Rename inside the copied designer files**

In `SpellFixerQueryFormBase.Designer.cs`:
- `namespace SILConvertersOffice` becomes `namespace SIL.SpellFixerPluginForParatext`.
- `partial class BaseConverterForm` becomes `partial class SpellFixerQueryFormBase`.
- `typeof(BaseConverterForm)` becomes `typeof(SpellFixerQueryFormBase)`.
- `this.Name = "BaseConverterForm";` becomes `this.Name = "SpellFixerQueryFormBase";`.
- `this.Text = "SIL Converters";` becomes `this.Text = "Spell Fixer for Paratext";`.
- `this.textBoxConverted_TextChanged` stays as it is. Only its body, in the .cs file, changes.

In `SpellFixerQueryForm.Designer.cs`:
- `namespace SILConvertersOffice` becomes `namespace SIL.SpellFixerPluginForParatext`.
- `internal partial class SILConverterProcessorForm` becomes `internal partial class SpellFixerQueryForm`.
- `this.Name = "SILConverterProcessorForm";` becomes `this.Name = "SpellFixerQueryForm";`.
- If it contains `typeof(SILConverterProcessorForm)`, that becomes `typeof(SpellFixerQueryForm)`.

PowerShell to do it:

```powershell
$f = 'src\SpellFixerPluginForParatext\SpellFixerQueryFormBase.Designer.cs'
(Get-Content $f -Raw) -replace 'namespace SILConvertersOffice','namespace SIL.SpellFixerPluginForParatext' `
  -replace 'class BaseConverterForm','class SpellFixerQueryFormBase' `
  -replace 'typeof\(BaseConverterForm\)','typeof(SpellFixerQueryFormBase)' `
  -replace '"BaseConverterForm"','"SpellFixerQueryFormBase"' `
  -replace 'this\.Text = "SIL Converters";','this.Text = "Spell Fixer for Paratext";' | Set-Content $f -Encoding utf8
$f = 'src\SpellFixerPluginForParatext\SpellFixerQueryForm.Designer.cs'
(Get-Content $f -Raw) -replace 'namespace SILConvertersOffice','namespace SIL.SpellFixerPluginForParatext' `
  -replace 'class SILConverterProcessorForm','class SpellFixerQueryForm' `
  -replace 'typeof\(SILConverterProcessorForm\)','typeof(SpellFixerQueryForm)' `
  -replace '"SILConverterProcessorForm"','"SpellFixerQueryForm"' | Set-Content $f -Encoding utf8
```

- [ ] **Step 4: Write `SpellFixerQueryFormBase.cs`**

This is the Office `BaseConverterForm.cs`, adapted. `FontConverter` is replaced by a `DirectableEncConverter` plus a Font, `ForwardString` is renamed `CorrectedSpelling`, `Show` takes an owner, and the `IBaseConverterForm` interface, `FormButtons` (now in `SpellFixerSession.cs`) and the legacy-font Set*Font members are dropped.

```csharp
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using SilEncConverters40;

namespace SIL.SpellFixerPluginForParatext
{
    // adapted from SILConvertersOffice's BaseConverterForm
    internal partial class SpellFixerQueryFormBase : Form
    {
        protected DirectableEncConverter m_aEC;

        public SpellFixerQueryFormBase()
        {
            InitializeComponent();

            // we only ever ask about words the SpellFixer would change (and the checkbox is private to this class)
            checkBoxSkipIdenticalForms.Visible = false;
        }

        public virtual FormButtons Show
            (
            IWin32Window owner,
            DirectableEncConverter aEC,
            Font font,
            bool rightToLeft,
            string strInput,
            string strOutput
            )
        {
            m_aEC = aEC;
            ButtonPressed = FormButtons.None;

            if (font != null)
                textBoxInput.Font = textBoxConverted.Font = font;

            textBoxInput.RightToLeft = textBoxConverted.RightToLeft = rightToLeft ? RightToLeft.Yes : RightToLeft.No;

            InputString = strInput;
            CorrectedSpelling = strOutput;

            UpdateLhsUniCodes(InputString, this.labelInputCodePoints);
            UpdateRhsUniCodes(CorrectedSpelling, this.labelForwardCodePoints);

            // get some info to show in the title bar
            this.Text = String.Format("{0}: {1}", SpellFixerPlugin.PluginName, m_aEC.ToString());

            ShowDialog(owner);

            return ButtonPressed;
        }

        public string InputString
        {
            get { return this.textBoxInput.Text; }
            set { this.textBoxInput.Text = value; }
        }

        public string CorrectedSpelling
        {
            get { return this.textBoxConverted.Text; }
            set { this.textBoxConverted.Text = value; }
        }

        protected FormButtons m_btnPressed = FormButtons.None;
        public FormButtons ButtonPressed
        {
            get { return m_btnPressed; }
            set { m_btnPressed = value; }
        }

        protected void UpdateLegacyCodes(string strInputString, int cp, Label lableUniCodes)
        {
            // to get the real byte values, we need to first convert it using the def code page
            byte[] aby = EncConverters.GetBytesFromEncoding(cp, strInputString, true);
            string strWhole = null;
            foreach (byte by in aby)
                strWhole += String.Format("{0:D3} ", (int)by);

            lableUniCodes.Text = strWhole;
        }

        protected void UpdateUniCodes(string strInputString, Label lableUniCodes)
        {
            string strWhole = null;
            foreach (char ch in strInputString)
                strWhole += String.Format("{0:X4} ", (int)ch);

            lableUniCodes.Text = strWhole;
        }

        protected void UpdateLhsUniCodes(string strInputString, Label lableUniCodes)
        {
            if (m_aEC == null)
                return;

            // use the EncConverter to determine if this field is Legacy or not.
            if (m_aEC.IsLhsLegacy)
                UpdateLegacyCodes(strInputString, m_aEC.GetEncConverter.CodePageInput, lableUniCodes);
            else
                UpdateUniCodes(strInputString, lableUniCodes);
        }

        protected void UpdateRhsUniCodes(string strInputString, Label lableUniCodes)
        {
            if (m_aEC == null)
                return;

            // use the EncConverter to determine if this field is Legacy or not.
            if (m_aEC.IsRhsLegacy)
                UpdateLegacyCodes(strInputString, m_aEC.GetEncConverter.CodePageOutput, lableUniCodes);
            else
                UpdateUniCodes(strInputString, lableUniCodes);
        }

        protected void buttonNextWord_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.Next;
            this.Close();
        }

        protected void buttonReplaceOnce_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.ReplaceOnce;
            this.Close();
        }

        private void buttonReplaceEvery_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.ReplaceEvery;
            this.Close();
        }

        private void buttonReplaceAll_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.ReplaceAll;
            this.Close();
        }

        protected void buttonCancel_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.Cancel;
            Close();
        }

        protected void textBoxInput_TextChanged(object sender, EventArgs e)
        {
            UpdateLhsUniCodes(InputString, this.labelInputCodePoints);
        }

        void textBoxConverted_TextChanged(object sender, System.EventArgs e)
        {
            UpdateRhsUniCodes(CorrectedSpelling, this.labelForwardCodePoints);
        }

        // allow these to be overidden by sub-class forms
        protected virtual void RefreshTextBoxes(DirectableEncConverter aEC)
        {
            CorrectedSpelling = aEC.Convert(InputString);
        }

        // keep track of the last textbox that was right-click'd in, so we can handle the ChangeFont request
        protected EcTextBox m_tbLastClicked = null;
        private void contextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            ContextMenuStrip aCMS = (ContextMenuStrip)sender;
            m_tbLastClicked = (EcTextBox)aCMS.SourceControl;
            right2LeftToolStripMenuItem.Checked = (m_tbLastClicked.RightToLeft == RightToLeft.Yes);
        }

        private void changeFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
            {
                fontDialog.Font = m_tbLastClicked.Font;
                if (fontDialog.ShowDialog() == DialogResult.OK)
                    m_tbLastClicked.Font = fontDialog.Font;
            }
        }

        private void right2LeftToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
            {
                ToolStripMenuItem aMenuItem = (ToolStripMenuItem)sender;
                m_tbLastClicked.RightToLeft = (aMenuItem.Checked) ? RightToLeft.Yes : RightToLeft.No;
            }
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
                m_tbLastClicked.Undo();
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
            {
                if (m_tbLastClicked.SelectionLength == 0)
                    m_tbLastClicked.SelectAll();
                m_tbLastClicked.Cut();
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
            {
                if (m_tbLastClicked.SelectionLength == 0)
                    m_tbLastClicked.SelectAll();
                m_tbLastClicked.Copy();
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
                m_tbLastClicked.Paste();
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
                m_tbLastClicked.Clear();
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
                m_tbLastClicked.SelectAll();
        }
    }
}
```

- [ ] **Step 5: Write `SpellFixerQueryForm.cs`**

This is the Office `SILConverterProcessorForm.cs`, adapted to use the main window's `SpellFixerProject` instead of logging in again.

```csharp
using ECInterfaces;
using SilEncConverters40;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SIL.SpellFixerPluginForParatext
{
    // adapted from SILConvertersOffice's SILConverterProcessorForm
    internal partial class SpellFixerQueryForm : SpellFixerQueryFormBase
    {
        private readonly SpellFixerProject _spellFixerProject;

        public SpellFixerQueryForm(SpellFixerProject spellFixerProject)
        {
            InitializeComponent();
            _spellFixerProject = spellFixerProject;
        }

        public FormButtons Show(IWin32Window owner, Font font, bool rightToLeft, string word, string suggestion)
        {
            return Show(owner, _spellFixerProject.Converter, font, rightToLeft, word, suggestion);
        }

        private void buttonDebug_Click(object sender, EventArgs e)
        {
            IEncConverter aIEC = m_aEC?.GetEncConverter;
            if (aIEC != null)
            {
                bool bOrigValue = aIEC.Debug;
                aIEC.Debug = true;

                RefreshTextBoxes(m_aEC);

                aIEC.Debug = bOrigValue;
            }
        }

        protected void buttonRefresh_Click(object sender, EventArgs e)
        {
            RefreshTextBoxes(m_aEC);
        }

        private void buttonViewRule_Click(object sender, EventArgs e)
        {
            TryAction(() => _spellFixerProject.FindReplacementRule(textBoxInput.Text));
        }

        private void buttonAddRule_Click(object sender, EventArgs e)
        {
            TryAction(() => _spellFixerProject.AssignCorrectSpelling(textBoxInput.Text));
        }

        private void TryAction(Action action)
        {
            try
            {
                action();

                // the project reloaded its converter, so use the new one
                m_aEC = _spellFixerProject.Converter;
                RefreshTextBoxes(m_aEC);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, SpellFixerPlugin.PluginName);
            }
        }
    }
}
```

- [ ] **Step 6: Build**

`SpellFixerPlugin.PluginName` is defined in Task 5. To build now, add this stub file. Task 5 Step 3 replaces its contents:

`src/SpellFixerPluginForParatext/SpellFixerPlugin.cs`:
```csharp
namespace SIL.SpellFixerPluginForParatext
{
    public class SpellFixerPlugin
    {
        public const string PluginName = "Spell Fixer";
    }
}
```

```powershell
& $msbuild src\SpellFixerPluginForParatext\SpellFixerPluginForParatext.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
```
Expected: 0 errors.
- If a designer file references a handler or control that no longer exists, keep the designer and restore the member in the .cs file, matching the Office original.
- If `FindReplaceHelper`'s `(string)` ctor or `GetFindReplaceHelper` is missing from the 1.0.2 package, stop and report it. The spec says not to change EC core.

- [ ] **Step 7: Commit**

```powershell
git add src/SpellFixerPluginForParatext
git commit -m "feat(SpellFixerPluginForParatext): SpellFixerProject wrapper and query dialog copied from SILConvertersOffice`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: Plugin entry point, settings, and main window (without the checking loop)

**Files:**
- Modify (replace the stub): `src/SpellFixerPluginForParatext/SpellFixerPlugin.cs`
- Create: `src/SpellFixerPluginForParatext/Properties/Settings.settings`
- Create: `src/SpellFixerPluginForParatext/Properties/Settings.Designer.cs`
- Create: `src/SpellFixerPluginForParatext/SpellFixerForm.cs`
- Create: `src/SpellFixerPluginForParatext/SpellFixerForm.Designer.cs`

**Interfaces:**
- Consumes: `SpellFixerProject` (Task 4), `PluginAssemblyResolver.Register` (Task 1).
- Produces:
  - `public class SpellFixerPlugin : IParatextStandalonePlugin` with `public const string PluginName = "Spell Fixer"`.
  - `internal partial class SpellFixerForm : Form` with ctor `(IPluginHost host, IPluginObject plugin, IProject project, IVerseRef initialVerseReference, Action<IVerseRef> setSyncReference)`.
  - `Properties.Settings.Default`: `UpgradeSettings` (bool, default True), `MapPtxProjectToSpellFixerConverter` (StringCollection), `PinToTop` (bool, default False), `WindowLocation` (Point, default 0,0).

- [ ] **Step 1: Settings**

`Properties/Settings.settings`:

```xml
<?xml version='1.0' encoding='utf-8'?>
<SettingsFile xmlns="http://schemas.microsoft.com/VisualStudio/2004/01/settings" CurrentProfile="(Default)" GeneratedClassNamespace="SIL.SpellFixerPluginForParatext.Properties" GeneratedClassName="Settings">
  <Profiles />
  <Settings>
    <Setting Name="UpgradeSettings" Type="System.Boolean" Scope="User">
      <Value Profile="(Default)">True</Value>
    </Setting>
    <Setting Name="MapPtxProjectToSpellFixerConverter" Type="System.Collections.Specialized.StringCollection" Scope="User">
      <Value Profile="(Default)" />
    </Setting>
    <Setting Name="PinToTop" Type="System.Boolean" Scope="User">
      <Value Profile="(Default)">False</Value>
    </Setting>
    <Setting Name="WindowLocation" Type="System.Drawing.Point" Scope="User">
      <Value Profile="(Default)">0, 0</Value>
    </Setting>
  </Settings>
</SettingsFile>
```

`Properties/Settings.Designer.cs`:

```csharp
namespace SIL.SpellFixerPluginForParatext.Properties {

    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {

        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));

        public static Settings Default {
            get {
                return defaultInstance;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool UpgradeSettings {
            get {
                return ((bool)(this["UpgradeSettings"]));
            }
            set {
                this["UpgradeSettings"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::System.Collections.Specialized.StringCollection MapPtxProjectToSpellFixerConverter {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["MapPtxProjectToSpellFixerConverter"]));
            }
            set {
                this["MapPtxProjectToSpellFixerConverter"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool PinToTop {
            get {
                return ((bool)(this["PinToTop"]));
            }
            set {
                this["PinToTop"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0, 0")]
        public global::System.Drawing.Point WindowLocation {
            get {
                return ((global::System.Drawing.Point)(this["WindowLocation"]));
            }
            set {
                this["WindowLocation"] = value;
            }
        }
    }
}
```

- [ ] **Step 2: `SpellFixerForm.Designer.cs`**

```csharp
namespace SIL.SpellFixerPluginForParatext
{
    partial class SpellFixerForm
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
            this.labelPtxProjectCaption = new System.Windows.Forms.Label();
            this.labelPtxProject = new System.Windows.Forms.Label();
            this.buttonPinToTop = new System.Windows.Forms.Button();
            this.labelSpellFixerProjectCaption = new System.Windows.Forms.Label();
            this.labelSpellFixerProject = new System.Windows.Forms.Label();
            this.buttonChooseProject = new System.Windows.Forms.Button();
            this.labelVerseCaption = new System.Windows.Forms.Label();
            this.labelVerse = new System.Windows.Forms.Label();
            this.buttonCheck = new System.Windows.Forms.Button();
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonAssignCorrectSpelling = new System.Windows.Forms.Button();
            this.buttonFindReplacementRule = new System.Windows.Forms.Button();
            this.buttonEditSpellingFixes = new System.Windows.Forms.Button();
            this.textBoxStatus = new System.Windows.Forms.TextBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel.SuspendLayout();
            this.flowLayoutPanelButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanel
            //
            this.tableLayoutPanel.ColumnCount = 3;
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.Controls.Add(this.labelPtxProjectCaption, 0, 0);
            this.tableLayoutPanel.Controls.Add(this.labelPtxProject, 1, 0);
            this.tableLayoutPanel.Controls.Add(this.buttonPinToTop, 2, 0);
            this.tableLayoutPanel.Controls.Add(this.labelSpellFixerProjectCaption, 0, 1);
            this.tableLayoutPanel.Controls.Add(this.labelSpellFixerProject, 1, 1);
            this.tableLayoutPanel.Controls.Add(this.buttonChooseProject, 2, 1);
            this.tableLayoutPanel.Controls.Add(this.labelVerseCaption, 0, 2);
            this.tableLayoutPanel.Controls.Add(this.labelVerse, 1, 2);
            this.tableLayoutPanel.Controls.Add(this.buttonCheck, 2, 2);
            this.tableLayoutPanel.Controls.Add(this.flowLayoutPanelButtons, 0, 3);
            this.tableLayoutPanel.Controls.Add(this.textBoxStatus, 0, 4);
            this.tableLayoutPanel.SetColumnSpan(this.flowLayoutPanelButtons, 3);
            this.tableLayoutPanel.SetColumnSpan(this.textBoxStatus, 3);
            this.tableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel.Name = "tableLayoutPanel";
            this.tableLayoutPanel.Padding = new System.Windows.Forms.Padding(6);
            this.tableLayoutPanel.RowCount = 5;
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            //
            // labels
            //
            this.labelPtxProjectCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelPtxProjectCaption.AutoSize = true;
            this.labelPtxProjectCaption.Name = "labelPtxProjectCaption";
            this.labelPtxProjectCaption.Text = "Paratext project:";
            this.labelPtxProject.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelPtxProject.AutoSize = true;
            this.labelPtxProject.Name = "labelPtxProject";
            this.labelSpellFixerProjectCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelSpellFixerProjectCaption.AutoSize = true;
            this.labelSpellFixerProjectCaption.Name = "labelSpellFixerProjectCaption";
            this.labelSpellFixerProjectCaption.Text = "Spell Fixer project:";
            this.labelSpellFixerProject.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelSpellFixerProject.AutoSize = true;
            this.labelSpellFixerProject.Name = "labelSpellFixerProject";
            this.labelSpellFixerProject.Text = "(none chosen)";
            this.labelVerseCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelVerseCaption.AutoSize = true;
            this.labelVerseCaption.Name = "labelVerseCaption";
            this.labelVerseCaption.Text = "Start at:";
            this.labelVerse.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelVerse.AutoSize = true;
            this.labelVerse.Name = "labelVerse";
            //
            // buttonPinToTop
            //
            this.buttonPinToTop.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.buttonPinToTop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonPinToTop.FlatAppearance.BorderSize = 0;
            this.buttonPinToTop.Name = "buttonPinToTop";
            this.buttonPinToTop.Size = new System.Drawing.Size(24, 24);
            this.toolTip.SetToolTip(this.buttonPinToTop, "Click to keep this window on top of Paratext (or to stop keeping it on top)");
            this.buttonPinToTop.Click += new System.EventHandler(this.ButtonPinToTop_Click);
            //
            // buttonChooseProject
            //
            this.buttonChooseProject.AutoSize = true;
            this.buttonChooseProject.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonChooseProject.Name = "buttonChooseProject";
            this.buttonChooseProject.Text = "&Choose Project...";
            this.toolTip.SetToolTip(this.buttonChooseProject, "Choose the Spell Fixer project (whole word Consistent Spelling Checker, or partial word legacy SpellFixer) to use for this Paratext project");
            this.buttonChooseProject.Click += new System.EventHandler(this.ButtonChooseProject_Click);
            //
            // buttonCheck
            //
            this.buttonCheck.AutoSize = true;
            this.buttonCheck.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonCheck.Name = "buttonCheck";
            this.buttonCheck.Text = "C&heck from Current Verse";
            this.toolTip.SetToolTip(this.buttonCheck, "Check each word from the verse selected in Paratext to the end of the chapter, asking about each one the Spell Fixer would change");
            this.buttonCheck.Click += new System.EventHandler(this.ButtonCheck_Click);
            //
            // flowLayoutPanelButtons
            //
            this.flowLayoutPanelButtons.AutoSize = true;
            this.flowLayoutPanelButtons.Controls.Add(this.buttonAssignCorrectSpelling);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonFindReplacementRule);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonEditSpellingFixes);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.buttonAssignCorrectSpelling.AutoSize = true;
            this.buttonAssignCorrectSpelling.Name = "buttonAssignCorrectSpelling";
            this.buttonAssignCorrectSpelling.Text = "&Assign Correct Spelling (clipboard)";
            this.toolTip.SetToolTip(this.buttonAssignCorrectSpelling, "Assign the correct spelling for the word on the system clipboard. Copy the word (e.g. from Paratext) to the clipboard before clicking this button.");
            this.buttonAssignCorrectSpelling.Click += new System.EventHandler(this.ButtonAssignCorrectSpelling_Click);
            this.buttonFindReplacementRule.AutoSize = true;
            this.buttonFindReplacementRule.Name = "buttonFindReplacementRule";
            this.buttonFindReplacementRule.Text = "&Find Replacement Rule (clipboard)";
            this.toolTip.SetToolTip(this.buttonFindReplacementRule, "Find the replacement rule that applies to the word on the system clipboard. Copy the word to the clipboard before clicking this button.");
            this.buttonFindReplacementRule.Click += new System.EventHandler(this.ButtonFindReplacementRule_Click);
            this.buttonEditSpellingFixes.AutoSize = true;
            this.buttonEditSpellingFixes.Name = "buttonEditSpellingFixes";
            this.buttonEditSpellingFixes.Text = "&Edit Spelling Fixes...";
            this.toolTip.SetToolTip(this.buttonEditSpellingFixes, "Edit the list of spelling fixes in the Spell Fixer project");
            this.buttonEditSpellingFixes.Click += new System.EventHandler(this.ButtonEditSpellingFixes_Click);
            //
            // textBoxStatus
            //
            this.textBoxStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxStatus.Multiline = true;
            this.textBoxStatus.Name = "textBoxStatus";
            this.textBoxStatus.ReadOnly = true;
            //
            // SpellFixerForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 200);
            this.Controls.Add(this.tableLayoutPanel);
            this.MinimumSize = new System.Drawing.Size(500, 200);
            this.Name = "SpellFixerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Spell Fixer for Paratext";
            this.Load += new System.EventHandler(this.SpellFixerForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SpellFixerForm_FormClosing);
            this.tableLayoutPanel.ResumeLayout(false);
            this.tableLayoutPanel.PerformLayout();
            this.flowLayoutPanelButtons.ResumeLayout(false);
            this.flowLayoutPanelButtons.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Label labelPtxProjectCaption;
        private System.Windows.Forms.Label labelPtxProject;
        private System.Windows.Forms.Button buttonPinToTop;
        private System.Windows.Forms.Label labelSpellFixerProjectCaption;
        private System.Windows.Forms.Label labelSpellFixerProject;
        private System.Windows.Forms.Button buttonChooseProject;
        private System.Windows.Forms.Label labelVerseCaption;
        private System.Windows.Forms.Label labelVerse;
        private System.Windows.Forms.Button buttonCheck;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Button buttonAssignCorrectSpelling;
        private System.Windows.Forms.Button buttonFindReplacementRule;
        private System.Windows.Forms.Button buttonEditSpellingFixes;
        private System.Windows.Forms.TextBox textBoxStatus;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
```

- [ ] **Step 3: `SpellFixerPlugin.cs` (replace the Task 4 stub)**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Paratext.PluginInterfaces;
using SIL.ParatextBackTranslationHelperPlugin;

namespace SIL.SpellFixerPluginForParatext
{
    public class SpellFixerPlugin : IParatextStandalonePlugin
    {
        public const string PluginName = "Spell Fixer";
        public string Name => PluginName;
        public Version Version => new Version(1, 0);
        public string VersionString => Version.ToString();
        public string Publisher => "SIL";

        private static IPluginHost _host;
        private static SpellFixerPlugin _this;
        private static SpellFixerForm _mainWindow;

        public SpellFixerPlugin()
        {
            _this = this;

            PluginAssemblyResolver.Register(msg => _host?.Log(_this, msg));
        }

        public IEnumerable<PluginMenuEntry> PluginMenuEntries
        {
            get
            {
                yield return new PluginMenuEntry($"&{PluginName}...", Run, PluginMenuLocation.ScrTextTools);
            }
        }

        public IDataFileMerger GetMerger(IPluginHost host, string dataIdentifier) => throw new NotImplementedException();

        public string GetDescription(string locale)
        {
            return "Checks the spelling of the active project, word by word, using a SpellFixer (Consistent Spelling Checker or legacy SpellFixer) project, and fixes the words you approve.";
        }

        /// <summary>
        /// Called by Paratext when the menu item created for this plugin was clicked.
        /// </summary>
        private static void Run(IPluginHost host, IParatextChildState state)
        {
            Application.EnableVisualStyles();
            _host = host;

            var shortName = state.Project?.ShortName ?? host.ActiveWindowState?.Project?.ShortName;
            var project = host.GetAllProjects().FirstOrDefault(p => p.ShortName == shortName);
            if (project == null)
            {
                MessageBox.Show("Open a Paratext project window and then choose this command from its Tools menu.", PluginName);
                return;
            }

            // one window at a time: re-use it if it's for the same project
            if ((_mainWindow != null) && !_mainWindow.IsDisposed)
            {
                if (_mainWindow.ProjectShortName == project.ShortName)
                {
                    _mainWindow.Activate();
                    return;
                }
                _mainWindow.Close();
            }

            _host.Log(_this, "Starting " + PluginName);

            Action<IVerseRef> setSyncReference = verseReference => _host.SetReferenceForSyncGroup(verseReference, state.SyncReferenceGroup);

            _mainWindow = new SpellFixerForm(_host, _this, project, state.VerseRef, setSyncReference);
            _mainWindow.Show();
        }
    }
}
```

- [ ] **Step 4: `SpellFixerForm.cs` (everything except the checking loop, which is Task 6)**

```csharp
using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SIL.SpellFixerPluginForParatext
{
    internal partial class SpellFixerForm : Form
    {
        private readonly IPluginHost _host;
        private readonly IPluginObject _plugin;
        private readonly IProject _project;
        private readonly Action<IVerseRef> _setSyncReference;
        private IVerseRef _verseReference;
        private SpellFixerProject _spellFixerProject;
        private bool _isChecking;

        private static readonly Bitmap _pinUp = LoadBitmap("pinup.bmp");
        private static readonly Bitmap _pinDown = LoadBitmap("pindown.bmp");

        public SpellFixerForm(IPluginHost host, IPluginObject plugin, IProject project, IVerseRef initialVerseReference, Action<IVerseRef> setSyncReference)
        {
            InitializeComponent();
            InitializeSettings();

            _host = host;
            _plugin = plugin;
            _project = project;
            _setSyncReference = setSyncReference;
            SetVerseReference(initialVerseReference);

            labelPtxProject.Text = project.ShortName;
            SetPinToTop(Properties.Settings.Default.PinToTop);

            _host.VerseRefChanged += Host_VerseRefChanged;
        }

        public string ProjectShortName => _project.ShortName;

        private static Bitmap LoadBitmap(string name)
        {
            return new Bitmap(typeof(SpellFixerForm).Assembly.GetManifestResourceStream($"SIL.SpellFixerPluginForParatext.{name}"));
        }

        /// <summary>
        /// If Ptx is upgraded, then we lose the settings. This upgrades them the first time after an install.
        /// </summary>
        private static void InitializeSettings()
        {
            if (!Properties.Settings.Default.UpgradeSettings)
                return;

            Properties.Settings.Default.Upgrade();
            Properties.Settings.Default.UpgradeSettings = false;
            Properties.Settings.Default.Save();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadSavedSpellFixerProject();
            UpdateButtonStates();
        }

        private void SpellFixerForm_Load(object sender, EventArgs e)
        {
            var location = Properties.Settings.Default.WindowLocation;
            if ((location != Point.Empty) && Screen.AllScreens.Any(s => s.WorkingArea.Contains(location)))
                Location = location;
        }

        private void SpellFixerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isChecking)
            {
                e.Cancel = true;    // let the run finish (click Cancel in the query dialog to stop it)
                return;
            }

            _host.VerseRefChanged -= Host_VerseRefChanged;

            if (WindowState == FormWindowState.Normal)
                Properties.Settings.Default.WindowLocation = Location;
            Properties.Settings.Default.Save();
        }

        private void Host_VerseRefChanged(IPluginHost sender, IVerseRef newReference, SyncReferenceGroup group)
        {
            // while checking, we're the ones moving Paratext around, so don't change where the next run would start
            if (_isChecking)
                return;

            SetVerseReference(newReference);
        }

        private void SetVerseReference(IVerseRef verseReference)
        {
            _verseReference = (verseReference?.RepresentsMultipleVerses ?? false)
                                ? verseReference.AllVerses.First()
                                : verseReference;
            labelVerse.Text = _verseReference?.ToString();
        }

        #region SpellFixer project (remembered per Paratext project)

        private void LoadSavedSpellFixerProject()
        {
            var converterName = GetSavedConverterName(_project.ShortName);
            if (String.IsNullOrEmpty(converterName))
            {
                ChooseSpellFixerProject();
                return;
            }

            try
            {
                SetSpellFixerProject(SpellFixerProject.FromConverterName(converterName));
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"Unable to load Spell Fixer project '{converterName}': {ex.Message}");
                SaveConverterName(_project.ShortName, null);
                ChooseSpellFixerProject();
            }
        }

        private void ChooseSpellFixerProject()
        {
            try
            {
                var spellFixerProject = SpellFixerProject.QueryUser();
                if (spellFixerProject == null)
                    return;     // keep what we had

                SetSpellFixerProject(spellFixerProject);
                SaveConverterName(_project.ShortName, spellFixerProject.ConverterName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, SpellFixerPlugin.PluginName);
            }
        }

        private void SetSpellFixerProject(SpellFixerProject spellFixerProject)
        {
            _spellFixerProject = spellFixerProject;
            _queryForm?.Dispose();
            _queryForm = null;
            labelSpellFixerProject.Text = spellFixerProject.DisplayName;
            UpdateButtonStates();
        }

        // the setting is a list of "PtxShortName=SpellFixerEncConverterName" entries
        private static string GetSavedConverterName(string ptxProjectShortName)
        {
            var prefix = ptxProjectShortName + "=";
            return Properties.Settings.Default.MapPtxProjectToSpellFixerConverter?
                                              .Cast<string>()
                                              .FirstOrDefault(s => s.StartsWith(prefix))?
                                              .Substring(prefix.Length);
        }

        private static void SaveConverterName(string ptxProjectShortName, string converterName)
        {
            var prefix = ptxProjectShortName + "=";
            var entries = Properties.Settings.Default.MapPtxProjectToSpellFixerConverter?.Cast<string>()
                                                     .Where(s => !s.StartsWith(prefix))
                                                     .ToList()
                          ?? new List<string>();
            if (!String.IsNullOrEmpty(converterName))
                entries.Add(prefix + converterName);

            var collection = new StringCollection();
            collection.AddRange(entries.ToArray());
            Properties.Settings.Default.MapPtxProjectToSpellFixerConverter = collection;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region Buttons

        private void UpdateButtonStates()
        {
            var haveProject = (_spellFixerProject != null);
            buttonChooseProject.Enabled = !_isChecking;
            buttonCheck.Enabled = haveProject && !_isChecking;
            buttonAssignCorrectSpelling.Enabled = haveProject && !_isChecking;
            buttonFindReplacementRule.Enabled = haveProject && !_isChecking;
            buttonEditSpellingFixes.Enabled = haveProject && !_isChecking;
        }

        private void ButtonChooseProject_Click(object sender, EventArgs e)
        {
            ChooseSpellFixerProject();
        }

        private void ButtonAssignCorrectSpelling_Click(object sender, EventArgs e)
        {
            DoWithClipboardWord(word => _spellFixerProject.AssignCorrectSpelling(word));
        }

        private void ButtonFindReplacementRule_Click(object sender, EventArgs e)
        {
            DoWithClipboardWord(word => _spellFixerProject.FindReplacementRule(word));
        }

        private void ButtonEditSpellingFixes_Click(object sender, EventArgs e)
        {
            TryAction(() => _spellFixerProject.EditSpellingFixes());
        }

        private void DoWithClipboardWord(Action<string> action)
        {
            var word = Clipboard.ContainsText(TextDataFormat.UnicodeText)
                        ? Clipboard.GetText(TextDataFormat.UnicodeText)?.Trim()
                        : null;
            if (String.IsNullOrEmpty(word))
            {
                MessageBox.Show("Copy a word (e.g. from Paratext) to the clipboard first, and then click this button again.", SpellFixerPlugin.PluginName);
                return;
            }

            TryAction(() => action(word));
        }

        private void TryAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, SpellFixerPlugin.PluginName);
            }
        }

        private void ButtonPinToTop_Click(object sender, EventArgs e)
        {
            SetPinToTop(!TopMost);
            Properties.Settings.Default.PinToTop = TopMost;
            Properties.Settings.Default.Save();
        }

        private void SetPinToTop(bool pinToTop)
        {
            TopMost = pinToTop;
            buttonPinToTop.Image = pinToTop ? _pinDown : _pinUp;
        }

        private void ButtonCheck_Click(object sender, EventArgs e)
        {
            CheckFromCurrentVerse();
        }

        #endregion

        private SpellFixerQueryForm _queryForm;

        // implemented in Task 6
        private void CheckFromCurrentVerse()
        {
        }
    }
}
```

- [ ] **Step 5: Build, then smoke-test in Paratext**

```powershell
& $msbuild src\SpellFixerPluginForParatext\SpellFixerPluginForParatext.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
```
Expected: 0 errors. If `%ParatextInstallDir%` is set, the post-build copies the plugin into Paratext.

Manual: start Paratext 9, open a project, and choose **Tools > Spell Fixer...**.
- **First launch:** the window opens and asks for the project kind and the project. The window then shows the Paratext project, the "Whole word (CSC): …" or "Partial word (legacy): …" label, and the current verse.
- **Verse tracking:** moving around in Paratext updates "Start at".
- **Pin:** toggles stay-on-top and the icon.
- **Clipboard buttons:** with an empty clipboard they show the "Copy a word…" message. With a copied word they open the SpellFixer UI.
- **Reopen:** close and reopen the window; it doesn't ask for the project again.
- **Choose Project…:** lets you switch to the other kind, and the new choice is remembered.

- [ ] **Step 6: Commit**

```powershell
git add src/SpellFixerPluginForParatext
git commit -m "feat(SpellFixerPluginForParatext): plugin entry point, settings and non-modal main window`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Checking loop (verse by verse, writing once per verse, continue to next chapter/book)

**Files:**
- Modify: `src/SpellFixerPluginForParatext/SpellFixerForm.cs`. Replace the empty `CheckFromCurrentVerse` and add the methods below.

**Interfaces:**
- Consumes:
  - `UsfmChapterTokens.GroupByVerse`, `StartVerseKey` and `NextVerseKey` (Task 1).
  - `SpellFixerTokenProcessor.FixVerseTokens`, `SpliceVerse` and `VerseText` (Task 2).
  - `SpellFixerSession` (Task 3).
  - `SpellFixerQueryForm.Show(owner, font, rtl, word, suggestion)` and `CorrectedSpelling` (Task 4).
  - `IProject.GetUSFMTokens(int, int)`, `RequestWriteLock(IPluginObject, Action<IWriteLock>, int, int)` and `PutUSFMTokens(IWriteLock, IReadOnlyList<IUSFMToken>, int)`; `IVerseRef.GetNextChapter(IProject)`.

- [ ] **Step 1: Replace the `CheckFromCurrentVerse` stub with the loop**

Add `using SIL.ParatextBackTranslationHelperPlugin;` at the top of `SpellFixerForm.cs`, then replace

```csharp
        // implemented in Task 6
        private void CheckFromCurrentVerse()
        {
        }
```
with:

```csharp
        private Font _projectFont;
        private bool _hasShownConvertError;

        private void CheckFromCurrentVerse()
        {
            if ((_spellFixerProject == null) || (_verseReference == null))
                return;

            var session = new SpellFixerSession(ConvertWord, AskUser);
            var totalWordsFixed = 0;
            var chapterReference = _verseReference;
            var startAtBeginningOfChapter = false;
            _hasShownConvertError = false;

            SetChecking(true);
            try
            {
                while (chapterReference != null)
                {
                    if (!CheckChapter(chapterReference, startAtBeginningOfChapter, session, ref totalWordsFixed))
                        break;  // cancelled or couldn't write

                    chapterReference = QueryNextChapter(chapterReference);
                    startAtBeginningOfChapter = true;
                }
            }
            finally
            {
                SetChecking(false);
            }

            SetStatus($"Done: {totalWordsFixed} word(s) fixed.");
        }

        private void SetChecking(bool isChecking)
        {
            _isChecking = isChecking;
            UpdateButtonStates();
        }

        private void SetStatus(string status)
        {
            textBoxStatus.Text = status;
            textBoxStatus.Refresh();
        }

        /// <summary>
        /// Checks one chapter, writing each verse that has accepted fixes. Returns false if the run should stop
        /// (the user cancelled or a verse couldn't be written)
        /// </summary>
        private bool CheckChapter(IVerseRef chapterReference, bool startAtBeginningOfChapter, SpellFixerSession session, ref int totalWordsFixed)
        {
            var vrefTokens = LoadChapter(chapterReference);
            if (vrefTokens == null)
                return true;    // some books don't return proper things (e.g. GLO), so just go on

            var verseKey = startAtBeginningOfChapter
                            ? vrefTokens.Keys.FirstOrDefault()
                            : UsfmChapterTokens.StartVerseKey(vrefTokens, chapterReference);

            while (verseKey != null)
            {
                var verseTokens = vrefTokens[verseKey];
                var verseReference = verseTokens.First().VerseRef;
                _setSyncReference(verseReference);
                SetStatus($"Checking {verseReference}...");

                var cancelled = false;
                var fixedTokens = SpellFixerTokenProcessor.FixVerseTokens(verseTokens, session.FixWord, ref cancelled, out int wordsFixed);
                if (wordsFixed > 0)
                {
                    if (!WriteVerse(chapterReference, verseKey, verseTokens, fixedTokens))
                        return false;

                    totalWordsFixed += wordsFixed;
                    SetStatus($"Wrote {wordsFixed} fix(es) to {verseReference}");

                    vrefTokens = LoadChapter(chapterReference);     // Paratext has the new data now
                    if (vrefTokens == null)
                        return false;
                }

                if (cancelled)
                    return false;

                verseKey = UsfmChapterTokens.NextVerseKey(vrefTokens, verseKey);
            }

            return true;
        }

        private SortedDictionary<string, List<IUSFMToken>> LoadChapter(IVerseRef chapterReference)
        {
            var chapterTokens = _project.GetUSFMTokens(chapterReference.BookNum, chapterReference.ChapterNum)?.ToList();
            return ((chapterTokens == null) || !chapterTokens.Any())
                    ? null
                    : UsfmChapterTokens.GroupByVerse(chapterTokens);
        }

        /// <summary>
        /// Writes the chapter back to Paratext with the verse's tokens replaced by the fixed ones (but only if the
        /// verse wasn't changed in Paratext since we read it)
        /// </summary>
        private bool WriteVerse(IVerseRef chapterReference, string verseKey, List<IUSFMToken> originalVerseTokens, List<IUSFMToken> fixedVerseTokens)
        {
            IWriteLock writeLock = null;
            try
            {
                writeLock = _project.RequestWriteLock(_plugin, ReleaseRequested, chapterReference.BookNum, chapterReference.ChapterNum);
                if (writeLock == null)
                {
                    // e.g. the user doesn't have edit permissions on this chapter
                    MessageBox.Show($"You don't have edit privilege on this chapter: {chapterReference.BookCode} {chapterReference.ChapterNum} of the {_project.ShortName} project", SpellFixerPlugin.PluginName);
                    return false;
                }

                // getting the lock may have saved changes the user had made in Paratext, so make sure we aren't
                //  about to overwrite them with stale data
                var currentTokens = LoadChapter(chapterReference);
                if ((currentTokens == null) || !currentTokens.TryGetValue(verseKey, out List<IUSFMToken> currentVerseTokens)
                    || (SpellFixerTokenProcessor.VerseText(currentVerseTokens) != SpellFixerTokenProcessor.VerseText(originalVerseTokens)))
                {
                    MessageBox.Show($"{fixedVerseTokens.First().VerseRef} was changed in Paratext while it was being checked, so the fixes to it weren't saved. Check it again.", SpellFixerPlugin.PluginName);
                    return false;
                }

                var chapterTokens = SpellFixerTokenProcessor.SpliceVerse(currentTokens, verseKey, fixedVerseTokens);
                _project.PutUSFMTokens(writeLock, chapterTokens, chapterReference.BookNum);
                return true;
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: WriteVerse: {ex}");
                MessageBox.Show($"Unable to write the fixes to Paratext:{Environment.NewLine}{ex.Message}", SpellFixerPlugin.PluginName);
                return false;
            }
            finally
            {
                writeLock?.Dispose();
            }
        }

        private void ReleaseRequested(IWriteLock writeLock)
        {
            writeLock?.Dispose();
        }

        /// <summary>
        /// Asks whether to go on to the next chapter (or the next book at the end of a book). Returns null if not
        /// (or if there isn't one)
        /// </summary>
        private IVerseRef QueryNextChapter(IVerseRef chapterReference)
        {
            var next = chapterReference.GetNextChapter(_project);
            if (next == null)
            {
                MessageBox.Show("That was the last chapter in the project.", SpellFixerPlugin.PluginName);
                return null;
            }

            var question = (next.BookNum == chapterReference.BookNum)
                            ? $"Finished {chapterReference.BookCode} {chapterReference.ChapterNum}. Continue checking with the next chapter ({next.BookCode} {next.ChapterNum})?"
                            : $"Finished {chapterReference.BookCode}. Continue checking with the next book ({next.BookCode})?";

            return (MessageBox.Show(this, question, SpellFixerPlugin.PluginName, MessageBoxButtons.YesNo) == DialogResult.Yes)
                    ? next
                    : null;
        }

        private string ConvertWord(string word)
        {
            try
            {
                return _spellFixerProject.Convert(word);
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: unable to convert '{word}': {ex.Message}");
                if (!_hasShownConvertError)
                {
                    _hasShownConvertError = true;
                    MessageBox.Show($"The Spell Fixer couldn't process '{word}' (it will be skipped):{Environment.NewLine}{ex.Message}", SpellFixerPlugin.PluginName);
                }
                return word;    // i.e. skip it
            }
        }

        private FormButtons AskUser(string word, string suggestion, out string correctedSpelling)
        {
            _queryForm ??= new SpellFixerQueryForm(_spellFixerProject);
            _projectFont ??= GetProjectFont();

            var button = _queryForm.Show(this, _projectFont, _project.Language?.IsRtoL ?? false, word, suggestion);
            correctedSpelling = _queryForm.CorrectedSpelling;
            return button;
        }

        private Font GetProjectFont()
        {
            try
            {
                var font = _project.Language?.Font;
                if (font != null)
                    return new Font(font.FontFamily, Math.Max(12F, (float)font.Size));
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: unable to use the project font: {ex.Message}");
            }
            return null;    // i.e. use the dialog's default
        }
```

- [ ] **Step 2: Build**

```powershell
& $msbuild src\SpellFixerPluginForParatext\SpellFixerPluginForParatext.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
```
Expected: 0 errors. If `IProject.Language` is not on `IProject`, use the same member the sister plugin uses (`_projectTarget.Language` in `BackTranslationHelperForm.InitializeProjects`). If `IFont.Size` is not numeric, use `12F`.

- [ ] **Step 3: Manual test in Paratext**

Use a test project, or a clone of one, that has a SpellFixer CSC project with at least one rule, e.g. `teh → the`. Type `teh` into two verses and a section heading in one chapter.
1. **Verse 1 of that chapter → Check from Current Verse:**
   - Paratext moves to each verse as it's checked.
   - The query dialog shows `teh` / `the` in the project font.
   - **Replace Once:** that verse's text updates in Paratext right away. That's the one write for that verse.
2. **Skip:** the text is unchanged.
3. **Replace Every:** the next `teh` is fixed without asking.
4. **Replace All:** no more questions for the rest of the run.
5. **Cancel:** the run stops, and fixes already accepted stay.
6. **End of chapter:** you're asked about the next chapter. Answering Yes continues from that chapter's start, including any heading before `\v 1`.
7. **Last chapter of a book:** you're asked about the next book.
8. **Combined verse:** start with the cursor in verse 4 of a `\v 3-4` combined verse. Checking starts at that combined verse.
9. **Read-only project or chapter** (no edit permission): you get the "edit privilege" message and the run stops.
10. **Switch to a legacy (partial word) SpellFixer project with Choose Project…** and repeat step 1.
11. **View Rule and Add Rule** inside the query dialog work and refresh the suggestion.

- [ ] **Step 4: Commit**

```powershell
git add src/SpellFixerPluginForParatext/SpellFixerForm.cs
git commit -m "feat(SpellFixerPluginForParatext): verse-by-verse checking loop with per-verse writes and continue to next chapter/book`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: Installer

**Files:**
- Modify: `Installer/ParatextBackTranslationHelperPlugin64bitMM/ParatextBackTranslationHelperPlugin_MergeModule.wxs`
- Modify: `Installer/SEC Setup 64bit/EcFeatures.wxs` (~L93)

- [ ] **Step 1: Add the plugin to the merge module**

In `ParatextBackTranslationHelperPlugin_MergeModule.wxs`, inside `<Directory Id="Paratext9Plugins" Name="plugins">` and after the `ParatextPluginBackTranslationHelper` `</Directory>`, add:

```xml
          <Directory Id="SpellFixerPluginForParatext" Name="SpellFixerPluginForParatext">
            <Component Id="SpellFixerPluginForParatext.ptxplg" Guid="{9B7E2C41-3D6A-4F18-A5C2-7E0B9D3F1A64}">
              <File Id="SpellFixerPluginForParatext.ptxplg" Name="SpellFixerPluginForParatext.ptxplg" KeyPath="yes" Assembly=".net" AssemblyManifest="SpellFixerPluginForParatext.ptxplg" AssemblyApplication="SpellFixerPluginForParatext.ptxplg" Source="..\..\output\x64\release\SpellFixerPluginForParatext.dll" />
            </Component>
          </Directory>
```

Change the `SummaryInformation` `Description` to `"Installer for the SIL Converters Plug-ins for Paratext (Back Translation Helper and Spell Fixer)"`.

- [ ] **Step 2: Retitle the feature**

In `EcFeatures.wxs`, keep the Id, Level, Condition and MergeRefs, and change only `Title` and `Description` of `Feature Id="Paratext_BackTranslation_Helper_Plugin"`:

```xml
      <Feature Id="Paratext_BackTranslation_Helper_Plugin" Title="SIL Converters Plugins for Paratext" Description="Plugins launched from the Paratext, Tools menu (newer than 9.2.102.17): the Back Translation Helper (uses Bing/DeepL/Google Translate, etc. to prepare a back translation of 1 project into another; requires an internet connection) and the Spell Fixer (checks and fixes the spelling of a project using a SpellFixer/Consistent Spelling Checker project)." Display="expand" Level="4" AllowAdvertise="no">
```

- [ ] **Step 3: Build Release x64 of the plugin and the merge module**

```powershell
& $msbuild src\SpellFixerPluginForParatext\SpellFixerPluginForParatext.csproj -restore -p:Configuration=Release -p:Platform=x64 -v:m
& $msbuild src\ParatextPluginBackTranslationHelper\ParatextPluginBackTranslationHelper.csproj -restore -p:Configuration=Release -p:Platform=x64 -v:m
& $msbuild Installer\ParatextBackTranslationHelperPlugin64bitMM\ParatextBackTranslationHelperPlugin64bitMM.wixproj -restore -p:Configuration=Release -p:Platform=x64 -v:m
```
Expected: 0 errors. `Installer\MergeModules\x64\ParatextBackTranslationHelperPlugin64bitMM.msm` is updated.

Check that the msm contains the new file:
```powershell
wix msi decompile Installer\MergeModules\x64\ParatextBackTranslationHelperPlugin64bitMM.msm -o $env:TEMP\ptxmm.wxs; Select-String -Path $env:TEMP\ptxmm.wxs -Pattern 'SpellFixerPluginForParatext'
```
(If the `wix` global tool isn't installed, run `dotnet tool install --global wix --version 6.0.2` first, as build.yml does.) Expected: at least one match.

- [ ] **Step 4: Commit**

```powershell
git add Installer/ParatextBackTranslationHelperPlugin64bitMM/ParatextBackTranslationHelperPlugin_MergeModule.wxs "Installer/SEC Setup 64bit/EcFeatures.wxs"
git commit -m "installer: add SpellFixerPluginForParatext to the Paratext plugins merge module and retitle the feature`n`nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

Don't commit the rebuilt `.msm` unless `git status` shows it's tracked and previous commits have included rebuilt copies. Check with `git log --oneline -- Installer/MergeModules/x64/ParatextBackTranslationHelperPlugin64bitMM.msm`.

---

### Task 8: Full-solution build and regression check

- [ ] **Step 1: Build the whole solution the way CI does (Release x64)**

```powershell
& $msbuild "SEC VS2019.sln" -restore -p:Configuration=Release -p:Platform=x64 -v:m
```
Expected: 0 errors.

- [ ] **Step 2: Run all Paratext-related tests**

```powershell
& $msbuild src\SILConvertersWordML\TestBwdc\TestBwdc.csproj -restore -p:Configuration=Debug -p:Platform=x64 -v:m
& $vstest src\SILConvertersWordML\TestBwdc\bin\x64\Debug\TestBwdc.dll /Platform:x64 /TestCaseFilter:"FullyQualifiedName~UnitTest_PtxSpellFixer|FullyQualifiedName~UnitTest_UsfmChapterTokens|FullyQualifiedName~UnitTest_PtxBackTrHelper"
```
Expected: the new tests pass, and `UnitTest_PtxBackTrHelper` gives the same results as on `master`.

- [ ] **Step 3: Regression smoke test of the Back Translation Helper in Paratext (Debug build)**

Open **Tools > Back Translation Helper...** on a daughter project. It loads the verse, translates, and Write To Target still writes. This checks the `UsfmChapterTokens`, `PtxPluginHelpers` and `PluginAssemblyResolver` moves.

- [ ] **Step 4: Push the branch and open a PR only if the user asks**
