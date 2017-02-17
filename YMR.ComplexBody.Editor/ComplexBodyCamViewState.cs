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
            this.View.SetToolbarCamSettingsEnabled(false);
            this.CameraObj.Active = false;

            // Register events
            DualityEditorApp.SelectionChanged += this.DualityEditorApp_SelectionChanged;
            DualityEditorApp.ObjectPropertyChanged += this.DualityEditorApp_ObjectPropertyChanged;
        }

        protected override void OnLeaveState()
        {
            // Unregister events
            DualityEditorApp.SelectionChanged -= this.DualityEditorApp_SelectionChanged;
            DualityEditorApp.ObjectPropertyChanged -= this.DualityEditorApp_ObjectPropertyChanged;

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
                selectedBody.MustBeUpdated = true;
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
                selectedBody.MustBeUpdated = true;
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
