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
using System.Windows;
using System.Security.Policy;

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
        /// The number of text lines in the source data (i.e. the number of IUSFMTextTokens in the verse(s))
        /// </summary>
        private int SourceDataLineCount { get; set; }

        /// <summary>
        /// This contains the list of marker tokens immediately preceding the text tokens in the source data
        /// </summary>
        private List<IUSFMMarkerToken> TextTokenMarkersSource { get; set; } = new List<IUSFMMarkerToken>();

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
            backTranslationHelperCtrl.RegisterForNotification(BackTranslationHelperCtrl.SubscribeableEventKeyTargetBackTranslationTextChanged,
                                                              TargetBackTranslationTextChanged);

            _host.VerseRefChanged += Host_VerseRefChanged;
            _projectSource.ScriptureDataChanged += ScriptureDataChangedHandlerSource;
            _projectTarget.ScriptureDataChanged += ScriptureDataChangedHandlerTarget;
        }

        private void TargetBackTranslationTextChanged(string value)
        {
            var translatedCount = GetTranslatedLines(value).Count;
            System.Diagnostics.Debug.WriteLine($"PtxBTH: TargetBackTranslationTextChanged: SourceDataLineCount = '{SourceDataLineCount}', translatedCount = '{translatedCount}'");
            if (SourceDataLineCount != translatedCount)
                textBoxStatus.Text = $"There are currently {translatedCount} lines of text in the Target Translation box vs. {SourceDataLineCount} text lines in the source verse (one for each of these markers: {String.Join(",", TextTokenMarkersSource.Select(m => $"\\{m.Marker}"))})";
            else
                textBoxStatus.Text = String.Empty;

            Application.DoEvents(); // this says we need to do this for when it won't display the change: https://social.msdn.microsoft.com/Forums/vstudio/en-US/983d2e3b-9bcb-4c9c-9e85-59f8b2051b3e/program-updating-a-textbox-does-not-work?forum=csharpgeneral
        }

        private static string GetFrameText(IProject projectSource, IProject projectTarget, IVerseRef versesReference)
        {
            return String.Format(FrameTextFormat, GetProjectName(projectSource, projectTarget), versesReference);
        }

        private static string GetProjectName(IProject projectSource, IProject projectTarget)
        {
            return String.Format(ProjectNameFormat, projectSource, projectTarget);
        }

        private bool IsDirty(IVerseRef verseReference, int bookNum, int chapterNum)
        {
            return ((bookNum == 0) || (verseReference.BookNum == bookNum)) && ((chapterNum == 0) || (verseReference.ChapterNum == chapterNum));
        }

        private void ScriptureDataChangedHandlerSource(IProject sender, int bookNum, int chapterNum)
        {
            System.Diagnostics.Debug.WriteLine($"PtxBTH: ScriptureDataChangedHandlerSource: bookNum = '{bookNum}', chapterNum = ‘{chapterNum}‘, IsModified: {backTranslationHelperCtrl.IsModified}, _verseReference: {_verseReference}");

            // if the change was in the chapter we're processing, then clear what we think the data is, so we'll repull it later
            if (IsDirty(_verseReference, bookNum, chapterNum))
                UsfmTokensSource.Clear();
        }

        private void ScriptureDataChangedHandlerTarget(IProject sender, int bookNum, int chapterNum)
        {
            System.Diagnostics.Debug.WriteLine($"PtxBTH: ScriptureDataChangedHandlerTarget: bookNum = '{bookNum}', chapterNum = ‘{chapterNum}‘, IsModified: {backTranslationHelperCtrl.IsModified}, _verseReference: {_verseReference}, _isChangingTarget: {AreWeChangingTheTarget}");
            if (AreWeChangingTheTarget)  // if we're the ones who changed it, then ignore
                return;

            // if the change was in the chapter we're processing, then clear what we think the data is, so we'll repull it later
            if (IsDirty(_verseReference, bookNum, chapterNum))
                UsfmTokensTarget.Clear();

#if TriedAndFailed
            // UPDATE: Ptx by definition has the write lock right now, so we can't call GetNewReference here
            //  Go back to calling it in Activate
            // unless we were editing it, and then force it to stay the same
            if (backTranslationHelperCtrl.IsModified)
                return;

            GetNewReference(_verseReference);
#endif
        }

        private void Host_VerseRefChanged(IPluginHost sender, IVerseRef newReference, SyncReferenceGroup group)
        {
            var newRef = (newReference.RepresentsMultipleVerses) ? newReference.AllVerses.First() : newReference;
            System.Diagnostics.Debug.WriteLine($"PtxBTH: In Host_VerseRefChanged {newReference} (newRef: {newRef}) & {group}, _isNotInFocus: {_isNotInFocus}, IsModified: {backTranslationHelperCtrl.IsModified}");

            // since this will initialize the _verseReference, which is intended to be the first
            //  of a series of verses...
            // UPDATE: but not if the Form isn't in focus (so we don't thrash around converting stuff while
            //  the user may be editing stuff in Ptx)
            if (_isNotInFocus && backTranslationHelperCtrl.IsModified)
            {
                textBoxStatus.Text = $"Staying on {_verseReference} because the Target Translation box was modified. Click here to update to current verse in Paratext";
                textBoxStatus.Tag = newRef;
                Application.DoEvents(); // this says we need to do this for when it won't display the change: https://social.msdn.microsoft.com/Forums/vstudio/en-US/983d2e3b-9bcb-4c9c-9e85-59f8b2051b3e/program-updating-a-textbox-does-not-work?forum=csharpgeneral
                return;
            }
            else
                textBoxStatus.Tag = null;

            GetNewReference(newRef);
        }

        private void TextBoxStatus_Click(object sender, System.EventArgs e)
        {
            if (textBoxStatus.Tag != null)
            {
                backTranslationHelperCtrl.IsModified = false;
                var newReference = (IVerseRef)textBoxStatus.Tag;
                GetNewReference(newReference);
            }
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
                var keyBookChapterVerse = GetBookChapterVerseRangeKey(_verseReference);
                if (!UsfmTokensSource.TryGetValue(keyBookChapterVerse, out List<IUSFMToken> tokens))
                {
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: Loading UsfmTokensSource for {keyBookChapterVerse}");
                    tokens = _projectSource.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum, _verseReference.VerseNum).ToList();
                    UsfmTokensSource.Add(keyBookChapterVerse, tokens);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: Already have UsfmTokensSource for {keyBookChapterVerse}");
                }

                var data = tokens.OfType<IUSFMTextToken>()
                                 .Where(t => IsPublishableVernacular(t, tokens) && IsMatchingVerse(t.VerseRef, _verseReference))
                                 .ToDictionary(ta => ta, ta => ta.VerseRef);

                if (!data.Any())
                    return null;

                TextTokenMarkersSource = GetTextTokenMarkers(tokens, data.Keys.ToList());

                var textValues = data.Select(t => t.Key.Text);
                SourceDataLineCount = textValues.Count();

                var sourceString = string.Join(Environment.NewLine, textValues);

                // set the verse reference to the last of a combined set of verses (which we can only get from the USFM markers)
                _versesReference = data.Values.First();
                _verseReferenceLast = (_versesReference.RepresentsMultipleVerses)
                                        ? _versesReference.AllVerses.Last()
                                        : _verseReference;

                return sourceString;
            }
        }

        // normally, text tokens are publishable, but there are some that aren't (e.g. the text content of an \id marker).
        // And there's one case that seems like a bug to me, but which I've been told has worked that way forever and so
        // there's no changing it now... vis-a-vis:
        // the \va...\va* inline marker is defined differently depending on whether it comes immediately after a \v [num(s)] 
        // marker than if it comes elsewhere in a verse. The relevant difference is that when it comes immediately after a \v 
        // marker, it's value for IsPublishableVernacular (false) and IsMetadata (true) are opposite from the other case. 
        // So... if IsPubliableVernacular is false, at least check if this is that case, and return true, so we'll try to 
        // translate it as the others are (bkz we only send IsPub text segments for translation)
        private bool IsPublishableVernacular(IUSFMTextToken t, List<IUSFMToken> tokens)
        {
            return t.IsPublishableVernacular || 
                   (PreviousToken(t, tokens, out IUSFMMarkerToken mt) && (mt.Marker == "va") && mt.IsMetadata);
        }

        private bool PreviousToken(IUSFMTextToken t, List<IUSFMToken> tokens, out IUSFMMarkerToken previousToken)
        {
            var index = tokens.IndexOf(t) - 1;
            if ((index >= 0) && (index < tokens.Count) && (tokens[index] is IUSFMMarkerToken prevToken))
            {
                previousToken = prevToken;
                return true;
            }

            previousToken = null;
            return false;
        }

        private List<IUSFMMarkerToken> GetTextTokenMarkers(List<IUSFMToken> tokens, List<IUSFMTextToken> textTokens)
        {
            IUSFMMarkerToken lastMarkerToken = null;
            var textTokenMarkers = new List<IUSFMMarkerToken>();
            foreach (var token in tokens)
            {
                if (token is IUSFMMarkerToken)
                {
                    lastMarkerToken = token as IUSFMMarkerToken;
                }
                else if (textTokens.Contains(token) && (lastMarkerToken != null))
                {
                    textTokenMarkers.Add(lastMarkerToken);
                }
            }
            return textTokenMarkers;
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
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: Loading UsfmTokensTarget for {bookChapterKey}");
                    var chapterTokens = _projectTarget.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum).ToList();
                    var dict = chapterTokens.GroupBy(t => t.VerseRef, t => t, (key, g) => new { VerseRef = key, USFMTokens = g.ToList() })
                                            .ToDictionary(t => GetBookChapterVerseRangeKey(t.VerseRef), t => t.USFMTokens);
                    vrefTokens = new SortedDictionary<string, List<IUSFMToken>>(dict);
                    UsfmTokensTarget.Add(bookChapterKey, vrefTokens);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: Already have UsfmTokensTarget for {bookChapterKey}");
                }

                var bookChapterVerseKey = GetBookChapterVerseRangeKey(_verseReference);

                // issue: if Ptx is in "I'm just a single verse" mode (e.g. clicking up/down in the combo box at the top)
                //  then this will show a single verse even if the text is a multi-verse situation
                //  (e.g. 42_001_006, while the key in vrefTokens might be: 42_001_006-007). So figure out
                //  which one it *should* be so we can find a hit in vrefTokens
                bookChapterVerseKey = TriangulateBookChapterVerseKey(bookChapterVerseKey, vrefTokens);

                if (String.IsNullOrEmpty(bookChapterVerseKey) || !vrefTokens.ContainsKey(bookChapterVerseKey))
                    return null;

                var tokens = vrefTokens[bookChapterVerseKey];
                var data = tokens.OfType<IUSFMTextToken>()
                                 .Where(t => IsPublishableVernacular(t, tokens) && IsMatchingVerse(t.VerseRef, _verseReference));

                if (!data.Any())
                    return null;

                var values = data.Select(t => t.Text);
                var verseData = string.Join(Environment.NewLine, values);
                return verseData;
            }
        }

        private static string TriangulateBookChapterVerseKey(string bookChapterVerseKey, SortedDictionary<string, List<IUSFMToken>> vrefTokens)
        {
            if (vrefTokens.ContainsKey(bookChapterVerseKey))
                return bookChapterVerseKey;

            var vrefTokenKey = vrefTokens.FirstOrDefault(t => t.Value.Any(v => v.VerseRef.AllVerses.Any(sv => GetBookChapterVerseRangeKey(sv) == bookChapterVerseKey))).Key;
            return vrefTokenKey;
        }

        private static string GetBookChapterKey(IVerseRef verseReference)
        {
            // get the key, which for the target data is the entire chapter (we have to Put as a whole chapter)
            return $"{verseReference.BookNum:D2}_{verseReference.ChapterNum:D3}";
        }

        private static string GetBookChapterVerseRangeKey(IVerseRef verseReference)
        {
            // get the key to see if we already have this data (TODO: add a 'it was changed in Ptx', so we can remove it from this collection)
            var bookChapterFirstVerse = $"{verseReference.BookNum:D2}_{verseReference.ChapterNum:D3}_{verseReference.VerseNum:D3}";
            if (verseReference.RepresentsMultipleVerses)
                bookChapterFirstVerse += $"-{verseReference.AllVerses.Last().VerseNum:D3}";
            return bookChapterFirstVerse;
        }

        private static bool IsMatchingVerse(IVerseRef verseReferenceFromToken, IVerseRef verseReference)
        {
            return ((verseReferenceFromToken?.ToString() == verseReference?.ToString()) || 
                    (verseReferenceFromToken.AllVerses?.Any(vr => vr.ToString() == verseReference?.ToString()) ?? false) ||
                    (verseReference?.AllVerses?.Any(vr => vr.ToString() == verseReferenceFromToken.ToString()) ?? false));
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
            _host.Log(_plugin, $"PtxBTH: {message}");
        }

        void IBackTranslationHelperDataSource.MoveToNext()
        {
            _buttonPressed = ButtonPressed.MoveToNext;

            _verseReference = _verseReferenceLast.GetNextVerse(_projectSource);

            // Set the sync group our window belongs to:
            _setSyncReferenceGroup(_verseReference);
        }

        private bool AreWeChangingTheTarget { get; set; }

        bool IBackTranslationHelperDataSource.WriteToTarget(string text)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("PtxBTH: In WriteToTarget");

                _buttonPressed = ButtonPressed.WriteToTarget;

                var vrefTokensTarget = CalculateTargetTokens(_verseReference, _versesReference, text, UsfmTokensSource, UsfmTokensTarget);
                if (vrefTokensTarget == null)
                {
                    // if we don't already have it... it's probably because something was changed in the project
                    //  external to this form (we lose the data if we get activated).
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: RequeryWarning");

                    // Note: if the Target Translation textbox has modified data, the current implementation shouldn't
                    //  overwrite it (see UpdateData in BackTranslationHelperCtrl.cs, which shows that if it's
                    //  modified, it will get it from the edit box instead of the model... i.e. won't change it)
                    // So if we don't have it, just requery the data and return false (so we don't move on)
                    GetNewReference(_verseReference);

                    // by returning false, we prevent it from doing 'next' if that's the button the user clicked,
                    //  while resetting the IsModified status, so they can click 'Next' next time and have the edited
                    //  text written to Ptx
                    return false;
                }

                AreWeChangingTheTarget = true;
                _writeLock ??= _projectTarget.RequestWriteLock(_plugin, ReleaseRequested, _verseReference.BookNum, _verseReference.ChapterNum);

                var tokens = vrefTokensTarget.SelectMany(d => d.Value).ToList();

                _projectTarget.PutUSFMTokens(_writeLock, tokens, _verseReference.BookNum);
                Unlock();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception caught:\n{ex.Message}");
            }
            finally
            {
                AreWeChangingTheTarget = false;
            }
            return false;
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
            var translatedValues = GetTranslatedLines(text);
            if ((translatedValues == null) || !translatedValues.Any())    // nothing to do
                return null;

            // get the relevant target token collection (for the verse we're editing), so we can replace them.
            var keyBookChapter = GetBookChapterKey(verseReference);
            if (!usfmTokensTarget.TryGetValue(keyBookChapter, out SortedDictionary<string, List<IUSFMToken>> vrefTokensTarget))
                return null;

            // get the source project tokens (so we can use those in the target project)
            // if by chance, there are none, then it must be that we marked them 'dirty', so just return mull 
            //  to have them be requeried
            var keyBookChapterVerse = GetBookChapterVerseRangeKey(verseReference);
            if (!usfmTokensSource.TryGetValue(keyBookChapterVerse, out List<IUSFMToken> tokensSource))
                return null;

            // NB: for reasons that are not clear, I was trying to maintain the markers that were in the target project 
            //  and put the translated lines in those... but the entire point of this plugin is to make a back translation
            //  of the *source* project. So why wouldn't I just use the source project's tokens and put the translated 
            //  bits in them.
            //  There may be fewer translated lines in the 'Target Translation' box than there were IUSFMTextToken in
            //  the source -- esp. if it the user started with the data from the existing Target project. But we'll just
            //  have to warn the user thru the UI of that issue.
            //  Bottom line, fit the translated lines into the IUSFMTextToken tokens of the *source* project in order 
            //  (until we run out), and if there are more translated lines than IUSFMTextToken tokens, just add them as
            //  '\p's and let the user edit them in Paratext after we write them out to the target project
            // BUT one issue does arise: if the source has a multiple combined verses and the target doesn't (or vice-versa)
            //  then we'll need to purge the existing (target) verse(s) so we can add the new (source-based) ones.

            // get the reference to the USFM tokens for the corresponding verse in the target project. If we don't
            //  have any, it probably means that the user didn't have any data in the target project yet... But 
            //  it doesn't really matter, bkz we're just going to replace them with a copy of the source tokens
            //  anyway, which will be replaced w/ the translated text below
            var keyBookChapterVerses = GetBookChapterVerseRangeKey(versesReference);
            var tokensTarget = tokensSource;

            // before adding it back in, remove any data in the target collection for any and all verses in the source range
            var matchingTokensInTarget = vrefTokensTarget.Where(kvp => kvp.Value.Any(t => IsMatchingVerse(t.VerseRef, verseReference))).ToList();
            matchingTokensInTarget.ForEach(kvp => vrefTokensTarget.Remove(kvp.Key));

            if (vrefTokensTarget.ContainsKey(keyBookChapterVerses))
                vrefTokensTarget[keyBookChapterVerses] = tokensSource;
            else
                vrefTokensTarget.Add(keyBookChapterVerses, tokensSource);

            // go thru all the ones we had and put the translated text into the text ones and transfer the non-text ones in order into the list to Put
            var i = 0;
            TextToken latestTextToken = null;
            var updatedTokens = new List<IUSFMToken>();
            foreach (var token in tokensTarget)
            {
                IUSFMToken updatedToken = token;
                if ((token is IUSFMTextToken textToken) && textToken.IsPublishableVernacular && IsMatchingVerse((IVerseRef)textToken.VerseRef, verseReference))
                {
                    latestTextToken = new TextToken(textToken)
                    {
                        Text = (i >= translatedValues.Count)
                                    ? String.Empty              // empty the source text, bkz we ran out of translated lines
                                    : translatedValues[i++]
                    };
                    updatedToken = latestTextToken;
                }

                updatedTokens.Add(updatedToken);
            }

            // if there are still some translated portions we haven't processed yet, then insert them as copies of the last text 
            //  one we did just after the last one we did (w/ a paragraph token before it).
            var insertionIndex = updatedTokens.Count;
            while (translatedValues.Count > i)
            {
                // using the last text token we saw in the target as a template (which could possibly be from the source (read: untranslated)),
                //  create a new one w/ the next translated value
                latestTextToken = new TextToken(latestTextToken)
                {
                    Text = translatedValues[i++]
                };

                updatedTokens.Insert(insertionIndex++, latestTextToken);
            }

            vrefTokensTarget[keyBookChapterVerses] = updatedTokens;

#if SerializeToCreateTestFiles
            var strTokensTargetUpdate = ToJson(vrefTokensTarget);
#endif

            return vrefTokensTarget;
        }

        private static List<string> GetTranslatedLines(string text)
        {
            return text?.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                                        .ToList();
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

        private static readonly List<string> _paragraphMarkers = new() { "p", "m" };

        private static bool IsParagraphToken(IUSFMToken token)
        {
            return (token is IUSFMMarkerToken markerToken) && (markerToken.Type == MarkerType.Paragraph) && _paragraphMarkers.Contains(markerToken.Marker);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            GetNewReference(_verseReference);
        }

        private void BackTranslationHelperForm_Load(object sender, EventArgs e)
        {
            Text = GetFrameText(_projectSource, _projectTarget, _verseReference);
            Location = Properties.Settings.Default.WindowLocation;
            WindowState = Properties.Settings.Default.DefaultWindowState;
            if (MinimumSize.Height <= Properties.Settings.Default.WindowSize.Height &&
                MinimumSize.Width <= Properties.Settings.Default.WindowSize.Width)
            {
                Size = Properties.Settings.Default.WindowSize;
            }
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

            System.Diagnostics.Debug.WriteLine($"PtxBTH: BackTranslationHelperForm_Deactivate: _isNotInFocus = '{_isNotInFocus}', IsModified: {backTranslationHelperCtrl.IsModified}, _verseReference: {_verseReference}");

            // if nothing's been changed, then no need to show it as needing to be requeried
            // WRONG: if they edit something while deactivated, we won’t query it again if we don’t clear it
            //if (!backTranslationHelperCtrl.IsModified)
            //    return;
        }

        private void BackTranslationHelperForm_Activated(object sender, EventArgs e)
        {
            _isNotInFocus = false;
            System.Diagnostics.Debug.WriteLine($"PtxBTH: BackTranslationHelperForm_Activated: _isNotInFocus = '{_isNotInFocus}', IsModified: {backTranslationHelperCtrl.IsModified}, _verseReference: {_verseReference}");

#if false
            // THIS is now done in ScriptureDataChangedHandlerSource & ScriptureDataChangedHandlerTarget
            // make the source and target data stale, and trigger a requery...
            PurgeSourceData(_verseReference);

            var bookChapterKey = GetBookChapterKey(_verseReference);
            if (UsfmTokensTarget.ContainsKey(bookChapterKey))
            {
                UsfmTokensTarget.Remove(bookChapterKey);
            }
#endif

            // unless we were editing it, and then force it to stay the same
            if (backTranslationHelperCtrl.IsModified)
                return;

            GetNewReference(_verseReference);
        }
    }
}
