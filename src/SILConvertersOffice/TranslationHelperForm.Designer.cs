
namespace SILConvertersOffice
{
    partial class TranslationHelperForm
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
            // backTranslationHelperCtrl1
            // 
            this.backTranslationHelperCtrl.Location = new System.Drawing.Point(12, 12);
            this.backTranslationHelperCtrl.Name = "backTranslationHelperCtrl1";
            this.backTranslationHelperCtrl.Size = new System.Drawing.Size(769, 458);
            this.backTranslationHelperCtrl.TabIndex = 0;
            // 
            // TranslationHelperForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(793, 482);
            this.Controls.Add(this.backTranslationHelperCtrl);
            this.Name = "TranslationHelperForm";
            this.Text = "Back Translating from {0} - {1}";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TranslationHelperForm_FormClosing);
            this.ResumeLayout(false);

        }

        #endregion

        private BackTranslationHelper.BackTranslationHelperCtrl backTranslationHelperCtrl;
    }
}