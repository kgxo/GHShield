using System.Windows.Forms;

using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

namespace GHShield.Core
{
    /// <summary>
    /// The methods the generated protection proxies call into.
    ///
    /// These are deliberately plain static methods taking 'object': emitting
    /// IL that calls a simple, non-generic signature is far easier to get
    /// right than emitting the decision logic itself. All the thinking stays
    /// in normal C# here; the generated code is only a branch.
    /// </summary>
    public static class ProxyInterceptor
    {
        /// <summary>
        /// True when the object owning these attributes is frozen.
        /// A proxy on an unfrozen object always falls through to the base
        /// implementation, so it is completely inert until you freeze.
        /// </summary>
        public static bool ShouldBlock(object attributes)
        {
            try
            {
                IGH_Attributes attr = attributes as IGH_Attributes;

                if (attr == null)
                    return false;

                IGH_DocumentObject owner = attr.DocObject;

                return owner != null && SecurityManager.IsFrozen(owner);
            }
            catch
            {
                // Never let a guard throw into Grasshopper's mouse handling.
                return false;
            }
        }

        /// <summary>
        /// Swallows the interaction, but still selects the object so the user
        /// retains a route to Unfreeze.
        /// </summary>
        public static GH_ObjectResponse Block(
            object attributes,
            GH_Canvas sender,
            GH_CanvasMouseEvent e)
        {
            try
            {
                IGH_Attributes attr = attributes as IGH_Attributes;

                if (attr != null)
                {
                    if (e != null && e.Button == MouseButtons.Left)
                    {
                        attr.Selected = true;

                        if (sender != null)
                            sender.Refresh();
                    }

                    // Grasshopper's own context menu is not safe to show on a
                    // frozen object - it edits values. GHShield offers its own
                    // instead, which is also the unfreeze route that no
                    // keyboard handler can steal.
                    if (e != null && e.Button == MouseButtons.Right)
                        GHShield.UI.FrozenMenu.Show(sender, attr.DocObject);

                    if (attr.DocObject != null)
                    {
                        Log.Debug(
                            $"Frozen '{attr.DocObject.NickName}': interaction blocked by proxy.");
                    }
                }
            }
            catch
            {
                // Blocking is the important part; the niceties are optional.
            }

            return GH_ObjectResponse.Handled;
        }
    }
}
