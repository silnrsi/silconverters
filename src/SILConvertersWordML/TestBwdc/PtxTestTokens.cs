using System.Collections.Generic;
using Paratext.PluginInterfaces;
using SIL.ParatextBackTranslationHelperPlugin;

namespace TestBwdc
{
    /// <summary>
    /// Builders for Paratext USFM tokens to use in tests (all references are in MAT)
    /// </summary>
    internal static class PtxTestTokens
    {
        public static IVerseRef Vref(int chapter, int verse, int? lastVerse = null)
        {
            var vref = new TestVerseReference
            {
                BookCode = "MAT",
                BookNum = 40,
                ChapterNum = chapter,
                VerseNum = verse,
                BBBCCCVVV = (40 * 1000000) + (chapter * 1000) + verse,
                RepresentsMultipleVerses = lastVerse.HasValue,
            };

            var allVerses = new List<IVerseRef>();
            if (lastVerse.HasValue)
            {
                for (var v = verse; v <= lastVerse.Value; v++)
                    allVerses.Add(Vref(chapter, v));
            }
            else
                allVerses.Add(vref);

            vref.AllVerses = allVerses;
            return vref;
        }

        public static IUSFMTextToken Text(IVerseRef vref, string text, bool publishable = true)
        {
            return new TestTextToken(vref, text, publishable);
        }

        public static IUSFMMarkerToken Marker(IVerseRef vref, string marker, MarkerType type)
        {
            return new MarkerToken(vref, isScripture: true, isPublishableVernacular: true, verseOffset: 0)
            {
                Marker = marker,
                Type = type,
            };
        }
    }

    internal class TestTextToken : TokenBase, IUSFMTextToken
    {
        public TestTextToken(IVerseRef vref, string text, bool publishable)
            : base(vref, false, false, false, true, !publishable, publishable)
        {
            Text = text;
        }

        public string Text { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}
