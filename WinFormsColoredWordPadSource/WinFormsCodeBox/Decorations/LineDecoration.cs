using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils.Selectors;

namespace WinFormsCodeBox.Decorations
{
    /// <summary>
    /// Decoration of a Specified line of text
    /// </summary>
 public   class LineDecoration:Decoration 
    {

        public int Line { get; set; }

        public override TextUtils.TextIndexList Ranges(string text)
        {
            LineSelector ls = new LineSelector();
            ls.Lines.Add(Line);
            return ls.SelectIndexes(text);
        }
    }
}
