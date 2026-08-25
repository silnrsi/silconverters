// #define TurnOffCsc30   // turn off CSC30 features

using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Diagnostics;       // for Debug
using Microsoft.Win32;          // for RegistryKey
using ECInterfaces;
using SilEncConverters40;
using System.IO;                // for FileInfo
using System.Text;              // for Encoding
using System.Runtime.InteropServices;   // DllImport
using System.Collections.Generic;

#if !TurnOffCsc30
using SpellingFixer30;
#else
// if we add a reference to SpellFixerEC assembly (in order to call it), then the SpellFixerEC
//  assembly must exist in the local folder. But I don't want to *require* it to have been installed.
//  If it isn't loadable, then this app fatal excepts (since my installer doesn't include the SF assembly)
//  There is a way to call SF (if installed) by using reflection. So, do that for now. If we ever decide
//  to ship ClipboardEC with a SpellFixer merge module, then we can define 'IncludeSpellFixer'
//  to get the real thing (of course, after having added a reference to it).
//  Otherwise, I've hacked a wrapper to call it and then we don't require it to be present.
#if IncludeSpellFixer
using SpellingFixerEC;          // to access SpellFixer (if adding a reference to it)
#else
#endif
#endif

// put this in 'HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run' to get it to run
//  in the system tray at startup
namespace ClipboardEC
{
    /// <summary>
    /// ClipboardEncConverter: convert the text on the clipboard using one of the system converters.
    /// </summary>
    public partial class FormClipboardEncConverter : System.Windows.Forms.Form
    {
        public const string cstrCaption = "Clipboard EncConverter";
        public const string  cstrProjectMemoryKey = @"SOFTWARE\SIL\SilEncConverters40\ClipboardEC";
        private string  cstrProjectShowPreviewLastState = "LastShowPreviewState";
        private string  cstrProjectDebugModeLastState = "LastDebugModeState";
        public const string  cstrProjectImplTypeFilterLastState = "LastImplTypeFilterState";
        public const string  cstrProjectTransTypeFilterLastState = "LastProcessTypeFilterState";
        public const string  cstrProjectEncodingFilterLastState = "LastEncodingFilterState";
        internal const string cstrSpellFixerProgID = "SpellingFixerEC.SpellingFixerEC";
        internal const string cstrProjectEncodingFilterOffDisplayString = "Show All Encoding IDs";

        private bool _windowInitialised = false;

        [DllImport("user32", SetLastError=true)]
        static extern bool IsGUIThread(bool bConvert);

        public delegate void FakeDelegate();
        public FormClipboardEncConverter()
        {
            // from the website: http://forums.msdn.microsoft.com/en-US/netfxbcl/thread/fb267827-1765-4bd9-ae2f-0abbd5a2ae22/
            //  the following snippet is supposed to get rid of the .NET-BroadcastEventWindow fatal exception when the process
            //  is ended.
            if (!_windowInitialised && IsGUIThread(false))
            {
                Microsoft.Win32.SystemEvents.InvokeOnEventsThread(new FakeDelegate(delegate()
                {
                    ;   // noop
                }));
                _windowInitialised = true;
            }

            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            CreateContextMenu();

            this.noneToolStripMenuItem.Click += new EventHandler(NormalizeClick);
            this.composedToolStripMenuItem.Click += new EventHandler(NormalizeClick);
            this.decomposedToolStripMenuItem.Click += new EventHandler(NormalizeClick);

            this.forwardToolStripMenuItem.Checked = true;
            this.forwardToolStripMenuItem.Click += new EventHandler(DirectionForwardClick);

            this.notifyIconClipboardEC.MouseUp += new MouseEventHandler(notifyIconClipboardEC_MouseUp);

            RegistryKey keyLastState = Registry.CurrentUser.OpenSubKey(cstrProjectMemoryKey);
            if (keyLastState != null)
            {
                try
                {
                    this.ShowPreview = ((int)keyLastState.GetValue(cstrProjectShowPreviewLastState) != 0);
                }
                catch { }
                this.previewToolStripMenuItem.Checked = ShowPreview;

                try
                {
                    this.DebugState = ((int)keyLastState.GetValue(cstrProjectDebugModeLastState) != 0);
                }
                catch { }
                this.debugToolStripMenuItem.Checked = DebugState;

                try
                {
                    this.ImplTypeFilter = (string)keyLastState.GetValue(cstrProjectImplTypeFilterLastState);
                }
                catch { }

                try
                {
                    this.EncodingFilter = (string)keyLastState.GetValue(cstrProjectEncodingFilterLastState);
                }
                catch { }

                try
                {
                    Int32 lValue = (Int32)keyLastState.GetValue(cstrProjectTransTypeFilterLastState);
                    this.ProcessTypeFilter = (ProcessTypeFlags)lValue;
                }
                catch { }
            }

            this.showAllTransductionTypesToolStripMenuItem.Checked = (this.ProcessTypeFilter == ProcessTypeFlags.DontKnow);

            this.unicodeEncodingConversionToolStripMenuItem.InitializeComponent(ProcessTypeFlags.UnicodeEncodingConversion, this);
            this.transliterationToolStripMenuItem.InitializeComponent(ProcessTypeFlags.Transliteration, this);
            this.icuTransliterationToolStripMenuItem.InitializeComponent(ProcessTypeFlags.ICUTransliteration, this);
            this.icuRegularExpressionToolStripMenuItem.InitializeComponent(ProcessTypeFlags.ICURegularExpression, this);
            this.icuConverterToolStripMenuItem.InitializeComponent(ProcessTypeFlags.ICUConverter, this);
            this.codePageToolStripMenuItem.InitializeComponent(ProcessTypeFlags.CodePageConversion, this);
            this.nonUnicodeEncodingConversionToolStripMenuItem.InitializeComponent(ProcessTypeFlags.NonUnicodeEncodingConversion, this);
            this.pythonScriptToolStripMenuItem.InitializeComponent(ProcessTypeFlags.PythonScript, this);
            this.spellingFixerProjectToolStripMenuItem.InitializeComponent(ProcessTypeFlags.SpellingFixerProject, this);
            this.perlExpressionToolStripMenuItem.InitializeComponent(ProcessTypeFlags.PerlExpression, this);
            this.spare1userdefinableToolStripMenuItem.InitializeComponent(ProcessTypeFlags.UserDefinedSpare1, this);
            this.spare2userdefinableToolStripMenuItem.InitializeComponent(ProcessTypeFlags.UserDefinedSpare2, this);

            UpdateFilteringIndication();

#if !TurnOffCsc30
            this.notifyIconClipboardEC.Text = "Right-click: system converter; Left-click: Consistent Spelling";
#else
            UpdateIconText();
#endif

            if (Properties.Settings.Default.TranslatorSetsProjectNames == null)
                Properties.Settings.Default.TranslatorSetsProjectNames = new System.Collections.Specialized.StringCollection();
        }

        public void UpdateFilteringIndication()
        {
            // to avoid support problems, make it more clear when filtering is happening
            if(     this.showAllTransductionTypesToolStripMenuItem.Checked
                &&  String.IsNullOrEmpty(this.ImplTypeFilter)
                &&  String.IsNullOrEmpty(this.EncodingFilter))
            {
                this.filteringToolStripMenuItem.Text = "&Filtering";
            }
            else
            {
                this.filteringToolStripMenuItem.Text = "&Filtering (on)";
            }
        }

        public void UpdateToolTip(Control ctrl, string sTip)
        {
            toolTip.SetToolTip(ctrl, sTip);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.Run(new FormClipboardEncConverter());
        }

        private const ProcessTypeFlags  constAllProcessTypes =
            (
            ProcessTypeFlags.UnicodeEncodingConversion |
            ProcessTypeFlags.Transliteration |
            ProcessTypeFlags.ICUTransliteration	|
            ProcessTypeFlags.ICUConverter |
            ProcessTypeFlags.CodePageConversion |
            ProcessTypeFlags.NonUnicodeEncodingConversion |
            ProcessTypeFlags.SpellingFixerProject |
            ProcessTypeFlags.ICURegularExpression |
            ProcessTypeFlags.PythonScript |
            ProcessTypeFlags.PerlExpression |
            ProcessTypeFlags.UserDefinedSpare1 |
            ProcessTypeFlags.UserDefinedSpare2
            );

        private bool    m_bShowPreview = true;
        public  bool    ShowPreview
        {
            get { return m_bShowPreview; }
            set { m_bShowPreview = value; }
        }

        private bool    m_bDebugState = false;
        public  bool    DebugState
        {
            get { return m_bDebugState; }
            set { m_bDebugState = value; }
        }

        private ProcessTypeFlags m_lProcessTypeFilter = ProcessTypeFlags.DontKnow;
        public ProcessTypeFlags ProcessTypeFilter
        {
            get { return m_lProcessTypeFilter; }
            set { m_lProcessTypeFilter = value; }
        }

        private string m_strImplTypeFilter = null;
        public string ImplTypeFilter
        {
            get { return m_strImplTypeFilter; }
            set { m_strImplTypeFilter = value; }
        }

        private string m_strEncodingFilter = null;
        public string EncodingFilter
        {
            get { return m_strEncodingFilter; }
            set { m_strEncodingFilter = value; }
        }

        private DateTime        m_timeModified = DateTime.MinValue;
        private EncConverters   m_aECs = null;
        public EncConverters    GetEncConverters
        {
            get
            {
                DateTime timeModified = DateTime.MinValue;
                if(     (   (DoesFileExist(EncConverters.GetRepositoryFileName(), ref timeModified))
                        &&  (timeModified > m_timeModified)
                        )
                    ||  (m_aECs == null)
                    )
                {
                    m_aECs = new EncConverters();

                    // keep track of the modified date, so we can detect a new version to reload
                    m_timeModified = timeModified;
                }

                return m_aECs;
            }
        }

        protected bool DoesFileExist(string strFileName, ref DateTime TimeModified)
        {
            bool bRet = true;

            try
            {
                FileInfo fi = new FileInfo(strFileName);
                TimeModified = fi.LastWriteTime;
                bRet = fi.Exists;
            }
            catch
            {
                bRet = false;
            }

            return bRet;
        }

        private void contextMenuStripEC_Opening(object sender, CancelEventArgs e)
        {
            populateImplType_Opening();
            populateByEncoding_Opening();

            this.Cursor = Cursors.WaitCursor;
            try
            {
                CreateContextMenu();
            }
            catch { }
            this.Cursor = Cursors.Default;
        }

        private const int nFixedMenuItems = 15;
        private const int cnMaxPreviewLength = 30; // max chars to show for preview (so we don't go off the screen)
        static private string strInstallerLocationRegKey    = @"SOFTWARE\SIL\SilEncConverters40\Installer";
        static private string strInstallerPathKey           = "InstallerPath";

        private void CreateContextMenu()
        {
            this.Hide();

            while( this.contextMenuStripEC.Items.Count > nFixedMenuItems )
                this.contextMenuStripEC.Items.RemoveAt(0);

            // enable or disable the Launch Setup command depending on whether we can
            // find it or not
            this.launchSILConvertersSetupToolStripMenuItem.Enabled = false;
            RegistryKey keyInstallLocation = Registry.LocalMachine.OpenSubKey(strInstallerLocationRegKey);
            if( keyInstallLocation != null )
            {
                string strInstallPath = (string)keyInstallLocation.GetValue(strInstallerPathKey);
                if(!String.IsNullOrEmpty(strInstallPath) && File.Exists(strInstallPath))
                    this.launchSILConvertersSetupToolStripMenuItem.Enabled = true;
            }

            // get the contents of the clipboard (using the correct code page, etc.)
            string strInput = null;
            if( ShowPreview )
            {
                IDataObject iData = Clipboard.GetDataObject();

                // Determines whether the data is in a format you can use.
                if( iData.GetDataPresent(DataFormats.UnicodeText) )
                {
                    strInput = (string)iData.GetData(DataFormats.UnicodeText);
                }
            }

            // filter out the list based on the three filtering mechanisms:
            //  ProcessType (e.g. UnicodeEncodingConverter)
            //  Implementation Type (e.g. SIL.tec)
            //  Encoding ID (e.g. UNICODE)
            EncConverters aECs = GetEncConverters;
            if( (ProcessTypeFilter != constAllProcessTypes) && (ProcessTypeFilter != ProcessTypeFlags.DontKnow) )
                aECs = aECs.FilterByProcessType(ProcessTypeFilter);

            if( !String.IsNullOrEmpty(this.ImplTypeFilter) )
                aECs = aECs.FilterByImplementationType(ImplTypeFilter, ProcessTypeFilter);

            if( !String.IsNullOrEmpty(this.EncodingFilter) )
                aECs = aECs.FilterByEncodingID(this.EncodingFilter, ProcessTypeFilter);

            // sort the list so they are listed alphabetically
            SortedList aSL = new SortedList(aECs.Count);
            foreach(IEncConverter aEC in aECs.Values)
            {
                string sName = aEC.Name;
                aSL.Add(sName, sName);
            }

            for(int i = aSL.Count; i-- > 0; )
            {
                string strText = (string)aSL.GetKey(i);
                string strOutput = null;
                if( /* (aEC != null) && */ ShowPreview && !String.IsNullOrEmpty(strInput) )
                {
                    try
                    {
                        IEncConverter aEC = aECs[strText];

                        // if it's a Translator type, then hold off calling to the internet for conversion
                        //  which takes time and is possibly unnecessary), put in a dummy result to see if
                        //  that's really what they want
                        if (IsOnlineTranslator(aEC))
                        {
                            strOutput = $"Click to convert w/ {aEC.Name}";
                        }
                        else
                        {
                            strOutput = this.ConvertData(aEC, strInput);
                        }
                    }
                    catch (Exception e)
                    {
                        // since this is just a 'preview' (and exceptions can be expected), don't
                        //  allow them to be throw up.
                        strOutput = String.Format("Error: {0}", e.Message);
                    }
                }

                ToolStripMenuItem menuItem = new ToolStripMenuItem(strText);
                menuItem.ToolTipText = strOutput;
                if (!String.IsNullOrEmpty(strOutput) && (strOutput.Length > cnMaxPreviewLength))
                    strOutput = strOutput.Substring(0, cnMaxPreviewLength) + "...";
                menuItem.ShortcutKeyDisplayString = strOutput;
                menuItem.Click += new EventHandler(ConverterClick);
                this.contextMenuStripEC.Items.Insert(0,menuItem);
            }
        }

        private static bool IsOnlineTranslator(IEncConverter aEC)
        {
            var isOnlineTranslator = (aEC.ProcessType & (int)ProcessTypeFlags.Translation) == (int)ProcessTypeFlags.Translation;

            // it could still be if it's a primary-fallback or daisy-chain converter that has one (but there's no interface way
            //  to get at the individual converters... :-( (and changing the interface would just break existing clients)
            if ((aEC.ImplementType == EncConverters.strTypeSILcomp) || (aEC.ImplementType == EncConverters.strTypeSILfallback))
            {
                isOnlineTranslator = true;  // for now...
            }

            return isOnlineTranslator;
        }

        private void NormalizeClick(Object sender, EventArgs e)
        {
            // Determine if clicked menu item is the Blue menu item.
            if(sender == this.noneToolStripMenuItem)
            {
                // Set the checkmark for the menuItemBlue menu item.
                this.noneToolStripMenuItem.Checked = true;
                // Uncheck the menuItemRed and menuItemGreen menu items.
                this.composedToolStripMenuItem.Checked = false;
                this.decomposedToolStripMenuItem.Checked = false;
            }
            else if(sender == this.composedToolStripMenuItem)
            {
                // Set the checkmark for the menuItemBlue menu item.
                this.composedToolStripMenuItem.Checked = true;
                // Uncheck the menuItemRed and menuItemGreen menu items.
                this.noneToolStripMenuItem.Checked = false;
                this.decomposedToolStripMenuItem.Checked = false;
            }
            else
            {
                // Set the checkmark for the menuItemBlue menu item.
                this.decomposedToolStripMenuItem.Checked = true;
                // Uncheck the menuItemRed and menuItemGreen menu items.
                this.composedToolStripMenuItem.Checked = false;
                this.noneToolStripMenuItem.Checked = false;
            }
        }

        private void DirectionForwardClick(Object sender, EventArgs e)
        {
            // toggle
            this.forwardToolStripMenuItem.Checked = !this.forwardToolStripMenuItem.Checked;
        }

        private IEncConverter _lastEncConverterUsed;

        private void ConverterClick(Object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            string strConverterName = item.Text;

            _lastEncConverterUsed = GetEncConverters[strConverterName];

            // now convert the contents of the clipboard (using the correct code page, etc.)
            IDataObject iData = Clipboard.GetDataObject();

            // Determines whether the data is in a format you can use.
            if( iData.GetDataPresent(DataFormats.UnicodeText) )
            {
                string strInput = (string)iData.GetData(DataFormats.UnicodeText);
                string strOutput = ConvertDataWithProps(_lastEncConverterUsed, strInput, DebugState);
                if( strOutput != null )
                    Clipboard.SetDataObject(strOutput);
            }
        }

        private string ConvertDataWithProps(IEncConverter aEC, string strInput, bool bDebugState)
        {
            if (aEC == null)
                return strInput;

            aEC.Debug = bDebugState;

            if( this.noneToolStripMenuItem.Checked )
                aEC.NormalizeOutput = NormalizeFlags.None;
            else if( this.composedToolStripMenuItem.Checked )
                aEC.NormalizeOutput = NormalizeFlags.FullyComposed;
            else if( this.decomposedToolStripMenuItem.Checked )
                aEC.NormalizeOutput = NormalizeFlags.FullyDecomposed;

            string strOutput = null;
            try
            {
                strOutput = ConvertData(aEC, strInput);
            }
            catch(Exception e)
            {
                // since this is just a 'preview' (and exceptions can be expected), don't
                //  allow them to be throw up.
                strOutput = "Error: " + e.Message;
            }

            aEC.Debug = false;  // so next time, we don't do debug during preview
            aEC.NormalizeOutput = NormalizeFlags.None;

            return strOutput;
        }

        private string ConvertData(IEncConverter aEC, string strInput)
        {
            bool bDirForward = this.forwardToolStripMenuItem.Checked;
            if(     !bDirForward
                &&  (   (aEC.ConversionType == ConvType.Legacy_to_Legacy)
                    ||  (aEC.ConversionType == ConvType.Legacy_to_Unicode)
                    ||  (aEC.ConversionType == ConvType.Unicode_to_Legacy)
                    ||  (aEC.ConversionType == ConvType.Unicode_to_Unicode)
                    )
            )
            {
                // these types of converters aren't reversable! So just return null
                return null;
            }

            aEC.DirectionForward = bDirForward;

            string strOutput = null;
            if (!String.IsNullOrEmpty(strInput))
            {
                // if the input to the conversion is legacy and not encoded correctly, we have
                //  to fix that up.
                if (    (   bDirForward
                        &&  (EncConverter.NormalizeLhsConversionType(aEC.ConversionType) == NormConversionType.eLegacy)
                        &&  (aEC.CodePageInput != 0)
                        &&  (aEC.CodePageInput != Encoding.Default.CodePage)
                        )
                    ||  (   !bDirForward
                        &&  (EncConverter.NormalizeRhsConversionType(aEC.ConversionType) == NormConversionType.eLegacy)
                        &&  (aEC.CodePageOutput != 0)
                        &&  (aEC.CodePageOutput != Encoding.Default.CodePage)
                        )
                )
                {
                    // we get the legacy data from the clipboard with Encoding 0 == CP_ACP (or the default code page
                    //  for this computer), but if the CodePageInput used by EncConverters is a different code page,
                    //  then this will fail.
                    //  If so, then convert it to a byte array and pass that
                    byte[] abyInput = Encoding.Default.GetBytes(strInput);
                    strInput = ECNormalizeData.ByteArrToString(abyInput);
                    EncodingForm ef = aEC.EncodingIn;
                    aEC.EncodingIn = EncodingForm.LegacyBytes;
                    strOutput = aEC.Convert(strInput);
                    aEC.EncodingIn = ef;    // reset for later (e.g. in case the user switches directions)
                }
                else
                    strOutput = aEC.Convert(strInput);

                // similarly, if the output is legacy, then if the code page used was not the same as the
                //  default code page, then we have to convert it so it'll produce the correct answer
                //  (this probably doesn't work for Legacy<>Legacy code pages)
                if (    (   bDirForward
                        &&  (EncConverter.NormalizeRhsConversionType(aEC.ConversionType) == NormConversionType.eLegacy)
                        &&  (aEC.CodePageOutput != 0)
                        &&  (aEC.CodePageOutput != Encoding.Default.CodePage)
                        )
                    || (    !bDirForward
                        &&  (EncConverter.NormalizeLhsConversionType(aEC.ConversionType) == NormConversionType.eLegacy)
                        &&  (aEC.CodePageInput != 0)
                        &&  (aEC.CodePageInput != Encoding.Default.CodePage)
                       )
                )
                {
                    int nCP = (!aEC.DirectionForward) ? aEC.CodePageInput : aEC.CodePageOutput;
                    byte[] abyOutput = EncConverters.GetBytesFromEncoding(nCP, strOutput, true);
                    strOutput = new string(Encoding.Default.GetChars(abyOutput));
                }
            }

            return strOutput;
        }

        private void menuItemExit_Click(object sender, System.EventArgs e)
        {
            this.Dispose(true);
            Application.Exit();
        }

        private void previewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowPreview = this.previewToolStripMenuItem.Checked = !this.previewToolStripMenuItem.Checked;

            // add that to the registry also, so we remember it.
            RegistryKey keyLastShowPreviewState = Registry.CurrentUser.CreateSubKey(cstrProjectMemoryKey);
            keyLastShowPreviewState.SetValue(cstrProjectShowPreviewLastState, (ShowPreview) ? 1 : 0);
        }

        private void debugToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.DebugState = this.debugToolStripMenuItem.Checked = !this.debugToolStripMenuItem.Checked;

            // add that to the registry also, so we remember it.
            RegistryKey keyLastDebugState = Registry.CurrentUser.CreateSubKey(cstrProjectMemoryKey);
            keyLastDebugState.SetValue(cstrProjectDebugModeLastState, (DebugState) ? 1 : 0);
        }

        private void launchSILConvertersSetupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // launch the Setup program (short-cut to add new converters)
            RegistryKey keyInstallLocation = Registry.LocalMachine.OpenSubKey(strInstallerLocationRegKey);
            if (keyInstallLocation != null)
            {
                string strInstallPath = (string)keyInstallLocation.GetValue(strInstallerPathKey);
                if (!String.IsNullOrEmpty(strInstallPath) && File.Exists(strInstallPath))
                {
                    LaunchProgram(strInstallPath, null);
                    return;
                }
            }

            MessageBox.Show("Unable to Launch the Converter Installer. Reinstall.", cstrCaption);
        }

        static protected void LaunchProgram(string strProgram, string strArguments)
        {
            try
            {
                Process myProcess = new Process();

                myProcess.StartInfo.FileName = strProgram;
                myProcess.StartInfo.Arguments = strArguments;
                myProcess.Start();
            }
            catch {}    // we tried...
        }

        private void addConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // call the v2.2 interface to "AutoConfigure" a converter
            string strFriendlyName = null;
            EncConverters aECs = GetEncConverters;
            aECs.AutoConfigure(ConvType.Unknown, ref strFriendlyName);
        }

        private void editOrDeleteConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EncConverters aECs = GetEncConverters;

            string strInput = null;
            IDataObject iData = Clipboard.GetDataObject();
            if (iData.GetDataPresent(DataFormats.UnicodeText))
                strInput = (string)iData.GetData(DataFormats.UnicodeText);

            aECs.AutoSelectWithData(strInput, null, ConvType.Unknown, "Right-click on Converter to Edit/Delete");
        }

        private void populateImplType_Opening()
        {
            // populate this menu with the implementations defined in the repository (but only once)
            if( this.byImplementationTypeToolStripMenuItem.DropDownItems.Count == 0 )
            {
                ImplTypeMenuItem aMenuItem = new ImplTypeMenuItem(null, "Show All Implementation Types", this);
                aMenuItem.Checked = (String.IsNullOrEmpty(this.ImplTypeFilter));
                // aMenuItem.DefaultItem = true;
                this.byImplementationTypeToolStripMenuItem.DropDownItems.Add(aMenuItem);

#if !DontUseRegistry
                string[] astrImplementationTypes, astrDisplayNames;
                GetEncConverters.GetImplementationDisplayNames(out astrImplementationTypes, out astrDisplayNames);
                int nNumImplementationTypes = astrImplementationTypes.Length;
                for (int i = 0; i < nNumImplementationTypes; i++)
                {
                    string sImplementType = astrImplementationTypes[i];
                    string strDisplayName = astrDisplayNames[i];

                    aMenuItem = new ImplTypeMenuItem(sImplementType, strDisplayName, this);
                    this.byImplementationTypeToolStripMenuItem.DropDownItems.Add(aMenuItem);
                    aMenuItem.Checked = (sImplementType == this.ImplTypeFilter);
                }
#else
                RegistryKey keyCnvtrsSupported = Registry.LocalMachine.OpenSubKey(EncConverters.HKLM_CNVTRS_SUPPORTED);
                if( keyCnvtrsSupported != null )
                {
                    foreach( string sImplementType in keyCnvtrsSupported.GetSubKeyNames() )
                    {
                        RegistryKey keyDisplayName = keyCnvtrsSupported.OpenSubKey(sImplementType);
                        if( keyDisplayName != null )
                        {
                            string strDisplayName = (string)keyDisplayName.GetValue(EncConverters.strRegKeyForFriendlyName);

                            if( strDisplayName == null )
                                strDisplayName = sImplementType;

                            aMenuItem = new ImplTypeMenuItem(sImplementType, strDisplayName, this);
                            this.byImplementationTypeToolStripMenuItem.DropDownItems.Add(aMenuItem);
                            aMenuItem.Checked = (sImplementType == this.ImplTypeFilter);
                        }
                    }
                }
#endif
            }
        }

        private void populateByEncoding_Opening()
        {
            // populate this menu with the implementations defined in the repository (but only once)
            if (this.byEncodingToolStripMenuItem.DropDownItems.Count == 0)
            {
                EncodingFilterMenuItem aMenuItem = new EncodingFilterMenuItem(cstrProjectEncodingFilterOffDisplayString, this);
                aMenuItem.Checked = (String.IsNullOrEmpty(this.EncodingFilter));
                // aMenuItem.DefaultItem = true;
                this.byEncodingToolStripMenuItem.DropDownItems.Add(aMenuItem);

                // the possible Encoding IDs comes from the repository object
                foreach(string strEncodingID in GetEncConverters.Encodings)
                {
                    if( !String.IsNullOrEmpty(strEncodingID) )
                    {
                        aMenuItem = new EncodingFilterMenuItem(strEncodingID, this);
                        this.byEncodingToolStripMenuItem.DropDownItems.Add(aMenuItem);
                        aMenuItem.Checked = (strEncodingID == this.EncodingFilter);
                    }
                }
            }
        }

        private void showAllTransductionTypesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ToolStripMenuItem aMenuItem in this.byTransductionTypeToolStripMenuItem.DropDownItems)
                aMenuItem.Checked = false;
            this.showAllTransductionTypesToolStripMenuItem.Checked = true;

            this.ProcessTypeFilter = ProcessTypeFlags.DontKnow;
            UpdateFilteringIndication();

            // add that to the registry also, so we remember it.
            RegistryKey keyLastDebugState = Registry.CurrentUser.CreateSubKey(cstrProjectMemoryKey);
            keyLastDebugState.SetValue(cstrProjectTransTypeFilterLastState, (Int32)this.ProcessTypeFilter);
        }

#if !TurnOffCsc30
        protected SpellingFixer30.CscProject m_cscProject = null;
        protected SpellingFixer30.SpellingFixer m_aSpellFixerLegacy = null;

        protected bool TrySelectProject()
        {
            try
            {
                m_cscProject = CscProject.SelectProject();
            }
            catch (ApplicationException ex)
            {
                if (ex.Message == CscProject.ChooseProjectException)
                    selectProjectToolStripMenuItem.Checked = false;
                else
                    MessageBox.Show(ex.Message, cstrCaption);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, cstrCaption);
                return false;
            }
            return true;
        }

        protected bool TryLoginProject()
        {
            try
            {
                if (m_aSpellFixerLegacy == null)
                    m_aSpellFixerLegacy = new SpellingFixer30.SpellingFixer();
                m_aSpellFixerLegacy.LoginProject();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, cstrCaption);
                return false;
            }
            finally
            {
                m_cscProject = null;   // just in case
            }
            return true;
        }

        public void QuerySpellFixProjectType()
        {
            bool bCscProject = false;
            try
            {
                if (m_aSpellFixerLegacy == null)
                    m_aSpellFixerLegacy = new SpellingFixer30.SpellingFixer();

                if (m_aSpellFixerLegacy.QuerySpellFixProject() == SpellFixerMode.eConsistentSpellingChecker)
                {
                    bCscProject = TrySelectProject();
                }
                else
                    TryLoginProject();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, cstrCaption);
            }
            finally
            {
                if (bCscProject)
                    m_aSpellFixerLegacy = null; // just in case
            }
        }

        private void DealWithLeftClick()
        {
            if (!IsCscProject && !IsSpellFixerLegacyProject)
                QuerySpellFixProjectType();

            // if it's now available...
            if (IsSpellFixerLegacyProject || IsCscProject)
            {
                // ... go ahead and try to convert what's on the clipboard
                IDataObject iData = Clipboard.GetDataObject();

                // Determines whether the data is in a format you can use.
                if( iData.GetDataPresent(DataFormats.UnicodeText) )
                {
                    var strInput = (string)iData.GetData(DataFormats.UnicodeText);
                    if (!String.IsNullOrEmpty(strInput))
                    {
                        try
                        {
                            IEncConverter aEC = null;
                            if (IsSpellFixerLegacyProject)
                            {
                                m_aSpellFixerLegacy.AssignCorrectSpelling(strInput);
                                aEC = m_aSpellFixerLegacy.SpellFixerEncConverter;
                            }
                            else if (IsCscProject)
                            {
                                m_cscProject.AssignCorrectSpelling(strInput);
                                aEC = m_cscProject.SpellFixerEncConverter;
                            }

                            string strOutput = aEC.Convert(strInput);
                            if (strOutput != null)
                                Clipboard.SetDataObject(strOutput);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, cstrCaption);
                        }
                    }
                }
            }
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_cscProject = null;
            m_aSpellFixerLegacy = null;
        }

        private void displaySpellingFixToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if it's now available...
            if (IsSpellFixerLegacyProject || IsCscProject)
            {
                // ... get the word on the clipboard and call the 'FindReplacementRule' method
                IDataObject iData = Clipboard.GetDataObject();

                // Determines whether the data is in a format you can use.
                if (iData.GetDataPresent(DataFormats.UnicodeText))
                {
                    string strInput = (string)iData.GetData(DataFormats.UnicodeText);
                    if (strInput.Length > 0)
                    {
                        if (IsCscProject)
                            m_cscProject.FindReplacementRule(strInput);
                        else if (IsSpellFixerLegacyProject)
                            m_aSpellFixerLegacy.FindReplacementRule(strInput);
                    }
                }
            }
        }

        private void editSpellingFixesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if it's now available...
            if (IsCscProject)
                m_cscProject.EditSpellingFixes();
            else if (IsSpellFixerLegacyProject)
                m_aSpellFixerLegacy.EditSpellingFixes();
        }

        private void editDictionaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if it's now available...
            if (IsCscProject)
                m_cscProject.EditDictionary();
        }

        private void spellFixerToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool bProjectSelected = (IsCscProject || IsSpellFixerLegacyProject);
            displaySpellingFixToolStripMenuItem.Enabled = bProjectSelected;
            editSpellingFixesToolStripMenuItem.Enabled = bProjectSelected;
            editDictionaryToolStripMenuItem.Enabled = IsCscProject;
            resetToolStripMenuItem.Enabled = bProjectSelected;
        }

        private void consistentSpellingFixerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_aSpellFixerLegacy = null; // just in case
            TrySelectProject();
        }

        private void selectProjectToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            consistentSpellingFixerToolStripMenuItem.Checked = IsCscProject;
            legacySpellFixerToolStripMenuItem.Checked = IsSpellFixerLegacyProject;
        }

        private string _lastTranslationHelperUsed = null;
        private Dictionary<string, TranslationHelperForm> _mapConverterToTranslationForm = new Dictionary<string, TranslationHelperForm>();

        private void translationHelperFormToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            // remove all added ones (except for the 'add', 'delete', and the separator for them)
            while (translationHelperFormToolStripMenuItem.DropDownItems.Count > 3)
                translationHelperFormToolStripMenuItem.DropDownItems.RemoveAt(0);

            var insertIndex = 0;
            foreach (var projectName in Properties.Settings.Default.TranslatorSetsProjectNames)
            {
                var projectToolStripItem = new ToolStripMenuItem(projectName, null, translatorSetsProjectNamesToolStripMenuItem_Click);
                translationHelperFormToolStripMenuItem.DropDownItems.Insert(insertIndex++, projectToolStripItem);
            }
        }

        private void TranslationHelperFormToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_lastTranslationHelperUsed != null)
                ExecuteTranslationHelperDialog(_lastTranslationHelperUsed);
        }

        private void translatorSetsProjectNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var projectName = ((ToolStripDropDownItem)sender).Text;
            ExecuteTranslationHelperDialog(projectName);
        }

        private void ExecuteTranslationHelperDialog(string projectName)
        {
            _lastTranslationHelperUsed = projectName;

            if (!_mapConverterToTranslationForm.TryGetValue(projectName, out TranslationHelperForm form))
            {
                form = new TranslationHelperForm(projectName, RemoveTranslatorSetFromMap);
                _mapConverterToTranslationForm.Add(projectName, form);
            }

            if (!form.Visible)  // in case the user closed it
            {
                // make it show and give it time
                form.Show();
                return;     // the GetNewClipboardData will be triggered by OnShown
            }

            form.Focus();
            form.GetNewClipboardData();
        }

        private void RemoveTranslatorSetFromMap(string projectName)
        {
            _mapConverterToTranslationForm?.Remove(projectName);
            _lastTranslationHelperUsed = null;
        }

        private void selectProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            QuerySpellFixProjectType();
            UpdateIconText();
        }

        protected void UpdateIconText()
        {
            // if a spell fixer project is selected, then make that the left-click behavior
            if (IsSpellFixerProject)
            {
                this.notifyIconClipboardEC.Text = "L-click: Add Substitution; R-click: preview converters";
                var spellFixerEncConverterName = m_aSpellFixerLegacy?.SpellFixerEncConverterName ?? m_cscProject?.SpellFixerEncConverterName;
                MessageBox.Show(String.Format("Now you can click on the ClipboardEncConverter icon with the left mouse button to add a spelling correction to the{0}'{1}' SpellFixer project. The right mouse button still provides normal ClipboardEncConverter functionality.",
                    Environment.NewLine, spellFixerEncConverterName), cstrCaption);
            }
            else
                this.notifyIconClipboardEC.Text = "L-Click: Repeat last conversion; R-click: preview converters";
        }

        protected bool IsCscProject
        {
            get { return (m_cscProject?.SpellFixerEncConverterName != null); }
        }

        protected bool IsSpellFixerLegacyProject
        {
            get { return (m_aSpellFixerLegacy?.SpellFixerEncConverterName != null); }
        }

        protected bool IsSpellFixerProject
        {
            get { return IsSpellFixerLegacyProject || IsCscProject; }
        }

        private void addTranslatorSetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string projectName = null;
            const string Prompt = "Enter a name for the Translation Set (e.g. \"Hindi to English\")";
            do
            {
                var prompt = (projectName == null) ? Prompt : Prompt + $" ({projectName} already exists)";
                projectName = Microsoft.VisualBasic.Interaction.InputBox(prompt, "Translation Set Project Id", "Any to English");
                if (projectName == String.Empty)    // user clicked 'Cancel'
                    return;
            }
            while (Properties.Settings.Default.TranslatorSetsProjectNames.Contains(projectName));

            var form = new TranslationHelperForm(projectName, RemoveTranslatorSetFromMap);
            _mapConverterToTranslationForm.Add(projectName, form);

            Properties.Settings.Default.TranslatorSetsProjectNames.Add(projectName);
            Properties.Settings.Default.Save();

            ExecuteTranslationHelperDialog(projectName);
        }

        private void deleteTranslatorSetToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
#else
        private SpellFixerByReflection m_aSpellFixer = null;

        protected bool IsSpellFixerProject
        {
            get { return (m_aSpellFixer != null); }
        }

        private void spellFixerToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            displaySpellingFixToolStripMenuItem.Enabled =
            editSpellingFixesToolStripMenuItem.Enabled =
            resetToolStripMenuItem.Enabled = IsSpellFixerProject;
        }

        private string _lastTranslationHelperUsed = null;
        private Dictionary<string, TranslationHelperForm> _mapConverterToTranslationForm = new Dictionary<string, TranslationHelperForm>();

        private void translationHelperFormToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            // remove all added ones (except for the 'add', 'delete', and the separator for them)
            while (translationHelperFormToolStripMenuItem.DropDownItems.Count > 3)
                translationHelperFormToolStripMenuItem.DropDownItems.RemoveAt(0);

            var insertIndex = 0;
            foreach (var projectName in Properties.Settings.Default.TranslatorSetsProjectNames)
            {
                var projectToolStripItem = new ToolStripMenuItem(projectName, null, translatorSetsProjectNamesToolStripMenuItem_Click);
                translationHelperFormToolStripMenuItem.DropDownItems.Insert(insertIndex++, projectToolStripItem);
            }
        }

        private void TranslationHelperFormToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_lastTranslationHelperUsed != null)
                ExecuteTranslationHelperDialog(_lastTranslationHelperUsed);
        }

        private void translatorSetsProjectNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var projectName = ((ToolStripDropDownItem)sender).Text;
            ExecuteTranslationHelperDialog(projectName);
        }

        private void RemoveTranslatorSetFromMap(string projectName)
        {
            _mapConverterToTranslationForm?.Remove(projectName);
            _lastTranslationHelperUsed = null;
        }

        private void ExecuteTranslationHelperDialog(string projectName)
        {
            _lastTranslationHelperUsed = projectName;

            if (!_mapConverterToTranslationForm.TryGetValue(projectName, out TranslationHelperForm form))
            {
                form = new TranslationHelperForm(projectName, RemoveTranslatorSetFromMap);
                _mapConverterToTranslationForm.Add(projectName, form);
            }

            if (!form.Visible)  // in case the user closed it
            {
                // make it show and give it time
                form.Show();
                return;     // the GetNewClipboardData will be triggered by OnShown
            }

            form.Focus();
            form.GetNewClipboardData();
        }

        private void addTranslatorSetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string projectName = null;
            const string Prompt = "Enter a name for the Translation Set (e.g. \"Hindi to English\")";
            do
            {
                var prompt = (projectName == null) ? Prompt : Prompt + $" ({projectName} already exists)";
                projectName = Microsoft.VisualBasic.Interaction.InputBox(prompt, "Translation Set Project Id", "Any to English");
                if (projectName == String.Empty)    // user clicked 'Cancel'
                    return;
            }
            while (Properties.Settings.Default.TranslatorSetsProjectNames.Contains(projectName));

            var form = new TranslationHelperForm(projectName, RemoveTranslatorSetFromMap);
            _mapConverterToTranslationForm.Add(projectName, form);

            Properties.Settings.Default.TranslatorSetsProjectNames.Add(projectName);
            Properties.Settings.Default.Save();

            ExecuteTranslationHelperDialog(projectName);
        }

        private void deleteTranslatorSetToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void selectProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TryLoginProject();
            UpdateIconText();
        }

        protected bool TryLoginProject()
        {
            try
            {
                if (m_aSpellFixer == null)
                    m_aSpellFixer = new SpellFixerByReflection();
                m_aSpellFixer.LoginProject();

                // to avoid the annoying error from CC about Converting on an empty table...
                //  go ahead and try to convert what's on the clipboard
                string strBadWord = "incorect";
                IDataObject iData = Clipboard.GetDataObject();
                if (iData.GetDataPresent(DataFormats.UnicodeText))
                    strBadWord = (string)iData.GetData(DataFormats.UnicodeText);
                m_aSpellFixer.QueryForSpellingCorrectionIfTableEmpty(strBadWord);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, cstrCaption);
                return false;
            }
            return true;
        }

        protected void UpdateIconText()
        {
            // if a spell fixer project is selected, then make that the left-click behavior
            if (IsSpellFixerProject)
            {
                this.notifyIconClipboardEC.Text = "L-click: Add Substitution; R-click: preview converters";
                MessageBox.Show(String.Format("Now you can click on the ClipboardEncConverter icon with the left mouse button to add a spelling correction to the{0}'{1}' SpellFixer project. The right mouse button still provides normal ClipboardEncConverter functionality.",
                    Environment.NewLine, m_aSpellFixer.SpellFixerEncConverterName), cstrCaption);
            }
            else
                this.notifyIconClipboardEC.Text = "L-Click: Repeat last conversion; R-click: preview converters";
        }

        private void DealWithLeftClick()
        {
            // new behavior (2023-07-31): if there's a spell-fixer project loaded, then left-click means bring up the
            //  Add correction dialog and then process what's on the clipboard thru the updated spellfixer project. Otherwise,
            //  left click means process the data on the clipboard with the last converter used (so my repeated uses of the
            //  ClearOutSfmMarkers perl expression can just be a single click.

            // ... go ahead and try to convert what's on the clipboard
            IDataObject iData = Clipboard.GetDataObject();

            // Determines whether the data is in a format you can use.
            if (iData.GetDataPresent(DataFormats.UnicodeText))
            {
                string strInput = (string)iData.GetData(DataFormats.UnicodeText);
                if (strInput.Length > 0)
                {
                    // if the SpellFixer isn't installed... then just skip this.
                    IEncConverter aEC = null;
                    if (SpellFixerByReflection.IsSpellFixerAvailable && IsSpellFixerProject)
                    {
                        m_aSpellFixer.AssignCorrectSpelling(strInput);

                        // when the ACS method returns, the couplet has been (probably) added
                        aEC = m_aSpellFixer.SpellFixerEncConverter;
                    }
                    else if (_lastEncConverterUsed != null)
                    {
                        aEC = _lastEncConverterUsed;
                    }

                    var strOutput = aEC?.Convert(strInput);
                    if (strOutput != null)
                        Clipboard.SetDataObject(strOutput);
                }
            }
        }

        private void displaySpellingFixToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if it's now available...
            if (IsSpellFixerProject)
            {
                // ... get the word on the clipboard and call the 'FindReplacementRule' method
                IDataObject iData = Clipboard.GetDataObject();

                // Determines whether the data is in a format you can use.
                if (iData.GetDataPresent(DataFormats.UnicodeText))
                {
                    string strInput = (string)iData.GetData(DataFormats.UnicodeText);
                    if (strInput.Length > 0)
                    {
                        try
                        {
                            m_aSpellFixer.FindReplacementRule(strInput);
                        }
                        catch (Exception ex)
                        {
                            string strError = ex.Message;
                            if (ex.InnerException != null)
                                strError += String.Format("{0}Cause: {1}", Environment.NewLine, ex.InnerException.Message);
                            MessageBox.Show(strError, cstrCaption);
                        }
                    }
                }
            }
        }

        private void editSpellingFixesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if it's now available...
            if (IsSpellFixerProject)
            {
                try
                {
                    m_aSpellFixer.EditSpellingFixes();
                }
                catch (Exception ex)
                {
                    string strError = ex.Message;
                    if (ex.InnerException != null)
                        strError += String.Format("{0}Cause: {1}", Environment.NewLine, ex.InnerException.Message);
                    MessageBox.Show(strError, cstrCaption);
                }
            }
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_aSpellFixer = null;
            UpdateIconText();
        }
#endif

        private void notifyIconClipboardEC_MouseUp(object sender, MouseEventArgs e)
        {
            if( e.Button == MouseButtons.Left )
                DealWithLeftClick();
        }
    }
}
