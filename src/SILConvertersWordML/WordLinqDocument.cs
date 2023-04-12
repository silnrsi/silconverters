using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public static DocXmlDocument GetXmlDocument(ref string strXmlFilename, string strDocFilename, bool bSaveXmlOutputInFolder, bool combineRunsIntoIsoformattedParagraph)
        {
            // get the XDocument we're going to go through
            var doc = XDocument.Load(strXmlFilename);

            // pre-process the file to fix-up issues that are presumably from earlier versions of Word
            UnpackBareSymbolInserts(doc);

            // pre-process the file and combine the identically formatted runs of text within a paragraph
            CombineIsoFormattedRuns(doc);

            // pre-process the file and combine all the subsequent runs in a paragraph into the first run of the paragraph
            //  this is primarily for Translator type converters like Bing or DeepL, which work best with full sentences,
            //  but it also will clobber any intra paragraph formatting
            if (combineRunsIntoIsoformattedParagraph)
                CombineAllRunsIntoSingleRun(doc);

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
                Debug.Assert(Get_t(run) != null);
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
        /// This method will combine the w:t element values of sibling w:r elements into the first w:t element of the paragraph
        /// clobbering all intra-paragraph formatting, but causing the entire paragraph to be converted as a unit (best for 
        /// translator type converters like Bing or DeepL)
        /// </summary>
        /// <param name="doc">the XDocument to modify</param>
        public static void CombineAllRunsIntoSingleRun(XDocument doc)
        {
            /*  this is roughly what we're trying to combine. 
          <w:p wsp:rsidR="00123714" wsp:rsidRPr="00F44BD1" wsp:rsidRDefault="00123714" wsp:rsidP="00AC4DAD">
            <w:pPr>
              <w:pStyle w:val="ListParagraph" />
              <w:widowControl w:val="off" />
              <w:listPr>
                <w:ilvl w:val="0" />
                <w:ilfo w:val="3" />
                <wx:t wx:val="1." />
                <wx:font wx:val="Times New Roman" />
              </w:listPr>
              <w:pBdr>
                <w:top w:val="nil" />
                <w:left w:val="nil" />
                <w:bottom w:val="nil" />
                <w:right w:val="nil" />
                <w:between w:val="nil" />
              </w:pBdr>
              <w:rPr>
                <w:rFonts w:fareast="Times New Roman" w:cs="Times New Roman" />
                <w:color w:val="2F5496" />
                <w:lang w:fareast="ZH-CN" />
              </w:rPr>
            </w:pPr>
            <w:r wsp:rsidRPr="00F44BD1">
              <w:rPr>
                <w:rFonts w:ascii="Gungsuh" w:fareast="Gungsuh" w:h-ansi="Gungsuh" w:cs="Gungsuh" />
                <wx:font wx:val="Gungsuh" />
                <w:color w:val="2F5496" />
                <w:lang w:fareast="ZH-CN" />
              </w:rPr>
              <w:t>仔</w:t>
            </w:r>
            <w:r wsp:rsidRPr="00F44BD1">
              <w:rPr>
                <w:rFonts w:ascii="SimSun" w:h-ansi="SimSun" w:cs="SimSun" w:hint="fareast" />
                <wx:font wx:val="SimSun" />
                <w:color w:val="2F5496" />
                <w:lang w:fareast="ZH-CN" />
              </w:rPr>
              <w:t>细阅读</w:t>
            </w:r>
            <w:r wsp:rsidRPr="00F44BD1">
              <w:rPr>
                <w:rFonts w:ascii="Gungsuh" w:fareast="Gungsuh" w:h-ansi="Gungsuh" w:cs="Gungsuh" w:hint="fareast" />
                <wx:font wx:val="Gungsuh" />
                <w:color w:val="2F5496" />
                <w:lang w:fareast="ZH-CN" />
              </w:rPr>
              <w:t>本</w:t>
            </w:r>

            into a single run with all the text concatenated:

            <w:r wsp:rsidRPr="00F44BD1">
              <w:rPr>
                <w:rFonts w:ascii="Gungsuh" w:fareast="Gungsuh" w:h-ansi="Gungsuh" w:cs="Gungsuh" />
                <wx:font wx:val="Gungsuh" />
                <w:color w:val="2F5496" />
                <w:lang w:fareast="ZH-CN" />
              </w:rPr>
              <w:t>仔细阅读本</w:t>
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

                // get the 1st one that is a w:r and has text (if there's anything before this one, then those
                //  will remain as they are in the document...)
                var firstRun = runs.FirstOrDefault(e => (e.Name == w + "r") && (Get_t(e) != null));
                if (firstRun == null)
                    continue;

                bool firstNonPunctuationFound = false;
                XElement textOfNextRun, textOfFirstRun = Get_t(firstRun);
                for (var i = runs.IndexOf(firstRun) + 1; i < runs.Count; i++)
                {
                    // combine the text of any subsequent "w:r"s that have text into the text field of the 1st one
                    var nextRun = runs[i];
                    if ((nextRun.Name == w + "r") && ((textOfNextRun = Get_t(nextRun)) != null))
                    {
                        // Often, punctuation comes across as the wrong font (e.g. if you have a double
                        //  quote in Nastaliq, it'll be Times New Roman in Word. For punctuation-initial
                        //  runs, we don't want them to set the font/style of the combined run...
                        // if we haven't already found the first non-punctuation run of text,
                        //  check the current accumulated text and if it's all punctuation, then
                        //  if the next run is non-punctuation, then swap them, so that becomes
                        //  the first run
                        if (!firstNonPunctuationFound)
                        {
                            // if the current first run is all punctuation...
                            if (textOfFirstRun.Value.All(ch => !IsIndicitiveForFont(ch)))
                            {
                                // if the next run has some non-punctuation...
                                if (!textOfNextRun.Value.All(ch => !IsIndicitiveForFont(ch)))
                                {
                                    // then let's use that next run as a new first run and prepend
                                    //  its data with the earlier punctuation.
                                    var previousPunctuation = textOfFirstRun.Value;
                                    firstRun.Remove();
                                    firstRun = nextRun;
                                    textOfFirstRun = Get_t(firstRun);
                                    textOfFirstRun.Value = previousPunctuation + textOfFirstRun.Value;
                                    firstNonPunctuationFound = true;
                                    continue;
                                }
                            }
                            else
                                firstNonPunctuationFound = true;
                        }

                        textOfFirstRun.Value += textOfNextRun.Value;
                    }

                    // anything else (and any after the 1st one) are removed
                    nextRun.Remove();
                }
            }
        }
        
        /// <summary>
        /// Returns whether the given character is a good value to decide whether this run qualifies as the run of the paragraph
        /// </summary>
        /// <param name="ch"></param>
        /// <returns></returns>
        private static bool IsIndicitiveForFont(char ch)
        {
            return !(Char.IsPunctuation(ch) || Char.IsWhiteSpace(ch) || Char.IsDigit(ch));
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
                        !AreRunsIsoFormatted(thisRun, nextRun))
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

        private static bool AreRunsIsoFormatted(XContainer thisRun, XContainer nextRun)
        {
            // UPDATE: there's also stuff that even if two consecutive runs have it don't want to be considered
            //  identical. e.g.
            //         <w:r>
            //           <w:tab />
            //           <w:t>1.1  Introduction</w:t>
            //         </w:r>
            //         <w:r>
            //           <w:tab />
            //           <w:t>7</w:t>
            //         </w:r>
            // because the 'tab' means insert a tab, and if we combined them, we'd be losing one of them
            // return (GetRunIdentityValue(thisRun) != GetRunIdentityValue(nextRun));
            return (GetRunIdentityValue(thisRun) == GetRunIdentityValue(nextRun)) &&
                   ElementsThatBlockIsoFormatting.All(xn => !thisRun.Elements(xn).Any());
        }

        private static readonly List<XName> ElementsThatBlockIsoFormatting = new List<XName>
                                                                     {
                                                                         w + "tab" // if this element is present, then we don't want to combine two runs
                                                                     };

        private static readonly List<XName> ElementsToStripOut = new List<XName>
                                                                     {
                                                                         wx + "sym" // this is for insert symbols, which don't need (the value is already in the w:t element)
                                                                     };

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
            return GetElement(run, w + "t") ??
                   GetElement(run, w + "instrText") ??
                   GetElement(run, w + "delText");
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

        public override bool HasFonts(List<string> astrFontsToSearchFor)
        {
            return FontsListRoot.Elements(w + "font")
                                .Any(e => astrFontsToSearchFor.Contains(GetAttributeValue(e, w + "name")));
        }

        protected override void ReplaceFontElementNames(string strFontNameOld, string strFontNameNew)
        {
            // Update, for the D:\temp\BWDC\Dennis\Steve Parker\Steve P Working\Steve P Legacy files,
            //  I think we don't want to just change the name, but get rid of the element altogether... 
            //  (or it could display improperly if the original was a symbol font)
            // So now, just delete it... Word should be able to recreate it if it's actually needed
            //  <w:font w:name="Mangal">
            foreach (var fontElement in FontsListRoot.Elements(w + "font").Where(e => GetAttributeValue(e, w + "name") == strFontNameOld))
                fontElement.Remove();

            // <w:defaultFonts w:ascii="Times New Roman" w:fareast="Times New Roman" w:h-ansi="Times New Roman" w:cs="Times New Roman" />
            // and if that font is used as one of the ranges of the 'defaultFonts' element, then change that too
            UpdateAllChildElementAttributesValue(FontsListRoot, w + "defaultFonts", strFontNameOld, strFontNameNew);

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
