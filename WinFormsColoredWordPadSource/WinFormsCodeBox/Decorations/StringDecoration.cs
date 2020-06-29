using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils.Selectors;
namespace WinFormsCodeBox.Decorations
{
     

    /// <summary>
    ///Decoration based on index positions of a single string 
    /// </summary>
  
   [Serializable] 
   public  class StringDecoration:Decoration 
    {

       private string mString;
     /// <summary>
       /// The string to be searched for 
     /// </summary>
       public String String
        {
            get { return mString; }
            set {  mString = value ; }
        }

        public StringComparison StringComparison { get; set; }



        public override TextUtils.TextIndexList Ranges(string text)
        {
            SubstringSelector ss = new SubstringSelector();
            ss.Strings.Add( mString);

            ss.StringComparison = StringComparison;
            return ss.SelectIndexes(text);
        }
    }
}
