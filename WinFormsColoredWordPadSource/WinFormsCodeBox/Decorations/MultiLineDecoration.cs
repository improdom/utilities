using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils.Selectors;
namespace WinFormsCodeBox.Decorations
{
    /// <summary>
    /// Decoration of a List of Specified lines of text
    /// </summary>
 public  class MultiLineDecoration:Decoration 
    {
     private List<int> mLines = new List<int>();
     public List<int> Lines
     {
         get { return mLines; }
         set { mLines = value; }
     }
        public override TextUtils.TextIndexList Ranges(string text)
        {

            LineSelector ls = new LineSelector();
            ls.Lines = mLines;
            return ls.SelectIndexes(text);
        }
    }
}
