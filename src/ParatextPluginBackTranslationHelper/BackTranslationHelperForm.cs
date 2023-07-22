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
using System.Collections.Specialized;
using System.Threading.Tasks;
using ECInterfaces;
using System.Threading;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public partial class BackTranslationHelperForm : Form, IBackTranslationHelperDataSource
    {
        private const string FrameTextFormat = "Back Translating from {0} in verse: {1}";
        private const string ProjectNameFormat = "{0} - {1}";

        private IProject _projectSource;
        private IProject _projectTarget;
        private IProjectLanguage _languageSource;
        private IProjectLanguage _languageTarget;
        private IKeyboard _keyboardTarget;
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
        private TextTokenMarkers TextTokenMarkersSource { get; set; }

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
            IVerseRef initialVerseReference)
        {
            InitializeComponent();

            _host = host;
            _plugin = plugin;
            _versesReference = _verseReferenceLast = _verseReference = initialVerseReference;
            _setSyncReferenceGroup = setSyncReferenceGroup;

            InitializeProjects(_host);

            // this form is the implementation of the way to get get data
            backTranslationHelperCtrl.BackTranslationHelperDataSource = this;
            backTranslationHelperCtrl.RegisterForNotification(BackTranslationHelperCtrl.SubscribeableEventKeyTargetBackTranslationTextChanged,
                                                              TargetBackTranslationTextChanged);

            AddToSettingsMenu(backTranslationHelperCtrl);
            _host.VerseRefChanged += Host_VerseRefChanged;
        }

        private System.Windows.Forms.ToolStripMenuItem translateNothingButPublishableScriptureTextMenuItem;

        private void AddToSettingsMenu(BackTranslationHelperCtrl backTranslationHelperCtrl)
        {
            // add a menu to allow the user to choose a new source project
            var chooseSourceProjectMenuItem = new System.Windows.Forms.ToolStripMenuItem
            {
                Name = "chooseSourceProjectMenuItem",
                Size = new System.Drawing.Size(247, 22),
                Text = "&Select New Source Project",
                ToolTipText = "Click to bring up a dialog to select a different Paratext project to be the source text for the Translation(s).",
            };
            chooseSourceProjectMenuItem.Click += new System.EventHandler(this.ChangeSourceProject_Click);
            backTranslationHelperCtrl.AddToSettingsMenu(chooseSourceProjectMenuItem);

            translateNothingButPublishableScriptureTextMenuItem = new System.Windows.Forms.ToolStripMenuItem
            {
                CheckOnClick = true,
                Name = "translateNothingButPublishableScriptureTextMenuItem",
                Size = new System.Drawing.Size(247, 22),
                Text = "&Translate only verse text",
                ToolTipText = "Check this option to have the source text translated without interruption by inline footnotes or \\va or \\vp verse numbering (should generate a better translation)",
            };
            translateNothingButPublishableScriptureTextMenuItem.Checked = Properties.Settings.Default.TranslateOnlyText;
            translateNothingButPublishableScriptureTextMenuItem.CheckStateChanged += new System.EventHandler(this.TranslateNothingButPublishableScriptureTextMenuItem_CheckStateChanged);
            backTranslationHelperCtrl.AddToSettingsMenu(translateNothingButPublishableScriptureTextMenuItem);
        }

        private void TranslateNothingButPublishableScriptureTextMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            var newCheckState = translateNothingButPublishableScriptureTextMenuItem.Checked;
            if (newCheckState != Properties.Settings.Default.TranslateOnlyText)
            {
                Properties.Settings.Default.TranslateOnlyText = newCheckState;
                Properties.Settings.Default.Save();
            }

            UsfmTokensSource.Clear();   // so we requery from the new source project
            GetNewReference(_verseReference);
        }

        private void ChangeSourceProject_Click(object sender, EventArgs e)
        {
            var mapProjectNameToSourceProjectOverride = BackTranslationHelperCtrl.SettingToDictionary(Properties.Settings.Default.MapProjectNameToSourceProjectOverride);

            var projectName = _projectTarget.ShortName;
            if (mapProjectNameToSourceProjectOverride.ContainsKey(projectName))
            {
                // remove any previous ones in case the user just cancels the QueryForProject to remove the override
                mapProjectNameToSourceProjectOverride.Remove(projectName);
            }

            var projectSource = QueryForProject("Source");
            if ((projectSource != null) && (projectSource != _projectSource))
            {
                _projectSource = projectSource;
                var lstSourceProjects = new List<string> { _projectSource.ShortName };
                mapProjectNameToSourceProjectOverride[projectName] = lstSourceProjects;

                InitializeSourceProjectCorrelates(_projectSource);
                UsfmTokensSource.Clear();   // so we requery from the new source project
                GetNewReference(_verseReference);
            }

            Properties.Settings.Default.MapProjectNameToSourceProjectOverride = BackTranslationHelperCtrl.SettingFromDictionary(mapProjectNameToSourceProjectOverride);
            Properties.Settings.Default.Save();
        }

        private void InitializeProjects(IPluginHost host)
        {
            var projects = host.GetAllProjects();
            var selectedProject = host.ActiveWindowState?.Project;

            var projectName = selectedProject.ShortName;
            
            if (Properties.Settings.Default.MapProjectNameToSourceProjectOverride == null)
                Properties.Settings.Default.MapProjectNameToSourceProjectOverride = new StringCollection();
            var mapProjectNameToSourceProjectOverride = BackTranslationHelperCtrl.SettingToDictionary(Properties.Settings.Default.MapProjectNameToSourceProjectOverride);

            if (mapProjectNameToSourceProjectOverride.TryGetValue(projectName, out List<string> lstSourceProjects))
            {
                _projectSource = projects.FirstOrDefault(p => p.ShortName == lstSourceProjects[0]);
                _projectTarget = projects.FirstOrDefault(p => p.ShortName == selectedProject.ShortName);
            }

            // if the user selects the daughter/target project, let's assume that's the intended target from it's base project
            else if ((selectedProject != null) && (selectedProject.BaseProject != null))
            {
                _projectSource = projects.FirstOrDefault(p => p.ShortName == selectedProject.BaseProject.ShortName);
                _projectTarget = projects.FirstOrDefault(p => p.ShortName == selectedProject.ShortName);
            }
            else
            {
                // otherwise, make them choose
                _projectSource ??= QueryForProject("Source");
                _projectTarget ??= QueryForProject("Target");
            }

            if ((_projectSource == null) || (_projectTarget == null))
                throw new ApplicationException($"Source ('{_projectSource}') or Target ('{_projectTarget}') project not selected. Can't continue!");

            InitializeSourceProjectCorrelates(_projectSource);

            _languageTarget = _projectTarget.Language;
            _keyboardTarget = _projectTarget.VernacularKeyboard;
            _projectTarget.ScriptureDataChanged += ScriptureDataChangedHandlerTarget;
        }

        private void InitializeSourceProjectCorrelates(IProject projectSource)
        {
            _languageSource = projectSource.Language;
            projectSource.ScriptureDataChanged += ScriptureDataChangedHandlerSource;
        }

        private IProject QueryForProject(string projectType)
        {
            var projects = _host.GetAllProjects();
            var dlg = new ProjectListForm(projects, projectType);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                return projects.FirstOrDefault(p => p.ShortName == dlg.SelectedDisplayName);
            }

            return null;
        }

        private CancellationTokenSource cancellationTokenSource;
        private Task<BackgroundWorkerResult> backgroundTask = null;

        /// <summary>
        /// This method is called by the BackTranslationHelperCtrl, since we registered for any changes
        /// to the translated text. We use it to verify that the number of paragraphs of text in the target 
        /// translation text matches how many are needed for the source text markers that will be used for them.
        /// I've created this processing as an asynchronous task (since it can take some time) in the hopes of
        /// getting rid of the occasional error whereby the status text box and/or the tooltip stops updating.
        /// If we get multiple calls w/in a second, then the earlier executions will end up being canceled
        /// before completion, and the text box and tooltip will only be updated for the final one of a series.
        /// </summary>
        /// <param name="value"></param>
        private async void TargetBackTranslationTextChanged(string value)
        {
            // cancel any previous execution
            cancellationTokenSource?.Cancel();

            // Create a CancellationTokenSource to support cancellation
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            backgroundTask = CreateBackgroundTask(value, token);

            try
            {
                var backgroundWorkerResult = await backgroundTask;

                if ((backgroundTask.Status == TaskStatus.RanToCompletion) && (backgroundWorkerResult != null))
                {
                    textBoxStatus.Text = backgroundWorkerResult?.TextBoxText;
                    textBoxStatus.Tag = backgroundWorkerResult?.TextBoxTag;
                    toolTip.SetToolTip(textBoxStatus, backgroundWorkerResult?.TextBoxTooltip);
                }
            }
            catch (Exception ex)
            {
                LogExceptionMessage("TargetBackTranslationTextChanged", ex);
            }
        }

        private Task<BackgroundWorkerResult> CreateBackgroundTask(string value, CancellationToken cancelToken)
        {
            return Task.Factory.StartNew<BackgroundWorkerResult>(() =>
            {
                try
                {
                    Task.Delay(TimeSpan.FromMilliseconds(1000), cancelToken).Wait();
                    cancelToken.ThrowIfCancellationRequested();

                    var translatedLines = GetTranslatedLines(value);
                    var translatedCount = translatedLines.Count;
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: TargetBackTranslationTextChanged: cancelToken: {cancelToken.IsCancellationRequested}, SourceDataLineCount = '{SourceDataLineCount}', translatedCount = '{translatedCount}'");
                    var statusText = String.Empty;
                    if (SourceDataLineCount != translatedCount)
                    {
                        statusText = String.Format("There {0} currently {1} line{2} of text in the Target Translation box vs. {3} text line{4} in the source verse ({5}: {6}). Click (or hover your cursor here) to see the correspondence.",
                                                    (translatedCount > 1) ? "are" : "is",
                                                    translatedCount,
                                                    (translatedCount > 1) ? "s" : string.Empty,
                                                    SourceDataLineCount,
                                                    (SourceDataLineCount > 1) ? "s" : string.Empty,
                                                    (TextTokenMarkersSource.CountTextTokenMarkers > 1) ? "one for each of these markers" : "for this marker",
                                                    String.Join(",", TextTokenMarkersSource.Where(m => !m.IsParagraphMarkerWithoutText).Select(m => $"\\{m.USFMMarkerToken.Marker}")));
                    }

                    var preview = GetPreview(translatedLines, cancelToken);
                    var result = new BackgroundWorkerResult
                    {
                        TextBoxText = statusText,
                        TextBoxTag = translatedLines,
                        TextBoxTooltip = preview
                    };
                    return result;
                }
                catch (AggregateException ex)
                {
                    if ((ex.InnerExceptions.Count == 1) && (ex.InnerException is TaskCanceledException))
                        System.Diagnostics.Debug.WriteLine("CreateBackgroundTask: canceled task");
                    else
                        LogExceptionMessage("CreateBackgroundTask", ex);
                }
                catch (Exception ex)
                {
                    LogExceptionMessage("CreateBackgroundTask", ex);
                }
                return null;
            }, cancelToken);
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
            if (textBoxStatus.Tag == null)
                return;

            if (textBoxStatus.Tag is IVerseRef newReference)
            {
                // allow the user to decide whether to overwrite the edits (but only if he's back on the same verse we paused on. If not, then
                // we have to update)
                var overwriteEdits = (newReference?.ToString() != _verseReference?.ToString()) ||
                                     (MessageBox.Show("Would you like to keep the edited text here (click, 'Yes'), or refresh the target text from Paratext (click, 'No')?",
                                                      ParatextBackTranslationHelperPlugin.PluginName, MessageBoxButtons.YesNo) == DialogResult.No);
                if (overwriteEdits)
                    backTranslationHelperCtrl.IsModified = false; // putting this before GetNewReference causes us to refresh the editable box also

                GetNewReference(newReference);

                // if we didn't do it above, reset it to be not modified here, so it's the new beginning text and more easily overwritable
                if (!overwriteEdits)
                backTranslationHelperCtrl.IsModified = false;

                textBoxStatus.Clear();
                textBoxStatus.Tag = null;
                backTranslationHelperCtrl.Focus();  // so it doesn’t leave the cursor in the status textBox
            }
            else if ((textBoxStatus.Tag is List<string> textLines) && (TextTokenMarkersSource != null) && TextTokenMarkersSource.Any())
            {
                var preview = GetPreview(textLines);
                MessageBox.Show(preview, ParatextBackTranslationHelperPlugin.PluginName);
                backTranslationHelperCtrl.Focus();  // so it doesn’t leave the cursor in the status textBox
            }
        }

        private static readonly List<string> _previewInlineMarkersToIgnore = Properties.Settings.Default.GetPreviewInlineMarkersToIgnore.Cast<string>().ToList();

        private string GetPreview(List<string> textLines, CancellationToken? cancelToken = null)
        {
            string preview = null;
            var translatedLineCount = textLines.Count;
            var markerCount = TextTokenMarkersSource.Count;
            string lastNonInlineMarker = _processingQs ? @"\q1" : @"\p";  // occasionally correct... see note where _processingQs is defined
            int i = 0, j = 0;
            for (; i < markerCount; i++)
            {
                cancelToken?.ThrowIfCancellationRequested();
                var line = (translatedLineCount > j) ? textLines[j++] : String.Empty;
                bool isNoText = !String.IsNullOrEmpty(line.Trim());
                var token = TextTokenMarkersSource[i];
                var nextToken = (TextTokenMarkersSource.Count > i + 1) ? TextTokenMarkersSource[i + 1] : null;
                var tokenText = token.ToString();   // .Replace(Environment.NewLine, null);
                bool isInLine = IsInline(token.USFMMarkerToken);

                if (token.IsParagraphMarkerWithoutText)
                {
                    // write out '\p's and '\m's if they're followed by some inline marker, but don't write the empty text segment
                    lastNonInlineMarker = tokenText.Replace(Environment.NewLine, null);
                    preview += $"{tokenText} ";
                    j--;    // since we're not going past the current text one
                    continue;
                }
                if (tokenText.Contains("*"))
                {
                    if (isNoText)
                        tokenText = $"{Environment.NewLine}({lastNonInlineMarker} cont)";
                    else
                        continue;
                }
                else if (!isInLine)
                    lastNonInlineMarker = tokenText.Replace(Environment.NewLine, null);

                var suffix = isInLine ? " " : String.Empty; //  Environment.NewLine;
                preview += $"{tokenText} {line}{suffix}";
            }

            while (translatedLineCount > i)
                preview += $"(adding as new \\p) {textLines[i++]}{Environment.NewLine}";
            return preview;

            static bool IsInline(IUSFMMarkerToken token)
            {
                return (token.IsFootnoteOrCrossReference || 
                        token.IsMetadata || 
                        !String.IsNullOrEmpty(token.EndMarker) ||                   // e.g. \rq ... \rq*
                        _previewInlineMarkersToIgnore.Any(s => token.ToString().Contains(s)));     // e.g. \va that comes immediately after initial post-\p \v
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
                var error = LogExceptionMessage("GetNewReference", ex);
                MessageBox.Show(error, ParatextBackTranslationHelperPlugin.PluginName);
            }
            finally
            {
                Cursor = cursor;
            }
        }

        public static string LogExceptionMessage(string className, Exception ex)
        {
            string msg = "Error occurred: " + ex.Message;
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
                msg += $"{Environment.NewLine}because: (InnerException): {ex.Message}";
            }

            Util.DebugWriteLine(className, msg);
            return msg;
        }

        private void UpdateData(BackTranslationHelperModel model)
        {
            Text = GetFrameText(_projectSource, _projectTarget, _versesReference);
            _model = model;
            backTranslationHelperCtrl.Initialize(displayExistingTargetTranslation: true);
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
                var (sourceData, sourceDataAlternate) = CurrentSourceData;
                var model = new BackTranslationHelperModel
                {
                    SourceData = sourceData ?? "<source data empty>",
                    SourceDataAlternate = sourceDataAlternate,
                    TargetData = currentTargetData,
                    TargetDataPreExisting = currentTargetData,
                    TargetsPossible = new List<TargetPossible>()
                };
                return model;
            }
        }

        /// <summary>
        /// This field returns one or two flavors of the source data. For Paratext, the 'sourceData'
        /// return value represents the segments of scripture text in a marker, which could be a whole
        /// paragraph (e.g. after a \v marker and before another paragraph-breaking marker), or just a
        /// portion of text (e.g. after a \v marker and before an inline, say, footnote marker, which
        /// interrupts the full paragraph). The user may want to translate the entire paragraph as a 
        /// unit to get a better translation rather than shorter snippets of text, in which case, they
        /// can check the translateNothingButPublishableScriptureTextMenuItem menu and we will combine
        /// all scripture text together, followed separately by all footnote text to be converted, which
        /// is returned in the sourceDataAlternate return.
        /// </summary>
        private (string sourceData, string sourceDataAlternate) CurrentSourceData
        {
            get
            {
                var keyBookChapterVerse = GetBookChapterVerseRangeKey(_verseReference);
                if (!UsfmTokensSource.TryGetValue(keyBookChapterVerse, out List<IUSFMToken> tokens))
                {
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: Loading UsfmTokensSource for {keyBookChapterVerse}");
                    tokens = _projectSource.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum, _verseReference.VerseNum)?.ToList();
                    UsfmTokensSource[keyBookChapterVerse] = tokens;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: Already have UsfmTokensSource for {keyBookChapterVerse}");
                }

                var data = tokens?.OfType<IUSFMTextToken>()
                                  .Where(t => IsPublishableVernacular(t, tokens) && IsMatchingVerse(t.VerseRef, _verseReference))
                                  .ToDictionary(ta => ta, ta => ta.VerseRef);

                if ((data == null) || !data.Any())
                    return (null, null);

                TextTokenMarkersSource = TextTokenMarkers.GetTextTokenMarkers(tokens, data.Keys.ToList());

                var textValues = data.Select(t => t.Key.Text);
                SourceDataLineCount = textValues.Count();
                var sourceString = string.Join(Environment.NewLine, textValues);

                // if the user is requesting, grab all the scripture text first and then the footnotes, as separate
                //  stuff to translate (not interspersed, so as not to break up the translatable chunks)
                var sourceStringAlternate = GetSourceAlternate(tokens, data.Keys.ToList());

                // set the verse reference to the last of a combined set of verses (which we can only get from the USFM markers)
                _versesReference = data.Values.First();
                _verseReferenceLast = (_versesReference.RepresentsMultipleVerses)
                                        ? _versesReference.AllVerses.Last()
                                        : _verseReference;

                return (sourceString, sourceStringAlternate);
            }
        }

        // when generating the 'alternate' source translation (i.e. ignoring, or rather, moving inline markers w/ text to the end),
        //  treat the \q1-4 markers special, so their text segments get combined even though they're in different paragraphs (leads to better translation).
        // NB: BUT there is one glitch: if a \q1 paragraph marker is immediately followed by a \v marker, then technically,
        //  the \q1 is in the preceding verse reference; not this one. So though we'd want to say that we're 'processingQs' in this case,
        //  we can't, so the text on that line will be translated separately from the \q2, etc., that follows it.
        // By making this a global member, it will remember going from verse-to-verse. But one place this would not work: 
        //  if the user processes a verse that ends with a \q1-4 marker, but rather than clicking 'Next', goes to some other, non-sequential verse, 
        //  we'd be mistaken that we're processingQs... (but since this is just a preview and not something substantive, let's just ignore this
        //  hopefully unusual case.
        private bool _processingQs = false;

        private string GetSourceAlternate(List<IUSFMToken> tokens, List<IUSFMTextToken> textTokens)
        {
            string sourceStringAlternate = null;
            if (translateNothingButPublishableScriptureTextMenuItem.Checked)
            {
                string textValuesAlternate = null;
                List<string> textValuesAlternateFootnotes = new();
                foreach (var token in tokens)
                {
                    // if the token is a paragraph break token (\i.e. \p and \q{digit}), then put a new line in the running text
                    if (IsParagraphToken(token))
                    {
                        // but not for \q1-\q4, bkz we want those to be combined into a single run of text
                        _processingQs = (token is IUSFMMarkerToken markerToken) && (markerToken.Marker.Contains("q"));
                        if (!_processingQs)
                        {
                            textValuesAlternate += Environment.NewLine + Environment.NewLine;   // use 2 so it's more visible (since we're removing the 'va' and 'vp' verse numbering)
                            continue;
                        }
                    }

                    // if it's not something we want to translate (e.g. not a text marker or a va or vp verse numbers (which are text markers))...
                    if (!textTokens.Contains(token) || 
                        (AsTextToken(token, out IUSFMTextToken textToken) && !IsTranslatable(textToken, tokens)))
                        continue;   // skip it

                    // if it's scripture text (i.e. the translatable stuff)...
                    if (IsScriptureText(textToken))
                    {
                        textValuesAlternate += textToken.Text;  // add it to the running accumulation
                    }
                    else
                    {
                        // must be a footnote
                        textValuesAlternateFootnotes.Add(textToken.Text);
                    }
                }

                // combine the text fragments of inline markers too, but add them after the main, regular text of the verse, 
                //  so they don't interfere with the translation of the main text
                sourceStringAlternate = textValuesAlternateFootnotes.Aggregate(textValuesAlternate.Replace("  ", " ") + Environment.NewLine,
                                                                               (curr, next) => curr + Environment.NewLine + next);
            }

            return sourceStringAlternate;

            static bool AsTextToken(IUSFMToken token, out IUSFMTextToken textToken)
            {
                if (token is IUSFMTextToken)
                {
                    textToken = token as IUSFMTextToken;
                    return true;
                }
                textToken = null;
                return false;
            }
        }

        private static readonly List<string> _additionalMarkersToTranslate = Properties.Settings.Default.AdditionalMarkersToTranslate.Cast<string>().ToList();

        // this would return true for both regular scripture text (i.e.  text after any of these markers:
        // \v, \q[1-3], \m, \pc, etc) and footnote text that is translatable (i.e. \ft)
        private static bool IsTranslatable(IUSFMTextToken token, List<IUSFMToken> tokens)
        {
            PreviousToken(token, tokens, out IUSFMMarkerToken mt);
            return (IsScriptureText(token) && (mt.Marker != "va")) ||
                    _additionalMarkersToTranslate.Contains(mt.Marker);
        }

        private static bool IsScriptureText(IUSFMTextToken token)
        {
            return (token.IsPublishableVernacular && token.IsScripture);
        }

        // normally, text tokens are publishable, but there are some that aren't (e.g. the text content of an \id marker).
        // And there's one case that seems like a bug to me, but which I've been told has worked that way forever and so
        // there's no changing it now... vis-a-vis:
        // the \va...\va* inline marker is defined differently depending on whether it comes immediately after a \v [num(s)] 
        // marker than if it comes elsewhere in a verse. The relevant difference is that when it comes immediately after a \v 
        // marker, it's value for IsPublishableVernacular (false) and IsMetadata (true) are opposite from the other case. 
        // So... if IsPublishableVernacular is false, at least check if this is that case, and return true, so we'll try to 
        // translate it as the others are (bkz we only send IsPub text segments for translation)
        private static bool IsPublishableVernacular(IUSFMTextToken t, List<IUSFMToken> tokens)
        {
            return t.IsPublishableVernacular || 
                   (PreviousToken(t, tokens, out IUSFMMarkerToken mt) && (mt.Marker == "va") && mt.IsMetadata);
        }

        private static bool PreviousToken(IUSFMTextToken t, List<IUSFMToken> tokens, out IUSFMMarkerToken previousToken)
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

        public class TextMarkerToken
        {
            public IUSFMMarkerToken USFMMarkerToken { get; set; }

            public bool IsParagraphMarkerWithoutText { get; set; }

            public TextMarkerToken(IUSFMMarkerToken iUsfmMarkerToken, bool isParagraphMarkerWithoutText)
            {
                USFMMarkerToken = iUsfmMarkerToken;
                IsParagraphMarkerWithoutText = isParagraphMarkerWithoutText;
            }

            public override string ToString()
            {
                return USFMMarkerToken.ToString();
            }
        }

        public class TextTokenMarkers : List<TextMarkerToken>
        {
            public int CountTextTokenMarkers
            {
                get { return this.Count(i => !i.IsParagraphMarkerWithoutText); }
            }

            public static TextTokenMarkers GetTextTokenMarkers(List<IUSFMToken> tokens, List<IUSFMTextToken> textTokens)
            {
                IUSFMMarkerToken lastMarkerToken = null;
                var textTokenMarkers = new TextTokenMarkers();
                foreach (var token in tokens)
                {
                    if (token is IUSFMMarkerToken)
                    {
                        if (IsParagraphToken(lastMarkerToken))
                        {
                            textTokenMarkers.Add(new TextMarkerToken(lastMarkerToken, true));
                            lastMarkerToken = null;
                        }
                        lastMarkerToken = token as IUSFMMarkerToken;
                    }
                    else if (textTokens.Contains(token) && (lastMarkerToken != null))
                    {
                        textTokenMarkers.Add(new TextMarkerToken(lastMarkerToken, false));
                        lastMarkerToken = null;
                    }
                }
                return textTokenMarkers;
            }
        }

        private string CurrentTargetData
        {
            get
            {
                var bookChapterKey = GetBookChapterKey(_verseReference);
                if (!UsfmTokensTarget.TryGetValue(bookChapterKey, out SortedDictionary<string, List<IUSFMToken>> vrefTokens))
                {
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: Loading UsfmTokensTarget for {bookChapterKey}");
                    var chapterTokens = _projectTarget.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum).ToList();
                    var dict = chapterTokens.GroupBy(t => t.VerseRef, t => t, (key, g) => new { VerseRef = key, USFMTokens = g.ToList() })
                                            .ToDictionary(t => GetBookChapterVerseRangeKey(t.VerseRef), t => t.USFMTokens);
                    vrefTokens = new SortedDictionary<string, List<IUSFMToken>>(dict);
                    UsfmTokensTarget[bookChapterKey] = vrefTokens;
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
                    System.Diagnostics.Debug.WriteLine($"PtxBTH: RequeryWarning: _verseReference: {_verseReference}, _versesReference: {_versesReference}");

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
                if (_writeLock == null)
                {
                    _writeLock = _projectTarget.RequestWriteLock(_plugin, ReleaseRequested, _verseReference.BookNum, _verseReference.ChapterNum);

                    if (_writeLock == null) // if it still is, we should warn the user that it isn't going to work
                    {
                        MessageBox.Show($"You don't have edit privilege on this chapter: {_verseReference}");
                        return false;
                    }
                }

                var tokens = vrefTokensTarget.SelectMany(d => d.Value).ToList();

                _projectTarget.PutUSFMTokens(_writeLock, tokens, _verseReference.BookNum);
                Unlock();
                return true;
            }
            catch (Exception ex)
            {
                var error = LogExceptionMessage("WriteToTarget", ex);
                MessageBox.Show($"Exception caught:\n{error}");
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

            vrefTokensTarget[keyBookChapterVerses] = tokensSource;

            // go thru all the ones we had and put the translated text into the text ones and transfer the non-text ones in order into the list to Put
            var i = 0;
            TextToken latestTextToken = null;
            var updatedTokens = new List<IUSFMToken>();
            foreach (var token in tokensTarget)
            {
                IUSFMToken updatedToken = token;
                if ((token is IUSFMTextToken textToken) && 
                    IsPublishableVernacular(textToken, tokensTarget) 
                    && IsMatchingVerse((IVerseRef)textToken.VerseRef, verseReference))
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

        private static readonly List<string> _paragraphMarkers = new() { "p", "m" };

        private static bool IsParagraphToken(IUSFMToken token)
        {
            return (token is IUSFMMarkerToken markerToken) && (markerToken.Type == MarkerType.Paragraph); // not needed? if so, initialize list from a setting: && _paragraphMarkers.Contains(markerToken.Marker);
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

        public class BackgroundWorkerResult
        {
            public string TextBoxText { get; set; } 
            public List<string> TextBoxTag { get; set; }
            public string TextBoxTooltip { get; set; }
            public override string ToString()
            {
                return $"Text: {TextBoxText}, Tag: {String.Join(",", TextBoxTag)}, Tooltip: {TextBoxTooltip}";
            }
        }
    }
}
