using System;
using System.Collections.Generic;

namespace SIL.SpellFixerPluginForParatext
{
    // copied from SILConvertersOffice (BaseConverterForm.cs). 'Next' is the "Skip" button
    public enum FormButtons
    {
        None,
        Next,
        ReplaceOnce,
        ReplaceEvery,
        ReplaceAll,
        Cancel,
        Redo,
        Copy
    }

    /// <summary>
    /// Asks the user what to do about 'word', which the SpellFixer would change to 'suggestion'. 'correctedSpelling'
    /// is what's in the "Replace with" box when they click a button (they may have edited it)
    /// </summary>
    public delegate FormButtons AskUser(string word, string suggestion, out string correctedSpelling);

    /// <summary>
    /// Keeps track of the user's decisions during one checking run (e.g. Replace Every and Replace All)
    /// </summary>
    public class SpellFixerSession
    {
        private readonly Func<string, string> _convert;
        private readonly AskUser _askUser;
        private readonly Dictionary<string, string> _replaceEvery = new Dictionary<string, string>();

        public SpellFixerSession(Func<string, string> convert, AskUser askUser)
        {
            _convert = convert;
            _askUser = askUser;
        }

        public bool IsReplaceAll { get; private set; }

        /// <summary>
        /// Returns what 'word' should become (itself if unchanged), or null if the user cancelled
        /// </summary>
        public string FixWord(string word)
        {
            if (_replaceEvery.TryGetValue(word, out string everyReplacement))
                return everyReplacement;

            var suggestion = _convert(word);
            if (suggestion == word)
                return word;

            if (IsReplaceAll)
                return suggestion;

            var button = _askUser(word, suggestion, out string correctedSpelling);
            correctedSpelling ??= suggestion;
            switch (button)
            {
                case FormButtons.ReplaceOnce:
                    return correctedSpelling;

                case FormButtons.ReplaceEvery:
                    _replaceEvery[word] = correctedSpelling;
                    return correctedSpelling;

                case FormButtons.ReplaceAll:
                    IsReplaceAll = true;
                    return correctedSpelling;

                case FormButtons.Cancel:
                case FormButtons.None:
                    return null;

                default:    // Next (i.e. Skip)
                    return word;
            }
        }
    }
}
