using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using GHShield.Managers;
using System.Drawing;

namespace GHShield.Security
{
    public class GHShieldValidator : GH_CanvasValidator
    {
        public override bool CanDragObject(
            IGH_DocumentObject obj,
            PointF dragFromPoint)
        {
            if (FreezeManager.IsFrozen(obj))
            {
                Rhino.RhinoApp.WriteLine($"Blocked movement: {obj.NickName}");
                return false;
            }

            return base.CanDragObject(obj, dragFromPoint);
        }
    }
}