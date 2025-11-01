using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpellingFixer30
{
    internal class Bad2GoodMap : Dictionary<string,string>
    {
        protected DateTime m_dtLastRead = DateTime.MinValue;
        private string m_strFileSpec = null;
        public bool DoingLoadOrSave = false;

        public Bad2GoodMap(string strPath)
        {
            m_strFileSpec = strPath;
        }

        public new void Add(string strBadWord, string strGoodWord)
        {
            CheckForOutOfDate();
            base.Add(strBadWord, strGoodWord);
        }

        public new string this[string key]
        {
            get
            {
                CheckForOutOfDate();
                return (string)base[key];
            }
        }

        public new int Count
        {
            get
            {
                CheckForOutOfDate();
                return base.Count;
            }
        }

        public new bool ContainsValue(string value)
        {
            CheckForOutOfDate();
            return base.ContainsValue(value);
        }

        public new Enumerator GetEnumerator()
        {
            // this might be out of date, for example, during Save after we write the header
            //  CheckForOutOfDate();
            return base.GetEnumerator();
        }

        public new bool ContainsKey(string key)
        {
            // CheckForOutOfDate();
            return base.ContainsKey(key);
        }

        public void CheckForOutOfDate()
        {
            var dtLastModified = DateTime.MinValue;
            var fileExists = CscProject.DoesFileExist(m_strFileSpec, ref dtLastModified);

            if (DoingLoadOrSave)
            {
                // if we're in the middle of a load or save, don't try to reload (but the 
                //  timestamp shouldn't be out of date or we haven't sequenced things properly)
                // read: if you're going to save, then make sure to call this method before 
                //  you set m_bDoingLoadOrSave to true.
                System.Diagnostics.Debug.Assert(!fileExists || (dtLastModified == m_dtLastRead));
                return;
            }

            // if the file has changed since we last read it, reload it (note the "!=" comparison,
            //  not ">", in case the file got replaced with an older copy, since we back them up)
            if (fileExists && (dtLastModified != m_dtLastRead))
            {
                base.Clear();
                LoadTable();
            }
        }

        public static Bad2GoodMap LoadTable(string strPath)
        {
            Bad2GoodMap map = new Bad2GoodMap(strPath);
            map.LoadTable();
            return map;
        }

        protected void LoadTable()
        {
            try
            {
                DoingLoadOrSave = true;

                // get a stream writer for this encoding and append
                if (CscProject.DoesFileExist(m_strFileSpec, ref m_dtLastRead))
                {
                    StreamReader sr = SpellingFixer.InitReaderPastHeader(m_strFileSpec, Encoding.UTF8);

                    string line = null;
                    while ((line = sr.ReadLine()) != null)
                    {
                        int nLhsLeftIdx = line.IndexOf('\"', 0) + 1;
                        int nLhsRightIdx = line.IndexOf('\"', nLhsLeftIdx);
                        System.Diagnostics.Debug.Assert((nLhsLeftIdx != -1) && (nLhsRightIdx != -1) && ((nLhsRightIdx - nLhsLeftIdx) < line.Length));
                        if ((nLhsLeftIdx != -1) && (nLhsRightIdx != -1) && ((nLhsRightIdx - nLhsLeftIdx) < line.Length))
                        {
                            string strLhs = line.Substring(nLhsLeftIdx, nLhsRightIdx - nLhsLeftIdx);

                            int nRhsLeftIdx = line.IndexOf('\"', nLhsRightIdx + 1) + 1;
                            int nRhsRightIdx = line.IndexOf('\"', nRhsLeftIdx);
                            System.Diagnostics.Debug.Assert((nRhsLeftIdx != -1) && (nRhsRightIdx != -1) && ((nRhsRightIdx - nRhsLeftIdx) < line.Length));

                            if ((nRhsLeftIdx != -1) && (nRhsRightIdx != -1) && ((nRhsRightIdx - nRhsLeftIdx) < line.Length))
                            {
                                string strRhs = line.Substring(nRhsLeftIdx, nRhsRightIdx - nRhsLeftIdx);
                                base.Add(strLhs, strRhs);
                            }
                        }
                    }
                    sr.Close();
                }
            }
            finally
            {
                DoingLoadOrSave = false;
            }
        }

        public static void SaveTable(Bad2GoodMap map, string strFileSpec, string strEncConverterName, string strPunctuationAndWhiteSpace, string strCustomCode)
        {
            // always make a backup
            CscProject.BackupFile(strFileSpec);
            map.SaveTable(strFileSpec, strEncConverterName, strPunctuationAndWhiteSpace, strCustomCode);
        }

        protected void SaveTable(string strFileSpec, string strEncConverterName, string strPunctuationAndWhiteSpace, string strCustomCode)
        {
            try
            {
                CheckForOutOfDate();    // in case something else changed it, reload it before we change it.

                DoingLoadOrSave = true;

                // get a stream writer for this encoding and append
                StreamWriter sw = new StreamWriter(strFileSpec, false, Encoding.UTF8);
                LoginSF.CreateCCTable(sw, strEncConverterName, strPunctuationAndWhiteSpace, strCustomCode, true);

                // order them in the file first by those with a hyphen in alphabetical order,
                //  followed by those without hyphens in alphabetical order. (so that a non-hypthenated
                //  word that is a substring of a hyphenated word doesn't get matched first).
                foreach (KeyValuePair<string, string> kvp in this.OrderBy(kvp => !kvp.Key.Contains("-"))
                                                                 .ThenBy(kvp => kvp.Key))
                {
                    // always surround the word with delimiters, because in this application
                    //  it's always full word form searching
                    string strOrigWord = kvp.Key;
                    string strBadSpelling = String.Format("#{0}#", strOrigWord);
                    string strReplacement = kvp.Value;
                    sw.WriteLine(SpellingFixer.FormatSubstitutionRule(strBadSpelling, strReplacement, "#", strOrigWord));
                }
                sw.Flush();
                sw.Close();

                m_strFileSpec = strFileSpec;
                CscProject.DoesFileExist(m_strFileSpec, ref m_dtLastRead);
            }
            finally
            {
                DoingLoadOrSave = false;
            }
        }
    }
}