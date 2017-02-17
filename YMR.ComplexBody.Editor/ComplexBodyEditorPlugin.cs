using Duality.Editor;
using Duality.Editor.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YMR.ComplexBody.Editor
{
    public class ComplexBodyEditorPlugin : EditorPlugin
    {
        //private List<Vector2>

        public override string Id
        {
            get { return "YMR_ComplexBody_EditorPlugin"; }
        }

        protected override void InitPlugin(MainForm main)
        {
            base.InitPlugin(main);
        }
    }
}
