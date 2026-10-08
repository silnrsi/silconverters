using ECInterfaces;
using SilEncConverters40;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SIL.SpellFixerPluginForParatext
{
    // adapted from SILConvertersOffice's SILConverterProcessorForm
    internal partial class SpellFixerQueryForm : SpellFixerQueryFormBase
    {
        private readonly SpellFixerProject _spellFixerProject;
        private readonly Action<string, string> _ruleAdded;

        public SpellFixerQueryForm(SpellFixerProject spellFixerProject, Action<string, string> ruleAdded)
        {
            InitializeComponent();
            _spellFixerProject = spellFixerProject;
            _ruleAdded = ruleAdded;
        }

        public FormButtons Show(IWin32Window owner, Font font, bool rightToLeft, string word, string suggestion)
        {
            return Show(owner, _spellFixerProject.Converter, font, rightToLeft, word, suggestion);
        }

        private void buttonDebug_Click(object sender, EventArgs e)
        {
            IEncConverter aIEC = m_aEC?.GetEncConverter;
            if (aIEC != null)
            {
                bool bOrigValue = aIEC.Debug;
                aIEC.Debug = true;

                RefreshTextBoxes(m_aEC);

                aIEC.Debug = bOrigValue;
            }
        }

        protected void buttonRefresh_Click(object sender, EventArgs e)
        {
            RefreshTextBoxes(m_aEC);
        }

        private void buttonViewRule_Click(object sender, EventArgs e)
        {
            TryAction(() => _spellFixerProject.FindReplacementRule(textBoxInput.Text));
        }

        private void buttonAddRule_Click(object sender, EventArgs e)
        {
            var word = textBoxInput.Text;
            var before = SafeConvert(word);
            TryAction(() => _spellFixerProject.AssignCorrectSpelling(word));

            // FindReplaceHelper doesn't tell us what the user chose, so see what the converter does with it now
            var after = SafeConvert(word);
            if ((after != null) && (after != before) && (after != word))
                _ruleAdded?.Invoke(word, after);
        }

        private string SafeConvert(string word)
        {
            try
            {
                return _spellFixerProject.Convert(word);
            }
            catch
            {
                return null;
            }
        }

        private void TryAction(Action action)
        {
            try
            {
                action();

                // the project reloaded its converter, so use the new one
                m_aEC = _spellFixerProject.Converter;
                RefreshTextBoxes(m_aEC);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, SpellFixerPlugin.PluginName);
            }
        }
    }
}
