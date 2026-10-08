using BackTranslationHelper;
using ECInterfaces;
using SilEncConverters40;
using SpellingFixer30;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// The SpellFixer project (either a whole word Consistent Spelling Checker project or a (partial word) legacy
    /// SpellFixer project) the user chose, and the EncConverter used to check words with it
    /// </summary>
    internal class SpellFixerProject
    {
        private readonly PluginFindReplaceHelper _findReplaceHelper;

        private SpellFixerProject(PluginFindReplaceHelper findReplaceHelper)
        {
            _findReplaceHelper = findReplaceHelper;
            ConverterName = findReplaceHelper.SpellFixerEncConverterName;
            ReloadConverter();
        }

        /// <summary>
        /// Asks the user which kind of project to use (whole word or partial word) and which project. Returns null if
        /// they didn't choose one.
        /// </summary>
        public static SpellFixerProject QueryUser()
        {
            // same as FindReplaceHelper.GetFindReplaceHelper, but with our subclass
            var findReplaceHelper = new PluginFindReplaceHelper();
            findReplaceHelper.QuerySpellFixProjectType();
            return findReplaceHelper.HasProject ? new SpellFixerProject(findReplaceHelper) : null;
        }

        /// <summary>
        /// Loads a previously chosen project by its EncConverter name (e.g. "Consistent Spelling for xyz"); throws if
        /// it can't be loaded
        /// </summary>
        public static SpellFixerProject FromConverterName(string converterName)
        {
            return new SpellFixerProject(new PluginFindReplaceHelper(converterName));
        }

        public string ConverterName { get; }

        public DirectableEncConverter Converter { get; private set; }

        public bool IsWholeWord => ConverterName.StartsWith(SpellingFixer.SFConverterPrefixCsc);

        public string DisplayName => IsWholeWord
                                        ? $"Whole word (CSC): {ConverterName.Substring(SpellingFixer.SFConverterPrefixCsc.Length)}"
                                        : $"Partial word (legacy): {ConverterName.Substring(SpellingFixer.SFConverterPrefix.Length)}";

        public string Convert(string word)
        {
            return Converter.Convert(word);
        }

        public void AssignCorrectSpelling(string word)
        {
            _findReplaceHelper.AssignCorrectSpelling(word);
            ReloadConverter();
        }

        public void FindReplacementRule(string word)
        {
            _findReplaceHelper.FindReplacementRule(word);
            ReloadConverter();
        }

        public void EditSpellingFixes()
        {
            _findReplaceHelper.EditSpellingFixes();
            ReloadConverter();
        }

        public int WordsInContext => _findReplaceHelper.CscProject?.WordsInContext ?? 0;

        private HashSet<char> _trimCharacters;

        /// <summary>
        /// The characters (besides whitespace, digits and Unicode punctuation) the CC table doesn't consider part of a
        /// word, so they should be trimmed off a selected word: the default word boundary punctuation and, for a CSC
        /// project, its script system's 'extra punctuation' (e.g. '|')
        /// </summary>
        public ICollection<char> TrimCharacters
        {
            get
            {
                if (_trimCharacters == null)
                {
                    _trimCharacters = new HashSet<char>(System.Text.RegularExpressions.Regex
                                                            .Matches(SpellingFixer.cstrDefaultPunctuationAndWhitespace, "'(.)'|\"(.)\"")
                                                            .Cast<System.Text.RegularExpressions.Match>()
                                                            .Select(m => m.Groups[1].Success ? m.Groups[1].Value[0] : m.Groups[2].Value[0]));

                    // CscProject doesn't expose its script system's extra punctuation, so get it the hard way (and do
                    //  without it if that ever changes)
                    try
                    {
                        var trimPunctuationField = typeof(CscProject).GetField("m_achTrimPunctuation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if ((_findReplaceHelper.CscProject != null) && (trimPunctuationField?.GetValue(_findReplaceHelper.CscProject) is IEnumerable<char> extraPunctuation))
                            _trimCharacters.UnionWith(extraPunctuation);
                    }
                    catch
                    {
                        // just use the defaults
                    }
                }
                return _trimCharacters;
            }
        }

        /// <summary>
        /// Adds the bad -> good rule (without the modal 'Fix Spelling' dialog, but still with the project's own
        /// questions, e.g. "already a good word..."). For a CSC project, 'context' is stored with the good word.
        /// Returns whether the rule is now in effect (false if the user said No/Cancel to one of those questions,
        /// which just return without telling us); throws if it can't be added
        /// </summary>
        public bool AssignCorrectSpelling(string bad, string good, List<string> context)
        {
            var cscProject = _findReplaceHelper.CscProject;
            if (cscProject != null)
                cscProject.AssignCorrectSpelling(bad, good, bNoUI: false, context);   // its questions should be seen
            else
                _findReplaceHelper.LegacySpellingFixer.AssignCorrectSpelling(bad, good);

            ReloadConverter();
            return Convert(bad) == good;
        }

        /// <summary>
        /// The words the CscProject considers ambiguous with 'word' (its 'bundle'), most frequent first; empty for
        /// legacy projects (which don't have the notion)
        /// </summary>
        public List<SimilarWord> GetSimilarWords(string word)
        {
            var words = String.IsNullOrEmpty(word) ? null : _findReplaceHelper.CscProject?.GetAmbiguousWords(word);
            return words?.Select(w => new SimilarWord { Word = w.Value, Count = w.Count }).ToList()
                   ?? new List<SimilarWord>();
        }

        // so that rules just added or edited are used for the next word checked
        public void ReloadConverter()
        {
            DirectableEncConverter.EncConverters.Reinitialize();
            Converter = new DirectableEncConverter(ConverterName, bDirectionForward: true, NormalizeFlags.None);
        }
    }

    public class SimilarWord
    {
        public string Word { get; set; }
        public int Count { get; set; }
        public override string ToString() => $"{Word} ({Count})";
    }
}
