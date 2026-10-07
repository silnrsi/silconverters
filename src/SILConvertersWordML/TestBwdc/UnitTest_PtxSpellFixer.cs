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
    }
}
