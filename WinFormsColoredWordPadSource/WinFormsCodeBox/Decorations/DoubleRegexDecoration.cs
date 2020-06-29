using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils;
using TextUtils.Selectors;
namespace WinFormsCodeBox.Decorations
{
    [Serializable]
 public   class DoubleRegexDecoration :Decoration 
    {
        public override  TextIndexList Ranges(string text)
        {
            DoubleRegexSelector drs = new DoubleRegexSelector()
            {
                OuterRegexString = OuterRegexString,
                InnerRegexString = InnerRegexString
            };
            return drs.SelectIndexes(text);
            
        }
   

       /// <summary>
       /// The Outer Regular expression used to evaluate the regex expressed as a string
       /// </summary>
       public String OuterRegexString{get;set;}
       


       /// <summary>
       /// The Inner Regular expression used to evaluate the regex expressed as a string
       /// </summary>
       public String InnerRegexString{get;set;}
      

      
   }
}

 
