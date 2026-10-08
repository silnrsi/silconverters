using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// Merges SpellFixer fixes into a Paratext project's SpellingStatus.xml (also linked into SpellingStatusUpdater.exe)
    ///   <SpellingStatus>
    ///     <Status Word="the" State="R" />
    ///     <Status Word="teh" State="W"><Correction>the</Correction></Status>
    ///   </SpellingStatus>
    /// </summary>
    public static class SpellingStatusMerger
    {
        private const string RootName = "SpellingStatus";
        private const string StatusName = "Status";
        private const string CorrectionName = "Correction";

        public static XDocument LoadOrCreate(string path)
        {
            var doc = File.Exists(path)
                        ? XDocument.Load(path)
                        : new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(RootName));
            if (doc.Root == null)
                doc.Add(new XElement(RootName));
            return doc;
        }

        /// <summary>
        /// 'bad' becomes a wrong word with 'good' as its correction, and 'good' becomes a right word
        /// </summary>
        public static void Apply(XDocument doc, string bad, string good)
        {
            SetStatus(doc.Root, bad, "W", good);
            SetStatus(doc.Root, good, "R", null);
        }

        private static void SetStatus(XElement root, string word, string state, string correction)
        {
            var status = root.Elements(StatusName).FirstOrDefault(e => String.Equals((string)e.Attribute("Word"), word, StringComparison.Ordinal));
            if (status == null)
            {
                status = new XElement(StatusName, new XAttribute("Word", word));
                root.Add(status);
            }

            status.SetAttributeValue("State", state);
            status.Elements(CorrectionName).Remove();
            if (correction != null)
                status.Add(new XElement(CorrectionName, correction));
        }

        /// <summary>
        /// Saves the way Paratext does (UTF-8 w/ BOM, 2-space indent, CRLF), via a temp file
        /// </summary>
        public static void Save(XDocument doc, string path)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(true),
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace,
            };

            AtomicFile.Write(path, tempPath =>
            {
                using (var writer = XmlWriter.Create(tempPath, settings))
                    doc.Save(writer);
            });
        }
    }

    /// <summary>
    /// Writes a file by writing a temp file and then replacing the original with it
    /// </summary>
    public static class AtomicFile
    {
        public static void Write(string path, Action<string> writeTempFile)
        {
            var folder = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var tempPath = path + ".tmp";
            writeTempFile(tempPath);
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
    }
}
