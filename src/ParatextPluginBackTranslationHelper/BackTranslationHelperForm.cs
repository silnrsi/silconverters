// Uncomment this define to enable code that helps in test creation for a new scenario
//  See UnitTest_PtxBackTrHelper::It_Can_Determine_Correct_Update_To_Target_Project in the TestBwdc project for details
#if DEBUG
#define SerializeToCreateTestFiles
#endif

using BackTranslationHelper;
using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public partial class BackTranslationHelperForm : Form, IBackTranslationHelperDataSource
    {
        private const string FrameTextFormat = "Back Translating from {0} in verse: {1}";
        private const string ProjectNameFormat = "{0} - {1}";

        private readonly IProject _projectSource;
        private readonly IProject _projectTarget;
        private readonly IProjectLanguage _languageSource;
        private readonly IProjectLanguage _languageTarget;
        private readonly IKeyboard _keyboardTarget;
        private readonly IPluginHost _host;
        private readonly ParatextBackTranslationHelperPlugin _plugin;
        private Action<BackTranslationHelperModel> _updateControls;
        private BackTranslationHelperModel _model;
        private IWriteLock _writeLock = null;
        private readonly Action<IVerseRef> _setSyncReferenceGroup;
        private bool _isNotInFocus;

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
        /// this contains the tokens from the source project, but just the verse(s) that we're processing (e.g. v1 
        /// or could be v2-5)
        /// Key is the [BookNum]_[ChapterNum]_[VerseNum] (e.g. ACT_001_001)
        /// </summary>
        private Dictionary<string, List<IUSFMToken>> UsfmTokensSource { get; set; } = new Dictionary<string, List<IUSFMToken>>();

        /// <summary>
        /// this contains the tokens from the target project, for all the verses in the current chapter (we need the 
        /// whole chapter,because we have to Put the entire chapter when we go to write it.
        /// Key is the [BookNum]_[ChapterNum] (e.g. 44_001)
        /// Key2 (of the SortedDictionary) is [BookNum]_[ChapterNum]_[VerseNum] (e.g. ACT_001_001
        /// Note: beware that this could be out of date if the user edits the verse (or another) in Ptx after 
        /// we pulled these values. Cause it to requery if we lose focus.
        /// </summary>
        private Dictionary<string, SortedDictionary<string, List<IUSFMToken>>> UsfmTokensTarget { get; set; } = new Dictionary<string, SortedDictionary<string, List<IUSFMToken>>>();

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

            _host.VerseRefChanged += Host_VerseRefChanged;
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
            return String.Format(FrameTextFormat, GetProjectName(projectSource, projectTarget), versesReference);
        }

        private static string GetProjectName(IProject projectSource, IProject projectTarget)
        {
            return String.Format(ProjectNameFormat, projectSource, projectTarget);
        }

        private void ScriptureDataChangedHandlerSource(IProject sender, int bookNum, int chapterNum)
        {
            Unlock();
        }

        private void ScriptureDataChangedHandlerTarget(IProject sender, int bookNum, int chapterNum)
        {
            Unlock();
        }

        private void Host_VerseRefChanged(IPluginHost sender, IVerseRef newReference, SyncReferenceGroup group)
        {
            // since this will initialize the _verseReference, which is intended to be the first
            //  of a series of verses...
            // UPDATE: but not if the Form isn't in focus (so we don't thrash around converting stuff while
            //  the user may be editing stuff in Ptx)
            if (_isNotInFocus && backTranslationHelperCtrl.IsModified)
            {
                return;
            }

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
                var currentTargetData = CurrentTargetData;
                var model = new BackTranslationHelperModel
                {
                    SourceData = CurrentSourceData ?? "<source data empty>",
                    TargetData = currentTargetData,
                    TargetDataPreExisting = currentTargetData,
                    TargetsPossible = new List<TargetPossible>()
                };
                return model;
            }
        }

        private string CurrentSourceData
        {
            get
            {
                var keyBookChapterVerse = GetBookChapterVerseKey(_verseReference);
                if (!UsfmTokensSource.TryGetValue(keyBookChapterVerse, out List<IUSFMToken> tokens))
                {
                    tokens = _projectSource.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum, _verseReference.VerseNum).ToList();
                    UsfmTokensSource.Add(keyBookChapterVerse, tokens);
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

                var bookChapterKey = GetBookChapterKey(_verseReference);
                if (!UsfmTokensTarget.TryGetValue(bookChapterKey, out SortedDictionary<string, List<IUSFMToken>> vrefTokens))
                {
                    var chapterTokens = _projectTarget.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum).ToList();
                    var dict = chapterTokens.GroupBy(t => t.VerseRef, t => t, (key, g) => new { VerseRef = key, USFMTokens = g.ToList() })
                                            .ToDictionary(t => GetBookChapterVerseKey(t.VerseRef), t => t.USFMTokens);
                    vrefTokens = new SortedDictionary<string, List<IUSFMToken>>(dict);
                    UsfmTokensTarget.Add(bookChapterKey, vrefTokens);
                }

                var bookChapterVerseKey = GetBookChapterVerseKey(_verseReference);

                // issue: if Ptx is in "I'm just a single verse" mode (e.g. clicking up/down in the combo box at the top)
                //  then this will show a single verse even if the text is a multi-verse situation
                //  (e.g. 42_001_006, while the key in vrefTokens might be: 42_001_006-007). So figure out
                //  which one it *should* be so we can find a hit in vrefTokens
                bookChapterVerseKey = TriangulateBookChapterVerseKey(bookChapterVerseKey, vrefTokens);

                if (!vrefTokens.ContainsKey(bookChapterVerseKey))
                    return null;

                var tokens = vrefTokens[bookChapterVerseKey];
                var data = tokens.OfType<IUSFMTextToken>()
                                 .Where(t => t.IsPublishableVernacular && IsMatchingVerse(t.VerseRef, _verseReference));

                var values = data.Select(t => t.Text);
                var verseData = string.Join(Environment.NewLine, values);
                return verseData;
            }
        }

        private static string TriangulateBookChapterVerseKey(string bookChapterVerseKey, SortedDictionary<string, List<IUSFMToken>> vrefTokens)
        {
            if (vrefTokens.ContainsKey(bookChapterVerseKey))
                return bookChapterVerseKey;

            var vrefTokenKey = vrefTokens.FirstOrDefault(t => t.Value.Any(v => v.VerseRef.AllVerses.Any(sv => GetBookChapterVerseKey(sv) == bookChapterVerseKey))).Key;
            return vrefTokenKey;
        }

        private static string GetBookChapterKey(IVerseRef verseReference)
        {
            // get the key, which for the target data is the entire chapter (we have to Put as a whole chapter)
            return $"{verseReference.BookNum:D2}_{verseReference.ChapterNum:D3}";
        }

        private static string GetBookChapterVerseKey(IVerseRef verseReference)
        {
            // get the key to see if we already have this data (TODO: add a 'it was changed in Ptx', so we can remove it from this collection)
            var bookChapterFirstVerse = $"{verseReference.BookNum:D2}_{verseReference.ChapterNum:D3}_{verseReference.VerseNum:D3}";
            if (verseReference.RepresentsMultipleVerses)
                bookChapterFirstVerse += $"-{verseReference.AllVerses.Last().VerseNum:D3}";
            return bookChapterFirstVerse;
        }

        private static bool IsMatchingVerse(IVerseRef verseReferenceFromToken, IVerseRef verseReference)
        {
            return ((verseReferenceFromToken.ToString() == verseReference.ToString()) || 
                    (verseReferenceFromToken?.AllVerses?.Any(vr => vr.ToString() == verseReference?.ToString()) ?? false));
        }

        private void ReleaseRequested(IWriteLock obj)
        {
            Unlock();
        }

        string IBackTranslationHelperDataSource.ProjectName
        {
            get
            {
                return GetProjectName(_projectSource, _projectTarget);
            }
        }

        bool IBackTranslationHelperDataSource.SourceLanguageRightToLeft
        {
            get
            {
                return _projectSource.Language.IsRtoL;
            }
        }

        bool IBackTranslationHelperDataSource.TargetLanguageRightToLeft
        {
            get
            {
                return _projectTarget.Language.IsRtoL;
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
            try
            {
                _buttonPressed = ButtonPressed.WriteToTarget;

                var vrefTokensTarget = CalculateTargetTokens(_verseReference, _versesReference, text, UsfmTokensSource, UsfmTokensTarget);
                if (vrefTokensTarget == null)
                {
                    RequeryWarning(_verseReference);
                    return; // nothing to do
                }

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

        /// <summary>
        /// Calculates what set of tokens should be pushed back to Paratext for a whole chapter after translating a single (or combined) verse.
        /// </summary>
        /// <param name="verseReference">the IVerseRef of the single verse indicated by the control at the top of Ptx (e.g. Acts 1:2) even if your cursor is in a "verse" made up of multiple verses (e.g. Acts 1:2-5)</param>
        /// <param name="versesReference">the IVerseRef of the (possibly multiple combined) verses in a project window (e.g. Acts 1:2-5). The 'AllVerses' member will contain an IVerseRef for each of the combined verses</param>
        /// <param name="text">the line(s) of translation (possibly multiple, separated by '\r\n') to be inserted into the 'versesReference' of the target project</param>
        /// <param name="usfmTokensSource">the IUSFMTokens currently in the 'versesReference' of the source project</param>
        /// <param name="usfmTokensTarget">the IUSFMTokens currently in the entire chapter of the target project</param>
        /// <returns></returns>
        public static SortedDictionary<string, List<IUSFMToken>> CalculateTargetTokens(IVerseRef verseReference, IVerseRef versesReference, string text,
            Dictionary<string, List<IUSFMToken>> usfmTokensSource, Dictionary<string, SortedDictionary<string, List<IUSFMToken>>> usfmTokensTarget)
        {
#if SerializeToCreateTestFiles
            // See UnitTest_PtxBackTrHelper::It_Can_Determine_Correct_Update_To_Target_Project in the TestBwdc project for details
            //  this is a way to get data to use for testing this function. By converting the objects that come into this function 
            //  (while running connected to Paratext; not the tests), you copy those to the "EmbeddedResource" files to create a
            //  test of a scenario that should work. If someone complains that the inserted translation didn't have the right format
            //  or markers, clone their project and process the same verse to see what might be the problem w/ this function, and
            //  capture the data to create a new test that should continue to work once a change is made (so we can make sure we 
            //  haven't broken anything). P.S. there's one more of these statements for the output at the end of this function
            //  each test takes 5 files:
            var strTokensSource = ToJson(usfmTokensSource);     // put into, e.g. SingleVerse_TokensSource.json
            var strTokensTarget = ToJson(usfmTokensTarget);     // put into, e.g. SingleVerseMissingInTarget_TokensTarget.json
            var strVerseReference = ToJson(verseReference);     // put into, e.g. SingleVerse_VerseReference.json
            var strVersesReference = ToJson(versesReference);   // put into, e.g. SingleVerse_VersesReference.json
#endif
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
            if ((translatedValues == null) || !translatedValues.Any())    // nothing to do
                return null;

            // go thru all the ones we had and put the translated text into the text ones and transfer the non-text ones in order into the list to Put
            var keyBookChapter = GetBookChapterKey(verseReference);
            if (!usfmTokensTarget.TryGetValue(keyBookChapter, out SortedDictionary<string, List<IUSFMToken>> vrefTokensTarget))
            {
                return null;
            }

            // get the source project tokens (in case we need to copy from them)
            var keyBookChapterVerse = GetBookChapterVerseKey(verseReference);
            var sourceTokensFound = usfmTokensSource.TryGetValue(keyBookChapterVerse, out List<IUSFMToken> tokensSource);

            var keyBookChapterVerses = GetBookChapterVerseKey(versesReference);
            // get the USFM tokens for the corresponding verse in the target project (if we don't have at least 1 IUSFMTextToken...
            if (!vrefTokensTarget.TryGetValue(keyBookChapterVerses, out List<IUSFMToken> tokensTarget) ||
                !tokensTarget.Any(t => t is IUSFMTextToken))
            {
                // ... it could be that the user didn't put in any verse number/paragraphs/text yet... so just make a copy of the source tokens
                //  which will be replaced w/ the translated text below
                if (!sourceTokensFound)
                {
                    // if we didn't find those, then we'll have to start over
                    return null;
                }
                else
                    tokensTarget = tokensSource;

                // it may have some verses, but no IUSFMTextTokens among them...
                if (vrefTokensTarget.ContainsKey(keyBookChapterVerse))
                    vrefTokensTarget[keyBookChapterVerse] = tokensTarget;
                else
                    vrefTokensTarget.Add(keyBookChapterVerses, tokensTarget);
            }

            // tokensTarget now contains just the tokens for the current verse
            var i = 0;
            var purgedSomeParagraphs = false;
            var lastTokenWasText = false;
            TextToken latestTextToken = null;
            var updatedTokens = new List<IUSFMToken>();
            var countFewerTextTokensInTranslation = (tokensTarget.Count(t => t is IUSFMTextToken) - translatedValues.Count);
            foreach (var token in tokensTarget)
            {
                IUSFMToken updatedToken = token;
                if ((token is IUSFMTextToken textToken) && textToken.IsPublishableVernacular && IsMatchingVerse((IVerseRef)textToken.VerseRef, verseReference))
                {
                    if ((i >= translatedValues.Count) || (purgedSomeParagraphs && lastTokenWasText))
                    {
                        // this means the source had multiple paragraphs, so ignore this unnecessary (source) text token
                        purgedSomeParagraphs = true;
                        continue;
                    }

                    latestTextToken = new TextToken(textToken)
                    {
                        Text = translatedValues[i++]
                    };
                    updatedToken = latestTextToken;
                }

                // if we have fewer lines than the source had, then skip adding any more paragraph markers unless it's the final token
                if ((countFewerTextTokensInTranslation > 0) && IsParagraphToken(token) && (!IsParagraphToken(tokensTarget.Last()) || (token != tokensTarget.Last())))
                {
                    // this means we want to skip this paragraph token
                    purgedSomeParagraphs = true;
                    countFewerTextTokensInTranslation--;
                    continue;
                }

                lastTokenWasText = IsScriptureTextToken(updatedToken);
                updatedTokens.Add(updatedToken);
            }

            if (latestTextToken == null)
            {
                // this means the target (and source) verse had no IUSFMTextToken in it
                // so there's nothing to do?
                return null;
            }

            // if there are still some translated portions we haven't processed yet, then insert them as copies of the last one we did
            //  just after the last one we did (w/ a paragraph token before it).
            // if there are other markers *after* the last text token (e.g. a final paragraph token), then put the text (along
            //  with a preceding paragraph token) just after the last text token we added above
            var insertionIndex = updatedTokens.Count;
            var lastTokenIsParagraph = IsParagraphToken(updatedTokens.Last());
            if (lastTokenIsParagraph)
                insertionIndex--;

            while (translatedValues.Count > i)
            {
                // first skip past any markers common to both (but not if the final one was a \p
                if (!lastTokenIsParagraph)
                    SkipPastIdenticalTokens(tokensSource, updatedTokens, ref insertionIndex);

                // if the source has one or more (non-scripture) markers at the next insertion point, we want to add them here.
                //  But the 'NextTokenFromSource*' function below will return a newly created paragraph (e.g. to put before 
                //  a translated lines) if we run out of stuff in the source. In that case, only do this loop once
                var isInsertedParagraph = false;
                do
                {
                    // first see if the next token in the source is a marker
                    isInsertedParagraph = NextTokenFromSourceOrTemplateParagraphMarker(out IUSFMToken marker);

                    // if it's a text token, then skip adding it here and add it as a text token below
                    if (IsScriptureTextToken(marker))
                        break;

                    // add the non-text (read: marker) token first (and repeat of there are others)
                    updatedTokens.Insert(insertionIndex++, marker);
                } while (!isInsertedParagraph);
                    
                // using the last text token we saw in the target as a template (which could possibly be from the source (read: untranslated)),
                //  create a new one w/ the next translated value
                latestTextToken = new TextToken(latestTextToken)
                {
                    Text = translatedValues[i++]
                };

                updatedTokens.Insert(insertionIndex++, latestTextToken);
            }

            // in case the last one in the target happens to be common...
            SkipPastIdenticalTokens(tokensSource, updatedTokens, ref insertionIndex);

            // add any remaining ones that are in the source, but not in the target
            while (!purgedSomeParagraphs && (insertionIndex < tokensSource.Count))
            {
                updatedTokens.Add(tokensSource[insertionIndex++]);
            }

            vrefTokensTarget[keyBookChapterVerses] = updatedTokens;

#if SerializeToCreateTestFiles
            var strTokensTargetUpdate = ToJson(vrefTokensTarget);
#endif

            return vrefTokensTarget;

            // local function (to reduce the number of parameters needing to be passed)
            bool NextTokenFromSourceOrTemplateParagraphMarker(out IUSFMToken marker)
            {
                var isFromSource = (tokensSource.Count > insertionIndex);
                marker = isFromSource 
                            ? tokensSource[insertionIndex]
                            : ParagraphToken(usfmTokensSource, usfmTokensTarget, latestTextToken);  // fall back to a paragraph marker
                return !isFromSource || IsParagraphToken(marker);   // negative means isInsertedParagraph (or if the next one in the source was a paragraph)
            }
        }

#if SerializeToCreateTestFiles
        private static string ToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj, 
                                               Formatting.Indented, 
                                               new JsonSerializerSettings 
                                               { 
                                                   ReferenceLoopHandling = ReferenceLoopHandling.Ignore 
                                               });
        }
#endif

        private static bool IsScriptureTextToken(IUSFMToken token)
        {
            return (token is IUSFMTextToken textToken) && textToken.IsPublishableVernacular;
        }

        private static void SkipPastIdenticalTokens(List<IUSFMToken> tokensSource, List<IUSFMToken> updatedTokens, ref int insertionIndex)
        {
            while ((insertionIndex < updatedTokens.Count) && (insertionIndex < tokensSource.Count) &&
                   (updatedTokens[insertionIndex].ToString() == tokensSource[insertionIndex].ToString()))
            {
                insertionIndex++;
            }
        }

        public static IUSFMToken ParagraphToken(Dictionary<string, List<IUSFMToken>> usfmTokensSource, 
            Dictionary<string, SortedDictionary<string, List<IUSFMToken>>> usfmTokensTarget, TextToken previousTextToken)
        {
            // see if we can find a list that has one (check the source first, since target is likely to be lacking)
            var tokens = usfmTokensSource.Values.FirstOrDefault(l => l.Any(t => IsParagraphToken(t))) ??
                         usfmTokensTarget.Values.SelectMany(d => d.Values)
                                                 .FirstOrDefault(l => l.Any(t => IsParagraphToken(t)));

            var paragraphToken = (IUSFMMarkerToken)tokens?.FirstOrDefault(t => IsParagraphToken(t)) ??
                                    new MarkerToken(previousTextToken.VerseRef, true, true, previousTextToken.VerseOffset + previousTextToken.Text.Length)
                                    {
                                        Type = MarkerType.Paragraph,
                                        Marker = "p"
                                    };

            return new MarkerToken(paragraphToken, previousTextToken.VerseOffset + previousTextToken.Text.Length, previousTextToken.VerseRef);
        }

        private static readonly List<string> _paragraphMarkers = new List<string> { "p", "m" };

        private static bool IsParagraphToken(IUSFMToken token)
        {
            return (token is IUSFMMarkerToken markerToken) && (markerToken.Type == MarkerType.Paragraph) && _paragraphMarkers.Contains(markerToken.Marker);
        }

        private void RequeryWarning(IVerseRef verseReference)
        {
            // if we don't already have it... it's probably because something was changed in the project
            // TODO: do something! (probably just need to Initialize and UpdateData, so we'll have requeried everything)
            var res = MessageBox.Show("It seems that something might have changed in the target project, which requires us to requery. Click 'Yes' to requery and start again. Click 'No' if you want to close this box (and copy the current text if you made changes for next time).",
                                      ParatextBackTranslationHelperPlugin.PluginName, MessageBoxButtons.YesNo);

            if (res == DialogResult.Yes)
                GetNewReference(verseReference);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (Properties.Settings.Default.PinnedToTop)
            {
                buttonPinToTop.Image = global::SIL.ParatextBackTranslationHelperPlugin.Properties.Resources.pindown;
                this.TopMost = true;
            }

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

            _host.VerseRefChanged -= Host_VerseRefChanged;
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

        private void BackTranslationHelperForm_Deactivate(object sender, EventArgs e)
        {
            // if we lose focus, it's possible that the user switched to Ptx to make some edits,
            //  so purge the source and target tokens which would force us to requery them
            _isNotInFocus = true;

            System.Diagnostics.Debug.WriteLine($"BackTranslationHelperForm_Deactivate: _isNotInFocus = '{_isNotInFocus}'");

            /*
             * New plan: don't move from the current verse if it's modified. but this is now handled in GetNewReference
            var keyBookChapterVerse = GetBookChapterVerseKey(_verseReference);
            if (UsfmTokensSource.ContainsKey(keyBookChapterVerse))
            {
                UsfmTokensSource.Remove(keyBookChapterVerse);
            }

            var bookChapterKey = GetBookChapterKey(_verseReference);
            if (UsfmTokensTarget.ContainsKey(bookChapterKey))
            {
                UsfmTokensTarget.Remove(bookChapterKey);
            }
            */
        }

        private void BackTranslationHelperForm_Activated(object sender, EventArgs e)
        {
            _isNotInFocus = false;
            System.Diagnostics.Debug.WriteLine($"BackTranslationHelperForm_Activated: _isNotInFocus = '{_isNotInFocus}'");
        }

        private void ButtonPinToTop_Click(object sender, EventArgs e)
        {
            var newCheckState = !Properties.Settings.Default.PinnedToTop;
            Properties.Settings.Default.PinnedToTop = newCheckState;
            Properties.Settings.Default.Save();
            this.TopMost = newCheckState;
            buttonPinToTop.Image = (newCheckState)
                                    ? global::SIL.ParatextBackTranslationHelperPlugin.Properties.Resources.pindown
                                    : global::SIL.ParatextBackTranslationHelperPlugin.Properties.Resources.pinup;
        }
    }
}
