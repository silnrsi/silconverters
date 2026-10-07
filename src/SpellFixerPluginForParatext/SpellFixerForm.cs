using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SIL.SpellFixerPluginForParatext
{
    internal partial class SpellFixerForm : Form
    {
        private readonly IPluginHost _host;
        private readonly IPluginObject _plugin;
        private readonly IProject _project;
        private readonly Action<IVerseRef> _setSyncReference;
        private IVerseRef _verseReference;
        private SpellFixerProject _spellFixerProject;
        private bool _isChecking;

        private static readonly Bitmap _pinUp = LoadBitmap("pinup.bmp");
        private static readonly Bitmap _pinDown = LoadBitmap("pindown.bmp");

        public SpellFixerForm(IPluginHost host, IPluginObject plugin, IProject project, IVerseRef initialVerseReference, Action<IVerseRef> setSyncReference)
        {
            InitializeComponent();
            InitializeSettings();

            _host = host;
            _plugin = plugin;
            _project = project;
            _setSyncReference = setSyncReference;
            SetVerseReference(initialVerseReference);

            labelPtxProject.Text = project.ShortName;
            SetPinToTop(Properties.Settings.Default.PinToTop);

            _host.VerseRefChanged += Host_VerseRefChanged;
        }

        public string ProjectShortName => _project.ShortName;

        private static Bitmap LoadBitmap(string name)
        {
            return new Bitmap(typeof(SpellFixerForm).Assembly.GetManifestResourceStream($"SIL.SpellFixerPluginForParatext.{name}"));
        }

        /// <summary>
        /// If Ptx is upgraded, then we lose the settings. This upgrades them the first time after an install.
        /// </summary>
        private static void InitializeSettings()
        {
            if (!Properties.Settings.Default.UpgradeSettings)
                return;

            Properties.Settings.Default.Upgrade();
            Properties.Settings.Default.UpgradeSettings = false;
            Properties.Settings.Default.Save();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadSavedSpellFixerProject();
            UpdateButtonStates();
        }

        private void SpellFixerForm_Load(object sender, EventArgs e)
        {
            var location = Properties.Settings.Default.WindowLocation;
            if ((location != Point.Empty) && Screen.AllScreens.Any(s => s.WorkingArea.Contains(location)))
                Location = location;
        }

        private void SpellFixerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isChecking)
            {
                e.Cancel = true;    // let the run finish (click Cancel in the query dialog to stop it)
                return;
            }

            _host.VerseRefChanged -= Host_VerseRefChanged;

            if (WindowState == FormWindowState.Normal)
                Properties.Settings.Default.WindowLocation = Location;
            Properties.Settings.Default.Save();
        }

        private void Host_VerseRefChanged(IPluginHost sender, IVerseRef newReference, SyncReferenceGroup group)
        {
            // while checking, we're the ones moving Paratext around, so don't change where the next run would start
            if (_isChecking)
                return;

            SetVerseReference(newReference);
        }

        private void SetVerseReference(IVerseRef verseReference)
        {
            _verseReference = (verseReference?.RepresentsMultipleVerses ?? false)
                                ? verseReference.AllVerses.First()
                                : verseReference;
            labelVerse.Text = _verseReference?.ToString();
        }

        #region SpellFixer project (remembered per Paratext project)

        private void LoadSavedSpellFixerProject()
        {
            var converterName = GetSavedConverterName(_project.ShortName);
            if (String.IsNullOrEmpty(converterName))
            {
                ChooseSpellFixerProject();
                return;
            }

            try
            {
                SetSpellFixerProject(SpellFixerProject.FromConverterName(converterName));
            }
            catch (Exception ex)
            {
                _host.Log(_plugin, $"Unable to load Spell Fixer project '{converterName}': {ex.Message}");
                SaveConverterName(_project.ShortName, null);
                ChooseSpellFixerProject();
            }
        }

        private void ChooseSpellFixerProject()
        {
            try
            {
                var spellFixerProject = SpellFixerProject.QueryUser();
                if (spellFixerProject == null)
                    return;     // keep what we had

                SetSpellFixerProject(spellFixerProject);
                SaveConverterName(_project.ShortName, spellFixerProject.ConverterName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, SpellFixerPlugin.PluginName);
            }
        }

        private void SetSpellFixerProject(SpellFixerProject spellFixerProject)
        {
            _spellFixerProject = spellFixerProject;
            _queryForm?.Dispose();
            _queryForm = null;
            labelSpellFixerProject.Text = spellFixerProject.DisplayName;
            UpdateButtonStates();
        }

        // the setting is a list of "PtxShortName=SpellFixerEncConverterName" entries
        private static string GetSavedConverterName(string ptxProjectShortName)
        {
            var prefix = ptxProjectShortName + "=";
            return Properties.Settings.Default.MapPtxProjectToSpellFixerConverter?
                                              .Cast<string>()
                                              .FirstOrDefault(s => s.StartsWith(prefix))?
                                              .Substring(prefix.Length);
        }

        private static void SaveConverterName(string ptxProjectShortName, string converterName)
        {
            var prefix = ptxProjectShortName + "=";
            var entries = Properties.Settings.Default.MapPtxProjectToSpellFixerConverter?.Cast<string>()
                                                     .Where(s => !s.StartsWith(prefix))
                                                     .ToList()
                          ?? new List<string>();
            if (!String.IsNullOrEmpty(converterName))
                entries.Add(prefix + converterName);

            var collection = new StringCollection();
            collection.AddRange(entries.ToArray());
            Properties.Settings.Default.MapPtxProjectToSpellFixerConverter = collection;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region Buttons

        private void UpdateButtonStates()
        {
            var haveProject = (_spellFixerProject != null);
            buttonChooseProject.Enabled = !_isChecking;
            buttonCheck.Enabled = haveProject && !_isChecking;
            buttonAssignCorrectSpelling.Enabled = haveProject && !_isChecking;
            buttonFindReplacementRule.Enabled = haveProject && !_isChecking;
            buttonEditSpellingFixes.Enabled = haveProject && !_isChecking;
        }

        private void ButtonChooseProject_Click(object sender, EventArgs e)
        {
            ChooseSpellFixerProject();
        }

        private void ButtonAssignCorrectSpelling_Click(object sender, EventArgs e)
        {
            DoWithClipboardWord(word => _spellFixerProject.AssignCorrectSpelling(word));
        }

        private void ButtonFindReplacementRule_Click(object sender, EventArgs e)
        {
            DoWithClipboardWord(word => _spellFixerProject.FindReplacementRule(word));
        }

        private void ButtonEditSpellingFixes_Click(object sender, EventArgs e)
        {
            TryAction(() => _spellFixerProject.EditSpellingFixes());
        }

        private void DoWithClipboardWord(Action<string> action)
        {
            var word = Clipboard.ContainsText(TextDataFormat.UnicodeText)
                        ? Clipboard.GetText(TextDataFormat.UnicodeText)?.Trim()
                        : null;
            if (String.IsNullOrEmpty(word))
            {
                MessageBox.Show("Copy a word (e.g. from Paratext) to the clipboard first, and then click this button again.", SpellFixerPlugin.PluginName);
                return;
            }

            TryAction(() => action(word));
        }

        private void TryAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, SpellFixerPlugin.PluginName);
            }
        }

        private void ButtonPinToTop_Click(object sender, EventArgs e)
        {
            SetPinToTop(!TopMost);
            Properties.Settings.Default.PinToTop = TopMost;
            Properties.Settings.Default.Save();
        }

        private void SetPinToTop(bool pinToTop)
        {
            TopMost = pinToTop;
            buttonPinToTop.Image = pinToTop ? _pinDown : _pinUp;
        }

        private void ButtonCheck_Click(object sender, EventArgs e)
        {
            CheckFromCurrentVerse();
        }

        #endregion

        private SpellFixerQueryForm _queryForm;

        // implemented in Task 6
        private void CheckFromCurrentVerse()
        {
        }
    }
}
