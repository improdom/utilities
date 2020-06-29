using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinFormsCodeBox.Decorations;
namespace WinFormsCodeBox
{
    class DecorationCollection
    {
        private List<Decoration> mDecorations = new List<Decoration>();


        #region Add

        public void Add(DecorationScheme scheme)
        {
            if (scheme != null)
            {
                foreach (Decoration d in scheme.Decorations)
                {
                    mDecorations.Add(d);
                }
            }
        }
        public void Add(Decoration decoration)
        {
            if (decoration != null)
            {
                mDecorations.Add(decoration);
            }
        }

        public void Add(IEnumerable<Decoration> decorations)
        {
            if (decorations != null)
            {
                foreach (Decoration d in decorations)
                {
                    mDecorations.Add(d);
                }
            }
        }

        public void Add(IEnumerable<DecorationScheme> schemes)
        {
            if (schemes != null)
            {
                foreach (DecorationScheme ds in schemes)
                {
                    Add(ds);
                }
            }

        }

        #endregion

        public PreparedDecorationCollection Prepare(string text)
        {
            PreparedDecorationCollection prepared = new PreparedDecorationCollection();
            foreach (Decoration d in mDecorations)
            {
                prepared.Add(d, d.Ranges(text));
            }
            return prepared ;
        }
    }
}
