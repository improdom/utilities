using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextUtils;
using System.Diagnostics;
namespace WinFormsCodeBox
{
    public class TextDelta
    {
        private int mStart;
        private int mDeltaLength;
        private int mLength;
        private string mDeltaText ="";

        public TextDelta(string previousText, string currentText)
        {
            if (previousText == currentText)
            {
                DeltaType = EDeltaType.Same;
                return;
            }
            if (previousText == "")
            {
                DeltaType = EDeltaType.Insert;
                mStart = 0;
                mDeltaLength = currentText.Length;
                mDeltaText = currentText;
                return;
            }
            if (currentText == "")
            {
                DeltaType = EDeltaType.Delete;
                mStart = 0;
                mDeltaLength = -previousText.Length;
                mDeltaText = previousText;

            }

            int delta = Math.Sign(currentText.Length - previousText.Length);
            if (delta == 1) DeltaType = EDeltaType.Insert;
            if (delta == 0) DeltaType = EDeltaType.Update;
            if (delta == -1) DeltaType = EDeltaType.Delete;

            Char[] previous = previousText.ToCharArray();
            Char[] current = currentText.ToCharArray();
            mStart = previous.Length;
            mDeltaLength = currentText.Length - previous.Length;
            for (int i = 0; i < Math.Min(previous.Length, current.Length); i++)
            {
                if (previous[i] != current[i])
                {
                    mStart = i;
                    break;
                }
            }


            mLength = Math.Abs(mDeltaLength);
            int backLength = Math.Min(previous.Length, current.Length) - mStart ;
            for (int i = 0; i < backLength; i++)
            {
                if (previous[previous.Length -1 - i] != current[current.Length -1 - i])
                {
                    mLength = current.Length -  mStart -i +1;
                    break;
                }

            }
          
        }


        public enum EDeltaType
        {
            Insert,
            Delete,
            Update,
            Same
        }

        public EDeltaType DeltaType { get; set; }
        private TextIndex mTextIndex = new TextIndex();

        public TextIndex TextIndex
        {
            get { return new TextIndex(mStart, mLength); }
            
        }

        public string DeltaText
        {
            get { return mDeltaText; }
        }

        public int Start
        {
            get { return mStart; }
           

        }
        public int DeltaLength
        {
            get { return mDeltaLength;
          
            }
           
        }
        public override string ToString()
        {
            return TextIndex.ToString() + " " + DeltaText;
        }
    }
}
