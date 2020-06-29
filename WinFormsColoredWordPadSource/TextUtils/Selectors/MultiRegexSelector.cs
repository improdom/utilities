using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace TextUtils.Selectors
{
    /// <summary>
    /// Selects Text based on a List of regular expressions
    /// </summary>
  public class MultiRegexSelector:Selector 
    {

        private List<string> mRegexStrings = new List<string>();
        /// <summary>
        /// List of Strings defining the regular expressions
        /// </summary>
        public List<string> RegexStrings
        {
            get { return mRegexStrings; }
            set { mRegexStrings = value; }
        }

        

        public override TextIndexList SelectIndexes(string text)
        {
            TextIndexList indexes = new TextIndexList();
            foreach (string rString in mRegexStrings)
            {

                Regex rx = new Regex(rString);
                MatchCollection mc = rx.Matches(text);
                foreach (Match m in mc)
                {
                    indexes.Add(new TextIndex (m.Index, m.Length));
                }
            }
            return indexes;
        }
    }
}
