using BackTranslationHelper;
using ECInterfaces;
using SilEncConverters40;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SILConvertersOffice
{
    internal partial class TranslationHelperForm : Form, IBaseConverterForm, IBackTranslationHelperDataSource
    {
        protected FontConverter _theFontsAndEncConverter;
        protected BackTranslationHelperModel _model;
        protected Action<BackTranslationHelperModel> _updateDataProc;
        protected Font _targetFont;

        public TranslationHelperForm()
        {
            InitializeComponent();
        }

        bool IBaseConverterForm.SkipIdenticalValues => false;   // this was a feature for checking round-tripping, which doesn't apply here

        string IBaseConverterForm.ForwardString
        { 
            get
            {
                // TODO: fix this
                return backTranslationHelperCtrl.NewTargetTexts.FirstOrDefault()?.TargetData;
            }

            set
            {
                System.Diagnostics.Debug.Assert(_model != null);
                _model.TargetDataEditable = value;
                if (!_model.TargetsPossible.Any())
                    _model.TargetsPossible.Add(new TargetPossible { TargetData = value, FillButtonEnabled = true, PossibleIndex = 0, TranslatorName = _theFontsAndEncConverter.DirectableEncConverter.Name });
                // TODO: fix this
                _updateDataProc(_model);
            }
        }

        FormButtons IBaseConverterForm.Show(FontConverter fontConverter, string sourceText, string targetText)
        {
            ButtonPressed = FormButtons.Cancel; // reset and be pessimistic

            _theFontsAndEncConverter = fontConverter;
            _targetFont = fontConverter.RhsFont ?? fontConverter.Font;

            _model = new BackTranslationHelperModel
            {
                SourceData = sourceText,
                TargetDataEditable = targetText,
                TargetsPossible = new List<TargetPossible> 
                { 
                    new TargetPossible { TargetData = targetText, FillButtonEnabled = true, PossibleIndex = 0, TranslatorName = _theFontsAndEncConverter.DirectableEncConverter.Name } 
                },
            };

            // this form is the implementation of the way to get get data
            backTranslationHelperCtrl.BackTranslationHelperDataSource = this;
            backTranslationHelperCtrl.TheTranslators.Add(_theFontsAndEncConverter.DirectableEncConverter.GetEncConverter);
            backTranslationHelperCtrl.Initialize(displayExistingTargetTranslation: false);
            
            // TODO: fix this
            _updateDataProc(_model);

            // get some info to show in the title bar
            this.Text = String.Format("{0}: {1}", OfficeApp.cstrCaption, _theFontsAndEncConverter?.ToString());

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

        public void WriteToTarget(string text)
        {
            ButtonPressed = FormButtons.ReplaceEvery;
            Close();
        }

        public void SetDataUpdateProc(Action<BackTranslationHelperModel> updateDataProc)
        {
            _updateDataProc = updateDataProc;
        }

        public void MoveToNext()
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

        public void Cancel()
        {
            ButtonPressed = FormButtons.Cancel;
        }

        private void TranslationHelperForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // only allow Cancel or ReplaceEvery
            if ((ButtonPressed != FormButtons.ReplaceEvery) && (ButtonPressed != FormButtons.Cancel))
                e.Cancel = true;
        }

        void IBackTranslationHelperDataSource.ButtonPressed(ButtonPressed button)
        {
            if (Enum.TryParse(button.ToString(), out FormButtons result))
                ButtonPressed = result;
        }
    }
}
