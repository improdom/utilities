using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils;
using System.Text.RegularExpressions;

namespace WinFormsCodeBox.Decorations
{
    /// <summary>
    /// Decoration based on a single regular expression string
    /// </summary>
    [Serializable]
    public class RegexDecoration : Decoration
    {
       private string mRegexString;
       private TextUtils.Selectors.RegexSelector mRxSel;

       private RegexOptions mRegexOptions = RegexOptions.None;
       public RegexOptions RegexOptions
       {
           get { return mRegexOptions; }
           set
           {
               mRegexOptions = value;
               mSelectorDirty = true;
           }
       }


        
        /// <summary>
        /// The Regular expression used to evaluate the regex expressed as a string
        /// </summary>
        public String RegexString
        {
            get { return mRegexString; }
            set { mRegexString =  value;
                  mSelectorDirty = true;
            }
        }

        private bool mSelectorDirty = true;

        private void EnsureSelector()
        {
            if (mSelectorDirty)
            {
                mRxSel = new TextUtils.Selectors.RegexSelector()
                {
                    RegexString = mRegexString,
                    RegexOptions = mRegexOptions
                };
                mSelectorDirty = false;
            }
        }


        public override TextIndexList Ranges(string text)
        {
            EnsureSelector();
            return mRxSel.SelectIndexes(text);
           
        }

       
    }
}
