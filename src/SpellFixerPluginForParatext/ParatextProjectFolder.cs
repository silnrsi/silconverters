using Microsoft.Win32;
using System;
using System.IO;
using System.Xml.Linq;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Finds a Paratext project's folder (e.g. C:\My Paratext 9 Projects\Dog), which the plugin API doesn't expose
    /// </summary>
    public static class ParatextProjectFolder
    {
        private static readonly string[] SettingsKeys = { @"SOFTWARE\WOW6432Node\Paratext\8", @"SOFTWARE\Paratext\8" };

        public static string GetProjectsDirectory()
        {
            foreach (var keyName in SettingsKeys)
            {
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyName))
                    {
                        if ((key?.GetValue("Settings_Directory") is string directory) && Directory.Exists(directory))
                            return directory;
                    }
                }
                catch
                {
                    // try the next one
                }
            }
            return null;
        }

        /// <summary>
        /// Returns projectsDirectory\shortName if it has a Settings.xml whose Guid (if any) is the project's ID
        /// </summary>
        public static string Find(string projectsDirectory, string shortName, string projectId)
        {
            if (String.IsNullOrEmpty(projectsDirectory) || String.IsNullOrEmpty(shortName))
                return null;

            var folder = Path.Combine(projectsDirectory, shortName);
            var settingsPath = Path.Combine(folder, "Settings.xml");
            if (!File.Exists(settingsPath))
                return null;

            try
            {
                var guid = XDocument.Load(settingsPath).Root?.Element("Guid")?.Value?.Trim();
                if (!String.IsNullOrEmpty(guid) && !String.Equals(guid, projectId?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return null;
            }
            catch
            {
                return null;
            }

            return folder;
        }
    }
}
