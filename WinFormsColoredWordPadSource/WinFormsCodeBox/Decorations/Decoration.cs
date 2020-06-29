using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using TextUtils;
namespace WinFormsCodeBox.Decorations
{
    [Serializable]
    public abstract class Decoration
    {
        public string Key { get; set; }
        public EDecorationType DecorationType { get; set; }

        private bool mIsEnabled = true;
        public bool IsEnabled 
        {
            get { return mIsEnabled; }
            set { mIsEnabled = value; }

        }
      
        public Color Color{ get; set; }
      

        public abstract TextIndexList  Ranges(string text);
    }
}
