using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TextUtils.Selectors
{
  /// <summary>
  /// Abstract base Class for classes that Select, Delete , or replace text
  /// </summary>
 public abstract class Selector
 {
        #region SelectIndexes
        public abstract TextIndexList SelectIndexes(string text);
     #endregion

        #region SelectIndex
        public virtual TextIndex SelectIndex(string text)
        {
            TextIndexList tl = SelectIndexes(text);
            return tl[0];

        }
     

        public virtual TextIndex SelectIndex(string text, Func<string, bool> test)
        {
            TextIndexList tl = SelectIndexes(text);
            foreach (TextIndex t in tl)
            {
                if (test(t.Text(text)))
                {
                    return t;
                }
              
            }
             return null;
        }

        public virtual TextIndex SelectIndex(string text, Func<int, bool> test)
        {
            TextIndexList tl = SelectIndexes(text);
            for (int i = 0; i < tl.Count;  )
            {
                if (test(i))
                {
                    return tl[i];
                }
            }
            return null;
        }

        #endregion 

        public virtual bool DoesTextIndexExist(string text)
        {
            TextIndexList tl = SelectIndexes(text);
            return (tl.Count > 0);

        }

        public virtual List<string> SelectText(string text)
        {
            List<string> selected = new List<string>();
            TextIndexList tl = SelectIndexes(text);
            foreach (TextIndex ti in tl)
            {
                selected.Add(text.Substring(ti.Start, ti.Length));
            }
            return selected;
        }
        public virtual List<string> SelectText(string text, Func<string, bool> test)
        {
            List<string> selected = new List<string>();
            TextIndexList tl = SelectIndexes(text);
            foreach (TextIndex ti in tl)
            {
                string selectedText = ti.Text(text);
                if (test(selectedText))
                {
                    selected.Add(ti.Text(text));
                }
            }
            return selected;

        }
        public virtual List<string> SelectText(string text, Func<string, bool> test, Func<string, string> transformation)
        {
            List<string> selected = new List<string>();
            TextIndexList tl = SelectIndexes(text);
            foreach (TextIndex ti in tl)
            {
                string selectedText = ti.Text(text);
                if (test(selectedText))
                {
                    selected.Add(transformation(ti.Text(text)));
                }
            }
            return selected;

        }


         public virtual List<T> SelectList<T>(string text, Func<string,T> transformation){
             List<T> result = new List<T>();
             TextIndexList tl = SelectIndexes(text);
             foreach (TextIndex t in tl)
             {
                 result.Add(transformation(t.Text(text)));
             }

             return result;
         }


        public virtual string Delete(string text)
        {
            TextIndexList tl = SelectIndexes(text);
            return tl.Delete(text);
        }

        #region Replace

        //public virtual string Replace(string text, string replacementText);
        //public virtual string Replace(string text, Func<int, bool> test, string replacementText);
        //public virtual string Replace(string text, Func<string, bool> test, string replacementText);
        //public virtual string Replace(string text, Func<int, string, bool> test, string replacementText);

        //public virtual string Replace(string text, Func<int,string> transformation);
        //public virtual string Replace(string text, Func<string, string> transformation);
        //public virtual string Replace(string text, Func<int,string,string> transformation);

       
        //public virtual string Replace(string text, Func<string, bool> test, Func<int, string> transformation);
        //public virtual string Replace(string text, Func<string, bool> test, Func<string, string> transformation);
        //public virtual string Replace(string text, Func<string, bool> test, Func<int, string, string> transformation);

       
        //public virtual string Replace(string text, Func<int, bool> test, Func<int, string> transformation);
        //public virtual string Replace(string text, Func<int, bool> test, Func<string, string> transformation);
        //public virtual string Replace(string text, Func<int, bool> test, Func<int, string, string> transformation);


       
        //public virtual string Replace(string text, Func<int, string, bool> test, Func<int, string> transformation);
        //public virtual string Replace(string text, Func<int, string, bool> test, Func<string, string> transformation);
        //public virtual string Replace(string text, Func<int, string, bool> test, Func<int, string, string> transformation);

        #endregion

 }
}
