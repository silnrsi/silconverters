#define DefineWord07MLDocument	// turn this off until I implement it

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Drawing;

namespace SILConvertersWordML
{
    public abstract class DocXmlDocument
    {
        protected DocXmlDocument(MapIteratorList mapIteratorList)
        {
            MapIteratorList = mapIteratorList;
        }

        public Dictionary<string, string> MapStyleName2FontName = new Dictionary<string, string>();

        public MapIteratorList MapIteratorList { get; set; }

        protected string GetOutputFileSpecForXmlFile(string strDocFilename, bool bSaveXmlOutputInFolder)
        {
            string strXsltOutputFilename;
            if (bSaveXmlOutputInFolder)
            {
                strXsltOutputFilename = Path.Combine(Path.GetDirectoryName(strDocFilename),
                                                     Path.GetFileName(strDocFilename) +
                                                     GetXmlFileSuffix);
                if (File.Exists(strXsltOutputFilename))
                    File.Delete(strXsltOutputFilename);
            }
            else
            {
                strXsltOutputFilename = FontsStylesForm.GetTempFilename;
            }
            return strXsltOutputFilename;
        }

        protected abstract string GetXmlFileSuffix { get; }

        public abstract void InitializeIteratorsCustomFontNames(List<string> lstInGrid, Action<string, DataIterator> displayInGrid);
        public abstract void InitializeIteratorsFontsFromStyles(List<string> lstInGrid, Action<string, DataIterator> displayInGrid);
        public abstract void InitializeIteratorsStyleName(List<string> lstInGrid, Action<string, DataIterator> displayInGrid);

        public abstract bool ConvertDocumentByFontNameAndStyle(Dictionary<string, Font> mapName2Font, Func<string, DataIterator, string, Font, bool, bool> convertDoc);
        public abstract bool ConvertDocumentByStylesOnly(Dictionary<string, Font> mapName2Font, Func<string, DataIterator, string, Font, bool, bool> convertDoc);
        public abstract bool ConvertDocumentByFontNameOnly(Dictionary<string, Font> mapName2Font, Func<string, DataIterator, string, Font, bool, bool> convertDoc);
        public abstract bool HasFonts(List<string> astrFontsToSearchFor);
        public abstract void Save(string strXmlOutputFilename);
    }
}
