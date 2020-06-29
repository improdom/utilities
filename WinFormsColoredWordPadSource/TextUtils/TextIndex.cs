using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace TextUtils
{
    /// <summary>
    /// A pair of integers refering to the starting position and lenght of a piece of text
    /// </summary>
    public class TextIndex : IComparable<TextIndex>
    {
        /// <summary>
        ///The integer position of the first character
        /// </summary>
        public int Start { get; set; }
        
        /// <summary>
        /// Number of characters in range
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Contructor for the TextIndex
        /// </summary>
        public TextIndex() { }

        /// <summary>
        /// Contructor for the TextIndex
        /// </summary>
        public TextIndex(int start, int length)
        {
            Start = start;
            Length = length;
        }

        /// <summary>
        /// The integer position of the last character
        /// </summary>
        public int End
        {
            get { return Start + Length; }

        }

        public static TextIndex FromStartEnd(int start, int end)
        {

            return new TextIndex(start, end - start);

        }

        /// <summary>
        /// Returns the substring defined by the start and length of the textindex
        /// </summary>
        /// <param name="str">The string to extract the substring from</param>
        /// <returns>The Substring</returns>
        public string Text(string str)
        {
            return str.Substring(Start, Length);
        }


        public override string ToString()
        {
            return this.Start + "," + this.Length ;
        }


        public bool Contains(int position)
        {
            return (position >= Start && position <= End);

        }

        public int CompareTo(int position)
        {
            if (position < Start)
            {
                return  1;

            }
            else if (position > End)
            {

                return - 1;
            }
            else
            {
                return 0;
            }


        }

        public static TextIndex Parse(string text)
        {
            string[] strSplit = text.Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);
            if (strSplit.Length != 2)
            {
                throw new Exception("Bad format");
            }
            else
            {
               return new TextIndex(Int32.Parse(strSplit[0]), Int32.Parse(strSplit[1]) );
            }

        }

        #region IComparable<TextIndex> Members

        public int CompareTo(TextIndex other)
        {
            if ( Math.Sign( this.Start - other.Start) == 0) 
            {
                return Math.Sign(this.Length  - other.Length );
            }
            else
            {
                return Math.Sign(this.Start - other.Start);
            }
        }

        #endregion

        private SortedList<string, TextIndex> mGroups;
        public SortedList<string, TextIndex> Groups
        {
            get
            {
                if (mGroups == null)
                {
                    mGroups = new SortedList<string, TextIndex>();
                    mGroups.Add("0", this);
                   
                    
                }
                return mGroups;
            }
        }
    }
}
