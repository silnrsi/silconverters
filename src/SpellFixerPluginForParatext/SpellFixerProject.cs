using BackTranslationHelper;
using ECInterfaces;
using SilEncConverters40;
using SpellingFixer30;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// The SpellFixer project (either a whole word Consistent Spelling Checker project or a (partial word) legacy
    /// SpellFixer project) the user chose, and the EncConverter used to check words with it
    /// </summary>
    internal class SpellFixerProject
    {
        private readonly FindReplaceHelper _findReplaceHelper;

        private SpellFixerProject(FindReplaceHelper findReplaceHelper)
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
            var findReplaceHelper = FindReplaceHelper.GetFindReplaceHelper();
            return (findReplaceHelper == null) ? null : new SpellFixerProject(findReplaceHelper);
        }

        /// <summary>
        /// Loads a previously chosen project by its EncConverter name (e.g. "Consistent Spelling for xyz"); throws if
        /// it can't be loaded
        /// </summary>
        public static SpellFixerProject FromConverterName(string converterName)
        {
            return new SpellFixerProject(new FindReplaceHelper(converterName));
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

        // so that rules just added or edited are used for the next word checked
        public void ReloadConverter()
        {
            DirectableEncConverter.EncConverters.Reinitialize();
            Converter = new DirectableEncConverter(ConverterName, bDirectionForward: true, NormalizeFlags.None);
        }
    }
}
