using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils;
namespace WinFormsCodeBox.Decorations
{[Serializable]  
 public  class RegexMatchDecoration: Decoration 
    {
        


        private string mRegexString;
        private string mRegexMatch;
        private TextUtils.Selectors.RegexSelector mRxSel;



        /// <summary>
        /// The Regular expression used to evaluate the regex expressed as a string
        /// </summary>
     public String RegexString
        {
            get { return mRegexString; }
            set
            {
                mRegexString = value;
              
            }
        }


        /// <summary>
        /// The Name of the group that to be selected, the default group is "selected"
        /// </summary>
        public String RegexMatch
        {
            get { return mRegexMatch; }
            set { mRegexMatch = value ; }
        }



        public override TextIndexList Ranges(string text)
        {
            mRxSel = new TextUtils.Selectors.RegexSelector()
            {
                RegexString = mRegexString,
                RegexMatch = mRegexMatch
            };
            return mRxSel.SelectIndexes(text);

        }

    }
}
