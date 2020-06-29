using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace TextUtils.Selectors
{
  public  class DoubleRegexSelector:Selector 
    {
        public override TextIndexList SelectIndexes(string text)
        {
            TextIndexList indexes = new TextIndexList();
            if (OuterRegexString != "" && InnerRegexString != "")
            {
                try
                {
                    Regex orx = new Regex(OuterRegexString);
                    Regex irx = new Regex(InnerRegexString);

                    MatchCollection omc = orx.Matches(text);
                    foreach (Match om in omc)
                    {
                        if (om.Length > 0)
                        {
                            MatchCollection imc = irx.Matches(om.Value);
                            foreach (Match im in imc)
                            {
                                if (im.Length > 0)
                                {
                                    indexes.Add(new TextIndex(om.Index + im.Index, im.Length));
                                }

                            }
                        }
                    }
                }
                catch { }
            }

            return indexes;
        }

       /// <summary>
       /// The Outer Regular expression used to evaluate the regex expressed as a string
       /// </summary>
       public String OuterRegexString {get;set;}


       /// <summary>
       /// The Inner Regular expression used to evaluate the regex expressed as a string
       /// </summary>
       public String InnerRegexString {get ;set;}
      
      
   }
}
