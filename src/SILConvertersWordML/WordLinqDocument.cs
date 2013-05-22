using System;
using System.Collections.Generic;
using System.Linq;
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
            var paragraphs = doc.Root.Descendants(w + "p").ToList();
            foreach (var paragraph in paragraphs)
            {
                // now get the sibling runs (via 'Elements'; rather than 'Descendents', since they are direct children)
                var runs = paragraph.Elements(w + "r").ToList();
                if (runs.Count <= 1)
                    continue;

                // get the 1st one...
                var thisRun = runs.First();
                var this_rPr_Value = Get_rPr_value(thisRun);    // the xml string representation of the rPr element
                for (var i = 1; i < runs.Count; i++)
                {
                    // get the next one to compare with
                    var nextRun = runs[i];
                    var next_rPr_Value = Get_rPr_value(nextRun);

                    // sometimes a 'run' doesn't have a w:t field... (those should also interrupt the combining of iso-formatted runs)
                    var tNext = Get_t(nextRun);
                    var tThis = Get_t(thisRun);
                    if ((tThis == null) || (tNext == null) || (this_rPr_Value != next_rPr_Value))
                    {
                        // the 'next' one wasn't identical to 'this' one (or otherwise we need to stop combining)... 
                        // so skip to the next one as being the 'this' one so we can compare *it* with
                        //  what follows next time
                        thisRun = nextRun;
                        this_rPr_Value = Get_rPr_value(thisRun);
                        continue;
                    }

                    // we found an identically formatted run... concatenate the w:t values and remove the 'next' run
                    tThis.Value += tNext.Value;
                    nextRun.Remove();
                }
            }
        }

        private static XElement Get_t(XElement run)
        {
            return GetElement(run, w + "t");
        }

        private static readonly List<XName> AttributesToIgnore = new List<XName>
                                                                     {
                                                                         wx + "sym"    // this is for insert symbols, which don't need (the value is already in the w:t element)
                                                                     };

        private static string Get_rPr_value(XElement run)
        {
            var rPr = Get_rPr(run);
            return (rPr != null)
                       ? rPr.ToString()
                       : null;
        }

        private static XElement Get_rPr(XElement run)
        {
            var rPr = GetElement(run, w + "rPr");
            if (rPr != null)
                AttributesToIgnore.ForEach(xN =>
                                               {
                                                   var lst = rPr.Descendants(xN).ToList();
                                                   lst.ForEach(e => e.Remove());
                                               });
            return rPr;
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

            if (!String.IsNullOrEmpty(styleName))
                strStyleId = styleName;
            else
            {
                // 2) style override at the paragraph level -- i.e.:
                // <w:pPr>
                //   <w:pStyle w:val="Heading1" />      <=======
                styleName = GetDescendantAttributeValue(paragraph, w + "pStyle", w + "val");

                // 3) If neither of these apply, then it's 'Normal' style
                strStyleId = !String.IsNullOrEmpty(styleName) ? styleName : "Normal";
            }

            System.Diagnostics.Debug.Assert(!String.IsNullOrEmpty(strStyleId));
            var style = GetStyleById(strStyleId);
            strStyleName = style.Name;
            strFontName = style.FontNames.First();
        }

        protected override bool CheckForCustomFontFormatting(XElement run, ref string strFontName)
        {
            // looking for:
            // <w:rPr>
            //   <wx:font wx:val="Calibri" />   <======
            var fontName = GetDescendantAttributeValue(run, wx + "font", wx + "val");
            
            if (!String.IsNullOrEmpty(fontName))
            {
                strFontName = fontName;
                return true;
            }
            return false;
        }

        public override string GetTextFromRun(XElement run)
        {
            return Get_t(run).Value;
        }

        internal override void SetTextOfRun(XElement run, string str)
        {
            Get_t(run).Value = str;
        }

        protected StyleClass GetStyleById(string strStyleId)
        {
            //  <w:style w:type="paragraph" w:default="on" w:styleId="Normal">
            //    <w:name w:val="Normal" />
            return Styles[strStyleId];
        }

        protected override List<StyleClass> ListStyles(XElement documentRoot)
        {
            System.Diagnostics.Debug.Assert(DocumentRoot != null);

            // <w:styles
            // ...
            //   <w:style
            var styleList = new List<StyleClass>();
            var stylesRoot = DocumentRoot.Descendants(w + "styles").FirstOrDefault();
            if (stylesRoot != null)
                styleList.AddRange(from styleElem in stylesRoot.Elements(w + "style")
                                   let rPr = GetElement(styleElem, w + "rPr")
                                   where rPr != null
                                   let strFontName = GetElementAttributeValue(rPr, wx + "font", wx + "val")
                                   select new StyleClass
                                              {
                                                  Id = GetAttributeValue(styleElem, w + "styleId"), Name = GetElementAttributeValue(styleElem, w + "name", w + "val"), FontNames = new List<string> {strFontName}
                                              });
            return styleList;
        }

        protected override IEnumerable<XElement> ParagraphsWithText
        {
            get
            {
                return DocumentRoot.Descendants(w + "p")
                    .Where(p => p.Descendants(w + "t").Any());
            }
        }

        protected override IEnumerable<XElement> RunsWithText(XElement paragraph)
        {
            return paragraph.Elements(w + "r")
                .Where(r => r.Elements(w + "t").Any());
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
