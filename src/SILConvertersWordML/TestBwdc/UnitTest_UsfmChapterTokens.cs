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
