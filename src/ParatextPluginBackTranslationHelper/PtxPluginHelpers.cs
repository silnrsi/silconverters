using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    internal partial class PtxPluginHelpers
    {
        // normally, text tokens are publishable, but there are some that aren't (e.g. the text content of an \id marker).
        // And there's one case that seems like a bug to me, but which I've been told has worked that way forever and so
        // there's no changing it now... vis-a-vis:
        // the \va...\va* inline marker is defined differently depending on whether it comes immediately after a \v [num(s)] 
        // marker than if it comes elsewhere in a verse. The relevant difference is that when it comes immediately after a \v 
        // marker, it's value for IsPublishableVernacular (false) and IsMetadata (true) are opposite from the other case. 
        // So... if IsPublishableVernacular is false, at least check if this is that case, and return true, so we'll try to 
        // translate it as the others are (bkz we only send IsPub text segments for translation)
        public static bool IsPublishableVernacular(IUSFMTextToken t, List<IUSFMToken> tokens)
        {
            return t.IsPublishableVernacular ||
                   (PreviousToken(t, tokens, out IUSFMMarkerToken mt) && (mt.Marker == "va") && mt.IsMetadata);
        }

        public static bool PreviousToken(IUSFMTextToken t, List<IUSFMToken> tokens, out IUSFMMarkerToken previousToken)
        {
            var index = tokens.IndexOf(t) - 1;
            if ((index >= 0) && (index < tokens.Count) && (tokens[index] is IUSFMMarkerToken prevToken))
            {
                previousToken = prevToken;
                return true;
            }

            previousToken = null;
            return false;
        }

        public static bool IsMatchingVerse(IVerseRef verseReferenceFromToken, IVerseRef verseReference)
        {
            return ((verseReferenceFromToken?.ToString() == verseReference?.ToString()) ||
                    (verseReferenceFromToken.AllVerses?.Any(vr => vr.ToString() == verseReference?.ToString()) ?? false) ||
                    (verseReference?.AllVerses?.Any(vr => vr.ToString() == verseReferenceFromToken.ToString()) ?? false));
        }

        public static bool IsScriptureText(IUSFMTextToken token)
        {
            return (token.IsPublishableVernacular && token.IsScripture);
        }


        public static bool IsParagraphToken(IUSFMToken token)
        {
            return (token is IUSFMMarkerToken markerToken) && (markerToken.Type == MarkerType.Paragraph); // not needed? if so, initialize list from a setting: && _paragraphMarkers.Contains(markerToken.Marker);
        }
    }
}
