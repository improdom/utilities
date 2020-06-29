using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils.Selectors;
namespace WinFormsCodeBox.Decorations
{
    /// <summary>
    /// Decoration of Text Between Double Quotes
    /// </summary>
    [Serializable]
 public    class DoubleQuotedDecoration:Decoration 
    {
     static string rString  =  "\".*?\"" ;

     
        public override TextUtils.TextIndexList Ranges(string text)
        {
            RegexSelector rs = new RegexSelector() { RegexString = rString };
            return rs.SelectIndexes(text);
        }
    }
}
