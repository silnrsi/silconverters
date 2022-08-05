using BackTranslationHelper;
using ECInterfaces;
using Paratext.PluginInterfaces;
using SIL.Windows.Forms.FileDialogExtender;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public partial class BackTranslationHelperForm : Form, IBackTranslationHelperDataSource, IMessageFilter
    {
        private SplashScreenForm splashScreen;
        private IProject _projectNameSource;
        private IProject _projectNameTarget;
        private IProjectLanguage _languageSource;
        private IProjectLanguage _languageTarget;
        private IKeyboard _keyboardTarget;
        private IVerseRef _verseReference;
        private List<IUSFMToken> _usfmTokensSource;
        private IPluginHost _host;
        private ParatextBackTranslationHelperPlugin _plugin;
        private Action<BackTranslationHelperModel> _updateControls;
        private BackTranslationHelperModel _model;
        private IWriteLock _writeLock = null;

        public BackTranslationHelperForm(IPluginHost host, ParatextBackTranslationHelperPlugin plugin, SplashScreenForm splashScreen, IProject projectNameParent, IProject projectNameDaughter, 
            IProjectLanguage languageSource, IProjectLanguage languageTarget, IEncConverter theEc)
        {
            _host = host;
            _plugin = plugin;
            _host.VerseRefChanged += _host_VerseRefChanged;
            InitializeComponent();

            // this form is the implementation of the way to get get data
            backTranslationHelperCtrl.BackTranslationHelperDataSource = this;
            backTranslationHelperCtrl.TheTranslators.Add(theEc);

            Text = String.Format(Text, projectNameParent, projectNameDaughter);

            this.splashScreen = splashScreen;
            _projectNameSource = projectNameParent;
            _projectNameTarget = projectNameDaughter;
            _languageSource = languageSource;
            _languageTarget = languageTarget;
            _keyboardTarget = _projectNameTarget.VernacularKeyboard;
            Location = Properties.Settings.Default.WindowLocation;
            WindowState = Properties.Settings.Default.DefaultWindowState;
            if (MinimumSize.Height <= Properties.Settings.Default.WindowSize.Height &&
                MinimumSize.Width <= Properties.Settings.Default.WindowSize.Width)
            {
                Size = Properties.Settings.Default.WindowSize;
            }

            splashScreen.Close();
            backTranslationHelperCtrl.Focus();
        }

        private void _host_VerseRefChanged(IPluginHost sender, IVerseRef newReference, SyncReferenceGroup group)
        {
            GetNewReference(newReference);
        }

        private void GetNewReference(IVerseRef newReference)
        {
            Unlock();
            _verseReference = newReference;
            BackTranslationHelperModel model = null;    // means query the interface to get the data
            backTranslationHelperCtrl.GetNewData(ref model);
            ParatextBackTranslationHelperPlugin.InvokeOnMainWindowIfNotNull(() => UpdateData(model));
        }

        private void UpdateData(BackTranslationHelperModel model)
        {
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
                    TargetDataExisting = CurrentTargetData,
                    TargetsPossible = new List<TargetPossible>()
                };
                return model;
            }
        }

        private string _lastSourceString;
        private string CurrentSourceData
        {
            get
            {
                // get them all, bkz we have to write them all
                _usfmTokensSource = _projectNameSource.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum).ToList();
                var data = _usfmTokensSource.OfType<IUSFMTextToken>().Where(t => t.IsPublishableVernacular && IsMatchingVerse(t.VerseRef, _verseReference)).ToList();
                var textValues = data.Select(t => t.Text);
                _lastSourceString = string.Join(Environment.NewLine, textValues);
                return _lastSourceString;
            }
        }

        private string CurrentTargetData
        {
            get
            {
                _writeLock = _projectNameTarget.RequestWriteLock(_plugin, ReleaseRequested, _verseReference.BookNum, _verseReference.ChapterNum);
                if (_writeLock == null)
                {
                    // if this fails to return something, it means we can't edit it
                }
                var data = _projectNameTarget.GetUSFMTokens(_verseReference.BookNum, _verseReference.ChapterNum).OfType<IUSFMTextToken>().ToList();
                var publishable = data.Where(t => t.IsPublishableVernacular && IsMatchingVerse(t.VerseRef, _verseReference));
                var values = publishable.Select(t => t.Text);
                var verseData = string.Join(Environment.NewLine, values);
                return verseData;
            }
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
            var translatedValues = text?.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => !String.IsNullOrEmpty(s?.Trim()))
                                        .ToList();
            if (!translatedValues.Any())    // nothing to do
                return;

            // go thru all the ones we had and put the translated text into the text ones and transfer the non-text ones in order into the list to Put
            var i = 0;
            TextToken newToken = null;
            var usfmTokensTarget = new List<IUSFMToken>();
            foreach (var token in _usfmTokensSource)
            {
                IUSFMToken updatedToken = token;
                if ((token is IUSFMTextToken textToken) && textToken.IsPublishableVernacular && (textToken.VerseRef.ToString() == _verseReference.ToString()))
                {
                    if (i >= translatedValues.Count)
                        continue;

                    newToken = new TextToken(textToken)
                    {
                        Text = translatedValues[i++]
                    };
                    updatedToken = newToken;
                }
                usfmTokensTarget.Add(updatedToken);
            }

            // if there are still some we haven't processed, then put them in copies of the last one we did
            System.Diagnostics.Debug.Assert(newToken != null, "This should be able to happen, bkz we should have at least 1");
            while (translatedValues.Count < i)
            {
                var newTextToken = new TextToken(newToken)
                {
                    Text = translatedValues[i++]
                };
                usfmTokensTarget.Add(newTextToken);
            }

            try
            {
                if (_writeLock == null) // it shouldn't be, but if it is, this should warn the user that it isn't going to work
                    _writeLock = _projectNameTarget.RequestWriteLock(_plugin, ReleaseRequested, _verseReference.BookNum, _verseReference.ChapterNum);

                _projectNameTarget.PutUSFMTokens(_writeLock, usfmTokensTarget, _verseReference.BookNum);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception caught:\n{ex.Message}");
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // On Windows XP, TXL comes up underneath Paratext. See if this fixes it:
            TopMost = true;
            TopMost = false;
            Application.AddMessageFilter(this);

            var activeWindowState = _host.ActiveWindowState;
            _verseReference = activeWindowState.VerseRef;
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
        }

        private void Unlock()
        {
            if (_writeLock != null)
            {
                IWriteLock temp = _writeLock;
                temp.Dispose();
                _writeLock = null;
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            /*
            if (m.Msg == (int)Msg.WM_LBUTTONDOWN &&
                IsPointInSelectedTextInTranslationEditingControl(MousePosition))
            {
                // We support both move and copy, but we don't need to handle the result because
                // we really only support move if the text is moved to a new location within the
                // TextControl, in which case the "destination" code handles the entire operation,
                // including removing the existing selected text.
                TextControl.DoDragDrop(TextControl.SelectedText, DragDropEffects.Copy | DragDropEffects.Move);
                return true;
            }
            */
            return false;
        }
#if false
        /// ------------------------------------------------------------------------------------
        /// <summary>
        /// Determines whether the given point is contained within the selected region of text
        /// in the text control that is currently being used to edit the Translation.
        /// </summary>
        /// <param name="position">Point (typically a mouse position) relative to screen</param>
        /// ------------------------------------------------------------------------------------
        private bool IsPointInSelectedTextInTranslationEditingControl(Point position)
        {
            if (!EditingTranslation || TextControl.SelectedText.Length == 0)
                return false;

            var topLeft = TextControl.PointToScreen(new Point(TextControl.ClientRectangle.Top, TextControl.ClientRectangle.Left));
            var bottom = TextControl.PointToScreen(new Point(0, TextControl.ClientRectangle.Bottom)).Y;
            if (position.Y >= topLeft.Y && position.Y <= bottom)
            {
                var dxStartOfSelection = TextControl.PointToScreen(new Point(
                    (TextControl.SelectionStart == 0 ? TextControl.ClientRectangle.Left :
                    TextControl.GetPositionFromCharIndex(TextControl.SelectionStart - 1).X), 0)).X;
                var limSelection = TextControl.SelectionStart + TextControl.SelectionLength;
                var dxEndOfSelection = TextControl.PointToScreen(new Point(TextControl.Location.X +
                    (limSelection == TextControl.Text.Length ? TextControl.ClientSize.Width :
                    TextControl.GetPositionFromCharIndex(limSelection).X), 0)).X;

                if (position.X >= dxStartOfSelection && position.X <= dxEndOfSelection)
                    return true;
            }
            return false;
        }

        /// ------------------------------------------------------------------------------------
        /// <summary>
        /// Handles DragOver and DragEnter events
        /// </summary>
        /// ------------------------------------------------------------------------------------
        private void TextControl_Drag(object sender, DragEventArgs e)
        {
            if ((e.AllowedEffect & (DragDropEffects.Move)) > 0 &&
                (e.Data.GetDataPresent(DataFormats.StringFormat, false)))
            {
                // For now, we can safely assume that any "string" that
                // allows move is originating in the same TextControl because any
                // text from an outside app will not come in as a StringFormat
                // object and no other control in Transcelerator supports
                // moving string data.
                e.Effect = DragDropEffects.Move;
            }
            else if ((e.AllowedEffect & (DragDropEffects.Copy)) > 0 &&
                (e.Data.GetDataPresent(DataFormats.StringFormat, false) ||
                e.Data.GetDataPresent(DataFormats.UnicodeText, false) ||
                e.Data.GetDataPresent(DataFormats.Text, false)))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        /// ------------------------------------------------------------------------------------
        /// <summary>
        /// Handles GiveFeedback event to show the user where the text will be dropped.
        /// </summary>
        /// ------------------------------------------------------------------------------------
        void TextControl_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (!m_fEnableDragDrop && e.Effect == DragDropEffects.Move)
            {
                e.UseDefaultCursors = false;
            }
            //var ichInsert = TextControl.GetCharIndexFromPosition(TextControl.PointToClient(MousePosition));

            //if (e.Effect == DragDropEffects.Move && TextControl.SelectionLength > 0 &&
            //    ichInsert >= TextControl.SelectionStart &&
            //    ichInsert <= TextControl.SelectionStart + TextControl.SelectionLength)
            //{
            //    // Dropping selected text onto itself just removes the selection.
            //    e.UseDefaultCursors = false; // TODO: This doesn't do anything!
            //    return;
            //}
            //e.UseDefaultCursors = true;
            //// TODO: Need to return early (just do default behavior) if not over the TextControl
            //// TODO: Draw special insertion point to show where dropped text would go.
            //Debug.WriteLine("Insert position: " + ichInsert);
        }

        /// ------------------------------------------------------------------------------------
        /// <summary>
        /// Handles DragDrop event to complete the copy or move.
        /// </summary>
        /// ------------------------------------------------------------------------------------
        private void TextControl_DragDrop(object sender, DragEventArgs e)
        {
            var ichInsert = TextControl.GetCharIndexFromPosition(TextControl.PointToClient(new Point(e.X, e.Y)));

            if (!m_fEnableDragDrop)
            {
                TextControl.SelectionStart = ichInsert;
                TextControl.SelectionLength = 0;
                return;
            }

            bool fMove = e.Effect == DragDropEffects.Move;

            if ((fMove || e.Effect == DragDropEffects.Copy) &&
                (e.Data.GetDataPresent(DataFormats.StringFormat, false) ||
                e.Data.GetDataPresent(DataFormats.UnicodeText, false) ||
                e.Data.GetDataPresent(DataFormats.Text, false)))
            {
                string text = ((string)e.Data.GetData(DataFormats.StringFormat));

                if (fMove && TextControl.SelectionLength > 0 &&
                    ichInsert >= TextControl.SelectionStart &&
                    ichInsert <= TextControl.SelectionStart + TextControl.SelectionLength)
                {
                    // Don't try to move selected text onto itself. Instead just remove selection.
                    // This allows a simple click to behave properly.
                    TextControl.SelectionStart = ichInsert;
                    TextControl.SelectionLength = 0;
                    return;
                }

                if (text.Length > 0)
                {
                    var textBefore = TextControl.Text.Substring(0, ichInsert);
                    var textAfter = TextControl.Text.Substring(ichInsert);
                    var removeStart = TextControl.SelectionStart;
                    var removeLen = TextControl.SelectionLength;
                    if (ichInsert <= removeStart)
                        removeStart += text.Length;
                    else // post-adjust the insert location 
                        ichInsert -= text.Length;
                    TextControl.Text = textBefore + text + textAfter;
                    if (removeLen > 0)
                    {
                        // We need to handle removal of originally selected text because
                        // the code where the drag-drop originates assumes we will (it
                        // treats any copy/move as a copy because we don't want dragging
                        // from TXL to Word, for example, to result in a move.  
                        TextControl.Text = TextControl.Text.Remove(removeStart, removeLen);
                        // Now select the moved text
                        TextControl.SelectionStart = ichInsert;
                        TextControl.SelectionLength = text.Length;
                    }
                }
            }
        }
#endif
    }

    class TextToken : IUSFMTextToken
    {
        public TextToken(IUSFMTextToken token)
        {
            Text = token.Text;
            VerseRef = token.VerseRef;
            VerseOffset = token.VerseOffset;
            IsSpecial = token.IsSpecial;
            IsFigure = token.IsFigure;
            IsFootnoteOrCrossReference = token.IsFootnoteOrCrossReference;
            IsScripture = token.IsScripture;
            IsMetadata = token.IsMetadata;
            IsPublishableVernacular = token.IsPublishableVernacular;
        }

        public string Text { get; set; }

        public IVerseRef VerseRef { get; set; }

        public int VerseOffset { get; set; }

        public bool IsSpecial { get; set; }

        public bool IsFigure { get; set; }

        public bool IsFootnoteOrCrossReference { get; set; }

        public bool IsScripture { get; set; }

        public bool IsMetadata { get; set; }

        public bool IsPublishableVernacular { get; set; }
    }
}
