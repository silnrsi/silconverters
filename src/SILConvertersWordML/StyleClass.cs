using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SILConvertersWordML
{
    public class StyleClass
    {
        public delegate void ChangeFontName(string strOldName, string strNewName);

        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> FontNames { get; set; }
        public ChangeFontName ChangeFontNameFunc { get; set; }
        public XElement AssociatedStyleElem { get; set; }
        public bool HasFontFormatting
        {
            get { return (FontNames != null); }
        }
    }
}
