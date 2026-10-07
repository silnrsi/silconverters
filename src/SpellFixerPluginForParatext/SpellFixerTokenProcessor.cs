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
