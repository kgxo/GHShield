using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

using GHShield.UI;

namespace GHShield.Components
{
    /// <summary>
    /// Makes the manifest component double-clickable: it opens the GHShield
    /// panel. Double-clicking a component to open its settings is the gesture
    /// Grasshopper users already expect, and unlike a keyboard shortcut, the
    /// Rhino command line cannot steal it.
    /// </summary>
    public class GHShieldManifestAttributes : GH_ComponentAttributes
    {
        public GHShieldManifestAttributes(GHShieldManifestComponent owner)
            : base(owner)
        {
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(
            GH_Canvas sender,
            GH_CanvasMouseEvent e)
        {
            GHShieldPanel.ShowPanel();

            return GH_ObjectResponse.Handled;
        }
    }
}
