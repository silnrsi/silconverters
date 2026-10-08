using SIL.SpellFixerPluginForParatext;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SIL.SpellingStatusUpdater
{
    /// <summary>
    /// Merges each project's pending SpellFixer fixes (local\SpellFixer\PendingSpellingStatus.xml) into its
    /// SpellingStatus.xml. Only run when Paratext isn't (since Paratext keeps its own copy in memory)
    /// </summary>
    public static class SpellingStatusUpdateRunner
    {
        public const string LogFileName = "SpellingStatusUpdater.log";

        public static List<string> ProcessProjectsDirectory(string projectsDirectory, DateTime now)
        {
            return Directory.EnumerateDirectories(projectsDirectory)
                            .Where(projectFolder => File.Exists(PendingSpellingStatusStore.PathFor(projectFolder)))
                            .Select(projectFolder => ProcessProject(projectFolder, now))
                            .Where(line => line != null)
                            .ToList();
        }

        public static string ProcessProject(string projectFolder, DateTime now)
        {
            var projectName = Path.GetFileName(projectFolder);
            var spellFixerFolder = PendingSpellingStatusStore.FolderFor(projectFolder);
            var pendingPath = PendingSpellingStatusStore.PathFor(projectFolder);
            string line;
            try
            {
                var fixes = PendingSpellingStatusStore.Load(pendingPath);
                if (!fixes.Any())
                {
                    PendingSpellingStatusStore.Save(pendingPath, fixes);  // i.e. delete it
                    return null;
                }

                var statusPath = Path.Combine(projectFolder, "SpellingStatus.xml");
                if (File.Exists(statusPath))
                    File.Copy(statusPath, Path.Combine(spellFixerFolder, $"SpellingStatus.xml.{now:yyyyMMddHHmmss}.bak"), true);

                var doc = SpellingStatusMerger.LoadOrCreate(statusPath);
                foreach (var fix in fixes)
                    SpellingStatusMerger.Apply(doc, fix.Bad, fix.Good);
                SpellingStatusMerger.Save(doc, statusPath);

                PendingSpellingStatusStore.Save(pendingPath, Enumerable.Empty<PendingFix>());
                line = $"{now:s} {projectName}: merged {fixes.Count} fix(es) into SpellingStatus.xml";
            }
            catch (Exception ex)
            {
                line = $"{now:s} {projectName}: ERROR (the fixes are still pending): {ex.Message}";
            }

            try
            {
                Directory.CreateDirectory(spellFixerFolder);
                File.AppendAllText(Path.Combine(spellFixerFolder, LogFileName), line + Environment.NewLine);
            }
            catch
            {
                // nowhere else to report it
            }

            return line;
        }
    }
}
