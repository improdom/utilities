using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WinFormsColoredWordPad.Dialogs
{
    public partial class DiaLineNumbers : Form
    {
        public DiaLineNumbers()
        {
            InitializeComponent();
        }

        public List<int> LineNumbers
        {
            get
            {
                String[] strNums = txtLineNumbers.Text.Split(new string[1]{","},  StringSplitOptions.RemoveEmptyEntries);

                List<int> lines = new List<int>();
                foreach (string str in strNums)
                {
                    try
                    {
                        int line = int.Parse(str);
                        lines.Add (line);
                    }catch{}

                }
               
                return lines;
            }

        }
    }

    
}
