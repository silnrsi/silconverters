using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Records the spelling fixes made in the plugin in the project's pending file, so SpellingStatusUpdater.exe
    /// can put them into SpellingStatus.xml after Paratext exits (there's no plugin API to change spelling status)
    /// </summary>
    internal static class SpellingStatusRecorder
    {
        private static readonly HashSet<string> _warningsShown = new HashSet<string>();

        public static bool HasRecordedThisSession { get; private set; }

        public static void Record(IProject project, IPluginObject plugin, string bad, string good, Action<string> warnUser)
        {
            try
            {
                if (!project.CanEdit(plugin, DataType.SpellingStatus))
                {
                    WarnOnce(project, "permission", $"You don't have permission to change the spelling status of the {project.ShortName} project, so the fixes made here won't be added to Paratext's spelling list.", warnUser);
                    return;
                }

                var projectFolder = ParatextProjectFolder.Find(ParatextProjectFolder.GetProjectsDirectory(), project.ShortName, project.ID);
                if (projectFolder == null)
                {
                    WarnOnce(project, "folder", $"Couldn't find the folder of the {project.ShortName} project, so the fixes made here won't be added to Paratext's spelling list.", warnUser);
                    return;
                }

                PendingSpellingStatusStore.Append(PendingSpellingStatusStore.PathFor(projectFolder), bad, good, DateTime.UtcNow);
                HasRecordedThisSession = true;
            }
            catch (Exception ex)
            {
                WarnOnce(project, "error", $"Unable to record the fix for Paratext's spelling list: {ex.Message}", warnUser);
            }
        }

        private static void WarnOnce(IProject project, string kind, string message, Action<string> warnUser)
        {
            if (_warningsShown.Add($"{project.ShortName}|{kind}"))
                warnUser?.Invoke(message);
        }
    }
}
