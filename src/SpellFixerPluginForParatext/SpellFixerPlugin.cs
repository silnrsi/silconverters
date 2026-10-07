using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Paratext.PluginInterfaces;
using SIL.ParatextBackTranslationHelperPlugin;

namespace SIL.SpellFixerPluginForParatext
{
    public class SpellFixerPlugin : IParatextStandalonePlugin
    {
        public const string PluginName = "Spell Fixer";
        public string Name => PluginName;
        public Version Version => new Version(1, 0);
        public string VersionString => Version.ToString();
        public string Publisher => "SIL";

        private static IPluginHost _host;
        private static SpellFixerPlugin _this;
        private static SpellFixerForm _mainWindow;

        public SpellFixerPlugin()
        {
            _this = this;

            PluginAssemblyResolver.Register(msg => _host?.Log(_this, msg));
        }

        public IEnumerable<PluginMenuEntry> PluginMenuEntries
        {
            get
            {
                yield return new PluginMenuEntry($"&{PluginName}...", Run, PluginMenuLocation.ScrTextTools);
            }
        }

        public IDataFileMerger GetMerger(IPluginHost host, string dataIdentifier) => throw new NotImplementedException();

        public string GetDescription(string locale)
        {
            return "Checks the spelling of the active project, word by word, using a SpellFixer (Consistent Spelling Checker or legacy SpellFixer) project, and fixes the words you approve.";
        }

        /// <summary>
        /// Called by Paratext when the menu item created for this plugin was clicked.
        /// </summary>
        private static void Run(IPluginHost host, IParatextChildState state)
        {
            Application.EnableVisualStyles();
            _host = host;

            var shortName = state.Project?.ShortName ?? host.ActiveWindowState?.Project?.ShortName;
            var project = host.GetAllProjects().FirstOrDefault(p => p.ShortName == shortName);
            if (project == null)
            {
                MessageBox.Show("Open a Paratext project window and then choose this command from its Tools menu.", PluginName);
                return;
            }

            // one window at a time: re-use it if it's for the same project
            if ((_mainWindow != null) && !_mainWindow.IsDisposed)
            {
                if (_mainWindow.ProjectShortName == project.ShortName)
                {
                    _mainWindow.Activate();
                    return;
                }
                _mainWindow.Close();
            }

            _host.Log(_this, "Starting " + PluginName);

            Action<IVerseRef> setSyncReference = verseReference => _host.SetReferenceForSyncGroup(verseReference, state.SyncReferenceGroup);

            _mainWindow = new SpellFixerForm(_host, _this, project, state.VerseRef, setSyncReference);
            _mainWindow.Show();
        }
    }
}
