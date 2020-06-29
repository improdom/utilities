using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils.Selectors;
namespace WinFormsCodeBox.Decorations
{
    /// <summary>
    /// Decoration based on index positions of a list of strings
    /// </summary>
    [Serializable]
 public   class MultiStringDecoration:Decoration 
    {
        private List<string> mStrings = new List<string>();
        /// <summary>
        /// The list of strings to be searched for 
        /// </summary>
        public List<string> Strings
        {
            get { return mStrings; }
            set { mStrings = value; }
        }
        /// <summary>
        /// The System.StringComparison value to be used in searching 
        /// </summary>
        public StringComparison StringComparison { get; set; }

        public override TextUtils.TextIndexList Ranges(string text)
        {
            SubstringSelector ss = new SubstringSelector();
            ss.Strings = mStrings;

            ss.StringComparison = StringComparison;
            return ss.SelectIndexes(text);
            
        }
    }
}
