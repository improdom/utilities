using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinFormsCodeBox
{
  public  class StringDifferenceRange
    {
      public enum EStringDifferenceType
      {
          none,
          insert,
          append,
          delete,
          update
      }

      private int mCharactersFromFront = 0;
      private int mCharactersFromEnd = 0;
      private EStringDifferenceType mStringDifferenceType;
      public StringDifferenceRange(string firstString, string secondString)
      {
          if (firstString == secondString)
          {
              mStringDifferenceType = EStringDifferenceType.none;
          }
          else if (secondString.StartsWith(firstString))
          {
              mStringDifferenceType = EStringDifferenceType.append;
          }
          else
          {
              char[] firstChars = firstString.ToCharArray();
              char[] secondChars = secondString.ToCharArray();
              char[] smallerChars = null;
              char[] largerChars = null;
              if (firstString.Length < secondString.Length)
              {
                  mStringDifferenceType = EStringDifferenceType.insert;
                  smallerChars = firstChars;
                  largerChars = secondChars;
              }
              else if (firstString.Length > secondString.Length)
              {
                  mStringDifferenceType = EStringDifferenceType.delete;
                  smallerChars = secondChars;
                  largerChars = firstChars;
              }
              else
              {
                  mStringDifferenceType = EStringDifferenceType.update;
                  smallerChars = firstChars;
                  largerChars = secondChars;
              }

              for (int i = 0; i < smallerChars.Length; i++)
              {
                  if (smallerChars[i] != largerChars[i])
                  {
                      mCharactersFromFront = i;
                  }

              }
              for (int i = 0; i < smallerChars.Length; i++)
              {
                  if (smallerChars[smallerChars.Length -1 - i] != largerChars[largerChars.Length -1  - i])
                  {
                      mCharactersFromEnd = i;
                  }

              }



          }


      }     


      public EStringDifferenceType StringDifferenceType 
      {
          get { return mStringDifferenceType; }

      }

      public int CharactersFromFront
      {
          get { return mCharactersFromFront; }

      }
      public int CharactersFromEnd
      {
          get { return mCharactersFromEnd; }

      }

    }
}
