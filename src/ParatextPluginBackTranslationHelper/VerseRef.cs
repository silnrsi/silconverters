using Paratext.PluginInterfaces;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    /// <summary>
    /// creating a verse ref class so we can serialize it for testing
    /// </summary>
    public class TestVerseReference : IVerseRef
    {
        public string BookCode { get; set; }

        public int BookNum { get; set; }

        public int ChapterNum { get; set; }

        public int VerseNum { get; set; }

        public int BBBCCCVVV { get; set; }

        public IVersification Versification { get; set; }

        public bool RepresentsMultipleVerses { get; set; }

        public IReadOnlyList<IVerseRef> AllVerses { get; set; }

        public static IVerseRef ToIVerseRef(ExpandoObject verseRef)
        {
            var map = (IDictionary<string, object>)verseRef;

            var result = new TestVerseReference
            {
                BookCode = (string)map["BookCode"],
                BookNum = (int)Convert.ToInt32(map["BookNum"]),
                ChapterNum = (int)Convert.ToInt32(map["ChapterNum"]),
                VerseNum = (int)Convert.ToInt32(map["VerseNum"]),
                BBBCCCVVV = (int)Convert.ToInt32(map["BBBCCCVVV"]),
                RepresentsMultipleVerses = (bool)map["RepresentsMultipleVerses"],
            };

            var listAllVerses = (List<object>)map["AllVerses"];
            var lst = listAllVerses.Select(vr => ToIVerseRef((ExpandoObject)vr)).ToList();
            if (!lst.Any())
                lst.Add(result);   // always should have at least itself
            result.AllVerses = lst;
            return result;
        }

        public override string ToString()
        {
            return $"{BookCode} {ChapterNum}:{VerseNum}";
        }

        public IVerseRef ChangeVersification(IVersification newVersification)
        {
            throw new System.NotImplementedException();
        }

        public int CompareTo(IVerseRef other)
        {
            throw new System.NotImplementedException();
        }

        public bool Equals(IVerseRef other)
        {
            throw new System.NotImplementedException();
        }

        public IVerseRef GetNextBook(IProject project)
        {
            throw new System.NotImplementedException();
        }

        public IVerseRef GetNextChapter(IProject project)
        {
            throw new System.NotImplementedException();
        }

        public IVerseRef GetNextVerse(IProject project)
        {
            throw new System.NotImplementedException();
        }

        public IVerseRef GetPreviousBook(IProject project)
        {
            throw new System.NotImplementedException();
        }

        public IVerseRef GetPreviousChapter(IProject project)
        {
            throw new System.NotImplementedException();
        }

        public IVerseRef GetPreviousVerse(IProject project)
        {
            throw new System.NotImplementedException();
        }
    }
}
