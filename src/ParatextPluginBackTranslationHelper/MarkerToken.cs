using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    class MarkerToken : IUSFMMarkerToken
    {
        public MarkerToken() { }

        public MarkerToken(IUSFMMarkerToken paragraphToken, int verseOffset, IVerseRef verseRef)
        {
            Type = paragraphToken.Type;
            Marker = "p";
            Attributes = paragraphToken.Attributes;
            Data = paragraphToken.Data;
            EndMarker = paragraphToken.EndMarker;
            VerseRef = verseRef;
            IsSpecial = paragraphToken.IsSpecial;
            IsFigure = paragraphToken.IsFigure;
            IsFootnoteOrCrossReference = paragraphToken.IsFootnoteOrCrossReference;
            IsScripture = paragraphToken.IsScripture;
            IsMetadata = paragraphToken.IsMetadata;
            VerseOffset = verseOffset;
            IsPublishableVernacular = paragraphToken.IsPublishableVernacular;
        }

        public MarkerType Type { get; set; }

        public string Marker { get; set; }

        public IEnumerable<IUSFMAttribute> Attributes { get; set; }

        public string Data { get; set; }

        public string EndMarker { get; set; }

        public IVerseRef VerseRef { get; set; }

        public int VerseOffset { get; set; }

        public bool IsSpecial { get; set; }

        public bool IsFigure { get; set; }

        public bool IsFootnoteOrCrossReference { get; set; }

        public bool IsScripture { get; set; }

        public bool IsMetadata { get; set; }

        public bool IsPublishableVernacular { get; set; }
    }
}
