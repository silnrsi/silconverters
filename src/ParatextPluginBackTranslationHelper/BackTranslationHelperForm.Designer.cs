
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
            this.buttonPinToTop = new System.Windows.Forms.Button();
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
            this.backTranslationHelperCtrl.Size = new System.Drawing.Size(790, 449);
            this.backTranslationHelperCtrl.TabIndex = 1;
            // 
            // buttonPinToTop
            // 
            this.buttonPinToTop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonPinToTop.Image = global::SIL.ParatextBackTranslationHelperPlugin.Properties.Resources.pinup;
            this.buttonPinToTop.Location = new System.Drawing.Point(768, 0);
            this.buttonPinToTop.Name = "buttonPinToTop";
            this.buttonPinToTop.Size = new System.Drawing.Size(32, 32);
            this.buttonPinToTop.TabIndex = 2;
            this.buttonPinToTop.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.buttonPinToTop.UseVisualStyleBackColor = true;
            this.buttonPinToTop.Click += new System.EventHandler(this.ButtonPinToTop_Click);
            // 
            // BackTranslationHelperForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.buttonPinToTop);
            this.Controls.Add(this.backTranslationHelperCtrl);
            this.Name = "BackTranslationHelperForm";
            this.Text = "Back Translating from {0} - {1} in verse: {2}";
            this.Activated += new System.EventHandler(this.BackTranslationHelperForm_Activated);
            this.Deactivate += new System.EventHandler(this.BackTranslationHelperForm_Deactivate);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BackTranslationHelperForm_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private BackTranslationHelper.BackTranslationHelperCtrl backTranslationHelperCtrl;
        private System.Windows.Forms.Button buttonPinToTop;
    }
}