// Uncomment this define to enable code that helps in test creation for a new scenario
//  See UnitTest_PtxBackTrHelper::It_Can_Determine_Correct_Update_To_Target_Project in the TestBwdc project for details
// #define SerializeToCreateTestFiles

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
using System.Runtime.InteropServices;
using SilEncConverters40.EcTranslators.NllbTranslator;
using System.Diagnostics;
using SilEncConverters40;
using SilEncConverters40.EcTranslators;
using SilEncConverters40.PtxConverters;

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
        /// this is set to the last verse that was clicked on in Paratext (so we can update to that 
        /// if the user turns off the pause button)
        /// </summary>
        private IVerseRef _verseReferenceDuringPause;

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

            InitializeSettings(true);

            _host = host;
            _plugin = plugin;
            _versesReference = _verseReferenceLast = _verseReference = initialVerseReference;
            _setSyncReferenceGroup = setSyncReferenceGroup;

            InitializeProjects(_host);

            // this form is the implementation of the way to get get data
            backTranslationHelperCtrl.BackTranslationHelperDataSource = this;
            backTranslationHelperCtrl.buttonPauseUpdating.Visible = true;
            backTranslationHelperCtrl.RegisterForNotification(BackTranslationHelperCtrl.SubscribeableEventKeyTargetBackTranslationTextChanged,
                                                              TargetBackTranslationTextChanged);

            AddToSettingsMenu(backTranslationHelperCtrl);
            _host.VerseRefChanged += Host_VerseRefChanged;
        }

        /// <summary>
        /// If Ptx is upgraded, then we lose the settings. This should upgrade them, if we reinstall (bkz
        /// UpgradeSettings will be true 1st time after install).
        /// Settings if user wants to adjust something are stored in: \AppData\Local\United_Bible_Societies\Paratext.exe_[guid]\[version #]\user.config file
        /// e.g. C:\Users\pete_\AppData\Local\United_Bible_Societies\Paratext.exe_Url_10vizzham1xunpacgy3t1em4g1uelorz\9.3.103.14
        /// </summary>
        /// <param name="bDoUpgrade"></param>
        private void InitializeSettings(bool bDoUpgrade)
        {
            if (bDoUpgrade && Properties.Settings.Default.UpgradeSettings)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeSettings = false;
                Properties.Settings.Default.Save();
            }
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

            // add a check-able menu to get it to show/translate all the scripture text first and then the inline markers afterwards
            //  so that translation is don't w/o interuption by inline markers
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

        private string CreateParatextProjectConverterInput(bool? overrideTranslateNothingButPublishableScripture = null)
        {
            var shouldTranslateNothingButPublishableScripture = (overrideTranslateNothingButPublishableScripture == null)
                    ? translateNothingButPublishableScriptureTextMenuItem.Checked
                    : (bool)overrideTranslateNothingButPublishableScripture;

            return $"{GetBookChapterVerseRangeKey(_verseReference)};{shouldTranslateNothingButPublishableScripture}";
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

            var projectSource = QueryForProject("Source language");
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
                _projectSource ??= QueryForProject("Source language");
                _projectTarget ??= QueryForProject("Target language");
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
                        statusText = String.Format("There {0} currently {1} line{2} in the edit box vs {3} line{4} in the source. Click (or hover your cursor here) to see the correspondence",
                                                    (translatedCount > 1) ? "are" : "is",
                                                    translatedCount,
                                                    (translatedCount > 1) ? "s" : string.Empty,
                                                    SourceDataLineCount,
                                                    (SourceDataLineCount > 1) ? "s" : string.Empty
                                                    //,(TextTokenMarkersSource.CountTextTokenMarkers > 1) ? "one for each of these markers" : "for this marker",
                                                    //String.Join(",", TextTokenMarkersSource.Where(m => !m.IsParagraphMarkerWithoutText).Select(m => $"\\{m.USFMMarkerToken.Marker}"))
                                                    );
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

            // if the change was in the chapter we're processing, then clear what we think the data is, so we'll repull it again
            if (IsDirty(_verseReference, bookNum, chapterNum))
                UsfmTokensTarget.Clear();
        }

        private void Host_VerseRefChanged(IPluginHost sender, IVerseRef newReference, SyncReferenceGroup group)
        {
            var newRef = (newReference.RepresentsMultipleVerses) ? newReference.AllVerses.First() : newReference;
            System.Diagnostics.Debug.WriteLine($"PtxBTH: In Host_VerseRefChanged {newReference} (newRef: {newRef}) & {group}, _isNotInFocus: {_isNotInFocus}, IsModified: {backTranslationHelperCtrl.IsModified}, IsPaused {backTranslationHelperCtrl.IsPaused}");

            // since this will initialize the _verseReference, which is intended to be the first
            //  of a series of verses...
            // UPDATE: but not if the Form isn't in focus (so we don't thrash around converting stuff while
            //  the user may be editing stuff in Ptx
            // UPDATE (2024-02-17): added an explicit pause button to the control, so I don't have to modify it if I don't want it to requery
            if ((_isNotInFocus && backTranslationHelperCtrl.IsModified) || backTranslationHelperCtrl.IsPaused)
            {
                var reason = backTranslationHelperCtrl.IsPaused
                                ? "translation is paused"
                                : "Target Translation box was modified";

                textBoxStatus.Text = $"Staying on {_verseReference} because the {reason}. Click here to update to current verse in Paratext";
                Debug.WriteLine($"Set textBoxStatus.Text = {textBoxStatus.Text}");
                textBoxStatus.Tag = _verseReferenceDuringPause = newRef;
                Application.DoEvents(); // this says we need to do this for when it won't display the change: https://social.msdn.microsoft.com/Forums/vstudio/en-US/983d2e3b-9bcb-4c9c-9e85-59f8b2051b3e/program-updating-a-textbox-does-not-work?forum=csharpgeneral
                return;
            }
            else
                textBoxStatus.Tag = null;

            GetNewReference(newRef);
            disableActivateRefreshUntilNextVerse = false;   // reenable the activate refresh
        }

        private void TextBoxStatus_Click(object sender, System.EventArgs e)
        {
            if (textBoxStatus.Tag == null)
                return;

            if (textBoxStatus.Tag is IVerseRef newReference)
            {
                // whether we're reloading or not, if it's paused, then reset it
                if (backTranslationHelperCtrl.IsPaused)
                    backTranslationHelperCtrl.SetPausedAndImage(false);

                // allow the user to decide whether to overwrite the edits (but only if he's back on the same verse we paused on. If not, then
                // we have to update)
                var hasVerseChanged = newReference?.ToString() != _verseReference?.ToString();
                var overwriteEdits = hasVerseChanged ||
                                     (MessageBox.Show("Would you like to keep the edited text here (click, 'Yes'), or refresh the target text from Paratext (click, 'No')?",
                                                      ParatextBackTranslationHelperPlugin.PluginName, MessageBoxButtons.YesNo) == DialogResult.No);
                if (overwriteEdits)
                {
                    backTranslationHelperCtrl.IsModified = false;   // putting this before GetNewReference causes us to refresh the editable box also
                }
                GetNewReference(newReference);

                // if we didn't do it above, reset it to be not modified here (unless the verse didn’t change),
                //  so it's the new beginning text and more easily overwritable. But if the verse didn't change
                //  then we don't want to mark it as not modified or a subsequent clicking around in Ptx could clobber it.
                if (hasVerseChanged && !overwriteEdits)
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
        private bool _processingQs = false;

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
            _verseReference = newReference;
            BackTranslationHelperModel model = null;    // means query the interface to get the data
            var cursor = Cursor;
            try
            {
                Cursor = Cursors.WaitCursor;
                backTranslationHelperCtrl.GetNewData(false, ref model);
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
            try
            {
                Text = GetFrameText(_projectSource, _projectTarget, _versesReference);
                _model = model;
                var bookNum = _verseReference.BookNum;
                var chapterNum = _verseReference.ChapterNum;
                _model.IsTargetTranslationEditable = _projectTarget.CanEdit(_plugin, bookNum, chapterNum);
                backTranslationHelperCtrl.Initialize(_model);
                _updateControls(_model);
            }
            catch
            {
                // this can happen if the user is closing the form while we're still processing the translation calls.
            }
        }

        private void CheckAndAddNextToAllMenu(List<IEncConverter> theTranslators)
        {
            var firstWithOwnCreds = GetFirstFullChapterConverter();
            if (firstWithOwnCreds == null)
                return;

            var translatorName = firstWithOwnCreds.Name;
            var translateEntireChapterMenuItem = new System.Windows.Forms.ToolStripMenuItem
            {
                Name = "translateEntireChapterMenuItem",
                Size = new System.Drawing.Size(247, 22),
                Text = "&Translate the rest of the chapter",
                ToolTipText = $"Click this option to translate the source text into the target project with the '{translatorName}' translator beginning at the current verse until the end of the chapter.",
            };
            translateEntireChapterMenuItem.Click += new System.EventHandler(this.TranslateEntireChapterMenuItem_Click);
            backTranslationHelperCtrl.AddToSettingsMenu(translateEntireChapterMenuItem);
        }

        private IEncConverter GetFirstFullChapterConverter()
        {
            // allow users to convert entire chapters if the NLLB converter is present (OR if they have at least 1 w/ their own creds)
            return  backTranslationHelperCtrl.TheTranslators.FirstOrDefault(t => (t is PromptExeTranslator) || // has their own creds by definition
                                                                                 ((t is TranslatorConverter translatorConverter) && 
                                                                                    translatorConverter.HasUserOverriddenCredentials)) ??

                    // if there aren't any 'Prompt' or 'Translators' converters w/ their own creds...
                    //  then check if there's some non-translator EncConverter to use
                    backTranslationHelperCtrl.TheTranslators.FirstOrDefault(t => !(t is TranslatorConverter));
        }

        private void TranslateEntireChapterMenuItem_Click(object sender, EventArgs e)
        {
            var firstWithOwnCreds = GetFirstFullChapterConverter();
            if (firstWithOwnCreds == null)
                return;

            // remove the TargetPossible for the one we're using (so it'll get called) and set the 'TargetPossible'
            //  for all the other translators (so they don't get called during GetNewData below
            //  (if the TargetPossible already exists, it thinks they were already converted (cf. Word and PtxProjectData)
            var translatorName = firstWithOwnCreds.Name;
            _model.TargetsPossible.Clear();
            backTranslationHelperCtrl.TheTranslators.Where(t => t.Name != translatorName).ToList()
                                                    .ForEach(t =>
                                                    {
                                                        _model.TargetsPossible.Add(new TargetPossible
                                                        {
                                                            PossibleIndex = backTranslationHelperCtrl.TheTranslators.IndexOf(t),
                                                            TranslatorName = t.Name,
                                                            TargetData = "Ignore"
                                                        });
                                                    });

            var translatorIndex = backTranslationHelperCtrl.TheTranslators.IndexOf(firstWithOwnCreds);
            string keyBookChapter = null, startingKeyBookChapter = GetBookChapterKey(_verseReference);
            backTranslationHelperCtrl.IsPaused = disableActivateRefreshUntilNextVerse = true;    // something in the below causes the window to lose focus, which causes us to reprocess all the converters

            IWriteLock writeLock = null;
            try
            {
                // first have to get the lock (bkz if something was changed via Ptx and not saved, then the user
                //  will need to either save it or throw it away, which would force us to need to do a requery)
                if (!AcquireWriteLock(ref writeLock))
                {
                    // it won't still be null if the user chose 'Yes' or 'No' to Paratext dialog that asks,
                    //  "Do you want to save the changes?", but it might if the user doesn't have edit permissions.
                    // if we still don't have it, it means the user doesn't have the privilege to change the target project
                    MessageBox.Show($"You don't have edit privilege on this chapter: {_verseReference} of the {_projectTarget.ShortName} project");
                    return;
                }

                var verseReferenceNext = _verseReference;
                SortedDictionary<string, List<IUSFMToken>> vrefTokensTarget = null;
                do
                {
                    _verseReference = verseReferenceNext;

                    // we probably already have it, but the next time thru the loop, we'll need the next verse pulled in
                    //  this sets UsfmTokensSource for the current verse, which we'll need 
                    var currentSourceData = CurrentSourceData;

                    // for the PtxProjectEncConverter, we have to do this one specially (and then we don't need to call GetNewData)
                    if (firstWithOwnCreds is PtxProjectEncConverter ptxProjectEncConverter)
                    {
                        // note that since the data is coming from a paratext project, we don't need to consider 'translating' the verse text
                        //  as a unit and doing the inline markers later (since there's no 'translation' going on here)
                        var ptxProjectConverterInput = CreateParatextProjectConverterInput(overrideTranslateNothingButPublishableScripture: false);
                        InitializeModelForPtxProjectConverters(_model, ptxProjectConverterInput, ptxProjectEncConverter, translatorIndex);
                    }
                    else
                    {
                        // otherwise, clear the one we want to be called (leaving the others filled so they don't get called)
                        _model.TargetsPossible.RemoveAll(tp => tp.TranslatorName == translatorName);

                        // set the new source text
                        _model.SourceData = currentSourceData.sourceData;

                        // call the function that calls the 'converters to update the data for the new verse
                        backTranslationHelperCtrl.GetNewData(true, ref _model);
                    }

                    var translation = _model.TargetsPossible.FirstOrDefault(t => t.TranslatorName == translatorName)?.TargetData;

                    vrefTokensTarget = CalculateTargetTokens(_verseReference, _versesReference, translation, UsfmTokensSource, UsfmTokensTarget);

                    UsfmTokensTarget[startingKeyBookChapter] = vrefTokensTarget;

                    textBoxStatus.Text = $"{translatorName} translated {_verseReference} as {translation}";

                    Application.DoEvents();

                    // move on to the next verse
                    verseReferenceNext = _verseReferenceLast.GetNextVerse(_projectSource);
                    keyBookChapter = GetBookChapterKey(verseReferenceNext);
                } while (keyBookChapter == startingKeyBookChapter);

                // now write all the collected text to the target project
                WriteToTarget(writeLock, vrefTokensTarget);
            }
            catch (Exception ex)
            {
                var error = LogExceptionMessage("TranslateEntireChapter", ex);
                MessageBox.Show($"Exception:\n{error}");
            }
            finally
            {
                Unlock(writeLock);
            }

            backTranslationHelperCtrl.IsPaused = disableActivateRefreshUntilNextVerse = false;   // reenable the activate refresh
            _model.TargetsPossible.Clear();                 // so we don't keep ignoring the others...
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
                    TargetsPossible = new List<TargetPossible>(),
                    DisplayExistingTargetTranslation = true,
                };

                // special processing for the PtxProjectEncConverter (since we have to pass it the verse ref rather than text to 'translate')
                var ptxProjectConverterInput = CreateParatextProjectConverterInput();

                foreach (PtxProjectEncConverter ptxProjectEncConverter in backTranslationHelperCtrl.TheTranslators.Where(t => t is PtxProjectEncConverter))
                {
                    InitializeModelForPtxProjectConverters(model, ptxProjectConverterInput, ptxProjectEncConverter, 
                                                           backTranslationHelperCtrl.TheTranslators.IndexOf(ptxProjectEncConverter));
                }
                return model;
            }
        }

        private static void InitializeModelForPtxProjectConverters(BackTranslationHelperModel model, string ptxProjectConverterInput, 
                                                                   PtxProjectEncConverter ptxProjectEncConverter, int indexPossibleTarget)
        {
            var targetPossible = model.TargetsPossible.FirstOrDefault(tp => tp.TranslatorName == ptxProjectEncConverter.Name);
            if (targetPossible == null)
            {
                targetPossible = new TargetPossible
                {
                    PossibleIndex = indexPossibleTarget,
                    TranslatorName = ptxProjectEncConverter.Name
                };
                model.TargetsPossible.Add(targetPossible);
            }

            targetPossible.TargetData = ptxProjectEncConverter.Convert(ptxProjectConverterInput);
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
                                  .Where(t => PtxPluginHelpers.IsPublishableVernacular(t, tokens) && PtxPluginHelpers.IsMatchingVerse(t.VerseRef, _verseReference))
                                  .ToDictionary(ta => ta, ta => ta.VerseRef);

                if ((data == null) || !data.Any())
                    return (null, null);

                TextTokenMarkersSource = TextTokenMarkers.GetTextTokenMarkers(tokens, data.Keys.ToList());

                var textValues = data.Select(t => t.Key.Text);
                SourceDataLineCount = textValues.Count();
                var sourceString = string.Join(Environment.NewLine, textValues);

                // if the user is requesting, grab all the scripture text first and then the footnotes, as separate
                //  stuff to translate (not interspersed, so as not to break up the translatable chunks)
                var sourceStringAlternate = PtxPluginHelpers.GetSourceAlternate(translateNothingButPublishableScriptureTextMenuItem.Checked, tokens, data.Keys.ToList(),
                                                                                ref _processingQs);

                // set the verse reference to the last of a combined set of verses (which we can only get from the USFM markers)
                _versesReference = data.Values.First();
                _verseReferenceLast = (_versesReference.RepresentsMultipleVerses)
                                        ? _versesReference.AllVerses.Last()
                                        : _verseReference;

                return (sourceString, sourceStringAlternate);
            }
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
                        if (PtxPluginHelpers.IsParagraphToken(lastMarkerToken))
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
                    var chapterTokens = _projectTarget.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum)?.ToList();
                    if (chapterTokens == null)
                        return null;    // some books don't return proper things... (e.g. for me it was GLO)

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

                // we may have called this (during activate) to update the surrounding verses (if the user made
                //  a change in Ptx and re-activated the plugin form and Ptx said do you want to save changes
                //  and they said 'yes')... but if the target text has been modified, then don't change it
                if (backTranslationHelperCtrl.IsModified || String.IsNullOrEmpty(bookChapterVerseKey) || !vrefTokens.ContainsKey(bookChapterVerseKey))
                    return null;

                var tokens = vrefTokens[bookChapterVerseKey];
                var data = tokens.OfType<IUSFMTextToken>()
                                 .Where(t => PtxPluginHelpers.IsPublishableVernacular(t, tokens) && PtxPluginHelpers.IsMatchingVerse(t.VerseRef, _verseReference));

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

        private void ReleaseRequested(IWriteLock obj)
        {
            Unlock(obj);
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
            if (button != ButtonPressed.UpdateToCurrent)
            {
                disableActivateRefreshUntilNextVerse = true;
            }
            else
            {
                disableActivateRefreshUntilNextVerse = false;
                _verseReference = _verseReferenceDuringPause ?? _verseReference;
            }

            _buttonPressed = button;
        }

        void IBackTranslationHelperDataSource.Cancel()
        {
            disableActivateRefreshUntilNextVerse = true;
            Close();
        }

        void IBackTranslationHelperDataSource.SetDataUpdateProc(Action<BackTranslationHelperModel> updateControls)
        {
            _updateControls = updateControls;
        }

        void IBackTranslationHelperDataSource.Log(string message)
        {
            try
            {
                _host.Log(_plugin, $"PtxBTH: {message}");
            }
            catch
            {
                // this throws, I think, if the msg is too long... if it does, just ignore it
            }
        }

        void IBackTranslationHelperDataSource.MoveToNext()
        {
            MoveToNext();
        }

        private void MoveToNext()
        {
            _buttonPressed = ButtonPressed.MoveToNext;

            _verseReference = _verseReferenceLast.GetNextVerse(_projectSource);

            // Set the sync group our window belongs to:
            _setSyncReferenceGroup(_verseReference);
        }

        private bool AreWeChangingTheTarget { get; set; }

        bool IBackTranslationHelperDataSource.WriteToTarget(string text)
        {
            IWriteLock writeLock = null;
            try
            {
                System.Diagnostics.Debug.WriteLine("PtxBTH: In IBackTranslationHelperDataSource.WriteToTarget");

                _buttonPressed = ButtonPressed.WriteToTarget;

                // first have to get the lock (bkz if something was changed via Ptx and not saved, then the user will
                // need to either save it or throw it away, the former of which would force us to need to do a requery)
                if (!AcquireWriteLock(ref writeLock))
                {
                    // it won't still be null if the user chose 'Yes' or 'No' to Paratext dialog that asks,
                    //  "Do you want to save the changes?", but it might if the user doesn't have edit permissions.
                    // if we still don't have it, it means the user doesn't have edit permissions on the target project
                    MessageBox.Show($"You don't have edit privilege on this chapter: {_verseReference} of the {_projectTarget.ShortName} project");
                    return false;
                }

                // next, if the user did choose 'Yes' to save during the call to RequestWriteLock, that results in 
                //  UsfmTokensTarget being empty (bkz we got a call to ScriptureDataChangedHandlerTarget to inform us
                //  that the scripture text has changed)
                SortedDictionary<string, List<IUSFMToken>> vrefTokensTarget;
                if (!UsfmTokensTarget.Any() || 
                    (vrefTokensTarget = CalculateTargetTokens(_verseReference, _versesReference, text, UsfmTokensSource, UsfmTokensTarget)) == null)
                {
                    // To get it to work properly, requery the data and return false (so we try again rather than move on)
                    GetNewReference(_verseReference);
                    return false;   // this will cause us to try it again automatically.
                }

                return WriteToTarget(writeLock, vrefTokensTarget);
            }
            catch (Exception ex)
            {
                var error = LogExceptionMessage("WriteToTarget", ex);
                MessageBox.Show($"Exception:\n{error}");
            }
            finally
            {
                Unlock(writeLock);
            }
            return false;
        }

        private bool AcquireWriteLock(ref IWriteLock writeLock)
        {
            writeLock = _projectTarget.RequestWriteLock(_plugin, ReleaseRequested, _verseReference.BookNum, _verseReference.ChapterNum);
            return writeLock != null;
        }

        private bool WriteToTarget(IWriteLock writeLock, SortedDictionary<string, List<IUSFMToken>> vrefTokensTarget)
        {
            try
            {
                var tokens = vrefTokensTarget.SelectMany(d => d.Value).ToList();

                AreWeChangingTheTarget = true;
                _projectTarget.PutUSFMTokens(writeLock, tokens, _verseReference.BookNum);
                return true;
            }
            catch (Exception ex)
            {
                var error = LogExceptionMessage("WriteToTarget", ex);
                MessageBox.Show($"Exception:\n{error}");
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
            var matchingTokensInTarget = vrefTokensTarget.Where(kvp => kvp.Value.Any(t => PtxPluginHelpers.IsMatchingVerse(t.VerseRef, verseReference))).ToList();
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
                    PtxPluginHelpers.IsPublishableVernacular(textToken, tokensTarget) 
                    && PtxPluginHelpers.IsMatchingVerse((IVerseRef)textToken.VerseRef, verseReference))
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

        private void BackTranslationHelperForm_Resize(object sender, EventArgs e)
        {
            disableActivateRefreshUntilNextVerse = (this.WindowState == FormWindowState.Minimized);
        }

        private void BackTranslationHelperForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            disableActivateRefreshUntilNextVerse = true;    // don't let it refresh if we're closing

            // only allow Cancel or ReplaceEvery
            if ((e.CloseReason != CloseReason.UserClosing) &&
                ((_buttonPressed == ButtonPressed.WriteToTarget)
                || (_buttonPressed == ButtonPressed.MoveToNext)
                || (_buttonPressed == ButtonPressed.Copy)))
                e.Cancel = true;

            _host.VerseRefChanged -= Host_VerseRefChanged;

            if (this.WindowState == FormWindowState.Minimized)
                return; // don't save location info if the dialog is minimized

            Properties.Settings.Default.DefaultWindowState = WindowState;
            Properties.Settings.Default.WindowLocation = Location;
            
            // someone had it disappear except for the frame... probably bkz Size was 0,0 somehow at this point
            if (Size.Height >= MinimumSize.Height &&
                Size.Width >= MinimumSize.Width)
            {
                Properties.Settings.Default.WindowSize = Size;
            }
            Properties.Settings.Default.Save();

            Dispose();
        }

        private void Unlock(IWriteLock writeLock)
        {
            if (writeLock != null)
            {
                IWriteLock temp = writeLock;
                writeLock = null;  // to prevent it being called twice while freeing the lock
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

        private bool disableActivateRefreshUntilNextVerse = true;   // require us to wait to do activate refresh until we get the converters situated

        private async void BackTranslationHelperForm_Activated(object sender, EventArgs e)
        {
            _isNotInFocus = false;

            // Allow any pending UI events to process before starting the lengthy operation
            //  (possibly including allowing backTranslationHelperCtrl.IsModified and the
            //  disableActivateRefreshUntilNextVerse flag getting set), either of which 
            //  would block calling GetNewReference--e.g. if the user clicks the 'Close' button)
            //  Otherwise, we can get some bogus MouseDown/Up events that cause the editable 
            //  textbox to annoyingly be in 'select text/drag' mode.
            await Task.Delay(100);

            System.Diagnostics.Debug.WriteLine($"PtxBTH: BackTranslationHelperForm_Activated: _isNotInFocus = '{_isNotInFocus}', IsModified: {backTranslationHelperCtrl.IsModified}, _verseReference: {_verseReference}");

            // unless we were editing it, and then force it to stay the same
            // UPDATE: if the user made changes in Paratext, activated the form by clicking
            //  save changes, then if we don't update to the changes they made, we'll end
            //  up writing the old version before their changes... so we really do want to 
            //  call GetNewReference below even if it's modified, and we solve this by 
            //  returning nothing from CurrentTargetData if it's modified, causing the ctrl's
            //  UpdateData to just use what's in the edit box instead.
            //if (backTranslationHelperCtrl.IsModified)
            //    return;

            // we have this issue that if we launch a dialog (and thus become inactive), when that 
            //  dialog goes away, it looks like we need to refresh... But this can get us into near
            //  infinite loops, so have a way to disable this until we get to another verse
            if (!disableActivateRefreshUntilNextVerse)
            {
                GetNewReference(_verseReference);
            }
        }

        public void TranslatorSetChanged(List<IEncConverter> theTranslators)
        {
            // now that Initialize has been called, the collection of EncConverters is initialized
            // See if we need to add the 'translate to end of chapter' menu item to the Settings
            // menu (if we have an NLLB type converter)
            // if there are none (it means the user canceled the select converter dialog). In that case,
            //  disable activate refresh, so we don't get stuck in a loop
            disableActivateRefreshUntilNextVerse = !theTranslators.Any();
            if (!disableActivateRefreshUntilNextVerse)
                CheckAndAddNextToAllMenu(theTranslators);

            // also, if any of them are PtxProjectEncConverters, then set their project interfaces
            var ptxProjectConverterInput = CreateParatextProjectConverterInput();
            var projects = _host.GetAllProjects();
            foreach (PtxProjectEncConverter ptxProjectEncConverter in theTranslators.Where(t => t is PtxProjectEncConverter))
            {
                var projectShortName = ptxProjectEncConverter.ConverterIdentifier;
                var project = projects.FirstOrDefault(p => p.ShortName == projectShortName);
                if (project == null)
                    continue;

                ptxProjectEncConverter.ParatextProject = project;

                if (_model == null)
                    continue;

                InitializeModelForPtxProjectConverters(_model, ptxProjectConverterInput, ptxProjectEncConverter,
                                                       theTranslators.IndexOf(ptxProjectEncConverter));
            }
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
