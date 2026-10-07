using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using SilEncConverters40;

namespace SIL.SpellFixerPluginForParatext
{
    // adapted from SILConvertersOffice's BaseConverterForm
    internal partial class SpellFixerQueryFormBase : Form
    {
        protected DirectableEncConverter m_aEC;

        public SpellFixerQueryFormBase()
        {
            InitializeComponent();

            // we only ever ask about words the SpellFixer would change (and the checkbox is private to this class)
            checkBoxSkipIdenticalForms.Visible = false;
        }

        public virtual FormButtons Show
            (
            IWin32Window owner,
            DirectableEncConverter aEC,
            Font font,
            bool rightToLeft,
            string strInput,
            string strOutput
            )
        {
            m_aEC = aEC;
            ButtonPressed = FormButtons.None;

            if (font != null)
                textBoxInput.Font = textBoxConverted.Font = font;

            textBoxInput.RightToLeft = textBoxConverted.RightToLeft = rightToLeft ? RightToLeft.Yes : RightToLeft.No;

            InputString = strInput;
            CorrectedSpelling = strOutput;

            UpdateLhsUniCodes(InputString, this.labelInputCodePoints);
            UpdateRhsUniCodes(CorrectedSpelling, this.labelForwardCodePoints);

            // get some info to show in the title bar
            this.Text = String.Format("{0}: {1}", SpellFixerPlugin.PluginName, m_aEC.ToString());

            ShowDialog(owner);

            return ButtonPressed;
        }

        public string InputString
        {
            get { return this.textBoxInput.Text; }
            set { this.textBoxInput.Text = value; }
        }

        public string CorrectedSpelling
        {
            get { return this.textBoxConverted.Text; }
            set { this.textBoxConverted.Text = value; }
        }

        protected FormButtons m_btnPressed = FormButtons.None;
        public FormButtons ButtonPressed
        {
            get { return m_btnPressed; }
            set { m_btnPressed = value; }
        }

        protected void UpdateLegacyCodes(string strInputString, int cp, Label lableUniCodes)
        {
            // to get the real byte values, we need to first convert it using the def code page
            byte[] aby = EncConverters.GetBytesFromEncoding(cp, strInputString, true);
            string strWhole = null;
            foreach (byte by in aby)
                strWhole += String.Format("{0:D3} ", (int)by);

            lableUniCodes.Text = strWhole;
        }

        protected void UpdateUniCodes(string strInputString, Label lableUniCodes)
        {
            string strWhole = null;
            foreach (char ch in strInputString)
                strWhole += String.Format("{0:X4} ", (int)ch);

            lableUniCodes.Text = strWhole;
        }

        protected void UpdateLhsUniCodes(string strInputString, Label lableUniCodes)
        {
            if (m_aEC == null)
                return;

            // use the EncConverter to determine if this field is Legacy or not.
            if (m_aEC.IsLhsLegacy)
                UpdateLegacyCodes(strInputString, m_aEC.GetEncConverter.CodePageInput, lableUniCodes);
            else
                UpdateUniCodes(strInputString, lableUniCodes);
        }

        protected void UpdateRhsUniCodes(string strInputString, Label lableUniCodes)
        {
            if (m_aEC == null)
                return;

            // use the EncConverter to determine if this field is Legacy or not.
            if (m_aEC.IsRhsLegacy)
                UpdateLegacyCodes(strInputString, m_aEC.GetEncConverter.CodePageOutput, lableUniCodes);
            else
                UpdateUniCodes(strInputString, lableUniCodes);
        }

        protected void buttonNextWord_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.Next;
            this.Close();
        }

        protected void buttonReplaceOnce_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.ReplaceOnce;
            this.Close();
        }

        private void buttonReplaceEvery_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.ReplaceEvery;
            this.Close();
        }

        private void buttonReplaceAll_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.ReplaceAll;
            this.Close();
        }

        protected void buttonCancel_Click(object sender, EventArgs e)
        {
            ButtonPressed = FormButtons.Cancel;
            Close();
        }

        protected void textBoxInput_TextChanged(object sender, EventArgs e)
        {
            UpdateLhsUniCodes(InputString, this.labelInputCodePoints);
        }

        void textBoxConverted_TextChanged(object sender, System.EventArgs e)
        {
            UpdateRhsUniCodes(CorrectedSpelling, this.labelForwardCodePoints);
        }

        // allow these to be overidden by sub-class forms
        protected virtual void RefreshTextBoxes(DirectableEncConverter aEC)
        {
            CorrectedSpelling = aEC.Convert(InputString);
        }

        // keep track of the last textbox that was right-click'd in, so we can handle the ChangeFont request
        protected EcTextBox m_tbLastClicked = null;
        private void contextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            ContextMenuStrip aCMS = (ContextMenuStrip)sender;
            m_tbLastClicked = (EcTextBox)aCMS.SourceControl;
            right2LeftToolStripMenuItem.Checked = (m_tbLastClicked.RightToLeft == RightToLeft.Yes);
        }

        private void changeFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
            {
                fontDialog.Font = m_tbLastClicked.Font;
                if (fontDialog.ShowDialog() == DialogResult.OK)
                    m_tbLastClicked.Font = fontDialog.Font;
            }
        }

        private void right2LeftToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
            {
                ToolStripMenuItem aMenuItem = (ToolStripMenuItem)sender;
                m_tbLastClicked.RightToLeft = (aMenuItem.Checked) ? RightToLeft.Yes : RightToLeft.No;
            }
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
                m_tbLastClicked.Undo();
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
            {
                if (m_tbLastClicked.SelectionLength == 0)
                    m_tbLastClicked.SelectAll();
                m_tbLastClicked.Cut();
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
            {
                if (m_tbLastClicked.SelectionLength == 0)
                    m_tbLastClicked.SelectAll();
                m_tbLastClicked.Copy();
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
                m_tbLastClicked.Paste();
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
                m_tbLastClicked.Clear();
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_tbLastClicked != null)
                m_tbLastClicked.SelectAll();
        }
    }
}
