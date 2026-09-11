using System;
using System.Windows.Forms;

using Grasshopper;
using Grasshopper.GUI.Canvas;

using GHShield.Core;
using GHShield.Managers;

namespace GHShield.UI
{
    /// <summary>
    /// Application-level keyboard interception.
    ///
    /// GH_Canvas.KeyDown is too late for some combinations: Grasshopper and
    /// the Rhino host both get a look at the keystroke first, and whichever
    /// one claims it means our handler never runs. That is why Ctrl+Shift+F
    /// worked while Ctrl+Shift+U and Ctrl+Shift+D silently did nothing.
    ///
    /// An IMessageFilter sees WM_KEYDOWN before it is dispatched to any
    /// control, so nothing upstream can take it away from us. Returning true
    /// consumes the message entirely.
    ///
    /// One combination therefore does the work of two. Ctrl+Shift+F freezes
    /// the selection, and unfreezes it when everything selected is already
    /// frozen. Combinations involving Alt are still offered, but the menu bar
    /// has a prior claim on Alt and they cannot be depended on.
    /// </summary>
    public class ShortcutFilter : IMessageFilter
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private static bool _installed;

        public static void Install()
        {
            if (_installed)
                return;

            try
            {
                Application.AddMessageFilter(new ShortcutFilter());
                _installed = true;
            }
            catch (Exception ex)
            {
                Log.Error($"could not install keyboard filter: {ex.Message}");
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            try
            {
                return Handle(ref m);
            }
            catch (Exception ex)
            {
                // A throwing message filter would break the whole UI thread.
                Log.Debug($"Shortcut filter error: {ex.Message}");
                return false;
            }
        }

        private static bool Handle(ref Message m)
        {
            if (m.Msg != WM_KEYDOWN && m.Msg != WM_SYSKEYDOWN)
                return false;

            Keys key = ((Keys)(int)m.WParam) & Keys.KeyCode;

            bool ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
            bool alt = (Control.ModifierKeys & Keys.Alt) == Keys.Alt;

            if (!ctrl || (!shift && !alt))
                return false;

            if (key != Keys.F &&
                key != Keys.R &&
                key != Keys.U &&
                key != Keys.D)
            {
                return false;
            }

            GH_Canvas canvas = Instances.ActiveCanvas;

            if (canvas == null || canvas.Document == null)
                return false;

            // Only claim the keystroke when it was actually headed for the
            // Grasshopper canvas. Otherwise we would hijack Ctrl+Shift+D
            // everywhere else in Rhino.
            if (!IsCanvasMessage(m.HWnd, canvas))
                return false;

            switch (key)
            {
                case Keys.F:
                    if (alt)
                    {
                        // Ctrl+Alt+F. Kept because it is documented, but not
                        // relied on: Alt belongs to the window's menu bar, and
                        // once GHShield added a menu of its own there are two
                        // claimants for the keystroke.
                        Log.Debug("Ctrl+Alt+F -> unfreeze selection.");

                        ContextMenuManager.UnfreezeClicked(canvas, EventArgs.Empty);
                    }
                    else
                    {
                        // Ctrl+Shift+F. The one combination proven to reach us
                        // in Rhino 7, so it carries both directions: freeze,
                        // or unfreeze when everything selected is already
                        // frozen. See ContextMenuManager.ToggleFreezeClicked.
                        Log.Debug("Ctrl+Shift+F -> toggle freeze on selection.");

                        ContextMenuManager.ToggleFreezeClicked(canvas, EventArgs.Empty);
                    }

                    return true;

                case Keys.R:
                case Keys.U:
                    Log.Debug("Ctrl+Shift+U -> unfreeze selection.");

                    ContextMenuManager.UnfreezeClicked(canvas, EventArgs.Empty);
                    return true;

                case Keys.D:
                    ContextMenuManager.ToggleVerboseClicked(canvas, EventArgs.Empty);
                    return true;
            }

            return false;
        }

        private static bool IsCanvasMessage(IntPtr hwnd, GH_Canvas canvas)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            if (hwnd == canvas.Handle)
                return true;

            Control control = Control.FromHandle(hwnd);

            while (control != null)
            {
                if (ReferenceEquals(control, canvas))
                    return true;

                control = control.Parent;
            }

            return false;
        }
    }
}
