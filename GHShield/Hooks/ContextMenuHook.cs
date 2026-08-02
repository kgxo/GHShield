using System.Windows.Forms;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using GHShield.Managers;

namespace GHShield.Hooks
{
    public static class ContextMenuHook
    {
        public static void Attach(GH_Canvas canvas)
        {
            canvas.MouseUp += Canvas_MouseUp;
        }

        private static void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            GH_Canvas canvas = sender as GH_Canvas;
            if (canvas == null)
                return;

            if (FreezeManager.LastClickedObject == null)
                return;

            FreezeManager.FreezeLast();

            Rhino.RhinoApp.WriteLine("Selected object frozen.");
        }
    }
}