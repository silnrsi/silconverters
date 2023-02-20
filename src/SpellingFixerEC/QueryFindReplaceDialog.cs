using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SpellingFixerEC
{
    public partial class QueryFindReplaceDialog : Form
    {
        private string _findWhat;
        public string FindWhat 
        {
            get
            {
                return $"{BoundaryInitial}{_findWhat}{BoundaryFinal}";
            }
            set 
            {
                _findWhat = value;
            }
        }
        public string ReplaceWith { get; set; }
        public string BoundaryCharacter { get; set; }

        private String BoundaryInitial
        {
            get
            {
                return (_isBoundaryInitial) ? BoundaryCharacter : String.Empty;
            }
        }

        private String BoundaryFinal
        {
            get
            {
                return (_isBoundaryFinal) ? BoundaryCharacter : String.Empty;
            }
        }

        public QueryFindReplaceDialog(Font font)
        {
            InitializeComponent();

            textBoxFindWord.Font = font = 
                textBoxOriginalWord.Font =
                textBoxWordBoundaryFindText.Font = 
                textBoxReplaceWord.Font = font;
        }

        public DialogResult ShowDialog(string findWhat, string replaceWith, string originalWord,
                                bool isRightToLeft, string boundaryCharacter, bool isShowDelete)
        {
            if (findWhat?.StartsWith(boundaryCharacter) ?? false)
            {
                _isBoundaryInitial = checkBoxWordInitial.Checked = true;
                findWhat = findWhat.Substring(1);
            }

            if (findWhat?.EndsWith(boundaryCharacter) ?? false)
            {
                _isBoundaryFinal = checkBoxWordFinal.Checked = true;
                findWhat = findWhat.Substring(0, findWhat.Length - 1);
            }

            if (originalWord?.StartsWith(boundaryCharacter) ?? false)
            {
                originalWord = originalWord.Substring(1);
            }

            if (originalWord?.EndsWith(boundaryCharacter) ?? false)
            {
                originalWord = originalWord.Substring(0, originalWord.Length - 1);
            }

            FindWhat = textBoxFindWord.Text = findWhat;
            textBoxReplaceWord.Text = replaceWith;
            textBoxOriginalWord.Text = originalWord;
            BoundaryCharacter = boundaryCharacter;

            if (isRightToLeft)
            {
                textBoxFindWord.RightToLeft = RightToLeft.Yes;
                textBoxReplaceWord.RightToLeft = RightToLeft.Yes;
                textBoxOriginalWord.RightToLeft = RightToLeft.Yes;
                textBoxWordBoundaryFindText.RightToLeft = RightToLeft.Yes;
            }

            buttonDelete.Visible = isShowDelete;

            if (findWhat != replaceWith)
            {
                this.Text = "Existing Replacement Rule";
                if (!String.IsNullOrEmpty(originalWord))
                {
                    labelAddedFromOriginalWord.Visible =
                        textBoxOriginalWord.Visible = true;
                    this.textBoxOriginalWord.Text = originalWord;
                }
            }

            UpdateFindWhatDisplay();
            var res = base.ShowDialog();
            return res;
        }

        private void UpdateUniCodes(string strInputString)
        {
            int nLenString = strInputString.Length;

            string strWhole = null, strPiece = null, strUPiece = null;
            foreach (char ch in strInputString)
            {
                if (ch == 0)   // sometimes it's null (esp. for utf32)
                    strPiece = "nul (u0000)  ";
                else
                {
                    strUPiece = String.Format("{0:X}", (int)ch);

                    // left pad with 0's (there may be a better way to do this, but 
                    //  I don't know what it is)
                    while (strUPiece.Length < 4) strUPiece = "0" + strUPiece;

                    strPiece = String.Format("{0:#} (u{1,4})  ", ch, strUPiece);
                }
                strWhole += strPiece;
            }

            labelUnicodeCodes.Text = strWhole;
        }

        private void checkBoxWordBoundary_CheckedChanged(object sender, EventArgs e)
        {
            UpdateFindWhatDisplay();
        }

        private void textBoxFindWord_TextChanged(object sender, EventArgs e)
        {
            FindWhat = textBoxFindWord.Text;
            UpdateFindWhatDisplay();
            UpdateUniCodes(textBoxFindWord.Text);
        }

        private void textBoxReplaceWord_TextChanged(object sender, EventArgs e)
        {
            UpdateUniCodes(textBoxReplaceWord.Text);
            ReplaceWith = textBoxReplaceWord.Text;
        }

        private void textBoxFindWord_Enter(object sender, EventArgs e)
        {
            UpdateUniCodes(textBoxFindWord.Text);
        }

        private void textBoxReplaceWord_Enter(object sender, EventArgs e)
        {
            UpdateUniCodes(textBoxReplaceWord.Text);
        }

        private bool _isBoundaryInitial { get; set; }
        private bool _isBoundaryFinal { get; set; }

        private void UpdateFindWhatDisplay()
        {
            _isBoundaryInitial = checkBoxWordInitial.Checked;
            _isBoundaryFinal = checkBoxWordFinal.Checked;

            textBoxWordBoundaryFindText.Text = FindWhat;
        }

        private void QueryFindReplaceDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (String.IsNullOrEmpty(FindWhat) && (DialogResult == DialogResult.OK))
            {
                MessageBox.Show("You can't have an empty value for the \"Find What\" text. Click Cancel if you didn't mean to add a rule.", SpellingFixerEC.cstrCaption);
                e.Cancel = true;
            }
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            Close();    // button has DialogResult.Abort associated w/ it, which triggers the caller to delete
        }
    }
}
