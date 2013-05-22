using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace SILConvertersWordML
{
    public abstract class DataIterator
    {
        public abstract string CurrentValue { get; }
        public abstract void SetCurrentValue(string str);
        public abstract bool MoveNext();
        public abstract DataIterator Clone();
    }

    class LinqDataIterator : DataIterator
    {
        private List<XElement> _lstOfRuns;
        private List<XElement>.Enumerator _enumListOfRuns;

        public LinqDocument LinqDocument { get; set; }

        public List<XElement> ListOfRuns
        {
            get { return _lstOfRuns; } 
            set
            {
                _lstOfRuns = value;
                if (value != null)
                {
                    _enumListOfRuns = ListOfRuns.GetEnumerator();
                    _enumListOfRuns.MoveNext();
                }
            }
        }

        #region Overrides of DataIterator

        public override string CurrentValue
        {
            get { return LinqDocument.GetTextFromRun(_enumListOfRuns.Current); }
        }

        public override void SetCurrentValue(string str)
        {
            LinqDocument.SetTextOfRun(_enumListOfRuns.Current, str);
        }

        public override bool MoveNext()
        {
            return _enumListOfRuns.MoveNext();
        }

        public override DataIterator Clone()
        {
            // I think this is a don't care for XDoc... let's hold off for now
            return this;
        }

        #endregion
    }

    public class IteratorXPath : DataIterator
	{
		protected XPathNodeIterator m_ni = null;
		protected bool m_bConvertAsCharValue;

        public IteratorXPath(XPathNodeIterator ni, bool bConvertAsCharValue)
		{
			NodeIterator = ni;
			ConvertAsCharValue = bConvertAsCharValue;
		}

		protected XPathNodeIterator NodeIterator
		{
			get { return m_ni; }
			set { m_ni = value; }
		}

		public bool ConvertAsCharValue
		{
			get { return m_bConvertAsCharValue; }
			set { m_bConvertAsCharValue = value; }
		}

        public override DataIterator Clone()
		{
            return new IteratorXPath(NodeIterator.Clone(), ConvertAsCharValue);
		}

		public override string CurrentValue
		{
			get
			{
				string strValue;
				if (ConvertAsCharValue)
				{
					try
					{
						char ch = (char)Convert.ToInt32(NodeIterator.Current.Value, 16);
						strValue = ch.ToString();
					}
					catch(Exception ex)
					{
						throw new ApplicationException(String.Format("Can't convert inserted symbol value '{0}'. Contact silconverters_support@sil.org", NodeIterator.Current.Value), ex);
					}
				}
				else
					strValue = NodeIterator.Current.Value;
				return strValue;
			}
		}

		public override void SetCurrentValue(string str)
		{
			if (ConvertAsCharValue)
				str = String.Format("{0:X4}", (int)str[0]);
			NodeIterator.Current.SetValue(str);
		}

		public override bool MoveNext()
		{
			return NodeIterator.MoveNext();
		}

        public bool IsInsertSymbolSituation
        {
            get
            {
                return ((NodeIterator != null) && 
                        (NodeIterator.Current != null)) &&
                       ((NodeIterator.Current.Name == "w:char") ||
                        (NodeIterator.Current.Name == "wx:char"));
            }
        }
    }

	public class IteratorMap : Dictionary<string, DataIterator>
    {
        public new void Clear()
        {
            base.Clear();
        }
    }

    public abstract class MapIteratorList
    {
        public abstract bool IsInitializedCustomFontName { get; }
        public abstract bool IsInitializedFontsFromStyles { get; }
        public abstract bool IsInitializedStyleName { get; }
        public abstract void ResetMaps();
    }
}
