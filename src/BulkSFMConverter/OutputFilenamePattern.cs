using System;
using System.IO;

// this file is also linked into the SILConvertersWordML project, so they both name batch output files the same way
namespace SilConvertersShared
{
    /// <summary>
    /// When converting a batch of files, this figures out how the user renamed one output file (e.g. saved it in
    /// a different folder, added a prefix/suffix, and/or replaced some part of the name) so that the rest of the
    /// files in the batch can be named the same way without asking the user for each one.
    /// </summary>
    public class OutputFilenamePattern
    {
        private enum PatternType
        {
            None,       // we don't know how the user changes the name (yet)
            Affixes,    // e.g. "Gen 1.docx" -> "pre Gen 1 suf.doc" (or the same name in a different folder)
            Replace     // e.g. "Gen 1 xnr (final).docx" -> "Gen 1 dgo AI draft.doc"
        }

        private enum ReplaceAnchor
        {
            Start,
            End,
            Middle
        }

        private PatternType _type = PatternType.None;
        private string _folder;
        private bool _useSourceFolder;
        private string _extn;

        // for PatternType.Affixes
        private string _prefix, _suffix;

        // for PatternType.Replace
        private string _oldText, _newText;
        private ReplaceAnchor _anchor;

        public bool IsLearned => _type != PatternType.None;

        /// <summary>
        /// Figure out (if possible) how the user changed the name of sourceFileSpec to get outputFileSpec
        /// </summary>
        public void Learn(string sourceFileSpec, string outputFileSpec)
        {
            _type = PatternType.None;
            if (String.Equals(Path.GetFullPath(sourceFileSpec), Path.GetFullPath(outputFileSpec), StringComparison.OrdinalIgnoreCase))
                return; // same file -- nothing to learn

            _folder = Path.GetDirectoryName(outputFileSpec);
            _extn = Path.GetExtension(outputFileSpec);

            // if the user saved it in the original file's folder (e.g. just changing the suffix), then save the
            //  others in *their* own folder as well
            _useSourceFolder = String.Equals(_folder, Path.GetDirectoryName(sourceFileSpec), StringComparison.OrdinalIgnoreCase);

            var strSourceTitle = Path.GetFileNameWithoutExtension(sourceFileSpec);
            var strNewTitle = Path.GetFileNameWithoutExtension(outputFileSpec);

            // first see if the original name is some portion of the new name (i.e. they added a prefix and/or suffix)
            int nIndexOfOrigName = strNewTitle.IndexOf(strSourceTitle, StringComparison.OrdinalIgnoreCase);
            if (nIndexOfOrigName != -1)
            {
                _prefix = strNewTitle.Substring(0, nIndexOfOrigName);
                _suffix = strNewTitle.Substring(nIndexOfOrigName + strSourceTitle.Length);
                _type = PatternType.Affixes;
                return;
            }

            // otherwise, see if they replaced some part of the original name, by finding what the beginning and
            //  end of the two names have in common (but only up to a word boundary, so we don't split words)
            int nMaxCommon = Math.Min(strSourceTitle.Length, strNewTitle.Length);
            int nCommonPrefix = 0;
            while ((nCommonPrefix < nMaxCommon) && (Char.ToLowerInvariant(strSourceTitle[nCommonPrefix]) == Char.ToLowerInvariant(strNewTitle[nCommonPrefix])))
                nCommonPrefix++;
            while ((nCommonPrefix > 0) && (IsMidWord(strSourceTitle, nCommonPrefix) || IsMidWord(strNewTitle, nCommonPrefix)))
                nCommonPrefix--;

            int nCommonSuffix = 0;
            while ((nCommonSuffix < nMaxCommon - nCommonPrefix)
                && (Char.ToLowerInvariant(strSourceTitle[strSourceTitle.Length - 1 - nCommonSuffix]) == Char.ToLowerInvariant(strNewTitle[strNewTitle.Length - 1 - nCommonSuffix])))
                nCommonSuffix++;
            while ((nCommonSuffix > 0) && (IsMidWord(strSourceTitle, strSourceTitle.Length - nCommonSuffix) || IsMidWord(strNewTitle, strNewTitle.Length - nCommonSuffix)))
                nCommonSuffix--;

            // if the names have nothing in common, then we can't tell how to change the next one
            if ((nCommonPrefix == 0) && (nCommonSuffix == 0))
                return;

            _oldText = strSourceTitle.Substring(nCommonPrefix, strSourceTitle.Length - nCommonPrefix - nCommonSuffix);
            _newText = strNewTitle.Substring(nCommonPrefix, strNewTitle.Length - nCommonPrefix - nCommonSuffix);

            // if nothing was replaced, it was just inserted in the middle somewhere, which we can't locate in
            //  another name reliably
            if (String.IsNullOrEmpty(_oldText))
                return;

            _anchor = (nCommonSuffix == 0) ? ReplaceAnchor.End
                    : (nCommonPrefix == 0) ? ReplaceAnchor.Start
                    : ReplaceAnchor.Middle;
            _type = PatternType.Replace;
        }

        /// <summary>
        /// Returns the output file spec for sourceFileSpec using the learned pattern, or null if there is no
        /// pattern or it doesn't apply to this file (e.g. it doesn't contain the text the user replaced)
        /// </summary>
        public string GetOutputFileSpec(string sourceFileSpec)
        {
            var strTitle = Path.GetFileNameWithoutExtension(sourceFileSpec);
            string strNewTitle = null;
            switch (_type)
            {
                case PatternType.Affixes:
                    strNewTitle = _prefix + strTitle + _suffix;
                    break;

                case PatternType.Replace:
                    int nIndex;
                    if (_anchor == ReplaceAnchor.End)
                        nIndex = strTitle.EndsWith(_oldText, StringComparison.OrdinalIgnoreCase) ? strTitle.Length - _oldText.Length : -1;
                    else if (_anchor == ReplaceAnchor.Start)
                        nIndex = strTitle.StartsWith(_oldText, StringComparison.OrdinalIgnoreCase) ? 0 : -1;
                    else
                        nIndex = strTitle.IndexOf(_oldText, StringComparison.OrdinalIgnoreCase);

                    if (nIndex != -1)
                        strNewTitle = strTitle.Substring(0, nIndex) + _newText + strTitle.Substring(nIndex + _oldText.Length);
                    break;
            }

            if (strNewTitle == null)
                return null;

            return Path.Combine(GetFolder(sourceFileSpec), strNewTitle + _extn);
        }

        /// <summary>
        /// Returns a name to suggest to the user when GetOutputFileSpec can't be used: the original name plus
        /// defaultSuffix, in the learned folder and with the learned extension (if any)
        /// </summary>
        public string GetDefaultFileSpec(string sourceFileSpec, string defaultSuffix, bool includeExtension = true)
        {
            var strFilename = Path.GetFileNameWithoutExtension(sourceFileSpec) + defaultSuffix;
            if (includeExtension)
                strFilename += (_extn ?? Path.GetExtension(sourceFileSpec));
            return Path.Combine(GetFolder(sourceFileSpec), strFilename);
        }

        // (the folder and extension are remembered even if we couldn't learn how the name was changed)
        private string GetFolder(string sourceFileSpec)
        {
            return ((_folder == null) || _useSourceFolder) ? Path.GetDirectoryName(sourceFileSpec) : _folder;
        }

        // true if the boundary just before index nIndex is between two letters/digits (i.e. in the middle of a word)
        private static bool IsMidWord(string str, int nIndex)
        {
            return (nIndex > 0) && (nIndex < str.Length)
                && Char.IsLetterOrDigit(str[nIndex - 1]) && Char.IsLetterOrDigit(str[nIndex]);
        }
    }
}
