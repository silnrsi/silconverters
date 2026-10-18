using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
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
            return ValidateSelection(s, null, out word, out wordOffset, out reason);
        }

        /// <summary>
        /// Checks that the selection is a single word within one verse and returns that word, without the leading
        /// and trailing characters the SpellFixer's CC table doesn't consider part of a word (the same ones
        /// CscProject.TrimWhiteSpaceAndPunctuation trims: whitespace, digits, Unicode punctuation (e.g. ',' and the
        /// danda), plus extraTrimCharacters, e.g. the project's script system's extra punctuation), and the offset
        /// of the word in the verse's USFM
        /// </summary>
        public static bool ValidateSelection(SelectionInfo s, ICollection<char> extraTrimCharacters, out string word, out int wordOffset, out string reason)
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
            else if (s.VerseRefStart == null)
                reason = "the selection isn't in a verse";
            else if (!IsSameVerse(s.VerseRefStart, s.VerseRefEnd))
                reason = "the selection spans more than one verse";

            if (reason != null)
                return false;

            var start = SpellFixerTokenProcessor.TrimmedWordBounds(text, extraTrimCharacters, out int length);
            if (length <= 0)
            {
                reason = "the selection doesn't have a word in it (only punctuation, digits or spaces)";
                return false;
            }

            var candidate = text.Substring(start, length);
            if (candidate.Any(Char.IsWhiteSpace))
            {
                reason = "select a single word (the selection has more than one)";
                return false;
            }

            word = candidate;
            wordOffset = s.Offset + start;
            return true;
        }

        private static bool IsSameVerse(IVerseRef start, IVerseRef end)
        {
            return (end == null) ||
                   ((start != null) && (start.BookNum == end.BookNum) && (start.ChapterNum == end.ChapterNum) && (start.VerseNum == end.VerseNum));
        }

        /// <summary>
        /// Replaces 'bad' (selected at badOffset) with 'good' in the verse's current USFM, but only if the text
        /// around it still looks the way it did when the selection was made. The selection's contexts aren't
        /// necessarily exactly the verse's USFM (e.g. line endings, or where the verse is considered to end), so
        /// only up to 20 characters on either side of the word are compared (ignoring differences in whitespace),
        /// and if the word isn't at badOffset any more, the one place in the verse where that matches is used.
        /// </summary>
        public static bool TryReplaceInVerse(string currentVerseUsfm, SelectionInfo s, string bad, int badOffset, string good,
                                             out string newVerseUsfm, out string reason)
        {
            newVerseUsfm = null;
            reason = null;
            currentVerseUsfm ??= String.Empty;
            var selected = s.SelectedText ?? String.Empty;

            // the text before and after the word itself (which may be only part of the selection)
            var wordInSelection = badOffset - s.Offset;
            if (String.IsNullOrEmpty(bad) || (wordInSelection < 0) || (wordInSelection + bad.Length > selected.Length) ||
                (String.CompareOrdinal(selected, wordInSelection, bad, 0, bad.Length) != 0))
            {
                reason = $"'{bad}' isn't the selected word";
                return false;
            }

            var before = (s.BeforeContext ?? String.Empty) + selected.Substring(0, wordInSelection);
            var after = selected.Substring(wordInSelection + bad.Length) + (s.AfterContext ?? String.Empty);

            var index = OccursAt(currentVerseUsfm, badOffset, bad) && NeighbourhoodMatches(currentVerseUsfm, badOffset, bad.Length, before, after)
                            ? badOffset
                            : FindUniqueMatch(currentVerseUsfm, bad, before, after, out reason);
            if (index < 0)
                return false;

            newVerseUsfm = currentVerseUsfm.Substring(0, index) + good + currentVerseUsfm.Substring(index + bad.Length);
            return true;
        }

        private static bool OccursAt(string verse, int index, string word)
        {
            return (index >= 0) && (index + word.Length <= verse.Length) && (String.CompareOrdinal(verse, index, word, 0, word.Length) == 0);
        }

        // the index of the only occurrence of word in verse whose neighbourhood matches (or -1 and why not)
        private static int FindUniqueMatch(string verse, string word, string before, string after, out string reason)
        {
            reason = null;
            var found = -1;
            for (var i = verse.IndexOf(word, StringComparison.Ordinal); i >= 0; i = verse.IndexOf(word, i + 1, StringComparison.Ordinal))
            {
                if (!NeighbourhoodMatches(verse, i, word.Length, before, after))
                    continue;

                if (found >= 0)
                {
                    reason = $"'{word}' couldn't be located unambiguously in the verse";
                    return -1;
                }
                found = i;
            }

            if (found < 0)
                reason = "the text around the selection has changed since the word was selected";
            return found;
        }

        // compare up to 20 characters on either side of the word (ignoring differences in whitespace, e.g. \r\n vs. \n)
        private static bool NeighbourhoodMatches(string verse, int index, int length, string before, string after)
        {
            var beforeTail = CollapseWhitespace(before.Substring(Math.Max(0, before.Length - NeighbourhoodLength))).TrimStart();
            var afterHead = CollapseWhitespace(after.Substring(0, Math.Min(NeighbourhoodLength, after.Length))).TrimEnd();

            return CollapseWhitespace(verse.Substring(0, index)).EndsWith(beforeTail, StringComparison.Ordinal) &&
                   CollapseWhitespace(verse.Substring(index + length)).StartsWith(afterHead, StringComparison.Ordinal);
        }

        private static string CollapseWhitespace(string s)
        {
            return Whitespace.Replace(s, " ");
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
