using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WinFormsColoredWordPad;
using WinFormsCodeBox.Decorations;
namespace WinFormsColoredWordPad
{
    public partial class TextDocument : Form
    {
        public TextDocument()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.codeBox.Text = Properties.Settings.Default.LastText;
            cmbDecorationScheme.SelectedIndex = 1;
        }

    

        private void cmbDecorationScheme_SelectedIndexChanged(object sender, EventArgs e)
        {
             switch (cmbDecorationScheme.SelectedIndex)
            {

                case 0:
                    codeBox.DecorationScheme = null;
                    break;
                case 1:
                    codeBox.DecorationScheme = WinFormsCodeBox.Decorations.DecorationSchemes.CSharp3;
                    break;

                case 2:
                    codeBox.DecorationScheme = WinFormsCodeBox.Decorations.DecorationSchemes.Xml;
                    break;

            }
        }

        private void hiliteLineToolStripMenuItem_Click(object sender, EventArgs e)
        {   codeBox.Decorations.Clear();
            Dialogs.DiaLineNumbers dia = new  Dialogs.DiaLineNumbers();
            if (dia.ShowDialog() == DialogResult.OK)
            {
                if (dia.LineNumbers.Count > 0)
                {

                    if (dia.LineNumbers.Count == 1)
                    {
                        LineDecoration ld = new LineDecoration()
                        {
                            DecorationType = WinFormsCodeBox.Decorations.EDecorationType.Hilight,
                            Color = Color.Yellow,
                            Line = dia.LineNumbers[0]
                        };
                        codeBox.Decorations.Add(ld);
                    }
                    else
                    {
                        MultiLineDecoration ld = new MultiLineDecoration()
                        {
                            DecorationType = WinFormsCodeBox.Decorations.EDecorationType.Hilight,
                            Color = Color.Yellow,
                            Lines = dia.LineNumbers 
                        };
                        codeBox.Decorations.Add(ld);
                        
                    }

                }
            }

          codeBox.ApplyDecorations(); 
                 
        }

        private void codeBox_SelectionChanged(object sender, EventArgs e)
        {
            lblTextIndex.Text = codeBox.SelectionStart +","  + codeBox.SelectionLength ;
            
        }

        private void TextDocument_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.LastText = this.codeBox.Text;
            Properties.Settings.Default.Save();

        }

    }
}
