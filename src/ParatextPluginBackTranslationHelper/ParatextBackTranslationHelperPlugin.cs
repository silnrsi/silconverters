using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Paratext.PluginInterfaces;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public class ParatextBackTranslationHelperPlugin : IParatextStandalonePlugin
	{
		public const string PluginName = "Back Translation Helper";
		public const string emailAddress = "silconverters_support@sil.org";
		public string Name => PluginName;
		public Version Version => new(1, 0);
		public string VersionString => Version.ToString();
		public string Publisher => "SIL/UBS";

		private static IPluginHost _host;
		private static IParatextChildState _state;

#if UseWebForm    // can be used to switch to using a web-browser based display (if your OS doesn't support WinForm controls) -- not fully functional though...
		private static BackTranslationHelperWebForm _mainWindow;
#else
		private static BackTranslationHelperForm _mainWindow;
#endif
		private static ParatextBackTranslationHelperPlugin _this;
		private static IProject _projectNameParent;
		private static IProject _projectNameDaughter;

		public ParatextBackTranslationHelperPlugin()
        {
			_this = this;

			AppDomain.CurrentDomain.AssemblyResolve +=
			   CurrentDomain_AssemblyResolve;
		}

		private List<string> _assembliesToFindInPluginFolder = new List<string>
		{
			"SilEncConverters40.dll",
			"ECInterfaces.dll",
		};

		private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
			// Ignore missing resources
			if (!_assembliesToFindInPluginFolder.Any(s => args.Name.Contains(s)))
				return null;

			try
			{
				var pathToPluginFolder = Assembly.GetExecutingAssembly().Location;
				pathToPluginFolder = Path.Combine(Path.GetDirectoryName(pathToPluginFolder), "SilEncConverters40.dll");
				var asm = Assembly.LoadFrom(pathToPluginFolder);
				var types = asm.GetTypes();

				foreach (var type in types)
				{
					try
					{
						Activator.CreateInstance(type);
					}
					catch   // ignore errors
					{
					}
				}
			}
			catch (Exception ex)
			{
				var msg = $"Unable to load add-in assembly: {Path.GetFileNameWithoutExtension(args.Name)}: {ex.Message}";
				_host.Log(_this, msg);
			}

			return null;
		}

		public IEnumerable<PluginMenuEntry> PluginMenuEntries
		{
			get
			{
				var entry = new PluginMenuEntry($"&{PluginName}...", Run, PluginMenuLocation.ScrTextTools);
				entry.LocalizedTextNeeded += delegate (string defaultText, string locale)
				{
					switch (locale)
					{
						case "es": return "Hola mundo...";
						default: return defaultText;
					}
				};
				yield return entry;

				// This is an example of how a plugin could throw an exception. Paratext should catch it and alert the user but
				// not send in an exception report.
				//yield return new PluginMenuEntry("Throw exception!",
				//    (host, state) => throw new InvalidOperationException("Don't click that!"), PluginMenuLocation.ParatextAdvanced);
			}
		}

        public IDataFileMerger GetMerger(IPluginHost host, string dataIdentifier) => throw new NotImplementedException();

		public string GetDescription(string locale)
		{
			return "Calls off to Bing Translator, etc., to help with doing back-translation of the active project putting the results in the defined daughter translation project.";
		}

		/// <summary>
		/// Called by Paratext when the menu item created for this plugin was clicked.
		/// </summary>
		private static void Run(IPluginHost host, IParatextChildState state)
		{
			Application.EnableVisualStyles();
			_host = host;
			_state = state;

			_host.Log(_this, "Starting " + PluginName);

			var initialVerseReference = state.VerseRef;

			Action<IVerseRef> syncReferenceGroup = verseReference =>
			{
				_host.SetReferenceForSyncGroup(verseReference, state.SyncReferenceGroup);
			};

			var formToShow = _mainWindow = new BackTranslationHelperForm(_host, _this, syncReferenceGroup, initialVerseReference);
			formToShow.Show();
		}
	}
}
