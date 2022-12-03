using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Dynamic;
using static System.Net.Mime.MediaTypeNames;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public class MarkerToken : TokenBase, IUSFMMarkerToken
    {
        public MarkerToken(IVerseRef verseRef, bool isScripture, bool isPublishableVernacular, int verseOffset)
            : base (verseRef, false, false, false, isScripture, false, isPublishableVernacular, verseOffset) 
        { 
        }

        public MarkerToken(IUSFMMarkerToken paragraphToken, int verseOffset, IVerseRef verseRef)
            : base(verseRef, paragraphToken.IsSpecial, paragraphToken.IsFigure, paragraphToken.IsFootnoteOrCrossReference,
                   paragraphToken.IsScripture, paragraphToken.IsMetadata, paragraphToken.IsPublishableVernacular, verseOffset)
        {
            Marker = paragraphToken.Marker ?? "p";  // default to paragraph
            Type = paragraphToken.Type;
            Attributes = paragraphToken.Attributes;
            Data = paragraphToken.Data;
            EndMarker = paragraphToken.EndMarker;
        }

        public MarkerToken(ExpandoObject expandoToken) :
            base((IVerseRef)TestVerseReference.ToIVerseRef(((dynamic)expandoToken).VerseRef),
                 (bool)(((dynamic)expandoToken).IsSpecial),
                 (bool)(((dynamic)expandoToken).IsFigure),
                 (bool)(((dynamic)expandoToken).IsFootnoteOrCrossReference),
                 (bool)(((dynamic)expandoToken).IsScripture),
                 (bool)(((dynamic)expandoToken).IsMetadata),
                 (bool)(((dynamic)expandoToken).IsPublishableVernacular),
                 (int)(((dynamic)expandoToken).VerseOffset))
        {
            Marker = (string)((dynamic)expandoToken).Marker;
            Type = (MarkerType)((dynamic)expandoToken).Type;
            Data = (string)((dynamic)expandoToken).Data;
            EndMarker = (string)((dynamic)expandoToken).EndMarker;
        }

        public MarkerType Type { get; set; }

        public string Marker { get; set; }

        public IEnumerable<IUSFMAttribute> Attributes { get; set; }

        public string Data { get; set; }

        public string EndMarker { get; set; }

        public override string ToString()
        {
            return $@"\{Marker}";
        }
    }
}
