using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils;
namespace WinFormsCodeBox.Decorations
{
    /// <summary>
    /// Decoration explicily specified list of as a starting positions and a lengths
    /// </summary>
    [Serializable]
    public class MultiExplicitDecoration : Decoration
    {
        private TextIndexList mColoredRanges = new TextIndexList();
        public TextIndexList ColoredRanges
        {
            get { return mColoredRanges; }
            set { mColoredRanges = value; }

        }


        public override TextIndexList Ranges(string Text)
        {
            return mColoredRanges;
        }

      
    }
}
