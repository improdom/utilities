using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WinFormsCodeBox.Decorations;
using TextUtils;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Diagnostics;
using System.ComponentModel;
namespace WinFormsCodeBox
{
    [Serializable]
 public   class WinFormsCodeBox:RichTextBox
 {
     #region Imports
     [DllImport("user32.dll")]
     private static extern int SendMessage(IntPtr hwndLock, Int32 wMsg, Int32 wParam, ref Point pt);

     [DllImport("user32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
     private static extern int LockWindowUpdate(int hWnd);

     #endregion

     #region Cache
     string mLastText = "";
     string mUndoText = "";
     PreparedDecorationCollection mLastPrepared = new PreparedDecorationCollection();

     #endregion

     public override string Text
     {
         get
         {
             return base.Text;
         }
         set
         {
             mLastPrepared = new PreparedDecorationCollection();
             base.Text = value;
         }
     }

     private Point ScrollPosition
        {
            get
            {
                const int EM_GETSCROLLPOS = 0x0400 + 221;
                Point pt = new Point();

                SendMessage(this.Handle, EM_GETSCROLLPOS, 0, ref pt);
                return pt;
            }
            set
            {
                const int EM_SETSCROLLPOS = 0x0400 + 222;

                SendMessage(this.Handle, EM_SETSCROLLPOS, 0, ref value);
            }
        } 

     public WinFormsCodeBox()
     {
         
        
     }

     protected override void OnKeyPress(KeyPressEventArgs e)
     {
         

         if (e.KeyChar.GetHashCode() == 1703962)//Control Z
         {
            int selStart = this.SelectionStart;
            this.Text = mUndoText;
            this.SelectionStart = selStart;

         }
         else if (e.KeyChar.GetHashCode() == 196611)//Control C
         {
             
         }
         else if (e.KeyChar.GetHashCode() == 1441814)//Control V
         {
             mLastPrepared = new PreparedDecorationCollection();
             ApplyDecorations();

         }
         base.OnKeyPress(e);
     }

  
         
     private bool mDecorationInProgress; 

     protected override void OnTextChanged(EventArgs e)
     { 
         Debug.WriteLine("TextChanged");
         base.OnTextChanged(e);
         
             ApplyDecorations();
         
     }


     public  void ApplyDecorations()
     {
         if (mDecorationInProgress) return;
         mDecorationInProgress = true;
      
         
        string testText = this.Text;


        if (testText.Trim() == "")//No text implies no need for decorations 
        {                                                  
            mLastPrepared = new PreparedDecorationCollection();
            mDecorationInProgress = false; ;
            return;
        }
         
            DecorationCollection dc = new DecorationCollection();
            dc.Add(mDecorationScheme);
            dc.Add(mDecorations);

            PreparedDecorationCollection current = dc.Prepare(testText);

           
       
            TextDelta td = new TextDelta(mLastText, testText);
             
             
            if (td.DeltaType == TextDelta.EDeltaType.Insert || td.DeltaType == TextDelta.EDeltaType.Delete)
            {
                mLastPrepared.Shift(td.Start, td.DeltaLength );
            }

          
            PreparedDecorationCollection active = PreparedDecorationCollection.Merge(mLastPrepared, current, td);
            
           

            //activeBounds refers to to the area where there a difference between the last and the current decorations
            TextIndex activeBounds = active.DifferenceRange;
    
            mLastPrepared = current;
            if (mLastText != this.Text)
            {
                mUndoText = mLastText;
                mLastText = this.Text;
            }

            if (activeBounds != null || active.AreDecorationsChanged)
            {
                //Prepare for decorating
                int selStart = this.SelectionStart;
                int selLength = this.SelectionLength;
                Point origScroll = ScrollPosition;
                LockWindowUpdate(this.Handle.ToInt32());



                if (active.AreDecorationsChanged)
                {
                    Select(0, testText.Length);
                }
                else
                {
                    Select(activeBounds.Start, activeBounds.Length);
                }
                this.SelectionColor = this.ForeColor;
                this.SelectionBackColor = this.BackColor;

                for (int i = 0; i < active.Count; i++)
                {
                    ApplyDecoration(active.Decoration(i), active.Index(i));
                }

                //restore from decorating
                Select(selStart, selLength);
                this.SelectionColor = this.ForeColor;
                ScrollPosition = origScroll;
                LockWindowUpdate(0);


            }
        
         mDecorationInProgress = false;
     }

     private void ApplyDecoration(Decoration d, TextIndexList tl)
     {
       switch (d.DecorationType)
             {
                 case EDecorationType.TextColor:
                     foreach (TextIndex t in tl)
                     {
                         this.Select(t.Start, t.Length);
                         this.SelectionColor = d.Color;
                     }
                     break;
                 case EDecorationType.Hilight :
                     foreach (TextIndex t in tl)
                     {
                         this.Select(t.Start, t.Length);
                         this.SelectionBackColor  = d.Color;
                     }
                     break;
             }
     }


//[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
     private DecorationList  mDecorations = new DecorationList();
     /// <summary>
     /// List of the Decorative attributes assigned to the text
     /// </summary>
     public DecorationList Decorations
     {
         get { return mDecorations; }
         set { mDecorations = value; }
     }


     private DecorationScheme mDecorationScheme;
     /// <summary>
     /// The DecorationScheme used for the CodeBox
     /// </summary>
     /// 
     public DecorationScheme DecorationScheme
     {
         get { return mDecorationScheme; }
         set { mDecorationScheme = value;
         ApplyDecorations();

         }
     }

    

    }
}
