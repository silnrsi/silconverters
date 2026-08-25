using System.Windows.Forms;

namespace ClipboardEC
{
    partial class FormClipboardEncConverter
    {
        private ContextMenuStrip contextMenuStripEC;
        private System.Windows.Forms.NotifyIcon notifyIconClipboardEC;
        private System.Windows.Forms.ToolTip toolTip;
        private ProcTypeMenuItem unicodeEncodingConversionToolStripMenuItem;
        private ProcTypeMenuItem transliterationToolStripMenuItem;
        private ProcTypeMenuItem icuTransliterationToolStripMenuItem;
        private ProcTypeMenuItem icuRegularExpressionToolStripMenuItem;
        private ProcTypeMenuItem icuConverterToolStripMenuItem;
        private ProcTypeMenuItem codePageToolStripMenuItem;
        private ProcTypeMenuItem nonUnicodeEncodingConversionToolStripMenuItem;
        private ProcTypeMenuItem pythonScriptToolStripMenuItem;
        private ProcTypeMenuItem spellingFixerProjectToolStripMenuItem;
        private ProcTypeMenuItem perlExpressionToolStripMenuItem;
        private ProcTypeMenuItem spare1userdefinableToolStripMenuItem;
        private ProcTypeMenuItem spare2userdefinableToolStripMenuItem;
        private ToolStripMenuItem normalizationToolStripMenuItem;
        private ToolStripMenuItem noneToolStripMenuItem;
        private ToolStripMenuItem composedToolStripMenuItem;
        private ToolStripMenuItem decomposedToolStripMenuItem;
        private ToolStripMenuItem forwardToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem previewToolStripMenuItem;
        private ToolStripMenuItem debugToolStripMenuItem;
        private ToolStripMenuItem filteringToolStripMenuItem;
        internal ToolStripMenuItem byTransductionTypeToolStripMenuItem;
        internal ToolStripMenuItem byImplementationTypeToolStripMenuItem;
        internal ToolStripMenuItem byEncodingToolStripMenuItem;
        internal ToolStripMenuItem showAllTransductionTypesToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripMenuItem launchSILConvertersSetupToolStripMenuItem;
        private ToolStripMenuItem addConverterToolStripMenuItem;
        private ToolStripMenuItem editOrDeleteConverterToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem spellFixerToolStripMenuItem;
        private ToolStripMenuItem translationHelperFormToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator9;
        private ToolStripMenuItem addTranslatorSetToolStripMenuItem;
        private ToolStripMenuItem deleteTranslatorSetToolStripMenuItem;
        private ToolStripMenuItem resetToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator5;
        private ToolStripMenuItem displaySpellingFixToolStripMenuItem;
        private ToolStripMenuItem editSpellingFixesToolStripMenuItem;
        private ToolStripMenuItem editDictionaryToolStripMenuItem;
        private ToolStripMenuItem selectProjectToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator6;
        private ToolStripMenuItem consistentSpellingFixerToolStripMenuItem;
        private ToolStripMenuItem legacySpellFixerToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator7;
        private ToolStripSeparator toolStripSeparator8;
        private System.ComponentModel.IContainer components;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // this supposedly makes the icon go away on exit (otherwise, it seems to take
                //  until you move the cursor over it).
                if (this.notifyIconClipboardEC != null)
                    this.notifyIconClipboardEC.Dispose();

                if (components != null)
                {
                    components.Dispose();
                }
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormClipboardEncConverter));
            this.notifyIconClipboardEC = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStripEC = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.normalizationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.noneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.decomposedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.composedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.forwardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.previewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debugToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.filteringToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.byTransductionTypeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showAllTransductionTypesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.unicodeEncodingConversionToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.transliterationToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.icuTransliterationToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.icuRegularExpressionToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.icuConverterToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.codePageToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.nonUnicodeEncodingConversionToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.pythonScriptToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.spellingFixerProjectToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.perlExpressionToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.spare1userdefinableToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.spare2userdefinableToolStripMenuItem = new ClipboardEC.ProcTypeMenuItem();
            this.byImplementationTypeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.byEncodingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.launchSILConvertersSetupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addConverterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editOrDeleteConverterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.translationHelperFormToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addTranslatorSetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteTranslatorSetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator9 = new System.Windows.Forms.ToolStripSeparator();
            this.spellFixerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.selectProjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
            this.displaySpellingFixToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editSpellingFixesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.resetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.consistentSpellingFixerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.legacySpellFixerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.editDictionaryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.contextMenuStripEC.SuspendLayout();
            this.SuspendLayout();
            //
            // notifyIconClipboardEC
            //
            this.notifyIconClipboardEC.ContextMenuStrip = this.contextMenuStripEC;
            this.notifyIconClipboardEC.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIconClipboardEC.Icon")));
            this.notifyIconClipboardEC.Text = "Loading... Please wait...";
            this.notifyIconClipboardEC.Visible = true;
            //
            // contextMenuStripEC
            //
            this.contextMenuStripEC.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripSeparator1,
            this.normalizationToolStripMenuItem,
            this.forwardToolStripMenuItem,
            this.toolStripSeparator2,
            this.previewToolStripMenuItem,
            this.debugToolStripMenuItem,
            this.filteringToolStripMenuItem,
            this.toolStripSeparator3,
            this.launchSILConvertersSetupToolStripMenuItem,
            this.addConverterToolStripMenuItem,
            this.editOrDeleteConverterToolStripMenuItem,
            this.toolStripSeparator5,
            this.translationHelperFormToolStripMenuItem,
            this.spellFixerToolStripMenuItem,
            this.toolStripSeparator4,
            this.exitToolStripMenuItem});
            this.contextMenuStripEC.Name = "contextMenuStripEC";
            this.contextMenuStripEC.Size = new System.Drawing.Size(213, 298);
            this.contextMenuStripEC.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStripEC_Opening);
            //
            // toolStripSeparator1
            //
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(209, 6);
            //
            // normalizationToolStripMenuItem
            //
            this.normalizationToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.noneToolStripMenuItem,
            this.decomposedToolStripMenuItem,
            this.composedToolStripMenuItem});
            this.normalizationToolStripMenuItem.Name = "normalizationToolStripMenuItem";
            this.normalizationToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.normalizationToolStripMenuItem.Text = "&Normalization";
            this.normalizationToolStripMenuItem.ToolTipText = "Unicode Normalization Forms for the output of the conversion";
            //
            // noneToolStripMenuItem
            //
            this.noneToolStripMenuItem.Checked = true;
            this.noneToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.noneToolStripMenuItem.Name = "noneToolStripMenuItem";
            this.noneToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.noneToolStripMenuItem.Text = "N&one";
            this.noneToolStripMenuItem.ToolTipText = "Output of the conversion is returned as is (no change)";
            //
            // decomposedToolStripMenuItem
            //
            this.decomposedToolStripMenuItem.Name = "decomposedToolStripMenuItem";
            this.decomposedToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.decomposedToolStripMenuItem.Text = "&Decomposed";
            this.decomposedToolStripMenuItem.ToolTipText = "Output of the conversion is returned in Unicode Normalization Form Decomposed";
            //
            // composedToolStripMenuItem
            //
            this.composedToolStripMenuItem.Name = "composedToolStripMenuItem";
            this.composedToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.composedToolStripMenuItem.Text = "&Composed";
            this.composedToolStripMenuItem.ToolTipText = "Output of the conversion is returned in Unicode Normalization Form Composed";
            //
            // forwardToolStripMenuItem
            //
            this.forwardToolStripMenuItem.Checked = true;
            this.forwardToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.forwardToolStripMenuItem.Name = "forwardToolStripMenuItem";
            this.forwardToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.forwardToolStripMenuItem.Text = "&Forward";
            this.forwardToolStripMenuItem.ToolTipText = "Specifies the direction of the conversion (checked=Forward)";
            //
            // toolStripSeparator2
            //
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(209, 6);
            //
            // previewToolStripMenuItem
            //
            this.previewToolStripMenuItem.Name = "previewToolStripMenuItem";
            this.previewToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.previewToolStripMenuItem.Text = "&Preview";
            this.previewToolStripMenuItem.ToolTipText = "Specifies whether or not to show a preview of the conversion (checked=Yes)";
            this.previewToolStripMenuItem.Click += new System.EventHandler(this.previewToolStripMenuItem_Click);
            //
            // debugToolStripMenuItem
            //
            this.debugToolStripMenuItem.Name = "debugToolStripMenuItem";
            this.debugToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.debugToolStripMenuItem.Text = "&Debug";
            this.debugToolStripMenuItem.ToolTipText = "Specifies whether to display debug information sent to/received from the underlyi" +
    "ng conversion engine";
            this.debugToolStripMenuItem.Click += new System.EventHandler(this.debugToolStripMenuItem_Click);
            //
            // filteringToolStripMenuItem
            //
            this.filteringToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.byTransductionTypeToolStripMenuItem,
            this.byImplementationTypeToolStripMenuItem,
            this.byEncodingToolStripMenuItem});
            this.filteringToolStripMenuItem.Name = "filteringToolStripMenuItem";
            this.filteringToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.filteringToolStripMenuItem.Text = "&Filtering";
            this.filteringToolStripMenuItem.ToolTipText = "Allows you to filter the list of converters (to reduce processing time)";
            //
            // byTransductionTypeToolStripMenuItem
            //
            this.byTransductionTypeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showAllTransductionTypesToolStripMenuItem,
            this.unicodeEncodingConversionToolStripMenuItem,
            this.transliterationToolStripMenuItem,
            this.icuTransliterationToolStripMenuItem,
            this.icuRegularExpressionToolStripMenuItem,
            this.icuConverterToolStripMenuItem,
            this.codePageToolStripMenuItem,
            this.nonUnicodeEncodingConversionToolStripMenuItem,
            this.pythonScriptToolStripMenuItem,
            this.spellingFixerProjectToolStripMenuItem,
            this.perlExpressionToolStripMenuItem,
            this.spare1userdefinableToolStripMenuItem,
            this.spare2userdefinableToolStripMenuItem});
            this.byTransductionTypeToolStripMenuItem.Name = "byTransductionTypeToolStripMenuItem";
            this.byTransductionTypeToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.byTransductionTypeToolStripMenuItem.Text = "By &Transduction Type";
            //
            // showAllTransductionTypesToolStripMenuItem
            //
            this.showAllTransductionTypesToolStripMenuItem.Name = "showAllTransductionTypesToolStripMenuItem";
            this.showAllTransductionTypesToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.showAllTransductionTypesToolStripMenuItem.Text = "Show All Transduction Types";
            this.showAllTransductionTypesToolStripMenuItem.Click += new System.EventHandler(this.showAllTransductionTypesToolStripMenuItem_Click);
            //
            // unicodeEncodingConversionToolStripMenuItem
            //
            this.unicodeEncodingConversionToolStripMenuItem.Name = "unicodeEncodingConversionToolStripMenuItem";
            this.unicodeEncodingConversionToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.unicodeEncodingConversionToolStripMenuItem.Text = "Unicode Encoding Conversion";
            //
            // transliterationToolStripMenuItem
            //
            this.transliterationToolStripMenuItem.Name = "transliterationToolStripMenuItem";
            this.transliterationToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.transliterationToolStripMenuItem.Text = "Transliteration";
            //
            // icuTransliterationToolStripMenuItem
            //
            this.icuTransliterationToolStripMenuItem.Name = "icuTransliterationToolStripMenuItem";
            this.icuTransliterationToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.icuTransliterationToolStripMenuItem.Text = "ICU Transliteration";
            //
            // icuRegularExpressionToolStripMenuItem
            //
            this.icuRegularExpressionToolStripMenuItem.Name = "icuRegularExpressionToolStripMenuItem";
            this.icuRegularExpressionToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.icuRegularExpressionToolStripMenuItem.Text = "ICU Regular Expression";
            //
            // icuConverterToolStripMenuItem
            //
            this.icuConverterToolStripMenuItem.Name = "icuConverterToolStripMenuItem";
            this.icuConverterToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.icuConverterToolStripMenuItem.Text = "ICU Converter";
            //
            // codePageToolStripMenuItem
            //
            this.codePageToolStripMenuItem.Name = "codePageToolStripMenuItem";
            this.codePageToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.codePageToolStripMenuItem.Text = "Code Page";
            //
            // nonUnicodeEncodingConversionToolStripMenuItem
            //
            this.nonUnicodeEncodingConversionToolStripMenuItem.Name = "nonUnicodeEncodingConversionToolStripMenuItem";
            this.nonUnicodeEncodingConversionToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.nonUnicodeEncodingConversionToolStripMenuItem.Text = "Non-Unicode Encoding Conversion";
            //
            // pythonScriptToolStripMenuItem
            //
            this.pythonScriptToolStripMenuItem.Name = "pythonScriptToolStripMenuItem";
            this.pythonScriptToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.pythonScriptToolStripMenuItem.Text = "Python script";
            //
            // spellingFixerProjectToolStripMenuItem
            //
            this.spellingFixerProjectToolStripMenuItem.Name = "spellingFixerProjectToolStripMenuItem";
            this.spellingFixerProjectToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.spellingFixerProjectToolStripMenuItem.Text = "Spelling Fixer Project";
            //
            // perlExpressionToolStripMenuItem
            //
            this.perlExpressionToolStripMenuItem.Name = "perlExpressionToolStripMenuItem";
            this.perlExpressionToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.perlExpressionToolStripMenuItem.Text = "Perl Expression";
            //
            // spare1userdefinableToolStripMenuItem
            //
            this.spare1userdefinableToolStripMenuItem.Name = "spare1userdefinableToolStripMenuItem";
            this.spare1userdefinableToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.spare1userdefinableToolStripMenuItem.Text = "Spare 1 (user-definable)";
            //
            // spare2userdefinableToolStripMenuItem
            //
            this.spare2userdefinableToolStripMenuItem.Name = "spare2userdefinableToolStripMenuItem";
            this.spare2userdefinableToolStripMenuItem.Size = new System.Drawing.Size(262, 22);
            this.spare2userdefinableToolStripMenuItem.Text = "Spare 2 (user-definable)";
            //
            // byImplementationTypeToolStripMenuItem
            //
            this.byImplementationTypeToolStripMenuItem.Name = "byImplementationTypeToolStripMenuItem";
            this.byImplementationTypeToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.byImplementationTypeToolStripMenuItem.Text = "By &Implementation Type";
            //
            // byEncodingToolStripMenuItem
            //
            this.byEncodingToolStripMenuItem.Name = "byEncodingToolStripMenuItem";
            this.byEncodingToolStripMenuItem.Size = new System.Drawing.Size(202, 22);
            this.byEncodingToolStripMenuItem.Text = "By &Encoding";
            //
            // toolStripSeparator3
            //
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(209, 6);
            //
            // launchSILConvertersSetupToolStripMenuItem
            //
            this.launchSILConvertersSetupToolStripMenuItem.Name = "launchSILConvertersSetupToolStripMenuItem";
            this.launchSILConvertersSetupToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.launchSILConvertersSetupToolStripMenuItem.Text = "&Launch Converter Installer";
            this.launchSILConvertersSetupToolStripMenuItem.ToolTipText = "Click here to launch the Converter Installer";
            this.launchSILConvertersSetupToolStripMenuItem.Click += new System.EventHandler(this.launchSILConvertersSetupToolStripMenuItem_Click);
            //
            // addConverterToolStripMenuItem
            //
            this.addConverterToolStripMenuItem.Name = "addConverterToolStripMenuItem";
            this.addConverterToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.addConverterToolStripMenuItem.Text = "&Add Converter";
            this.addConverterToolStripMenuItem.ToolTipText = "Click here to add a new converter to the list";
            this.addConverterToolStripMenuItem.Click += new System.EventHandler(this.addConverterToolStripMenuItem_Click);
            //
            // editOrDeleteConverterToolStripMenuItem
            //
            this.editOrDeleteConverterToolStripMenuItem.Name = "editOrDeleteConverterToolStripMenuItem";
            this.editOrDeleteConverterToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.editOrDeleteConverterToolStripMenuItem.Text = "Ed&it or Delete Converter";
            this.editOrDeleteConverterToolStripMenuItem.ToolTipText = "Click here to bring up the Choose Converter dialog from which you can right-click" +
    " on a converter to edit or delete it";
            this.editOrDeleteConverterToolStripMenuItem.Click += new System.EventHandler(this.editOrDeleteConverterToolStripMenuItem_Click);
            //
            // toolStripSeparator5
            //
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(209, 6);
            //
            // translationHelperFormToolStripMenuItem
            //
            this.translationHelperFormToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripSeparator9,
            this.addTranslatorSetToolStripMenuItem,
            this.deleteTranslatorSetToolStripMenuItem});
            this.translationHelperFormToolStripMenuItem.Name = "translationHelperFormToolStripMenuItem";
            this.translationHelperFormToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.translationHelperFormToolStripMenuItem.Text = "&Translation Helper";
            this.translationHelperFormToolStripMenuItem.ToolTipText = "Click to process the clipboard data through the last Translation Helper Dialog used (or choose 'Add' in the sub-menu to add one)";
            this.translationHelperFormToolStripMenuItem.DropDownOpening += new System.EventHandler(this.translationHelperFormToolStripMenuItem_DropDownOpening);
            this.translationHelperFormToolStripMenuItem.Click += TranslationHelperFormToolStripMenuItem_Click;
            //
            // spellFixerToolStripMenuItem
            //
            this.spellFixerToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectProjectToolStripMenuItem,
            this.toolStripSeparator8,
            this.displaySpellingFixToolStripMenuItem,
            this.editSpellingFixesToolStripMenuItem,
            this.toolStripSeparator7,
            this.resetToolStripMenuItem});
            this.spellFixerToolStripMenuItem.Name = "spellFixerToolStripMenuItem";
            this.spellFixerToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.spellFixerToolStripMenuItem.Text = "&Spell Fixer";
            this.spellFixerToolStripMenuItem.ToolTipText = "SpellFixer options";
            this.spellFixerToolStripMenuItem.DropDownOpening += new System.EventHandler(this.spellFixerToolStripMenuItem_DropDownOpening);
            //
            // selectProjectToolStripMenuItem
            //
            this.selectProjectToolStripMenuItem.Name = "selectProjectToolStripMenuItem";
            this.selectProjectToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.selectProjectToolStripMenuItem.Text = "&Select Project";
            this.selectProjectToolStripMenuItem.ToolTipText = "Load a SpellFixer project to work with";
            this.selectProjectToolStripMenuItem.Click += new System.EventHandler(this.selectProjectToolStripMenuItem_Click);
            //
            // toolStripSeparator8
            //
            this.toolStripSeparator8.Name = "toolStripSeparator8";
            this.toolStripSeparator8.Size = new System.Drawing.Size(177, 6);
            //
            // displaySpellingFixToolStripMenuItem
            //
            this.displaySpellingFixToolStripMenuItem.Name = "displaySpellingFixToolStripMenuItem";
            this.displaySpellingFixToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.displaySpellingFixToolStripMenuItem.Text = "&Display Spelling Fix";
            this.displaySpellingFixToolStripMenuItem.ToolTipText = "Click to search the database for a spelling fix for the word on the clipboard";
            this.displaySpellingFixToolStripMenuItem.Click += new System.EventHandler(this.displaySpellingFixToolStripMenuItem_Click);
            //
            // editSpellingFixesToolStripMenuItem
            //
            this.editSpellingFixesToolStripMenuItem.Name = "editSpellingFixesToolStripMenuItem";
            this.editSpellingFixesToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.editSpellingFixesToolStripMenuItem.Text = "&Edit Spelling Fixes";
            this.editSpellingFixesToolStripMenuItem.ToolTipText = "Click to edit the spelling fix database in a grid editor";
            this.editSpellingFixesToolStripMenuItem.Click += new System.EventHandler(this.editSpellingFixesToolStripMenuItem_Click);
            //
            // toolStripSeparator7
            //
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(177, 6);
            //
            // resetToolStripMenuItem
            //
            this.resetToolStripMenuItem.Name = "resetToolStripMenuItem";
            this.resetToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.resetToolStripMenuItem.Text = "&Reset";
            this.resetToolStripMenuItem.ToolTipText = "Click to turn off SpellFixer mode";
            this.resetToolStripMenuItem.Click += new System.EventHandler(this.resetToolStripMenuItem_Click);
            //
            // toolStripSeparator4
            //
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(209, 6);
            //
            // exitToolStripMenuItem
            //
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(212, 22);
            this.exitToolStripMenuItem.Text = "&Exit";
            this.exitToolStripMenuItem.ToolTipText = "Click to exit the Clipboard EncConverter";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.menuItemExit_Click);
            //
            // consistentSpellingFixerToolStripMenuItem
            //
            this.consistentSpellingFixerToolStripMenuItem.Name = "consistentSpellingFixerToolStripMenuItem";
            this.consistentSpellingFixerToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.consistentSpellingFixerToolStripMenuItem.Text = "&Consistent Spelling Fixer";
            this.consistentSpellingFixerToolStripMenuItem.ToolTipText = "Click to load a Consistent Spelling Fixer project (for whole word spelling fixes)" +
    "";
            //
            // legacySpellFixerToolStripMenuItem
            //
            this.legacySpellFixerToolStripMenuItem.Name = "legacySpellFixerToolStripMenuItem";
            this.legacySpellFixerToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.legacySpellFixerToolStripMenuItem.Text = "&Legacy SpellFixer";
            this.legacySpellFixerToolStripMenuItem.ToolTipText = "Click to load a Legacy Spell Fixer project (supports partial word spelling change" +
    "s)";
            //
            // toolStripSeparator6
            //
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new System.Drawing.Size(161, 6);
            //
            // editDictionaryToolStripMenuItem
            //
            this.editDictionaryToolStripMenuItem.Name = "editDictionaryToolStripMenuItem";
            this.editDictionaryToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
            this.editDictionaryToolStripMenuItem.Text = "Edit &Dictionary";
            this.editDictionaryToolStripMenuItem.ToolTipText = "Click to edit the list of known good spellings in a list editor";
            //
            // toolTip
            //
            this.toolTip.ShowAlways = true;
            //
            // addTranslatorSetToolStripMenuItem
            //
            this.addTranslatorSetToolStripMenuItem.Name = "addTranslatorSetToolStripMenuItem";
            this.addTranslatorSetToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.addTranslatorSetToolStripMenuItem.Text = "&Add Translator Set";
            this.addTranslatorSetToolStripMenuItem.Click += new System.EventHandler(this.addTranslatorSetToolStripMenuItem_Click);
            //
            // deleteTranslatorSetToolStripMenuItem
            //
            this.deleteTranslatorSetToolStripMenuItem.Name = "deleteTranslatorSetToolStripMenuItem";
            this.deleteTranslatorSetToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.deleteTranslatorSetToolStripMenuItem.Text = "&Delete Translator Set";
            this.deleteTranslatorSetToolStripMenuItem.Click += new System.EventHandler(this.deleteTranslatorSetToolStripMenuItem_Click);
            //
            // toolStripSeparator9
            //
            this.toolStripSeparator9.Name = "toolStripSeparator9";
            this.toolStripSeparator9.Size = new System.Drawing.Size(177, 6);
            //
            // FormClipboardEncConverter
            //
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.CausesValidation = false;
            this.ClientSize = new System.Drawing.Size(166, 253);
            this.ControlBox = false;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormClipboardEncConverter";
            this.Opacity = 0D;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.contextMenuStripEC.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion
    }
}
