using BackTranslationHelper;
using ClipboardEC.Properties;
using ECInterfaces;
using SilEncConverters40;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipboardEC
{
    public partial class TranslationHelperForm : Form, IBackTranslationHelperDataSource
    {
        protected BackTranslationHelperModel _model;
        protected Action<BackTranslationHelperModel> _updateDataProc;
        protected Action<string> _removeTranslatorSetFromClipboardEncConverter;
        protected static string _lastOrNextString;

        public TranslationHelperForm(string projectName, Action<string> removeTranslatorSetFromMap)
        {
            InitializeComponent();

            ProjectName = projectName;
            backTranslationHelperCtrl.BackTranslationHelperDataSource = this;
            _removeTranslatorSetFromClipboardEncConverter = removeTranslatorSetFromMap;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            GetNewClipboardData();
        }

        public void GetNewClipboardData()
        {
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
                var error = EncConverters.LogExceptionMessage("GetNewClipboardData", ex);
                MessageBox.Show(error);
            }
            finally
            {
                Cursor = cursor;
            }
        }

        private void UpdateData(BackTranslationHelperModel model)
        {
            try
            {
                Text = ProjectName;
                _model = model;
                _model.IsTargetTranslationEditable = true;
                backTranslationHelperCtrl.Initialize(_model);
                _updateDataProc(_model);
            }
            catch
            {
                // this can happen if the user is closing the form while we're still processing the translation calls.
            }
        }

        public Font SourceLanguageFont => this.Font;

        public bool SourceLanguageRightToLeft => false;

        public Font TargetLanguageFont => this.Font;

        public bool TargetLanguageRightToLeft => false;

        public BackTranslationHelperModel Model
        {
            get
            {
                var sourceString = ReadFromClipboard(useLastOrNextIfNonNull: true);

                var model = new BackTranslationHelperModel
                {
                    SourceData = sourceString,
                    TargetsPossible = new List<TargetPossible>(),
                    DisplayExistingTargetTranslation = false,
                };

                return model;
            }
        }

        private static string ReadFromClipboard(bool useLastOrNextIfNonNull)
        {
            string sourceString;
            if (useLastOrNextIfNonNull && !String.IsNullOrEmpty(_lastOrNextString))
            {
                sourceString = _lastOrNextString;
            }
            else
            {
                var iData = Clipboard.GetDataObject();
                sourceString = (iData.GetDataPresent(DataFormats.UnicodeText)) ? (string)iData.GetData(DataFormats.UnicodeText) : "No Unicode data on the clipboard";
            }
            return sourceString;
        }

        public string ProjectName { get; set; }

        public void ActivateKeyboard()
        {
            // TODO
        }

        public void ButtonPressed(ButtonPressed button)
        {
            switch (button)
            {
                case BackTranslationHelper.ButtonPressed.Skip:
                    DialogResult = DialogResult.No;
                    break;
                case BackTranslationHelper.ButtonPressed.MoveToNext:
                case BackTranslationHelper.ButtonPressed.WriteToTarget:
                    DialogResult = DialogResult.OK;
                    break;
                case BackTranslationHelper.ButtonPressed.Cancel:
                    DialogResult = DialogResult.Cancel;
                    break;
            }
        }

        public void Cancel()
        {
            Close();
        }

        public void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }

        public void MoveToNext()
        {
            GetNewClipboardData();
        }

        public void SetDataUpdateProc(Action<BackTranslationHelperModel> updateControls)
        {
            _updateDataProc = updateControls;
        }

        public bool WriteToTarget(string text)
        {
            if (text != null)
            {
                _lastOrNextString = ReadFromClipboard(useLastOrNextIfNonNull: false);
                Clipboard.SetDataObject(text);
            }
            return true;
        }

        public void TranslatorSetChanged(List<IEncConverter> theTranslators)
        {
        }

        private void TranslationHelperForm_Load(object sender, EventArgs e)
        {
            Location = Settings.Default.WindowLocation;
            WindowState = Settings.Default.DefaultWindowState;
            if (MinimumSize.Height <= Settings.Default.WindowSize.Height &&
                MinimumSize.Width <= Settings.Default.WindowSize.Width)
            {
                Size = Settings.Default.WindowSize;
            }
        }

        private void TranslationHelperForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _removeTranslatorSetFromClipboardEncConverter(ProjectName);

            if (this.WindowState == FormWindowState.Minimized)
                return; // don't save location info if the dialog is minimized

            Properties.Settings.Default.DefaultWindowState = WindowState;
            Properties.Settings.Default.WindowLocation = Location;
            Properties.Settings.Default.WindowSize = Size;
            Properties.Settings.Default.Save();
        }
    }
}
