
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
            this.backTranslationHelperCtrl = new BackTranslationHelper.BackTranslationHelperCtrl();
            this.SuspendLayout();
            // 
            // backTranslationHelperCtrl
            // 
            this.backTranslationHelperCtrl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.backTranslationHelperCtrl.AutoSize = true;
            this.backTranslationHelperCtrl.Location = new System.Drawing.Point(0, 0);
            this.backTranslationHelperCtrl.Name = "backTranslationHelperCtrl";
            this.backTranslationHelperCtrl.Size = new System.Drawing.Size(775, 425);
            this.backTranslationHelperCtrl.TabIndex = 1;
            // 
            // BackTranslationHelperForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.backTranslationHelperCtrl);
            this.Name = "BackTranslationHelperForm";
            this.Text = "Back Translating from {0} - {1}";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BackTranslationHelperForm_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private BackTranslationHelper.BackTranslationHelperCtrl backTranslationHelperCtrl;
    }
}