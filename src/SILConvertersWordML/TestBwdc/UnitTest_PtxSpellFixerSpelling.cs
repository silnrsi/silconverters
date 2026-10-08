extern alias SpellFixer;
extern alias Updater;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using SpellFixer::SIL.SpellFixerPluginForParatext;
using static TestBwdc.PtxTestTokens;
using SpellingStatusUpdateRunner = Updater::SIL.SpellingStatusUpdater.SpellingStatusUpdateRunner;

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
        public void ValidateSelection_RejectsSelectionWithoutVerseReference()
        {
            var s = Sel("\\v 1 ", "teh", " end");
            s.VerseRefStart = null;
            s.VerseRefEnd = null;
            Assert.IsFalse(SelectionReplacer.ValidateSelection(s, out _, out _, out string reason));
            Assert.IsFalse(string.IsNullOrEmpty(reason));
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

        #region SpellingStatusUpdateRunner

        [Test]
        public void AnyPending_FindsLeftoverPendingFilesInAnyProject()
        {
            MakeProject("Cat", null);
            Assert.IsFalse(PendingSpellingStatusStore.AnyPending(_tempDir));
            Assert.IsFalse(PendingSpellingStatusStore.AnyPending(Path.Combine(_tempDir, "missing")));
            Assert.IsFalse(PendingSpellingStatusStore.AnyPending(null));

            var projectFolder = MakeProject("Dog", null);
            PendingSpellingStatusStore.Append(PendingSpellingStatusStore.PathFor(projectFolder), "teh", "the", DateTime.UtcNow);
            Assert.IsTrue(PendingSpellingStatusStore.AnyPending(_tempDir));
        }

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
    }
}
