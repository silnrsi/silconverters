using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Non-modal dialog to add a bad -> good spelling rule for the word selected in Paratext (so the user can
    /// still search, copy, etc. in Paratext while it's up)
    /// </summary>
    internal partial class FixSpellingForm : Form
    {
        private readonly SpellFixerProject _spellFixerProject;
        private readonly IKeyboard _vernacularKeyboard;
        private readonly IKeyboard _defaultKeyboard;
        private readonly Func<string, string, bool> _applyFix;

        public FixSpellingForm(SpellFixerProject spellFixerProject, Font font, bool rightToLeft, IKeyboard vernacularKeyboard,
                               IKeyboard defaultKeyboard, Func<string, string, bool> applyFix)
        {
            InitializeComponent();
            _spellFixerProject = spellFixerProject;
            _vernacularKeyboard = vernacularKeyboard;
            _defaultKeyboard = defaultKeyboard;
            _applyFix = applyFix;

            Text = $"{SpellFixerPlugin.PluginName}: Fix Spelling ({spellFixerProject.DisplayName})";
            if (font != null)
                textBoxBadForm.Font = textBoxReplacement.Font = listBoxSimilarWords.Font = font;
            textBoxBadForm.RightToLeft = textBoxReplacement.RightToLeft = listBoxSimilarWords.RightToLeft =
                rightToLeft ? RightToLeft.Yes : RightToLeft.No;
        }

        public void LoadWord(string badForm, string verseReference)
        {
            labelVerse.Text = verseReference;
            textBoxBadForm.Text = badForm;      // TextChanged fills in the replacement and similar words
            RefreshSuggestions();               // ... even if it's the same word as before

            // so Ctrl+V pastes into the Replacement box (Focus() alone doesn't work before the form is first shown)
            ActiveControl = textBoxReplacement;
            textBoxReplacement.Focus();
            textBoxReplacement.SelectAll();
        }

        private void RefreshSuggestions()
        {
            var bad = textBoxBadForm.Text.Trim();

            var replacement = bad;
            try
            {
                replacement = _spellFixerProject.Convert(bad);
            }
            catch
            {
                // leave it as the bad form
            }
            textBoxReplacement.Text = replacement;
            textBoxReplacement.SelectAll();

            List<SimilarWord> similarWords;
            try
            {
                similarWords = _spellFixerProject.GetSimilarWords(bad);
            }
            catch
            {
                similarWords = new List<SimilarWord>();     // (this runs on every keystroke, so don't complain)
            }
            listBoxSimilarWords.BeginUpdate();
            listBoxSimilarWords.Items.Clear();
            listBoxSimilarWords.Items.AddRange(similarWords.Cast<object>().ToArray());
            listBoxSimilarWords.EndUpdate();
            listBoxSimilarWords.Visible = labelSimilarWords.Visible = similarWords.Any();
        }

        private void TextBoxBadForm_TextChanged(object sender, EventArgs e)
        {
            RefreshSuggestions();
        }

        private void ListBoxSimilarWords_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxSimilarWords.SelectedItem is SimilarWord similarWord)
                textBoxReplacement.Text = similarWord.Word;
        }

        private void ListBoxSimilarWords_DoubleClick(object sender, EventArgs e)
        {
            if (listBoxSimilarWords.SelectedItem is SimilarWord)
                ButtonOk_Click(sender, e);
        }

        private void TextBox_Enter(object sender, EventArgs e)
        {
            ActivateKeyboard(_vernacularKeyboard);
        }

        private void FixSpellingForm_Deactivate(object sender, EventArgs e)
        {
            ActivateKeyboard(_defaultKeyboard);
        }

        // coming back from Paratext (e.g. after searching there), Enter isn't raised again for the text box that
        //  still has the focus, so switch back to the vernacular keyboard here
        private void FixSpellingForm_Activated(object sender, EventArgs e)
        {
            if ((ActiveControl == textBoxBadForm) || (ActiveControl == textBoxReplacement))
                ActivateKeyboard(_vernacularKeyboard);
        }

        private static void ActivateKeyboard(IKeyboard keyboard)
        {
            try
            {
                keyboard?.Activate();
            }
            catch
            {
                // not worth bothering the user about
            }
        }

        private void ButtonOk_Click(object sender, EventArgs e)
        {
            var bad = textBoxBadForm.Text.Trim();
            var good = textBoxReplacement.Text.Trim();
            if (String.IsNullOrEmpty(bad) || String.IsNullOrEmpty(good) || (bad == good))
            {
                MessageBox.Show(this, "Put the correct spelling in the Replacement box first (e.g. paste it with Ctrl+V, or click one of the similar words).",
                                SpellFixerPlugin.PluginName);
                return;
            }

            if (_applyFix(bad, good))
                Close();
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
