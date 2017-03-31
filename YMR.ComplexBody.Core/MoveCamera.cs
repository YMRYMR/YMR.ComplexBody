using Duality;
using Duality.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YMR.ComplexBody.Core
{
    [RequiredComponent(typeof(Camera))]
    public class MoveCamera : Component, ICmpUpdatable
    {
        public void OnUpdate()
        {
            float x = this.GameObj.Transform.Pos.X;
            float y = this.GameObj.Transform.Pos.Y;
            float z = this.GameObj.Transform.Pos.Z;
            if (DualityApp.Keyboard[Duality.Input.Key.Left]) x -= 1;
            else if (DualityApp.Keyboard[Duality.Input.Key.Right]) x += 1;
            if (DualityApp.Keyboard[Duality.Input.Key.Up]) y -= 1;
            else if (DualityApp.Keyboard[Duality.Input.Key.Down]) y += 1;
            this.GameObj.Transform.Pos = new Vector3(x, y, z);
        }
    }
}
