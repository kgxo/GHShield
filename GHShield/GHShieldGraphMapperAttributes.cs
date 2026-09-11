using System.Windows.Forms;

using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

using GHShield.Core;

namespace GHShield
{
    /// <summary>
    /// Protection adapter for a Graph Mapper.
    ///
    /// The Graph Mapper runs its own graph editor: control points are dragged
    /// directly inside the object, so a generic ObjectChanged handler never
    /// sees the edit. This was the first adapter written and the pattern the
    /// others follow.
    ///
    /// Selection is still allowed - otherwise a frozen object could never be
    /// selected in order to unfreeze it.
    /// </summary>
    public class GHShieldGraphMapperAttributes
        : GH_GraphMapperAttributes, IGHShieldAttributes
    {
        public GHShieldGraphMapperAttributes(Grasshopper.Kernel.Special.GH_GraphMapper owner)
            : base(owner)
        {
        }

        private bool Frozen
        {
            get { return Core.SecurityManager.IsFrozen(Owner); }
        }

        private GH_ObjectResponse Block(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e != null && e.Button == MouseButtons.Left)
            {
                Selected = true;

                if (sender != null)
                    sender.Refresh();
            }

            // Right-click gets GHShield's menu rather than Grasshopper's,
            // which is where this object would otherwise be edited.
            if (e != null && e.Button == MouseButtons.Right)
                GHShield.UI.FrozenMenu.Show(sender, Owner);

            Core.Log.Debug(
                $"Frozen Graph Mapper '{Owner.NickName}': interaction blocked.");

            return GH_ObjectResponse.Handled;
        }

        public override GH_ObjectResponse RespondToMouseDown(
            GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            return Frozen ? Block(sender, e) : base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseMove(
            GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            return Frozen
                ? GH_ObjectResponse.Handled
                : base.RespondToMouseMove(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseUp(
            GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            return Frozen
                ? GH_ObjectResponse.Handled
                : base.RespondToMouseUp(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(
            GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            return Frozen
                ? GH_ObjectResponse.Handled
                : base.RespondToMouseDoubleClick(sender, e);
        }
    }
}
