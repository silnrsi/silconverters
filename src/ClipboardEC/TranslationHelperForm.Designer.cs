namespace ClipboardEC
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
            this.components = new System.ComponentModel.Container();
            this.backTranslationHelperCtrl = new BackTranslationHelper.BackTranslationHelperCtrl();
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
            this.backTranslationHelperCtrl.Size = new System.Drawing.Size(800, 450);
            this.backTranslationHelperCtrl.TabIndex = 0;
            // 
            // TranslationHelperForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.backTranslationHelperCtrl);
            this.Name = "TranslationHelperForm";
            this.Text = "TranslationHelperForm";
            this.Load += new System.EventHandler(this.TranslationHelperForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TranslationHelperForm_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private BackTranslationHelper.BackTranslationHelperCtrl backTranslationHelperCtrl;
    }
}