using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace SIL.SpellFixerPluginForParatext
{
    /// <summary>
    /// The word a check run is currently asking about, which CheckedWordHighlightPlugin highlights in the Scripture
    /// text windows of its project. (The plugin API can't select text in a text window, but it can annotate it.)
    /// </summary>
    internal static class CheckedWordHighlighter
    {
        public static string ProjectShortName { get; private set; }
        public static IVerseRef VerseReference { get; private set; }
        public static string Word { get; private set; }

        /// <summary>
        /// Raised with the short names of the projects whose text windows need to be redrawn
        /// </summary>
        public static event Action<ICollection<string>> Changed;

        /// <summary>
        /// Highlights (every whole word occurrence of) the word in the verse, replacing any previous highlight
        /// </summary>
        public static void Set(string projectShortName, IVerseRef verseReference, string word)
        {
            var affected = new HashSet<string> { projectShortName };
            if (ProjectShortName != null)
                affected.Add(ProjectShortName);

            ProjectShortName = projectShortName;
            VerseReference = verseReference;
            Word = word;
            Changed?.Invoke(affected);
        }

        public static void Clear()
        {
            if (ProjectShortName == null)
                return;

            var affected = new HashSet<string> { ProjectShortName };
            ProjectShortName = null;
            VerseReference = null;
            Word = null;
            Changed?.Invoke(affected);
        }
    }

    /// <summary>
    /// Adds "Highlight the word SpellFixer is checking" to the View menu of Scripture text windows. Once the user
    /// turns it on (Paratext remembers it), the word a check run is asking about is highlighted in the text.
    /// </summary>
    public class CheckedWordHighlightPlugin : IParatextScrTextAnnotationPlugin
    {
        public const string MenuText = "Highlight the word SpellFixer is checking";

        public string Name => $"{SpellFixerPlugin.PluginName} word highlighter";
        public string GetDescription(string locale) => $"Highlights the word that {SpellFixerPlugin.PluginName}'s check is asking about.";
        public Version Version => new Version(1, 0);
        public string VersionString => Version.ToString();
        public string Publisher => "SIL";

        public IEnumerable<PluginAnnotationMenuEntry> PluginAnnotationMenuEntries
        {
            get
            {
                yield return new PluginAnnotationMenuEntry(MenuText, project => new AnnotationSource(project?.ShortName));
            }
        }

        public IDataFileMerger GetMerger(IPluginHost host, string dataIdentifier) => throw new NotImplementedException();

        private sealed class AnnotationSource : IPluginAnnotationSource
        {
            private const string HighlightStyle = "spellfixer-checked-word";
            private readonly string _projectShortName;

            public AnnotationSource(string projectShortName)
            {
                _projectShortName = projectShortName;

                // Paratext doesn't tell us when the menu item is turned off again, but then it also doesn't ask for
                //  annotations, so (other than this subscription) it costs nothing
                CheckedWordHighlighter.Changed += affected =>
                {
                    if (affected.Contains(_projectShortName))   // only redraw the windows of the project being checked
                        AnnotationsChanged?.Invoke(this, EventArgs.Empty);
                };
            }

            public event EventHandler AnnotationsChanged;

            public bool MaintainSelectionsOnWordBoundaries => false;

            public IReadOnlyList<AnnotationStyle> GetStyleInfo(double zoom)
            {
                return new[] { new AnnotationStyle(HighlightStyle, "background-color: yellow; outline: 2px solid red;") };
            }

            public IReadOnlyList<IPluginAnnotation> GetAnnotations(IVerseRef verseRef, string verseUsfm)
            {
                var word = CheckedWordHighlighter.Word;
                var target = CheckedWordHighlighter.VerseReference;
                if (String.IsNullOrEmpty(word) || (target == null) || (verseRef == null) || (verseUsfm == null) ||
                    (_projectShortName != CheckedWordHighlighter.ProjectShortName) || (verseRef.BBBCCCVVV != target.BBBCCCVVV))
                    return new IPluginAnnotation[0];

                // every whole word occurrence (i.e. not part of a longer word)
                var regex = new Regex($@"(?<![\p{{L}}\p{{M}}\p{{N}}]){Regex.Escape(word)}(?![\p{{L}}\p{{M}}\p{{N}}])");
                return regex.Matches(verseUsfm).Cast<Match>()
                            .Select(m => (IPluginAnnotation)new Annotation(new TextSelection(m.Value, verseUsfm.Substring(0, m.Index),
                                                                                             verseUsfm.Substring(m.Index + m.Length), verseRef, m.Index)))
                            .ToList();
            }

            private sealed class Annotation : IPluginAnnotation
            {
                public Annotation(IScriptureTextSelection selection)
                {
                    ScriptureSelection = selection;
                }

                public IScriptureTextSelection ScriptureSelection { get; }
                public string StyleName => HighlightStyle;
                public string Icon => null;
                public string IconToolTipText => null;
                public bool Click(MouseButton button, bool onIcon, Point location) => false;
            }

            private sealed class TextSelection : IScriptureTextSelection
            {
                public TextSelection(string selectedText, string beforeContext, string afterContext, IVerseRef verseRef, int offset)
                {
                    SelectedText = selectedText;
                    BeforeContext = beforeContext;
                    AfterContext = afterContext;
                    VerseRefStart = VerseRefEnd = verseRef;
                    Offset = offset;
                }

                public IVerseRef VerseRefStart { get; }
                public IVerseRef VerseRefEnd { get; }
                public string SelectedText { get; }
                public int Offset { get; }
                public string BeforeContext { get; }
                public string AfterContext { get; }

                public bool Equals(ISelection other)
                {
                    return (other is IScriptureTextSelection s) && (s.VerseRefStart?.BBBCCCVVV == VerseRefStart?.BBBCCCVVV) &&
                           (s.Offset == Offset) && (s.SelectedText == SelectedText);
                }
            }
        }
    }
}
