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
            else if (s.VerseRefStart == null)
                reason = "the selection isn't in a verse";
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
