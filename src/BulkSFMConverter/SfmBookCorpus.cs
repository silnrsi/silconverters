using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SFMConv
{
    /// <summary>
    /// Helps the Recent Files list remember a set of SFM files that were opened together: it describes them by the
    /// books they contain as a corpus spec (e.g. "GEN EXO RUT MAT-REV"), which is what's shown in the menu, and keeps
    /// the actual file paths, so clicking it opens the same files again.
    /// </summary>
    /// <remarks>
    /// A Recent Files entry for a set of files is stored as "{display}|{file 1}|{file 2}|..." ('|' can't occur in a
    /// Windows path, so single-file entries, which are just the path, are never confused with these).
    /// </remarks>
    internal static class SfmBookCorpus
    {
        internal const char EntrySeparator = '|';

        /// <summary>
        /// The Paratext book codes, in canonical order (the same order as PtxConNoteCommonLib.VerseNumberComparer.BookNameOrder)
        /// </summary>
        internal static readonly List<string> BookNameOrder = new List<string>
        {
            "GEN", "EXO", "LEV", "NUM", "DEU", "JOS", "JDG", "RUT", "1SA", "2SA", "1KI", "2KI", "1CH", "2CH", "EZR", "NEH",
            "EST", "JOB", "PSA", "PRO", "ECC", "SNG", "ISA", "JER", "LAM", "EZK", "DAN", "HOS", "JOL", "AMO", "OBA", "JON",
            "MIC", "NAM", "HAB", "ZEP", "HAG", "ZEC", "MAL",
            "MAT", "MRK", "LUK", "JHN", "ACT", "ROM", "1CO", "2CO", "GAL", "EPH", "PHP", "COL", "1TH", "2TH", "1TI", "2TI",
            "TIT", "PHM", "HEB", "JAS", "1PE", "2PE", "1JN", "2JN", "3JN", "JUD", "REV",
            "TOB", "JDT", "ESG", "WIS", "SIR", "BAR", "LJE", "S3Y", "SUS", "BEL", "1MA", "2MA", "3MA", "4MA", "1ES", "2ES",
            "MAN", "PS2", "ODA", "PSS", "JSA", "JDB", "TBS", "SST", "DNT", "BLT", "XXA", "XXB", "XXC", "XXD", "XXE", "XXF",
            "XXG", "FRT", "BAK", "OTH", "3ES", "EZA", "5EZ", "6EZ", "INT", "CNC", "GLO", "TDX", "NDX", "DAG", "PS3", "2BA",
            "LBA", "JUB", "ENO", "1MQ", "2MQ", "3MQ", "REP", "4BA", "LAO"
        };

        private const int MaxLinesToLookForId = 20;

        /// <summary>
        /// Returns the book code of an SFM file: from its \id line (if one of the first few lines), or else from
        /// its name if it follows the Paratext pattern (e.g. "41MATxyz.SFM" or "MATxyz.SFM"); or null if neither works.
        /// </summary>
        internal static string GetBookCode(string filePath, Encoding enc)
        {
            try
            {
                foreach (var line in File.ReadLines(filePath, enc).Take(MaxLinesToLookForId))
                {
                    var trimmed = line.TrimStart('\uFEFF', ' ', '\t');
                    if (!trimmed.StartsWith(@"\id ") && !trimmed.StartsWith("\\id\t"))
                        continue;
                    var code = trimmed.Substring(4).TrimStart().Split(new[] { ' ', '\t' }, 2)[0].ToUpperInvariant();
                    if (BookNameOrder.Contains(code))
                        return code;
                    break;
                }
            }
            catch (Exception)
            {
                // fall thru to the file name
            }

            var match = Regex.Match(Path.GetFileNameWithoutExtension(filePath) ?? String.Empty, @"^(?:\d\d)?([0-9A-Za-z]{3})");
            if (match.Success)
            {
                var code = match.Groups[1].Value.ToUpperInvariant();
                if (BookNameOrder.Contains(code))
                    return code;
            }
            return null;
        }

        /// <summary>
        /// Makes a corpus spec out of book codes: in canonical order, with runs of 3 or more consecutive books
        /// abbreviated as a range (e.g. GEN, EXO, RUT, MAT, MRK, ... REV => "GEN EXO RUT MAT-REV").
        /// </summary>
        internal static string ToCorpusSpec(IEnumerable<string> bookCodes)
        {
            var indices = bookCodes.Select(b => BookNameOrder.IndexOf(b)).Where(i => i >= 0).Distinct().OrderBy(i => i).ToList();
            var parts = new List<string>();
            for (int i = 0; i < indices.Count; )
            {
                int j = i;
                while ((j + 1 < indices.Count) && (indices[j + 1] == indices[j] + 1))
                    j++;
                if (j - i >= 2)
                    parts.Add($"{BookNameOrder[indices[i]]}-{BookNameOrder[indices[j]]}");
                else
                    for (int k = i; k <= j; k++)
                        parts.Add(BookNameOrder[indices[k]]);
                i = j + 1;
            }
            return String.Join(" ", parts);
        }

        /// <summary>
        /// Makes the Recent Files entry for a set of files opened together, e.g.
        ///     "C:\My Paratext 9 Projects\XYZ: GEN EXO RUT MAT-REV|C:\...\01GENXYZ.SFM|..."
        /// (any files whose book can't be determined are listed by name after the books)
        /// </summary>
        internal static string MakeRecentFilesEntry(string[] filePaths, Encoding enc)
        {
            var books = new List<string>();
            var unknown = new List<string>();
            foreach (var path in filePaths)
            {
                var code = GetBookCode(path, enc);
                if (code != null)
                    books.Add(code);
                else
                    unknown.Add(Path.GetFileName(path));
            }

            var description = ToCorpusSpec(books);
            if (unknown.Count > 0)
                description = String.Join(" ", new[] { description, $"+ {String.Join(", ", unknown)}" }.Where(s => s.Length > 0));

            var folders = filePaths.Select(p => Path.GetDirectoryName(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var display = (folders.Count == 1)
                            ? $"{folders[0]}: {description}"
                            : $"{description} (in {folders.Count} folders)";

            return display + EntrySeparator + String.Join(EntrySeparator.ToString(), filePaths);
        }

        internal static bool IsMultiFileEntry(string entry) => entry.IndexOf(EntrySeparator) >= 0;

        /// <summary>
        /// The text to show in the Recent Files menu for an entry
        /// </summary>
        internal static string GetDisplayText(string entry)
        {
            var index = entry.IndexOf(EntrySeparator);
            return (index >= 0) ? entry.Substring(0, index) : entry;
        }

        /// <summary>
        /// The files to open for an entry
        /// </summary>
        internal static string[] GetFilePaths(string entry)
        {
            return IsMultiFileEntry(entry)
                    ? entry.Split(EntrySeparator).Skip(1).Where(p => p.Length > 0).ToArray()
                    : new[] { entry };
        }
    }
}
