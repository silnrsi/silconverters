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
#if true
			Application.EnableVisualStyles();
			_host = host;
			_state = state;

			InitializeProjects(host, ref _projectNameParent, ref _projectNameDaughter);

			if ((_projectNameParent == null) || (_projectNameDaughter == null))
				throw new ApplicationException($"Source ('{_projectNameParent}') or Target ('{_projectNameDaughter}') project not selected. Can't continue!");

			_host.Log(_this, "Starting " + PluginName);

			var initialVerseReference = state.VerseRef;

			Action<IVerseRef> syncReferenceGroup = verseReference =>
			{
				_host.SetReferenceForSyncGroup(verseReference, state.SyncReferenceGroup);
			};

			var formToShow = _mainWindow = new BackTranslationHelperForm(_host, _this, syncReferenceGroup, initialVerseReference, 
											_projectNameParent, _projectNameDaughter, _projectNameParent.Language, _projectNameDaughter.Language);
			formToShow.Show();
#else
			lock (_this)
            {
                if (_host != null)
                {
                    // This should never happen, but just in case Host does something wrong...
                    host.Log(_this, "Run called more than once!");
                    return;
                }
            }

            try
            {
                Application.EnableVisualStyles();

                _host = host;
				_state = state;

				InitializeProjects(host, ref _projectNameParent, ref _projectNameDaughter);

                if ((_projectNameParent == null) || (_projectNameDaughter == null))
                    throw new ApplicationException($"Source ('{_projectNameParent}') or Target ('{_projectNameDaughter}') project not selected. Can't continue!");

#if DEBUG
                MessageBox.Show("Attach debugger now (if you want to)", pluginName);
#endif
                _host.Log(_this, "Starting " + pluginName);

                string preferredUiLocale = "en";
                try
                {
                    preferredUiLocale = _host.UserSettings.UiLocale;
                    if (String.IsNullOrWhiteSpace(preferredUiLocale))
                        preferredUiLocale = "en";
                }
                catch (Exception)
                {
                }

                // SetUpLocalization(preferredUiLocale);

                var initialVerseReference = state.VerseRef;

				Action<IVerseRef> syncReferenceGroup = verseReference =>
				{
					_host.SetReferenceForSyncGroup(verseReference, state.SyncReferenceGroup);
				};

				var mainUIThread = new Thread(() =>
                {
                    // InitializeErrorHandling();

#if UseWebForm  // can be used to switch to using a web-browser based display (if your OS doesn't support WinForm controls)
                    BackTranslationHelperWebForm formToShow;
#else
                    BackTranslationHelperForm formToShow;
#endif
                    lock (_this)
                    {
                        // KeyboardController.Initialize();

                        Action<bool> activateKeyboard = vern =>
                        {
                            if (vern)
                            {
                                try
                                {
                                    host.ActiveWindowState?.Project?.VernacularKeyboard?.Activate();
                                }
                                catch (ApplicationException e)
                                {
                                    // For some reason, the very first time this gets called it throws a COM exception, wrapped as
                                    // an ApplicationException. Mysteriously, it seems to work just fine anyway, and then all subsequent
                                    // calls work with no exception. Paratext seems to make this same call without any exceptions. The
                                    // documentation for ITfInputProcessorProfiles.ChangeCurrentLanguage (which is the method call
                                    // in SIL.Windows.Forms.Keyboarding.Windows that throws the COM exception says that an E_FAIL is an
                                    // unspecified error, so that's fairly helpful.
                                    if (!(e.InnerException is COMException))
                                        throw;
                                }
                            }
                            else
                                host.DefaultKeyboard?.Activate();
                        };

						//var fileAccessor = new ParatextDataFileAccessor(fileId => _host.GetPlugInData(_this, m_projectName, fileId),
						//	(fileId, reader) => _host.PutPlugInData(_this, m_projectName, fileId, reader),
						//	fileId => _host.GetPlugInDataLastModifiedTime(_this, m_projectName, fileId));

						//bool fEnableDragDrop = true;
						//try
						//{
						//	string dragDropSetting = _host.GetApplicationSetting("EnableDragAndDrop");
						//	if (dragDropSetting != null)
						//		fEnableDragDrop = bool.Parse(dragDropSetting);
						//}
						//catch (Exception)
						//{
						//}

						formToShow = _mainWindow =
#if UseWebForm  // can be used to switch to using a web-browser based display (if your OS doesn't support WinForm controls)
						new BackTranslationHelperWebForm
#else
                        new BackTranslationHelperForm
#endif
                        (_host, _this, state, syncReferenceGroup, initialVerseReference, _projectNameParent,
                            _projectNameDaughter, _projectNameParent.Language, _projectNameDaughter.Language);
                    }

#if DEBUG
                    // Always track if this is a debug build, but track to a different segment.io project
                    const bool allowTracking = true;
                    const string key = "0mtsix4obm";
#else
                    // If this is a release build, then allow an environment variable to be set to false
                    // so that testers aren't generating false analytics
                    string feedbackSetting = Environment.GetEnvironmentVariable("FEEDBACK");

                    var allowTracking = string.IsNullOrEmpty(feedbackSetting) || feedbackSetting.ToLower() == "yes" || feedbackSetting.ToLower() == "true";

                    const string key = "3iuv313n8t";
#endif
#if false
                    using (new Analytics(key, GetUserInfo(_host.UserInfo), allowTracking))
                    {
                        Analytics.Track("Startup", new Dictionary<string, string>
                        {{"Specific version", Assembly.GetExecutingAssembly().GetName().Version.ToString()}});

                        formToShow.ShowDialog();
                    }
#else
                    formToShow.ShowDialog();
#endif
                    _host.Log(_this, "Closing " + pluginName);
                    Environment.Exit(0);
                });
                mainUIThread.Name = pluginName;
                mainUIThread.IsBackground = false;
                mainUIThread.SetApartmentState(ApartmentState.STA);
                mainUIThread.Start();
                // Avoid putting any code after this line. Any exceptions thrown will not be able to be reported via the
                // "green screen" because we are not running in STA.
            }
            catch (Exception e)
			{
				MessageBox.Show(string.Format("General.ErrorStarting: Error occurred attempting to start {0}: ",
					"Param is \"ParatextBackTranslationHelper\" (plugin name)"), pluginName + e.Message);
				_host = null;	// so we can be called again, but not twice (i.e. two dialogs)
				// not sure why we'd want to throw...
				//  throw;
			}
#endif
		}

        private static void LoadEncConverterClassesFromLocalFolder(string asdg)
        {
            Assembly asm = null;
            Type[] types = null;
            string pathToPluginFolder = null;
            try
            {
                pathToPluginFolder = Assembly.GetExecutingAssembly().Location;
                pathToPluginFolder = Path.Combine(Path.GetDirectoryName(pathToPluginFolder), asdg);
                asm = Assembly.LoadFrom(pathToPluginFolder);
                types = asm.GetTypes();

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
                var msg = $"Unable to load add-in assembly: {Path.GetFileNameWithoutExtension(pathToPluginFolder)}: {ex.Message}";

				_host.Log(_this, msg);
            }
        }

        private static void InitializeProjects(IPluginHost host, ref IProject projectNameSource, ref IProject projectNameTarget)
        {
			var projects = host.GetAllProjects();
			var selectedProject = host.ActiveWindowState?.Project;

			// if the user selects the daughter/target project, let's assume that's the intended target from it's base project
			if ((selectedProject != null) && (selectedProject.BaseProject != null))
            {
				projectNameSource = projects.FirstOrDefault(p => p.ShortName == selectedProject.BaseProject.ShortName);
				projectNameTarget = projects.FirstOrDefault(p => p.ShortName == selectedProject.ShortName);
			}
			else
			{
				// otherwise, make them choose
				projectNameSource = QueryForProject("Source");
				projectNameTarget = QueryForProject("Target");
			}
		}

		private static IProject QueryForProject(string projectType)
        {
			var projects = _host.GetAllProjects();
			var dlg = new ProjectListForm(projects, projectType);
			if (dlg.ShowDialog() == DialogResult.OK)
            {
				return projects.FirstOrDefault(p => p.ShortName == dlg.SelectedDisplayName);
            }

			return null;
		}

		/*
		public static bool InvokeOnMainWindowIfNotNull(Action action)
		{
			lock (_this)
			{
				if (_mainWindow != null)
				{
					if (_mainWindow.InvokeRequired)
						_mainWindow.Invoke(action);
					else
						action();
					return true;
				}
			}
			return false;
		}
		*/

		//public void RequestShutdown()
		//{
		//	lock (this)
		//	{
		//		if (_mainWindow != null)
		//		{
		//			InvokeOnUiThread(delegate
		//			{
		//				_mainWindow.Activate();
		//				_mainWindow.Close();
		//			});
		//		}
		//		else
		//			Environment.Exit(0);
		//	}
		//}

		/*
		public void Activate(string activeProjectName)
		{
			if (_mainWindow != null)
			{
				lock (this)
				{
					InvokeOnUiThread(delegate { _mainWindow.Activate(); });
				}
			}
			else
			{
				// Can't lock because the whole start-up sequence takes several seconds and the
				// whole point of this code is to activate the splash screen so the user can see
				// it's still starting up. But there is no harm in calling Activate on the splash
				// screen if we happen to catch it between the time it is closed and the member
				// variable is set to null, since in that case, the "real" splash screen is closed
				// and Activate is a no-op. But we do need to use a temp variable because it could
				// get set to null between the time we check for null and the call to Activate.
				var _splashScreen = new SplashScreenForm();
				_splashScreen.Show();
				Application.DoEvents();
			}
		}

        private static UserInfo GetUserInfo(IUserInfo userInfo)
        {
            string lastName = userInfo.Name;
            string firstName = "";
            if (lastName != null)
            {
                var split = lastName.LastIndexOf(" ", StringComparison.Ordinal);
                if (split <= 0)
                    split = lastName.LastIndexOf("_", StringComparison.Ordinal);
                if (split > 0)
                {
                    firstName = lastName.Substring(0, split);
                    lastName = lastName.Substring(split + 1);
                }
            }

            return new UserInfo { FirstName = firstName, LastName = lastName, UILanguageCode = "en" };
        }

        private void InvokeOnUiThread(Action action)
		{
			lock (this)
			{
				if (_mainWindow.InvokeRequired)
					_mainWindow.Invoke(action);     // _mainWindow is the thread my dialog was created/run on
				else
					action();
			}
		}
		*/

		/*
		private static void InitializeErrorHandling()
		{
			ErrorReport.SetErrorReporter(new WinFormsErrorReporter());
			ErrorReport.EmailAddress = emailAddress;
			ErrorReport.AddStandardProperties();
			// The version that gets added to the report by default is for the entry assembly, which is
			// AddInProcess32.exe. Even if if reported a version (which it doesn't), it wouldn't be very
			// useful.
			ErrorReport.AddProperty("Plugin Name", pluginName);
			Assembly assembly = Assembly.GetExecutingAssembly();
			ErrorReport.AddProperty("Version", string.Format("{0} (apparent build date: {1})",
				assembly.GetName().Version,
				File.GetLastWriteTime(assembly.Location).ToShortDateString()));
			ErrorReport.AddProperty("Host Application", _host.ApplicationName + " " + _host.ApplicationVersion);
			ErrorReport.AddProperty("Source Project Name", _projectNameParent.ShortName);
			ErrorReport.AddProperty("Target Project Name", _projectNameDaughter.ShortName);
			ExceptionHandler.Init(new WinFormsExceptionHandler());
		}

		private static void SetUpLocalization(string desiredUiLangId)
		{
			var assembly = Assembly.GetExecutingAssembly();
			var attributes = assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
			var company = attributes.Length == 0 ? "SIL" : ((AssemblyCompanyAttribute)attributes[0]).Company;
			var installedStringFileFolder = FileLocationUtilities.GetDirectoryDistributedWithApplication("localization");
			var relativeSettingPathForLocalizationFolder = Path.Combine(company, pluginName);
			var version = assembly.GetName().Version.ToString();
			LocalizationManager.Create(TranslationMemory.XLiff, desiredUiLangId, pluginName, pluginName, version,
				installedStringFileFolder, relativeSettingPathForLocalizationFolder, new Icon(FileLocationUtilities.GetFileDistributedWithApplication("TXL no TXL.ico")), emailAddress,
				"SIL.Transcelerator", "SIL.Utils");
		}
		*/
	}
}
