namespace SIL.SpellFixerPluginForParatext
{
    partial class FixSpellingForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.labelVerseCaption = new System.Windows.Forms.Label();
            this.labelVerse = new System.Windows.Forms.Label();
            this.labelSimilarWords = new System.Windows.Forms.Label();
            this.labelBadForm = new System.Windows.Forms.Label();
            this.textBoxBadForm = new SilEncConverters40.EcTextBox();
            this.listBoxSimilarWords = new System.Windows.Forms.ListBox();
            this.labelReplacement = new System.Windows.Forms.Label();
            this.textBoxReplacement = new SilEncConverters40.EcTextBox();
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOk = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel.SuspendLayout();
            this.flowLayoutPanelButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanel
            //
            this.tableLayoutPanel.ColumnCount = 3;
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanel.Controls.Add(this.labelVerseCaption, 0, 0);
            this.tableLayoutPanel.Controls.Add(this.labelVerse, 1, 0);
            this.tableLayoutPanel.Controls.Add(this.labelSimilarWords, 2, 0);
            this.tableLayoutPanel.Controls.Add(this.labelBadForm, 0, 1);
            this.tableLayoutPanel.Controls.Add(this.textBoxBadForm, 1, 1);
            this.tableLayoutPanel.Controls.Add(this.listBoxSimilarWords, 2, 1);
            this.tableLayoutPanel.Controls.Add(this.labelReplacement, 0, 2);
            this.tableLayoutPanel.Controls.Add(this.textBoxReplacement, 1, 2);
            this.tableLayoutPanel.Controls.Add(this.flowLayoutPanelButtons, 0, 3);
            this.tableLayoutPanel.SetRowSpan(this.listBoxSimilarWords, 3);
            this.tableLayoutPanel.SetColumnSpan(this.flowLayoutPanelButtons, 2);
            this.tableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel.Name = "tableLayoutPanel";
            this.tableLayoutPanel.Padding = new System.Windows.Forms.Padding(6);
            this.tableLayoutPanel.RowCount = 4;
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            //
            // labels
            //
            this.labelVerseCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelVerseCaption.AutoSize = true;
            this.labelVerseCaption.Name = "labelVerseCaption";
            this.labelVerseCaption.Text = "Selected in:";
            this.labelVerse.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelVerse.AutoSize = true;
            this.labelVerse.Name = "labelVerse";
            this.labelSimilarWords.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelSimilarWords.AutoSize = true;
            this.labelSimilarWords.Name = "labelSimilarWords";
            this.labelSimilarWords.Text = "&Similar words:";
            this.labelBadForm.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelBadForm.AutoSize = true;
            this.labelBadForm.Name = "labelBadForm";
            this.labelBadForm.Text = "&Bad form:";
            this.labelReplacement.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelReplacement.AutoSize = true;
            this.labelReplacement.Name = "labelReplacement";
            this.labelReplacement.Text = "&Replacement:";
            //
            // textBoxBadForm
            //
            this.textBoxBadForm.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxBadForm.Name = "textBoxBadForm";
            this.toolTip.SetToolTip(this.textBoxBadForm, "The misspelled word (from the selection in Paratext)");
            this.textBoxBadForm.TextChanged += new System.EventHandler(this.TextBoxBadForm_TextChanged);
            this.textBoxBadForm.Enter += new System.EventHandler(this.TextBox_Enter);
            //
            // textBoxReplacement
            //
            this.textBoxReplacement.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxReplacement.Name = "textBoxReplacement";
            this.toolTip.SetToolTip(this.textBoxReplacement, "The correct spelling (paste it with Ctrl+V, or click one of the similar words)");
            this.textBoxReplacement.Enter += new System.EventHandler(this.TextBox_Enter);
            //
            // listBoxSimilarWords
            //
            this.listBoxSimilarWords.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxSimilarWords.IntegralHeight = false;
            this.listBoxSimilarWords.Name = "listBoxSimilarWords";
            this.toolTip.SetToolTip(this.listBoxSimilarWords, "Words the Spell Fixer project considers similar (most frequent first). Click one to use it as the replacement; double-click to use it and click OK");
            this.listBoxSimilarWords.SelectedIndexChanged += new System.EventHandler(this.ListBoxSimilarWords_SelectedIndexChanged);
            this.listBoxSimilarWords.DoubleClick += new System.EventHandler(this.ListBoxSimilarWords_DoubleClick);
            //
            // flowLayoutPanelButtons
            //
            this.flowLayoutPanelButtons.AutoSize = true;
            this.flowLayoutPanelButtons.Controls.Add(this.buttonCancel);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonOk);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);
            this.buttonOk.Name = "buttonOk";
            this.buttonOk.Text = "OK";
            this.toolTip.SetToolTip(this.buttonOk, "Add the rule, replace the selected word in Paratext, and remember the fix for Paratext's spelling list");
            this.buttonOk.Click += new System.EventHandler(this.ButtonOk_Click);
            //
            // FixSpellingForm
            //
            this.AcceptButton = this.buttonOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(560, 180);
            this.Controls.Add(this.tableLayoutPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.MinimumSize = new System.Drawing.Size(420, 160);
            this.Name = "FixSpellingForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Fix Spelling";
            this.Deactivate += new System.EventHandler(this.FixSpellingForm_Deactivate);
            this.tableLayoutPanel.ResumeLayout(false);
            this.tableLayoutPanel.PerformLayout();
            this.flowLayoutPanelButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Label labelVerseCaption;
        private System.Windows.Forms.Label labelVerse;
        private System.Windows.Forms.Label labelSimilarWords;
        private System.Windows.Forms.Label labelBadForm;
        private SilEncConverters40.EcTextBox textBoxBadForm;
        private System.Windows.Forms.ListBox listBoxSimilarWords;
        private System.Windows.Forms.Label labelReplacement;
        private SilEncConverters40.EcTextBox textBoxReplacement;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonOk;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
