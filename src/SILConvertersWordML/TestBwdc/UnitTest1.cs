using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SILConvertersWordML;

namespace TestBwdc
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void MakeSureWeCombineIsoFormattedRuns()
        {
            const string cstrTwoIsoFormattedRuns =
@"<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>vkcfnueu dj fH</w:t>
</w:r>
<w:r wsp:rsidR=""00B06F43"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>kM+ tksgku ls iqNyk;a]</w:t>
</w:r>";
            const string cstrOneRunOutput =
@"<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>vkcfnueu dj fHkM+ tksgku ls iqNyk;a]</w:t>
</w:r>";
            var strInput = String.Format(Properties.Resources.TestFile1,
                                         cstrTwoIsoFormattedRuns,
                                         cstrTwoIsoFormattedRuns);
            var doc = XDocument.Parse(strInput);
            WordLinqDocument.CombineIsoFormattedRuns(doc);
            var strResult = doc.ToString();
            Assert.IsTrue(strResult.Contains(cstrOneRunOutput));
        }

        [TestMethod]
        public void MakeSureWeCombineThreeIsoFormattedRuns()
        {
            const string cstrThreeIsoFormattedRuns =
@"<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>vkcfnueu dj fH</w:t>
</w:r>
<w:r wsp:rsidR=""00B06F43"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>kM+ tksgku ls iqNyk;a]</w:t>
</w:r>
<w:r wsp:rsidR=""00B06F43"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>dded on a 3rd one</w:t>
</w:r>";
            const string cstrOneRunOutput =
@"<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>vkcfnueu dj fHkM+ tksgku ls iqNyk;a]dded on a 3rd one</w:t>
</w:r>";
            var strInput = String.Format(Properties.Resources.TestFile1,
                                         cstrThreeIsoFormattedRuns,
                                         cstrThreeIsoFormattedRuns);
            var doc = XDocument.Parse(strInput);
            WordLinqDocument.CombineIsoFormattedRuns(doc);
            var strResult = doc.ToString();
            Assert.IsTrue(strResult.Contains(cstrOneRunOutput));
        }

        [TestMethod]
        public void MakeSureWeCombine2SetsOfIsoFormattedRuns()
        {
            const string cstrTwoIsoFormattedRunsInterruptedByAdifferentOne =
@"<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>vkcfnueu dj fH</w:t>
</w:r>
<w:r wsp:rsidR=""00B06F43"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>kM+ tksgku ls iqNyk;a]</w:t>
</w:r>
<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 010"" />
    </w:rPr>
    <w:t>vkcfnueu dj fH</w:t>
</w:r>
<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 012"" w:h-ansi=""Kruti Dev 012"" />
        <wx:font wx:val=""Kruti Dev 012"" />
    </w:rPr>
    <w:t>vkcfnueu dj fH</w:t>
</w:r>
<w:r wsp:rsidR=""00B06F43"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 012"" w:h-ansi=""Kruti Dev 012"" />
        <wx:font wx:val=""Kruti Dev 012"" />
    </w:rPr>
    <w:t>kM+ tksgku ls iqNyk;a]</w:t>
</w:r>";
            const string cstrOneRunOutput1 =
@"<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>vkcfnueu dj fHkM+ tksgku ls iqNyk;a]</w:t>
</w:r>";
            const string cstrOneRunOutput2 =
@"<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 012"" w:h-ansi=""Kruti Dev 012"" />
        <wx:font wx:val=""Kruti Dev 012"" />
    </w:rPr>
    <w:t>vkcfnueu dj fHkM+ tksgku ls iqNyk;a]</w:t>
</w:r>";
            var strInput = String.Format(Properties.Resources.TestFile1,
                                         cstrTwoIsoFormattedRunsInterruptedByAdifferentOne,
                                         cstrTwoIsoFormattedRunsInterruptedByAdifferentOne);
            var doc = XDocument.Parse(strInput);
            WordLinqDocument.CombineIsoFormattedRuns(doc);
            var strResult = doc.ToString();
            Assert.IsTrue(strResult.Contains(cstrOneRunOutput1));
            Assert.IsTrue(strResult.Contains(cstrOneRunOutput2));
        }

        [TestMethod]
        public void MakeSureWeCombineAdjacentInsertSymbols()
        {
            const string cstrTwoIsoFormattedRuns =
@"<w:r>
    <w:rPr>
        <w:rStyle w:val=""HindiWord"" />
        <wx:font wx:val=""Arial Unicode MS"" />
        <wx:sym wx:font=""Arial Unicode MS"" wx:char=""0915"" />
    </w:rPr>
    <w:t>क</w:t>
</w:r>
<w:r>
    <w:rPr>
        <w:rStyle w:val=""HindiWord"" />
        <wx:font wx:val=""Arial Unicode MS"" />
        <wx:sym wx:font=""Arial Unicode MS"" wx:char=""093F"" />
    </w:rPr>
    <w:t>ि</w:t>
</w:r>";
            const string cstrOneRunOutput =
@"<w:r>
    <w:rPr>
        <w:rStyle w:val=""HindiWord"" />
        <wx:font wx:val=""Arial Unicode MS"" />
        
    </w:rPr>
    <w:t>कि</w:t>
</w:r>";
            var strInput = String.Format(Properties.Resources.TestFile1,
                                         cstrTwoIsoFormattedRuns,
                                         cstrTwoIsoFormattedRuns);
            var doc = XDocument.Parse(strInput);
            WordLinqDocument.CombineIsoFormattedRuns(doc);
            var strResult = doc.ToString();
            Assert.IsTrue(strResult.Contains(cstrOneRunOutput));
        }

        [TestMethod]
        public void MakeSureWeDontCombineNonIsoFormattedRuns()
        {
            const string cstrDifferentFontIn2nd =
@"<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>vkcfnueu dj fH</w:t>
</w:r>
<w:r wsp:rsidR=""00B06F43"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 010"" />
    </w:rPr>
    <w:t>kM+ tksgku ls iqNyk;a]</w:t>
</w:r>";
            var strInput = String.Format(Properties.Resources.TestFile1,
                                         cstrDifferentFontIn2nd,
                                         cstrDifferentFontIn2nd);
            var doc = XDocument.Parse(strInput);
            WordLinqDocument.CombineIsoFormattedRuns(doc);
            Assert.IsTrue(doc.ToString().Contains(cstrDifferentFontIn2nd));
        }

        [TestMethod]
        public void MakeSureWeCombineIsoFormattedRuns2()
        {
            const string cstrExtraSizeElementIn2nd =
@"<w:r wsp:rsidR=""00B1028E"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
        <w:sz w:val=""36"" />
    </w:rPr>
    <w:t>vkcfnueu dj fH</w:t>
</w:r>
<w:r wsp:rsidR=""00B06F43"" wsp:rsidRPr=""00972CAF"">
    <w:rPr>
        <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
        <wx:font wx:val=""Kruti Dev 011"" />
    </w:rPr>
    <w:t>kM+ tksgku ls iqNyk;a]</w:t>
</w:r>";
            var strInput = String.Format(Properties.Resources.TestFile1,
                                         cstrExtraSizeElementIn2nd,
                                         cstrExtraSizeElementIn2nd);
            var doc = XDocument.Parse(strInput);
            WordLinqDocument.CombineIsoFormattedRuns(doc);
            Assert.IsTrue(doc.ToString().Contains(cstrExtraSizeElementIn2nd));
        }

        [TestMethod]
        public void TestingCombiningBiggerChunk()
        {
            // from temp\BWDC\Bobby\41MATSCK.doc
            const string cstr =
@"<w:p wsp:rsidR=""003C2014"" wsp:rsidRDefault=""003C2014"" wsp:rsidP=""00422CC3"">
    <w:pPr>
        <w:jc w:val=""both"" />
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
    </w:pPr>
    <w:r>
        <w:rPr>
            <w:rFonts w:ascii=""Charis SIL"" w:fareast=""SimSun"" w:h-ansi=""Charis SIL"" w:cs=""Charis SIL"" />
            <wx:font wx:val=""Charis SIL"" />
            <w:lang w:fareast=""ZH-CN"" w:bidi=""TA"" />
        </w:rPr>
        <w:t>\s1</w:t>
    </w:r>
    <w:r wsp:rsidR=""00465C31"">
        <w:rPr>
            <w:rFonts w:ascii=""Charis SIL"" w:fareast=""SimSun"" w:h-ansi=""Charis SIL"" w:cs=""Charis SIL"" />
            <wx:font wx:val=""Charis SIL"" />
            <w:lang w:fareast=""ZH-CN"" w:bidi=""TA"" />
        </w:rPr>
        <w:t>  </w:t>
    </w:r>
    <w:r wsp:rsidR=""00F9239D"" wsp:rsidRPr=""00D53070"">
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
        <w:t>;h’kq dh [kaU</w:t>
    </w:r>
    <w:r wsp:rsidR=""004030DC"" wsp:rsidRPr=""00D53070"">
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
        <w:t>nk</w:t>
    </w:r>
    <w:r wsp:rsidR=""00F9239D"" wsp:rsidRPr=""00D53070"">
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
        <w:t>u</w:t>
    </w:r>
    <w:r wsp:rsidR=""004030DC"" wsp:rsidRPr=""00D53070"">
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
        <w:t>]</w:t>
    </w:r>
    <w:r wsp:rsidR=""00F9239D"" wsp:rsidRPr=""00D53070"">
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
        <w:t> </w:t>
    </w:r>
</w:p>";
            const string cstrOutput =
@"<w:p wsp:rsidR=""003C2014"" wsp:rsidRDefault=""003C2014"" wsp:rsidP=""00422CC3"">
    <w:pPr>
        <w:jc w:val=""both"" />
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
    </w:pPr>
    <w:r>
        <w:rPr>
            <w:rFonts w:ascii=""Charis SIL"" w:fareast=""SimSun"" w:h-ansi=""Charis SIL"" w:cs=""Charis SIL"" />
            <wx:font wx:val=""Charis SIL"" />
            <w:lang w:fareast=""ZH-CN"" w:bidi=""TA"" />
        </w:rPr>
        <w:t>\s1  </w:t>
    </w:r>
    <w:r wsp:rsidR=""00F9239D"" wsp:rsidRPr=""00D53070"">
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
        <w:t>;h’kq dh [kaUnku] </w:t>
    </w:r>
</w:p>";

            var strInput = String.Format(Properties.Resources.TestFile2,
                                         cstr);
            var doc = XDocument.Parse(strInput);
            WordLinqDocument.CombineIsoFormattedRuns(doc);
            var strResult = doc.Root.Descendants(WordLinqDocument.w + "p")
                                    .FirstOrDefault()
                                    .ToString();
            AssertEqual(strResult, cstrOutput);
        }

        public void AssertEqual(string str1, string str2)
        {
            Assert.IsTrue(XmlUtilities.AreXmlElementsEqual(RemoveNameSpaces(str1), RemoveNameSpaces(str2)));
        }

        [TestMethod]
        public void TestingStripNamespaces()
        {
            const string cstr1 =
@"<w:p wsp:rsidR=""003C2014"" wsp:rsidRDefault=""003C2014"" wsp:rsidP=""00422CC3"" 
xmlns:wsp=""http://schemas.microsoft.com/office/word/2003/wordml/sp2"" xmlns:w=""http://schemas.microsoft.com/office/word/2003/wordml"">
    <w:pPr>
        <w:jc w:val=""both"" />
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
    </w:pPr>
    <w:r>
        <w:rPr>
            <w:rFonts w:ascii=""Charis SIL"" w:fareast=""SimSun"" w:h-ansi=""Charis SIL"" w:cs=""Charis SIL"" />
            <wx:font wx:val=""Charis SIL"" />
            <w:lang w:fareast=""ZH-CN"" w:bidi=""TA"" />
        </w:rPr>
        <w:t>\s1  </w:t>
    </w:r>
    <w:r wsp:rsidR=""00F9239D"" wsp:rsidRPr=""00D53070"">
        <w:rPr>
            <w:rFonts w:ascii=""Kruti Dev 011"" w:h-ansi=""Kruti Dev 011"" />
            <wx:font wx:val=""Kruti Dev 011"" />
            <w:sz w:val=""36"" />
            <w:sz-cs w:val=""36"" />
        </w:rPr>
        <w:t>;h’kq dh [kaUnku] </w:t>
    </w:r>
</w:p>";
            const string cstr2 =
@"<p rsidR=""003C2014"" rsidRDefault=""003C2014"" rsidP=""00422CC3"">
    <pPr>
        <jc val=""both"" />
        <rPr>
            <rFonts ascii=""Kruti Dev 011"" h-ansi=""Kruti Dev 011"" />
            <font val=""Kruti Dev 011"" />
            <sz val=""36"" />
            <sz-cs val=""36"" />
        </rPr>
    </pPr>
    <r>
        <rPr>
            <rFonts ascii=""Charis SIL"" fareast=""SimSun"" h-ansi=""Charis SIL"" cs=""Charis SIL"" />
            <font val=""Charis SIL"" />
            <lang fareast=""ZH-CN"" bidi=""TA"" />
        </rPr>
        <t>\s1  </t>
    </r>
    <r rsidR=""00F9239D"" rsidRPr=""00D53070"">
        <rPr>
            <rFonts ascii=""Kruti Dev 011"" h-ansi=""Kruti Dev 011"" />
            <font val=""Kruti Dev 011"" />
            <sz val=""36"" />
            <sz-cs val=""36"" />
        </rPr>
        <t>;h’kq dh [kaUnku] </t>
    </r>
</p>";
            var strResult = RemoveNameSpaces(cstr1);
            Assert.AreEqual(strResult, cstr2);
        }


        readonly Regex _regexRemoveNamespaces = new Regex(@"(?<=([\< ]|\</))[a-z]+?\:");
        readonly Regex _regexRemoveXmlnses = new Regex(@"[\r\n ]*xmlns\:.*""[\r\n ]*");
        public string RemoveNameSpaces(string str)
        {
            var res = _regexRemoveXmlnses.Replace(str, String.Empty);
            return _regexRemoveNamespaces.Replace(res, String.Empty);
        }
    }
}
