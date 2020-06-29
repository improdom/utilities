using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace TextUtils.Selectors
{

 public   class WordsSelector:Selector 
    {
        private List<string> mWords = new List<string>();
        public List<string> Words
        {
            get { return mWords; }
            set { mWords = value; }
        }
        public bool IsCaseSensitive { get; set; }


        public override TextIndexList SelectIndexes(string text)
        {
            TextIndexList pairs = new TextIndexList();
            foreach (string word in mWords)
            {
                string rstring = @"(?i:\b" + word + @"\b)";
                if (IsCaseSensitive)
                {
                    rstring = @"\b" + word + @"\b)";
                }
                Regex rx = new Regex(rstring);
                MatchCollection mc = rx.Matches(text);
                foreach (Match m in mc)
                {
                    pairs.Add(new TextIndex(m.Index, m.Length));
                }
            }
            return pairs;
        }
    }
}
