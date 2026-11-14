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
        /// Whether ch is one of the characters the SpellFixer's CC table doesn't consider part of a word, so it gets
        /// trimmed off the ends of a word (the same ones CscProject.TrimWhiteSpaceAndPunctuation trims: whitespace,
        /// digits, Unicode punctuation (e.g. ',' and the danda), plus extraTrimCharacters, e.g. the project's script
        /// system's extra punctuation)
        /// </summary>
        public static bool IsTrimCharacter(char ch, ICollection<char> extraTrimCharacters)
        {
            return (ch <= 0x0020) || Char.IsDigit(ch) || Char.IsPunctuation(ch) || Char.IsWhiteSpace(ch) ||
                   ((extraTrimCharacters != null) && extraTrimCharacters.Contains(ch));
        }

        /// <summary>
        /// Returns the index of the first and the length of the word in 'run' without its leading and trailing trim
        /// characters (length 0 if it's all trim characters)
        /// </summary>
        public static int TrimmedWordBounds(string run, ICollection<char> extraTrimCharacters, out int length)
        {
            int start = 0, end = run.Length - 1;
            while ((start <= end) && IsTrimCharacter(run[start], extraTrimCharacters))
                start++;
            while ((end >= start) && IsTrimCharacter(run[end], extraTrimCharacters))
                end--;

            length = end - start + 1;
            return start;
        }

        public static string FixWords(string text, WordFixer fixWord, ref bool cancelled, out int wordsFixed)
        {
            return FixWords(text, null, fixWord, ref cancelled, out wordsFixed);
        }

        /// <summary>
        /// Calls fixWord for each word in text (without any leading or trailing punctuation, which is put back
        /// around the fixed word) and returns the rebuilt text. If fixWord returns null, 'cancelled' is set and the
        /// rest of the text is left as is.
        /// </summary>
        public static string FixWords(string text, ICollection<char> extraTrimCharacters, WordFixer fixWord, ref bool cancelled, out int wordsFixed)
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

                var start = TrimmedWordBounds(run, extraTrimCharacters, out int length);
                if (length <= 0)
                {
                    sb.Append(run);     // only punctuation and/or digits
                    continue;
                }

                var word = run.Substring(start, length);
                var fixedWord = fixWord(word);
                if (fixedWord == null)
                {
                    cancelled = true;
                    sb.Append(run);
                    continue;
                }

                if (fixedWord != word)
                    wordsFixed++;
                sb.Append(run, 0, start).Append(fixedWord).Append(run, start + length, run.Length - start - length);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns a new list of the verse's tokens in which each publishable vernacular text token that had a word
        /// fixed is replaced by a new TextToken (all other tokens are the same instances)
        /// </summary>
        public static List<IUSFMToken> FixVerseTokens(List<IUSFMToken> verseTokens, WordFixer fixWord, ref bool cancelled, out int wordsFixed)
        {
            return FixVerseTokens(verseTokens, null, fixWord, ref cancelled, out wordsFixed);
        }

        public static List<IUSFMToken> FixVerseTokens(List<IUSFMToken> verseTokens, ICollection<char> extraTrimCharacters, WordFixer fixWord,
                                                      ref bool cancelled, out int wordsFixed)
        {
            wordsFixed = 0;
            var result = new List<IUSFMToken>(verseTokens.Count);
            foreach (var token in verseTokens)
            {
                if (!cancelled && (token is IUSFMTextToken textToken) && PtxPluginHelpers.IsPublishableVernacular(textToken, verseTokens))
                {
                    var newText = FixWords(textToken.Text, extraTrimCharacters, fixWord, ref cancelled, out int wordsFixedInToken);
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
        /// Returns all of the chapter's tokens, in their document order, with each of the verse's tokens (which are
        /// in chapterTokens, though not necessarily contiguous) replaced by the corresponding one in newVerseTokens
        /// (i.e. what to pass to IProject.PutUSFMTokens). newVerseTokens must be parallel to verseTokensInChapter
        /// (as returned by FixVerseTokens).
        /// </summary>
        public static List<IUSFMToken> SpliceVerse(List<IUSFMToken> chapterTokens, List<IUSFMToken> verseTokensInChapter, List<IUSFMToken> newVerseTokens)
        {
            if (verseTokensInChapter.Count != newVerseTokens.Count)
                throw new ArgumentException("The new verse tokens must correspond one-to-one with the verse's tokens", nameof(newVerseTokens));

            return chapterTokens.Select(token =>
                                {
                                    var index = verseTokensInChapter.FindIndex(t => ReferenceEquals(t, token));
                                    return (index < 0) ? token : newVerseTokens[index];
                                })
                                .ToList();
        }

        /// <summary>
        /// The markers and text of a verse, in order (to detect whether it was changed in Paratext since we read it)
        /// </summary>
        public static string VerseSignature(IEnumerable<IUSFMToken> verseTokens)
        {
            return String.Join("\n", verseTokens.Select(t => (t is IUSFMTextToken textToken)
                                                               ? textToken.Text
                                                               : (t is IUSFMMarkerToken markerToken)
                                                                   ? $"\\{markerToken.Marker}|{markerToken.Data}|{markerToken.EndMarker}"
                                                                   : t.GetType().Name));
        }

        /// <summary>
        /// Returns the key of the verse to start checking at: the verse (range) containing verseReference, except
        /// when that's verse 1 (or the chapter start), in which case it's the first key of the chapter (so the
        /// section heading, etc. before \v 1 gets checked too)
        /// </summary>
        /// <summary>
        /// Returns the key of just the verse (range) containing verseReference (e.g. "40_005_003-004" for 5:4 if
        /// verses 3-4 are combined; "40_005_000" for a heading before \v 1), or null if it isn't in the chapter
        /// </summary>
        public static string CurrentVerseKey(SortedDictionary<string, List<IUSFMToken>> vrefTokens, IVerseRef verseReference)
        {
            return UsfmChapterTokens.TriangulateBookChapterVerseKey(UsfmChapterTokens.GetBookChapterVerseRangeKey(verseReference), vrefTokens);
        }

        public static string StartVerseKey(SortedDictionary<string, List<IUSFMToken>> vrefTokens, IVerseRef verseReference)
        {
            return (verseReference.VerseNum <= 1)
                    ? vrefTokens.Keys.FirstOrDefault()
                    : UsfmChapterTokens.StartVerseKey(vrefTokens, verseReference);
        }
    }
}
