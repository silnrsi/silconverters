using BackTranslationHelper;
using SpellingFixer30;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Gives the plugin access to the CscProject/SpellingFixer that FindReplaceHelper loaded (so we can add rules
    /// without its modal UI, and get the similar words), while keeping a single in-memory copy of the project
    /// </summary>
    internal class PluginFindReplaceHelper : FindReplaceHelper
    {
        public PluginFindReplaceHelper()
        {
        }

        public PluginFindReplaceHelper(string projectName)
            : base(projectName)
        {
        }

        public CscProject CscProject => IsCscProject ? m_cscProject : null;

        public SpellingFixer LegacySpellingFixer => IsSpellFixerLegacyProject ? m_aSpellFixerLegacy : null;

        public bool HasProject => IsSpellFixerProject;
    }
}
