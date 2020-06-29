using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils.Selectors;

namespace WinFormsCodeBox.Decorations
{
    /// <summary>
    /// Decoration based on a list of strings sandwiched between word boundaries
    /// </summary>
    [Serializable]
 public   class MultiRegexWordDecoration:Decoration 
    {

        private List<string> mWords = new List<string>();
        public List<string> Words
        {
            get { return mWords; }
            set { mWords = value; }
        }
        public bool IsCaseSensitive { get; set; }



        public override TextUtils.TextIndexList Ranges(string text)
        {
            WordsSelector ws = new WordsSelector()
            {
                IsCaseSensitive = IsCaseSensitive,
                Words = mWords
            };
            return ws.SelectIndexes(text);
        }
    }
}
