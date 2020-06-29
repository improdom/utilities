using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using TextUtils;
using WinFormsCodeBox.Decorations;
using System.Diagnostics;
namespace WinFormsCodeBox
{
    class PreparedDecorationCollection
    {

        private List<Decoration> mDecorations = new List<Decoration>();
        private List<TextIndexList> mIndexes = new List<TextIndexList>();
        private bool mAreDecorationsChanged;


        public bool AreDecorationsChanged
        {
            get { return mAreDecorationsChanged; }

        }
      public void  Add(Decoration decoration, TextIndexList index)
      {
          mDecorations.Add(decoration);
          mIndexes.Add(index);

      }

      public void Shift(int start, int amount)
      {
          foreach (TextIndexList tl in mIndexes)
          {
              tl.Shift(start, amount);

          }

      }

      public static PreparedDecorationCollection Merge(PreparedDecorationCollection previous, PreparedDecorationCollection current, TextDelta td)
      {
          if (previous.mDecorations.Count == 0)
          {
              current.mDifferenceRange = current.Bounds();
              return current;
          }
          if (AreDecorationsSame(previous, current))
          {
              PreparedDecorationCollection merged = new PreparedDecorationCollection();
              TextIndexList differenceRanges = new TextIndexList();
              for (int i = 0; i < previous.Count; i++)
              {
                 TextIndexList previousIndex = previous.Index(i);
                 TextIndexList currentIndex = current.Index(i);
                TextIndex differenceRange =currentIndex.RangeDifferentFrom( previousIndex);
                  if (differenceRange != null && differenceRange.Length > 0)
                  {
                    differenceRanges.Add(differenceRange);
                  }
               
              }
               differenceRanges.Add(td.TextIndex);
               
              merged.mDifferenceRange = differenceRanges.Bounds;
              TextIndex activeArea = differenceRanges.Bounds;
              if (activeArea != null)
              {
                  for (int i = 0; i < previous.Count; i++)
                  {
                 TextIndexList currentIndex =  current.Index(i);
                 TextIndexList projected = currentIndex.Projection(activeArea);

                  if (projected.IndexLengthUpperBound() > 0)// Are there and textindexes in projected that have a length > 0
                  {
                      merged.Add(current.Decoration(i), projected);
                  }
                  }
              }

              return merged;

          }
          else
          {
              current.mAreDecorationsChanged = true;
            return current;

          }
          
      }
      private static  bool AreDecorationsSame(PreparedDecorationCollection previous, PreparedDecorationCollection current)
      {
          //please note that my comparison is by references to the Decorations
          //Though a more thourough comparison could be made the trade off aginst this quicker comparison
          //does not seem worhwhile
          if (previous.Count  == current.Count)
          {
              for (int i = 0; i < previous.Count; i++)
              {
                  if (previous.Decoration(i) != current.Decoration(i) )
                  {
                      return false;
                  }
              }
              return true;
          }
          else
          {

              return false;

          }


      }


      public Decoration Decoration(int position)
      {
          return mDecorations[position];

      }

      public TextIndexList Index(int position)
      {
          return mIndexes[position];

      }

      public int Count 
      {
          get
          {
              return mDecorations.Count;
          }

      }

      public bool HasDecorations
      {
          get
          {
              return mIndexes.Count  > 0;
          }
      }

      private TextIndex mDifferenceRange;
      public TextIndex DifferenceRange
      {
          get { return mDifferenceRange; }

      }
      public  TextIndex Bounds()
      {
          TextIndexList  boundlist = new  TextIndexList();
          foreach (TextIndexList t in this.mIndexes)
          {
              if (t!= null && t.Count > 0  )
              {
                  boundlist.Add(t.Bounds);
              }

          }
          if (boundlist.Count > 0)
          {
              return boundlist.Bounds;
          }
          else
          {
              return null;
          }

      }

        public string AffectedText(string text)
        {
           return AffectedText(text, v => true);

        }

        public string AffectedText(string text, Predicate<Decoration> predicate)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < mIndexes.Count; i++)
            {
                if (predicate(mDecorations[i]))
                {
                    sb.Append(mDecorations[i].Key + "-" + mDecorations[i].GetType().Name + "  ");
                    foreach (TextIndex t in mIndexes[i])
                    {
                        sb.Append(t.ToString() + " : " + t.Text(text) + "   ");

                    }

                    sb.Append("\n");
                }

            }
            sb.Append("\n");
            return sb.ToString();


        }
    }
}
