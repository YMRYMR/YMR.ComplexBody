using Duality;
using Duality.Components;
using Duality.Drawing;
using Duality.Editor;
using Duality.Editor.Plugins.CamView.CamViewStates;
using Duality.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YMR.ComplexBody.Editor
{
    public class ComplexBodyCamViewState : ObjectEditorCamViewState
    {
        private ToolStrip toolstrip;

        protected Core.ComplexBody selectedBody = null;

        public override string StateName
        {
            get { return "ComplexBody Editor"; }
        }

        public ComplexBodyCamViewState()
        {
            this.CameraActionAllowed = true;
            this.EngineUserInput = true;
        }

        protected override void OnEnterState()
        {
            base.OnEnterState();

            // Init the custom tile editing toolbar
            {
                this.View.SuspendLayout();
                toolstrip = new ToolStrip();
                toolstrip.SuspendLayout();

                toolstrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
                toolstrip.Name = "toolstrip";
                toolstrip.Text = "ComplexBody Editor Tools";
                toolstrip.Renderer = new Duality.Editor.Controls.ToolStrip.DualitorToolStripProfessionalRenderer();

                ToolStripButton btnInvHor = new ToolStripButton()
                {
                    Tag = "Invert Horizontal",
                    Text = "Invert Horizontal",
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    AutoToolTip = true
                };
                ToolStripButton btnInvVert = new ToolStripButton()
                {
                    Tag = "Invert Vertical",
                    Text = "Invert Vertical",
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    AutoToolTip = true
                };
                ToolStripButton btnCenterInObj = new ToolStripButton()
                {
                    Tag = "Center in Object",
                    Text = "Center in Object",
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    AutoToolTip = true
                };
                btnInvHor.Click += BtnInvHor_Click;
                btnInvVert.Click += BtnInvVert_Click;
                btnCenterInObj.Click += BtnCenterInObj_Click;
                toolstrip.Items.Add(btnInvHor);
                toolstrip.Items.Add(btnInvVert);
                toolstrip.Items.Add(btnCenterInObj);

                this.View.Controls.Add(toolstrip);
                this.View.Controls.SetChildIndex(toolstrip, this.View.Controls.IndexOf(this.View.ToolbarCamera));
                toolstrip.ResumeLayout(true);
                this.View.ResumeLayout(true);
            }

            this.View.SetToolbarCamSettingsEnabled(false);
            this.CameraObj.Active = false;

            // Register events
            DualityEditorApp.SelectionChanged += this.DualityEditorApp_SelectionChanged;
            DualityEditorApp.ObjectPropertyChanged += this.DualityEditorApp_ObjectPropertyChanged;
        }

        private void BtnCenterInObj_Click(object sender, EventArgs e)
        {
            Transform tr = selectedBody.GameObj.GetComponent<Transform>();
            List<Vector2> points = selectedBody.Points;
            Vector2 min = new Vector2(points.Min(x => x.X), points.Min(x => x.Y));
            Vector2 max = new Vector2(points.Max(x => x.X), points.Max(x => x.Y));
            float diffX = (tr.Pos.X - (min.X + (max.X - min.X) * .5f)) - tr.Pos.X;
            float diffY = (tr.Pos.Y - (min.Y + (max.Y - min.Y) * .5f)) - tr.Pos.Y;

            int t = points.Count;
            for (int i = 0; i < t; i++)
            {
                points[i] = new Vector2(points[i].X + diffX, points[i].Y + diffY);
            }

            selectedBody.UpdateBody(true);
        }

        private void BtnInvVert_Click(object sender, EventArgs e)
        {
            Invert(1, -1);
        }
        private void BtnInvHor_Click(object sender, EventArgs e)
        {
            Invert(-1, 1);
        }
        private void Invert(int x, int y)
        {
            int t = selectedBody.Points.Count;
            for(int i = 0; i < t; i++)
            {
                selectedBody.Points[i] = new Vector2(selectedBody.Points[i].X * x, selectedBody.Points[i].Y * y);
            }
            selectedBody.UpdateBody(true);
        }

        protected override void OnLeaveState()
        {
            // Unregister events
            DualityEditorApp.SelectionChanged -= this.DualityEditorApp_SelectionChanged;
            DualityEditorApp.ObjectPropertyChanged -= this.DualityEditorApp_ObjectPropertyChanged;

            this.View.SuspendLayout();
            toolstrip.SuspendLayout();

            toolstrip.Items.Clear();
            this.View.Controls.Remove(toolstrip);
            toolstrip.Dispose();

            toolstrip.ResumeLayout(true);
            this.View.ResumeLayout(true);

            base.OnLeaveState();
            this.View.SetToolbarCamSettingsEnabled(true);
            this.CameraObj.Active = true;
        }
        //protected override void OnRenderState()
        //{
        //    base.OnRenderState();
            
        //    //// Render game pov
        //    //Rect viewportRect = new Rect(this.ClientSize.Width, this.ClientSize.Height);
        //    //if (!Scene.Current.FindComponents<Camera>().Any()) DrawDevice.RenderVoid(viewportRect);
        //    //else DualityApp.Render(viewportRect);
        //}
        //protected override void OnCollectStateWorldOverlayDrawcalls(Canvas canvas)
        //{
        //    base.OnCollectStateWorldOverlayDrawcalls(canvas);

        //    //GameObject selGameObj = this.selectedBody != null ? this.selectedBody.GameObj : null;
        //    //Transform selTransform = selGameObj != null ? selGameObj.Transform : null;
        //    //if (selTransform == null) return;
        //}

        private void DualityEditorApp_ObjectPropertyChanged(object sender, ObjectPropertyChangedEventArgs e)
        {
            
        }

        private void DualityEditorApp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(selectedBody != null) selectedBody.IsSelected = false;

            if (e.Current != null)
            {
                GameObject gameObj = e.Current.GameObjects.Where(x => x.GetComponent<Core.ComplexBody>() != null).FirstOrDefault();
                if (gameObj != null)
                {
                    selectedBody = gameObj.GetComponent<Core.ComplexBody>();
                    selectedBody.IsSelected = true;
                    this.View.Refresh();
                    this.View.Focus();
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            //base.OnKeyDown(e);

            if (selectedBody != null)
            {
                selectedBody.CtrlPressed = e.Control;
                selectedBody.OnUpdate();
                this.View.Refresh();
            }
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            //base.OnKeyUp(e);

            OnKeyDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            //base.OnMouseMove(e);

            if (selectedBody != null)
            {
                selectedBody.MouseX = e.X;
                selectedBody.MouseY = e.Y;
                selectedBody.MouseLeft = e.Button == MouseButtons.Left;
                selectedBody.MouseRight = e.Button == MouseButtons.Right;
                selectedBody.OnUpdate();
                this.View.Refresh();
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            //base.OnMouseDown(e);

            OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            //base.OnMouseUp(e);

            OnMouseMove(e);
        }
    }
}
