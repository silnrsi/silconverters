using Paratext.PluginInterfaces;
using System.Dynamic;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public abstract class TokenBase : IUSFMToken
    {
        protected TokenBase(IVerseRef verseRef, bool isSpecial, bool isFigure, bool isFootnoteOrCrossReference, bool isScripture, bool isMetaData,
                            bool isPublishableVernacular, int offset = 0)
        {
            VerseRef = verseRef;
            IsSpecial = isSpecial;
            IsFigure = isFigure;
            IsFootnoteOrCrossReference = isFootnoteOrCrossReference;
            IsScripture = isScripture;
            IsMetadata = isMetaData;
            IsPublishableVernacular = isPublishableVernacular;
            VerseOffset = offset;
        }

        public IVerseRef VerseRef { get; }
        public int VerseOffset { get; }
        public virtual bool IsSpecial { get; }
        public virtual bool IsFigure { get; }
        public virtual bool IsFootnoteOrCrossReference { get; }
        public virtual bool IsScripture { get; }
        public virtual bool IsMetadata { get; }
        public virtual bool IsPublishableVernacular { get; }

        // public abstract int EndVerseOffset { get; }
    }
}
