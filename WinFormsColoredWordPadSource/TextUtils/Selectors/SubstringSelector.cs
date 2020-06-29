using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TextUtils.Selectors
{
  public class SubstringSelector: Selector 
    {
      private List<string> mStrings = new List<string>();


      /// <summary>
      /// The System.StringComparison value to be used in searching 
      /// </summary>
      public StringComparison StringComparison { get; set; }

      /// <summary>
      /// The list of strings to be searched for 
      /// </summary>
      public List<string> Strings
      {
          get { return mStrings; }
          set { mStrings = value; }
      }


        public override TextIndexList SelectIndexes(string text)
        {
            TextIndexList indexes  = new TextIndexList();
            foreach (string word in mStrings)
            {
                int index = text.IndexOf(word, 0, StringComparison);
                while (index != -1)
                {
                    indexes.Add(new TextIndex(index, word.Length));
                    index = text.IndexOf(word, index + word.Length, StringComparison);
                }
            }
            return indexes;
        }
    }
}
