using ECInterfaces;
using SilEncConverters40;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace BackTranslationHelper
{
    public partial class BackTranslationHelperCtrl : UserControl
    {
        public int MaxPossibleTargetTranslations = 3;  // to add more, you have to add new lines like the one starting at row 2

        #region Member variables
        // the form in which this UserControl is embedded will initialize these
        public IBackTranslationHelperDataSource BackTranslationHelperDataSource;
        public List<IEncConverter> TheTranslators = new List<IEncConverter>();

        public BackTranslationHelperModel _model;
        #endregion

        public BackTranslationHelperCtrl()
        {
            InitializeComponent();
        }

        public void Initialize(bool displayExistingTargetTranslation)
        {
            BackTranslationHelperDataSource.SetDataUpdateProc(UpdateData);

            /* do w/ GetPluginData/PutPluginData
            foreach (var translatorName in Properties.Settings.Default.MapProjectsToTranslatorNames.Cast<string>().ToList())
            {
                if (!TheTranslators.Select(t => t.Name).Contains(translatorName))
                {
                    if (DirectableEncConverter.EncConverters.ContainsKey(translatorName))
                        TheTranslators.Add(DirectableEncConverter.EncConverters[translatorName]);
                    else
                    {
                        Properties.Settings.Default.MapProjectsToTranslatorNames.Remove(translatorName);
                        Properties.Settings.Default.Save();
                    }
                }
            }
            */

            // see how many converters are configured (if none, then query for one)
            if (!TheTranslators.Any())
            {
                var aTranslator = QueryTranslator();
                TheTranslators.Add(aTranslator.GetEncConverter);
            }

            tableLayoutPanel.SuspendLayout();
            SuspendLayout();

            hideColumn1LabelsToolStripMenuItem.Checked = Properties.Settings.Default.HideLabels;
            InitializeLabelHiding();

            labelSourceData.Font = BackTranslationHelperDataSource.SourceLanguageFont;
            if (displayExistingTargetTranslation)
            {
                labelForExistingTargetData.Visible = !hideColumn1LabelsToolStripMenuItem.Checked;
                labelTargetTextExisting.Visible = true;
                labelTargetTextExisting.Font = BackTranslationHelperDataSource.TargetLanguageFont;
            }
            else
                labelTargetTextExisting.Visible = labelForExistingTargetData.Visible = false;

            textBoxTargetBackTranslation.Font = BackTranslationHelperDataSource.TargetLanguageFont;

            // we're either showing the target translated suggestion in a textbox (if there's only 1 converter)
            //  or in labels above it to choose from (if there are more than one converter)
            var labelsPossibleTargetTranslations = tableLayoutPanel.Controls.OfType<Label>().Where(l => l.Name.Contains("labelPossibleTargetTranslation")).ToList();
            var buttonsFillTargetOption = tableLayoutPanel.Controls.OfType<Button>().Where(b => b.Name.Contains("buttonFillTargetTextOption")).ToList();
            var numOfTranslators = TheTranslators.Count;

            // if there's only one, then we don't need to display the 'possible' translations to start with.
            var i = 0;
            if (numOfTranslators == 1)
            {
                // hide them all
                for (; i < MaxPossibleTargetTranslations; i++)
                {
                    var label = labelsPossibleTargetTranslations[i];
                    var button = buttonsFillTargetOption[i];
                    label.Visible = button.Visible = false;
                }
                labelForTargetDataOptions.Visible = false;
            }
            else
            {
                labelForTargetDataOptions.Visible = true;

                for (; i < numOfTranslators; i++)
                {
                    var label = labelsPossibleTargetTranslations[i];
                    var button = buttonsFillTargetOption[i];
                    label.Visible = button.Visible = true;
                    label.Font = labelTargetTextExisting.Font;
                    toolTip.SetToolTip(label, $"This is the translation from the {TheTranslators[i].Name} Translator");
                }

                for (; i < MaxPossibleTargetTranslations; i++)
                {
                    var label = labelsPossibleTargetTranslations[i];
                    var button = buttonsFillTargetOption[i];
                    label.Visible = button.Visible = false;
                }
            }

            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        public static DirectableEncConverter QueryTranslator()
        {
            DirectableEncConverter theEc;
            try
            {
                var aEc = DirectableEncConverter.EncConverters.AutoSelectWithTitle(ConvType.Unicode_to_Unicode,
                                                                                   "Select the Encoding Converter to do the Translation (e.g. a Bing or DeepL translator)");
                if (aEc == null)
                    throw new ApplicationException("Unable to proceed if you don't select a Translator/EncConverter resource");

                theEc = new DirectableEncConverter(aEc);
            }
            catch (Exception)
            {
                throw;
            }

            return theEc;
        }

        public List<TargetPossible> NewTargetTexts
        {
            get
            {
                // always choose the one in the text box
                var newTargetTexts = new List<TargetPossible> 
                { 
                    new TargetPossible 
                    { 
                        TargetData = textBoxTargetBackTranslation.Text,
                        FillButtonEnabled = true,
                        PossibleIndex = 0,
                        TranslatorName = TheTranslators.FirstOrDefault()?.Name
                    } 
                };
                return newTargetTexts;
            }

            set
            {
                if (TheTranslators.Count == 1)
                {
                    System.Diagnostics.Debug.Assert(value.Count == 1);
                    var targetPossible = value.FirstOrDefault();
                    textBoxTargetBackTranslation.Text = targetPossible.TargetData;
                }
                else
                {
                    var labelsPossibleTargetTranslations = tableLayoutPanel.Controls.OfType<Label>().Where(l => l.Name.Contains("labelPossibleTargetTranslation")).ToList();
                    System.Diagnostics.Debug.Assert(value.Count == TheTranslators.Count);
                    System.Diagnostics.Debug.Assert(labelsPossibleTargetTranslations.Take(value.Count).ToList().All(l => l.Visible));

                    for (var i = 0; i < TheTranslators.Count; i++)
                    {
                        var label = labelsPossibleTargetTranslations[i];
                        label.Text = value[i].TargetData;
                    }
                }
            }
        }

        #region Event handlers
        public void GetNewData(ref BackTranslationHelperModel model)
        {
            if (model == null)
                _model = BackTranslationHelperDataSource.Model;

            else if (_model?.SourceData != model.SourceData)
                _model = model;

            for (var i = _model.TargetsPossible.Count; i < TheTranslators.Count; i++)
            {
                var theTranslator = TheTranslators[i];
                var translatedText = theTranslator.Convert(_model.SourceData);
                _model.TargetsPossible.Add(new TargetPossible { TargetData = translatedText, FillButtonEnabled = true, PossibleIndex = i, TranslatorName = theTranslator.Name });
            }

            model = _model;
        }

        public void UpdateData(BackTranslationHelperModel model)
        {
            labelSourceData.Text = model.SourceData;
            labelTargetTextExisting.Text = model.TargetDataExisting;

            // some clients (i.e. Word) only pass 1 translated target text (bkz it only knows about 1 EncConverter/Translator)
            //  if we have fewer than the number of possible target translations (i.e. we added 1 or more addl Translators),
            //  then convert the missing ones to add to this collection
            var numOfTranslators = TheTranslators.Count;
            for (var i = model.TargetsPossible.Count; i < numOfTranslators; i++)
            {
                var theTranslator = TheTranslators[i];
                var translatedText = theTranslator.Convert(model.SourceData);
                model.TargetsPossible.Add(new TargetPossible { TargetData = translatedText, FillButtonEnabled = true, PossibleIndex = i, TranslatorName = theTranslator.Name });
            }

            NewTargetTexts = model.TargetsPossible;
        }

        #endregion

        #region Private helper methods

        #endregion

        private void buttonWriteTextToTarget_Click(object sender, System.EventArgs e)
        {
            BackTranslationHelperDataSource.ButtonPressed(ButtonPressed.WriteToTarget);
            BackTranslationHelperDataSource.Log($"change target text from '{labelTargetTextExisting.Text}' to '{textBoxTargetBackTranslation.Text}'");
            BackTranslationHelperDataSource.WriteToTarget(textBoxTargetBackTranslation.Text);
        }

        private void buttonCopyToClipboard_Click(object sender, System.EventArgs e)
        {
            Clipboard.SetDataObject(textBoxTargetBackTranslation.Text);
            BackTranslationHelperDataSource.ButtonPressed(ButtonPressed.Copy);
        }

        private void buttonNextSection_Click(object sender, System.EventArgs e)
        {
            if (BackTranslationHelperDataSource == null)
            {
                // see if we can manually trigger the change of verse number in the host
                return;
            }
            BackTranslationHelperDataSource.ButtonPressed(ButtonPressed.MoveToNext);
            var existingTargetText = labelTargetTextExisting.Text;
            var newTargetText = textBoxTargetBackTranslation.Text;
            var modified = existingTargetText != newTargetText;

            if (!autoSaveToolStripMenuItem.Checked)
            {
                if (modified)
                {
                    var res = MessageBox.Show($"Do you want to save/write out the translated text before moving to the next portion?", BackTranslationHelperDataSource.ProjectName, MessageBoxButtons.YesNoCancel);
                    if (res == DialogResult.Cancel)
                    {
                        BackTranslationHelperDataSource.Cancel();
                        return;
                    }
                    if (res == DialogResult.No)
                    {
                        BackTranslationHelperDataSource.MoveToNext();
                        return;
                    }
                }
            }

            if (modified)
            {
                BackTranslationHelperDataSource.Log($"change target text from '{existingTargetText}' to '{newTargetText}'");
                BackTranslationHelperDataSource.WriteToTarget(newTargetText);
            }

            BackTranslationHelperDataSource.MoveToNext();
        }

        private void changeEncConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void addEncConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var newTranslator = QueryTranslator();
            TheTranslators.Add(newTranslator.GetEncConverter);
            Initialize(labelTargetTextExisting.Visible);

            System.Diagnostics.Debug.Assert(_model != null);

            UpdateData(_model);
        }

        private void buttonFillTargetTextOption1_Click(object sender, EventArgs e)
        {
            textBoxTargetBackTranslation.Text = labelPossibleTargetTranslation1.Text;
        }

        private void buttonFillTargetTextOption2_Click(object sender, EventArgs e)
        {
            textBoxTargetBackTranslation.Text = labelPossibleTargetTranslation2.Text;
        }

        private void buttonFillTargetTextOption3_Click(object sender, EventArgs e)
        {
            textBoxTargetBackTranslation.Text = labelPossibleTargetTranslation3.Text;
        }

        private void textBoxTargetBackTranslation_Enter(object sender, EventArgs e)
        {
            BackTranslationHelperDataSource?.ActivateKeyboard();
        }

        private void InitializeLabelHiding()
        {
            labelForSourceData.Visible =
                labelForExistingTargetData.Visible =
                labelForTargetDataOptions.Visible =
                labelForTargetTranslation.Visible = !Properties.Settings.Default.HideLabels;
        }

        private void hideColumn1LabelsToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            var newCheckState = hideColumn1LabelsToolStripMenuItem.Checked;
            if (newCheckState != Properties.Settings.Default.HideLabels)
            {
                Properties.Settings.Default.HideLabels = newCheckState;
                Properties.Settings.Default.Save();
                if (_model != null)
                {
                    Initialize(labelTargetTextExisting.Visible);
                    UpdateData(_model);
                }
            }
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            var location = textBox.GetPositionFromCharIndex(textBox.TextLength - 1);
            textBox.Height = textBox.Location.Y + location.Y;
        }
    }
}
