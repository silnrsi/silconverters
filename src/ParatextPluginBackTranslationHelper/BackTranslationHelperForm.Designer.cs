﻿
namespace SIL.ParatextBackTranslationHelperPlugin
{
    partial class BackTranslationHelperForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.backTranslationHelperCtrl = new BackTranslationHelper.BackTranslationHelperCtrl();
            this.textBoxStatus = new System.Windows.Forms.TextBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // backTranslationHelperCtrl
            // 
            this.backTranslationHelperCtrl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            // if you edit the form (e.g. to add a control), then the designer auto coding will change this "AutoSize = false"
            //  to (possibly true) and add an 'AutoSizeMode = GrowAndShrink'... but this will cause the embedded control to
            //  stop showing properly. It *must* be AutoSize = false. (keep this comment here too, so the next person sees it too)
            // (I'm not sure if it's specifically necessary, but you might need to restore the PerformLayout at the bottom,
            //  which the editing of the form will remove too)
            this.backTranslationHelperCtrl.AutoSize = false;
            this.backTranslationHelperCtrl.Location = new System.Drawing.Point(0, 0);
            this.backTranslationHelperCtrl.Name = "backTranslationHelperCtrl";
            this.backTranslationHelperCtrl.Size = new System.Drawing.Size(800, 428);
            this.backTranslationHelperCtrl.TabIndex = 1;
            // 
            // textBoxStatus
            // 
            this.textBoxStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxStatus.Location = new System.Drawing.Point(0, 430);
            this.textBoxStatus.Name = "textBoxStatus";
            this.textBoxStatus.ReadOnly = true;
            this.textBoxStatus.Size = new System.Drawing.Size(800, 20);
            this.textBoxStatus.TabIndex = 2;
            this.textBoxStatus.Click += TextBoxStatus_Click;
            // 
            // BackTranslationHelperForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.textBoxStatus);
            this.Controls.Add(this.backTranslationHelperCtrl);
            this.Name = "BackTranslationHelperForm";
            this.Text = "Back Translating from {0} - {1} in verse: {2}";
            this.Activated += new System.EventHandler(this.BackTranslationHelperForm_Activated);
            this.Deactivate += new System.EventHandler(this.BackTranslationHelperForm_Deactivate);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BackTranslationHelperForm_FormClosing);
            this.Load += new System.EventHandler(this.BackTranslationHelperForm_Load);
            this.Resize += new System.EventHandler(this.BackTranslationHelperForm_Resize);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private BackTranslationHelper.BackTranslationHelperCtrl backTranslationHelperCtrl;
        private System.Windows.Forms.TextBox textBoxStatus;
        private System.Windows.Forms.ToolTip toolTip;
    }
}