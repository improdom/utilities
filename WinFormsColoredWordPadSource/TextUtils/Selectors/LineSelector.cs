using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TextUtils.Selectors
{
public class LineSelector:Selector 
    {

       private List<int> mLines = new List<int>();

        public List<int> Lines
        {
            get { return mLines; }
            set { mLines = value; }

        }
        public override TextIndexList SelectIndexes(string text)
        {
            StringReader sr = new StringReader(text);
            TextIndexList tl = new TextIndexList();
            int currentPos = 0;
            int currentLine = 0;
            while (sr.Peek() != -1)
            {
                string line = sr.ReadLine();
                if (mLines.Contains(currentLine))
                {
                    tl.Add(new TextIndex(currentPos, line.Length));
                }
                currentPos += line.Length +1;
                currentLine++;
            }
            return tl;
        }
    }
}
