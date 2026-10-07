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
        }

        public string ProjectShortName => _project.ShortName;

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
            if (_isChecking)
            {
                e.Cancel = true;    // let the run finish (click Cancel in the query dialog to stop it)
                return;
            }

            _host.VerseRefChanged -= Host_VerseRefChanged;

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
            buttonChooseProject.Enabled = !_isChecking;
            buttonCheck.Enabled = haveProject && !_isChecking;
            buttonAssignCorrectSpelling.Enabled = haveProject && !_isChecking;
            buttonFindReplacementRule.Enabled = haveProject && !_isChecking;
            buttonEditSpellingFixes.Enabled = haveProject && !_isChecking;
        }

        private void ButtonChooseProject_Click(object sender, EventArgs e)
        {
            ChooseSpellFixerProject();
        }

        private void ButtonAssignCorrectSpelling_Click(object sender, EventArgs e)
        {
            DoWithClipboardWord(word => _spellFixerProject.AssignCorrectSpelling(word));
        }

        private void ButtonFindReplacementRule_Click(object sender, EventArgs e)
        {
            DoWithClipboardWord(word => _spellFixerProject.FindReplacementRule(word));
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
            _queryForm ??= new SpellFixerQueryForm(_spellFixerProject);
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
