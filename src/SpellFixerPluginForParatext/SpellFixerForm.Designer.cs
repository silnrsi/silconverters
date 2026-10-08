namespace SIL.SpellFixerPluginForParatext
{
    partial class SpellFixerForm
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
            this.labelPtxProjectCaption = new System.Windows.Forms.Label();
            this.labelPtxProject = new System.Windows.Forms.Label();
            this.buttonPinToTop = new System.Windows.Forms.Button();
            this.labelSpellFixerProjectCaption = new System.Windows.Forms.Label();
            this.labelSpellFixerProject = new System.Windows.Forms.Label();
            this.buttonChooseProject = new System.Windows.Forms.Button();
            this.labelVerseCaption = new System.Windows.Forms.Label();
            this.labelVerse = new System.Windows.Forms.Label();
            this.buttonCheck = new System.Windows.Forms.Button();
            this.buttonCheckCurrentVerse = new System.Windows.Forms.Button();
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonAssignCorrectSpelling = new System.Windows.Forms.Button();
            this.buttonFindReplacementRule = new System.Windows.Forms.Button();
            this.buttonEditSpellingFixes = new System.Windows.Forms.Button();
            this.textBoxStatus = new System.Windows.Forms.TextBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel.SuspendLayout();
            this.flowLayoutPanelButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanel
            //
            this.tableLayoutPanel.ColumnCount = 3;
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.Controls.Add(this.labelPtxProjectCaption, 0, 0);
            this.tableLayoutPanel.Controls.Add(this.labelPtxProject, 1, 0);
            this.tableLayoutPanel.Controls.Add(this.buttonPinToTop, 2, 0);
            this.tableLayoutPanel.Controls.Add(this.labelSpellFixerProjectCaption, 0, 1);
            this.tableLayoutPanel.Controls.Add(this.labelSpellFixerProject, 1, 1);
            this.tableLayoutPanel.Controls.Add(this.buttonChooseProject, 2, 1);
            this.tableLayoutPanel.Controls.Add(this.labelVerseCaption, 0, 2);
            this.tableLayoutPanel.Controls.Add(this.labelVerse, 1, 2);
            this.tableLayoutPanel.Controls.Add(this.buttonCheck, 2, 2);
            this.tableLayoutPanel.Controls.Add(this.buttonCheckCurrentVerse, 2, 3);
            this.tableLayoutPanel.Controls.Add(this.flowLayoutPanelButtons, 0, 4);
            this.tableLayoutPanel.Controls.Add(this.textBoxStatus, 0, 5);
            this.tableLayoutPanel.SetColumnSpan(this.flowLayoutPanelButtons, 3);
            this.tableLayoutPanel.SetColumnSpan(this.textBoxStatus, 3);
            this.tableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel.Name = "tableLayoutPanel";
            this.tableLayoutPanel.Padding = new System.Windows.Forms.Padding(6);
            this.tableLayoutPanel.RowCount = 6;
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            //
            // labels
            //
            this.labelPtxProjectCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelPtxProjectCaption.AutoSize = true;
            this.labelPtxProjectCaption.Name = "labelPtxProjectCaption";
            this.labelPtxProjectCaption.Text = "Paratext project:";
            this.labelPtxProject.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelPtxProject.AutoSize = true;
            this.labelPtxProject.Name = "labelPtxProject";
            this.labelSpellFixerProjectCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelSpellFixerProjectCaption.AutoSize = true;
            this.labelSpellFixerProjectCaption.Name = "labelSpellFixerProjectCaption";
            this.labelSpellFixerProjectCaption.Text = "Spell Fixer project:";
            this.labelSpellFixerProject.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelSpellFixerProject.AutoSize = true;
            this.labelSpellFixerProject.Name = "labelSpellFixerProject";
            this.labelSpellFixerProject.Text = "(none chosen)";
            this.labelVerseCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelVerseCaption.AutoSize = true;
            this.labelVerseCaption.Name = "labelVerseCaption";
            this.labelVerseCaption.Text = "Current verse:";
            this.labelVerse.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelVerse.AutoSize = true;
            this.labelVerse.Name = "labelVerse";
            //
            // buttonPinToTop
            //
            this.buttonPinToTop.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.buttonPinToTop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonPinToTop.FlatAppearance.BorderSize = 0;
            this.buttonPinToTop.Name = "buttonPinToTop";
            this.buttonPinToTop.Size = new System.Drawing.Size(24, 24);
            this.toolTip.SetToolTip(this.buttonPinToTop, "Click to keep this window on top of Paratext (or to stop keeping it on top)");
            this.buttonPinToTop.Click += new System.EventHandler(this.ButtonPinToTop_Click);
            //
            // buttonChooseProject
            //
            this.buttonChooseProject.AutoSize = true;
            this.buttonChooseProject.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonChooseProject.Name = "buttonChooseProject";
            this.buttonChooseProject.Text = "&Choose Project...";
            this.toolTip.SetToolTip(this.buttonChooseProject, "Choose the Spell Fixer project (whole word Consistent Spelling Checker, or partial word legacy SpellFixer) to use for this Paratext project");
            this.buttonChooseProject.Click += new System.EventHandler(this.ButtonChooseProject_Click);
            //
            // buttonCheck
            //
            this.buttonCheck.AutoSize = true;
            this.buttonCheck.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonCheck.Name = "buttonCheck";
            this.buttonCheck.Text = "C&heck from Current Verse";
            this.toolTip.SetToolTip(this.buttonCheck, "Check each word from the current verse in Paratext to the end of the chapter, asking about each one the Spell Fixer would change");
            this.buttonCheck.Click += new System.EventHandler(this.ButtonCheck_Click);
            //
            // buttonCheckCurrentVerse
            //
            this.buttonCheckCurrentVerse.AutoSize = true;
            this.buttonCheckCurrentVerse.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonCheckCurrentVerse.Name = "buttonCheckCurrentVerse";
            this.buttonCheckCurrentVerse.Text = "Check Current &Verse";
            this.toolTip.SetToolTip(this.buttonCheckCurrentVerse, "Check each word of just the current verse (or combined verses) in Paratext, asking about each one the Spell Fixer would change");
            this.buttonCheckCurrentVerse.Click += new System.EventHandler(this.ButtonCheckCurrentVerse_Click);
            //
            // flowLayoutPanelButtons
            //
            this.flowLayoutPanelButtons.AutoSize = true;
            this.flowLayoutPanelButtons.Controls.Add(this.buttonAssignCorrectSpelling);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonFindReplacementRule);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonEditSpellingFixes);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.buttonAssignCorrectSpelling.AutoSize = true;
            this.buttonAssignCorrectSpelling.Name = "buttonAssignCorrectSpelling";
            this.buttonAssignCorrectSpelling.Text = "&Assign Correct Spelling (selection)";
            this.toolTip.SetToolTip(this.buttonAssignCorrectSpelling, "Select the misspelled word in the Paratext text window, then click here. Paste the correct spelling into the Replacement box (Ctrl+V), or click one of the similar words.");
            this.buttonAssignCorrectSpelling.Click += new System.EventHandler(this.ButtonAssignCorrectSpelling_Click);
            this.buttonFindReplacementRule.AutoSize = true;
            this.buttonFindReplacementRule.Name = "buttonFindReplacementRule";
            this.buttonFindReplacementRule.Text = "&Find Replacement Rule";
            this.toolTip.SetToolTip(this.buttonFindReplacementRule, "Find the replacement rule for the word selected in the Paratext text window (or, if nothing is selected, the word on the clipboard).");
            this.buttonFindReplacementRule.Click += new System.EventHandler(this.ButtonFindReplacementRule_Click);
            this.buttonEditSpellingFixes.AutoSize = true;
            this.buttonEditSpellingFixes.Name = "buttonEditSpellingFixes";
            this.buttonEditSpellingFixes.Text = "&Edit Spelling Fixes...";
            this.toolTip.SetToolTip(this.buttonEditSpellingFixes, "Edit the list of spelling fixes in the Spell Fixer project");
            this.buttonEditSpellingFixes.Click += new System.EventHandler(this.ButtonEditSpellingFixes_Click);
            //
            // textBoxStatus
            //
            this.textBoxStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxStatus.Multiline = true;
            this.textBoxStatus.Name = "textBoxStatus";
            this.textBoxStatus.ReadOnly = true;
            //
            // SpellFixerForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 200);
            this.Controls.Add(this.tableLayoutPanel);
            this.MinimumSize = new System.Drawing.Size(500, 200);
            this.Name = "SpellFixerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Spell Fixer for Paratext";
            this.Load += new System.EventHandler(this.SpellFixerForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SpellFixerForm_FormClosing);
            this.tableLayoutPanel.ResumeLayout(false);
            this.tableLayoutPanel.PerformLayout();
            this.flowLayoutPanelButtons.ResumeLayout(false);
            this.flowLayoutPanelButtons.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.Label labelPtxProjectCaption;
        private System.Windows.Forms.Label labelPtxProject;
        private System.Windows.Forms.Button buttonPinToTop;
        private System.Windows.Forms.Label labelSpellFixerProjectCaption;
        private System.Windows.Forms.Label labelSpellFixerProject;
        private System.Windows.Forms.Button buttonChooseProject;
        private System.Windows.Forms.Label labelVerseCaption;
        private System.Windows.Forms.Label labelVerse;
        private System.Windows.Forms.Button buttonCheck;
        private System.Windows.Forms.Button buttonCheckCurrentVerse;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Button buttonAssignCorrectSpelling;
        private System.Windows.Forms.Button buttonFindReplacementRule;
        private System.Windows.Forms.Button buttonEditSpellingFixes;
        private System.Windows.Forms.TextBox textBoxStatus;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
