using System;
using System.Windows.Forms;

using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

using GHShield.Core;
using GHShield.Managers;

namespace GHShield.UI
{
    /// <summary>
    /// The right-click menu for a frozen object.
    ///
    /// A frozen object cannot be allowed to show Grasshopper's own context
    /// menu: that menu is full of editing commands - a slider's range, a value
    /// list's contents, Disable, Bake - and every one of them would walk
    /// straight around the freeze. So the interaction is swallowed, and this
    /// small menu is offered in its place.
    ///
    /// It is also the one route to unfreezing that nothing can intercept.
    /// Keyboard shortcuts in Rhino 7 are contested by the command line and the
    /// menu bar, and three combinations were tried and abandoned before this.
    /// A mouse click on an object has no such competition.
    /// </summary>
    public static class FrozenMenu
    {
        private static ContextMenuStrip _menu;

        public static void Show(GH_Canvas canvas, IGH_DocumentObject obj)
        {
            if (canvas == null || obj == null)
                return;

            try
            {
                // Right-click arrives at more than one handler. If the menu is
                // already up, the second arrival must not stack another on
                // top of it.
                if (_menu != null && _menu.Visible)
                    return;

                Close();

                _menu = new ContextMenuStrip();

                ToolStripMenuItem header = new ToolStripMenuItem(
                    "Frozen by GHShield");

                header.Enabled = false;

                _menu.Items.Add(header);
                _menu.Items.Add(new ToolStripSeparator());

                Grasshopper.Kernel.Special.GH_Group owner =
                    FreezeManager.FrozenGroupOf(obj);

                if (owner == null)
                {
                    _menu.Items.Add(Item(
                        $"Unfreeze '{Label(obj)}'",
                        (s, e) => UnfreezeOne(canvas, obj)));
                }
                else
                {
                    // Sealed inside a frozen group. Releasing it on its own
                    // would let it move, and a group is drawn around wherever
                    // its members are - so the group would change shape. The
                    // group is the unit here, and the menu says so instead of
                    // silently doing nothing.
                    ToolStripMenuItem note = new ToolStripMenuItem(
                        "Sealed inside a frozen group");

                    note.Enabled = false;

                    _menu.Items.Add(note);

                    _menu.Items.Add(Item(
                        $"Unfreeze This Group ({FreezeManager.MemberCount(owner)})",
                        (s, e) => UnfreezeOne(canvas, owner)));
                }

                _menu.Items.Add(Item(
                    "Unfreeze Everything",
                    (s, e) => ContextMenuManager.UnfreezeAllClicked(canvas, EventArgs.Empty)));

                _menu.Items.Add(new ToolStripSeparator());

                _menu.Items.Add(Item(
                    "Show Report...",
                    ReportDialog.Show));

                _menu.Items.Add(Item(
                    "GHShield Panel...",
                    GHShieldPanel.ClickedFromMenu));

                _menu.Show(canvas, canvas.PointToClient(Control.MousePosition));
            }
            catch (Exception ex)
            {
                Log.Debug($"Frozen menu failed: {ex.Message}");
            }
        }

        private static void UnfreezeOne(GH_Canvas canvas, IGH_DocumentObject obj)
        {
            try
            {
                FreezeManager.Unfreeze(obj);

                GH_Document document = obj.OnPingDocument();

                if (document != null)
                    ManifestManager.Sync(document);

                if (canvas != null)
                    canvas.Refresh();

                Log.Info($"unfroze '{Label(obj)}'.");
            }
            catch (Exception ex)
            {
                Log.Error($"could not unfreeze: {ex.Message}");
            }
        }

        private static string Label(IGH_DocumentObject obj)
        {
            string name = obj.NickName;

            if (string.IsNullOrWhiteSpace(name))
                name = obj.Name;

            return string.IsNullOrWhiteSpace(name) ? "this object" : name;
        }

        private static ToolStripMenuItem Item(string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);

            item.Click += handler;

            return item;
        }

        private static void Close()
        {
            if (_menu == null)
                return;

            try
            {
                _menu.Close();
                _menu.Dispose();
            }
            catch
            {
                // Already gone.
            }

            _menu = null;
        }
    }
}
