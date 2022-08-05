using System.Collections.Generic;

namespace BackTranslationHelper
{
    public class BackTranslationHelperModel
    {
        public string SourceData { get; set; }

        public string TargetDataExisting { get; set; }
        
        public bool TargetDataExistingEnabled { get; set; }

        public List<TargetPossible> TargetsPossible { get; set; }

        public string TargetDataEditable { get; set; }

        public bool SaveTargetButtonEnabled { get; set; }

        public bool MoveNextButtonEnabled { get; set; }
    }

    public class TargetPossible
    {
        public int PossibleIndex { get; set; }

        public string TranslatorName { get; set; }

        public string TargetData { get; set; }

        public bool FillButtonEnabled { get; set; }
    }
}
