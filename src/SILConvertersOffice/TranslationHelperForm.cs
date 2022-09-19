﻿using BackTranslationHelper;
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
                _model.TargetData = value;
                if (!_model.TargetsPossible.Any())
                    _model.TargetsPossible.Add(new TargetPossible { TargetData = value, PossibleIndex = 0, TranslatorName = _theFontsAndEncConverter.DirectableEncConverter.Name });
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
                TargetData = targetText,
                TargetDataPreExisting = false,  // we don't have an original version of the target (unless someday we allow side-by-side processing of 2 word docs)

                // the control we're sending this to may have other EncConverters associated w/ this font, but we only have the one. So add it
                //  here and when it gets initialized below, it may add other conversions done at that time
                TargetsPossible = new List<TargetPossible> 
                { 
                    new TargetPossible { TargetData = targetText, PossibleIndex = 0, TranslatorName = _theFontsAndEncConverter.DirectableEncConverter.Name } 
                },
            };

            // this form is the implementation of the way to get get data
            backTranslationHelperCtrl.BackTranslationHelperDataSource = this;
            if (!backTranslationHelperCtrl.TheTranslators.Any(t => t.Name == _theFontsAndEncConverter.DirectableEncConverter.GetEncConverter.Name))
                backTranslationHelperCtrl.TheTranslators.Add(_theFontsAndEncConverter.DirectableEncConverter.GetEncConverter);

            backTranslationHelperCtrl.Initialize(displayExistingTargetTranslation: false);

            // TODO: fix this
            backTranslationHelperCtrl.GetNewData(ref _model);
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
            if ((ButtonPressed != FormButtons.ReplaceEvery) &&
                (ButtonPressed != FormButtons.ReplaceOnce) &&
                (ButtonPressed != FormButtons.Cancel) && 
                (ButtonPressed != FormButtons.Next))
                e.Cancel = true;
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
                        ButtonPressed = FormButtons.Next;
                        break;
                    case "WriteToTarget":
                        ButtonPressed = FormButtons.ReplaceOnce;
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