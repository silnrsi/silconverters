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

        public SpellFixerQueryForm(SpellFixerProject spellFixerProject)
        {
            InitializeComponent();
            _spellFixerProject = spellFixerProject;
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
            TryAction(() => _spellFixerProject.AssignCorrectSpelling(textBoxInput.Text));
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
