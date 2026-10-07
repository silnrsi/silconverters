using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    /// <summary>
    /// Hooks AppDomain.AssemblyResolve to load the EncConverters assemblies (also linked into the
    /// SpellFixerPluginForParatext project)
    /// </summary>
    public static class PluginAssemblyResolver
    {
        private static readonly List<string> _assembliesToFindInPluginFolder = new List<string>
        {
            "SilEncConverters40.dll",
            "ECInterfaces.dll",
        };

        private static Action<string> _log;
        private static bool _isRegistered;

        public static void Register(Action<string> log)
        {
            _log = log;
            if (_isRegistered)
                return;

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            _isRegistered = true;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
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
                _log?.Invoke(msg);
            }

            return null;
        }
    }
}
