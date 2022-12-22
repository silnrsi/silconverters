using BackTranslationHelper;
using ECInterfaces;
using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public partial class BackTranslationHelperWebForm : Form, IBackTranslationHelperDataSource
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
        private IWriteLock _writeLock = null;

        public BackTranslationHelperWebForm(IPluginHost host, ParatextBackTranslationHelperPlugin plugin, SplashScreenForm splashScreen, IProject projectNameParent, IProject projectNameDaughter,
            IProjectLanguage languageSource, IProjectLanguage languageTarget, IEncConverter theEc)
        {
            _host = host;
            _plugin = plugin;
            _host.VerseRefChanged += _host_VerseRefChanged;
            InitializeComponent();

            // this form is the implementation of the way to get get data
            backTranslationHelperView.BackTranslationHelperDataSource = this;
            backTranslationHelperView.TheTranslators.Add(theEc);

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
        }
        private void _host_VerseRefChanged(IPluginHost sender, IVerseRef newReference, SyncReferenceGroup group)
        {
            Unlock();
            _verseReference = newReference;
            BackTranslationHelperModel model = null;
            backTranslationHelperView.GetNewData(ref model);
            ParatextBackTranslationHelperPlugin.InvokeOnMainWindowIfNotNull(() => UpdateData(model));
        }

        private void UpdateData(BackTranslationHelperModel model)
        {
            backTranslationHelperView.Initialize(true);
            _updateControls(model);
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
                var data = _usfmTokensSource.OfType<IUSFMTextToken>().Where(t => t.IsPublishableVernacular && (t.VerseRef?.ToString() == _verseReference?.ToString())).ToList();
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
                var publishable = data.Where(t => t.IsPublishableVernacular && (t.VerseRef?.ToString() == _verseReference?.ToString()));
                var values = publishable.Select(t => t.Text);
                var verseData = string.Join(Environment.NewLine, values);
                return verseData;
            }
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
                _projectNameTarget.PutUSFMTokens(_writeLock, usfmTokensTarget, _verseReference.BookNum);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception caught:\n{ex.Message}");
            }
        }

        private void BackTranslationHelperForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Unlock();

            // only allow Cancel or ReplaceEvery
            if ((e.CloseReason != CloseReason.UserClosing) &&
                ((_buttonPressed == ButtonPressed.WriteToTarget)
                || (_buttonPressed == ButtonPressed.MoveToNext)
                || (_buttonPressed == ButtonPressed.Copy)))
                e.Cancel = true;

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
    }
    /*
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
    */
}
