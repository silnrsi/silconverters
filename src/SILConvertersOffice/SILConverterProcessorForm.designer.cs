namespace SILConvertersOffice
{
    internal partial class SILConverterProcessorForm
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
            this.tableLayoutPanelDebugRefresh = new System.Windows.Forms.TableLayoutPanel();
            this.buttonViewRule = new System.Windows.Forms.Button();
            this.buttonAddRule = new System.Windows.Forms.Button();
            this.buttonDebug = new System.Windows.Forms.Button();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.tableLayoutPanelDebugRefresh.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelDebugRefresh
            // 
            this.tableLayoutPanelDebugRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelDebugRefresh.ColumnCount = 2;
            this.tableLayoutPanelDebugRefresh.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelDebugRefresh.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelDebugRefresh.Controls.Add(this.buttonViewRule, 0, 0);
            this.tableLayoutPanelDebugRefresh.Controls.Add(this.buttonAddRule, 1, 0);
            this.tableLayoutPanelDebugRefresh.Controls.Add(this.buttonDebug, 1, 2);
            this.tableLayoutPanelDebugRefresh.Controls.Add(this.buttonRefresh, 0, 2);
            this.tableLayoutPanelDebugRefresh.Location = new System.Drawing.Point(477, 148);
            this.tableLayoutPanelDebugRefresh.Name = "tableLayoutPanelDebugRefresh";
            this.tableLayoutPanelDebugRefresh.RowCount = 3;
            this.tableLayoutPanelDebugRefresh.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelDebugRefresh.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelDebugRefresh.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelDebugRefresh.Size = new System.Drawing.Size(184, 60);
            this.tableLayoutPanelDebugRefresh.TabIndex = 12;
            // 
            // buttonViewRule
            // 
            this.buttonViewRule.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonViewRule.Location = new System.Drawing.Point(3, 3);
            this.buttonViewRule.Name = "buttonViewRule";
            this.buttonViewRule.Size = new System.Drawing.Size(86, 23);
            this.buttonViewRule.TabIndex = 2;
            this.buttonViewRule.Text = "&View Rule";
            this.buttonViewRule.UseVisualStyleBackColor = true;
            this.buttonViewRule.Click += new System.EventHandler(this.buttonViewRule_Click);
            // 
            // buttonAddRule
            // 
            this.buttonAddRule.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonAddRule.Location = new System.Drawing.Point(95, 3);
            this.buttonAddRule.Name = "buttonAddRule";
            this.buttonAddRule.Size = new System.Drawing.Size(86, 23);
            this.buttonAddRule.TabIndex = 3;
            this.buttonAddRule.Text = "A&dd Rule";
            this.toolTip.SetToolTip(this.buttonAddRule, "Click to add a new rule");
            this.buttonAddRule.UseVisualStyleBackColor = true;
            this.buttonAddRule.Click += new System.EventHandler(this.buttonAddRule_Click);
            // 
            // buttonDebug
            // 
            this.buttonDebug.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonDebug.Location = new System.Drawing.Point(95, 32);
            this.buttonDebug.Name = "buttonDebug";
            this.buttonDebug.Size = new System.Drawing.Size(86, 25);
            this.buttonDebug.TabIndex = 1;
            this.buttonDebug.Text = "&Debug";
            this.toolTip.SetToolTip(this.buttonDebug, "Click here to re-run the conversions and show feedback at each step of the conver" +
        "sion process");
            this.buttonDebug.UseVisualStyleBackColor = true;
            this.buttonDebug.Click += new System.EventHandler(this.buttonDebug_Click);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRefresh.Location = new System.Drawing.Point(3, 32);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(86, 25);
            this.buttonRefresh.TabIndex = 0;
            this.buttonRefresh.Text = "Refre&sh";
            this.toolTip.SetToolTip(this.buttonRefresh, "Click here to re-run the conversion processes (e.g. if you changed the underlying" +
        " conversion table)");
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // SILConverterProcessorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.ClientSize = new System.Drawing.Size(679, 223);
            this.Controls.Add(this.tableLayoutPanelDebugRefresh);
            this.Name = "SILConverterProcessorForm";
            this.Controls.SetChildIndex(this.tableLayoutPanelDebugRefresh, 0);
            this.tableLayoutPanelDebugRefresh.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        protected internal System.Windows.Forms.TableLayoutPanel tableLayoutPanelDebugRefresh;
        private System.Windows.Forms.Button buttonViewRule;
        private System.Windows.Forms.Button buttonAddRule;
        private System.Windows.Forms.Button buttonDebug;
        private System.Windows.Forms.Button buttonRefresh;
    }
}
