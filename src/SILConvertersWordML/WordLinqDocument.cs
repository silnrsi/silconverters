using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SILConvertersWordML
{
    public class WordLinqDocument : LinqDocument
    {
        // need to be public for the UnitTests
        public readonly static XNamespace w = "http://schemas.microsoft.com/office/word/2003/wordml";
        public readonly static XNamespace wx = "http://schemas.microsoft.com/office/word/2003/auxHint";
        public readonly static XNamespace wsp = "http://schemas.microsoft.com/office/word/2003/wordml/sp2";

        public static DocXmlDocument GetXmlDocument(ref string strXmlFilename, string strDocFilename, bool bSaveXmlOutputInFolder)
        {
            // get the XDocument we're going to go through
            var doc = XDocument.Load(strXmlFilename);

            // pre-process the file to fix-up issues that are presumably from earlier versions of Word
            UnpackBareSymbolInserts(doc);

            // pre-process the file and combine the identically formatted runs of text within a paragraph
            CombineIsoFormattedRuns(doc);

            var thisDoc = new WordLinqDocument
                              {
                                  XDocument = doc
                              };

            // get the name of the document to store the output in
            var strXsltOutputFilename = thisDoc.GetOutputFileSpecForXmlFile(strDocFilename, bSaveXmlOutputInFolder);

            // Save the document to a file and auto-indent the output.
            using (var writer = new XmlTextWriter(strXsltOutputFilename, null) { Formatting = Formatting.Indented })
            {
                doc.Save(writer);
            }

            // get the full list of potential font and style names (these aren't what we'll present
            //  to the user, because we'll only show those that have some text, but just to get a
            //  full list that we won't have to a) requery or b) look beyond)
            thisDoc.HarvestFontsAndStylesUsedInAllText();

            return thisDoc;
        }

        public static void UnpackBareSymbolInserts(XDocument doc)
        {
            /*
              <w:p wsp:rsidR="00133C43" wsp:rsidRDefault="00133C43">
                <w:r>
                  <w:t>e.g. plosives </w:t>
                </w:r>
                <w:r>
                  <w:sym w:font="Wingdings" w:char="F0E0" />
                </w:r>
            */
            // This 'Wingdings' entry is weird... there's no rPr formatting element, but there is a w:sym, which
            //  the below function would strip out. So if we find any w:sym with *no* w:t (which that function
            //  expects to exist), then add it
            Debug.Assert(doc.Root != null, "doc.Root != null");
            var runsWithAbberentSyms = doc.Root.Descendants(w + "r").Where(r => r.Elements(w + "sym").Any() && !r.Elements(w + "t").Any());
            foreach (var run in runsWithAbberentSyms)
            {
                var sym = GetElement(run, w + "sym");
                var fontName = GetAttributeValue(sym, w + "font");
                var strCharVal = GetAttributeValue(sym, w + "char");

                // if this element doesn't have a rPr, then add it
                var rPr = GetElement(run, w + "rPr");
                if (rPr == null)
                {
                    // add this:
                    // <w:r>
                    //   <w:rPr>
                    //     <wx:font wx:val="Wingdings" />
                    //   </w:rPr>
                    // ...
                    //   <w:t></w:t>
                    rPr = new XElement(w + "rPr", CreateWxFont(fontName));
                    run.AddFirst(rPr);
                }
                else
                {
                    var wxfont = GetElement(rPr, wx + "font");
                    if (wxfont == null)
                    {
                        wxfont = CreateWxFont(fontName);
                        rPr.Add(wxfont);
                    }

                    /*
                     * D:\temp\BWDC\Dennis\Steve Parker\Steve P Working\Steve P Legacy\section 1.doc
                     * had this:
                      <w:rPr>
                        <w:rFonts w:ascii="B_steve B_steve SILDoulosL" w:h-ansi="B_steve B_steve SILDoulosL" />
                        <wx:font wx:val="B_steve B_steve SILDoulosL" />
                      </w:rPr>
                      <w:sym w:font="Symbol" w:char="F0B1" />
                     * It turns out that Word displays this as B_steve B_steve SILDoulosL... so it appears that the w:font 
                     * of the w:sym can be overridden!
                     * So
                     // Debug.Assert(GetAttributeValue(wxfont, wx + "val") == fontName);
                    */
                }

                var cCharValue = (char) Convert.ToInt32(strCharVal, 16);
                var t = new XElement(w + "t", cCharValue);
                run.Add(t);

                // finally, get rid of the 'sym', since we don't want it now
                sym.Remove();
            }

            // get rid of any non-abberant 'sym'bol inserts too
            foreach (var run in doc.Root.Descendants(w + "r").Where(r => r.Elements(w + "rPr").Any(rPr => rPr.Elements(wx + "sym").Any())))
            {
                Debug.Assert(run.Descendants(w + "t").Any());
                var sym = run.Descendants(wx + "sym").FirstOrDefault();
                Debug.Assert(sym != null, "sym != null");
                sym.Remove();
            }
        }

        private static XElement CreateWxFont(string fontName)
        {
            return new XElement(wx + "font",
                                new XAttribute(wx + "val", fontName));
        }

        /// <summary>
        /// This method will combine the w:t element values of sibling w:r elements whose w:rPr formatting specifications are equivalent
        /// </summary>
        /// <param name="doc">the XDocument to modify</param>
        public static void CombineIsoFormattedRuns(XDocument doc)
        {
            /*  this is roughly what we're trying to combine. We can ignore 'wsp:rsidR' and 'wsp:rsidRPr' 
            *  and any 'wx:char' attributes
            <w:r wsp:rsidR="00B1028E"   wsp:rsidRPr="00972CAF">
                <w:rPr>
                <w:rFonts w:ascii="Kruti Dev 011" w:h-ansi="Kruti Dev 011" />
                <wx:font wx:val="Kruti Dev 011" />
                </w:rPr>
                <w:t>vkcfnueu dj fH</w:t>
            </w:r>
            <w:r wsp:rsidR="00B06F43" wsp:rsidRPr="00972CAF">
                <w:rPr>
                <w:rFonts w:ascii="Kruti Dev 011" w:h-ansi="Kruti Dev 011" />
                <wx:font wx:val="Kruti Dev 011" />
                </w:rPr>
                <w:t>kM+ tksgku ls iqNyk;a]</w:t>
            </w:r>
        */
            // first get all the 'p'aragraphs, so we can find sibling 'r'uns
            Debug.Assert(doc.Root != null, "doc.Root != null");
            var paragraphs = doc.Root.Descendants(w + "p").ToList();
            foreach (var paragraph in paragraphs)
            {
                // now get the sibling runs (via 'Elements'; rather than 'Descendents', since they are direct children)
                // var runs = paragraph.Elements(w + "r").ToList();
                // UPDATE: I'm uncomfortable with joining runs that are broken up by something else... just saw this:
                // <w:r>
                //   <w:t>PH11 Introduction to Phonology</w:t>
                // </w:r>
                // <aml:annotation aml:id="33" w:type="Word.Bookmark.End" />
                // <w:r>
                //   <w:t> (GRAD DIP)</w:t>
                // </w:r>
                // SO, grab Elements; rather than just "w:r"s
                var runs = paragraph.Elements().ToList();
                if (runs.Count <= 1)
                    continue;

                // get the 1st one...
                var thisRun = runs.First();
                for (var i = 1; i < runs.Count; i++)
                {
                    // skip over any elements that aren't bonafide "w:r"s
                    var nextRun = runs[i];
                    if ((thisRun.Name != w + "r") || (nextRun.Name != w + "r"))
                    {
                        thisRun = nextRun;
                        continue;
                    }

                    // sometimes a 'run' doesn't have a w:t field... (those should also interrupt the combining of iso-formatted runs)
                    var tNext = Get_t(nextRun);
                    var tThis = Get_t(thisRun);

                    if ((tThis == null) ||
                        (tNext == null) ||
                        (GetRunIdentityValue(thisRun) != GetRunIdentityValue(nextRun)))
                    {
                        // the 'next' one wasn't identical to 'this' one (or otherwise we need to stop combining)... 
                        // so skip to the next one as being the 'this' one so we can compare *it* with
                        //  what follows next time
                        thisRun = nextRun;
                        continue;
                    }

                    // we found an identically formatted run... concatenate the w:t values and remove the 'next' run
                    tThis.Value += tNext.Value;
                    nextRun.Remove();
                }
            }
        }

        private static string GetRunIdentityValue(XContainer run)
        {
            // var str = Get_rPr_value(run);
            // UPDATE: initially, I was assuming that all the formatting was in the rPr, but that's not true. e.g.
            // <w:r>
            //   <w:t>(a) voice assimilation</w:t>
            // </w:r>
            // <w:r>
            //   <w:br />
            //   <w:t>(b) metathesis</w:t>
            // </w:r>
            // these shouldn't be combined, because the 2nd has a "w:br" element.
            // SO, return all the stuff between w:r to w:t (I've never seen anything *below* a w:t)

            var str = run.Descendants().Where(elem => !elem.HasElements && 
                                                      (elem.Name != w + "t") && 
                                                      !ElementsToStripOut.Contains(elem.Name))
                                       .Aggregate<XElement, string>(null, (current, elem) => current + elem.ToString());
            return str;
        }

        private static XElement Get_t(XElement run)
        {
            // in a few cases, I've seen the text actually be in a w:instrText element (rather than w:t)
            //  NOTE: this method might return null!
            return GetElement(run, w + "t") ?? GetElement(run, w + "instrText");
        }

        private static readonly List<XName> ElementsToStripOut = new List<XName>
                                                                     {
                                                                         wx + "sym" // this is for insert symbols, which don't need (the value is already in the w:t element)
                                                                     };

        protected override void ReplaceFontNameAtParagraphLevel(string strFontNameOld, string strFontNameNew)
        {
            /*
              <w:p ...>
                <w:pPr>
                  <w:rPr>
                    <w:rFonts w:ascii="VG2000 Main" w:h-ansi="VG2000 Main" />               <============= replace these bits
                    <wx:font wx:val="VG2000 Main" />
                  </w:rPr>
                </w:pPr>
                ...
              </w:p>
            */
            var paragraphStyles = DocumentRoot.Descendants(w + "pPr");
            foreach (var elemFormatting in paragraphStyles.Select(GetRunFormattingParent)
                                                          .Where(elemFormatting => elemFormatting != null))
            {
                UpdateAllChildElementAttributesValue(elemFormatting, strFontNameOld, strFontNameNew);
            }
        }

        protected override void GetMostRelevantStyleFormat(XElement run, XElement paragraph, out string strStyleId, out string strStyleName, out string strFontName)
        {
            // looking for:
            // 1) style override on the run level -- i.e.:
            // <w:rPr>
            //   <w:rStyle w:val="Heading2Char" />      <=======
            //   <wx:font wx:val="Calibri" />           sometimes it tells you which font of the style is being used... but I'm not sure that's always true
            //                                                   actually, though, it doesn't matter, because this'll be picked up later by the CheckForCustomFont...
            var styleName = GetDescendantAttributeValue(run, w + "rStyle", w + "val");

            // in checking for style override at the run level, only accept it if the style found
            //  actually has Font formatting (if it doesn't then it's not relevant)
            StyleClass styleClass;
            if (String.IsNullOrEmpty(styleName) || !(styleClass = GetStyleById(styleName)).HasFontFormatting)
            {
                // 2) style override at the paragraph level -- i.e.:
                // <w:pPr>
                //   <w:pStyle w:val="Heading1" />      <=======
                styleName = GetDescendantAttributeValue(paragraph, w + "pStyle", w + "val");

                // 3) If neither of these apply, then it's 'Normal' style
                styleClass = (!String.IsNullOrEmpty(styleName) && (styleClass = GetStyleById(styleName)).HasFontFormatting)
                                ? styleClass 
                                : GetStyleById("Normal");
            }

            strStyleId = styleClass.Id;
            strStyleName = styleClass.Name;
            System.Diagnostics.Debug.Assert(styleClass.HasFontFormatting);   // if this doesn't turn out to be true, then get it from w:font/@defaultFonts...
            strFontName = styleClass.FontNames.First();
        }

        protected override bool CheckForCustomFontFormatting(XElement run, ref string strFontName)
        {
            // looking for:
            // <w:rPr>
            //   <wx:font wx:val="Calibri" />   <======
            var fontName = GetDescendantAttributeValue(run, wx + "font", wx + "val");

            // UPDATE: but there shouldn't be an "w:rStyle", which indicates style-based formatting
            // if (!String.IsNullOrEmpty(fontName))
            // UPDATE2: but... the style formatting might not have a font defined, so... it really would be custom formatting
            // if (!String.IsNullOrEmpty(fontName) && 
            //     !run.Descendants(w + "rStyle").Any())
            // UPDATE3: I'm not sure this is correct either... even if the style has font formatting, 
            //  the presence of a "w:rFonts" will *override* the style formatting. So I think the bottom 
            //  line is that if there's a w:rFonts, *then* it's custom formatting.
            // if (!String.IsNullOrEmpty(fontName))
            // {
            //     // so now see if there's a style format also there with a font embedded
            //     var styleName = GetDescendantAttributeValue(run, w + "rStyle", w + "val");
            //     if (String.IsNullOrEmpty(styleName) || !GetStyleById(styleName).HasFontFormatting)
            // UPDATE4: let's say it's custom formatting if it has a w:rFonts OR if it doesn't have a w:rStyle
            if (!String.IsNullOrEmpty(fontName) && 
                (run.Descendants(w + "rFonts").Any() || !run.Descendants(w + "rStyle").Any()))
            {
                strFontName = fontName;
                return true;
            }

            return false;
        }

        public override string GetTextFromRun(XElement run)
        {
            var tElem = Get_t(run);
            return (tElem != null)
                       ? tElem.Value
                       : null;
        }

        internal override void SetTextOfRun(XElement run, string str)
        {
            var tElem = Get_t(run);
            // if it's null, then there's nothing to do
            if (tElem != null)
                tElem.Value = str;
        }

        protected StyleClass GetStyleById(string strStyleId)
        {
            //  <w:style w:type="paragraph" w:default="on" w:styleId="Normal">
            //    <w:name w:val="Normal" />
            return Styles[strStyleId];
        }

        protected override XElement GetRunFormattingParent(XElement run)
        {
            return GetElement(run, w + "rPr");
        }

        private static readonly XName CxNameElementForStyleFontName = wx + "font";
        private static readonly XName CxNameAttributeForStyleFontName = wx + "val";
        private static readonly XName CxNameElementForStyleFontNames = w + "rFonts";

        protected override List<StyleClass> ListStyles(XElement documentRoot)
        {
            System.Diagnostics.Debug.Assert(DocumentRoot != null);

            // <w:styles
            // ...
            //   <w:style
            var styleList = new List<StyleClass>();
            var stylesRoot = DocumentRoot.Descendants(w + "styles").FirstOrDefault();
            System.Diagnostics.Debug.Assert(stylesRoot != null);

            foreach (var styleElem in stylesRoot.Elements(w + "style"))
            {
                List<string> lstFontNames = null;
                var rPr = GetElement(styleElem, w + "rPr");
                
                // not all styles have font formatting
                if (rPr != null)
                {
                    // first check to see if there's a wx:font... (this is a single value and usually (always?)
                    //  is the fonts that are used
                    var strFontName = GetElementAttributeValue(rPr, CxNameElementForStyleFontName,
                                                               CxNameAttributeForStyleFontName);
                    if (!String.IsNullOrEmpty(strFontName))
                    {
                        lstFontNames = new List<string> { strFontName };
                    }
                    else
                    {
                        // if not, then check to see if there's a w:rFonts
                        // <w:rPr>
                        //   <w:rFonts w:ascii="Tahoma" w:h-ansi="Tahoma" />
                        var rFonts = GetElement(rPr, CxNameElementForStyleFontNames);
                        if (rFonts != null)
                        {
                            lstFontNames = new List<string>();
                            lstFontNames.AddRange(GetAllAttributeValues(rFonts));
                        }
                    }
                }

                var styleClass = new StyleClass
                                        {
                                            Id = GetAttributeValue(styleElem, w + "styleId"),
                                            Name = GetStyleName(styleElem),
                                            FontNames = lstFontNames,
                                            AssociatedStyleElem = styleElem
                                        };
                styleList.Add(styleClass);
            }
            return styleList;
        }

        protected string GetStyleName(XElement styleElem)
        {
            // in some documents, they use wx:uiName for the name to display for the font
            // <w:style w:type="paragraph" w:styleId="Header">
            //   <w:name w:val="header" />
            //   <wx:uiName wx:val="Header" />
            var strStyleName = GetElementAttributeValue(styleElem, wx + "uiName", wx + "val");
            if (String.IsNullOrEmpty(strStyleName))
                strStyleName = GetElementAttributeValue(styleElem, w + "name", w + "val");
            return strStyleName;
        }

        protected override void ReplaceStyleFontName(StyleClass styleClass, string strFontNameSource, string strFontNameTarget)
        {
            // <w:style w:type="paragraph" w:default="on" w:styleId="Normal">
            //   <w:name w:val="Normal" />
            //   <w:rsid w:val="0015478C" />
            //   <w:pPr>
            //     <w:spacing w:after="200" w:line="276" w:line-rule="auto" />
            //   </w:pPr>
            //   <w:rPr>
            //     <wx:font wx:val="Calibri" />
            var rPr = GetRunFormattingParent(styleClass.AssociatedStyleElem);
            UpdateAllChildElementAttributesValue(rPr, strFontNameSource, strFontNameTarget);
        }

        protected override IEnumerable<XElement> Paragraphs
        {
            get
            {
                return DocumentRoot.Descendants(w + "p");
                // turns out we don't care whether there's text or not
                //  for one thing, it might be w:instrText... for another
                //  , we still want to convert any font naming in the 'empty' run
                //  .Where(p => p.Descendants(w + "t").Any());
            }
        }

        protected override XContainer FontsListRoot
        {
            get
            {
                // <w:fonts>
                //   <w:defaultFonts w:ascii="Calibri" w:fareast="Calibri" w:h-ansi="Calibri" w:cs="Arial" />
                //   <w:font w:name="Arial">
                return DocumentRoot.Descendants(w + "fonts").FirstOrDefault();
            }
        }

        protected override IEnumerable<XElement> Runs(XElement paragraph)
        {
            return paragraph.Elements(w + "r");
                // turns out we don't care whether there's text or not
                //  for one thing, it might be w:instrText... for another
                //  , we still want to convert any font naming in the 'empty' run
                // .Where(r => r.Elements(w + "t").Any());
        }
    }
}

/*
// a different implementation based on Linq rather than XPath
public class LinqDocumentOrig : DocXmlDocument
{
    private XDocument XDocument { get; set; }

    private XElement DocumentRoot
    {
        get
        {
            if ((XDocument == null) || (XDocument.Root == null))
                throw new ApplicationException(
                    "Document wasn't initialized properly. If this error continues, then send an email to silconverters_support@sil.org with the steps to reproduce this error (including the document)");
            return XDocument.Root;
        }
    }

    public readonly static XNamespace w = "http://schemas.microsoft.com/office/word/2003/wordml";
    protected readonly static XNamespace wx = "http://schemas.microsoft.com/office/word/2003/auxHint";
    protected readonly static XNamespace wsp = "http://schemas.microsoft.com/office/word/2003/wordml/sp2";

    #region Overrides of DocXmlDocument

    protected override string GetXmlFileSuffix
    {
        get { return FontsStylesForm.cstrLeftXmlFileSuffixAfterLinqTransform; }
    }

    public override bool ConvertDocumentByFontNameAndStyle(Dictionary<string, Font> mapName2Font, Func<string, DataIterator, string, Font, bool, bool> convertDoc)
    {
        throw new NotImplementedException();
    }

    public override bool ConvertDocumentByStylesOnly(Dictionary<string, Font> mapName2Font, Func<string, DataIterator, string, Font, bool, bool> convertDoc)
    {
        throw new NotImplementedException();
    }

    public override bool ConvertDocumentByFontNameOnly(Dictionary<string, Font> mapName2Font, Func<string, DataIterator, string, Font, bool, bool> convertDoc)
    {
        throw new NotImplementedException();
    }

    public override bool HasFonts(List<string> astrFontsToSearchFor)
    {
        throw new NotImplementedException();
    }

    public override void GetTextIteratorListStyle(ref IteratorMap mapStyleId2Iterator)
    {
        throw new NotImplementedException();
    }

    public override void GetTextIteratorListFontStyle(ref IteratorMap mapDefStyleFontNames2Iterator, ref IteratorMap mapPStyleFontNames2Iterator, ref IteratorMap mapCStyleFontNames2Iterator)
    {
        throw new NotImplementedException();
    }

    public override void GetTextIteratorListFontCustom(ref IteratorMap mapFontNames2Iterator, ref IteratorMap mapSymbolFontNames2Iterator)
    {
        throw new NotImplementedException();
    }

    #endregion

    /// <summary>
    /// get the full list of potential font and style names when we first open the XML documents so we don't
    ///  need to do this again (e.g. when the user selects a different radio button)
    /// </summary>
    /// <param name="strFilename"></param>
    protected void GetFullNameLists(string strFilename)
    {
        // get all the fonts associated with custom formatting (into lstFontNamesCustom)
        GetCustomFontLists(ref LstFontNamesCustom);

        // get all inserted symbol items into lstFontNamesSymbolText
        GetInsertedSymbolFontNames(ref LstFontNamesSymbolText);

        // get all the fonts associated with style-based formatting
        GetStyleBasedFormattingFontNamesNormalStyle(ref LstDefaultStyleFontName);
        GetStyleBasedFormattingFontNamesParagraphStyle(ref LstFontNamesPStyle);
        GetStyleBasedFormattingFontNamesCharacterStyle(ref LstFontNamesCStyle);

        // get all the style names as well
        // was:
        // public const string cstrXPathWordMLGetPStyle = "/w:wordDocument/w:styles/w:style[@w:type = 'paragraph'][w:rPr/wx:font/@wx:val]";
        GetStyleNameIdLists(strFilename, "paragraph", wx + "font", wx + "val", ref LstPStyleIdList);

        // and character style:
        // public const string cstrXPathWordMLGetCStyle = "/w:wordDocument/w:styles/w:style[@w:type = 'character'][w:rPr/w:rFonts/@w:ascii]";
        GetStyleNameIdLists(strFilename, "character", w + "rFonts", w + "ascii", ref LstCStyleIdList);
    }

    private void GetStyleNameIdLists(string strFilename, string typeStyle, XName xNameLeafElement, XName xNameLeafAttr,
                                        ref List<string> lstStyleId)
    {
        // was:
        // public const string cstrXPathWordMLGetPStyle = "/w:wordDocument/w:styles/w:style[@w:type = 'paragraph'][w:rPr/wx:font/@wx:val]";
        XAttribute attr;
        var styles = DocumentRoot.Descendants(w + "style")
                                    .Where(s => ((attr = s.Attribute(w + "type")) != null) &&
                                                (attr.Value == typeStyle) &&
                                                s.Descendants(xNameLeafElement).Any());

        foreach (var style in styles)
        {
            var strName = GetElementAttributeValue(style, w + "name", w + "val");
            var strId = GetAttributeValue(style, w + "styleId");
            var strFont = GetDescendantAttributeValue(style, xNameLeafElement, xNameLeafAttr);

            InitializeMapsFromStyleInfo(strFont, strId, strName, lstStyleId, strFilename);
        }
    }


    private void GetCustomFontLists(ref List<string> lst)
    {
        /* was
    public const string cstrXPathWordMLDefFontAscii = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/w:rFonts/@w:ascii";
    public const string cstrXPathWordMLDefFontFareast = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/w:rFonts/@w:fareast";
    public const string cstrXPathWordMLDefFontHAnsi = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/w:rFonts/@w:h-ansi";
    public const string cstrXPathWordMLDefFontCS = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/w:rFonts/@w:cs";
	public const string cstrXPathWordMLGetFontNames = "/w:wordDocument/w:body//w:p/w:r[w:t]/w:rPr/wx:font/@wx:val";
        // find the w:rFonts element and grab whatever values there are in the @w:ascii, @w:fareast, @w:h-ansi, @w:cs attributes
        //  and the wx:font element and grab whatever value is in the wx:val attribute
        var rPrs = DocumentRoot.Descendants(w + "r")                               // search for runs (w:r)
                                .Where(r => r.Elements(w + "t").Any() &&            // which have a 'w:t' child
                                            (r.Descendants(w + "rFonts").Any() ||   // and custom formatting (i.e. either a w:rFonts
                                            r.Descendants(wx + "font").Any()))     //  or a wx:font child element)
                                .Select(r => r.Element(w + "rPr"));                 // end up with the rPr elements

        foreach (var rPr in rPrs)
        {
            GetAttributeValues(rPr, w + "rFonts", lst);
            GetAttributeValues(rPr, wx + "font", lst);
        }
    }

    private void GetStyleBasedFormattingFontNamesNormalStyle(ref List<string> lstDefaultStyleFontName)
    {
        // was:
        //  public const string cstrXPathWordMLGetDefaultPStyleFontName = "/w:wordDocument/w:styles/w:style[@w:styleId = 'Normal']/w:rPr/w:rFonts/@w:ascii";
        XAttribute attr;
        var rPrNormalStyle = DocumentRoot.Descendants(w + "style")
                                            .Where(s => ((attr = s.Attribute(w + "styleId")) != null) &&
                                                        (attr.Value == "Normal"))
                                            .Select(s => s.Element(w + "rPr"))
                                            .FirstOrDefault();

        if (rPrNormalStyle != null)
            GetAttributeValues(rPrNormalStyle, w + "rFonts", lstDefaultStyleFontName);
    }

    private void GetStyleBasedFormattingFontNamesParagraphStyle(ref List<string> lst)
    {
        // was:
        //  public const string cstrXPathWordMLGetPStyleFontNames = "/w:wordDocument/w:styles/w:style[@w:type = 'paragraph']/w:rPr/wx:font/@wx:val";
        XAttribute attr;
        var stylesFont = DocumentRoot.Descendants(w + "style")
                                        .Where(s => ((attr = s.Attribute(w + "type")) != null) &&
                                                    (attr.Value == "paragraph"))
                                        .Select(s => s.Descendants(wx + "font"))
                                        .FirstOrDefault();

        if (stylesFont != null)
            foreach (var value in stylesFont.Select(styleFont => GetAttributeValue(styleFont, wx + "val")))
            {
                if (!lst.Contains(value))
                    lst.Add(value);
            }
    }

    private void GetStyleBasedFormattingFontNamesCharacterStyle(ref List<string> lst)
    {
        // was:
        //  public const string cstrXPathWordMLGetCStyleFontNames = "/w:wordDocument/w:styles/w:style[@w:type = 'character']/w:rPr/w:rFonts/@w:ascii, etc";
        XAttribute attr;
        var stylesrPrs = DocumentRoot.Descendants(w + "style")
                                        .Where(s => ((attr = s.Attribute(w + "type")) != null) &&
                                                    (attr.Value == "character"))
                                        .Select(s => s.Elements(w + "rPr"))
                                        .FirstOrDefault();

        if (stylesrPrs != null)
            foreach (var stylesrPr in stylesrPrs)
                GetAttributeValues(stylesrPr, w + "rFonts", lst);
    }

    private void GetInsertedSymbolFontNames(ref List<string> lst)
    {
        // was:
        //  public const string cstrXPathWordMLGetSymbolTextFontNames = "/w:wordDocument/w:body//w:p/w:r/w:sym/@w:font";
        var sym = DocumentRoot.Descendants(w + "r")                    // search for runs (w:r)
                                .Where(r => r.Elements(w + "t").Any() && // which have a 'w:t' child
                                            r.Elements(w + "sym").Any()) //  and a w:sym descendent (i.e. inserted symbol)
                                .Select(r => r.Element(w + "sym"))       // end up with the sym elements
                                .FirstOrDefault();                       // there should only be one, but there may be none!

        if (sym == null)
            return;

        var fontName = sym.Attributes().Where(a => a.Name == w + "font").Select(a => a.Value).First();
        if (!lst.Contains(fontName))
            lst.Add(fontName);
    }

    private static void GetAttributeValues(XElement rPr, XName xNameChild, List<string> lst)
    {
        var rFonts = rPr.Elements(xNameChild).FirstOrDefault();
        if (rFonts != null)
            foreach (var attr in rFonts.Attributes().Select(attr => attr.Value).Distinct().Where(attr => !lst.Contains(attr)))
                lst.Add(attr);
    }
}
*/
