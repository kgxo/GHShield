using System.Windows.Forms;

using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

using GHShield.Core;

namespace GHShield.UI
{
    /// <summary>
    /// Protection adapter for a Boolean Toggle.
    ///
    /// A Toggle flips on a single left click, so mouse-down IS the edit - there
    /// is nothing left to reverse afterwards.
    ///
    /// Interaction is intercepted at the attributes level, because a generic
    /// ObjectChanged handler cannot see an edit made inside the object's own
    /// editor. Selection is still allowed - otherwise a frozen object could
    /// never be selected in order to unfreeze it.
    /// </summary>
    public class GHShieldToggleAttributes : GH_BooleanToggleAttributes, IGHShieldAttributes
    {
        public GHShieldToggleAttributes(Grasshopper.Kernel.Special.GH_BooleanToggle owner)
            : base(owner)
        {
        }

        private bool Frozen
        {
            get { return SecurityManager.IsFrozen(Owner); }
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

            Log.Debug($"Frozen a Boolean Toggle '{Owner.NickName}': interaction blocked.");

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
