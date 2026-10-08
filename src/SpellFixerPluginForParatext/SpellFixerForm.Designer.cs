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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SpellFixerForm));
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
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
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
            this.tableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel.Name = "tableLayoutPanel";
            this.tableLayoutPanel.Padding = new System.Windows.Forms.Padding(6);
            this.tableLayoutPanel.RowCount = 6;
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel.Size = new System.Drawing.Size(600, 200);
            this.tableLayoutPanel.TabIndex = 0;
            // 
            // labelPtxProjectCaption
            // 
            this.labelPtxProjectCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelPtxProjectCaption.AutoSize = true;
            this.labelPtxProjectCaption.Location = new System.Drawing.Point(18, 14);
            this.labelPtxProjectCaption.Name = "labelPtxProjectCaption";
            this.labelPtxProjectCaption.Size = new System.Drawing.Size(84, 13);
            this.labelPtxProjectCaption.TabIndex = 0;
            this.labelPtxProjectCaption.Text = "Paratext project:";
            // 
            // labelPtxProject
            // 
            this.labelPtxProject.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelPtxProject.AutoSize = true;
            this.labelPtxProject.Location = new System.Drawing.Point(108, 14);
            this.labelPtxProject.Name = "labelPtxProject";
            this.labelPtxProject.Size = new System.Drawing.Size(0, 13);
            this.labelPtxProject.TabIndex = 1;
            // 
            // buttonPinToTop
            // 
            this.buttonPinToTop.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.buttonPinToTop.FlatAppearance.BorderSize = 0;
            this.buttonPinToTop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonPinToTop.Location = new System.Drawing.Point(567, 9);
            this.buttonPinToTop.Name = "buttonPinToTop";
            this.buttonPinToTop.Size = new System.Drawing.Size(24, 24);
            this.buttonPinToTop.TabIndex = 2;
            this.toolTip.SetToolTip(this.buttonPinToTop, "Click to keep this window on top of Paratext (or to stop keeping it on top)");
            this.buttonPinToTop.Click += new System.EventHandler(this.ButtonPinToTop_Click);
            // 
            // labelSpellFixerProjectCaption
            // 
            this.labelSpellFixerProjectCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelSpellFixerProjectCaption.AutoSize = true;
            this.labelSpellFixerProjectCaption.Location = new System.Drawing.Point(9, 44);
            this.labelSpellFixerProjectCaption.Name = "labelSpellFixerProjectCaption";
            this.labelSpellFixerProjectCaption.Size = new System.Drawing.Size(93, 13);
            this.labelSpellFixerProjectCaption.TabIndex = 3;
            this.labelSpellFixerProjectCaption.Text = "Spell Fixer project:";
            // 
            // labelSpellFixerProject
            // 
            this.labelSpellFixerProject.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelSpellFixerProject.AutoSize = true;
            this.labelSpellFixerProject.Location = new System.Drawing.Point(108, 44);
            this.labelSpellFixerProject.Name = "labelSpellFixerProject";
            this.labelSpellFixerProject.Size = new System.Drawing.Size(75, 13);
            this.labelSpellFixerProject.TabIndex = 4;
            this.labelSpellFixerProject.Text = "(none chosen)";
            // 
            // buttonChooseProject
            // 
            this.buttonChooseProject.AutoSize = true;
            this.buttonChooseProject.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonChooseProject.Location = new System.Drawing.Point(453, 39);
            this.buttonChooseProject.Name = "buttonChooseProject";
            this.buttonChooseProject.Size = new System.Drawing.Size(138, 23);
            this.buttonChooseProject.TabIndex = 5;
            this.buttonChooseProject.Text = "&Choose Project...";
            this.toolTip.SetToolTip(this.buttonChooseProject, "Choose the Spell Fixer project (whole word Consistent Spelling Checker, or partia" +
        "l word legacy SpellFixer) to use for this Paratext project");
            this.buttonChooseProject.Click += new System.EventHandler(this.ButtonChooseProject_Click);
            // 
            // labelVerseCaption
            // 
            this.labelVerseCaption.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelVerseCaption.AutoSize = true;
            this.labelVerseCaption.Location = new System.Drawing.Point(29, 73);
            this.labelVerseCaption.Name = "labelVerseCaption";
            this.labelVerseCaption.Size = new System.Drawing.Size(73, 13);
            this.labelVerseCaption.TabIndex = 6;
            this.labelVerseCaption.Text = "Current verse:";
            // 
            // labelVerse
            // 
            this.labelVerse.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelVerse.AutoSize = true;
            this.labelVerse.Location = new System.Drawing.Point(108, 73);
            this.labelVerse.Name = "labelVerse";
            this.labelVerse.Size = new System.Drawing.Size(0, 13);
            this.labelVerse.TabIndex = 7;
            // 
            // buttonCheck
            // 
            this.buttonCheck.AutoSize = true;
            this.buttonCheck.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonCheck.Location = new System.Drawing.Point(453, 68);
            this.buttonCheck.Name = "buttonCheck";
            this.buttonCheck.Size = new System.Drawing.Size(138, 23);
            this.buttonCheck.TabIndex = 8;
            this.buttonCheck.Text = "C&heck from Current Verse";
            this.toolTip.SetToolTip(this.buttonCheck, "Check each word from the current verse in Paratext to the end of the chapter, ask" +
        "ing about each one the Spell Fixer would change");
            this.buttonCheck.Click += new System.EventHandler(this.ButtonCheck_Click);
            // 
            // buttonCheckCurrentVerse
            // 
            this.buttonCheckCurrentVerse.AutoSize = true;
            this.buttonCheckCurrentVerse.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonCheckCurrentVerse.Location = new System.Drawing.Point(453, 97);
            this.buttonCheckCurrentVerse.Name = "buttonCheckCurrentVerse";
            this.buttonCheckCurrentVerse.Size = new System.Drawing.Size(138, 23);
            this.buttonCheckCurrentVerse.TabIndex = 9;
            this.buttonCheckCurrentVerse.Text = "Check Current &Verse";
            this.toolTip.SetToolTip(this.buttonCheckCurrentVerse, "Check each word of just the current verse (or combined verses) in Paratext, askin" +
        "g about each one the Spell Fixer would change");
            this.buttonCheckCurrentVerse.Click += new System.EventHandler(this.ButtonCheckCurrentVerse_Click);
            // 
            // flowLayoutPanelButtons
            // 
            this.flowLayoutPanelButtons.AutoSize = true;
            this.tableLayoutPanel.SetColumnSpan(this.flowLayoutPanelButtons, 3);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonAssignCorrectSpelling);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonFindReplacementRule);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonEditSpellingFixes);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelButtons.Location = new System.Drawing.Point(9, 126);
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.flowLayoutPanelButtons.Size = new System.Drawing.Size(582, 29);
            this.flowLayoutPanelButtons.TabIndex = 10;
            // 
            // buttonAssignCorrectSpelling
            // 
            this.buttonAssignCorrectSpelling.AutoSize = true;
            this.buttonAssignCorrectSpelling.Location = new System.Drawing.Point(3, 3);
            this.buttonAssignCorrectSpelling.Name = "buttonAssignCorrectSpelling";
            this.buttonAssignCorrectSpelling.Size = new System.Drawing.Size(176, 23);
            this.buttonAssignCorrectSpelling.TabIndex = 0;
            this.buttonAssignCorrectSpelling.Text = "&Assign Correct Spelling (selection)";
            this.toolTip.SetToolTip(this.buttonAssignCorrectSpelling, "Select the misspelled word in the Paratext text window, then click here. Paste th" +
        "e correct spelling into the Replacement box (Ctrl+V), or click one of the simila" +
        "r words.");
            this.buttonAssignCorrectSpelling.Click += new System.EventHandler(this.ButtonAssignCorrectSpelling_Click);
            // 
            // buttonFindReplacementRule
            // 
            this.buttonFindReplacementRule.AutoSize = true;
            this.buttonFindReplacementRule.Location = new System.Drawing.Point(185, 3);
            this.buttonFindReplacementRule.Name = "buttonFindReplacementRule";
            this.buttonFindReplacementRule.Size = new System.Drawing.Size(128, 23);
            this.buttonFindReplacementRule.TabIndex = 1;
            this.buttonFindReplacementRule.Text = "&Find Replacement Rule";
            this.toolTip.SetToolTip(this.buttonFindReplacementRule, "Find the replacement rule for the word selected in the Paratext text window (or, " +
        "if nothing is selected, the word on the clipboard).");
            this.buttonFindReplacementRule.Click += new System.EventHandler(this.ButtonFindReplacementRule_Click);
            // 
            // buttonEditSpellingFixes
            // 
            this.buttonEditSpellingFixes.AutoSize = true;
            this.buttonEditSpellingFixes.Location = new System.Drawing.Point(319, 3);
            this.buttonEditSpellingFixes.Name = "buttonEditSpellingFixes";
            this.buttonEditSpellingFixes.Size = new System.Drawing.Size(111, 23);
            this.buttonEditSpellingFixes.TabIndex = 2;
            this.buttonEditSpellingFixes.Text = "&Edit Spelling Fixes...";
            this.toolTip.SetToolTip(this.buttonEditSpellingFixes, "Edit the list of spelling fixes in the Spell Fixer project");
            this.buttonEditSpellingFixes.Click += new System.EventHandler(this.ButtonEditSpellingFixes_Click);
            // 
            // textBoxStatus
            // 
            this.tableLayoutPanel.SetColumnSpan(this.textBoxStatus, 3);
            this.textBoxStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxStatus.Location = new System.Drawing.Point(9, 161);
            this.textBoxStatus.Multiline = true;
            this.textBoxStatus.Name = "textBoxStatus";
            this.textBoxStatus.ReadOnly = true;
            this.textBoxStatus.Size = new System.Drawing.Size(582, 30);
            this.textBoxStatus.TabIndex = 11;
            // 
            // SpellFixerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 200);
            this.Controls.Add(this.tableLayoutPanel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(500, 200);
            this.Name = "SpellFixerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Spell Fixer for Paratext";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SpellFixerForm_FormClosing);
            this.Load += new System.EventHandler(this.SpellFixerForm_Load);
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
