using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace SILConvertersWordML
{
    // a different implementation based on Linq rather than XPath
    public abstract class LinqDocument : DocXmlDocument
    {
        protected LinqDocument()
            : base(new MapIteratorListLinq())
        {

        }

        protected XDocument XDocument { get; set; }

        protected XElement DocumentRoot
        {
            get
            {
                if ((XDocument == null) || (XDocument.Root == null))
                    throw new ApplicationException(
                        "Document wasn't initialized properly. If this error continues, then send an email to silconverters_support@sil.org with the steps to reproduce this error (including the document)");
                return XDocument.Root;
            }
        }

        protected Dictionary<string, StyleClass> _styles;

        protected Dictionary<string, StyleClass> Styles
        {
            get { return _styles ?? (_styles = ListStyles(DocumentRoot).ToDictionary(s => s.Id, s => s)); }
        }

        // map for fontName => List of XElements of the runs associated with that font THAT HAVE CUSTOM FORMATTING
        protected Dictionary<string, List<XElement>> MapCustomFontNameToListOfRuns =
            new Dictionary<string, List<XElement>>();

        // map for fontName => List of XElements of the runs associated with that font THAT ARE BASED ON NON-CUSTOM FORMATTING 
        //  (i.e. the font name associated with the associated style formatting)
        protected Dictionary<string, List<XElement>> MapStyleFontNameToListOfRuns =
            new Dictionary<string, List<XElement>>();

        // map for the styleId => a) styleName and b) List of XElements of the runs associated with that styleId
        protected Dictionary<string, Tuple<string, List<XElement>>> MapStyleIdToListOfRuns =
            new Dictionary<string, Tuple<string, List<XElement>>>();

        #region Overrides of DocXmlDocument

        protected override string GetXmlFileSuffix
        {
            get { return FontsStylesForm.cstrLeftXmlFileSuffixAfterLinqTransform; }
        }

        public override bool ConvertDocumentByFontNameAndStyle(Dictionary<string, Font> mapName2Font,
                                                               Func<string, DataIterator, string, Font, bool, bool>
                                                                   convertDoc)
        {
            throw new NotImplementedException();
        }

        public override bool ConvertDocumentByStylesOnly(Dictionary<string, Font> mapName2Font,
                                                         Func<string, DataIterator, string, Font, bool, bool> convertDoc)
        {
            throw new NotImplementedException();
        }

        public override bool ConvertDocumentByFontNameOnly(Dictionary<string, Font> mapName2Font,
                                                           Func<string, DataIterator, string, Font, bool, bool>
                                                               convertDoc)
        {
            throw new NotImplementedException();
        }

        public override bool HasFonts(List<string> astrFontsToSearchFor)
        {
            throw new NotImplementedException();
        }

        #endregion

        protected virtual void HarvestFontsAndStylesUsedInAllText()
        {
            // go through all the runs in the document that a) have text and b) find out what font as associated with it
            // to start with, get all the 'paragraphs' that have some text associated with it
            foreach (var paragraph in ParagraphsWithText)
            {
                HarvestFontAndOrStyleFromParagraph(paragraph);
            }
        }

        protected virtual void HarvestFontAndOrStyleFromParagraph(XElement paragraph)
        {
            foreach (var run in RunsWithText(paragraph))
            {
                HarvestFontAndOrStyleFromRun(run, paragraph);
            }
        }

        protected virtual void HarvestFontAndOrStyleFromRun(XElement run, XElement paragraph)
        {
            // there are four types of ways in which a font is associated with some text:
            //  1) Custom formatting on the run (i.e. w:rFonts and/or wx:font within the run's w:rPr) (I think wx:font/@wx:val actually indicates the font being used an rFonts gives the possibilities for which range)
            //  2) Style formatting override on the run (i.e. w:rStyle within the run's w:rPr)
            //  3) Default Paragraph Style formatting override
            //  4) Default document formatting

            // there's *always* a style associated with a run. If it's not in the 'run', 
            //  look in the paragraph. If it's not in the paragraph, then it's 'Normal'
            //  also, get the font associated with that style, which 
            string strFontName, strStyleId, strStyleName;
            GetMostRelevantStyleFormat(run, paragraph, out strStyleId, out strStyleName, out strFontName);
            AddRunToStyleNameList(strStyleId, strStyleName, run);

            // but if there's custom formatting, that overrules everything
            AddRunToFontNameList(strFontName, run,
                                 (CheckForCustomFontFormatting(run, ref strFontName))
                                     ? MapCustomFontNameToListOfRuns
                                     : MapStyleFontNameToListOfRuns);
        }

        private static void AddRunToFontNameList(string strFontName, XElement run,
                                                 Dictionary<string, List<XElement>> mapToListOfRuns)
        {
            List<XElement> listOfRunsForFontName;
            if (!mapToListOfRuns.TryGetValue(strFontName, out listOfRunsForFontName))
            {
                listOfRunsForFontName = new List<XElement>();
                mapToListOfRuns.Add(strFontName, listOfRunsForFontName);
            }
            listOfRunsForFontName.Add(run);
        }

        private void AddRunToStyleNameList(string strStyleId, string strStyleName, XElement run)
        {
            Tuple<string, List<XElement>> tupleForStyleNameAndListOfRunsForStyleId;
            if (!MapStyleIdToListOfRuns.TryGetValue(strStyleId, out tupleForStyleNameAndListOfRunsForStyleId))
            {
                tupleForStyleNameAndListOfRunsForStyleId = Tuple.Create(strStyleName, new List<XElement>());
                MapStyleIdToListOfRuns.Add(strStyleId, tupleForStyleNameAndListOfRunsForStyleId);
            }
            tupleForStyleNameAndListOfRunsForStyleId.Item2.Add(run);
        }

        protected static string GetElementAttributeValue(XElement elemParent, XName xNameElement, XName xNameAttribute)
        {
            var elem = elemParent.Element(xNameElement);
            return GetAttributeValue(elem, xNameAttribute);
        }

        protected static string GetDescendantAttributeValue(XElement elemParent, XName xNameElement,
                                                            XName xNameAttribute)
        {
            var elem = elemParent.Descendants(xNameElement).FirstOrDefault();
            return GetAttributeValue(elem, xNameAttribute);
        }

        protected static string GetAttributeValue(XElement elem, XName xNameAttribute)
        {
            if (elem != null)
            {
                var attr = elem.Attribute(xNameAttribute);
                if (attr != null)
                    return attr.Value;
            }
            return null;
        }

        protected static XElement GetElement(XElement elem, XName xName)
        {
            return elem.Element(xName);
        }

        protected MapIteratorListLinq MyMapIteratorList
        {
            get { return MapIteratorList as MapIteratorListLinq; }
        }

        public override void InitializeIteratorsCustomFontName(List<string> lstInGrid,
                                                               Action<string, DataIterator> displayInGrid)
        {
            // initialize the MapFontNames2Iterator
            foreach (var kvp in MapCustomFontNameToListOfRuns)
            {
                var iterator = new LinqDataIterator
                                   {
                                       LinqDocument = this,
                                       ListOfRuns = kvp.Value
                                   };
                MyMapIteratorList.MapCustomFontName2Iterator.Add(kvp.Key, iterator);
            }

            // put a clone in the grid
            foreach (var kvp in MyMapIteratorList.MapCustomFontName2Iterator
                .Where(kvp => !lstInGrid.Contains(kvp.Key)))
            {
                displayInGrid(kvp.Key, kvp.Value);
                lstInGrid.Add(kvp.Key);
            }
        }

        public override void InitializeIteratorsFontsFromStyles(List<string> lstInGrid,
                                                                Action<string, DataIterator> displayInGrid)
        {
            // initialize the MapFontNames2Iterator
            foreach (var kvp in MapStyleFontNameToListOfRuns)
            {
                var iterator = new LinqDataIterator
                                   {
                                       LinqDocument = this,
                                       ListOfRuns = kvp.Value
                                   };
                MyMapIteratorList.MapStyleFontName2Iterator.Add(kvp.Key, iterator);
            }

            // put a clone in the grid
            foreach (var kvp in MyMapIteratorList.MapStyleFontName2Iterator
                .Where(kvp => !lstInGrid.Contains(kvp.Key)))
            {
                displayInGrid(kvp.Key, kvp.Value);
                lstInGrid.Add(kvp.Key);
            }
        }

        public override void InitializeIteratorsStyleName(List<string> lstInGrid,
                                                          Action<string, DataIterator> displayInGrid)
        {
            // initialize the MapFontNames2Iterator
            foreach (var kvp in MapStyleIdToListOfRuns)
            {
                var iterator = new LinqDataIterator
                                   {
                                       LinqDocument = this,
                                       ListOfRuns = kvp.Value.Item2
                                   };
                MyMapIteratorList.MapStyleFontName2Iterator.Add(kvp.Value.Item1, iterator);
            }

            // put them in the grid
            foreach (var kvp in MyMapIteratorList.MapStyleFontName2Iterator
                .Where(kvp => !lstInGrid.Contains(kvp.Key)))
            {
                displayInGrid(kvp.Key, kvp.Value);
                lstInGrid.Add(kvp.Key);
            }
        }

        protected abstract List<StyleClass> ListStyles(XElement documentRoot);
        protected abstract IEnumerable<XElement> ParagraphsWithText { get; }
        protected abstract IEnumerable<XElement> RunsWithText(XElement paragraph);

        protected abstract void GetMostRelevantStyleFormat(XElement run, XElement paragraph, out string strStyleId,
                                                           out string strStyleName, out string strFontName);

        protected abstract bool CheckForCustomFontFormatting(XElement run, ref string strFontName);
        public abstract string GetTextFromRun(XElement run);
        internal abstract void SetTextOfRun(XElement xElement, string str);
    }

    public class MapIteratorListLinq : MapIteratorList
    {
        public MapIteratorListLinq()
        {
            ResetMaps();
        }

        public IteratorMap MapCustomFontName2Iterator;
                           // for "Custom Font only" and with the next for "Style and Custom Formatting"

        public IteratorMap MapStyleFontName2Iterator; // for "Style and Custom Formatting" (with the previous)
        public IteratorMap MapStyleId2Iterator; // for "Style-only" formatting

        #region Overrides of MapIteratorList

        public override bool IsInitializedCustomFontName
        {
            get { return (MapCustomFontName2Iterator.Count > 0); }
        }

        public override bool IsInitializedFontsFromStyles
        {
            get { return (MapStyleFontName2Iterator.Count > 0); }
        }

        public override bool IsInitializedStyleName
        {
            get { return (MapStyleId2Iterator.Count > 0); }
        }

        public override sealed void ResetMaps()
        {
            MapCustomFontName2Iterator = new IteratorMap();
            MapStyleFontName2Iterator = new IteratorMap();
            MapStyleId2Iterator = new IteratorMap();
        }

        #endregion
    }
}