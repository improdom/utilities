using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
namespace TextUtils.Selectors
{
  public  class AllLinesSelector:Selector 
    {
   

        public override TextIndexList SelectIndexes(string text)
        {
            StringReader sr = new StringReader(text);
            TextIndexList tl = new TextIndexList();
            int currentPos = 0;
            while (sr.Peek() != -1)
            {
                string line = sr.ReadLine();
                tl.Add(new TextIndex (currentPos, line.Length));
                currentPos += line.Length;
            }
            return tl;
        }

        public override List<string> SelectText(string text)
        {
            StringReader sr = new StringReader(text);
            List<string> result = new List<string>();
            while (sr.Peek() != -1)
            {
                result.Add(sr.ReadLine());
            }
            return result;
        }


        
        public override bool DoesTextIndexExist(string text)
        {
            return (text != null && text != "");
        }
    }
}
