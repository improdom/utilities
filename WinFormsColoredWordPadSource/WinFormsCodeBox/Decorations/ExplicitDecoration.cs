using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils;

namespace WinFormsCodeBox.Decorations
{
    /// <summary>
    /// Decoration explicily specified as a starting position and a length
    /// </summary>
    [Serializable]
    public class ExplicitDecoration : Decoration
    {
        public int Start { get; set; }
        public int Length { get; set; }

        public override TextIndexList Ranges(string text)
        {
            TextIndexList ranges = new TextIndexList();

            TextIndex t = new TextIndex(Start, Length);
            ranges.Add(t);
            return ranges;
        }
 
    }
}
