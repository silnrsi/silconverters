using BackTranslationHelper;
using ECInterfaces;
using SilConvertersXML.Properties;
using SilEncConverters40;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SilConvertersXML
{
    public partial class TranslationHelperForm : Form, IBackTranslationHelperDataSource
    {
        protected BackTranslationHelperModel _model;
        protected Action<BackTranslationHelperModel> _updateDataProc;
        protected Font _font = null;
        protected DirectableEncConverter _theEc = null;

        public TranslationHelperForm()
        {
            InitializeComponent();
        }

        private string _translatedOutput = null;
        public string TranslatedOutput
        {
            get
            {
                return _translatedOutput ?? backTranslationHelperCtrl.GetNewTargetTexts().FirstOrDefault().TargetData;
            }

            private set
            {
                _translatedOutput = value;
            }
        }

        public DialogResult Show(DirectableEncConverter theEc, Font font, string sourceString)
        {
            var cursor = Cursor;
            try
            {
                Cursor = Cursors.WaitCursor;
                _font = font;
                _theEc = theEc;
                _model = new BackTranslationHelperModel
                {
                    SourceData = sourceString,
                    TargetData = null,
                    TargetDataPreExisting = null,  // we don't have an original version of the target (unless someday we allow side-by-side processing of 2 word docs)

                    // the control we're sending this to may have other EncConverters associated w/ this font, but we only have the one. So add it
                    //  here and when it gets initialized below, it may add other conversions done at that time
                    TargetsPossible = new List<TargetPossible>(),
                    DisplayExistingTargetTranslation = false,
                    IsTargetTranslationEditable = true,
                };

                // this form is the implementation of the way to get data
                backTranslationHelperCtrl.BackTranslationHelperDataSource = this;
                if (!backTranslationHelperCtrl.TheTranslators.Any(t => t.Name == theEc.GetEncConverter.Name))
                    backTranslationHelperCtrl.TheTranslators.Add(theEc.GetEncConverter);

                backTranslationHelperCtrl.Initialize(_model);
                backTranslationHelperCtrl.GetNewData(ref _model);
                _updateDataProc(_model);

                // get some info to show in the title bar
                this.Text = String.Format("{0}: {1}", XMLViewForm.cstrCaption, theEc?.ToString());
            
                Cursor = cursor;

                return ShowDialog();
            }
            catch (Exception ex)
            {
                Cursor = cursor;
                var error = ex.Message;
                while (ex.InnerException != null)
                {
                    error += Environment.NewLine + Environment.NewLine + ex.InnerException.Message;
                    ex = ex.InnerException;
                }

                MessageBox.Show(error, XMLViewForm.cstrCaption);
            }

            return DialogResult.None;
        }

        public Font SourceLanguageFont => _font;

        public bool SourceLanguageRightToLeft => false;

        public Font TargetLanguageFont => _font;

        public bool TargetLanguageRightToLeft => false;

        public BackTranslationHelperModel Model => _model;

        public string ProjectName => _theEc.Name;

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
            Close();
        }

        public void SetDataUpdateProc(Action<BackTranslationHelperModel> updateControls)
        {
            _updateDataProc = updateControls;
        }

        public bool WriteToTarget(string text)
        {
            TranslatedOutput = text;
            DialogResult = DialogResult.OK;
            Close();
            return true;
        }

        public void TranslatorSetChanged(List<IEncConverter> theTranslators)
        {
            if (!theTranslators.Any(t => t.Name == _theEc.Name))
            {
                // We no longer have the original encConverter that the Add-in knows about... 
                DialogResult = DialogResult.Cancel;
                Close();
            }
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
            Properties.Settings.Default.DefaultWindowState = WindowState;
            Properties.Settings.Default.WindowLocation = Location;
            Properties.Settings.Default.WindowSize = Size;
            Properties.Settings.Default.Save();
        }
    }
}
