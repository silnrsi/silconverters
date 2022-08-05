
namespace SIL.ParatextBackTranslationHelperPlugin
{
    partial class BackTranslationHelperWebForm
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
            this.backTranslationHelperView = new BackTranslationHelper.BackTranslationHelperView();
            this.SuspendLayout();
            // 
            // backTranslationHelperView
            // 
            this.backTranslationHelperView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.backTranslationHelperView.Location = new System.Drawing.Point(0, 0);
            this.backTranslationHelperView.Name = "backTranslationHelperView";
            this.backTranslationHelperView.Size = new System.Drawing.Size(1057, 574);
            this.backTranslationHelperView.TabIndex = 0;
            // 
            // BackTranslationHelperWebForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1057, 574);
            this.Controls.Add(this.backTranslationHelperView);
            this.Name = "BackTranslationHelperWebForm";
            this.Text = "BackTranslationHelperWebForm";
            this.ResumeLayout(false);

        }

        #endregion

        private BackTranslationHelper.BackTranslationHelperView backTranslationHelperView;
    }
}