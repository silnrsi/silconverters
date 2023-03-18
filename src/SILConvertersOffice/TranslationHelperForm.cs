using BackTranslationHelper;
using ECInterfaces;
using SilEncConverters40;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
#if BUILD_FOR_OFF11
using SILConvertersOffice.Properties;
#elif BUILD_FOR_OFF12
using SILConvertersOffice07.Properties;
#elif BUILD_FOR_OFF14
using SILConvertersOffice10.Properties;
#elif BUILD_FOR_OFF15
using SILConvertersOffice13.Properties;
#endif

namespace SILConvertersOffice
{
    /// <summary>
    /// each project (read: font and converter combination) needs its own TranslationHelperForm
    /// so it can manage things like its own SpellFixer and set of EncConverters to use. So use 
    /// this class (w/ an 's') to triangulate between the different instances of it based on the 
    /// requested FontConverter
    /// </summary>
    internal class TranslationHelperForms : IBaseConverterForm
    {
        private static TranslationHelperForm _current;
        private Dictionary<FontConverter, TranslationHelperForm> Forms = new Dictionary<FontConverter, TranslationHelperForm>();

        bool IBaseConverterForm.SkipIdenticalValues => false;   // this was a feature for checking round-tripping, which doesn't apply here

        string IBaseConverterForm.ForwardString
        {
            get
            {
                return _current?.ForwardString;
            }

            set
            {
                _current.ForwardString = value;
            }
        }

        FormButtons IBaseConverterForm.Show(FontConverter aThisFC, string strInput, string strOutput)
        {
            if (!Forms.TryGetValue(aThisFC, out var form))
            {
                form = new TranslationHelperForm();
            }

            _current = form;

            return _current.Show(aThisFC, strInput, strOutput);
        }
    }

    internal partial class TranslationHelperForm : Form, IBackTranslationHelperDataSource
    {
        protected FontConverter _theFontsAndEncConverter;
        protected BackTranslationHelperModel _model;
        protected Action<BackTranslationHelperModel> _updateDataProc;
        protected Font _targetFont;

        public TranslationHelperForm()
        {
            InitializeComponent();
        }

        public string ForwardString
        { 
            get
            {
                // TODO: fix this
                return backTranslationHelperCtrl.GetNewTargetTexts().FirstOrDefault()?.TargetData;
            }

            set
            {
                System.Diagnostics.Debug.Assert(_model != null);
                _model.TargetData = value;
                if (!_model.TargetsPossible.Any())
                    _model.TargetsPossible.Add(new TargetPossible { TargetData = value, PossibleIndex = 0, TranslatorName = _theFontsAndEncConverter.DirectableEncConverter.Name });
                // TODO: fix this
                _updateDataProc(_model);
            }
        }

        public FormButtons Show(FontConverter fontConverter, string sourceText, string targetText)
        {
            WordApp.SetCursorToWaiting();
            ButtonPressed = FormButtons.Cancel; // reset and be pessimistic

            _theFontsAndEncConverter = fontConverter;
            _targetFont = fontConverter.RhsFont ?? fontConverter.Font;

            _model = new BackTranslationHelperModel
            {
                SourceData = sourceText,
                TargetData = targetText,
                TargetDataPreExisting = null,  // we don't have an original version of the target (unless someday we allow side-by-side processing of 2 word docs)

                // the control we're sending this to may have other EncConverters associated w/ this font, but we only have the one. So add it
                //  here and when it gets initialized below, it may add other conversions done at that time
                TargetsPossible = new List<TargetPossible> 
                { 
                    new TargetPossible { TargetData = targetText, PossibleIndex = 0, TranslatorName = _theFontsAndEncConverter.DirectableEncConverter.Name } 
                },
            };

            // this form is the implementation of the way to get data
            backTranslationHelperCtrl.BackTranslationHelperDataSource = this;
            if (!backTranslationHelperCtrl.TheTranslators.Any(t => t.Name == _theFontsAndEncConverter.DirectableEncConverter.GetEncConverter.Name))
                backTranslationHelperCtrl.TheTranslators.Add(_theFontsAndEncConverter.DirectableEncConverter.GetEncConverter);

            backTranslationHelperCtrl.Initialize(displayExistingTargetTranslation: false);

            // TODO: fix this
            backTranslationHelperCtrl.GetNewData(ref _model);
            _updateDataProc(_model);

            // get some info to show in the title bar
            this.Text = String.Format("{0}: {1}", OfficeApp.cstrCaption, _theFontsAndEncConverter?.ToString());

            WordApp.SetCursorToDefault();
            var res = ShowDialog();

            return ButtonPressed;
        }

        public void ActivateKeyboard()
        { 
            // ToDo
        }


        public void Log(string message)
        {
            // TODO: 
        }

        public bool WriteToTarget(string text)
        {
            ButtonPressed = FormButtons.ReplaceEvery;
            Close();
            return true;
        }

        public void SetDataUpdateProc(Action<BackTranslationHelperModel> updateDataProc)
        {
            _updateDataProc = updateDataProc;
        }

        void IBackTranslationHelperDataSource.MoveToNext()
        {
            Close();
        }
 
        protected FormButtons m_btnPressed = FormButtons.None;
        public FormButtons ButtonPressed
        {
            get { return m_btnPressed; }
            set { m_btnPressed = value; }
        }

        Font IBackTranslationHelperDataSource.SourceLanguageFont
        {
            get
            {
                return _theFontsAndEncConverter?.Font;
            }
        }

        Font IBackTranslationHelperDataSource.TargetLanguageFont
        {
            get
            {
                return _targetFont;
            }
        }

        BackTranslationHelperModel IBackTranslationHelperDataSource.Model
        {
            get { return _model; }
        }

        string IBackTranslationHelperDataSource.ProjectName
        {
            get
            {
                return _theFontsAndEncConverter?.ToString();
            }
        }

        bool IBackTranslationHelperDataSource.SourceLanguageRightToLeft
        {
            get
            {
                return WordApp.SelectionIsRightAligned;
            }
        }

        bool IBackTranslationHelperDataSource.TargetLanguageRightToLeft
        {
            get
            {
                return false;
            }
        }

        void IBackTranslationHelperDataSource.Cancel()
        {
            ButtonPressed = FormButtons.Cancel;
            Close();
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
            // only allow Cancel or ReplaceEvery
            if ((ButtonPressed != FormButtons.ReplaceEvery) &&
                (ButtonPressed != FormButtons.ReplaceOnce) &&
                (ButtonPressed != FormButtons.Cancel) && 
                (ButtonPressed != FormButtons.Next))
                e.Cancel = true;
        
            Properties.Settings.Default.DefaultWindowState = WindowState;
            Properties.Settings.Default.WindowLocation = Location;
            Properties.Settings.Default.WindowSize = Size;
            Properties.Settings.Default.Save();
        }

        void IBackTranslationHelperDataSource.ButtonPressed(ButtonPressed button)
        {
            if (Enum.TryParse(button.ToString(), out FormButtons result))
                ButtonPressed = result;
            else
            {
                switch(button.ToString())
                {
                    case "MoveToNext":
                    case "WriteToTarget":
                        ButtonPressed = FormButtons.ReplaceOnce;    // these both mean replace
                        break;
                    case "Cancel":
                        ButtonPressed = FormButtons.Cancel;
                        break;
                    case "Skip":
                        ButtonPressed = FormButtons.Next;
                        break;
                }
            }
        }
    }
}
