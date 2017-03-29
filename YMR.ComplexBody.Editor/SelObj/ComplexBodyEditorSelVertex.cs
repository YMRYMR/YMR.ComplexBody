using Duality;
using Duality.Editor.Plugins.CamView.CamViewStates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YMR.ComplexBody.Editor.SelObj
{
    public class ComplexBodyEditorSelVertex : ObjectEditorSelObj
    {
        private GameObject bodyObj;
        private Core.ComplexBody complexBody;
        private int vertexIndex = -1;

        public int VertexIndex { get { return vertexIndex; } }
        public Core.ComplexBody ComplexBody { get { return complexBody; } }

        public ComplexBodyEditorSelVertex(int index)
        {

        }

        public override object ActualObject { get { return this.bodyObj == null || this.bodyObj.Disposed ? null : this.bodyObj; } }
        public override string DisplayObjectName { get { return string.Format("{0} (vertex {1})", bodyObj.Name, vertexIndex); } }
        public override bool HasTransform
        {
            get { return this.bodyObj != null && !this.bodyObj.Disposed && this.bodyObj.Transform != null; }
        }
        public override Vector3 Pos
        {
            get { return this.bodyObj.Transform.Pos; }
            set { }
        }
        public override float Angle
        {
            get { return this.bodyObj.Transform.Angle; }
            set { }
        }
        public override Vector3 Scale
        {
            get { return Vector3.One * this.bodyObj.Transform.Scale; }
            set { }
        }
        public override float BoundRadius
        {
            get
            {
                ICmpRenderer r = this.bodyObj.GetComponent<ICmpRenderer>();
                return r == null ? CamView.DefaultDisplayBoundRadius : r.BoundRadius;
            }
        }
        public override bool ShowPos
        {
            get { return false; }
        }

        public RigidBodyEditorSelBody(RigidBody obj)
        {
            this.bodyObj = obj != null ? obj.GameObj : null;
        }

        public override bool IsActionAvailable(ObjectEditorAction action)
        {
            return false;
        }
    }
}
