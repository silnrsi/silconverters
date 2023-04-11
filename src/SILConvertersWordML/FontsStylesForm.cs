#define DefineWord07MLDocument

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Word = Microsoft.Office.Interop.Word;
using System.IO;
using ECInterfaces;
using SilEncConverters40;
using System.Runtime.Serialization;                 // for SerializationException
using System.Runtime.Serialization.Formatters.Soap; // for soap formatter
using System.Runtime.InteropServices;               // for Marshal
using System.Diagnostics;                           // for Process
using System.Xml.Linq;

namespace SILConvertersWordML
{
    public partial class FontsStylesForm : Form
    {
        public const string cstrCaption = "SILConverters for Word Documents";
        protected const string cstrClickMsg = "Select a converter";
        protected const string cstrOutputFileAddn = " (Convert'd)";
        protected const string cstrCancelSearch = "Canceling the search";
        protected const string cstrLeftXmlFileSuffixBefore = " (SILConverters-generated before conversion).xml";
        internal const string cstrLeftXmlFileSuffixAfterXsltTransform = " (SILConverters-generated after xslt transform).xml";
        internal const string cstrLeftXmlFileSuffixAfterLinqTransform = " (SILConverters-generated after linq transform).xml";
        protected const string cstrLeftXmlFileSuffixAfter = " (SILConverters-generated after conversion).xml";

        protected static int cnMaxConverterName = 30;
        protected const int nMaxRecentFiles = 15;
        public const int cnDefaultFontSize = 12;

        protected Dictionary<string, DocXmlDocument> m_mapDocName2XmlDocument = new Dictionary<string, DocXmlDocument>();
        protected Dictionary<string, string> m_mapBackupNameToDocName = new Dictionary<string, string>();

        protected Hashtable m_mapEncConverters = new Hashtable();

        public Dictionary<string, Font> mapName2Font = new Dictionary<string, Font>();

        protected DirectableEncConverter m_aECLast = null;
        protected Font m_aFontLast = null;
        
        const int cnFontStyleColumn = 0;
        const int cnExampleDataColumn = 1;
        const int cnEncConverterColumn = 2;
        const int cnExampleOutputColumn = 3;
        const int cnTargetFontColumn = 4;

        protected DateTime m_dtStarted = DateTime.Now;
        TimeSpan m_timeMinStartup = new TimeSpan(0, 0, 1);

        object oMissing = System.Reflection.Missing.Value;
        object oFalse = false;
        object oTrue = true;

        public FontsStylesForm()
        {
            InitializeComponent();

            helpProvider.SetHelpString(this.dataGridView, Properties.Resources.dataGridViewHelp);

#if DEBUG
            leaveXMLFileInFolderToolStripMenuItem.Checked = true;
#endif
        }

        public void OpenDocuments(string[] astrFilenames)
        {
            Cursor = Cursors.WaitCursor;

            // in case the app was already open and the user clicks the "Open SFM document" again.
            Reset();

            GetXmlDocuments(astrFilenames);
            DoRestOfOpen(astrFilenames);

            Cursor = Cursors.Default;
        }

        public void DoRestOfOpen(string[] astrFilenames)
        {
            PopulateGrid();
            AddFilenameToTitle(astrFilenames);
            convertAndSaveDocumentsToolStripMenuItem.Enabled = this.toolStripButtonConvertAndSave.Enabled = 
                singlestepConversionToolStripMenuItem.Enabled = toolStripButtonSingleStep.Enabled = 
                reloadToolStripMenuItem.Enabled = this.toolStripButtonRefresh.Enabled = true;
        }

        public void AddFilenameToTitle(string[] FileNames)
        {
            Debug.Assert(FileNames.Length > 0);
            string strTitleName = "<various>";  // assume multiple files

            // if there's only one, then add it to the recently used files
            if (FileNames.Length == 1)
            {
                // if there's only one, then use that in the frame title
                string strFilename = FileNames[0];
                strTitleName = Path.GetFileName(strFilename);

                // add this filename to the list of recently used files
                if (Properties.Settings.Default.RecentFiles.Contains(strFilename))
                    Properties.Settings.Default.RecentFiles.Remove(strFilename);
                else if (Properties.Settings.Default.RecentFiles.Count > nMaxRecentFiles)
                    Properties.Settings.Default.RecentFiles.RemoveAt(nMaxRecentFiles);

                Properties.Settings.Default.RecentFiles.Insert(0, strFilename);
                Properties.Settings.Default.Save();
            }

            this.Text = String.Format("{0} -- {1}", cstrCaption, strTitleName);
        }

        public void AddToConverterMappingRecentlyUsed(string strFilename)
        {
            Debug.Assert(!String.IsNullOrEmpty(strFilename));

            // add this filename to the list of recently used files
            if (Properties.Settings.Default.ConverterMappingRecentFiles.Contains(strFilename))
                Properties.Settings.Default.ConverterMappingRecentFiles.Remove(strFilename);
            else if (Properties.Settings.Default.ConverterMappingRecentFiles.Count > nMaxRecentFiles)
                Properties.Settings.Default.ConverterMappingRecentFiles.RemoveAt(nMaxRecentFiles);

            Properties.Settings.Default.ConverterMappingRecentFiles.Insert(0, strFilename);
            Properties.Settings.Default.Save();
        }

        protected DataIterator GetIteratorFromMap(string strFontName, IteratorMap mapNames2Iterator)
        {
            return mapNames2Iterator[strFontName];
        }

        public bool IsConverterDefined(string strFontStyleName)
        {
            return m_mapEncConverters.ContainsKey(strFontStyleName);
        }

        public void DefineConverter(string strFontStyleName, DirectableEncConverter aEC)
        {
            if (IsConverterDefined(strFontStyleName))
                m_mapEncConverters.Remove(strFontStyleName);
            m_mapEncConverters.Add(strFontStyleName, aEC);
        }

        public DirectableEncConverter GetConverter(string strFontName)
        {
            return (DirectableEncConverter)m_mapEncConverters[strFontName];
        }

        protected void UpdateExampleDataColumns(DataGridViewRow theRow, string strSampleValue)
        {
            theRow.Cells[cnExampleDataColumn].Value = strSampleValue;
            string strFontStyleName = (string)theRow.Cells[cnFontStyleColumn].Value;
            if (!String.IsNullOrEmpty(strSampleValue) && IsConverterDefined(strFontStyleName))
            {
                var aEC = (DirectableEncConverter)m_mapEncConverters[strFontStyleName];
                strSampleValue = CallSafeConvert(aEC, strSampleValue);
            }
            theRow.Cells[cnExampleOutputColumn].Value = strSampleValue;
        }

        protected void UpdateSampleValue(DataGridViewRow theRow)
        {
            var xpIterator = (DataIterator)theRow.Tag;
            Debug.Assert(xpIterator != null);
            if (xpIterator.MoveNext())
				UpdateExampleDataColumns(theRow, GetCurrentValue(xpIterator));
        }

        internal static void UpdateConverterCellValue(DataGridViewCell theCell, DirectableEncConverter aEC)
        {
            if (aEC == null)
            {
                theCell.Value = cstrClickMsg;
                theCell.ToolTipText = null;
            }
            else
            {
                string strName = aEC.Name;
                if (strName.Length > cnMaxConverterName)
                    strName = strName.Substring(0, cnMaxConverterName);
                theCell.Value = strName;
                theCell.ToolTipText = aEC.ToString();
            }
        }

        protected void UpdateTargetFontCellValue(DataGridViewRow theRow, Font fontTarget)
        {
            theRow.Cells[cnTargetFontColumn].Value = fontTarget.Name;
            theRow.Cells[cnExampleOutputColumn].Style.Font = fontTarget;
            RowMaxHeight = Math.Max(RowMaxHeight, fontTarget.Height);
        }

        protected string GetCurrentValue(DataIterator dataIterator)
        {
            return dataIterator.CurrentValue;
        }

        protected void PopulateGrid()
        {
            dataGridView.Rows.Clear();
            var lstInGrid = new List<string>();    // used so we don't add something twice
            if (this.radioButtonEverything.Checked)
            {
                ColumnFont.HeaderText = "Font";
                ColumnTargetFont.HeaderText = "Apply Font";

                // get the Fonts and Styles out of the xml docs
                GetTextIteratorListCustomFont(ref lstInGrid);
                GetTextIteratorListStyleFont(ref lstInGrid);
            }
            else if (radioButtonStylesOnly.Checked)
            {
                ColumnFont.HeaderText = "Style";
                ColumnTargetFont.HeaderText = "New Style Font";

                // get the Fonts and Styles out of the xml doc
                GetTextIteratorListStyleOnly(ref lstInGrid);
            }
            else if (radioButtonFontsOnly.Checked)
            {
                ColumnFont.HeaderText = "Font";
                ColumnTargetFont.HeaderText = "Apply Font";

                // get the Fonts and Styles out of the xml doc
                GetTextIteratorListCustomFont(ref lstInGrid);
            }
            else
                Debug.Assert(false);

            // set it up so we 'see' the click
            m_dtStarted = DateTime.Now;
            UpdateStatusBar("Choose the converter(s) and target font you want to apply to the text");
        }

        public int RowMaxHeight = 28;    // start with this

        protected void DisplayInGrid(string strName, DataIterator dataIterator)
        {
            string strTextSample = GetCurrentValue(dataIterator);
            string strConverterName = cstrClickMsg;
            string strOutput = strTextSample;
            string strTooltip = cstrClickMsg;

            // if there's not already a mapping, see if the repository can help us
            if (!IsConverterDefined(strName))
            {
                EncConverters aECs = GetEncConverters;
                if (aECs != null)
                {
                    string strMappingName = aECs.GetMappingNameFromFont(strName);
                    if (!String.IsNullOrEmpty(strMappingName))
                    {
                        strConverterName = strMappingName;
                        IEncConverter aIEC = aECs[strConverterName];

                        if (aIEC != null)
                        {
                            DirectableEncConverter aEC = new DirectableEncConverter(aIEC);
                            DefineConverter(strName, aEC);
                        }
                    }
                }
            }

            if (IsConverterDefined(strName))
            {
                DirectableEncConverter aEC = GetConverter(strName);
                strConverterName = aEC.Name;
                strOutput = CallSafeConvert(aEC, strTextSample);
                strTooltip = aEC.ToString();
            }

            if (!mapName2Font.ContainsKey(strName))
            {
                string strTargetFontName = strName;
                if (IsConverterDefined(strName))
                {
                    EncConverters aECs = GetEncConverters;
                    if (aECs != null)
                    {
                        DirectableEncConverter aEC = GetConverter(strName);
                        string[] astrFontnames = aECs.GetFontMapping(aEC.Name, strName);
                        if (astrFontnames.Length > 0)
                        {
                            strTargetFontName = astrFontnames[0];
                        }
                    }
                }

                Font font = CreateFontSafe(strTargetFontName);
                mapName2Font.Add(strName, font);
            }

            Font fontSource = CreateFontSafe(strName);
            Font fontTarget = mapName2Font[strName];

            string[] row = { strName, strTextSample, strConverterName, strOutput, fontTarget.Name };
            int nIndex = this.dataGridView.Rows.Add(row);
            DataGridViewRow thisRow = dataGridView.Rows[nIndex];
            thisRow.Cells[cnEncConverterColumn].ToolTipText = strTooltip;
            thisRow.Tag = dataIterator;
            thisRow.Cells[cnExampleDataColumn].Style.Font = fontSource;
            thisRow.Cells[cnExampleOutputColumn].Style.Font = fontTarget;
            thisRow.Height = RowMaxHeight;
        }

        // the creation of a Font can throw an exception if, for example, you try to construct one with
        //  the default style 'Regular' when the font itself doesn't have a Regular style. So this method
        //  can be called to create one and it'll try different styles if it fails.
        protected Font CreateFontSafe(string strFontName)
        {
            Font font = null;
            try
            {
                font = new Font(strFontName, cnDefaultFontSize);
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("' does not support style '") != -1)
                {
                    try
                    {
                        font = new Font(strFontName, cnDefaultFontSize, FontStyle.Bold);
                    }
                    catch
                    {
                        if (ex.Message.IndexOf("' does not support style '") != -1)
                        {
                            try
                            {
                                font = new Font(strFontName, cnDefaultFontSize, FontStyle.Italic);
                            }
                            catch { }
                        }
                    }
                }
            }
            finally
            {
                if (font == null)
                    font = dataGridView.Font;
            }

            return font;
        }

        protected string CallSafeConvert(DirectableEncConverter aEC, string strInput)
        {
            try
            {
                return aEC.Convert(strInput);
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Conversion failed! Reason: {0}", ex.Message), cstrCaption);
            }

            return null;
        }

        protected void Reset()
        {
            m_mapDocName2XmlDocument.Clear();
            dataGridView.Rows.Clear();
            m_mapBackupNameToDocName.Clear();
            
            // initialize the tooltip for the convert and save button (assume that it'll be "save with new name")
            //  but override in the AutoSearch, which saves the files automatically either in-situ or in the backup folder
            this.convertAndSaveDocumentsToolStripMenuItem.ToolTipText = this.toolStripButtonConvertAndSave.ToolTipText = 
                "Click to convert the opened Word document(s) and save them with a new name";
        }

        // if the document is based on a SaveFormat from Office 2007, then use the Office2007 "FlatXML" format
        //  for the temporary XML file.
        protected const Word.WdSaveFormat wdFormatXMLDocument = (Word.WdSaveFormat)12;
        protected const Word.WdSaveFormat wdFormatFlatXML = (Word.WdSaveFormat)19;
        
        protected Word.WdSaveFormat m_oFormatOpen = Word.WdSaveFormat.wdFormatDocument;
        protected string m_strExtnOpen = null;
        protected bool m_bWarnAboutWord2007Conversion = true;

        protected DocXmlDocument ConvertDocToXml(Word.Application wrdApp, string strDocFilename)
        {
            object oDocFilename = strDocFilename;
            Word._Document wrdDoc = wrdApp.Documents.Open(ref oDocFilename, ref oFalse, ref oFalse, 
                ref oFalse, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing, 
                ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing, 
                ref oMissing);

            m_strExtnOpen = Path.GetExtension(strDocFilename);
            if (!String.IsNullOrEmpty(m_strExtnOpen))
            {
                m_strExtnOpen = m_strExtnOpen.ToLower();
                m_oFormatOpen = (Word.WdSaveFormat)wrdDoc.SaveFormat;
#if DEBUG && false
                MessageBox.Show(String.Format("File opened as format: '{0}'", m_oFormatOpen.ToString()));
#endif
            }

            var bWord2007 = (wrdDoc.SaveFormat >= (int)wdFormatXMLDocument);
            if (bWord2007)
            {
                if (m_bWarnAboutWord2007Conversion)
                {
                    DialogResult res = MessageBox.Show(String.Format("The document '{0}' can be processed if you use Word 2003 compatibility mode, though you may lose some formatting. Would you like to use Word 2003 compatibility mode on this and all subsequent Word 2007 documents?", strDocFilename), cstrCaption, MessageBoxButtons.YesNoCancel);
                    if (res == DialogResult.No)
                        return null;
                    m_bWarnAboutWord2007Conversion = false;
                }
                bWord2007 = false;  // use Word 2003 compatibility mode
            }

            string strXmlFilename = GetTempFilename + ".xml";
            object oXmlFilename = strXmlFilename;
            object oXmlFormat = (bWord2007) ? wdFormatFlatXML : Word.WdSaveFormat.wdFormatXML;

            wrdDoc.SaveAs(ref oXmlFilename, ref oXmlFormat, ref oMissing, ref oMissing, ref oFalse, ref oMissing,
                ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing,
                ref oMissing, ref oMissing, ref oMissing);

            wrdDoc.Close(false);
            Marshal.ReleaseComObject(wrdDoc);
            wrdDoc = null;

            SaveIntermediateXmlFile(ref strXmlFilename, cstrLeftXmlFileSuffixBefore,
                                    strDocFilename, 
                                    leaveXMLFileInFolderToolStripMenuItem.Checked, null);

            var bUsingLinq = useLinqToolStripMenuItem.Checked;
            var doc = bUsingLinq
                          ? WordLinqDocument.GetXmlDocument(ref strXmlFilename, strDocFilename,
                                                            leaveXMLFileInFolderToolStripMenuItem.Checked,
                                                            combineIntoIsoformattedParagraphToolStripMenuItem.Checked)
                          : Word03MLDocument.GetXmlDocument(ref strXmlFilename, strDocFilename,
                                                            leaveXMLFileInFolderToolStripMenuItem.Checked);

            return doc;
        }

        protected XDocument SaveIntermediateXmlFile(ref string strXmlFilename, string strXmlFilenameSuffix,
            string strDocFilename, bool bSaveXmlOutputInFolder, XDocument doc)
        {
            try
            {
                if (bSaveXmlOutputInFolder)
                {
                    doc ??= XDocument.Load(strXmlFilename);

                    int nIndex = strDocFilename.LastIndexOf('.');
                    if (nIndex != -1)
                    {
                        strXmlFilename = String.Format(@"{0}\{1}{2}",
                            Path.GetDirectoryName(strDocFilename),
                            Path.GetFileName(strDocFilename),
                            strXmlFilenameSuffix);
                        if (File.Exists(strXmlFilename))
                            File.Delete(strXmlFilename);
                    }
                }
                
                doc?.Save(strXmlFilename);
            }
            catch (Exception ex)
            {
                string strErrorMsg = String.Format("Unable to save a copy of the xml file in the local folder (requested by \"Advanced\", \"Leave XML file in folder\" option)! Reason:{0}{0}{1}{0}{0}Would you like to continue with the conversion process anyway?",
                    Environment.NewLine, ex.Message);
                DialogResult res = MessageBox.Show(strErrorMsg, cstrCaption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Error);
                if (res != DialogResult.Yes)
                    return null;
            }
            return doc;
        }

        // different versions of Word must surely be allowed to do this... 
        //  use the extension insteads...
        /*
        //  1:  Word Document (*.doc)|*.doc|
        //  2:  XML Document (*.xml)|*.xml|
        //  3:  Single File Web Page (*.mht; *.mhtml)|*.mht; *.mhtml|
        //  4:  Web Page (*.htm; *.html)|*.htm; *.html|
        //  5:  Web Page, Filtered (*.htm; *.html)|*.htm; *.html|
        //  6:  Document Template (*.dot)|*.dot|
        //  7:  Rich Text Format (*.rtf)|*.rtf|
        //  8:  Plain Text (*.txt)|*.txt|
        //  9:  Encoded Text (*.txt)|*.txt|
        //  10: Unicode Text (*.txt)|*.txt|
        //  11: Text with line breaks (*.txt)|*.txt|
        //  12: All files (*.*)|*.*
        protected Word.WdSaveFormat SaveFormatFromIndex(int nIndex, string strFilename)
        {
            Word.WdSaveFormat wdSaveFormat;
            switch (nIndex)
            {
                case 1:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatDocument;
                    break;
                case 2:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatXML;
                    break;
                case 3:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatWebArchive;
                    break;
                case 4:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatHTML;
                    break;
                case 5:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatFilteredHTML;
                    break;
                case 6:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatTemplate;
                    break;
                case 7:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatRTF;
                    break;
                case 8:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatText;
                    break;
                case 9:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatEncodedText;
                    break;
                case 10:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatUnicodeText;
                    break;
                case 11:
                    wdSaveFormat = Word.WdSaveFormat.wdFormatTextLineBreaks;
                    break;
                default:
                    // otherwse, try to figure it out from the extension
                    wdSaveFormat = GuessSaveFormat(strFilename);
                    break;
            }

            return wdSaveFormat;
        }
        */

        protected Word.WdSaveFormat SaveFormatFromIndexFromFilename(string strFilename)
        {
            // from the FileSave dialog: Word Document (*.doc)|*.doc|XML Document (*.xml)|*.xml|Single File Web Page (*.mht; *.mhtml)|*.mht; *.mhtml|Web Page (*.htm; *.html)|*.htm; *.html|Web Page, Filtered (*.htm; *.html)|*.htm; *.html|Document Template (*.dot)|*.dot|Rich Text Format (*.rtf)|*.rtf|Plain Text (*.txt)|*.txt|Encoded Text (*.txt)|*.txt|Unicode Text (*.txt)|*.txt|Text with line breaks (*.txt)|*.txt|All files (*.*)|*.*
            var strExtn = Path.GetExtension(strFilename);
            if (!String.IsNullOrEmpty(strExtn))
            {
                strExtn = strExtn.ToLower();
                switch (strExtn)
                {
                    case ".xml":
                        return Word.WdSaveFormat.wdFormatXML;
                    case ".mhtml":
                    case ".mht":
                        return Word.WdSaveFormat.wdFormatWebArchive;
                    case ".html":
                    case ".htm":
                        return Word.WdSaveFormat.wdFormatHTML;
                    case ".dot":
                        return Word.WdSaveFormat.wdFormatTemplate;
                    case ".rtf":
                        return Word.WdSaveFormat.wdFormatRTF;
                    case ".txt":
                        return Word.WdSaveFormat.wdFormatEncodedText;
                }
            }
            return Word.WdSaveFormat.wdFormatDocument;
        }

        const Word.WdSaveFormat wdFormatXMLDocumentMacroEnabled = (Word.WdSaveFormat)13;
        const Word.WdSaveFormat wdFormatXMLTemplateMacroEnabled = (Word.WdSaveFormat)15;
        const Word.WdSaveFormat wdFormatDocumentDefault = (Word.WdSaveFormat)16;
        // Doesn't work in Beta2TR  const Word.WdSaveFormat wdFormatPDF = (Word.WdSaveFormat)17;
        // Doesn't work in Beta2TR  const Word.WdSaveFormat wdFormatXPS = (Word.WdSaveFormat)18;

        protected Word.WdSaveFormat GuessSaveFormat(string strFilename)
        {
            Word.WdSaveFormat wdSaveFormat = Word.WdSaveFormat.wdFormatDocument;
            string strExtn = Path.GetExtension(strFilename);
            if (!String.IsNullOrEmpty(strExtn))
            {
                // first see if it's the same as the open format (in which case we should use that). We do this
                //  just in case it was something like ".txt" which could a whole bunch of different formats.
                // BUT not if we're doing a whole bunch of files at once, in which case, m_oFormatOpen is the 
                //  format of the last one opened!
                if ((m_mapBackupNameToDocName.Count == 0) && (m_strExtnOpen == strExtn))
                {
                    wdSaveFormat = m_oFormatOpen;
                }
                else
                {
                    // otherwise, just pick based on the extension
                    //  Word Document (*.doc)|*.doc|
                    //  XML Document (*.xml)|*.xml|
                    //  Single File Web Page (*.mht; *.mhtml)|*.mht; *.mhtml|
                    //  Web Page (*.htm; *.html)|*.htm; *.html|
                    //  Web Page, Filtered (*.htm; *.html)|*.htm; *.html|
                    //  Document Template (*.dot)|*.dot|
                    //  Rich Text Format (*.rtf)|*.rtf|
                    //  Plain Text (*.txt)|*.txt|
                    //  All files (*.*)|*.*
                    if (strExtn == ".doc")
                        wdSaveFormat = Word.WdSaveFormat.wdFormatDocument;
                    else if (strExtn == ".docx")
                        wdSaveFormat = wdFormatDocumentDefault;
                    else if (strExtn == ".docm")
                        wdSaveFormat = wdFormatXMLDocumentMacroEnabled;
                    /*  Doesn't work in Beta2TR
                    else if (strExtn == "pdf")
                        wdSaveFormat = wdFormatPDF;
                    else if (strExtn == "xps")
                        wdSaveFormat = wdFormatXPS;
                    */
                    else if (strExtn == ".dotm")
                        wdSaveFormat = wdFormatXMLTemplateMacroEnabled;
                    else if (strExtn == ".xml")
                        wdSaveFormat = Word.WdSaveFormat.wdFormatXML;
                    else if ((strExtn == ".mht") || (strExtn == ".mhtml"))
                        wdSaveFormat = Word.WdSaveFormat.wdFormatWebArchive;
                    else if ((strExtn == ".htm") || (strExtn == ".html"))
                        wdSaveFormat = Word.WdSaveFormat.wdFormatHTML;
                    else if (strExtn == ".txt")
                        wdSaveFormat = Word.WdSaveFormat.wdFormatEncodedText;
                    else if (strExtn == ".rtf")
                        wdSaveFormat = Word.WdSaveFormat.wdFormatRTF;
                    else if (strExtn == ".dot")
                        wdSaveFormat = Word.WdSaveFormat.wdFormatTemplate;
                }
            }

            return wdSaveFormat;
        }

        protected void ConvertXmlToDoc(Word.Application wrdApp, string strXmlFilename, string strDocFilename, 
            Word.WdSaveFormat wdSaveFormat)
        {
            object oFormat = null;
            Word._Document wrdDoc = null;
#if !FlawedImplementationByMS
            try
            {
                // passing "false" for "ConfirmConversion" (i.e. the 2nd parameter to Documents.Open)
                //  will actually reset the static options property "Tools, Options dialog, General Tab, 
                //  Confirm Conversion at Open"
                // so we have to remember what it was and reset it afterwards... don't ask...
                bool oOriginalConfirmConversionValue = wrdApp.Options.ConfirmConversions;
#endif
                object oXmlFilename = strXmlFilename;
                oFormat = Word.WdOpenFormat.wdOpenFormatXML;
                wrdDoc = wrdApp.Documents.Open(ref oXmlFilename, ref oFalse, ref oTrue,
                    ref oFalse, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing,
                    ref oFormat, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing);

#if !FlawedImplementationByMS
                // passing "false" for "ConfirmConversion" (i.e. the 2nd parameter to Documents.Open)
                //  will actually reset the static options property "Tools, Options dialog, General Tab, 
                //  Confirm Conversion at Open"
                // so we have to remember what it was and reset it afterwards... don't ask...
                wrdApp.Options.ConfirmConversions = oOriginalConfirmConversionValue;
#endif
            }
            catch (Exception)
            {
                object oXmlFilename = strXmlFilename;
                oFormat = Word.WdOpenFormat.wdOpenFormatXML;
                wrdDoc = wrdApp.Documents.Open(ref oXmlFilename, ref oFalse, ref oTrue,
                    ref oFalse, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing,
                    ref oFormat, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing);
            }

            object oDocFilename = strDocFilename;

            // All Word Documents (*.doc; *.dot; *.htm; *.html; *.url; *.rtf; *.mht; *.mhtml; *.xml)
            // save with the correct format id based on the extn the user chose
            oFormat = wdSaveFormat;

            wrdDoc.SaveAs(ref oDocFilename, ref oFormat, ref oMissing, ref oMissing, ref oFalse, ref oMissing,
                ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing, ref oMissing,
                ref oMissing, ref oMissing, ref oMissing);

            wrdDoc.Close(false);
            Marshal.ReleaseComObject(wrdDoc);
            wrdDoc = null;

            try
            {
                // at least *try* to clean up the temp files
                if (!leaveXMLFileInFolderToolStripMenuItem.Checked)
                    File.Delete(strXmlFilename);
            }
            catch { }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult res = this.openFileDialog.ShowDialog();
            if (res == DialogResult.OK)
            {
                // these only want to be "Reset" if the user is explicitly opening new files 
                //  (e.g. not if it's a refresh)
                mapName2Font.Clear();
                m_mapEncConverters.Clear();

                Program.FileNames = openFileDialog.FileNames;
                Program.myTimer.Start();
            }
        }

        // the GetDirectoryName returns a final slash, but only for files in the root folder
        //  so make sure we get exactly one.
        private string GetDirEnsureFinalSlash(string strFilename)
        {
            string strFolder = Path.GetDirectoryName(strFilename);
            if (strFolder[strFolder.Length - 1] != '\\')
                strFolder += '\\';
            return strFolder;
        }

        private void convertAndSaveDocumentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO: see what happens if PIAs aren't installed
            Cursor = Cursors.WaitCursor;
            if (!CheckForWinWord())
                return;

            Word.Application wrdApp = new Word.Application();
            try
            {
                string strFilenamePath = null, strFilenamePrefix = null, strFilenameSuffix = cstrOutputFileAddn, strExtn = null;  // for starters

                foreach (string strOrigFileSpec in m_mapDocName2XmlDocument.Keys)
                {
                    // convert the data in the document
                    string strConvertedXmlFilename;
                    ConvertAndSaveDoc(strOrigFileSpec, out strConvertedXmlFilename);

                    // now convert it back to a Word .doc file
                    // First calculate the new filename
                    if (strExtn == null)
                        strExtn = Path.GetExtension(strOrigFileSpec);

                    if (strFilenamePath == null)
                        strFilenamePath = GetDirEnsureFinalSlash(strOrigFileSpec);

                    string strFileTitle = Path.GetFileNameWithoutExtension(strOrigFileSpec);

                    string strOrigFileSpecLowerCase = strOrigFileSpec.ToLower();
                    string strNewSaveFileSpec = null;
                    string strBackup = null;
                    if ((m_mapBackupNameToDocName.Count > 0) && (m_mapBackupNameToDocName.ContainsKey(strOrigFileSpec)))
                    {
                        strNewSaveFileSpec = m_mapBackupNameToDocName[strOrigFileSpec];
                        strBackup = String.Format(@"{0}\Backup of {1}",
                            Path.GetDirectoryName(strNewSaveFileSpec), Path.GetFileName(strOrigFileSpec));
                        File.Move(strNewSaveFileSpec, strBackup);

                        if (leaveXMLFileInFolderToolStripMenuItem.Checked)
                        {
                            string strBackupXml = String.Format(@"{0}\{1}",
                                Path.GetDirectoryName(strNewSaveFileSpec), Path.GetFileName(strConvertedXmlFilename));
                            File.Copy(strConvertedXmlFilename, strBackupXml, true);
                        }
                    }
                    else
                    {
                        string strNewSaveFileSpecLowerCase;
                        do
                        {
                            // if the user is saving the file somewhere besides the original folder...
                            if (GetDirEnsureFinalSlash(strOrigFileSpec) != strFilenamePath)
                            {
                                // then no need to query for the name -- just use the bits we figured out from before
                                var strOutputFilenameOrig = strFilenamePath + strFilenamePrefix + strFileTitle + strFilenameSuffix + strExtn;
                                saveFileDialog.FileName = strOutputFilenameOrig;
                                strNewSaveFileSpecLowerCase = strOutputFilenameOrig.ToLower();
                            }
                            else
                            {
                                // otherwise, query for the name to make sure it isn't the same as the original file
                                var strOutputFilenameOrig = strFilenamePath + strFilenamePrefix + strFileTitle +
                                                               strFilenameSuffix; //  +strExtn;

                                saveFileDialog.FileName = strOutputFilenameOrig;

                                DialogResult res = this.saveFileDialog.ShowDialog();
                                strNewSaveFileSpecLowerCase = saveFileDialog.FileName.ToLower();
                                if (res == DialogResult.Cancel)
                                {
                                    UpdateStatusBar(
                                        "Do not click 'File', 'Convert and Save' again or you may convert the document twice! Press F5 to reload the files from scratch.");
                                    return;
                                }
                                else if ((res == DialogResult.OK) && (strNewSaveFileSpecLowerCase == strOrigFileSpecLowerCase))
                                    MessageBox.Show("Sorry, you cannot save this file with the same name", cstrCaption);
                            }
                        } while (strNewSaveFileSpecLowerCase == strOrigFileSpecLowerCase);
                        strNewSaveFileSpec = saveFileDialog.FileName;
                    }

                    string strOutputFilenameNew = strNewSaveFileSpec;
                    var wdSaveFormat = SaveFormatFromIndexFromFilename(strOutputFilenameNew);
                    ConvertXmlToDoc(wrdApp, strConvertedXmlFilename, strOutputFilenameNew, wdSaveFormat);

                    if (!String.IsNullOrEmpty(strBackup) && File.Exists(strBackup))
                    {
                        try
                        {
                            File.Delete(strBackup);
                        }
                        catch { }
                    }

                    // calculate (if possible) the suffix the user used so we can use that next time.
                    strExtn = Path.GetExtension(strOutputFilenameNew);
                    strFilenamePath = GetDirEnsureFinalSlash(strOutputFilenameNew);
                    string strNewTitle = Path.GetFileNameWithoutExtension(strOutputFilenameNew);

                    // see if the original name is some portion of the new name
                    int nIndexOfOrigName = strNewTitle.IndexOf(strFileTitle, StringComparison.InvariantCultureIgnoreCase);

                    // if we found the original name in the new name...
                    if (nIndexOfOrigName != -1)
                    {
                        // first check for a prefix (which must be beyond the path)
                        if (nIndexOfOrigName > 0)
                            strFilenamePrefix = strNewTitle.Substring(0, nIndexOfOrigName);

                        // also check for a suffix
                        int nSuffixStart = (nIndexOfOrigName + strFileTitle.Length);
                        if (strNewTitle.Length > nSuffixStart)
                        {
                            Debug.Assert(nSuffixStart >= 0);
                            strFilenameSuffix = strNewTitle.Substring(nSuffixStart);
                        }
                        else
                            strFilenameSuffix = null;
                    }
                }

                UpdateStatusBar("Convert and Save complete!");
            }
            catch (Exception ex)
            {
                string strErrorMsg = String.Format("Unable to convert and/or save! Reason:{0}{0}{1}{0}{0}If the conversion was already started when this error occurred,{0}then you can't click 'Convert and Save' again (to avoid converting the document twice).{0}{0}Click 'Yes' to reload the files from scratch.",
                    Environment.NewLine, ex.Message);
                DialogResult res = MessageBox.Show(strErrorMsg, cstrCaption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Error);
                if (res == DialogResult.Yes)
                    Program.myTimer.Start();
                else
                    UpdateStatusBar("Do not click 'File', 'Convert and Save' again or you may convert the document twice! Press F5 to reload the files from scratch.");
            }
            finally
            {
                wrdApp.Quit(false);
                Marshal.ReleaseComObject(wrdApp);
                wrdApp = null;
                Cursor = Cursors.Default;
            }
        }

        public bool ConvertDoc(string strFontStyleName, DataIterator dataIteratorFontStyleText, 
			string strLhsFont, Font fontApply, bool bConvertCharValue)
        {
            bool bModified = false;
            if (IsConverterDefined(strFontStyleName))
            {
                var aEC = GetConverter(strFontStyleName);
                var fontLhs = CreateFontSafe(strLhsFont);
				bModified = SetValues(dataIteratorFontStyleText, strFontStyleName, aEC, 
					fontLhs, fontApply, bConvertCharValue);
            }
            return bModified;
        }

        protected string m_strCurrentDocument = null;

        protected bool ConvertAndSaveDoc(string strDocFilename, out string strXmlOutputFilename)
        {
            // set this in the global "doc we're working on" string so it can be used when writing to the
            //  status bar
            m_strCurrentDocument = Path.GetFileName(strDocFilename);
            var doc = m_mapDocName2XmlDocument[strDocFilename];
            bool bModified = false;

            if (this.radioButtonEverything.Checked)
            {
                bModified |= doc.ConvertDocumentByFontNameAndStyle(mapName2Font, ConvertDoc);
            }
            else if (radioButtonStylesOnly.Checked)
            {
                bModified |= doc.ConvertDocumentByStylesOnly(mapName2Font, ConvertDoc);
            }
            else if (radioButtonFontsOnly.Checked)
            {
                bModified |= doc.ConvertDocumentByFontNameOnly(mapName2Font, ConvertDoc);
            }

            strXmlOutputFilename = Path.GetTempFileName() + ".xml";

            try
            {
                if (leaveXMLFileInFolderToolStripMenuItem.Checked)
                {
                    strXmlOutputFilename = String.Format(@"{0}\{1}{2}",
                                Path.GetDirectoryName(strDocFilename),
                                Path.GetFileName(strDocFilename),
                                cstrLeftXmlFileSuffixAfter);
                    if (File.Exists(strXmlOutputFilename))
                        File.Delete(strXmlOutputFilename);
                    doc.Save(strXmlOutputFilename);
                }
                else
                {
                    doc.Save(strXmlOutputFilename);
                    /*
                    // at least *try* to clean up the temp files
                    const string cstrUdiPrefix = "file:///";
                    string strXmlFilename = doc.BaseURI;
                    int nIndex = strXmlFilename.IndexOf(cstrUdiPrefix);
                    if (nIndex == 0)
                        strXmlFilename = strXmlFilename.Replace(cstrUdiPrefix, null);
                    File.Delete(strXmlFilename);
                    */
                }
            }
            catch { }

            return bModified;
        }

        protected bool SetValues(DataIterator dataIterator, string strFontStyleName,
			DirectableEncConverter aEC, Font fontLhs, Font fontRhs, bool bConvertCharValue)
        {
            BaseConverterForm dlg = null;
            if (SingleStep)
                dlg = new BaseConverterForm(aEC, fontLhs, fontRhs, m_strCurrentDocument);

            FormButtons res = FormButtons.Replace;  // default behavior
            bool bModified = false, bContinue = true;
			string strReplacementCharacter = (aEC.IsRhsLegacy) ? "?" : "\ufffd";
            do
            {
                string strInput = GetCurrentValue(dataIterator);
                if (strInput == null)
                    break;
                string strOutput = CallSafeConvert(aEC, strInput);

                UpdateStatusBar(String.Format("In '{0}', converting: '{1}' to '{2}'",
                    m_strCurrentDocument, strInput, strOutput));

                // if this string gets converted as a bunch of "?"s, it's probably an error. Show it to the
                //  user as a potential problem (unless we're already in single-step mode).
                bool bShowPotentialError = false;
                if (!SingleStep)
                {
                    const double cfMinErrorDetectPercentage = 0.9;
					string strWithoutErrors = strOutput.Replace(strReplacementCharacter, "");
                    if (strWithoutErrors.Length < (strOutput.Length * cfMinErrorDetectPercentage))
                    {
                        bShowPotentialError = true;
                        if (dlg == null)
                            dlg = new BaseConverterForm(aEC, fontLhs, fontRhs, m_strCurrentDocument);
                        dlg.Text = "Potential error detected: " + dlg.Text;
                    }
                }

                /* TODO: I think I'm stripping out inserted symbols altogether (no need for them)
                // did a single inserted symbol result in a multi-char result?
                if (dataIterator.IsInsertSymbolSituation && (strOutput.Length > 1))
                {
                    bShowPotentialError = true;
                    if (dlg == null)
                        dlg = new BaseConverterForm(aEC, fontLhs, fontRhs, m_strCurrentDocument);
                    dlg.Text = "Inserted Symbol is converted to multi-char result! You probably want to use FullyComposed or result will be truncated";
                }
                */

                // show user this one 
                if (    (   (res != FormButtons.ReplaceAll)
                        &&  SingleStep 
                        &&  (!dlg.SkipIdenticalValues || (strInput != strOutput))
                        )
                    ||  bShowPotentialError)
                {
                    res = dlg.Show(strInput, strOutput);
                    strOutput = dlg.ForwardString;  // just in case the user re-typed it
                }

                if ((res == FormButtons.Replace) || (res == FormButtons.ReplaceAll))
                {
					dataIterator.SetCurrentValue(strOutput);
                    bModified = true;
                }
                else if (res == FormButtons.Cancel)
                {
                    DialogResult dres = MessageBox.Show("If you have converted some of the document already, then cancelling now will leave your document in an unuseable state (unless you are doing 'spell fixing' or something which doesn't change the encoding). Click 'Yes' to confirm the cancellation or 'No' to continue with the conversion.", cstrCaption, MessageBoxButtons.YesNo);
                    if (dres == DialogResult.Yes)
                        throw new ApplicationException("User cancelled");
                    continue;
                }
                else
                {
                    Debug.Assert(res == FormButtons.Next);
                    res = FormButtons.Replace;  // reset for next time.
                }
                
                // don't put this in the while look in case the above 'continue' is executed (which will cause
                //  us to repeat the last word again)
                bContinue = dataIterator.MoveNext();

            } while (bContinue);

            return bModified;
        }

        protected bool SingleStep
        {
            get
            {
                bool b = (toolStripButtonSingleStep.Checked || singlestepConversionToolStripMenuItem.Checked);
                return b;
            }
            set
            {
                toolStripButtonSingleStep.Checked = singlestepConversionToolStripMenuItem.Checked = value;
            }
        }

        public void UpdateStatusBar(string strMessage)
        {
            textBoxStatus.Text = strMessage;
            Application.DoEvents();
        }

        public void UpdateStatusBarDocNamePlusOne(string strFormat, string strParam)
        {
            string strMessage = String.Format(strFormat, m_strCurrentDocument, strParam);
            textBoxStatus.Text = strMessage;
            Application.DoEvents();
        }

        public void UpdateStatusBarDocNamePlusTwo(string strFormat, string strParam1, string strParam2)
        {
            string strMessage = String.Format(strFormat, m_strCurrentDocument, strParam1, strParam2);
            textBoxStatus.Text = strMessage;
            Application.DoEvents();
        }

        internal static string GetTempFilename
        {
            get
            {
                return Path.GetTempFileName(); 
            }
        }

		protected bool CheckForWinWord()
		{
            bool bReady;
			do
			{
				bReady = true;
				Process[] myProcesses = Process.GetProcesses();
				foreach (Process myProcess in myProcesses)
				{
					if (myProcess.ProcessName.ToLower() == "winword")
					{
						var res = MessageBox.Show("It's best for any existing running instances of Microsoft Word (including Outlook when Word is the email editor) to be closed before doing the conversion. Would you like to close any other instances?", cstrCaption, MessageBoxButtons.YesNoCancel);
                        if (res == DialogResult.Cancel)
                        {
                            return false;
                        }                        
                        else if (res == DialogResult.Yes)
                        {
                            try
                            {
                                myProcess.Close();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message, cstrCaption);
                            }
                            bReady = true; 
                            break;  // it's not absolutely necessary -- it just does things like wanting to close the normal.dot file... so just skip it if the user has tried this at least once...
                        }
                        bReady = false;
					}
				}
			} while (!bReady);
            return true;
		}

        protected void GetXmlDocuments(string[] astrFileNames)
        {
            // TODO: see what happens if PIAs aren't installed
            UpdateStatusBar("Starting Word...");
            if (!CheckForWinWord())
                return;

            var wrdApp = new Word.Application();
            try
            {
                // first open all of the given files and save them as xml so we can work with them.
                foreach (string strDocFilename in astrFileNames)
                {
                    m_strCurrentDocument = Path.GetFileName(strDocFilename);
                    UpdateStatusBar(String.Format("Examining '{0}'", m_strCurrentDocument));

                    // convert the document to XML and get an XmlDoc for it (on which we can do queries for data)
                    var doc = ConvertDocToXml(wrdApp, strDocFilename);

                    // put it in a map if it exists
                    if (doc != null)
                        m_mapDocName2XmlDocument.Add(strDocFilename, doc);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, cstrCaption);
            }
            finally
            {
                wrdApp.Quit(false);
                Marshal.ReleaseComObject(wrdApp);
                wrdApp = null;
            }
        }

        // this gets the text iterators for both Style (based on font name) and Custom formatted
        //  (i.e. "Styles & Custom formatting" = "do it all")
        protected void GetTextIteratorListStyleFont(ref List<string> lstInGrid)
        {
            foreach (var kvp in m_mapDocName2XmlDocument)
            {
                m_strCurrentDocument = Path.GetFileName(kvp.Key);
                var doc = kvp.Value;
                doc.InitializeIteratorsFontsFromStyles(lstInGrid, DisplayInGrid);
            }
        }

        protected void GetTextIteratorListStyleOnly(ref List<string> lstInGrid)
        {
            foreach (var kvp in m_mapDocName2XmlDocument)
            {
                m_strCurrentDocument = Path.GetFileName(kvp.Key);
                var doc = kvp.Value;
                doc.InitializeIteratorsStyleName(lstInGrid, DisplayInGrid);
            }
        }

        protected void GetTextIteratorListCustomFont(ref List<string> lstInGrid)
        {
            foreach (var kvp in m_mapDocName2XmlDocument)
            {
                m_strCurrentDocument = Path.GetFileName(kvp.Key);
                var doc = kvp.Value;
                doc.InitializeIteratorsCustomFontNames(lstInGrid, DisplayInGrid);
            }
        }

        private void dataGridView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            int nColumnIndex = e.ColumnIndex;
            // if the user clicks on the header... that doesn't work
            if (((e.RowIndex < 0) || (e.RowIndex > dataGridView.Rows.Count))
                || ((nColumnIndex < cnFontStyleColumn) || (nColumnIndex > cnTargetFontColumn)))
                return;

            DataGridViewRow theRow = this.dataGridView.Rows[e.RowIndex];
            string strFontStyleName = (string)theRow.Cells[cnFontStyleColumn].Value;
            DataGridViewCell theCell = theRow.Cells[e.ColumnIndex];
            DirectableEncConverter aEC = null;
            switch (nColumnIndex)
            {
                case cnExampleDataColumn:
                    UpdateSampleValue(theRow);
                    break;

                case cnEncConverterColumn:
                    string strExampleData = (string)theRow.Cells[cnExampleDataColumn].Value;

                    if (e.Button == MouseButtons.Right)
                    {
                        aEC = m_aECLast;
                    }
                    else
                    {
                        EncConverters aECs = GetEncConverters;
                        if (aECs != null)
                        {
                            string strFontName = null;
                            if (!radioButtonStylesOnly.Checked)
                                strFontName = strFontStyleName;
                            IEncConverter aIEC = aECs.AutoSelectWithData(strExampleData, strFontName, ConvType.Unknown, "Select Converter");
                            if (aIEC != null)
                                aEC = new DirectableEncConverter(aIEC);
                        }
                    }

                    if (aEC != null)
                    {
                        DefineConverter(strFontStyleName, aEC);
                    }
                    else if (IsConverterDefined(strFontStyleName))
                    {
                        m_mapEncConverters.Remove(strFontStyleName);
                    }

                    UpdateExampleDataColumns(theRow, strExampleData);
                    UpdateConverterCellValue(theCell, aEC);
                    m_aECLast = aEC;
                    break;

                case cnTargetFontColumn:
                    Font font = null;
                    if ((e.Button == MouseButtons.Right) && (m_aFontLast != null))
                    {
                        font = m_aFontLast;
                    }
                    else
                    {
                        fontDialog.Font = (m_aFontLast != null)
                                              ? mapName2Font[strFontStyleName]
                                              : new Font("Arial Unicode MS", 12);   // the 1st time this is done, set this as the font, since it doesn't otherwise want to show up!?

                        if (fontDialog.ShowDialog() == DialogResult.OK)
                        {
                            m_aFontLast = font = fontDialog.Font;
                        }
                    }

                    if (font != null)
                    {
                        if (mapName2Font.ContainsKey(strFontStyleName))
                            mapName2Font.Remove(strFontStyleName);
                        mapName2Font.Add(strFontStyleName, font);
                        UpdateTargetFontCellValue(theRow, font);
                    }
                    break;
            }
        }

        internal static EncConverters GetEncConverters
        {
            get
            {
                try
                {
                    return DirectableEncConverter.EncConverters;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Can't access the repository because: " + ex.Message, cstrCaption); 
                }
                return null;
            }
        }

        private void setDefaultConverterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;

            EncConverters aECs = GetEncConverters;
            if (aECs != null)
            {
                IEncConverter aIEC = aECs.AutoSelectWithTitle(ConvType.Unknown, "Select Default Converter");
                if (aIEC != null)
                {
                    DirectableEncConverter aEC = new DirectableEncConverter(aIEC);
                    foreach (DataGridViewRow aRow in dataGridView.Rows)
                    {
                        string strFontStyle = (string)aRow.Cells[cnFontStyleColumn].Value;
                        if (!IsConverterDefined(strFontStyle))
                        {
                            DefineConverter(strFontStyle, aEC);    // add it
                            aRow.Cells[cnEncConverterColumn].Value = aEC.Name;
                            aRow.Cells[cnEncConverterColumn].ToolTipText = aEC.ToString();
                            string strInput = (string)aRow.Cells[cnExampleDataColumn].Value;
                            aRow.Cells[cnExampleOutputColumn].Value = CallSafeConvert(aEC, strInput);
                        }
                    }

                    // clear the last one selected so that a right-click can be used to cancel the selection
                    m_aECLast = null;
                }
            }

            Cursor = Cursors.Default;
        }

        private void UpdateConverterNames()
        {
            foreach (DataGridViewRow aRow in dataGridView.Rows)
            {
                string strFontStyle = (string)aRow.Cells[cnFontStyleColumn].Value;
                UpdateExampleDataColumns(aRow, (string)aRow.Cells[cnExampleDataColumn].Value);
                UpdateConverterCellValue(aRow.Cells[cnEncConverterColumn], GetConverter(strFontStyle));
                if (mapName2Font.ContainsKey(strFontStyle))
                    UpdateTargetFontCellValue(aRow, mapName2Font[strFontStyle]);
            }
        }
        
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_mapEncConverters.Clear();
            UpdateConverterNames();
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlgSettings = new OpenFileDialog();
            dlgSettings.DefaultExt = "fcm";
            dlgSettings.InitialDirectory = Application.UserAppDataPath;
            dlgSettings.Filter = "Font-Style Converter mapping files (*.fcm)|*.fcm|All files|*.*";
            dlgSettings.RestoreDirectory = true;

            if (dlgSettings.ShowDialog() == DialogResult.OK)
            {
                LoadConverterMappingFile(dlgSettings.FileName);
            }
        }

        protected void LoadConverterMappingFile(string strFilename)
        {
            FileStream fs = new FileStream(strFilename, FileMode.Open);

            // Construct a SoapFormatter and use it 
            // to serialize the data to the stream.
            try
            {
                SoapFormatter formatter = new SoapFormatter();
                formatter.Binder = new DirectableEncConverterDeserializationBinder();
                formatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
                Hashtable map = (Hashtable)formatter.Deserialize(fs);
                foreach (string strKey in map.Keys)
                {
                    if (mapName2Font.ContainsKey(strKey))
                        mapName2Font.Remove(strKey);
                    mapName2Font.Add(strKey, (Font)map[strKey]);
                }

                m_mapEncConverters = (Hashtable)formatter.Deserialize(fs);
                AddToConverterMappingRecentlyUsed(strFilename);
            }
            catch (SerializationException ex)
            {
                MessageBox.Show("Failed to open mapping file. Reason: " + ex.Message + Environment.NewLine + "Sorry... this must be from an incompatible version...", cstrCaption);
                if (m_mapEncConverters == null)
                    m_mapEncConverters = new Hashtable();   // the rest of the program doesn't like this to potentially be null
            }
            finally
            {
                fs.Close();
            }

            if ((m_mapEncConverters != null) && (m_mapEncConverters.Count > 0))
                UpdateConverterNames();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlgSettings = new SaveFileDialog();
            dlgSettings.DefaultExt = "fcm";
            dlgSettings.FileName = "Font-Style Converter mapping1.fcm";
            if (!Directory.Exists(Application.UserAppDataPath))
                Directory.CreateDirectory(Application.UserAppDataPath);
            dlgSettings.InitialDirectory = Application.UserAppDataPath;
            dlgSettings.Filter = "Font-Style Converter mapping files (*.fcm)|*.fcm|All files|*.*";
            dlgSettings.RestoreDirectory = true;

            if (dlgSettings.ShowDialog() == DialogResult.OK)
            {
                // Construct a SoapFormatter and use it 
                // to serialize the data to the stream.
                FileStream fs = new FileStream(dlgSettings.FileName, FileMode.Create);
                SoapFormatter formatter = new SoapFormatter();
                try
                {
                    formatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
                    
                    // save the (font/style) name to Font map (have to put it into a Hashtable, because we can't
                    //  (yet) serialize generic types
                    Hashtable map = new Hashtable();
                    foreach (KeyValuePair<string, Font> kvp in mapName2Font)
                        if (kvp.Value.Name != "Microsoft Sans Serif")
                            map.Add(kvp.Key, kvp.Value);

                    formatter.Serialize(fs, map);
                    formatter.Serialize(fs, m_mapEncConverters);
                    AddToConverterMappingRecentlyUsed(dlgSettings.FileName);
                }
                catch (SerializationException ex)
                {
                    MessageBox.Show("Failed to save! Reason: " + ex.Message, cstrCaption);
                }
                finally
                {
                    fs.Close();
                }
            }
        }

        private void radioButtonFontsOnly_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            PopulateGrid();
            Cursor = Cursors.Default;
        }

        private void radioButtonStylesOnly_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            PopulateGrid();
            Cursor = Cursors.Default;
        }

        private void radioButtonEverything_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            PopulateGrid();
            Cursor = Cursors.Default;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        void recentFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripDropDownItem aRecentFile = (ToolStripDropDownItem)sender;
            try
            {
                Program.FileNames = new string[] { aRecentFile.Text };
                Program.myTimer.Start();
            }
            catch (Exception ex)
            {
                // probably means the file doesn't exist anymore, so remove it from the recent used list
                Properties.Settings.Default.RecentFiles.Remove(aRecentFile.Text);
                MessageBox.Show(ex.Message, cstrCaption);
            }
        }

        void converterMapRecentFilesMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripDropDownItem aRecentFile = (ToolStripDropDownItem)sender;
            try
            {
                LoadConverterMappingFile(aRecentFile.Text);
            }
            catch (Exception ex)
            {
                // probably means the file doesn't exist anymore, so remove it from the recent used list
                Properties.Settings.Default.ConverterMappingRecentFiles.Remove(aRecentFile.Text);
                MessageBox.Show(ex.Message, cstrCaption);
            }
        }

        private void fileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            recentFilesToolStripMenuItem.DropDownItems.Clear();
            foreach (string strRecentFile in Properties.Settings.Default.RecentFiles)
                recentFilesToolStripMenuItem.DropDownItems.Add(strRecentFile, null, recentFilesToolStripMenuItem_Click);

            recentFilesToolStripMenuItem.Enabled = (recentFilesToolStripMenuItem.DropDownItems.Count > 0);
        }

        private void converterMappingsToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            recentToolStripMenuItem.DropDownItems.Clear();
            foreach (string strRecentFile in Properties.Settings.Default.ConverterMappingRecentFiles)
                recentToolStripMenuItem.DropDownItems.Add(strRecentFile, null, converterMapRecentFilesMenuItem_Click);

            recentToolStripMenuItem.Enabled = (recentToolStripMenuItem.DropDownItems.Count > 0);
        }

        private void reloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.myTimer.Start();
        }

        private void toolStripButtonSingleStep_CheckStateChanged(object sender, EventArgs e)
        {
            singlestepConversionToolStripMenuItem.Checked = ((ToolStripButton)sender).Checked;
        }

        private void singlestepConversionToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            toolStripButtonSingleStep.Checked = ((ToolStripMenuItem)sender).Checked;
        }

        protected void BackupFile(FileInfo fiFrom, FileInfo fiTo)
        {
            Debug.Assert(fiFrom.Exists);
            if (fiFrom.Exists)
            {
                // make sure the target folder exists
                if (!fiTo.Directory.Exists)
                    fiTo.Directory.Create();

                // see if the file exists on the target
                while (fiTo.Exists)
                {
                    // check to see if they have the same last modified stamp
                    //  (if so, then skip it)
                    // If copying to a FAT32 drive, then the timestamp from NTFS
                    //  can be as much as 2 seconds off, so make sure it's greater
                    //  than that before considering these different
                    DateTime dtTo = fiTo.LastWriteTime;
                    DateTime dtFrom = fiFrom.LastWriteTime;
                    TimeSpan ts = dtFrom - dtTo;
                    TimeSpan tsMinSignificant = new TimeSpan(0, 0, 2);
                    if (Math.Abs(ts.TotalSeconds) < tsMinSignificant.TotalSeconds)
                        return;

                    // otherwise, make it a copy of the original
                    string strNewName = String.Format(@"{0}\Copy of {1}",
                        fiTo.DirectoryName, fiTo.Name);
                    fiTo = new FileInfo(strNewName);
                }

                // copy file!
                CopyFile(fiFrom, fiTo);
            }
        }

        protected void CopyFile(FileInfo fiFrom, FileInfo fiTo)
        {
            Debug.Assert(fiFrom.Exists);
            Debug.Assert(!fiTo.Exists);

            try
            {
                File.Copy(fiFrom.FullName, fiTo.FullName, true);

                // when making copies, remove the Read Only attribute if set
                fiTo.Attributes = FileAttributes.Archive;
            }
            catch (Exception ex)
            {
                string strErrorMsg = String.Format("Unable to copy from '{0}' to '{1}'", fiFrom.FullName, fiTo.FullName);
                throw new System.Exception(strErrorMsg, ex);
            }
        }

        private void autoSearchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Reset();
            AutoSearchOptions dlg = new AutoSearchOptions();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                UpdateStatusBar("Starting Word...");
                CheckForWinWord();
                Word.Application wrdApp = new Word.Application();
                try
                {
                    // first collect all the files that we'll eventually want to process (i.e. those that match the 
                    //  filter and use one or more of the fonts in the search filter)
                    string strSearchPathRoot = dlg.SearchPath;
                    string strTimeDateStamp = DateTime.Now.ToString("yyyy-MM-dd hh.mm.ss tt");
                    string strUserBackupPath = String.Format(@"{0}\{1}", dlg.StorePath, strTimeDateStamp);
                    string strAppBackupPath = String.Format(@"{0}\Backup\{1}", Application.UserAppDataPath, strTimeDateStamp);
                    DirectoryInfo diForSearch = new DirectoryInfo(strSearchPathRoot);
                    List<FileInfo> afiFilesInToPath = new List<FileInfo>();

                    RecursePath(ref afiFilesInToPath, dlg.SearchFilters, dlg.FontsToSearchFor, diForSearch, wrdApp,
                        strSearchPathRoot, strUserBackupPath, strAppBackupPath, dlg.ConvertBackupFiles);

                    if (afiFilesInToPath.Count > 0)
                    {
                        // take the files that matched the search criteria and pass them to the continuation of the open routine
                        //  Note: these are already opened and saved as (temporary) xml files AND are backed up to the AppData area. 
                        //  To recover the original filename (which is what the converted file will be saved as), the 
                        //  m_mapBackupNameToDocName has this mapping. Also, m_mapDocName2XmlDocument has all the xml doc objects 
                        //  that use one of the searched for fonts.
                        // But first, give the user a chance to cancel the request for certain files:
                        FilesToOpenListDlg dlgSelect = new FilesToOpenListDlg(ref m_mapDocName2XmlDocument, afiFilesInToPath, 
                            strAppBackupPath, (dlg.ConvertBackupFiles) ? strUserBackupPath : strSearchPathRoot);
                        if (dlgSelect.ShowDialog() == DialogResult.OK)
                        {
                            SetSaveConvertToolTip(dlg.StorePath, dlg.ConvertBackupFiles);
                            Program.FileNames = dlgSelect.FilesToOpen;
                            DoRestOfOpen(Program.FileNames);
                            this.reloadToolStripMenuItem.Enabled = this.toolStripButtonRefresh.Enabled = false;
                            return; // don't fall thru.
                        }
                    }

                    UpdateStatusBar("No files match the search criteria!");
                }
                catch (ApplicationException ex)
                {
                    if (ex.Message == cstrCancelSearch)
                    {
                        UpdateStatusBar("User-cancelled search");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    string strMsg = String.Format("Searching for Word documents failed! Reason: {0}", ex.Message);
                    if (ex.InnerException != null)
                        strMsg += String.Format("{0}{0}{1}", Environment.NewLine, ex.InnerException.Message);

                    MessageBox.Show(strMsg, cstrCaption);
                }
                finally
                {
                    ((Microsoft.Office.Interop.Word._Application)wrdApp).Quit(ref oMissing, ref oMissing, ref oMissing);
                    Marshal.ReleaseComObject(wrdApp);
                }
            }
        }

        protected void SetSaveConvertToolTip(string strBackupPath, bool bConvertBackupFiles)
        {
            string str = (bConvertBackupFiles) ? "copied to the backup folder '{0}'" : "in their original locations after saving a backup copy in '{0}'";
            this.convertAndSaveDocumentsToolStripMenuItem.ToolTipText = this.toolStripButtonConvertAndSave.ToolTipText =
                String.Format("Click to convert the Word document(s) {0}", String.Format(str, strBackupPath));
            
        }

        protected bool m_bAnnoyAboutReadonly = true;

        protected void RecursePath(ref List<FileInfo> afiFilesInToPath, List<string> astrSearchPatterns, List<string> astrFontsToSearchFor,
            DirectoryInfo diSearchSource, Word.Application wrdApp, string strSearchPathRoot, string strUserBackupPath, string strAppBackupPath, 
            bool bConvertBackupFiles)
        {
            // the GetFiles can only handle a single filter at a time, so do them that way
            foreach (string strSearchPattern in astrSearchPatterns)
            {
                try
                {
                    foreach (FileInfo fi in diSearchSource.GetFiles(strSearchPattern))
                    {
                        string strFolderOffset = String.Format(@"{0}\{1}", fi.DirectoryName.Substring(strSearchPathRoot.Length), fi.Name);
                        UpdateStatusBar(String.Format("Searching in '...{0}' ", strFolderOffset));

                        DocXmlDocument doc;
                        // 'GetFiles' will match *.docx when searching for *.doc, so make sure they're the same
                        // then, if this file is using one of the searched for fonts...
                        if ((Path.GetExtension(fi.Name.ToLower()) == Path.GetExtension(strSearchPattern))
                            && HasFont(fi.FullName, wrdApp, astrFontsToSearchFor, out doc))
                        {
                            // now we should have an xml object representing of this file
                            Debug.Assert(doc != null);
                            UpdateStatusBar(String.Format("Found font data in '...{0}' ", strFolderOffset));

                            // first make a backup of the file in two locations: where the user requested it and in our AppData area (I don't
                            //  trust users, and want a backup of my own in case they complain that this program clobbered their data)
                            // backup to user requested backup path
                            string strUserBackup = strUserBackupPath + strFolderOffset;
                            BackupFile(fi, new FileInfo(strUserBackup));

                            // backup to App definitely writable location
                            string strAppBackup = strAppBackupPath + strFolderOffset;
                            BackupFile(fi, new FileInfo(strAppBackup));

                            // we'll "open" the copied (backup) file (copied to the AppData area), 
                            //  process that, and write it out (if successful) with the original name. So save this mapping.
                            if (bConvertBackupFiles)
                            {
                                m_mapBackupNameToDocName.Add(strAppBackup, strUserBackup);
                            }
                            else
                            {
                                // if we're supposed to convert the files in-situ, then they can't be readonly
                                if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                {
                                    if (m_bAnnoyAboutReadonly && MessageBox.Show(String.Format("The file '{0}' is read-only, which means we won't be able to save it after conversion. Would you like me to reset the read-only property on this and all subsequent files?", fi.FullName), cstrCaption, MessageBoxButtons.YesNoCancel) != DialogResult.Yes)
                                        throw new ApplicationException(cstrCancelSearch);   // refuse to go on if they don't say 'yes'

                                    // stop being annoying and remove the readonly attribute
                                    m_bAnnoyAboutReadonly = false;
                                    fi.Attributes &= ~FileAttributes.ReadOnly;
                                }
                                m_mapBackupNameToDocName.Add(strAppBackup, fi.FullName);
                            }

                            // put the appData file into a list so we can pass that to the open routines.
                            afiFilesInToPath.Add(new FileInfo(strAppBackup));

                            // finally, keep track of our mapping of xml document to original file it was based on.
                            m_mapDocName2XmlDocument.Add(strAppBackup, doc);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // if we get an UnauthorizedAccessException exception, then just ignore these files
                }
            }

            // now recurse to all possible sub-folders and redo
            DirectoryInfo[] dis = null;
            try
            {
                dis = diSearchSource.GetDirectories();
            }
            catch (UnauthorizedAccessException)
            {
                // if we get an UnauthorizedAccessException exception, then just ignore these folders
                return;
            }

            foreach (DirectoryInfo di in dis)
                RecursePath(ref afiFilesInToPath, astrSearchPatterns, astrFontsToSearchFor, di, wrdApp,
                    strSearchPathRoot, strUserBackupPath, strAppBackupPath, bConvertBackupFiles);
        }

        // this routine will convert the given document to an xml file and then look inside it to see if 
        //  one of the list of font names are used within it. If so, then it will also set the doc output param
        protected bool HasFont(string strDocFilename, Word.Application wrdApp, List<string> astrFontsToSearchFor, out DocXmlDocument doc)
        {
            try
            {
                // convert the document to XML and get an XmlDoc for it (on which we can do XPath queries
                doc = ConvertDocToXml(wrdApp, strDocFilename);

                // put it in a map if it exists
                if ((doc != null) && doc.HasFonts(astrFontsToSearchFor))
                    return true;
            }
            catch (Exception ex)
            {
                string strMsg = String.Format("Unable to process the file '{0}'. Reason: {1}", m_strCurrentDocument, ex.Message);
                if (ex.InnerException != null)
                    strMsg += String.Format("{0}{0}{1}", Environment.NewLine, ex.InnerException.Message);

                strMsg += String.Format("{0}{0}{1}", Environment.NewLine, "Do you want to continue, skipping this file?");
                
                DialogResult res = MessageBox.Show(strMsg, cstrCaption, MessageBoxButtons.YesNoCancel);
                if (res != DialogResult.Yes)
                    throw new ApplicationException(cstrCancelSearch);
            }

            doc = null;
            return false;
        }

        internal void AddFontIfNeeded(DocXmlDocument doc, string strStyleName)
        {
            if (!mapName2Font.ContainsKey(strStyleName))
            {
                Debug.Assert(doc.MapStyleName2FontName.ContainsKey(strStyleName));
                string strFontName = doc.MapStyleName2FontName[strStyleName];
                Font font = CreateFontSafe(strFontName);
                mapName2Font.Add(strStyleName, font);
                RowMaxHeight = Math.Max(RowMaxHeight, font.Height);
            }
        }

        private void SomeCheckStateChangedRequiringReload(object sender, EventArgs e)
        {
            Program.myTimer.Start();    // force reload
        }
    }
}