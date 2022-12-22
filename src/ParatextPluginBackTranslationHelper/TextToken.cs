using Paratext.PluginInterfaces;
using System.Dynamic;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public class TextToken : TokenBase, IUSFMTextToken
    {
        public TextToken(IUSFMTextToken token)
            : base(token.VerseRef, token.IsSpecial, token.IsFigure, token.IsFootnoteOrCrossReference, token.IsScripture, token.IsMetadata, 
                  token.IsPublishableVernacular, token.VerseOffset)
        {
            Text = token.Text;
        }

        /// <summary>
        /// ctor for testing
        /// </summary>
        /// <param name="expandoToken"></param>
        public TextToken(ExpandoObject expandoToken) :
            base((IVerseRef)TestVerseReference.ToIVerseRef(((dynamic)expandoToken).VerseRef),
                 (bool)(((dynamic)expandoToken).IsSpecial),
                 (bool)(((dynamic)expandoToken).IsFigure),
                 (bool)(((dynamic)expandoToken).IsFootnoteOrCrossReference),
                 (bool)(((dynamic)expandoToken).IsScripture),
                 (bool)(((dynamic)expandoToken).IsMetadata),
                 (bool)(((dynamic)expandoToken).IsPublishableVernacular),
                 (int)(((dynamic)expandoToken).VerseOffset))
        {
            Text = (string)((dynamic)expandoToken).Text;
        }

        public string Text { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}
