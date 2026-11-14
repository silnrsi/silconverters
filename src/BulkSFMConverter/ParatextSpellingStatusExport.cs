using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using SIL.SpellFixerPluginForParatext;  // for SpellingStatusMerger (linked from the SpellFixerPluginForParatext project)
using SpellingFixer30;

namespace SFMConv
{
    /// <summary>
    /// A word whose spelling status in a Paratext project's SpellingStatus.xml disagrees with the SpellFixer project's
    /// (i.e. one that the user has to decide about, rather than one that can just be added)
    /// </summary>
    internal class ParatextSpellingConflict
    {
        public string Word { get; set; }

        /// <summary>"R" (correct) or "W" (wrong)</summary>
        public string ParatextState { get; set; }

        /// <summary>Paratext's correction for the word (if it's "W" and has one)</summary>
        public string ParatextCorrection { get; set; }

        /// <summary>SpellFixer's correction for the word, or null if SpellFixer has it as a correct (good) word</summary>
        public string SpellFixerCorrection { get; set; }

        internal XElement Status { get; set; }

        public string ParatextDescription => (ParatextState == ParatextSpellingStatusExport.StateRight)
            ? "correct"
            : String.IsNullOrEmpty(ParatextCorrection) ? "misspelled" : $"misspelled → {ParatextCorrection}";

        public string SpellFixerDescription => (SpellFixerCorrection == null)
            ? "correct"
            : $"misspelled → {SpellFixerCorrection}";
    }

    /// <summary>
    /// Brings a SpellFixer project's good words and spelling corrections into a Paratext project's SpellingStatus.xml file
    /// (the inverse of CscProject.AddParatextSpellingStatusToCheckLists):
    ///   <SpellingStatus>
    ///     <Status Word="the" State="R" />
    ///     <Status Word="teh" State="W"><Correction>the</Correction></Status>
    ///   </SpellingStatus>
    /// Words that Paratext doesn't have yet (or has as wrong, but w/o a correction) are just added (to the in-memory
    /// document); words that Paratext has differently are collected as Conflicts for the user to decide about (see Apply).
    /// Nothing is written until the file is saved (see Export).
    /// </summary>
    internal class ParatextSpellingStatusExport
    {
        public const string StateRight = "R";
        public const string StateWrong = "W";
        private const string StatusName = "Status";
        private const string WordName = "Word";
        private const string StateName = "State";
        private const string CorrectionName = "Correction";

        public XDocument Document { get; private set; }
        public List<ParatextSpellingConflict> Conflicts { get; } = new List<ParatextSpellingConflict>();

        public int NumApprovedWordsAdded { get; private set; }
        public int NumCorrectionsAdded { get; private set; }
        public int NumConflictsReplaced { get; private set; }

        /// <summary>the number of good words or corrections that aren't single words (e.g. phrases), which Paratext can't hold</summary>
        public int NumSkipped { get; private set; }

        public bool HasChanges => (NumApprovedWordsAdded + NumCorrectionsAdded + NumConflictsReplaced) > 0;

        /// <summary>
        /// Puts the SpellFixer project's good words (as State="R") and spelling corrections (as State="W" w/ a Correction)
        /// into a Paratext project's SpellingStatus.xml file. Words Paratext doesn't have yet are just added; words Paratext
        /// has differently (e.g. a good word that Paratext has as wrong, or a correction that Paratext corrects to something
        /// else) are listed for the user to decide which to replace. Paratext must not be running (or it'd overwrite the
        /// changes), and a backup of the file is put in the SpellFixer project's folder before it's changed.
        /// </summary>
        /// <param name="summary">a description of what was done (e.g. for a status bar)</param>
        /// <returns>true if the file was updated (or was already up to date); false if the user cancelled</returns>
        public static bool Export(CscProject project, string filespec, IWin32Window owner, out string summary)
        {
            if (!File.Exists(filespec))
                throw new FileNotFoundException($"The Paratext spelling status file '{filespec}' doesn't exist", filespec);

            summary = "Exporting spelling data to Paratext cancelled";
            if (!CheckParatextIsClosed(owner))
                return false;

            var export = Plan(filespec, project.KnownGoodWords.ToList(), project.SpellingCorrections.ToList());
            if (export.Conflicts.Any())
            {
                using (var dlg = new ParatextConflictsForm(export.Conflicts, filespec, project.Font))
                {
                    if (dlg.ShowDialog(owner) != DialogResult.OK)
                        return false;
                    export.Apply(dlg.ConflictsToReplace);
                }
            }

            var numLeftAlone = export.Conflicts.Count - export.NumConflictsReplaced;
            var details = $"{export.NumApprovedWordsAdded} approved words added, {export.NumCorrectionsAdded} corrections added, " +
                          $"{export.NumConflictsReplaced} differences replaced, {numLeftAlone} differences left as Paratext has them";
            if (export.NumSkipped > 0)
                details += $", {export.NumSkipped} phrases skipped";

            if (!export.HasChanges)
            {
                summary = $"Paratext's spelling data ('{filespec}') already has all of SpellFixer's ({details})";
                return true;
            }

            // (in case Paratext was started while the user was deciding)
            if (!CheckParatextIsClosed(owner))
                return false;

            var paratextProjectName = Path.GetFileName(Path.GetDirectoryName(filespec));
            File.Copy(filespec, Path.Combine(project.ProjectDirectory, $"{paratextProjectName} {Path.GetFileName(filespec)}.bak"), true);
            SpellingStatusMerger.Save(export.Document, filespec);   // (the way Paratext writes it, via a temp file)

            summary = $"Spelling data exported to Paratext ('{filespec}'): {details}";
            return true;
        }

        public static ParatextSpellingStatusExport Plan(string filespec, IEnumerable<string> goodWords,
                                                        IEnumerable<KeyValuePair<string, string>> corrections)
        {
            var export = new ParatextSpellingStatusExport { Document = XDocument.Load(filespec) };
            if (export.Document.Root == null)
                throw new ApplicationException($"The file '{filespec}' isn't a Paratext SpellingStatus file");
            export.Plan(goodWords, corrections.ToList());
            return export;
        }

        private void Plan(IEnumerable<string> goodWords, List<KeyValuePair<string, string>> corrections)
        {
            var root = Document.Root;
            var statusByWord = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var status in root.Elements(StatusName))
            {
                var word = (string)status.Attribute(WordName);
                if (!String.IsNullOrEmpty(word) && !statusByWord.ContainsKey(word))
                    statusByWord.Add(word, status);
            }

            // the good words (incl. the corrected spellings, which should already be good words, but just in case)
            var goodSet = new HashSet<string>(goodWords, StringComparer.Ordinal);
            goodSet.UnionWith(corrections.Select(c => c.Value).Where(v => !String.IsNullOrEmpty(v)));
            foreach (var good in goodSet.OrderBy(w => w, StringComparer.Ordinal))
            {
                if (!IsSingleWord(good))
                {
                    NumSkipped++;
                    continue;
                }

                if (!statusByWord.TryGetValue(good, out var status))
                {
                    statusByWord.Add(good, AddStatus(root, good, StateRight, null));
                    NumApprovedWordsAdded++;
                }
                else if (StateOf(status) == StateWrong)
                {
                    Conflicts.Add(new ParatextSpellingConflict
                    {
                        Word = good,
                        ParatextState = StateWrong,
                        ParatextCorrection = CorrectionOf(status),
                        SpellFixerCorrection = null,
                        Status = status
                    });
                }
                else if (StateOf(status) != StateRight)
                {
                    // (some state we don't know about; treat it as not having a status)
                    SetStatus(status, StateRight, null);
                    NumApprovedWordsAdded++;
                }
            }

            foreach (var correction in corrections.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                var bad = correction.Key;
                var good = correction.Value;
                if (!IsSingleWord(bad) || !IsSingleWord(good))
                {
                    NumSkipped++;
                    continue;
                }

                // (if SpellFixer has it as both good and bad, which shouldn't happen, the good word was dealt with above)
                if (goodSet.Contains(bad))
                    continue;

                if (!statusByWord.TryGetValue(bad, out var status))
                {
                    statusByWord.Add(bad, AddStatus(root, bad, StateWrong, good));
                    NumCorrectionsAdded++;
                    continue;
                }

                var state = StateOf(status);
                var paratextCorrection = CorrectionOf(status);
                if ((state == StateWrong) && (paratextCorrection == good))
                    continue;   // already the same

                if ((state == StateWrong) && String.IsNullOrEmpty(paratextCorrection))
                {
                    // Paratext knows it's wrong, but doesn't have a correction for it, so just add ours
                    SetStatus(status, StateWrong, good);
                    NumCorrectionsAdded++;
                }
                else if ((state == StateWrong) || (state == StateRight))
                {
                    Conflicts.Add(new ParatextSpellingConflict
                    {
                        Word = bad,
                        ParatextState = state,
                        ParatextCorrection = paratextCorrection,
                        SpellFixerCorrection = good,
                        Status = status
                    });
                }
                else
                {
                    SetStatus(status, StateWrong, good);
                    NumCorrectionsAdded++;
                }
            }
        }

        /// <summary>
        /// Replaces Paratext's status with SpellFixer's for the given conflicts (the others are left as Paratext has them)
        /// </summary>
        public void Apply(IEnumerable<ParatextSpellingConflict> conflictsToReplace)
        {
            foreach (var conflict in conflictsToReplace)
            {
                if (conflict.SpellFixerCorrection == null)
                    SetStatus(conflict.Status, StateRight, null);
                else
                    SetStatus(conflict.Status, StateWrong, conflict.SpellFixerCorrection);
                NumConflictsReplaced++;
            }
        }

        private static bool CheckParatextIsClosed(IWin32Window owner)
        {
            while (IsParatextRunning())
            {
                if (MessageBox.Show(owner, "Paratext is running. It has to be closed before its spelling data can be changed (otherwise it would overwrite the changes)." +
                                           $"{Environment.NewLine}{Environment.NewLine}Close Paratext and then click 'Retry'.",
                                    CscProject.ApplicationCaption, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Paratext must not be running when its SpellingStatus.xml file is changed (it would overwrite the changes
        /// with what it has in memory)
        /// </summary>
        public static bool IsParatextRunning()
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.ProcessName.StartsWith("Paratext", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // (e.g. it exited while we were looking at it)
                }
                finally
                {
                    process.Dispose();
                }
            }
            return false;
        }

        private static bool IsSingleWord(string word)
        {
            return !String.IsNullOrEmpty(word) && !word.Any(Char.IsWhiteSpace);
        }

        private static string StateOf(XElement status)
        {
            return (string)status.Attribute(StateName);
        }

        private static string CorrectionOf(XElement status)
        {
            return status.Element(CorrectionName)?.Value;
        }

        private static XElement AddStatus(XElement root, string word, string state, string correction)
        {
            var status = new XElement(StatusName, new XAttribute(WordName, word));
            root.Add(status);
            SetStatus(status, state, correction);
            return status;
        }

        private static void SetStatus(XElement status, string state, string correction)
        {
            status.SetAttributeValue(StateName, state);
            status.Elements(CorrectionName).Remove();
            if (correction != null)
                status.Add(new XElement(CorrectionName, correction));
        }
    }
}
