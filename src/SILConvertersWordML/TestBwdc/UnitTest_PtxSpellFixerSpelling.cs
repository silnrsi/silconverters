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
