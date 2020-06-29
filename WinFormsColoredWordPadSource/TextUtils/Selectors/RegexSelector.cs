using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace TextUtils.Selectors
{
  public  class RegexSelector :  Selector
    {
        public String RegexString { get; set; }
        public String RegexMatch { get; set; }

        private RegexOptions mRegexOptions = RegexOptions.None;
        public RegexOptions RegexOptions {
            get { return RegexOptions; }
            set{mRegexOptions= value;}
        }

        public override TextIndexList SelectIndexes(string Text)
        {
            TextIndexList indexes = new TextIndexList();
            if (RegexString != "" || RegexString != null)
            {
                if (RegexMatch == "" || RegexMatch == null)
                {
                    try
                    {
                        Regex rx = new Regex(RegexString, mRegexOptions  );
                        MatchCollection mc = rx.Matches(Text);
                        foreach (Match m in mc)
                        {
                            if (m.Length > 0)
                            {
                                indexes.Add(new TextIndex(m.Index, m.Length));
                            }
                        }
                    }
                    catch (Exception ex){
                        Debug.WriteLine(ex.Message);
                    }
                }
                else
                {
                    try
                    {
                        Regex rx = new Regex(RegexString, RegexOptions);
                        MatchCollection mc = rx.Matches(Text);
                        foreach (Match m in mc)
                        {
                            if (m.Length > 0)
                            {
                                indexes.Add(new TextIndex(m.Groups[RegexMatch].Index, m.Groups[RegexMatch].Length));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
            return indexes;
        }

        public override TextIndex SelectIndex(string text)
        {
            TextIndex index = null;
            if (RegexString != "" || RegexString != null)
            {
                if (RegexMatch == "" || RegexMatch == null)
                {
                    try
                    {
                        Regex rx = new Regex(RegexString, mRegexOptions);
                        Match m = rx.Match(text);
                        if (m.Success)
                        {
                            index = new TextIndex(m.Index, m.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
                else
                {
                    try
                    {
                        Regex rx = new Regex(RegexString, mRegexOptions );
                        Match m = rx.Match(text);
                        if (m.Success)
                        {
                            if (m.Groups[RegexMatch] != null)
                            {
                                index = new TextIndex(m.Groups[RegexMatch].Index, m.Groups[RegexMatch].Length);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
            return index;
        }

        
    }
}
