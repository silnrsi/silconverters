using BackTranslationHelper;
using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public partial class BackTranslationHelperForm : Form, IBackTranslationHelperDataSource
    {
        private const string FrameTextFormat = "Back Translating from {0} - {1} in verse: {2}";

        private IProject _projectSource;
        private IProject _projectTarget;
        private IProjectLanguage _languageSource;
        private IProjectLanguage _languageTarget;
        private IKeyboard _keyboardTarget;
        private IPluginHost _host;
        private ParatextBackTranslationHelperPlugin _plugin;
        private Action<BackTranslationHelperModel> _updateControls;
        private BackTranslationHelperModel _model;
        private IWriteLock _writeLock = null;
        private Action<IVerseRef> _setSyncReferenceGroup;

        /// <summary>
        /// the current verse we're processing. (this generally is the 1st of a range if it is a combined verse).
        /// See _verseReferenceLast below.
        /// </summary>
        private IVerseRef _verseReference;

        /// <summary>
        /// the current verse(s) we're processing. This comes from the USFMTokens, so it could be, e.g. Acts 1:2-5
        /// </summary>
        private IVerseRef _versesReference;

        /// <summary>
        /// this is set to the last verse of a range (i.e. 5 if the USFM markers were from Acts 1:2-5), 
        /// so that when we say "GetNextVerse", it gives us the next verse (rather than 2->3 and getting 
        /// the same verses)
        /// </summary>
        private IVerseRef _verseReferenceLast;

        /// <summary>
        /// this contains the tokens from the source project, but just the verse(s) that we're processing (e.g. v1 or could be v2-5)
        /// Key is the [BookCode]_[ChapterNum]_[VerseNum] (e.g. ACT_1_1)
        /// </summary>
        private Dictionary<string, List<IUSFMToken>> _usfmTokensSource { get; set; } = new Dictionary<string, List<IUSFMToken>>();

        /// <summary>
        /// this contains the tokens from the target project, for all the verses in the current chapter (we need the whole chapter,
        /// because we have to Put the entire chapter when we go to write it
        /// </summary>
        private Dictionary<string, SortedDictionary<IVerseRef, List<IUSFMToken>>> _usfmTokensTarget { get; set; } = new Dictionary<string, SortedDictionary<IVerseRef, List<IUSFMToken>>>();

        public BackTranslationHelperForm(IPluginHost host, ParatextBackTranslationHelperPlugin plugin, Action<IVerseRef> setSyncReferenceGroup,
            IVerseRef initialVerseReference, IProject projectSource, IProject projectTarget, IProjectLanguage languageSource, IProjectLanguage languageTarget)
        {
            InitializeComponent();

            _host = host;
            _plugin = plugin;
            _versesReference = _verseReferenceLast = _verseReference = initialVerseReference;
            _setSyncReferenceGroup = setSyncReferenceGroup;
            _projectSource = projectSource;
            _projectTarget = projectTarget;
            _languageSource = languageSource;
            _languageTarget = languageTarget;
            _keyboardTarget = _projectTarget.VernacularKeyboard;

            // this form is the implementation of the way to get get data
            backTranslationHelperCtrl.BackTranslationHelperDataSource = this;

            _host.VerseRefChanged += host_VerseRefChanged;
            _projectSource.ScriptureDataChanged += ScriptureDataChangedHandlerSource;
            _projectTarget.ScriptureDataChanged += ScriptureDataChangedHandlerTarget;

            Text = GetFrameText(_projectSource, _projectTarget, _verseReference);
            Location = Properties.Settings.Default.WindowLocation;
            WindowState = Properties.Settings.Default.DefaultWindowState;
            if (MinimumSize.Height <= Properties.Settings.Default.WindowSize.Height &&
                MinimumSize.Width <= Properties.Settings.Default.WindowSize.Width)
            {
                Size = Properties.Settings.Default.WindowSize;
            }
        }

        private static string GetFrameText(IProject projectSource, IProject projectTarget, IVerseRef versesReference)
        {
            return String.Format(FrameTextFormat, projectSource, projectTarget, versesReference);
        }

        private void ScriptureDataChangedHandlerSource(IProject sender, int bookNum, int chapterNum)
        {
            Unlock();
        }

        private void ScriptureDataChangedHandlerTarget(IProject sender, int bookNum, int chapterNum)
        {
            Unlock();
        }

        private void host_VerseRefChanged(IPluginHost sender, IVerseRef newReference, SyncReferenceGroup group)
        {
            // since this will initialize the _verseReference, which is intended to be the first of a series of verses...
            var newRef = (newReference.RepresentsMultipleVerses) ? newReference.AllVerses.First() : newReference;
            GetNewReference(newRef);
        }

        private void GetNewReference(IVerseRef newReference)
        {
            Unlock();
            _verseReference = newReference;
            BackTranslationHelperModel model = null;    // means query the interface to get the data
            var cursor = Cursor;
            try
            {
                Cursor = Cursors.WaitCursor;
                backTranslationHelperCtrl.GetNewData(ref model);
                UpdateData(model);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                while (ex.InnerException != null)
                {
                    error += Environment.NewLine + Environment.NewLine + ex.InnerException.Message;
                    ex = ex.InnerException;
                }

                MessageBox.Show(error, ParatextBackTranslationHelperPlugin.PluginName);
            }
            finally
            {
                Cursor = cursor;
            }
        }

        private void UpdateData(BackTranslationHelperModel model)
        {
            Text = GetFrameText(_projectSource, _projectTarget, _versesReference);
            _model = model;
            backTranslationHelperCtrl.Initialize(true);
            _updateControls(_model);
        }

        Font IBackTranslationHelperDataSource.SourceLanguageFont
        {
            get
            {
                return new Font(_languageSource.Font.FontFamily, _languageSource.Font.Size);
            }
        }

        Font IBackTranslationHelperDataSource.TargetLanguageFont
        {
            get
            {
                return new Font(_languageTarget.Font.FontFamily, _languageTarget.Font.Size);
            }
        }

        void IBackTranslationHelperDataSource.ActivateKeyboard()
        {
            _keyboardTarget?.Activate();
        }

        BackTranslationHelperModel IBackTranslationHelperDataSource.Model
        {
            get
            {
                var model = new BackTranslationHelperModel
                {
                    SourceData = CurrentSourceData ?? "<source data empty>",
                    TargetData = CurrentTargetData,
                    TargetDataPreExisting = true,
                    TargetsPossible = new List<TargetPossible>()
                };
                return model;
            }
        }

        private string CurrentSourceData
        {
            get
            {
                var key = GetSourceUsfmTokenKey(_verseReference);
                if (!_usfmTokensSource.TryGetValue(key, out List<IUSFMToken> tokens))
                {
                    tokens = _projectSource.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum, _verseReference.VerseNum).ToList();
                    _usfmTokensSource.Add(key, tokens);
                }

                var data = tokens.OfType<IUSFMTextToken>()
                                 .Where(t => t.IsPublishableVernacular && IsMatchingVerse(t.VerseRef, _verseReference))
                                 .ToDictionary(ta => ta, ta => ta.VerseRef);

                var textValues = data.Select(t => t.Key.Text);
                var sourceString = string.Join(Environment.NewLine, textValues);

                // set the verse reference to the last of a combined set of verses (which we can only get from the USFM markers)
                _versesReference = data.Values.First();
                _verseReferenceLast = (_versesReference.RepresentsMultipleVerses)
                                        ? _versesReference.AllVerses.Last()
                                        : _verseReference;

                return sourceString;
            }
        }

        private string GetSourceUsfmTokenKey(IVerseRef verseReference)
        {
            // get the key to see if we already have this data (TODO: add a 'it was changed in Ptx', so we can remove it from this collection)
            return $"{verseReference.BookNum}_{verseReference.ChapterNum}_{verseReference.VerseNum}";
        }

        private string CurrentTargetData
        {
            get
            {
                _writeLock = _projectTarget.RequestWriteLock(_plugin, ReleaseRequested, _verseReference.BookNum, _verseReference.ChapterNum);
                if (_writeLock == null)
                {
                    // if this fails to return something, it means we can't edit it
                    MessageBox.Show($"You don't have edit privilege on this chapter: {_verseReference}");
                }
                var verseKey = GetTargetUsfmTokenKey(_verseReference);
                if (!_usfmTokensTarget.TryGetValue(verseKey, out SortedDictionary<IVerseRef, List<IUSFMToken>> vrefTokens))
                {
                    var chapterTokens = _projectTarget.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum).ToList();
                    var dict = chapterTokens.GroupBy(t => t.VerseRef, t => t, (key, g) => new { VerseRef = key, USFMTokens = g.ToList() })
                                            .ToDictionary(t => t.VerseRef, t => t.USFMTokens);
                    vrefTokens = new SortedDictionary<IVerseRef, List<IUSFMToken>>(dict);
                    _usfmTokensTarget.Add(verseKey, vrefTokens);
                }

                if (!vrefTokens.ContainsKey(_verseReference))
                    return null;

                var tokens = vrefTokens[_verseReference];
                var data = tokens.OfType<IUSFMTextToken>()
                                 .Where(t => t.IsPublishableVernacular && IsMatchingVerse(t.VerseRef, _verseReference));

                var values = data.Select(t => t.Text);
                var verseData = string.Join(Environment.NewLine, values);
                return verseData;
            }
        }

        private string GetTargetUsfmTokenKey(IVerseRef verseReference)
        {

            // get the key, which for the target data is the entire chapter (we have to Put as a whole chapter)
            return $"{verseReference.BookNum}_{verseReference.ChapterNum}";
        }

        private bool IsMatchingVerse(IVerseRef verseReferenceFromToken, IVerseRef verseReference)
        {
            return verseReferenceFromToken?.AllVerses?.Any(vr => vr.ToString() == _verseReference?.ToString()) ?? false;
        }

        private void ReleaseRequested(IWriteLock obj)
        {
            Unlock();
        }

        string IBackTranslationHelperDataSource.ProjectName
        {
            get
            {
                return Text;
            }
        }

        private ButtonPressed _buttonPressed;

        void IBackTranslationHelperDataSource.ButtonPressed(ButtonPressed button)
        {
            _buttonPressed = button;
        }

        void IBackTranslationHelperDataSource.Cancel()
        {
            Close();
        }

        void IBackTranslationHelperDataSource.SetDataUpdateProc(Action<BackTranslationHelperModel> updateControls)
        {
            _updateControls = updateControls;
        }

        void IBackTranslationHelperDataSource.Log(string message)
        {
            _host.Log(_plugin, message);
        }

        void IBackTranslationHelperDataSource.MoveToNext()
        {
            _buttonPressed = ButtonPressed.MoveToNext;

            _verseReference = _verseReferenceLast.GetNextVerse(_projectSource);

            // Set the sync group our window belongs to:
            _setSyncReferenceGroup(_verseReference);
        }

        void IBackTranslationHelperDataSource.WriteToTarget(string text)
        {
            _buttonPressed = ButtonPressed.WriteToTarget;

            // get the new values to write, which may be on separate lines (if they were from separate lines from the Get)
            // there are 3 possibilities:
            //  1) there are fewer lines of text now than there were in Paratext
            //      ... in which case, we ignore the extra text tokens from the Get to drop them
            //  2) there are the same number of lines of text as there were in Paratext
            //      ... in which case, we put one line of each translated version into one of the text tokens
            //  3) there are more lines of text translated than there were in Paratext
            //      ... in which case, we'll duplicate the last text token and push the extra lines in it
            var translatedValues = text?.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                                        .ToList();
            if (!translatedValues.Any())    // nothing to do
                return;

            // go thru all the ones we had and put the translated text into the text ones and transfer the non-text ones in order into the list to Put
            var key = GetTargetUsfmTokenKey(_verseReference);
            if (!_usfmTokensTarget.TryGetValue(key, out SortedDictionary<IVerseRef, List<IUSFMToken>> vrefTokensTarget))
            {
                RequeryWarning();
                return;
            }

            // get the USFM tokens for the corresponding verse in the target project (if we don't have at least 1 IUSFMTextToken...
            if (!vrefTokensTarget.TryGetValue(_versesReference, out List<IUSFMToken> tokensTarget) ||
                !tokensTarget.Any(t => t is IUSFMTextToken))
            {
                // ... it could be that the user didn't put in any verse number/paragraphs/text yet... so just make a copy of the source tokens
                //  which will be replaced w/ the translated text below
                key = GetSourceUsfmTokenKey(_verseReference);
                if (!_usfmTokensSource.TryGetValue(key, out tokensTarget))
                {
                    // if we didn't find those, then we'll have to start over
                    RequeryWarning();
                    return;
                }

                // it may have some verses, but they're are no IUSFMTextTokens among them...
                if (vrefTokensTarget.ContainsKey(_verseReference))
                    vrefTokensTarget[_verseReference] = tokensTarget;
                else
                    vrefTokensTarget.Add(_versesReference, tokensTarget);
            }

            // tokensTarget now contains just the tokens for the current verse
            var i = 0;
            var purgeConsecutiveParagraphs = false;
            TextToken latestTextToken = null;
            var updatedTokens = new List<IUSFMToken>();
            foreach (var token in tokensTarget)
            {
                IUSFMToken updatedToken = token;
                if ((token is IUSFMTextToken textToken) && textToken.IsPublishableVernacular && IsMatchingVerse(textToken.VerseRef, _verseReference))
                {
                    if (i >= translatedValues.Count)
                    {
                        // this means the source had multiple paragraphs, but the target doesn't, which means we'll have 1 too many paragraph markers
                        purgeConsecutiveParagraphs = true;
                        continue;
                    }

                    latestTextToken = new TextToken(textToken)
                    {
                        Text = translatedValues[i++]
                    };
                    updatedToken = latestTextToken;
                }
                updatedTokens.Add(updatedToken);
            }

            if (latestTextToken == null)
            {
                // this means the target (and source) verse had no IUSFMTextToken in it
                // so there's nothing to do?
                return;
            }

            // deal w/ the case where the user has either fewer or more paragraphs than the source
            if (purgeConsecutiveParagraphs)
            {
                var curr = updatedTokens.FirstOrDefault(t => IsParagraphToken(t));
                if (curr != null)
                {
                    for (var j = updatedTokens.IndexOf(curr) + 1; j < updatedTokens.Count; j++)
                    {
                        var next = updatedTokens[j];
                        if (IsParagraphToken(next))
                            updatedTokens.Remove(curr);
                        curr = next;
                    }
                }
            }
            else
            {
                // if there are still some translated portions we haven't processed yet, then insert them as copies of the last one we did
                //  just after the last one we did (w/ a paragraph token before it).
                while (translatedValues.Count > i)
                {
                    // if there are other markers *after* the last text token (e.g. a final paragraph token), then put the text (along
                    //  with a preceding paragraph token) just after the last text token we added above
                    var insertionIndex = updatedTokens.IndexOf(latestTextToken) + 1;
                    updatedTokens.Insert(insertionIndex++, ParagraphToken(latestTextToken));

                    latestTextToken = new TextToken(latestTextToken)
                    {
                        Text = translatedValues[i++]
                    };
                    updatedTokens.Insert(insertionIndex, latestTextToken);
                }
            }

            vrefTokensTarget[_versesReference] = updatedTokens;

            try
            {
                if (_writeLock == null) // it shouldn't be, but if it is, this should warn the user that it isn't going to work
                    _writeLock = _projectTarget.RequestWriteLock(_plugin, ReleaseRequested, _verseReference.BookNum, _verseReference.ChapterNum);

                var tokens = vrefTokensTarget.SelectMany(d => d.Value).ToList();
                _projectTarget.PutUSFMTokens(_writeLock, tokens, _verseReference.BookNum);
                Unlock();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception caught:\n{ex.Message}");
            }
        }

        public IUSFMToken ParagraphToken(TextToken previousTextToken)
        {
            // see if we can find a list that has one (check the source first, since target is likely to be lacking)
            var tokens = _usfmTokensSource.Values.FirstOrDefault(l => l.Any(t => IsParagraphToken(t))) ??
                         _usfmTokensTarget.Values.SelectMany(d => d.Values)
                                                 .FirstOrDefault(l => l.Any(t => IsParagraphToken(t)));

            var paragraphToken = (IUSFMMarkerToken)tokens.FirstOrDefault(t => IsParagraphToken(t)) ??
                                    new MarkerToken
                                    {
                                        Type = MarkerType.Paragraph,
                                        Marker = "p",
                                        IsPublishableVernacular = true,
                                        IsScripture = true,
                                        VerseOffset = previousTextToken.VerseOffset + previousTextToken.Text.Length,
                                        VerseRef = previousTextToken.VerseRef
                                    };

            return new MarkerToken(paragraphToken, previousTextToken.VerseOffset + previousTextToken.Text.Length, previousTextToken.VerseRef);
        }

        private static List<string> _paragraphMarkers = new List<string> { "p", "m" };

        private static bool IsParagraphToken(IUSFMToken token)
        {
            return (token is IUSFMMarkerToken markerToken) && (markerToken.Type == MarkerType.Paragraph) && _paragraphMarkers.Contains(markerToken.Marker);
        }

        private void RequeryWarning()
        {
            // if we don't already have it... it's probably because something was changed in the project
            // TODO: do something! (probably just need to Initialize and UpdateData, so we'll have requeried everything)
            var res = MessageBox.Show("It seems that something might have changed in the target project, which requires us to requery. Click 'Yes' to requery and start again. Click 'No' if you want to close this box (and copy the current text if you made changes for next time).",
                                      ParatextBackTranslationHelperPlugin.PluginName, MessageBoxButtons.YesNo);

            if (res == DialogResult.Yes)
                GetNewReference(_verseReference);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // On Windows XP, TXL comes up underneath Paratext. See if this fixes it:
            TopMost = true;
            TopMost = false;
            GetNewReference(_verseReference);
        }

        private void BackTranslationHelperForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // only allow Cancel or ReplaceEvery
            if ((e.CloseReason != CloseReason.UserClosing) &&
                ((_buttonPressed == ButtonPressed.WriteToTarget)
                || (_buttonPressed == ButtonPressed.MoveToNext)
                || (_buttonPressed == ButtonPressed.Copy)))
                e.Cancel = true;

            Unlock();

            Properties.Settings.Default.DefaultWindowState = WindowState;
            Properties.Settings.Default.WindowLocation = Location;
            Properties.Settings.Default.WindowSize = Size;
            Properties.Settings.Default.Save();

            _host.VerseRefChanged -= host_VerseRefChanged;
            Dispose();
        }

        private void Unlock()
        {
            if (_writeLock != null)
            {
                IWriteLock temp = _writeLock;
                _writeLock = null;  // to prevent it being called twice while freeing the lock
                temp.Dispose();
            }
        }
    }
}
