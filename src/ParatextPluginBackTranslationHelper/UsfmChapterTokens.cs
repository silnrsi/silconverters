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
