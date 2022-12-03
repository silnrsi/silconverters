#define Csc30   // turn off CSC30 features

using System;
using System.Collections.Generic;
using System.Windows.Forms;             // for DialogResult
using Word = Microsoft.Office.Interop.Word;
/*
#if !Csc30
    using SpellingFixer30;
#else
using SpellingFixerEC;
#endif
*/
namespace SILConvertersOffice
{
    internal class WordDocument : OfficeTextDocument
    {
		public WordDocument(Word.Document doc, ProcessingType eType)
            : base(doc, eType)
        {
        }

        public Word.Document Document
        {
            get { return (Word.Document)m_baseDocument; }
        }

        public override int WordCount
        {
            get { return Document.Words.Count; }
        }

        public override string SelectedText
        {
            get { return Document.Application.Selection.Text; }
        }

        public override bool IsSelection
        {
            get
            {
                Word.Selection aSelection = Document.Application.Selection;
                return (aSelection.Start != aSelection.End);
            }
        }

        public override OfficeRange SelectionRange
        {
            get { return new WordRange(Document.Application.Selection.Range); }
        }

		public override bool ProcessWordByWord(OfficeDocumentProcessor aWordProcessor, ProcessingType eType)
        {
            if (eType == ProcessingType.eParagraphByParagraph)
            {
                if (aWordProcessor.AreLeftOvers && (aWordProcessor.LeftOvers.Start != aWordProcessor.LeftOvers.End))
                    return ProcessSelectionIgnoreFormatting(aWordProcessor, Document.Application.Selection);
                else
                    return ProcessParagraphsIgnoreFormatting(aWordProcessor, Document.Paragraphs);
            }
            else
            {
                if (aWordProcessor.AreLeftOvers)
                {
                    DialogResult res = MessageBox.Show("Click 'Yes' to restart where you left off, 'No' to start over at the top, and 'Cancel' to quit", OfficeApp.cstrCaption, MessageBoxButtons.YesNoCancel);
                    if (res == DialogResult.No)
                        aWordProcessor.LeftOvers = null;
                    if (res == DialogResult.Cancel)
                        return true;
                }

                if (eType == ProcessingType.eWordByWord)
                    return ProcessParagraphs(aWordProcessor, Document.Paragraphs);
                else if (eType == ProcessingType.eIsoFormattedRun)
                    return ProcessParagraphsIsoFormat(aWordProcessor, Document.Paragraphs);
                else
                    throw new ApplicationException($"Unknown ProcessingType: {eType}");
            }
        }

        protected bool ProcessSelectionIgnoreFormatting(OfficeDocumentProcessor aWordProcessor, Word.Selection selection)
        {
            // if multiple paragraphs...
            int nCharIndex = 0;
            WordParagraphs aParagraphRanges = new WordParagraphs(selection);
            foreach (Word.Range aRange in aParagraphRanges)
            {
                WordRange aThisParagraph = new WordRange(aRange);
                if (!ProcessWholeRange(aWordProcessor, aThisParagraph, nCharIndex))
                    return false;
            }
            return true;
        }

        protected bool ProcessParagraphsIgnoreFormatting(OfficeDocumentProcessor aWordProcessor, Word.Paragraphs aParagraphs)
        {
            foreach (Word.Paragraph aParagraph in aParagraphs)
            {
                // get the Range object for this paragraph
                Word.Range aParagraphRange = aParagraph.Range;

                // if we're picking up where we left off and we're not there yet...
                int nCharIndex = 0;
                if (aWordProcessor.AreLeftOvers)
                {
                    if (aWordProcessor.LeftOvers.Start > aParagraphRange.End)
                        continue;   // skip to the next paragraph

                    nCharIndex = aWordProcessor.LeftOvers.StartIndex;
                    aWordProcessor.LeftOvers = null; // turn off "left overs"
                }

#if BUILD_FOR_OFF12 || BUILD_FOR_OFF14 || BUILD_FOR_OFF15
                // if there are any ContentControls in this paragraph, we need to get rid of them or the
                // processor won't deal with them properly (they're going to go away anyway with this
                // approach of processing entire paragraphs in one go), so it shouldn't matter much).
                if (aParagraphRange.ContentControls.Count > 0)
                {
                    foreach (Word.ContentControl cc in aParagraphRange.ContentControls)
                        cc.Delete(DeleteContents: false);   // remove the control, but not the text
                }
#endif

                WordRange aThisParagraph = new WordRange(aParagraphRange);
                if (!ProcessWholeRange(aWordProcessor, aThisParagraph, nCharIndex))
                    return false;
            }

            return true;
        }
        protected bool ProcessParagraphs(OfficeDocumentProcessor aWordProcessor, Word.Paragraphs aParagraphs)
        {
            foreach (Word.Paragraph aParagraph in aParagraphs)
            {
                // get the Range object for this paragraph
                Word.Range aParagraphRange = aParagraph.Range;

                // if we're picking up where we left off and we're not there yet...
                int nCharIndex = 0;
                if (aWordProcessor.AreLeftOvers)
                {
                    if (aWordProcessor.LeftOvers.Start > aParagraphRange.End)
                        continue;   // skip to the next paragraph

                    nCharIndex = aWordProcessor.LeftOvers.StartIndex;
                    aWordProcessor.LeftOvers = null; // turn off "left overs"
                }

                WordRange aThisParagraph = new WordRange(aParagraphRange);
                if (!ProcessRangeAsWords(aWordProcessor, aThisParagraph, nCharIndex))
                    return false;
            }

            return true;
        }

        protected override OfficeRange Duplicate(OfficeRange aParagraphRange)
        {
            return new WordRange((WordRange)aParagraphRange);
        }

        public bool ProcessWholeParagraphs(OfficeDocumentProcessor aWordProcessor, Word.Paragraphs aParagraphs)
        {
            foreach (Word.Paragraph aParagraph in aParagraphs)
            {
                // get the Range object for this paragraph
                Word.Range aParagraphRange = aParagraph.Range;

                // if we're picking up where we left off and we're not there yet...
                int nCharIndex = 0;
                if (aWordProcessor.AreLeftOvers)
                {
                    if (aWordProcessor.LeftOvers.Start > aParagraphRange.End)
                        continue;   // skip to the next paragraph

                    nCharIndex = aWordProcessor.LeftOvers.StartIndex;
                    aWordProcessor.LeftOvers = null; // turn off "left overs"
                }

                WordRange aRunRange = new WordRange(aParagraphRange);
                int nEndIndex = --aRunRange.EndIndex;
                int nLastIndex = nCharIndex;

                // exclude the paragraph end and process it as a whole unit (we may have to do this multiple times
                bool bStop = false;
                while (!bStop && (nCharIndex < nEndIndex))
                {
                    aRunRange.StartIndex = nCharIndex;
                    if (aRunRange.EndIndex != nEndIndex)
                        aRunRange.EndIndex = nEndIndex;

                    nLastIndex = nCharIndex;

                    System.Diagnostics.Trace.WriteLine(String.Format("Start: {0}, End: {1}, text: {2}, length: {3}",
                        aRunRange.Start, aRunRange.End, aRunRange.Text, aRunRange.Text.Length));

                    if (!aWordProcessor.Process(aRunRange, ref nCharIndex))
                    {
                        aWordProcessor.LeftOvers = aRunRange;
                        return false;
                    }

                    if (nLastIndex == nCharIndex)
                        break;
                }
            }

            return true;
        }

        private List<Char> ParagraphEndings = new List<char> { '\n', '\r' };    // w/ text files, they have both

        public bool ProcessParagraphsIsoFormat(OfficeDocumentProcessor aWordProcessor, Word.Paragraphs aParagraphs)
		{
			foreach (Word.Paragraph aParagraph in aParagraphs)
			{
				// get the Range object for this paragraph
				Word.Range aParagraphRange = aParagraph.Range;

				// if we're picking up where we left off and we're not there yet...
				int nCharIndex = 0;
				if (aWordProcessor.AreLeftOvers)
				{
					if (aWordProcessor.LeftOvers.Start > aParagraphRange.End)
						continue;   // skip to the next paragraph

					nCharIndex = aWordProcessor.LeftOvers.StartIndex;
					aWordProcessor.LeftOvers = null; // turn off "left overs"
				}

				WordRange aRunRange = new WordRange(aParagraphRange);
				int nStartIndex = aRunRange.StartIndex;
				System.Diagnostics.Debug.Assert(nStartIndex == 0);

				// if we have mixed character formatting, then use a binary search algorithm to find
				//	the maximum run.
				if (MixedCharacterFormatting(aRunRange))
				{
					int nWidth = aRunRange.EndIndex / 2;
					
					do
					{
						aRunRange.EndIndex++;

					} while (!MixedCharacterFormatting(aRunRange));

                    TrimParagraphEnding(aRunRange); // back up one
                }
				else
                {
                    // the whole paragraph seems to be iso formatted, so exclude the paragraph end and
                    // process it as a whole unit
                    TrimParagraphEnding(aRunRange);

                    if (!aWordProcessor.Process(aRunRange, ref nCharIndex))
                    {
                        aWordProcessor.LeftOvers = aRunRange;
                        return false;
                    }
                }
            }

			return true;
		}

        private void TrimParagraphEnding(WordRange aRunRange)
        {
            // w/ text files, they have both
            do
            {
                aRunRange.EndIndex--;
            }
            while ((aRunRange.Text != null) && (aRunRange.Text.Length >= aRunRange.EndIndex) && ParagraphEndings.Contains(aRunRange.Text[aRunRange.EndIndex - 1]));
        }

        protected bool MixedCharacterFormatting(OfficeRange aRange)
		{
            Word.Range thisRange = ((WordRange)aRange).RangeBasedOn;
            /*
            int nTextLen = aRange.Text.Length - 1;
            char chRhs = aRange.Text.Substring(nTextLen - 1, 1);
            if (chRhs == chCellBreak)
            {
            }
            Word.Font aFont = thisRange.Font;
            if	(	(aFont.Color == 9999999)
                ||  (aFont.ColorIndex == 9999999)
                ||  (aFont.Size == 9999999)
                ||  (aFont.Bold == 9999999)
                ||  (aFont.Italic == 9999999)
                ||  (aFont.Superscript == 9999999)
                ||  (aFont.Spacing == 9999999)
                ||  (aFont.Kerning == 9999999)
                ||  (aFont.Position == 9999999)
                ||  (aFont.Underline == 9999999)
                ||  (aFont.StrikeThrough == 9999999)
                ||  (aFont.Shadow == 9999999)
                ||  (aFont.Hidden == 9999999)
                ||  (aFont.SmallCaps == 9999999)
                ||  (aFont.AllCaps == 9999999)
                ||  (aFont.Outline == 9999999))
            {
                return true;
            }
            */

            return false;
		}
        /*
        protected bool ProcessRange(OfficeDocumentProcessor aWordProcessor, Word.Range aParagraphRange, int nCharIndex)
        {
            string strText = aParagraphRange.Text;
            int nLength = (strText != null) ? strText.Length : 0;
            
            while ((nCharIndex < nLength) && (strText.IndexOfAny(m_achParagraphTerminators, nCharIndex, 1) == -1))
            {
                // get a copy of the range to work with
                WordRange aWordRange = new WordRange(aParagraphRange);

                // skip past initial spaces
                while ((nCharIndex < nLength) && (strText.IndexOfAny(m_achWhiteSpace, nCharIndex, 1) != -1))
                    nCharIndex++;

                // make sure we haven't hit the end of the paragraph (i.e. '\r') or range (beyond len)
                if (nCharIndex < nLength)
                {
                    if (strText.IndexOfAny(m_achParagraphTerminators, nCharIndex, 1) == -1)
                    {
                        // set the start index
                        aWordRange.StartIndex = nCharIndex;

                        // run through the characters in the word (i.e. until, space, NL, etc)
                        while ((nCharIndex < nLength) && (strText.IndexOfAny(m_achWordTerminators, nCharIndex, 1) == -1))
                            nCharIndex++;

                        // set the end of the range after the first space after the word
                        aWordRange.EndIndex = ++nCharIndex;

                        // make sure the word has text (sometimes it doesn't)
                        if (aWordRange.Text == null)  // e.g. Figure "1" returns a null Text string
                            // if it does, see if it's "First Character" has any text (which it does in this case)
                            aWordRange.Reset(aWordRange.Characters.First);

                        // finally check it.
                        if (!aWordProcessor.Process(aWordRange, ref nCharIndex))
                        {
                            aWordProcessor.LeftOvers = aWordRange;
                            return false;
                        }
                    }

                    strText = aParagraphRange.Text; // incase of replace, we've changed it.
                    nLength = (strText != null) ? strText.Length : 0;
                }
                else
                    break;
            }

            return true;
        }
    */
    }

    internal class WordSelectionDocument : WordDocument
    {
        public WordSelectionDocument(Word.Document doc, OfficeTextDocument.ProcessingType eType)
            : base(doc, eType)
        {
        }

        public override bool ProcessWordByWord(OfficeDocumentProcessor aWordProcessor)
        {
            // if multiple paragraphs...
            int nCharIndex = 0;
            WordParagraphs aParagraphRanges = new WordParagraphs(Document.Application.Selection);
            foreach (Word.Range aRange in aParagraphRanges)
            {
                WordRange aThisParagraph = new WordRange(aRange);
                if (!ProcessRangeAsWords(aWordProcessor, aThisParagraph, nCharIndex))
                    return false;
            }
            return true;
        }
    }
}
