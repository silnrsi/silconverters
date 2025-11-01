using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ECInterfaces;
using SilEncConverters40;
using SpellingFixer30;

namespace SILConvertersOffice
{
    internal partial class SILConverterProcessorForm : BaseConverterForm, IBaseConverterForm
    {
        protected CscProject m_cscProject = null;

        public SILConverterProcessorForm()
        {
            InitializeComponent();
        }

        private void buttonDebug_Click(object sender, EventArgs e)
        {
            DirectableEncConverter aEC = m_aFontPlusEC?.DirectableEncConverter;
            if (aEC != null)
            {
                IEncConverter aIEC = aEC.GetEncConverter;
                if (aIEC != null)
                {
                    bool bOrigValue = aIEC.Debug;
                    aIEC.Debug = true;

                    RefreshTextBoxes(aEC);

                    aIEC.Debug = bOrigValue;
                }
            }
        }

        protected void buttonRefresh_Click(object sender, EventArgs e)
        {
            RefreshTextBoxes(m_aFontPlusEC.DirectableEncConverter);
        }

        private void buttonViewRule_Click(object sender, EventArgs e)
        {
            // see if this is a spellfixer-based converter
            m_cscProject ??= TrySelectProject();
            m_cscProject?.FindReplacementRule(textBoxInput.Text);
            RefreshTextBoxes(m_aFontPlusEC.DirectableEncConverter);
        }

        protected CscProject TrySelectProject()
        {
            try
            {
                return CscProject.SelectProject();
            }
            catch (Exception ex)
            {
            }
            return null;
        }
    }

    internal class SpellingFixerWordProcessor : OfficeDocumentProcessor
    {
        public SpellingFixerWordProcessor(FontConverters aFCs)
            : base(aFCs, new SILConverterProcessorForm())
        {
        }

        protected override FormButtons ConvertProcessing(OfficeRange aWordRange, FontConverter aThisFC, string strInput, ref string strReplace)
        {
            string strOutput = aThisFC.DirectableEncConverter.Convert(strInput);

            SILConverterProcessorForm form = (SILConverterProcessorForm)Form;
            FormButtons res = FormButtons.None;
            if (!form.SkipIdenticalValues || (strInput != strOutput))
            {
                if (ReplaceAll)
                {
                    strReplace = strOutput;
                    res = FormButtons.ReplaceAll;
                }
                else
                {
                    res = form.Show(aThisFC, strInput, strOutput);

                    // just in case it's Replace or ReplaceAll, our replacement string is the 'RoundTripString'
                    strReplace = form.ForwardString;
                }
            }

            return res;
        }

        public override void SetRangeFont(OfficeRange aWordRange, string strFontName)
        {
            // this sub-class doesn't ever change the font
            // base.SetRangeFont(aWordRange, strFontName);
        }

        protected override FontConverter QueryForFontConvert(string strFontName)
        {
            return new FontConverter(strFontName);
        }
    }
}

