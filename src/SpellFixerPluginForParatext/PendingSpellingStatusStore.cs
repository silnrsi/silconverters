using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SIL.SpellFixerPluginForParatext
{
    public class PendingFix
    {
        public string Bad { get; set; }
        public string Good { get; set; }
        public DateTime WhenUtc { get; set; }
    }

    /// <summary>
    /// The fixes made in the SpellFixer plugin that still need to go into the project's SpellingStatus.xml (after
    /// Paratext exits). Kept in &lt;projectFolder&gt;\local\SpellFixer (which isn't Send/Received). Also linked into
    /// SpellingStatusUpdater.exe
    ///   <PendingSpellingStatus>
    ///     <Fix Bad="teh" Good="the" When="2026-10-08T14:03:00Z" />
    ///   </PendingSpellingStatus>
    /// </summary>
    public static class PendingSpellingStatusStore
    {
        public const string FileName = "PendingSpellingStatus.xml";
        public const string SubFolder = @"local\SpellFixer";

        public static string FolderFor(string projectFolder) => Path.Combine(projectFolder, SubFolder);

        public static string PathFor(string projectFolder) => Path.Combine(FolderFor(projectFolder), FileName);

        public static List<PendingFix> Load(string path)
        {
            if (!File.Exists(path))
                return new List<PendingFix>();

            return XDocument.Load(path).Root?
                            .Elements("Fix")
                            .Select(e => new PendingFix
                            {
                                Bad = (string)e.Attribute("Bad"),
                                Good = (string)e.Attribute("Good"),
                                WhenUtc = DateTime.Parse((string)e.Attribute("When") ?? "2000-01-01T00:00:00Z", CultureInfo.InvariantCulture,
                                                         DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
                            })
                            .Where(f => !String.IsNullOrEmpty(f.Bad) && (f.Good != null))
                            .ToList()
                   ?? new List<PendingFix>();
        }

        public static void Append(string path, string bad, string good, DateTime whenUtc)
        {
            var fixes = Load(path);
            fixes.Add(new PendingFix { Bad = bad, Good = good, WhenUtc = whenUtc });
            Save(path, fixes);
        }

        public static void Save(string path, IEnumerable<PendingFix> fixes)
        {
            var list = fixes.ToList();
            if (!list.Any())
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            var doc = new XDocument(new XElement("PendingSpellingStatus",
                list.Select(f => new XElement("Fix",
                                              new XAttribute("Bad", f.Bad),
                                              new XAttribute("Good", f.Good),
                                              new XAttribute("When", f.WhenUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))))));
            AtomicFile.Write(path, tempPath => doc.Save(tempPath));
        }
    }
}
