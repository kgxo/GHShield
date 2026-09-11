using System;

using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

using GHShield.Core;

namespace GHShield.Managers
{
    /// <summary>
    /// Every GHShield command lives here as a plain EventHandler, so the
    /// menu items and the keyboard shortcuts run identical code.
    /// </summary>
    public static class ContextMenuManager
    {
        private static GH_Document ResolveDocument(object sender)
        {
            GH_Canvas canvas = sender as GH_Canvas;

            if (canvas == null)
                canvas = Grasshopper.Instances.ActiveCanvas;

            if (canvas == null)
            {
                Log.Info("No Grasshopper canvas available.");
                return null;
            }

            GH_Document document = canvas.Document;

            if (document == null)
                Log.Info("No Grasshopper document is active.");

            return document;
        }

        /// <summary>
        /// Runs after any command that changed protection state: keeps the
        /// manifest component (and therefore the saved file) in step, then
        /// repaints.
        /// </summary>
        private static void Finish(GH_Document document)
        {
            ManifestManager.Sync(document);

            if (Grasshopper.Instances.ActiveCanvas != null)
                Grasshopper.Instances.ActiveCanvas.Refresh();
        }

        // =====================================================
        // DOUBLE-FIRE GUARD
        // =====================================================

        private static string _lastCommand;
        private static DateTime _lastCommandTime = DateTime.MinValue;

        /// <summary>
        /// A command can arrive from the menu, from the canvas key handler,
        /// or from the message filter. Only one of those paths is live on any
        /// given machine, but if two ever were, a toggle would cancel itself
        /// out. Ignore a repeat of the same command within 300 ms.
        /// </summary>
        private static bool ShouldRun(string command)
        {
            DateTime now = DateTime.UtcNow;

            if (_lastCommand == command &&
                (now - _lastCommandTime).TotalMilliseconds < 300)
            {
                return false;
            }

            _lastCommand = command;
            _lastCommandTime = now;

            return true;
        }

        // =====================================================
        // TOGGLE  (the one shortcut that reliably reaches us)
        // =====================================================

        /// <summary>
        /// Freezes the selection, or unfreezes it if any of it is already
        /// frozen. Bound to Ctrl+Shift+F, because that is the only
        /// combination Grasshopper reliably lets through to the canvas.
        /// </summary>
        public static void ToggleFreezeClicked(object sender, EventArgs e)
        {
            if (!ShouldRun("toggle"))
                return;

            GH_Document document = ResolveDocument(sender);

            if (document == null)
                return;

            // Every selected object is already frozen, so the user is asking
            // for the opposite. A MIXED selection still freezes: unlocking
            // something by accident is the expensive mistake, not locking it.
            if (FreezeManager.SelectionAllFrozen(document))
            {
                int count = FreezeManager.UnfreezeSelection(document);

                Log.Debug($"Unfroze {count} selected object(s). {SecurityManager.FrozenCount} still protected.");
            }
            else
            {
                int count = FreezeManager.FreezeSelection(document);

                if (count == 0)
                {
                    Log.Info("Nothing selected. Select objects first, or use the GHShield menu.");
                    return;
                }

                Log.Debug($"Froze {count} selected object(s). {SecurityManager.FrozenCount} protected in total.");
            }

            Finish(document);
        }

        // =====================================================
        // FREEZE
        // =====================================================

        public static void FreezeClicked(object sender, EventArgs e)
        {
            // A keystroke can arrive through both the message filter and the
            // canvas KeyDown handler. Whichever gets there first wins; the
            // second is swallowed here rather than running the command twice.
            if (!ShouldRun("freeze"))
                return;

            GH_Document document = ResolveDocument(sender);

            if (document == null)
                return;

            int count = FreezeManager.FreezeSelection(document);

            if (count == 0)
            {
                Log.Info("Nothing selected - select the objects to freeze first.");
                return;
            }

            Log.Debug($"Froze {count} selected object(s). {SecurityManager.FrozenCount} protected in total.");

            Finish(document);
        }

        // =====================================================
        // UNFREEZE
        // =====================================================

        public static void UnfreezeClicked(object sender, EventArgs e)
        {
            if (!ShouldRun("unfreeze"))
                return;

            GH_Document document = ResolveDocument(sender);

            if (document == null)
                return;

            int count = FreezeManager.UnfreezeSelection(document);

            if (count > 0)
            {
                Log.Debug($"Unfroze {count} selected object(s). {SecurityManager.FrozenCount} still protected.");
                Finish(document);
                return;
            }

            // Nothing selected: fall back to clearing the whole document.
            // Without this the user can get locked out of their own file.
            count = FreezeManager.UnfreezeAll(document);

            if (count == 0)
            {
                Log.Info("Nothing is frozen in this document.");
                return;
            }

            Log.Info($"Nothing selected - unfroze all {count} protected object(s).");

            Finish(document);
        }

        // =====================================================
        // UNFREEZE EVERYTHING
        // =====================================================

        public static void UnfreezeAllClicked(object sender, EventArgs e)
        {
            GH_Document document = ResolveDocument(sender);

            if (document == null)
                return;

            int count = FreezeManager.UnfreezeAll(document);

            if (count == 0)
            {
                Log.Info("Nothing is frozen in this document.");
                return;
            }

            Log.Info($"Unfroze all {count} protected object(s).");

            Finish(document);
        }

        // =====================================================
        // REPORT
        // =====================================================

        public static void ReportClicked(object sender, EventArgs e)
        {
            GH_Document document = ResolveDocument(sender);

            if (document == null)
                return;

            int selected = 0;
            int frozenInDocument = 0;

            foreach (IGH_DocumentObject obj in document.Objects)
            {
                if (obj == null)
                    continue;

                if (SecurityManager.IsFrozen(obj))
                {
                    frozenInDocument++;
                    Log.Info($"  frozen: {obj.NickName}");
                }

                if (obj.Attributes != null && obj.Attributes.Selected)
                    selected++;
            }

            Log.Info(
                $"{frozenInDocument} frozen in this document, " +
                $"{SecurityManager.FrozenCount} known in total, " +
                $"{selected} currently selected.");
        }

        // =====================================================
        // PIN
        // =====================================================

        public static void PinClicked(object sender, EventArgs e)
        {
            if (!ShouldRun("pin"))
                return;

            GH_Document document = ResolveDocument(sender);

            if (document == null)
                return;

            int count = FreezeManager.PinSelection(document);

            if (count == 0)
            {
                Log.Info("nothing selected to pin.");
                return;
            }

            Log.Debug($"Pinned {count} object(s).");

            Finish(document);
        }

        public static void UnpinClicked(object sender, EventArgs e)
        {
            if (!ShouldRun("unpin"))
                return;

            GH_Document document = ResolveDocument(sender);

            if (document == null)
                return;

            int count = FreezeManager.UnpinSelection(document);

            if (count == 0)
            {
                Log.Info("nothing pinned in the selection.");
                return;
            }

            Log.Debug($"Unpinned {count} object(s).");

            Finish(document);
        }

        // =====================================================
        // VERBOSE LOGGING
        // =====================================================

        public static void ToggleVerboseClicked(object sender, EventArgs e)
        {
            if (!ShouldRun("verbose"))
                return;

            Log.Verbose = !Log.Verbose;

            Log.Info("verbose logging " + (Log.Verbose ? "ON." : "OFF."));
        }
    }
}
