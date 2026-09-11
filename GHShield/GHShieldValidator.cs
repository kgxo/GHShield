using System;
using System.Drawing;

using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

using GHShield.Core;

namespace GHShield
{
    /// <summary>
    /// Canvas validator.
    ///
    /// This is the API Grasshopper *intends* for exactly this job, and it is
    /// the right long-term home for protection. In Rhino 7 the drag and
    /// delete callbacks are not reliably invoked, so it currently acts as a
    /// second line of defence behind DocumentHook's event interception -
    /// and as the path that will work cleanly on Rhino 8.
    ///
    /// Deliberately silent: it can be called many times per frame.
    /// </summary>
    public class GHShieldValidator : GH_CanvasValidator
    {
        public override bool AppliesToDocument(Guid id)
        {
            return true;
        }

        public override bool CanDragObject(
            IGH_DocumentObject obj,
            PointF dragFromPoint)
        {
            return ProtectionService.CanMove(obj);
        }

        public override bool CanDeleteObject(IGH_DocumentObject obj)
        {
            return ProtectionService.CanDelete(obj);
        }

        public override bool CanCreateWire(IGH_Param source, IGH_Param target)
        {
            // A frozen component must not gain new inputs.
            return target == null || ProtectionService.CanEdit(target);
        }

        public override bool CanDeleteWire(IGH_Param source, IGH_Param target)
        {
            // ...nor lose the ones it has.
            return target == null || ProtectionService.CanEdit(target);
        }

        public override bool CanNavigateCanvas()
        {
            return true;
        }

        public override bool CanShowCanvasMenu(PointF pt)
        {
            return true;
        }

        public override bool CanShowObjectMenu(IGH_DocumentObject obj)
        {
            return true;
        }

        public override bool CanShowComponentSearchBox(PointF pt)
        {
            return true;
        }
    }
}
