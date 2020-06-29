using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
namespace TextUtils
{
    public class TextIndexList : List<TextIndex>
    {
        public string Delete(string text)
        {
            if (this.Count == 0)
            {
                return text;
            }
            else if (this.Count == 1)
            {
                return text.Remove(this[0].Start, this[0].Length);
            }
            else
            {
                bool[] positionsToDelete = new bool[text.Length];

                foreach (TextIndex t in this)
                {
                    for (int i = t.Start; i < t.Start + t.Length; i++)
                    {
                        positionsToDelete[i] = true;
                    }
                }
                char[] origChars = text.ToCharArray();
                List<char> finalChars = new List<char>();
                for (int i = 0; i < positionsToDelete.Length; i++)
                {
                    if (!positionsToDelete[i])
                    {
                        finalChars.Add(origChars[i]);
                    }
                }
                return new string(finalChars.ToArray());

            }


        }

        public override string ToString()
        {
            if (this.Count == 0)
            {
                return "";
            }
            else if (this.Count == 1)
            {
                return this[0].Start.ToString() + "," + this[0].Length.ToString();
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(this[0].Start.ToString() + "," + this[0].Length.ToString());
                for (int i = 1; i < this.Count; i++)
                {
                    sb.Append(":" + this[i].Start.ToString() + "," + this[i].Length.ToString());
                }
                return sb.ToString();
            }



        }


        public static TextIndexList Parse(string value)
        {
            TextIndexList tl = new TextIndexList();

            string[] strTextIndexes = value.Split(new string[1] { ":" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string strTextIndex in strTextIndexes)
            {
                string[] strSplit = strTextIndex.Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);
                if (strSplit.Length != 2)
                {
                    throw new Exception("Bad format");
                }
                else
                {
                    tl.Add(new TextIndex(Int32.Parse(strSplit[0]), Int32.Parse(strSplit[1])));
                }

            }
            return tl;

        }


        /// <summary>
        /// returns a TextIndex that runs from the start of the first Textindex to the end of the last
        /// </summary>
        /// <returns>a textindex</returns>
        public TextIndex Bounds 
        {
            get
            {
               
                    return TextIndex.FromStartEnd(this.Start, this.End);
                 
            }
        }

        public int Start
        {
            get
            {
               
                if (this.Count == 0) return 0;
            int result = Int32.MaxValue  ;
                foreach (TextIndex t in this)
                {
                    if (t.Start < result)
                    {
                        result = t.Start;
                    }
                }
                return result;

            }
        }

        public int End
        {
            get
            {
                if (this.Count == 0) return 0;
                int result = Int32.MinValue ;
                foreach (TextIndex t in this)
                {
                    if (t.End > result)
                    {
                        result = t.End ;
                    }
                }
                return result;
            }
        }


        /// <summary>
        /// Merges TextIndexes and sorts the textindexes
        /// </summary>
        public void Simplify()
        {
            BitArray ba = this.ToBitArray();
            TextIndexList  til = TextIndexList.FromBitArray(ba);
            this.Clear();
            this.AddRange(til);

        }

  
        /// <summary>
        /// Returns a BitArray representing the TextIndexList
        /// </summary>
        /// <returns></returns>
        public  BitArray ToBitArray()
        {
            if (this.Count == 0)
            {
                return new BitArray(0);
            }
            else
            {
                return ToBitArray(this.End);


            }
        }

        public BitArray ToBitArray(int size)
        {
            BitArray bits = new BitArray(size);
            foreach (TextIndex t in this)
            {
                int maxVal = Math.Min(size, t.Start + t.Length);
                for (int i = t.Start; i < maxVal; i++)
                {
                    bits[i] = true;
                }
            }
            return bits;
        }
       

        /// <summary>
        /// Creates a TextIndexList from a BitArray;
        /// </summary>
        /// <param name="bits"></param>
        /// <returns>TextIndexList</returns>
        public static TextIndexList FromBitArray(BitArray bits)
        {
            return FromBitArray(bits, new TextIndex (0, bits.Length));
        }

        public static TextIndexList FromBitArray(BitArray bits, TextIndex index)
        {
            string bitString = BitArrayString(bits);
            TextIndexList tl = new TextIndexList();
            int currentStart = -1;
            int lastBit = Math.Min(index.Start + index.Length, bits.Length);
            for (int i = index.Start; i < lastBit; i++)
            {
                if (bits[i])
                {
                    if (currentStart == -1)
                    {
                        currentStart = i;
                    }
                }
                else
                {
                    if (currentStart != -1)
                    {
                        tl.Add(TextIndex.FromStartEnd(currentStart, i  ));
                        currentStart = -1;
                    }
                }
            }
            if (currentStart != -1)
            {
                tl.Add(TextIndex.FromStartEnd(currentStart, index.End ));
            }
            return tl;
        }


        public TextIndexList Union(TextIndexList tl)
        {
            int arraySize = Math.Max(this.Bounds.End, tl.Bounds.End);
            BitArray bArray = this.ToBitArray(arraySize);
            BitArray btlArray = tl.ToBitArray(arraySize);
           
             BitArray bResult =  bArray.Or(btlArray);
             return TextIndexList.FromBitArray(bResult);
        }

        public TextIndexList Intersection(TextIndexList tl)
        {
            int arraySize = Math.Max(this.Bounds.End, tl.Bounds.End);
            BitArray bArray = this.ToBitArray(arraySize);
            BitArray btlArray = tl.ToBitArray(arraySize);

            BitArray bResult = bArray.And(btlArray);
            return TextIndexList.FromBitArray(bResult);

        }

        public TextIndexList SymetricDifference(TextIndexList tl)
        {
            int arraySize = Math.Max(this.Bounds.End, tl.Bounds.End);
            BitArray bArray = this.ToBitArray(arraySize);
            BitArray btlArray = tl.ToBitArray(arraySize);

            BitArray bResult = bArray.Xor(btlArray);
            return TextIndexList.FromBitArray(bResult);
        }

        public TextIndexList SubtractedFrom (TextIndexList tl)
        {
            int arraySize = Math.Max(this.Bounds.End, tl.Bounds.End);
            BitArray bArray = this.ToBitArray(arraySize);
            BitArray btlArray = tl.ToBitArray(arraySize);

            BitArray bXor = bArray.Xor(btlArray);
            return TextIndexList.FromBitArray(btlArray.And(bXor));

        }

        /// <summary>
        /// Shifts the Start values by amount
        /// </summary>
        /// <param name="startingIndex">value to start shifting at</param>
        /// <param name="amount">amount to shift</param>
        public void Shift(int startingIndex, int amount)
        {
            foreach (TextIndex t in this)
            {
                if (t.Contains(startingIndex ) )
                {
                    t.Length  +=amount;
                }
                else if (t.Start > startingIndex)
                {
                    t.Start += amount;
                }
                t.Length = Math.Max(0, t.Length);//TextIndex values are nonnegative
                t.Start = Math.Max(0, t.Start);  //TextIndex values are nonnegative
            }
        }

       
        public TextIndex RangeDifferentFrom(TextIndexList tl)
        {
           TextIndexList diff =  this.SymetricDifference(tl);
           return diff.Bounds;
        }


        public int MaxLength
        {
            get
            {
                int max = (from t in this
                           select t.Length).Max();
                return max;
            }
        }




        public TextIndexList Projection(TextIndex index)
        {
            if (this.Count == 0)
            {
                return new TextIndexList();
            }
            BitArray thisBA = this.ToBitArray();
            if (index.Start > thisBA.Length)
            {
                return new TextIndexList();
            }
            return FromBitArray(thisBA, index);

        }
        public TextIndexList Clone()
        {
            TextIndexList tl = new TextIndexList();
            foreach (TextIndex t in this)
            {
                tl.Add(new TextIndex(t.Start, t.Length));

            }
            return tl;

        }

        public int IndexLengthUpperBound()
        {
            int result = 0;
            foreach (TextIndex t in this)
            {
                result += t.Length;
            }

            return result;
        }

        public int IndexLength()
        {
            this.Simplify();
            return this.IndexLengthUpperBound();
        }

        private static string BitArrayString(BitArray ba)
        {
            StringBuilder sb = new StringBuilder();
            foreach (bool bit in ba)
            {
                if (bit)
                {
                    sb.Append(1);
                }
                else
                {
                    sb.Append(0);

                }

            }
            return sb.ToString();

        }
    }
}
