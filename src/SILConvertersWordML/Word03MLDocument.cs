using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace SILConvertersWordML
{
    // specifics for Word 2003
    public class Word03MLDocument : WordMLDocument
    {
        // XPath expressions
        // get the total list of fonts in the document
        // these are done when the xml file is first opened to get a huristic of the things that we might need to
        //  look for. If something doesn't show up in the list that ought to, it could be a problem from this list
        //  (e.g. if the user is looking for a character style that is based on a complex script, then the last 
        //  entry here won't find it (since it's only looking for 'ascii')).
        public const string cstrXPathWordMLDefFontAscii = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/w:rFonts/@w:ascii";
        public const string cstrXPathWordMLDefFontFareast = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/w:rFonts/@w:fareast";
        public const string cstrXPathWordMLDefFontHAnsi = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/w:rFonts/@w:h-ansi";
        public const string cstrXPathWordMLDefFontCS = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/w:rFonts/@w:cs";

        // get the total list of fonts in the document
        // these are done when the xml file is first opened to get a huristic of the things that we might need to
        //  look for. If something doesn't show up in the list that ought to, it could be a problem from this list
        //  (e.g. if the user is looking for a character style that is based on a complex script, then the last 
        //  entry here won't find it (since it's only looking for 'ascii')).
        public const string cstrXPathWordMLGetFontNames = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/wx:font/@wx:val";
        public const string cstrXPathWordMLGetSymbolTextFontNames = "/w:wordDocument/w:body//w:p/w:r/w:sym/@w:font";

        // rde: 2010-07-01: the default paragraph style font isn't the w:fonts/w:defaultFonts, but rather the font
        //  of the Normal style... 
        // public const string cstrXPathWordMLGetDefaultPStyleFontName = "/w:wordDocument/w:fonts/w:defaultFonts/@w:ascii";    // this only handles the lhs legacy case (but having a default complex doesn't seem to depend on this key)
        public const string cstrXPathWordMLGetDefaultPStyleFontName = "/w:wordDocument/w:styles/w:style[@w:styleId = 'Normal']/w:rPr/w:rFonts/@w:ascii";
        public const string cstrXPathWordMLGetPStyleFontNames = "/w:wordDocument/w:styles/w:style[@w:type = 'paragraph']/w:rPr/wx:font/@wx:val";
        public const string cstrXPathWordMLGetCStyleFontNames = "/w:wordDocument/w:styles/w:style[@w:type = 'character']/w:rPr/w:rFonts/@w:ascii";    // this is only one of the 4 possibles, but at least it should cover the legacy to unicode conversion case

        public const string cstrXPathExprFontFareast = "w:rPr/w:rFonts/@w:fareast";
        public const string cstrXPathExprFontHAnsi = "w:rPr/w:rFonts/@w:h-ansi";

        protected XPathExpression m_xpeWxFontVal = null;
        protected XPathExpression m_xpeSymWFontVal = null;
        protected XPathExpression m_xpeCSVal = null;

        // get the total list of style names and ids in the document (that have an associated font)
        //  (these are used to fill the m_astrFull{P,C}StyleNameList collections which are 
        //  then iterated over during the searching of the file and also the mapping of style name to id
        public const string cstrXPathWordMLGetPStyle = "/w:wordDocument/w:styles/w:style[@w:type = 'paragraph'][w:rPr/wx:font/@wx:val]";
        public const string cstrXPathWordMLGetCStyle = "/w:wordDocument/w:styles/w:style[@w:type = 'character'][w:rPr/w:rFonts/@w:ascii]";

        // format to get the style id of a particular style name (as part of a complex search)
        // public const string cstrXPathWordMLFormatGetPStyleId = "/w:wordDocument/w:styles/w:style[@w:type = 'paragraph'][w:name/@w:val = '{0}']/@w:styleId";
        // public const string cstrXPathWordMLFormatGetCStyleId = "/w:wordDocument/w:styles/w:style[@w:type = 'character'][w:name/@w:val = '{0}']/@w:styleId";

        // formats for building XPath statements to get text entries for text
        //  the first one (for custom formatting) definitely needs to have "/w:r" because in some docs 
        //  (e.g. the MTT manual) that node is embedded in different things. The style based ones (i.e. the latter two)
        //  might also should be "/w:r", but it really blows the time of search out greatly, so I'm taking it out until
        //  I know for sure whether it can happen or not
        // public const string cstrXPathWordMLFormatGetFontText = "/w:wordDocument/w:body//w:p//w:r[w:rPr/wx:font/@wx:val = '{0}']/w:t";
        // rde: 5/10/10 adding "[not(w:rPr/w:rStyle/@w:val)]", so we can prevent the case where it has
        //  both style and (what appears to be) custom formatting.
        /* e.g. in:
        <w:r>
          <w:rPr>
            <w:rStyle w:val="GWGreekWord" />
            <wx:font wx:val="SIL Galatia" />
            <wx:sym wx:font="SIL Galatia" wx:char="F06D" />
          </w:rPr>
          <w:t>x</w:t>
        * the presence of w:rStyle/@w:val suggests style-based formatting (which gets picked up elsewhere, so we have to prevent it here)
        * and the presence of wx:font/@wx:val suggests custom formatting (which gets picked here)
        */
        // rde: 5/9/13 (ironic date -- 3 years after the above change)
        //  Actually, we also have a case like this:
        /*
          <w:rPr>
            <w:rStyle w:val="Strong" />
            <w:rFonts w:ascii="Kruti Dev 010" w:h-ansi="Kruti Dev 010" />
            <wx:font wx:val="Kruti Dev 010" />
            <w:sz w:val="36" />
            <w:sz-cs w:val="36" />
          </w:rPr>
          <w:t>isjsfjr dj dke</w:t>
        */
        // where the style is a character style, but which isn't a font-oriented character style (here it's just 'bold').
        //  So in this case, we really do what to have the custom font (i.e. w:rFonts + w:font) to override the style.
        //  SO I think instead of blocking this situation here, let's block it in the style based on (so we add not(w:rPr/wx:font/@wx:val) to that one
        //  to block it from picking this up also
        // public const string cstrXPathWordMLFormatGetFontText = "/w:wordDocument/w:body//w:p//w:r[not(w:rPr/w:rStyle/@w:val)][w:rPr/wx:font/@wx:val = {0}{1}{0}]/w:t";

        // so back to the original:
        public const string cstrXPathWordMLFormatGetFontText = "/w:wordDocument/w:body//w:p//w:r[w:rPr/wx:font/@wx:val = {0}{1}{0}]/w:t";


        public const string cstrXPathWordMLFormatGetSymbolFontChar = "/w:wordDocument/w:body//w:p//w:r/w:sym[@w:font = {0}{1}{0}]/@w:char";
        // "/w:wordDocument/w:body//w:p//w:r/w:sym[@w:font = '{0}']";

        // public const string cstrXPathWordMLFormatGetDefaultPStyleFontText = "/w:wordDocument/w:body//w:p/w:r[not(w:rPr)]/w:t";
        // The above version was changed to the following to better distinguish between default paragraph style and regular 
        //  custom formatted text (i.e. cstrXPathWordMLFormatGetFontText)--as you can see, the rules are about opposites of each other.
        //  This was added to fix the file: C:\temp\SC for Word\Obadiah\Obadiah.Kalam.doc
        // public const string cstrXPathWordMLFormatGetDefaultPStyleFontText = "/w:wordDocument/w:body//w:p//w:r[not(w:rPr/wx:font/@wx:val)]/w:t";
        // unfortunately, this then causes a problem with the non-default paragraph style (i.e. cstrXPathWordMLFormatGetPStyleFontText below)
        // so now we need to prevent this from finding the text twice
        //  The "not(w:pPr/w:pStyle/@w:val)" part says it isn't a regular paragraph style (which should get picked up by
        //  cstrXPathWordMLFormatGetPStyleFontText) and the "not(w:rPr/wx:font/@wx:val)" part says it isn't custom formatting
        //  which gets picked up by cstrXPathWordMLFormatGetFontText
        // The test file for this is L:\Kangri\Texts\Copy of Indian cult.doc
        public const string cstrXPathWordMLFormatGetDefaultPStyleFontText = "/w:wordDocument/w:body//w:p[not(w:pPr/w:pStyle/@w:val)]//w:r[not(w:rPr/wx:font/@wx:val)]/w:t";

        public const string cstrXPathWordMLFormatGetPStyleFontText = "/w:wordDocument/w:body//w:p[w:pPr/w:pStyle/@w:val = //w:styles/w:style[@w:type = 'paragraph'][w:rPr/wx:font/@wx:val = {0}{1}{0}]/@w:styleId]/w:r[not(w:rPr/wx:font)]/w:t";
        public const string cstrXPathWordMLFormatReplaceFontNameGetPStyleFontName = "/w:wordDocument/w:styles/w:style[@w:type = 'paragraph'][w:rPr/wx:font/@wx:val = {0}{1}{0}]";

        public const string cstrXPathWordMLFormatGetCStyleFontText = "/w:wordDocument/w:body//w:p//w:r[w:rPr/w:rStyle/@w:val = //w:styles/w:style[@w:type = 'character']/@w:styleId][w:rPr/wx:font/@wx:val = {0}{1}{0}]/w:t";
        public const string cstrXPathWordMLFormatReplaceFontNameGetCStyleFontName = "/w:wordDocument/w:styles/w:style[@w:type = 'character'][w:rPr/w:rFonts/@w:ascii = {0}{1}{0}]";

        public const string cstrXPathWordMLFormatGetPStyleText = "/w:wordDocument/w:body//w:p[w:pPr/w:pStyle/@w:val = {0}{1}{0}]/w:r[not(w:rPr/wx:font)]/w:t";

        // public const string cstrXPathWordMLFormatGetCStyleText = "/w:wordDocument/w:body//w:p/w:r[w:rPr/w:rStyle/@w:val = {0}{1}{0}]/w:t";
        //  see note above for cstrXPathWordMLFormatGetFontText
        public const string cstrXPathWordMLFormatGetCStyleText = "/w:wordDocument/w:body//w:p/w:r[w:rPr/w:rStyle/@w:val = {0}{1}{0}][not(w:rPr/wx:font/@wx:val)]/w:t";


        protected const string cstrXPathHasSingleCharacterRun = "/w:wordDocument/w:body//w:p/w:r[w:rPr/wx:sym/@wx:char]/w:t";

        // public const string cstrXPathWordMLFormatReplaceFontNameGetFontText = "/w:wordDocument/w:body//w:p//w:r[w:rPr/wx:font/@wx:val = '{0}']";
        // public const string cstrXPathWordMLFormatReplaceFontNameGetFontText = "/w:wordDocument/w:body//w:p//w:r[not(w:rPr/w:rStyle/@w:val)][w:rPr/wx:font/@wx:val = {0}{1}{0}]";
        // rde: 5/9/13: this needs to be changed again to all the replacing of font names that have been converted that are both character style-based and custom formatting
        //  since a character style like "bold" still might need the font changed. 
        // If there's a place to "replace" the font of a style in the run area, then we might need to block it from happening by adding a not(w:rPr/wx:font/@wx:val)
        //  (as here, we were trying to block the custom formatting from overriding the Cstyle formatting, which turned out to be wrong)
        public const string cstrXPathWordMLFormatReplaceFontNameGetFontText = "/w:wordDocument/w:body//w:p//w:r[w:rPr/wx:font/@wx:val = {0}{1}{0}]";

        public const string cstrXPathWordMLFormatReplaceFontNameNoRunParagraphs = "/w:wordDocument/w:body//w:p[w:pPr/w:rPr/wx:font/@wx:val = {0}{1}{0}]/w:pPr";
        public const string cstrXPathWordMLFormatReplaceFontNameNoRunCsParagraphs = "/w:wordDocument/w:body//w:p[w:pPr/w:rPr/w:rFonts/@w:cs = {0}{1}{0}]/w:pPr";

        public const string cstrXPathWordMLFormatReplaceFontNameGetSymbolFontText = "/w:wordDocument/w:body//w:p//w:r[w:sym/@w:font = {0}{1}{0}]";

        public const string cstrXPathWordMLFormatReplaceFontNameGetStyleText = "/w:wordDocument/w:styles/w:style[w:name/@w:val = {0}{1}{0}]";

        public const string cstrXPathFindFontFormatExpression = "/w:wordDocument/w:fonts/w:font[{0}]";
        protected const string cstrXPathFindFontFormat = "@w:name = {0}{1}{0}";

        public override string XPathFormatGetFontText
        {
            get { return cstrXPathWordMLFormatGetFontText; }
        }
        public override string XPathFormatGetSymbolFontText
        {
            get { return cstrXPathWordMLFormatGetSymbolFontChar; }
        }
        public override string XPathFormatGetDefaultPStyleFontText
        {
            get { return cstrXPathWordMLFormatGetDefaultPStyleFontText; }
        }
        public override string XPathFormatGetPStyleFontText
        {
            get { return cstrXPathWordMLFormatGetPStyleFontText; }
        }
        public override string XPathFormatGetCStyleFontText
        {
            get { return cstrXPathWordMLFormatGetCStyleFontText; }
        }
        public override string XPathFormatGetPStyleText
        {
            get { return cstrXPathWordMLFormatGetPStyleText; }
        }
        public override string XPathFormatGetCStyleText
        {
            get { return cstrXPathWordMLFormatGetCStyleText; }
        }
        protected override string XPathGetDefPStyleFontName
        {
            get { return cstrXPathWordMLGetDefaultPStyleFontName; }
        }
        protected override string XPathGetSymbolFontName
        {
            get { return cstrXPathWordMLGetSymbolTextFontNames; }
        }
        protected override string XPathGetPStyleFontNames
        {
            get { return cstrXPathWordMLGetPStyleFontNames; }
        }
        protected override string XPathGetCStyleFontNames
        {
            get { return cstrXPathWordMLGetCStyleFontNames; }
        }
        protected override string XPathGetPStyle
        {
            get { return cstrXPathWordMLGetPStyle; }
        }
        protected override string XPathGetCStyle
        {
            get { return cstrXPathWordMLGetCStyle; }
        }
        protected override string XPathExprFontFareast
        {
            get { return cstrXPathExprFontFareast; }
        }
        protected override string XPathExprFontHAnsi
        {
            get { return cstrXPathExprFontHAnsi; }
        }

        protected override void InitXPathExpressions()
        {
            base.InitXPathExpressions();
            InitXPathExpression(cstrXPathExprFont, ref m_xpeWxFontVal);
            InitXPathExpression(cstrXPathExprCS, ref m_xpeCSVal);
        }

        protected override void InsureFontNameAttributes(XPathNodeIterator xpIterator, string strNewFontName, bool bCreateIfNotPresent)
        {
            bool bRemoveCs = true;	// this is in case we're going from Unicode to Legacy
            if (bRemoveCs)
                RemoveElement(xpIterator, m_xpeCSVal);
            base.InsureFontNameAttributes(xpIterator, strNewFontName, bCreateIfNotPresent);
            InsureFontNameAttributes(xpIterator, m_xpeWxFontVal, strNewFontName, bCreateIfNotPresent);
        }

        protected override void GetCustomFontLists()
        {
            // first look in these places to get the actual full list of potential font names
            GetFullNameList(cstrXPathWordMLDefFontAscii, ref LstFontNamesCustom);
            GetFullNameList(cstrXPathWordMLDefFontFareast, ref LstFontNamesCustom);
            GetFullNameList(cstrXPathWordMLDefFontHAnsi, ref LstFontNamesCustom);
            GetFullNameList(cstrXPathWordMLDefFontCS, ref LstFontNamesCustom);
            GetFullNameList(cstrXPathWordMLGetFontNames, ref LstFontNamesCustom);
        }

        // if we replace the name of the font (i.e. //wx:font/@wx:val) associated with some text (i.e. [w:t]), 
        //  then we must also replace the nearby //w:rFonts/@w:* items or Word crashes
        protected override void ReplaceTextFontNameGetFontText(string strOldFontName, string strNewFontName)
        {
            ReplaceTextNameFormatAttribs(cstrXPathWordMLFormatReplaceFontNameGetFontText, strOldFontName, strNewFontName);
            ReplaceTextNameFormatAttribs(cstrXPathWordMLFormatReplaceFontNameNoRunParagraphs, strOldFontName, strNewFontName);
            ReplaceTextNameFormatAttribs(cstrXPathWordMLFormatReplaceFontNameNoRunCsParagraphs, strOldFontName, strNewFontName);
        }

        protected override void ReplaceSymbolTextFontNameGetFontText(string strOldFontName, string strNewFontName)
        {
            if (m_xpeSymWFontVal == null)
                InitXPathExpression(cstrXPathExprSymFont, ref m_xpeSymWFontVal);
            ReplaceSymbolTextNameFormatAttribs(cstrXPathWordMLFormatReplaceFontNameGetSymbolFontText, strOldFontName, strNewFontName);
        }

        protected void ReplaceSymbolTextNameFormatAttribs(string strXPathFormat, string strOldFontName, string strNewFontName)
        {
            string strXPathReplaceFontName = String.Format(strXPathFormat,
                                                           QuoteCharToUse(strOldFontName),
                                                           strOldFontName);
            XPathNodeIterator xpIterator = GetIterator(strXPathReplaceFontName);
            while (xpIterator.MoveNext())
            {
                InsureFontNameAttributes(xpIterator, strNewFontName, true);
                InsureFontNameAttributes(xpIterator, m_xpeSymWFontVal, strNewFontName, true);
            }
        }

        protected override void ReplaceTextFontNameGetPStyleFontText(string strOldFontName, string strNewFontName)
        {
            ReplaceTextNameFormatAttribs(cstrXPathWordMLFormatReplaceFontNameGetPStyleFontName, strOldFontName, strNewFontName);
        }

        protected override void ReplaceTextFontNameGetCStyleFontText(string strOldFontName, string strNewFontName)
        {
            ReplaceTextNameFormatAttribs(cstrXPathWordMLFormatReplaceFontNameGetCStyleFontName, strOldFontName, strNewFontName);
        }

        protected override void ReplaceTextFontNameGetStyleText(string strStyleName, string strNewFontName)
        {
            ReplaceTextNameFormatAttribs(cstrXPathWordMLFormatReplaceFontNameGetStyleText, strStyleName, strNewFontName);
        }

        public static DocXmlDocument GetXmlDocument(ref string strXmlFilename,
            string strDocFilename, bool bSaveXmlOutputInFolder)
        {
            // when opening the xml file, let's do an xslt pass on it so we can 
            //  merge all consecutive single-character runs into one. Word builds
            //  these (e.g. for multiple, consecutive Insert Symbol events), but we
            //  don't want them to be separate runs or we won't convert them as a
            //  block. And if we don't convert them as a block, then context effects
            //  (e.g. Greek final sigma) won't behave properly.
            string strXSLT = Properties.Resources.MergeSingleCharacterRunsWordML;
            MemoryStream streamXSLT = new MemoryStream(Encoding.UTF8.GetBytes(strXSLT));
#if DEBUG
            long lStartTime = DateTime.Now.Ticks;
#endif
            XmlReader xslReaderXSLT = XmlReader.Create(streamXSLT);

            XslCompiledTransform myProcessor = new XslCompiledTransform();
            XsltSettings xsltSettings = new XsltSettings { EnableScript = true };
            myProcessor.Load(xslReaderXSLT, xsltSettings, null);

            var doc = new Word03MLDocument();
            var strXsltOutputFilename = doc.GetOutputFileSpecForXmlFile(strDocFilename, bSaveXmlOutputInFolder);

            myProcessor.Transform(strXmlFilename, strXsltOutputFilename);
#if DEBUG
            long lDeltaTime = DateTime.Now.Ticks - lStartTime;
            System.Diagnostics.Debug.WriteLine(String.Format("Transform took: '{0}' ticks", lDeltaTime));
#endif

            strXmlFilename = strXsltOutputFilename;

            doc.Load(strXmlFilename);
            doc.GetNameSpaceURIs(doc.DocumentElement);
            doc.InitXPathExpressions();

            // get the full list of potential font and style names (these aren't what we'll present
            //  to the user, because we'll only show those that have some text, but just to get a
            //  full list that we won't have to a) requery or b) look beyond)
            doc.GetFullNameLists(strDocFilename);

            return doc;
        }

        protected override string GetFindFontXPathExpression(List<string> astrFontsToSearchFor)
        {
            System.Diagnostics.Debug.Assert(astrFontsToSearchFor.Count > 0);

            // /w:wordDocument/w:fonts/w:font[@w:name = "SAG-IPA Super SILDoulos" or @w:name = "Annapurna"]
            string strFontname = String.Format(cstrXPathFindFontFormat,
                                               QuoteCharToUse(astrFontsToSearchFor[0]),
                                               astrFontsToSearchFor[0]);
            for (int i = 1; i < astrFontsToSearchFor.Count; i++)
                strFontname += String.Format(" or " + cstrXPathFindFontFormat,
                                             QuoteCharToUse(astrFontsToSearchFor[i]),
                                             astrFontsToSearchFor[i]);

            return String.Format(cstrXPathFindFontFormatExpression, strFontname);
        }

        protected override string GetXmlFileSuffix
        {
            get { return FontsStylesForm.cstrLeftXmlFileSuffixAfterXsltTransform; }
        }
    }
}