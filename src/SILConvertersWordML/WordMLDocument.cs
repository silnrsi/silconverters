using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;

namespace SILConvertersWordML
{
    // common stuff for Word 2003 and 2007
    public abstract class WordMLDocument : DocXmlDocument
    {
        protected WordMLDocument()
            : base(new MapIteratorListXPath())
        {
            XmlDocument = new XmlDocument();
        }

        protected XmlDocument XmlDocument { get; set; }

        protected List<string> LstFontNamesCustom = new List<string>();
        protected List<string> LstFontNamesSymbolText = new List<string>();
        protected List<string> LstFontNamesPStyle = new List<string>();
        protected List<string> LstFontNamesCStyle = new List<string>();
        protected List<string> LstPStyleIdList = new List<string>();
        protected List<string> LstCStyleIdList = new List<string>();
        protected List<string> LstDefaultStyleFontName = new List<string>();

        // XPath expressions for insuring these font names exist
        //  These are used after conversion, where we rewrite the font names listed by writing all the possible 
        //  permutations (because changing the 'ascii' fontname doesn't help if the result of the conversion, 
        //  is a complex script font).
        public const string cstrXPathExprFontAscii = "w:rPr/w:rFonts/@w:ascii";
        public const string cstrXPathExprFontCS = "w:rPr/w:rFonts/@w:cs";
        public const string cstrXPathExprFont = "w:rPr/wx:font/@wx:val";
        public const string cstrXPathExprSymFont = "w:sym/@w:font";

        public const string cstrXPathExprCS = "w:rPr/w:cs";
        public const string cstrXPathExprStyleName = "w:name/@w:val";
        public const string cstrXPathExprStyleId = "@w:styleId";
        public const string cstrXPathExprPStyleFont = "w:rPr/wx:font/@wx:val";
        public const string cstrXPathExprCStyleFont = "w:rPr/w:rFonts/@w:ascii";

        protected XPathExpression m_xpeWFontValAscii = null;
        protected XPathExpression m_xpeWFontValFareast = null;
        protected XPathExpression m_xpeWFontValHAnsi = null;
        protected XPathExpression m_xpeWFontValCS = null;

        protected XPathExpression m_xpeStyleName = null;
        protected XPathExpression m_xpeStyleId = null;
        protected XPathExpression m_xpePStyleFont = null;
        protected XPathExpression m_xpeCStyleFont = null;

        protected abstract string XPathExprFontFareast { get; }
        protected abstract string XPathExprFontHAnsi { get; }
        protected abstract string XPathGetDefPStyleFontName { get; }
        protected abstract string XPathGetSymbolFontName { get; }
        protected abstract string XPathGetPStyleFontNames { get; }
        protected abstract string XPathGetCStyleFontNames { get; }
        protected abstract string XPathGetPStyle { get; }
        protected abstract string XPathGetCStyle { get; }

        public abstract string XPathFormatGetFontText { get; }
        public abstract string XPathFormatGetSymbolFontText { get; }
        public abstract string XPathFormatGetDefaultPStyleFontText { get; }
        public abstract string XPathFormatGetPStyleFontText { get; }
        public abstract string XPathFormatGetCStyleFontText { get; }
        public abstract string XPathFormatGetPStyleText { get; }
        public abstract string XPathFormatGetCStyleText { get; }

        protected abstract void GetCustomFontLists();
        protected abstract void ReplaceTextFontNameGetFontText(string strOldFontName, string strNewFontName);
        protected abstract void ReplaceSymbolTextFontNameGetFontText(string strOldFontName, string strNewFontName);
        protected abstract void ReplaceTextFontNameGetPStyleFontText(string strOldFontName, string strNewFontName);
        protected abstract void ReplaceTextFontNameGetCStyleFontText(string strOldFontName, string strNewFontName);
        protected abstract void ReplaceTextFontNameGetStyleText(string strStyleName, string strNewFontName);

        protected Dictionary<string, string> m_mapPrefix2NamespaceURI = new Dictionary<string, string>();
        protected const string cstrDefaultNameSpaceAbrev = "n";

        protected virtual void InitXPathExpressions()
        {
            InitXPathExpression(cstrXPathExprFontAscii, ref m_xpeWFontValAscii);
            InitXPathExpression(XPathExprFontFareast, ref m_xpeWFontValFareast);
            InitXPathExpression(XPathExprFontHAnsi, ref m_xpeWFontValHAnsi);
            InitXPathExpression(cstrXPathExprFontCS, ref m_xpeWFontValCS);

            InitXPathExpression(cstrXPathExprStyleName, ref m_xpeStyleName);
            InitXPathExpression(cstrXPathExprStyleId, ref m_xpeStyleId);
            InitXPathExpression(cstrXPathExprPStyleFont, ref m_xpePStyleFont);
            InitXPathExpression(cstrXPathExprCStyleFont, ref m_xpeCStyleFont);
        }

        /// <summary>
        /// get the full list of potential font and style names when we first open the XML documents so we don't
        ///  need to do this again (e.g. when the user selects a different radio button)
        /// </summary>
        /// <param name="strFilename"></param>
        public void GetFullNameLists(string strFilename)
        {
            // get all the fonts associated with custom formatting (into doc.lstFontNamesCustom)
            GetCustomFontLists();

            // get all inserted symbol items into lstFontNamesSymbolText
            GetFullNameList(XPathGetSymbolFontName, ref LstFontNamesSymbolText);

            // get all the fonts associated with style-based formatting (into m_astrFullFontNamesStyle)
            string strDefaultStyleFontName;
            GetNamedItem(XPathGetDefPStyleFontName, out strDefaultStyleFontName);
            LstDefaultStyleFontName.Add(strDefaultStyleFontName);

            GetFullNameList(XPathGetPStyleFontNames, ref LstFontNamesPStyle);
            GetFullNameList(XPathGetCStyleFontNames, ref LstFontNamesCStyle);

            // get all the style names (into m_astrFullStyleNameList)
            GetStyleNameIdLists(strFilename, XPathGetPStyle, m_xpePStyleFont, ref LstPStyleIdList);
            GetStyleNameIdLists(strFilename, XPathGetCStyle, m_xpeCStyleFont, ref LstCStyleIdList);
        }

        protected static string QuoteCharToUse(string strFontNameOrStyleId)
        {
            return (strFontNameOrStyleId.IndexOf('\'') != -1) ? "\"" : "'";
        }

        protected void InitXPathExpression(string strXPathExpr, ref XPathExpression xpe)
        {
            XPathNavigator navigator = XmlDocument.CreateNavigator();
            xpe = navigator.Compile(strXPathExpr);
            XmlNamespaceManager manager;
            GetNamespaceManager(navigator, out manager);
            xpe.SetContext(manager);
        }

        protected void GetNamespaceManager(XPathNavigator navigator, out XmlNamespaceManager manager)
        {
            manager = new XmlNamespaceManager(navigator.NameTable);
            foreach (KeyValuePair<string, string> kvp in m_mapPrefix2NamespaceURI)
                manager.AddNamespace(String.IsNullOrEmpty(kvp.Key) ? String.Empty : kvp.Key, kvp.Value);
        }

        protected void GetExpressionValue(XPathNodeIterator xpIterator, XPathExpression xpe, out string str)
        {
            XPathNodeIterator xpIteratorName = xpIterator.Current.Select(xpe);
            if (xpIteratorName.MoveNext())
                str = xpIteratorName.Current.Value;
            else
            {
                str = null;
                Debug.Assert(false); // not expecting this
            }
        }

        protected void RemoveElement(XPathNodeIterator xpIterator, XPathExpression xpe)
        {
            XPathNodeIterator xpIteratorName = xpIterator.Current.Select(xpe);
            if (xpIteratorName.MoveNext())
                xpIteratorName.Current.DeleteSelf();
        }

        /// <summary>
        /// InsureFontNameAttributes
        /// </summary>
        /// <param name="xpIterator"></param>
        /// <param name="xpe"></param>
        /// <param name="strNewFontName"></param>
        /// <param name="bCreateIfNotPresent">indicates whether to create the attribute/element if it doesn't already exist</param>
        /// <returns>true if the attribute was already present; false if not (whether it was created or not)</returns>
        protected bool InsureFontNameAttributes(XPathNodeIterator xpIterator, XPathExpression xpe,
            string strNewFontName, bool bCreateIfNotPresent)
        {
            XPathNodeIterator xpIteratorAttrib = xpIterator.Current.Select(xpe);
            if (xpIteratorAttrib.MoveNext())
            {
                xpIteratorAttrib.Current.SetValue(strNewFontName);
                return true;
            }
            else if (bCreateIfNotPresent)
            {
                xpIteratorAttrib = xpIterator.Clone();
                string strExpr = xpe.Expression;

                // this code only handles expressions of the form "w:x/y:z" so there should be 4 "parts")
                string[] astrSplit = strExpr.Split(new char[] { '/', ':', '@' }, StringSplitOptions.RemoveEmptyEntries);
                Debug.Assert(astrSplit.Length == 6);

                string strChildElementPrefix = astrSplit[0];
                string strNameSpace = m_mapPrefix2NamespaceURI[strChildElementPrefix];
                string strChildElementName = astrSplit[1];
                int nOffset = 0;
                if (astrSplit.Length == 6)
                {
                    if (!xpIteratorAttrib.Current.MoveToChild(strChildElementName, strNameSpace))
                    {
                        xpIteratorAttrib.Current.PrependChildElement(strChildElementPrefix, strChildElementName, strNameSpace, null);
                        bool bMoveRes = xpIteratorAttrib.Current.MoveToChild(strChildElementName, strNameSpace);
                        Debug.Assert(bMoveRes);
                    }
                    nOffset = 2;
                }

                strChildElementPrefix = astrSplit[0 + nOffset];
                strNameSpace = m_mapPrefix2NamespaceURI[strChildElementPrefix];
                strChildElementName = astrSplit[1 + nOffset];
                string strAttribPrefix = astrSplit[2 + nOffset];
                string strAttribName = astrSplit[3 + nOffset];

                if (!xpIteratorAttrib.Current.MoveToChild(strChildElementName, strNameSpace))
                {
                    xpIteratorAttrib.Current.PrependChildElement(strChildElementPrefix, strChildElementName, strNameSpace, null);
                    bool bMoveRes = xpIteratorAttrib.Current.MoveToChild(strChildElementName, strNameSpace);
                    Debug.Assert(bMoveRes);
                }

                xpIteratorAttrib.Current.CreateAttribute(strAttribPrefix, strAttribName, strNameSpace, strNewFontName);
            }

            return false;
        }

        protected void InitializeMapsFromStyleInfo(string strFont, string strId,
                                                   string strName, List<string> lstStyleId, string strFilename)
        {
            if (!lstStyleId.Contains(strId))
                lstStyleId.Add(strId);

            Debug.Assert(!MyMapIteratorList.MapStyleId2Name.ContainsKey(strId));
            MyMapIteratorList.MapStyleId2Name.Add(strId, strName);

            // Apparently, it isn't impossible to have multiple styles with the same name... 
            //  see c:\temp\Buku\Buku Latihan Fnlg_06.doc
            // System.Diagnostics.Debug.Assert(!mapStyleName2FontName.ContainsKey(strName));
            if (MapStyleName2FontName.ContainsKey(strName))
                MessageBox.Show(
                    String.Format(
                        "The Word document '{0}' contains two styles with the same name '{1}'. After the conversion, you should check the converted file carefully to see if the data in that style name was converted correctly. You may need to combine the segments into a single style for this to work properly.",
                        strFilename, strName), FontsStylesForm.cstrCaption);
            else
                MapStyleName2FontName.Add(strName, strFont);
        }

        protected void GetStyleNameIdLists(string strFilename, string strXPath2Style, XPathExpression xpeFont,
            ref List<string> lstDocStyleIdList)
        {
            XPathNodeIterator xpIterator = GetIterator(strXPath2Style);
            while (xpIterator.MoveNext())
            {
                string strName, strId, strFont;
                GetExpressionValue(xpIterator, m_xpeStyleName, out strName);
                GetExpressionValue(xpIterator, m_xpeStyleId, out strId);
                GetExpressionValue(xpIterator, xpeFont, out strFont);

                InitializeMapsFromStyleInfo(strFont, strId, strName, lstDocStyleIdList, strFilename);
            }
        }

        protected void ReplaceTextNameFormatAttribs(string strXPathFormat, string strOldFontName, string strNewFontName)
        {
            string strXPathReplaceFontName = String.Format(strXPathFormat,
                                                           QuoteCharToUse(strOldFontName),
                                                           strOldFontName);
            XPathNodeIterator xpIterator = GetIterator(strXPathReplaceFontName);
            while (xpIterator.MoveNext())
                InsureFontNameAttributes(xpIterator, strNewFontName, false);
        }

        protected virtual void InsureFontNameAttributes(XPathNodeIterator xpIterator, string strNewFontName, bool bCreateIfNotPresent)
        {
            bool bAsciiPresent = InsureFontNameAttributes(xpIterator, m_xpeWFontValAscii, strNewFontName, bCreateIfNotPresent) | bCreateIfNotPresent;

            // I have no idea whether this is going to work or not, but the fareast attribute causes Devanagari to screw
            //  up, so don't create it if it isn't already present
            InsureFontNameAttributes(xpIterator, m_xpeWFontValFareast, strNewFontName, false);
            InsureFontNameAttributes(xpIterator, m_xpeWFontValHAnsi, strNewFontName, bAsciiPresent);
            InsureFontNameAttributes(xpIterator, m_xpeWFontValCS, strNewFontName, bAsciiPresent);
        }

        protected void GetNameSpaceURIs(XmlNode nodeParent)
        {
            foreach (XmlNode node in nodeParent.ChildNodes)
            {
                string strPrefix = String.IsNullOrEmpty(node.Prefix) ? cstrDefaultNameSpaceAbrev : node.Prefix;
                if (!m_mapPrefix2NamespaceURI.ContainsKey(strPrefix) && !String.IsNullOrEmpty(node.NamespaceURI))
                    m_mapPrefix2NamespaceURI.Add(strPrefix, node.NamespaceURI);

                // recurse children
                GetNameSpaceURIs(node);
            }
        }

        protected void HarvestFontsAndStylesUsedInAllText(string strXmlFilename, string strDocFilename)
        {
            XmlDocument.Load(strXmlFilename);
            GetNameSpaceURIs(XmlDocument.DocumentElement);
            InitXPathExpressions();

            // get the full list of potential font and style names (these aren't what we'll present
            //  to the user, because we'll only show those that have some text, but just to get a
            //  full list that we won't have to a) requery or b) look beyond)
            GetFullNameLists(strDocFilename);
        }

        protected bool IsNamespaceRequired
        {
            get { return (m_mapPrefix2NamespaceURI.Count > 0); }
        }

        protected XPathNodeIterator GetIterator(string strXPath)
        {
            XPathNavigator navigator = XmlDocument.CreateNavigator();

            XPathNodeIterator xpIterator = null;
            if (IsNamespaceRequired)
            {
                XmlNamespaceManager manager;
                GetNamespaceManager(navigator, out manager);
                xpIterator = navigator.Select(strXPath, manager);
            }
            else
            {
                xpIterator = navigator.Select(strXPath);
            }

            return xpIterator;
        }

        public void GetFullNameList(string strXPath2Names, ref List<string> lstDocNameList)
        {
            XPathNodeIterator xpIteratorName = GetIterator(strXPath2Names);
            while (xpIteratorName.MoveNext())
            {
                string strName = xpIteratorName.Current.Value;
                if (!lstDocNameList.Contains(strName))
                    lstDocNameList.Add(strName);
            }
        }

        protected void GetNamedItem(string strXPathFormat, string strName, out string strNamedItem)
        {
            string strXPath2NamedItem = String.Format(strXPathFormat, strName);
            GetNamedItem(strXPath2NamedItem, out strNamedItem);
        }

        protected void GetNamedItem(string strXPathExpress, out string strNamedItem)
        {
            strNamedItem = null;
            XPathNodeIterator xpIteratorFontName = GetIterator(strXPathExpress);
            if (xpIteratorFontName.MoveNext())
                strNamedItem = xpIteratorFontName.Current.Value;
        }

        public bool GetTextIteratorForName(string strFontNameOrStyleId, string strXPathFormat,
                                           IteratorMap mapNames2Iterator, bool bConvertAsCharValue)
        {
            bool bAdded = false;
            // before creating and adding this one, make sure it isn't a duplicate (which
            //	can now only happen if the same font is found in two different documents).
            //	It's okay that we'll ignore it in subsequent documents, because before we 
            //	convert the files, we re-query for all of them
            if (!mapNames2Iterator.ContainsKey(strFontNameOrStyleId))
            {
                // get an iterator to see if there's any actual data in this font in this document
                //  (and don't add it here if not)
                string strXPathText = String.Format(strXPathFormat,
                                                    QuoteCharToUse(strFontNameOrStyleId),
                                                    strFontNameOrStyleId);
                XPathNodeIterator xpIteratorFontText = GetIterator(strXPathText);
                if ((bAdded = xpIteratorFontText.MoveNext()))
                    mapNames2Iterator.Add(strFontNameOrStyleId, new IteratorXPath(xpIteratorFontText, bConvertAsCharValue));
            }
            else
                Debug.Assert(!Program.IsOnlyOneDoc, "Bad assumption: multiple fonts found for the same type of text and *not* because it's multiple documents! Send this document to silconverters_support@sil.org for help");

            return bAdded;
        }

        // this gets the text iterators for custom font runs
        //  (i.e. "Fonts only" -- "only custom font runs")
        private void GetTextIteratorListFontCustom()
        {
            // now m_astrFullFontNameList is loaded, so let's see which of these has any text associated 
            foreach (var strFontName in LstFontNamesCustom)
            {
                Program.UpdateStatusBarDocNamePlusOne("Examining '{0}'... Searching for custom formatting with font '{1}'...",
                    strFontName);
                GetTextIteratorForName(strFontName, XPathFormatGetFontText,
                    MyMapIteratorList.MapFontNames2Iterator, false);
            }

            // also check for inserted symbols
            foreach (var strFontName in LstFontNamesSymbolText)
            {
                Program.UpdateStatusBarDocNamePlusOne("Examining '{0}'... Searching for inserted symbols with font '{1}'...",
                    strFontName);
                GetTextIteratorForName(strFontName, XPathFormatGetSymbolFontText,
                    MyMapIteratorList.MapSymbolFontNames2Iterator, true);
            }
        }

        public override void InitializeIteratorsCustomFontNames(List<string> lstInGrid, Action<string, DataIterator> displayInGrid)
        {
            // first initialize the Maps for this document
            if (!MyMapIteratorList.IsInitializedCustomFontName)
                GetTextIteratorListFontCustom();

            // put a clone in the grid
            foreach (var kvp in MyMapIteratorList.MapFontNames2Iterator.Where(kvp => !lstInGrid.Contains(kvp.Key)))
            {
                displayInGrid(kvp.Key, kvp.Value.Clone());
                lstInGrid.Add(kvp.Key);
            }

            // and add the stuff that's specific to our way of doing it
            foreach (var kvp in MyMapIteratorList.MapSymbolFontNames2Iterator.Where(kvp => !lstInGrid.Contains(kvp.Key)))
            {
                displayInGrid(kvp.Key, kvp.Value.Clone());
                lstInGrid.Add(kvp.Key);
            }
        }

        public override void InitializeIteratorsFontsFromStyles(List<string> lstInGrid, Action<string, DataIterator> displayInGrid)
        {
            if (!MyMapIteratorList.IsInitializedFontsFromStyles)
                GetTextIteratorListFontStyle();

            // put a clone in the grid (but only if we haven't already done one via the Custom font)
            foreach (var kvp in MyMapIteratorList.MapDefStyleFontNames2Iterator
                                                 .Where(kvp => !lstInGrid.Contains(kvp.Key)))
            {
                displayInGrid(kvp.Key, kvp.Value.Clone());
                lstInGrid.Add(kvp.Key);
            }

            foreach (var kvp in MyMapIteratorList.MapPStyleFontNames2Iterator
                                                 .Where(kvp => !lstInGrid.Contains(kvp.Key)))
            {
                displayInGrid(kvp.Key, kvp.Value.Clone());
                lstInGrid.Add(kvp.Key);
            }

            foreach (var kvp in MyMapIteratorList.MapCStyleFontNames2Iterator
                                                 .Where(kvp => !lstInGrid.Contains(kvp.Key)))
            {
                displayInGrid(kvp.Key, kvp.Value.Clone());
                lstInGrid.Add(kvp.Key);
            }
        }

        public override void InitializeIteratorsStyleName(List<string> lstInGrid, Action<string, DataIterator> displayInGrid)
        {
            if (!MyMapIteratorList.IsInitializedStyleName)
                GetTextIteratorListStyle();

            // put a clone in the grid
            foreach (var kvpIterator in MyMapIteratorList.MapStyleId2Iterator)
            {
                if (!MyMapIteratorList.MapStyleId2Name.ContainsKey(kvpIterator.Key)) 
                    continue;

                var strStyleName = MyMapIteratorList.MapStyleId2Name[kvpIterator.Key];
                if (lstInGrid.Contains(strStyleName)) 
                    continue;

                displayInGrid(strStyleName, kvpIterator.Value.Clone());
                lstInGrid.Add(strStyleName);
            }
        }

        // this gets the text iterators for Styles (based on a certain font) runs
        public void GetTextIteratorListFontStyle()
        {
            var strDefaultStyleFontName = LstDefaultStyleFontName.FirstOrDefault();
            if (!String.IsNullOrEmpty(strDefaultStyleFontName))
            {
                Program.UpdateStatusBarDocNamePlusOne("Examining '{0}'... Searching for default paragraph style-based formatting based on font '{1}'...", strDefaultStyleFontName);
                GetTextIteratorForName(strDefaultStyleFontName, XPathFormatGetDefaultPStyleFontText,
                    MyMapIteratorList.MapDefStyleFontNames2Iterator, false);
            }

            foreach (var strFontName in LstFontNamesPStyle)
            {
                Program.UpdateStatusBarDocNamePlusOne("Examining '{0}'... Searching for paragraph style-based formatting based on font '{1}'...", strFontName);
                GetTextIteratorForName(strFontName, XPathFormatGetPStyleFontText,
                    MyMapIteratorList.MapPStyleFontNames2Iterator, false);
            }

            foreach (var strFontName in LstFontNamesCStyle)
            {
                Program.UpdateStatusBarDocNamePlusOne("Examining '{0}'... Searching for character style-based formatting based on font '{1}'...",
                    strFontName);
                GetTextIteratorForName(strFontName, XPathFormatGetCStyleFontText,
                    MyMapIteratorList.MapCStyleFontNames2Iterator, false);
            }
        }

        // this gets the text iterators for style-based formatting that has text runs
        //  (i.e. "Style only" -- "only Style-based runs")
        public void GetTextIteratorListStyle()
        {
            GetTextIteratorStyleType("paragraph", LstPStyleIdList, XPathFormatGetPStyleText);
            GetTextIteratorStyleType("character", LstCStyleIdList, XPathFormatGetCStyleText);
        }

        protected void GetTextIteratorStyleType(string strStyleType, List<string> lstStyleIds,
            string strXPathFormatText)
        {
            foreach (string strStyleId in lstStyleIds)
            {
                // get the id of this style (which we've already looked up)
                // (it may not occur in this particular document... so skip it if not)
                if (MyMapIteratorList.MapStyleId2Name.ContainsKey(strStyleId))
                {
                    string strStyleName = MyMapIteratorList.MapStyleId2Name[strStyleId];
                    Program.UpdateStatusBarDocNamePlusTwo("Examining '{0}'... Searching for {1} style-based formatting based on style '{2}'...",
                        strStyleType, strStyleName);

                    if (GetTextIteratorForName(strStyleId, strXPathFormatText,
                        MyMapIteratorList.MapStyleId2Iterator, false))
                    {
                        Program.m_aForm.AddFontIfNeeded(this, strStyleName);
                    }
                }
            }
        }

        protected MapIteratorListXPath MyMapIteratorList
        {
            get { return MapIteratorList as MapIteratorListXPath; }
        }

        public override bool ConvertDocumentByFontNameAndStyle(Dictionary<string, Font> mapName2Font, Func<string, DataIterator, string, Font, bool, bool> convertDoc)
        {
            // mapFontNames2Iterator, has one iterator for each unique font (across all docs). If there's
            //  only one doc, then it's already loaded. But if there's more than one doc, then we have to treat each 
            //  one as if by itself (which unfortunately means empty the collection and requery)
            if (!Program.IsOnlyOneDoc)
            {
                MapIteratorList = new MapIteratorListXPath();

                // this initializes mapFontNames2Iterator and mapSymbolFontNames2Iterator
                GetTextIteratorListFontCustom();

                // this initializes mapDefStyleFontNames2Iterator, and mapP/CStyleFontNames2Iterator
                GetTextIteratorListFontStyle();
            }

            var bModified = false;
            foreach (string strFontName in MyMapIteratorList.MapFontNames2Iterator.Keys)
            {
                Debug.Assert(mapName2Font.ContainsKey(strFontName));
                Font fontTarget = mapName2Font[strFontName];

                bModified |= convertDoc(strFontName, MyMapIteratorList.MapFontNames2Iterator[strFontName],
                                        strFontName, fontTarget, false);

                // update the font name as well
                if (strFontName != fontTarget.Name)
                    ReplaceTextFontNameGetFontText(strFontName, fontTarget.Name);
            }

            foreach (string strFontName in MyMapIteratorList.MapSymbolFontNames2Iterator.Keys)
            {
                Debug.Assert(mapName2Font.ContainsKey(strFontName));
                Font fontTarget = mapName2Font[strFontName];

                bModified |= convertDoc(strFontName, MyMapIteratorList.MapSymbolFontNames2Iterator[strFontName],
                                        strFontName, fontTarget, true);

                // update the font name as well
                if (strFontName != fontTarget.Name)
                    ReplaceSymbolTextFontNameGetFontText(strFontName, fontTarget.Name);
            }

            foreach (string strFontNameOfStyle in MyMapIteratorList.MapDefStyleFontNames2Iterator.Keys)
            {
                Debug.Assert(mapName2Font.ContainsKey(strFontNameOfStyle));
                Font fontTarget = mapName2Font[strFontNameOfStyle];

                bModified |= convertDoc(strFontNameOfStyle, MyMapIteratorList.MapDefStyleFontNames2Iterator[strFontNameOfStyle],
                                        strFontNameOfStyle, fontTarget, false);
            }

            foreach (string strFontNameOfStyle in MyMapIteratorList.MapPStyleFontNames2Iterator.Keys)
            {
                Debug.Assert(mapName2Font.ContainsKey(strFontNameOfStyle));
                Font fontTarget = mapName2Font[strFontNameOfStyle];

                bModified |= convertDoc(strFontNameOfStyle, MyMapIteratorList.MapPStyleFontNames2Iterator[strFontNameOfStyle],
                                        strFontNameOfStyle, fontTarget, false);

                // update the font name as well
                if (strFontNameOfStyle != fontTarget.Name)
                    ReplaceTextFontNameGetPStyleFontText(strFontNameOfStyle, fontTarget.Name);
            }

            foreach (string strFontNameOfStyle in MyMapIteratorList.MapCStyleFontNames2Iterator.Keys)
            {
                Debug.Assert(mapName2Font.ContainsKey(strFontNameOfStyle));
                Font fontTarget = mapName2Font[strFontNameOfStyle];

                bModified |= convertDoc(strFontNameOfStyle, MyMapIteratorList.MapCStyleFontNames2Iterator[strFontNameOfStyle],
                                        strFontNameOfStyle, fontTarget, false);

                // update the font name as well
                if (strFontNameOfStyle != fontTarget.Name)
                    ReplaceTextFontNameGetCStyleFontText(strFontNameOfStyle, fontTarget.Name);
            }
            return bModified;
        }

        public override bool ConvertDocumentByStylesOnly(Dictionary<string, Font> mapName2Font, Func<string, DataIterator, string, Font, bool, bool> convertDoc)
        {
            // MapStyleId2Iterator, has one iterator for each unique style (across all docs). If there's
            //  only one doc, then we're done. But if there's more than one doc, then we have to treat each 
            //  one as if by itself (which unfortunately means empty the collection and requery)
            if (!Program.IsOnlyOneDoc)
            {
                MapIteratorList = new MapIteratorListXPath();
                GetTextIteratorListStyle();  // this initializes MapStyleId2Iterator
            }

            var bModified = false;
            foreach (string strStyleId in MyMapIteratorList.MapStyleId2Iterator.Keys)
            {
                if (MyMapIteratorList.MapStyleId2Name.ContainsKey(strStyleId))
                {
                    string strStyleName = MyMapIteratorList.MapStyleId2Name[strStyleId];

                    Debug.Assert(mapName2Font.ContainsKey(strStyleName));
                    Debug.Assert(MapStyleName2FontName.ContainsKey(strStyleName));
                    Font fontTarget = mapName2Font[strStyleName];
                    string strOrigFont = MapStyleName2FontName[strStyleName];

                    bModified |= convertDoc(strStyleName, MyMapIteratorList.MapStyleId2Iterator[strStyleId],
                                            strOrigFont, fontTarget, false);

                    // update the font name as well
                    if (strOrigFont != fontTarget.Name)
                        ReplaceTextFontNameGetStyleText(strStyleName, fontTarget.Name);
                }
            }
            return bModified;
        }

        public override bool ConvertDocumentByFontNameOnly(Dictionary<string, Font> mapName2Font, Func<string, DataIterator, string, Font, bool, bool> convertDoc)
        {
            // MapFontNames2Iterator, has one iterator for each unique font (across all docs). If there's
            //  only one doc, then we're done. But if there's more than one doc, then we have to treat each 
            //  one as if by itself (which unfortunately means empty the collection and requery)
            if (!Program.IsOnlyOneDoc)
            {
                MapIteratorList = new MapIteratorListXPath();

                // this initializes MapFontNames2Iterator and MapSymbolFontNames2Iterator
                GetTextIteratorListFontCustom();
            }

            var bModified = false;
            foreach (string strFontName in MyMapIteratorList.MapFontNames2Iterator.Keys)
            {
                Debug.Assert(mapName2Font.ContainsKey(strFontName));
                Font fontTarget = mapName2Font[strFontName];

                bModified |= convertDoc(strFontName, MyMapIteratorList.MapFontNames2Iterator[strFontName],
                                        strFontName, fontTarget, false);

                // update the font name as well
                if (strFontName != fontTarget.Name)
                    ReplaceTextFontNameGetFontText(strFontName, fontTarget.Name);
            }

            foreach (string strFontName in MyMapIteratorList.MapSymbolFontNames2Iterator.Keys)
            {
                Debug.Assert(mapName2Font.ContainsKey(strFontName));
                Font fontTarget = mapName2Font[strFontName];

                bModified |= convertDoc(strFontName, MyMapIteratorList.MapSymbolFontNames2Iterator[strFontName],
                                        strFontName, fontTarget, false);

                // update the font name as well
                if (strFontName != fontTarget.Name)
                    ReplaceSymbolTextFontNameGetFontText(strFontName, fontTarget.Name);
            }
            return bModified;
        }

        protected abstract string GetFindFontXPathExpression(List<string> astrFontsToSearchFor);

        public override bool HasFonts(List<string> astrFontsToSearchFor)
        {
            // see if this document has an instance of the font
            XPathNodeIterator xpIteratorFontName = GetIterator(GetFindFontXPathExpression(astrFontsToSearchFor));
            return xpIteratorFontName.MoveNext();
        }

        public override void Save(string strXmlOutputFilename)
        {
            XmlDocument.Save(strXmlOutputFilename);
        }
    }

    public class MapIteratorListXPath : MapIteratorList
    {
        public MapIteratorListXPath()
        {
            ResetMaps();
        }

        public IteratorMap MapFontNames2Iterator;           // IsInitializedCustomFontName
        public IteratorMap MapSymbolFontNames2Iterator;

        public IteratorMap MapDefStyleFontNames2Iterator;   // IsInitializedFontsFromStyles
        public IteratorMap MapPStyleFontNames2Iterator;
        public IteratorMap MapCStyleFontNames2Iterator;

        public IteratorMap MapStyleId2Iterator;             // IsInitializedStyleName

        public Dictionary<string, string> MapStyleId2Name = new Dictionary<string, string>();

        #region Overrides of MapIteratorList

        public override bool IsInitializedCustomFontName
        {
            get
            {
                return (MapFontNames2Iterator.Count > 0) ||
                       (MapSymbolFontNames2Iterator.Count > 0);
            }
        }

        public override bool IsInitializedFontsFromStyles   
        {
            get
            {
                return (MapDefStyleFontNames2Iterator.Count > 0) ||
                       (MapPStyleFontNames2Iterator.Count > 0) ||
                       (MapCStyleFontNames2Iterator.Count > 0);
            }
        }

        public override bool IsInitializedStyleName 
        {
            get { return (MapStyleId2Iterator.Count > 0); }
        }

        public override sealed void ResetMaps()
        {
            MapFontNames2Iterator = new IteratorMap();
            MapSymbolFontNames2Iterator = new IteratorMap();
            MapStyleId2Iterator = new IteratorMap();
            MapDefStyleFontNames2Iterator = new IteratorMap();
            MapPStyleFontNames2Iterator = new IteratorMap();
            MapCStyleFontNames2Iterator = new IteratorMap();
        }

        #endregion
    }
}
