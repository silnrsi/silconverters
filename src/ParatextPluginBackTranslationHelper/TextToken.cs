using Paratext.PluginInterfaces;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public class TextToken : IUSFMTextToken
    {
        public TextToken(IUSFMTextToken token)
        {
            Text = token.Text;
            VerseRef = token.VerseRef;
            VerseOffset = token.VerseOffset;
            IsSpecial = token.IsSpecial;
            IsFigure = token.IsFigure;
            IsFootnoteOrCrossReference = token.IsFootnoteOrCrossReference;
            IsScripture = token.IsScripture;
            IsMetadata = token.IsMetadata;
            IsPublishableVernacular = token.IsPublishableVernacular;
        }

        public string Text { get; set; }

        public IVerseRef VerseRef { get; set; }

        public int VerseOffset { get; set; }

        public bool IsSpecial { get; set; }

        public bool IsFigure { get; set; }

        public bool IsFootnoteOrCrossReference { get; set; }

        public bool IsScripture { get; set; }

        public bool IsMetadata { get; set; }

        public bool IsPublishableVernacular { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}
