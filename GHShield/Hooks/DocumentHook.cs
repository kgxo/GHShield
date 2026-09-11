using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

using GHShield.Core;
using GHShield.Managers;
using GHShield.UI;

namespace GHShield.Hooks
{
    /// <summary>
    /// Canvas and document event interception.
    ///
    /// Grasshopper 7 exposes no universal "may this object be edited?" API,
    /// so protection is assembled from canvas events, document events and
    /// per-component adapters.
    /// </summary>
    public static class DocumentHook
    {
        /// <summary>Stops a deletion rollback from triggering another one.</summary>
        private static bool _rollingBackDeletion;

        /// <summary>The wire-loss warning is only worth showing once.</summary>
        private static bool _warnedAboutWireLoss;

        // =====================================================
        // INITIALIZE
        // =====================================================

        public static void Initialize()
        {
            Instances.CanvasCreated += CanvasCreated;
        }

        // =====================================================
        // CANVAS CREATED
        // =====================================================

        private static void CanvasCreated(GH_Canvas canvas)
        {
            if (canvas == null)
                return;

            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
            canvas.KeyDown += Canvas_KeyDown;

            canvas.DocumentObjectMouseDown += Canvas_DocumentObjectMouseDown;
            canvas.CanvasPostPaintWidgets += Canvas_PostPaintWidgets;
            canvas.Document_ObjectsDeleted += Canvas_Document_ObjectsDeleted;
            canvas.DocumentChanged += Canvas_DocumentChanged;

            // Registered for Rhino 8 / future compatibility. In Rhino 7 the
            // CanDragObject / CanDeleteObject callbacks are not reliably
            // invoked, which is why the event hooks above exist at all.
            canvas.AddValidator(new GHShieldValidator());

            if (canvas.Document != null)
            {
                AdapterHook.Attach(canvas.Document);
                SecurityManager.Reattach(canvas.Document);
            }

            ContextMenuHook.Attach(canvas);

            Log.Info("ready.  Ctrl+Shift+F freezes and unfreezes  |  right-click any object for the rest.");
        }

        // =====================================================
        // DOCUMENT CHANGED
        // =====================================================

        private static void Canvas_DocumentChanged(
            GH_Canvas sender,
            GH_CanvasDocumentChangedEventArgs e)
        {
            if (sender == null || sender.Document == null)
                return;

            Log.Debug("Grasshopper document changed.");

            AdapterHook.Attach(sender.Document);

            // A reloaded or switched document contains fresh object
            // instances. Re-apply protection to any Guid we already know.
            SecurityManager.Reattach(sender.Document);
        }

        // =====================================================
        // OBJECT MOUSE DOWN
        // =====================================================

        private static void Canvas_DocumentObjectMouseDown(
            object sender,
            GH_CanvasObjectMouseDownEventArgs e)
        {
            GH_Canvas canvas = sender as GH_Canvas;

            if (canvas == null || e == null || e.Object == null)
                return;

            IGH_DocumentObject obj = e.Object.Object;

            if (obj == null)
                return;

            if (!FreezeManager.IsProtected(obj))
                return;

            BlockDrag(canvas, obj);
        }

        // =====================================================
        // MOUSE DOWN
        // =====================================================

        private static void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            GH_Canvas canvas = sender as GH_Canvas;

            if (canvas == null)
                return;

            if (e.Button != MouseButtons.Left)
                return;

            GH_Document doc = canvas.Document;

            if (doc == null)
                return;

            IGH_DocumentObject clickedObject = FindObjectAt(canvas, e.Location);

            if (clickedObject == null)
            {
                FreezeManager.LastClickedObject = null;
                return;
            }

            FreezeManager.LastClickedObject = clickedObject;

            Log.Debug($"Clicked '{clickedObject.NickName}'.");

            if (FreezeManager.IsProtected(clickedObject))
            {
                BlockDrag(canvas, clickedObject);
                return;
            }

            // Mixed multi-selection: the clicked object is unprotected, but
            // dragging it would drag a frozen sibling along with it.
            //
            // Do NOT cancel the interaction here. Cancelling kills every kind
            // of interaction, including the in-component drags that an MD
            // Slider, Number Slider or Gradient rely on - so an unfrozen MD
            // Slider became impossible to move whenever a frozen object was
            // also selected. Dropping the frozen objects out of the selection
            // achieves the same protection and leaves the drag alone.
            if (clickedObject.Attributes != null &&
                clickedObject.Attributes.Selected &&
                FreezeManager.SelectionContainsFrozen(doc))
            {
                DeselectFrozen(canvas, doc);
            }
        }

        // =====================================================
        // DESELECT FROZEN
        // =====================================================

        /// <summary>
        /// Drops protected objects out of the current selection, so whatever
        /// Grasshopper does next simply does not include them.
        /// </summary>
        private static void DeselectFrozen(GH_Canvas canvas, GH_Document doc)
        {
            if (doc == null)
                return;

            int dropped = 0;

            foreach (IGH_DocumentObject obj in doc.Objects)
            {
                if (obj == null || obj.Attributes == null)
                    continue;

                if (!obj.Attributes.Selected)
                    continue;

                if (!FreezeManager.IsProtected(obj))
                    continue;

                obj.Attributes.Selected = false;
                dropped++;
            }

            if (dropped == 0)
                return;

            Log.Debug($"Dropped {dropped} protected object(s) from the selection.");

            if (canvas != null)
                canvas.Refresh();
        }

        // =====================================================
        // MOUSE MOVE
        // =====================================================

        private static void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            GH_Canvas canvas = sender as GH_Canvas;

            if (canvas == null || canvas.Document == null)
                return;

            IGH_DocumentObject dragCandidate = FreezeManager.LastClickedObject;

            if (dragCandidate == null)
                return;

            // Only the object actually being dragged is considered. Anything
            // broader cancels interactions that have nothing to do with
            // moving objects - which is what made an unfrozen MD Slider
            // impossible to drag.
            if (FreezeManager.IsProtected(dragCandidate))
                BlockDrag(canvas, dragCandidate);
        }

        // =====================================================
        // MOUSE UP
        // =====================================================

        private static void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            // Critical: without this, a single click on a frozen object
            // would keep blocking every unrelated drag afterwards.
            FreezeManager.LastClickedObject = null;
        }

        // =====================================================
        // WHY THERE IS NO "PUT IT BACK" CODE HERE
        // =====================================================
        //
        // Two attempts were made to hold objects in place by writing their
        // pivot back to a recorded anchor - first from the Layout event, then
        // from mouse-up. Both piled real components into a diagonal heap in
        // the corner of their group. Writing Pivot and calling ExpireLayout
        // is evidently not the symmetric operation it looks like, at least
        // not for every attributes class, and each pass shifted the objects a
        // little further.
        //
        // Position is therefore held the way it always was: by refusing the
        // drag before it starts. That path has been reliable since the
        // beginning, and it cannot move anything, because it never writes a
        // position at all.

        // =====================================================
        // HIT TESTING
        // =====================================================

        /// <summary>
        /// Finds the topmost object under the cursor.
        ///
        /// The mouse position arrives in CONTROL pixels; object bounds live
        /// in CANVAS units. They only agree at 100% zoom with zero pan, so
        /// the point must be unprojected through the viewport first.
        /// </summary>
        private static IGH_DocumentObject FindObjectAt(
            GH_Canvas canvas,
            Point controlPoint)
        {
            if (canvas == null || canvas.Document == null)
                return null;

            PointF canvasPoint;

            try
            {
                canvasPoint = canvas.Viewport.UnprojectPoint(
                    new PointF(controlPoint.X, controlPoint.Y));
            }
            catch
            {
                return null;
            }

            IGH_DocumentObject component = null;
            IGH_DocumentObject group = null;

            float smallest = float.MaxValue;

            foreach (IGH_DocumentObject obj in canvas.Document.Objects)
            {
                if (obj == null || obj.Attributes == null)
                    continue;

                RectangleF bounds = obj.Attributes.Bounds;

                if (!bounds.Contains(canvasPoint))
                    continue;

                // A GROUP'S BOUNDS COVER EVERYTHING INSIDE IT.
                //
                // Treating the group as the clicked object meant that clicking
                // any component inside a group that contained ONE frozen
                // object was read as clicking something protected - so the
                // drag was cancelled and a perfectly free component refused to
                // move. Groups are therefore only the answer when the click
                // landed on no component at all, which is what clicking a
                // group's edge or its empty space actually means.
                if (obj is Grasshopper.Kernel.Special.GH_Group)
                {
                    group = obj;
                    continue;
                }

                // Among overlapping components, the smallest is the one on
                // top as far as the user is concerned - a slider sitting over
                // a scribble, for instance.
                float area = bounds.Width * bounds.Height;

                if (area <= smallest)
                {
                    smallest = area;
                    component = obj;
                }
            }

            return component ?? group;
        }

        // =====================================================
        // BLOCK DRAG
        // =====================================================

        private static void BlockDrag(
            GH_Canvas canvas,
            IGH_DocumentObject obj)
        {
            if (canvas == null)
                return;

            if (obj != null)
                Log.Debug($"'{obj.NickName}' is frozen. Interaction blocked.");

            canvas.BeginInvoke(new Action(() =>
            {
                try
                {
                    canvas.ActiveInteraction = null;
                }
                catch
                {
                    // Nothing useful to do if the interaction already ended.
                }
            }));
        }

        // =====================================================
        // DELETION ROLLBACK
        // =====================================================

        private static void Canvas_Document_ObjectsDeleted(
            GH_Document sender,
            GH_DocObjectEventArgs e)
        {
            if (sender == null || e == null || e.Objects == null)
                return;

            if (_rollingBackDeletion)
                return;

            bool frozenWasDeleted = false;

            foreach (IGH_DocumentObject obj in e.Objects)
            {
                if (obj != null &&
                    (SecurityManager.IsFrozen(obj) ||
                     ManifestManager.IsProtectedManifest(obj)))
                {
                    frozenWasDeleted = true;
                    break;
                }
            }

            if (!frozenWasDeleted)
                return;

            GH_Canvas canvas = Instances.ActiveCanvas;

            // Roll back once the deletion has finished, never during it.
            Action rollback = () =>
            {
                _rollingBackDeletion = true;

                try
                {
                    if (TryUndo(sender))
                    {
                        // Undo restores objects AND their wires. The rebuilt
                        // instances keep their Guids, so ObjectsAdded will
                        // re-attach protection.
                        SecurityManager.Reattach(sender);
                        Log.Info("Deletion of a frozen object was reverted.");
                    }
                    else
                    {
                        ReAddDeletedObjects(sender, e);
                    }

                    if (canvas != null)
                        canvas.Refresh();
                }
                finally
                {
                    _rollingBackDeletion = false;
                }
            };

            if (canvas != null)
                canvas.BeginInvoke(rollback);
            else
                rollback();
        }

        /// <summary>
        /// GH_Document.Undo() is invoked reflectively so that a signature
        /// difference between Grasshopper builds degrades to the fallback
        /// below instead of breaking the build.
        /// </summary>
        private static bool TryUndo(GH_Document document)
        {
            try
            {
                MethodInfo undo = document
                    .GetType()
                    .GetMethod("Undo", Type.EmptyTypes);

                if (undo == null)
                    return false;

                undo.Invoke(document, null);

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug($"Undo-based rollback unavailable: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fallback rollback. Puts the objects back, but Grasshopper has
        /// already discarded their connections, so the definition comes
        /// back wired-up-wrong. Prevention beats this every time.
        /// </summary>
        private static void ReAddDeletedObjects(
            GH_Document document,
            GH_DocObjectEventArgs e)
        {
            foreach (IGH_DocumentObject obj in e.Objects)
            {
                if (obj == null)
                    continue;

                if (!SecurityManager.IsFrozen(obj) &&
                    !ManifestManager.IsProtectedManifest(obj))
                    continue;

                try
                {
                    document.AddObject(obj, false);

                    if (obj.Attributes != null)
                        obj.Attributes.Selected = true;
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not restore '{obj.NickName}': {ex.Message}");
                }
            }

            if (!_warnedAboutWireLoss)
            {
                _warnedAboutWireLoss = true;

                Log.Info(
                    "A deleted frozen object was restored, but its wires could " +
                    "not be recovered. Reconnect it before saving.");
            }
        }

        // =====================================================
        // KEYBOARD
        // =====================================================

        private static void Canvas_KeyDown(object sender, KeyEventArgs e)
        {
            GH_Canvas canvas = sender as GH_Canvas;

            if (canvas == null)
                return;

            GH_Document doc = canvas.Document;

            if (doc == null)
                return;

            // ---------- KEY PROBE ----------
            //
            // Grasshopper claims some modifier combinations before they reach
            // the canvas, and there is no published list of which. With
            // verbose logging on, this prints every modified keystroke that
            // does arrive - so a combination that logs nothing is one the
            // host swallowed, and is not worth binding a command to.

            if (Log.Verbose && (e.Control || e.Alt))
            {
                Log.Debug(
                    $"key reached canvas: Ctrl={e.Control} Shift={e.Shift} " +
                    $"Alt={e.Alt} Key={e.KeyCode}");
            }

            // ---------- DELETE ----------

            if (e.KeyCode == Keys.Delete &&
                (FreezeManager.SelectionContainsFrozen(doc) ||
                 ManifestManager.SelectionContainsProtectedManifest(doc)))
            {
                Log.Info("Selection is protected by GHShield. Delete blocked.");

                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // ---------- CUT / COPY ----------

            if (e.Control &&
                !e.Shift &&
                (e.KeyCode == Keys.X || e.KeyCode == Keys.C) &&
                (FreezeManager.SelectionContainsFrozen(doc) ||
                 ManifestManager.SelectionContainsProtectedManifest(doc)))
            {
                Log.Info(
                    e.KeyCode == Keys.X
                        ? "Selection contains a frozen object. Cut blocked."
                        : "Selection contains a frozen object. Copy blocked.");

                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (!e.Control)
                return;

            // ---------- FREEZE / UNFREEZE:  Ctrl+Shift+F ----------
            //
            // The same command as the message filter runs. These two paths
            // disagreeing is exactly the bug that made Ctrl+Shift+F refuse to
            // unfreeze: whichever path actually received the keystroke decided
            // what the key meant.

            if (e.Shift && !e.Alt && e.KeyCode == Keys.F)
            {
                ContextMenuManager.ToggleFreezeClicked(canvas, EventArgs.Empty);

                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // ---------- UNFREEZE:  Ctrl+Alt+F ----------
            //
            // Bound to several combinations on purpose. Rhino's command line
            // swallows plain letters when it, rather than the Grasshopper
            // canvas, has focus, and which combinations survive is not
            // documented anywhere. F4 was tried and withdrawn: it unfroze
            // correctly but Grasshopper also opened its own command bar on
            // the same keystroke, and a shortcut that does its job AND
            // something else is not a shortcut anyone will trust.

            bool unfreezeKey =
                (e.Alt && !e.Shift && e.KeyCode == Keys.F) ||
                (e.Shift && !e.Alt && (e.KeyCode == Keys.U || e.KeyCode == Keys.R));

            if (unfreezeKey)
            {
                ContextMenuManager.UnfreezeClicked(canvas, EventArgs.Empty);

                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // ---------- VERBOSE LOGGING:  Ctrl+Shift+D ----------

            if (e.Shift && !e.Alt && e.KeyCode == Keys.D)
            {
                ContextMenuManager.ToggleVerboseClicked(canvas, EventArgs.Empty);

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // =====================================================
        // FROZEN VISUAL
        // =====================================================

        private static void Canvas_PostPaintWidgets(GH_Canvas canvas)
        {
            if (canvas == null || canvas.Document == null)
                return;

            Graphics graphics = canvas.Graphics;

            if (graphics == null)
                return;

            foreach (IGH_DocumentObject obj in canvas.Document.Objects)
            {
                if (obj == null || obj.Attributes == null)
                    continue;

                if (SecurityManager.IsPinned(obj))
                {
                    FrozenRenderer.DrawPinned(graphics, obj.Attributes.Bounds);
                    continue;
                }

                if (!SecurityManager.IsFrozen(obj) &&
                    !ManifestManager.IsProtectedManifest(obj))
                    continue;

                FrozenRenderer.Draw(graphics, obj.Attributes.Bounds);
            }

        }
    }
}
