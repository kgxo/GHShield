using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using GHShield.Managers;
using System.Windows.Forms;

namespace GHShield.Hooks
{
    public static class DocumentHook
    {
        public static void Initialize()
        {
            Instances.CanvasCreated += CanvasCreated;
        }

        private static void CanvasCreated(GH_Canvas canvas)
        {
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;

            ContextMenuHook.Attach(canvas);
        }

        private static void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            GH_Canvas canvas = sender as GH_Canvas;
            if (canvas == null)
                return;

            var doc = canvas.Document;
            if (doc == null)
                return;

            foreach (IGH_DocumentObject obj in doc.Objects)
            {
                if (obj.Attributes == null)
                    continue;

                if (obj.Attributes.Bounds.Contains(e.Location))
                {
                    FreezeManager.LastClickedObject = obj;

                    Rhino.RhinoApp.WriteLine($"Selected: {obj.NickName}");

                    break;
                }
            }
        }

        private static void Canvas_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Rhino.RhinoApp.WriteLine(
                    $"Dragging Mouse: {e.Location.X}, {e.Location.Y}");
            }
        }
    }
}