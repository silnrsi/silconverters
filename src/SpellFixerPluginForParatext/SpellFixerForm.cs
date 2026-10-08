using Paratext.PluginInterfaces;
using SIL.ParatextBackTranslationHelperPlugin;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SIL.SpellFixerPluginForParatext
{
    internal partial class SpellFixerForm : Form
    {
        private readonly IPluginHost _host;
        private readonly IPluginObject _plugin;
        private readonly IProject _project;
        private readonly Action<IVerseRef> _setSyncReference;
        private IVerseRef _verseReference;
        private SpellFixerProject _spellFixerProject;
        private bool _isChecking;

        private static readonly Bitmap _pinUp = LoadBitmap("pinup.bmp");
        private static readonly Bitmap _pinDown = LoadBitmap("pindown.bmp");

        public SpellFixerForm(IPluginHost host, IPluginObject plugin, IProject project, IVerseRef initialVerseReference, Action<IVerseRef> setSyncReference)
        {
            InitializeComponent();
            InitializeSettings();

            _host = host;
            _plugin = plugin;
            _project = project;
            _setSyncReference = setSyncReference;
            SetVerseReference(initialVerseReference);

            labelPtxProject.Text = project.ShortName;
            SetPinToTop(Properties.Settings.Default.PinToTop);

            _host.VerseRefChanged += Host_VerseRefChanged;
            _host.ActiveWindowSelectionChanged += Host_ActiveWindowSelectionChanged;
            _lastSelection = CurrentSelection;
        }

        public string ProjectShortName => _project.ShortName;

        #region Paratext selection

        private SelectionInfo _lastSelection;

        private void Host_ActiveWindowSelectionChanged(IPluginHost sender, IParatextChildState activeWindowState, IReadOnlyList<ISelection> currentSelections)
        {
            // only keep selections in this project's (Scripture text) windows; moving to our own window must not lose it
            if (activeWindowState?.Project?.ShortName != _project.ShortName)
                return;

            var selection = currentSelections?.OfType<IScriptureTextSelection>().FirstOrDefault();
            if (selection != null)
                _lastSelection = SelectionInfo.FromSelection(selection);
        }

        private SelectionInfo CurrentSelection
        {
            get
            {
                var state = _host.ActiveWindowState;
                if (state?.Project?.ShortName == _project.ShortName)
                {
                    var selection = state.Selections?.OfType<IScriptureTextSelection>().FirstOrDefault();
                    if (selection != null)
                        return SelectionInfo.FromSelection(selection);
                }
                return _lastSelection;
            }
        }

        #endregion

        private static Bitmap LoadBitmap(string name)
        {
            return new Bitmap(typeof(SpellFixerForm).Assembly.GetManifestResourceStream($"SIL.SpellFixerPluginForParatext.{name}"));
        }

        /// <summary>
        /// If Ptx is upgraded, then we lose the settings. This upgrades them the first time after an install.
        /// </summary>
        private static void InitializeSettings()
        {
            if (!Properties.Settings.Default.UpgradeSettings)
                return;

            Properties.Settings.Default.Upgrade();
            Properties.Settings.Default.UpgradeSettings = false;
            Properties.Settings.Default.Save();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadSavedSpellFixerProject();
            UpdateButtonStates();
        }

        private void SpellFixerForm_Load(object sender, EventArgs e)
        {
            var location = Properties.Settings.Default.WindowLocation;
            if ((location != Point.Empty) && Screen.AllScreens.Any(s => s.WorkingArea.Contains(location)))
                Location = location;
        }

        private void SpellFixerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isChecking || _isApplyingFix)
            {
                e.Cancel = true;    // let the run (or the fix) finish (click Cancel in the query dialog to stop a run)
                return;
            }

            _host.VerseRefChanged -= Host_VerseRefChanged;
            _host.ActiveWindowSelectionChanged -= Host_ActiveWindowSelectionChanged;
            _fixSpellingForm?.Close();

            if (WindowState == FormWindowState.Normal)
                Properties.Settings.Default.WindowLocation = Location;
            Properties.Settings.Default.Save();
        }

        private void Host_VerseRefChanged(IPluginHost sender, IVerseRef newReference, SyncReferenceGroup group)
        {
            // while checking, we're the ones moving Paratext around, so don't change where the next run would start
            if (_isChecking)
                return;

            SetVerseReference(newReference);
        }

        private void SetVerseReference(IVerseRef verseReference)
        {
            _verseReference = (verseReference?.RepresentsMultipleVerses ?? false)
                                ? verseReference.AllVerses.First()
                                : verseReference;
            labelVerse.Text = _verseReference?.ToString();
        }

        #region SpellFixer project (remembered per Paratext project)

        private void LoadSavedSpellFixerProject()
        {
            var converterName = GetSavedConverterName(_project.ShortName);
            if (String.IsNullOrEmpty(converterName))
            {
                ChooseSpellFixerProject();
                return;
            }

            try
            {
                SetSpellFixerProject(SpellFixerProject.FromConverterName(converterName));
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"Unable to load Spell Fixer project '{converterName}': {ex.Message}");
                SaveConverterName(_project.ShortName, null);
                ChooseSpellFixerProject();
            }
        }

        private void ChooseSpellFixerProject()
        {
            try
            {
                var spellFixerProject = SpellFixerProject.QueryUser();
                if (spellFixerProject == null)
                    return;     // keep what we had

                SetSpellFixerProject(spellFixerProject);
                SaveConverterName(_project.ShortName, spellFixerProject.ConverterName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, SpellFixerPlugin.PluginName);
            }
        }

        private void SetSpellFixerProject(SpellFixerProject spellFixerProject)
        {
            _spellFixerProject = spellFixerProject;
            _queryForm?.Dispose();
            _queryForm = null;
            _fixSpellingForm?.Close();
            labelSpellFixerProject.Text = spellFixerProject.DisplayName;
            UpdateButtonStates();
        }

        // the setting is a list of "PtxShortName=SpellFixerEncConverterName" entries
        private static string GetSavedConverterName(string ptxProjectShortName)
        {
            var prefix = ptxProjectShortName + "=";
            return Properties.Settings.Default.MapPtxProjectToSpellFixerConverter?
                                              .Cast<string>()
                                              .FirstOrDefault(s => s.StartsWith(prefix))?
                                              .Substring(prefix.Length);
        }

        private static void SaveConverterName(string ptxProjectShortName, string converterName)
        {
            var prefix = ptxProjectShortName + "=";
            var entries = Properties.Settings.Default.MapPtxProjectToSpellFixerConverter?.Cast<string>()
                                                     .Where(s => !s.StartsWith(prefix))
                                                     .ToList()
                          ?? new List<string>();
            if (!String.IsNullOrEmpty(converterName))
                entries.Add(prefix + converterName);

            var collection = new StringCollection();
            collection.AddRange(entries.ToArray());
            Properties.Settings.Default.MapPtxProjectToSpellFixerConverter = collection;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region Buttons

        private void UpdateButtonStates()
        {
            var haveProject = (_spellFixerProject != null);
            var isBusy = _isChecking || _isApplyingFix;
            buttonChooseProject.Enabled = !isBusy;
            buttonCheck.Enabled = haveProject && !isBusy && ((_fixSpellingForm == null) || _fixSpellingForm.IsDisposed);
            buttonAssignCorrectSpelling.Enabled = haveProject && !isBusy;
            buttonFindReplacementRule.Enabled = haveProject && !isBusy;
            buttonEditSpellingFixes.Enabled = haveProject && !isBusy;
        }

        private void ButtonChooseProject_Click(object sender, EventArgs e)
        {
            ChooseSpellFixerProject();
        }

        private FixSpellingForm _fixSpellingForm;

        // the selection (and the word in it) the Fix Spelling dialog was opened for
        private SelectionInfo _fixSelection;
        private string _fixWord;
        private int _fixWordOffset;

        private void ButtonAssignCorrectSpelling_Click(object sender, EventArgs e)
        {
            if (_isApplyingFix)
                return;     // (the button is disabled then anyway)

            var selection = CurrentSelection;
            if (!SelectionReplacer.ValidateSelection(selection, out string word, out int wordOffset, out string reason))
            {
                MessageBox.Show($"Select the misspelled word in the {_project.ShortName} text window first (one word, within one verse): {reason}.",
                                SpellFixerPlugin.PluginName);
                return;
            }

            _fixSelection = selection;
            _fixWord = word;
            _fixWordOffset = wordOffset;

            if ((_fixSpellingForm == null) || _fixSpellingForm.IsDisposed)
            {
                _projectFont ??= GetProjectFont();
                _fixSpellingForm = new FixSpellingForm(_spellFixerProject, _projectFont, _project.Language?.IsRtoL ?? false,
                                                       _project.VernacularKeyboard, _host.DefaultKeyboard, ApplyFix);
                _fixSpellingForm.FormClosed += (s, args) =>
                {
                    _fixSpellingForm = null;
                    UpdateButtonStates();
                };
            }

            _fixSpellingForm.TopMost = TopMost;
            _fixSpellingForm.LoadWord(word, selection.VerseRefStart?.ToString());
            if (!_fixSpellingForm.Visible)
                _fixSpellingForm.Show(this);
            _fixSpellingForm.Activate();
            UpdateButtonStates();
        }

        private void ButtonFindReplacementRule_Click(object sender, EventArgs e)
        {
            if (SelectionReplacer.ValidateSelection(CurrentSelection, out string word, out _, out _))
                TryAction(() => _spellFixerProject.FindReplacementRule(word));
            else
                DoWithClipboardWord(clipboardWord => _spellFixerProject.FindReplacementRule(clipboardWord));
        }

        /// <summary>
        /// Called when OK is clicked in the Fix Spelling dialog: add the rule, replace the selected word in Paratext,
        /// and record the fix for Paratext's spelling status. Returns true to close the dialog.
        /// </summary>
        private bool ApplyFix(string bad, string good)
        {
            // the project's questions (e.g. "already a good word...") are modal only to the Fix Spelling dialog, so
            //  don't let this window change what we're fixing while they're up (e.g. by re-clicking Assign), and only
            //  use the selection as it was when OK was clicked
            var selection = _fixSelection;
            var fixWord = _fixWord;
            var fixWordOffset = _fixWordOffset;
            var owner = _fixSpellingForm;

            _isApplyingFix = true;
            UpdateButtonStates();
            try
            {
                // only if the dialog's bad form is still the selected word do we know where it is (for the context
                //  and the replacement in Paratext)
                var isSelectedWord = (selection != null) && (bad == fixWord);

                try
                {
                    var context = isSelectedWord && (_spellFixerProject.WordsInContext > 0)
                                    ? new List<string> { SelectionReplacer.BuildContext(selection, fixWord, fixWordOffset, _spellFixerProject.WordsInContext) }
                                    : null;
                    if (!_spellFixerProject.AssignCorrectSpelling(bad, good, context))
                    {
                        // the user said No/Cancel to one of the project's questions; leave the dialog up so they can adjust
                        SetStatus($"The rule '{bad}' → '{good}' wasn't added, so nothing was changed.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _host.Log(_plugin, $"SpellFixer: AssignCorrectSpelling: {ex}");
                    MessageBox.Show(owner, $"Unable to add the rule '{bad}' → '{good}':{Environment.NewLine}{ex.Message}", SpellFixerPlugin.PluginName);
                    return false;
                }

                var problem = isSelectedWord
                                ? ReplaceSelectedWord(selection, fixWord, fixWordOffset, good)
                                : $"The rule was added, but the text in Paratext wasn't changed because the bad form ('{bad}') isn't the selected word ('{fixWord}').";

                SpellingStatusRecorder.Record(_project, _plugin, bad, good, message => MessageBox.Show(message, SpellFixerPlugin.PluginName));

                SetStatus($"Added the rule '{bad}' → '{good}'" + ((problem == null) ? $" and fixed it in {selection.VerseRefStart}." : "."));
                if (problem != null)
                    MessageBox.Show(owner, problem, SpellFixerPlugin.PluginName);

                if (ReferenceEquals(_fixSelection, selection))
                    _fixSelection = null;
                return true;
            }
            finally
            {
                _isApplyingFix = false;
                UpdateButtonStates();
            }
        }

        private bool _isApplyingFix;

        /// <summary>
        /// Replaces the selected occurrence of the bad word with the good one (if the verse hasn't changed since
        /// it was selected). Returns null if it worked, or else the reason it didn't.
        /// </summary>
        private string ReplaceSelectedWord(SelectionInfo selection, string bad, int badOffset, string good)
        {
            var verseReference = selection.VerseRefStart;
            IWriteLock writeLock = null;
            try
            {
                writeLock = _project.RequestWriteLock(_plugin, ReleaseRequested, verseReference.BookNum, verseReference.ChapterNum);
                if (writeLock == null)
                    return $"The rule was added, but you don't have edit privilege on {verseReference.BookCode} {verseReference.ChapterNum} of the {_project.ShortName} project, so the text wasn't changed.";

                var chapterUsfm = _project.GetUSFM(verseReference.BookNum, verseReference.ChapterNum);
                var verseUsfm = _project.GetUSFM(verseReference.BookNum, verseReference.ChapterNum, verseReference.VerseNum);

                if (!SelectionReplacer.TryReplaceInVerse(verseUsfm, selection, bad, badOffset, good, out string newVerseUsfm, out string reason))
                    return $"The rule was added, but {verseReference} wasn't changed because {reason}. Fix it by hand (or select it again).";

                if (!SelectionReplacer.ReplaceVerseInChapter(chapterUsfm, verseUsfm, newVerseUsfm, out string newChapterUsfm))
                    return $"The rule was added, but {verseReference} wasn't changed because the verse couldn't be located in the chapter.";

                _project.PutUSFM(writeLock, newChapterUsfm, verseReference.BookNum);
                return null;
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: ReplaceSelectedWord: {ex}");
                return $"The rule was added, but changing {verseReference} failed:{Environment.NewLine}{ex.Message}";
            }
            finally
            {
                writeLock?.Dispose();
                _setSyncReference(verseReference);
            }
        }

        private void ButtonEditSpellingFixes_Click(object sender, EventArgs e)
        {
            TryAction(() => _spellFixerProject.EditSpellingFixes());
        }

        private void DoWithClipboardWord(Action<string> action)
        {
            var word = Clipboard.ContainsText(TextDataFormat.UnicodeText)
                        ? Clipboard.GetText(TextDataFormat.UnicodeText)?.Trim()
                        : null;
            if (String.IsNullOrEmpty(word))
            {
                MessageBox.Show("Copy a word (e.g. from Paratext) to the clipboard first, and then click this button again.", SpellFixerPlugin.PluginName);
                return;
            }

            TryAction(() => action(word));
        }

        private void TryAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, SpellFixerPlugin.PluginName);
            }
        }

        private void ButtonPinToTop_Click(object sender, EventArgs e)
        {
            SetPinToTop(!TopMost);
            Properties.Settings.Default.PinToTop = TopMost;
            Properties.Settings.Default.Save();
        }

        private void SetPinToTop(bool pinToTop)
        {
            TopMost = pinToTop;
            buttonPinToTop.Image = pinToTop ? _pinDown : _pinUp;
        }

        private void ButtonCheck_Click(object sender, EventArgs e)
        {
            CheckFromCurrentVerse();
        }

        #endregion

        private SpellFixerQueryForm _queryForm;

        private Font _projectFont;
        private bool _hasShownConvertError;

        private void CheckFromCurrentVerse()
        {
            if ((_spellFixerProject == null) || (_verseReference == null))
                return;

            var session = new SpellFixerSession(ConvertWord, AskUser);
            var totalWordsFixed = 0;
            var chapterReference = _verseReference;
            var startAtBeginningOfChapter = false;
            _hasShownConvertError = false;

            _lastCheckedReference = null;

            SetChecking(true);
            try
            {
                while (chapterReference != null)
                {
                    if (!CheckChapter(chapterReference, startAtBeginningOfChapter, session, ref totalWordsFixed))
                        break;  // cancelled or couldn't write

                    chapterReference = QueryNextChapter(chapterReference);
                    startAtBeginningOfChapter = true;
                }
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: CheckFromCurrentVerse: {ex}");
                MessageBox.Show($"Checking stopped because of an error:{Environment.NewLine}{ex.Message}", SpellFixerPlugin.PluginName);
            }
            finally
            {
                SetChecking(false);
            }

            // so the next run picks up where this one left off (which is also where Paratext is now)
            if (_lastCheckedReference != null)
                SetVerseReference(_lastCheckedReference);

            SetStatus($"Done: {totalWordsFixed} word(s) fixed.");
        }

        private IVerseRef _lastCheckedReference;

        private void SetChecking(bool isChecking)
        {
            _isChecking = isChecking;
            UpdateButtonStates();
        }

        private void SetStatus(string status)
        {
            textBoxStatus.Text = status;
            textBoxStatus.Refresh();
        }

        /// <summary>
        /// Checks one chapter, writing each verse that has accepted fixes. Returns false if the run should stop
        /// (the user cancelled or a verse couldn't be written)
        /// </summary>
        private bool CheckChapter(IVerseRef chapterReference, bool startAtBeginningOfChapter, SpellFixerSession session, ref int totalWordsFixed)
        {
            var vrefTokens = GroupByVerse(LoadChapter(chapterReference));
            if (vrefTokens == null)
                return true;    // some books don't return proper things (e.g. GLO), so just go on

            var verseKey = startAtBeginningOfChapter
                            ? vrefTokens.Keys.FirstOrDefault()
                            : SpellFixerTokenProcessor.StartVerseKey(vrefTokens, chapterReference);

            while (verseKey != null)
            {
                var verseTokens = vrefTokens[verseKey];
                var verseReference = verseTokens.First().VerseRef;
                _lastCheckedReference = verseReference;
                _setSyncReference(verseReference);
                SetStatus($"Checking {verseReference}...");

                var cancelled = false;
                var fixedTokens = SpellFixerTokenProcessor.FixVerseTokens(verseTokens, session.FixWord, ref cancelled, out int wordsFixed);
                if (wordsFixed > 0)
                {
                    if (!WriteVerse(chapterReference, verseKey, verseTokens, fixedTokens))
                        return false;

                    totalWordsFixed += wordsFixed;
                    SetStatus($"Wrote {wordsFixed} fix(es) to {verseReference}");

                    vrefTokens = GroupByVerse(LoadChapter(chapterReference));     // Paratext has the new data now
                    if (vrefTokens == null)
                        return false;
                }

                if (cancelled)
                    return false;

                verseKey = UsfmChapterTokens.NextVerseKey(vrefTokens, verseKey);
            }

            return true;
        }

        // the chapter's tokens in document order (null if there aren't any)
        private List<IUSFMToken> LoadChapter(IVerseRef chapterReference)
        {
            var chapterTokens = _project.GetUSFMTokens(chapterReference.BookNum, chapterReference.ChapterNum)?.ToList();
            return ((chapterTokens == null) || !chapterTokens.Any())
                    ? null
                    : chapterTokens;
        }

        private static SortedDictionary<string, List<IUSFMToken>> GroupByVerse(List<IUSFMToken> chapterTokens)
        {
            return (chapterTokens == null) ? null : UsfmChapterTokens.GroupByVerse(chapterTokens);
        }

        /// <summary>
        /// Writes the chapter back to Paratext with the verse's tokens replaced by the fixed ones (but only if the
        /// verse wasn't changed in Paratext since we read it)
        /// </summary>
        private bool WriteVerse(IVerseRef chapterReference, string verseKey, List<IUSFMToken> originalVerseTokens, List<IUSFMToken> fixedVerseTokens)
        {
            IWriteLock writeLock = null;
            try
            {
                writeLock = _project.RequestWriteLock(_plugin, ReleaseRequested, chapterReference.BookNum, chapterReference.ChapterNum);
                if (writeLock == null)
                {
                    // e.g. the user doesn't have edit permissions on this chapter
                    MessageBox.Show($"You don't have edit privilege on this chapter: {chapterReference.BookCode} {chapterReference.ChapterNum} of the {_project.ShortName} project", SpellFixerPlugin.PluginName);
                    return false;
                }

                // getting the lock may have saved changes the user had made in Paratext, so make sure we aren't
                //  about to overwrite them with stale data
                //  (markers included, e.g. if they changed \p to \q1)
                var currentChapterTokens = LoadChapter(chapterReference);
                var currentTokens = GroupByVerse(currentChapterTokens);
                if ((currentTokens == null) || !currentTokens.TryGetValue(verseKey, out List<IUSFMToken> currentVerseTokens)
                    || (SpellFixerTokenProcessor.VerseSignature(currentVerseTokens) != SpellFixerTokenProcessor.VerseSignature(originalVerseTokens)))
                {
                    MessageBox.Show($"{fixedVerseTokens.First().VerseRef} was changed in Paratext while it was being checked, so the fixes to it weren't saved. Check it again.", SpellFixerPlugin.PluginName);
                    return false;
                }

                // replace the verse's tokens in place, so the rest of the chapter is written back exactly as it is
                //  (e.g. even if verses are out of order)
                var chapterTokens = SpellFixerTokenProcessor.SpliceVerse(currentChapterTokens, currentVerseTokens, fixedVerseTokens);
                _project.PutUSFMTokens(writeLock, chapterTokens, chapterReference.BookNum);
                return true;
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: WriteVerse: {ex}");
                MessageBox.Show($"Unable to write the fixes to Paratext:{Environment.NewLine}{ex.Message}", SpellFixerPlugin.PluginName);
                return false;
            }
            finally
            {
                writeLock?.Dispose();
            }
        }

        private void ReleaseRequested(IWriteLock writeLock)
        {
            writeLock?.Dispose();
        }

        /// <summary>
        /// Asks whether to go on to the next chapter (or the next book at the end of a book). Returns null if not
        /// (or if there isn't one)
        /// </summary>
        private IVerseRef QueryNextChapter(IVerseRef chapterReference)
        {
            var next = chapterReference.GetNextChapter(_project);
            if (next == null)
            {
                MessageBox.Show("That was the last chapter in the project.", SpellFixerPlugin.PluginName);
                return null;
            }

            var question = (next.BookNum == chapterReference.BookNum)
                            ? $"Finished {chapterReference.BookCode} {chapterReference.ChapterNum}. Continue checking with the next chapter ({next.BookCode} {next.ChapterNum})?"
                            : $"Finished {chapterReference.BookCode}. Continue checking with the next book ({next.BookCode})?";

            return (MessageBox.Show(this, question, SpellFixerPlugin.PluginName, MessageBoxButtons.YesNo) == DialogResult.Yes)
                    ? next
                    : null;
        }

        private string ConvertWord(string word)
        {
            try
            {
                return _spellFixerProject.Convert(word);
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: unable to convert '{word}': {ex.Message}");
                if (!_hasShownConvertError)
                {
                    _hasShownConvertError = true;
                    MessageBox.Show($"The Spell Fixer couldn't process '{word}' (it will be skipped):{Environment.NewLine}{ex.Message}", SpellFixerPlugin.PluginName);
                }
                return word;    // i.e. skip it
            }
        }

        private FormButtons AskUser(string word, string suggestion, out string correctedSpelling)
        {
            _queryForm ??= new SpellFixerQueryForm(_spellFixerProject,
                (bad, good) => SpellingStatusRecorder.Record(_project, _plugin, bad, good, message => MessageBox.Show(message, SpellFixerPlugin.PluginName)));
            _projectFont ??= GetProjectFont();

            var button = _queryForm.Show(this, _projectFont, _project.Language?.IsRtoL ?? false, word, suggestion);
            correctedSpelling = _queryForm.CorrectedSpelling;
            return button;
        }

        private Font GetProjectFont()
        {
            try
            {
                var font = _project.Language?.Font;
                if (font != null)
                    return new Font(font.FontFamily, Math.Max(12F, (float)font.Size));
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"SpellFixer: unable to use the project font: {ex.Message}");
            }
            return null;    // i.e. use the dialog's default
        }
    }
}
