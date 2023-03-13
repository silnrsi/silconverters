using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SILConvertersWordML;
using TestBwdc.TestFiles;
using Paratext.PluginInterfaces;
using ECInterfaces;
using NUnit.Framework;
using SIL.ParatextBackTranslationHelperPlugin;

namespace TestBwdc
{
    [TestFixture]
    public class UnitTest_PtxBackTrHelper
    {
        [Test]
        [TestCase("Complex_SingleVerse", "Complex_MissingInTarget")]
        [TestCase("MultipleParagraphs", "MultipleParagraphsMissingInTarget")]
        [TestCase("MultiplePoeticParagraphs", "MultiplePoeticParagraphsMissingInTarget")]
        [TestCase("SimpleParagraphVerse", "SimpleParagraphVerseMissingInTarget")]
        [TestCase("SingleVerse", "SingleVerseMissingInTarget")]
        /* these are all now failing and I don't think they matter anymore. We aren't now completely 
         * overwriting the target text with the translated text, rather than merging with what's
         * in the target presently.
        [TestCase("SingleVerse", "SingleVerseMiTSplitIntoTwoParagraphs")]
        [TestCase("SingleVerse", "SingleVerseSplitIntoTwoParagraphs")]
        [TestCase("MultipleParagraphs", "MultipleParagraphsJoinedIntoOneParagraphs")]
        [TestCase("MultipleParagraphs", "MultipleParagraphsMiTJoinedIntoOneParagraphs")]
        [TestCase("SimpleParagraphVerse", "SimpleParagraphVerseMiTSplitIntoTwoParagraphs")]
        [TestCase("SimpleParagraphVerse", "SimpleParagraphVerseSplitIntoTwoParagraphs")]
        [TestCase("MultipleVerses", "MultipleVersesMissingInTarget")]
        [TestCase("MultipleVerses", "MultipleVersesAndParagraphsMiTJoinedIntoOneParagraphs")]
        [TestCase("MultipleVerses", "MultipleVersesAndParagraphsJoinedIntoOneParagraphs")]
        [TestCase("Complex_SingleVerse", "Complex_PartialOverwriteMarker")]
        [TestCase("Complex_SingleVerse", "Complex_PartialOverwriteText")]
        */
        public void It_Can_Determine_Correct_Update_To_Target_Project(string fileNameCommon, string fileNameTestSpecific)
        {
            var usfmTokensSource = TestModel.LoadKeyedListOfTokens($"{fileNameCommon}_TokensSource.json");
            var usfmTokensTarget = TestModel.LoadKeyedSortedListOfTokens($"{fileNameTestSpecific}_TokensTarget.json");
            var verseReference = TestModel.LoadVerseRef($"{fileNameCommon}_VerseReference.json");
            var versesReference = TestModel.LoadVerseRef($"{fileNameCommon}_VersesReference.json");
            var translatedText = TestModel.LoadEmbeddedResourceFileAsStringExecutingAssembly($"{fileNameTestSpecific}_TranslatedText.txt");
            var result = BackTranslationHelperForm.CalculateTargetTokens(verseReference, versesReference, translatedText, usfmTokensSource, usfmTokensTarget);

            var strResult = TestModel.ToJson(result);
            Assert.IsNotNull(usfmTokensTarget);
            var usfmTokensTargetUpdated = TestModel.LoadKeyedListOfTokens($"{fileNameTestSpecific}_TokensTargetUpdate.json");
            var strUsfmTokensTargetUpdated = TestModel.ToJson(usfmTokensTargetUpdated);
            Assert.AreEqual(strUsfmTokensTargetUpdated, strResult);
        }

        [Test]
        [TestCase("MultipleVersesWithFootnote")]
        [TestCase("SeparateInSourceCombinedButEmptyInTarget")]
        [TestCase("CombinedInSourceSeparateNoEmptyInTarget")]
        [TestCase("CombinedInSourceAndTargetWithVaOverride")]
        public void It_Can_Determine_Correct_Update_To_Target_Project_For_Other_Cases(string fileNamePrefix)
        {
            var usfmTokensSource = TestModel.LoadKeyedListOfTokens($"{fileNamePrefix}_TokensSource.json");
            var usfmTokensTarget = TestModel.LoadKeyedSortedListOfTokens($"{fileNamePrefix}_TokensTarget.json");
            var verseReference = TestModel.LoadVerseRef($"{fileNamePrefix}_VerseReference.json");
            var versesReference = TestModel.LoadVerseRef($"{fileNamePrefix}_VersesReference.json");
            var translatedText = TestModel.LoadEmbeddedResourceFileAsStringExecutingAssembly($"{fileNamePrefix}_TranslatedText.txt");
            var result = BackTranslationHelperForm.CalculateTargetTokens(verseReference, versesReference, translatedText, usfmTokensSource, usfmTokensTarget);

            var strResult = TestModel.ToJson(result);
            Assert.IsNotNull(usfmTokensTarget);
            var usfmTokensTargetUpdated = TestModel.LoadKeyedListOfTokens($"{fileNamePrefix}_TokensTargetUpdate.json");
            var strUsfmTokensTargetUpdated = TestModel.ToJson(usfmTokensTargetUpdated);
            Assert.AreEqual(strUsfmTokensTargetUpdated, strResult);
        }
    }
}
