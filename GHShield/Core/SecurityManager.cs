using System;
using System.Collections.Generic;
using System.Drawing;

using GH_IO.Serialization;

using Grasshopper.GUI.Base;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace GHShield.Core
{
    /// <summary>
    /// The protection registry.
    ///
    /// Everything is keyed by IGH_DocumentObject.InstanceGuid rather than by
    /// object reference. See SecurityObject for why that matters.
    /// </summary>
    public static class SecurityManager
    {
        // ==================================================
        // REGISTRY
        // ==================================================

        private static readonly Dictionary<Guid, SecurityObject> Objects =
            new Dictionary<Guid, SecurityObject>();

        /// <summary>
        /// Reverse lookup for slider events: GH_SliderBase.ValueChanged
        /// reports the slider widget as its sender, not the component that
        /// owns it, so we need a way back to the GH_NumberSlider.
        /// </summary>
        private static readonly Dictionary<GH_SliderBase, GH_NumberSlider> SliderOwners =
            new Dictionary<GH_SliderBase, GH_NumberSlider>();

        // ==================================================
        // RE-ENTRANCY GUARDS
        // ==================================================

        private static readonly HashSet<Guid> RestoringObjects =
            new HashSet<Guid>();

        private static readonly HashSet<GH_SliderBase> RestoringSliders =
            new HashSet<GH_SliderBase>();

        // ==================================================
        // LOOKUP
        // ==================================================

        /// <summary>Finds an entry. Never creates one.</summary>
        private static SecurityObject Find(Guid id)
        {
            SecurityObject so;

            return Objects.TryGetValue(id, out so)
                ? so
                : null;
        }

        private static SecurityObject Find(IGH_DocumentObject obj)
        {
            if (obj == null)
                return null;

            return Find(obj.InstanceGuid);
        }

        /// <summary>
        /// Finds an entry, creating it if absent.
        ///
        /// Only call this from a write path. Read paths use Find(), otherwise
        /// every permission query would silently grow the registry.
        /// </summary>
        private static SecurityObject GetOrCreate(IGH_DocumentObject obj)
        {
            if (obj == null)
                return null;

            Guid id = obj.InstanceGuid;

            SecurityObject so;

            if (!Objects.TryGetValue(id, out so))
            {
                so = new SecurityObject { Id = id };
                Objects[id] = so;
            }

            so.DisplayName = obj.NickName;

            return so;
        }

        public static SecurityObject GetSecurityObject(IGH_DocumentObject obj)
        {
            return Find(obj);
        }

        public static int FrozenCount
        {
            get
            {
                int count = 0;

                foreach (SecurityObject so in Objects.Values)
                {
                    if (so.IsFrozen)
                        count++;
                }

                return count;
            }
        }

        public static void Clear()
        {
            Objects.Clear();
            SliderOwners.Clear();
            RestoringObjects.Clear();
            RestoringSliders.Clear();
        }

        // ==================================================
        // PERSISTENCE
        // ==================================================

        /// <summary>
        /// The Guids of every protected object, for writing into the .gh file.
        /// </summary>
        public static List<Guid> ExportFrozenIds()
        {
            List<Guid> ids = new List<Guid>();

            foreach (KeyValuePair<Guid, SecurityObject> pair in Objects)
            {
                if (pair.Value != null && pair.Value.IsFrozen)
                    ids.Add(pair.Key);
            }

            return ids;
        }

        /// <summary>
        /// Marks a Guid as protected before its object exists.
        ///
        /// This is what makes loading work: the manifest is deserialized
        /// before the objects it refers to have been added to the document,
        /// so protection is recorded first and adopted by each object as it
        /// appears, via ReattachIfFrozen.
        /// </summary>
        public static void RegisterFrozen(Guid id)
        {
            if (id == Guid.Empty)
                return;

            SecurityObject so;

            if (!Objects.TryGetValue(id, out so))
            {
                so = new SecurityObject { Id = id };
                Objects[id] = so;
            }

            so.IsFrozen = true;

            so.CanMove = false;
            so.CanDelete = false;
            so.CanCopy = false;
            so.CanEdit = false;
        }

        // ==================================================
        // FREEZE
        // ==================================================

        public static void Freeze(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            SecurityObject so = GetOrCreate(obj);

            if (so == null)
                return;

            if (so.IsFrozen)
                return;

            // ----------------------------------------------
            // CAPTURE STATE BEFORE LOCKING ANYTHING DOWN
            // ----------------------------------------------

            byte[] snapshot = CaptureObjectState(obj);

            if (snapshot != null && snapshot.Length > 0)
                so.FrozenState = snapshot;

            if (obj.Attributes != null)
            {
                so.FrozenPivot = obj.Attributes.Pivot;
                so.HasFrozenPivot = true;
            }

            // ----------------------------------------------
            // PERMISSIONS
            // ----------------------------------------------

            so.IsFrozen = true;

            so.CanMove = false;
            so.CanDelete = false;
            so.CanCopy = false;
            so.CanEdit = false;

            Attach(obj, so);

            Log.Debug($"'{obj.NickName}' frozen.");
        }

        // ==================================================
        // UNFREEZE
        // ==================================================

        public static void Unfreeze(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            SecurityObject so = Find(obj);

            if (so == null)
                return;

            Detach(obj);

            so.IsFrozen = false;

            so.CanMove = true;
            so.CanDelete = true;
            so.CanCopy = true;
            so.CanEdit = true;

            so.FrozenState = null;
            so.HasFrozenPivot = false;
            so.FrozenSliderValue = null;
            so.IsRestoring = false;

            RestoringObjects.Remove(so.Id);

            // Drop the entry entirely unless it carries ownership metadata,
            // so the registry does not accumulate dead Guids.
            if (!so.HasStandingMetadata)
                Objects.Remove(so.Id);

            Log.Debug($"'{obj.NickName}' unfrozen.");
        }

        // ==================================================
        // PIN
        // ==================================================
        //
        // Pinning deliberately does NOT install an attributes proxy. The proxy
        // exists to swallow interaction, and a pinned object is meant to stay
        // interactive - the only thing taken away is the ability to move it or
        // delete it. Position is enforced afterwards instead, by pushing the
        // pivot back whenever something moves it.

        public static void Pin(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            SecurityObject so = GetOrCreate(obj);

            if (so == null)
                return;

            // Frozen is strictly stronger. Pinning a frozen object would be a
            // downgrade dressed up as an addition.
            if (so.IsFrozen || so.IsPinned)
                return;

            if (obj.Attributes != null)
            {
                so.FrozenPivot = obj.Attributes.Pivot;
                so.HasFrozenPivot = true;
            }

            so.IsPinned = true;
            so.DisplayName = obj.NickName;

            so.CanMove = false;
            so.CanDelete = false;
            so.CanCopy = true;
            so.CanEdit = true;

            Log.Debug($"'{obj.NickName}' pinned.");
        }

        public static void Unpin(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            SecurityObject so = Find(obj);

            if (so == null || !so.IsPinned)
                return;

            so.IsPinned = false;

            so.CanMove = true;
            so.CanDelete = true;

            if (!so.IsFrozen)
            {
                so.HasFrozenPivot = false;

                if (!so.HasStandingMetadata)
                    Objects.Remove(so.Id);
            }

            Log.Debug($"'{obj.NickName}' unpinned.");
        }

        /// <summary>Records a pinned Guid read from a saved file.</summary>
        public static void RegisterPinned(Guid id)
        {
            if (id == Guid.Empty)
                return;

            SecurityObject so;

            if (!Objects.TryGetValue(id, out so))
            {
                so = new SecurityObject { Id = id };
                Objects[id] = so;
            }

            so.IsPinned = true;

            so.CanMove = false;
            so.CanDelete = false;
        }

        public static bool IsPinned(IGH_DocumentObject obj)
        {
            return obj != null && IsPinned(obj.InstanceGuid);
        }

        public static bool IsPinned(Guid id)
        {
            SecurityObject so;

            return Objects.TryGetValue(id, out so) && so.IsPinned && !so.IsFrozen;
        }

        /// <summary>Frozen or pinned: either way, it may not be moved or deleted.</summary>
        public static bool IsHeld(IGH_DocumentObject obj)
        {
            return IsFrozen(obj) || IsPinned(obj);
        }

        public static int PinnedCount
        {
            get
            {
                int count = 0;

                foreach (SecurityObject so in Objects.Values)
                {
                    if (so != null && so.IsPinned && !so.IsFrozen)
                        count++;
                }

                return count;
            }
        }

        public static List<Guid> ExportPinnedIds()
        {
            List<Guid> ids = new List<Guid>();

            foreach (KeyValuePair<Guid, SecurityObject> pair in Objects)
            {
                if (pair.Value != null && pair.Value.IsPinned && !pair.Value.IsFrozen)
                    ids.Add(pair.Key);
            }

            return ids;
        }

        /// <summary>
        /// Where a pinned object is anchored, and whether it has an anchor at
        /// all. Used by the pin guard, which enforces position when a drag
        /// finishes rather than during layout.
        /// </summary>
        public static bool TryGetAnchor(IGH_DocumentObject obj, out PointF anchor)
        {
            anchor = PointF.Empty;

            SecurityObject so = Find(obj);

            if (so == null || !so.HasFrozenPivot)
                return false;

            anchor = so.FrozenPivot;

            return true;
        }

        /// <summary>Re-anchors a pinned object to where it currently sits.</summary>
        public static void ReAnchor(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            if (so == null || obj.Attributes == null)
                return;

            so.FrozenPivot = obj.Attributes.Pivot;
            so.HasFrozenPivot = true;
        }

        // ==================================================
        // EVENT WIRING
        // ==================================================

        private static void Attach(IGH_DocumentObject obj, SecurityObject so)
        {
            if (obj == null || so == null)
                return;

            obj.ObjectChanged -= FrozenObjectChanged;
            obj.ObjectChanged += FrozenObjectChanged;

            GH_NumberSlider numberSlider = obj as GH_NumberSlider;

            if (numberSlider != null)
                AttachSlider(numberSlider, so);

            // Blocks editing inside the object's own editor, whatever type it
            // is - including third-party components and the Galapagos Gene
            // Pool, which cannot be referenced at compile time. A no-op if a
            // hand-written adapter is already in place.
            AttributeProxyFactory.Protect(obj);

            // The generated proxy covers almost everything. For the handful of
            // attributes classes it cannot subclass - sealed, generic, or
            // without a usable constructor - fall back to the hand-written
            // adapter for that type, if GHShield has one.
            //
            // This runs at FREEZE time and never before it. An object nobody
            // froze keeps the attributes Grasshopper gave it.
            if (!(obj.Attributes is IGHShieldAttributes))
                GHShield.Hooks.AdapterHook.InstallAdapter(obj);
        }

        private static void Detach(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            obj.ObjectChanged -= FrozenObjectChanged;

            GH_NumberSlider numberSlider = obj as GH_NumberSlider;

            if (numberSlider == null)
                return;

            GH_SliderBase slider = numberSlider.Slider;

            if (slider == null)
                return;

            slider.ValueChanged -= FrozenSlider_ValueChanged;

            SliderOwners.Remove(slider);
            RestoringSliders.Remove(slider);
        }

        /// <summary>
        /// Re-applies protection to an object that Grasshopper has just
        /// rebuilt - after an undo, a redo, or a file reload.
        ///
        /// The rebuilt object keeps its InstanceGuid but is a NEW instance,
        /// so all of GHShield's event subscriptions were lost with the old
        /// one. Without this, Ctrl+Z quietly unfreezes everything.
        /// </summary>
        public static void ReattachIfFrozen(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            SecurityObject so = Find(obj);

            if (so == null)
                return;

            if (!so.IsFrozen)
            {
                if (!so.IsPinned)
                    return;

                // A pinned object needs only its anchor back. Nothing is
                // hooked: the pin is enforced when the mouse lets go.
                so.DisplayName = obj.NickName;

                if (!so.HasFrozenPivot && obj.Attributes != null)
                {
                    so.FrozenPivot = obj.Attributes.Pivot;
                    so.HasFrozenPivot = true;
                }

                Log.Debug($"Pin re-attached to '{obj.NickName}'.");
                return;
            }

            // ADOPTION
            //
            // The entry may have arrived from the saved file, in which case it
            // carries only a Guid. Fill in whatever this instance can tell us
            // now: its position, and a snapshot to restore edits from.
            so.DisplayName = obj.NickName;

            if (!so.HasFrozenPivot && obj.Attributes != null)
            {
                so.FrozenPivot = obj.Attributes.Pivot;
                so.HasFrozenPivot = true;
            }

            if (!so.HasFrozenState)
            {
                byte[] snapshot = CaptureObjectState(obj);

                if (snapshot != null && snapshot.Length > 0)
                    so.FrozenState = snapshot;
            }

            Attach(obj, so);

            Log.Debug($"Protection re-attached to '{obj.NickName}'.");
        }

        public static void Reattach(GH_Document document)
        {
            if (document == null)
                return;

            foreach (IGH_DocumentObject obj in document.Objects)
                ReattachIfFrozen(obj);
        }

        // ==================================================
        // STATE CAPTURE
        // ==================================================

        private static byte[] CaptureObjectState(IGH_DocumentObject obj)
        {
            GH_DocumentObject documentObject = obj as GH_DocumentObject;

            if (documentObject == null)
            {
                Log.Debug($"'{obj.NickName}' does not expose full serialization.");
                return null;
            }

            try
            {
                GH_Archive archive = new GH_Archive();

                GH_IWriter writer =
                    archive.CreateTopLevelNode("GHShieldFrozenObject");

                documentObject.WriteFull(writer);

                return archive.Serialize_Binary();
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not capture '{obj.NickName}': {ex.Message}");
                return null;
            }
        }

        // ==================================================
        // OBJECT CHANGED
        // ==================================================

        private static void FrozenObjectChanged(
            IGH_DocumentObject sender,
            GH_ObjectChangedEventArgs e)
        {
            if (sender == null || e == null)
                return;

            SecurityObject so = Find(sender);

            // Pinned objects are deliberately NOT handled here. Restoring a
            // position from inside a Layout event means reacting to every
            // relayout Grasshopper does for its own reasons - which piled a
            // group of pinned sliders on top of each other. Pinning is
            // enforced from the mouse gesture instead, in DocumentHook.
            if (so == null || !so.IsFrozen)
                return;

            if (RestoringObjects.Contains(so.Id))
                return;

            // Selecting an object is not an edit.
            if (e.Type == GH_ObjectEventType.Selected)
                return;

            // ----------------------------------------------
            // LAYOUT / MOVEMENT
            //
            // Layout fires constantly during normal solving, so restoring
            // the whole object here would be both slow and destructive.
            // Compare the pivot instead and only push back if it moved.
            // ----------------------------------------------

            if (e.Type == GH_ObjectEventType.Layout)
            {
                RestorePivot(sender, so);
                return;
            }

            // ----------------------------------------------
            // USER-EDITABLE CHANGES
            // ----------------------------------------------

            bool protectedChange =
                e.Type == GH_ObjectEventType.NickName ||
                e.Type == GH_ObjectEventType.NickNameAccepted ||
                e.Type == GH_ObjectEventType.Sources ||
                e.Type == GH_ObjectEventType.Enabled ||
                e.Type == GH_ObjectEventType.Preview ||
                e.Type == GH_ObjectEventType.PersistentData ||
                e.Type == GH_ObjectEventType.DataMatching ||
                e.Type == GH_ObjectEventType.DataMapping ||
                e.Type == GH_ObjectEventType.Options ||
                e.Type == GH_ObjectEventType.Custom;

            if (!protectedChange)
                return;

            Log.Debug($"Change blocked on '{sender.NickName}' (type {e.Type}).");

            RestoreObjectState(sender, so);
        }

        // ==================================================
        // PIVOT RESTORE  (cheap path)
        // ==================================================

        private static void RestorePivot(
            IGH_DocumentObject obj,
            SecurityObject so)
        {
            if (!so.HasFrozenPivot)
                return;

            if (obj.Attributes == null)
                return;

            PointF current = obj.Attributes.Pivot;

            // Layout events fire during ordinary solving. If nothing
            // actually moved there is nothing to undo.
            if (Math.Abs(current.X - so.FrozenPivot.X) < 0.01f &&
                Math.Abs(current.Y - so.FrozenPivot.Y) < 0.01f)
            {
                return;
            }

            RestoringObjects.Add(so.Id);
            so.IsRestoring = true;

            try
            {
                obj.Attributes.Pivot = so.FrozenPivot;
                obj.Attributes.ExpireLayout();

                if (Grasshopper.Instances.ActiveCanvas != null)
                    Grasshopper.Instances.ActiveCanvas.Refresh();

                Log.Debug($"'{obj.NickName}' pushed back to its frozen position.");
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not restore position of '{obj.NickName}': {ex.Message}");
            }
            finally
            {
                so.IsRestoring = false;
                RestoringObjects.Remove(so.Id);
            }
        }

        // ==================================================
        // FULL RESTORE  (last resort)
        // ==================================================

        private static void RestoreObjectState(
            IGH_DocumentObject obj,
            SecurityObject so)
        {
            if (!so.HasFrozenState)
            {
                Log.Debug($"No frozen snapshot available for '{obj.NickName}'.");
                return;
            }

            GH_DocumentObject documentObject = obj as GH_DocumentObject;

            if (documentObject == null)
                return;

            // ReadFull can hand the object a new InstanceGuid. Since the
            // Guid is our registry key, losing it would orphan the entry.
            Guid instanceGuid = documentObject.InstanceGuid;

            RestoringObjects.Add(so.Id);
            so.IsRestoring = true;

            try
            {
                GH_Archive archive = new GH_Archive();

                if (!archive.Deserialize_Binary(so.FrozenState))
                {
                    Log.Debug($"Could not read frozen state for '{obj.NickName}'.");
                    return;
                }

                documentObject.ReadFull(archive.GetRootNode);

                if (documentObject.InstanceGuid != instanceGuid)
                    documentObject.NewInstanceGuid(instanceGuid);

                so.IsFrozen = true;
                so.CanMove = false;
                so.CanDelete = false;
                so.CanCopy = false;
                so.CanEdit = false;

                Log.Debug($"Frozen state restored for '{obj.NickName}'.");
            }
            catch (Exception ex)
            {
                Log.Error($"Could not restore '{obj.NickName}': {ex.Message}");
            }
            finally
            {
                so.IsRestoring = false;
                RestoringObjects.Remove(so.Id);
            }
        }

        // ==================================================
        // NUMBER SLIDER
        // ==================================================

        private static void AttachSlider(
            GH_NumberSlider numberSlider,
            SecurityObject so)
        {
            GH_SliderBase slider = numberSlider.Slider;

            if (slider == null)
                return;

            // Only capture the value on the first freeze. On a re-attach
            // after undo we must keep the ORIGINAL frozen value, not
            // whatever the rebuilt slider happens to hold.
            if (so.FrozenSliderValue == null)
                so.FrozenSliderValue = slider.Value;

            SliderOwners[slider] = numberSlider;

            slider.ValueChanged -= FrozenSlider_ValueChanged;
            slider.ValueChanged += FrozenSlider_ValueChanged;

            Log.Debug($"Slider '{numberSlider.NickName}' pinned at {so.FrozenSliderValue}.");
        }

        private static void FrozenSlider_ValueChanged(
            object sender,
            GH_SliderEventArgs e)
        {
            GH_SliderBase slider = sender as GH_SliderBase;

            if (slider == null)
                return;

            GH_NumberSlider numberSlider;

            if (!SliderOwners.TryGetValue(slider, out numberSlider))
                return;

            if (numberSlider == null)
                return;

            SecurityObject so = Find(numberSlider);

            if (so == null || !so.IsFrozen)
                return;

            if (so.FrozenSliderValue == null)
                return;

            if (RestoringSliders.Contains(slider))
                return;

            decimal frozenValue = so.FrozenSliderValue.Value;

            if (slider.Value == frozenValue)
                return;

            Log.Debug(
                $"Slider '{numberSlider.NickName}' change blocked " +
                $"({e.Value} -> {frozenValue}).");

            RestoringSliders.Add(slider);

            try
            {
                bool raiseEvents = slider.RaiseEvents;

                slider.RaiseEvents = false;

                try
                {
                    slider.Value = frozenValue;
                }
                finally
                {
                    slider.RaiseEvents = raiseEvents;
                }

                numberSlider.ExpireSolution(true);
            }
            finally
            {
                RestoringSliders.Remove(slider);
            }
        }

        // ==================================================
        // STATE QUERIES
        // ==================================================

        public static bool IsFrozen(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            return so != null && so.IsFrozen;
        }

        public static bool IsFrozen(Guid id)
        {
            SecurityObject so = Find(id);

            return so != null && so.IsFrozen;
        }

        // ==================================================
        // LOCK
        // ==================================================

        public static void Lock(IGH_DocumentObject obj)
        {
            SecurityObject so = GetOrCreate(obj);

            if (so != null)
                so.IsLocked = true;
        }

        public static void Unlock(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            if (so == null)
                return;

            so.IsLocked = false;

            if (!so.IsFrozen && !so.HasStandingMetadata)
                Objects.Remove(so.Id);
        }

        public static bool IsLocked(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            return so != null && so.IsLocked;
        }

        // ==================================================
        // PERMISSIONS
        // ==================================================

        public static bool CanMove(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            if (so == null)
                return true;

            if (so.IsFrozen || so.IsLocked)
                return false;

            return so.CanMove;
        }

        public static bool CanDelete(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            if (so == null)
                return true;

            if (so.IsFrozen || so.IsLocked)
                return false;

            return so.CanDelete;
        }

        public static bool CanCopy(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            if (so == null)
                return true;

            if (so.IsFrozen || so.IsLocked)
                return false;

            return so.CanCopy;
        }

        public static bool CanEdit(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            if (so == null)
                return true;

            if (so.IsFrozen || so.IsLocked)
                return false;

            return so.CanEdit;
        }

        public static bool CanModify(IGH_DocumentObject obj)
        {
            return CanEdit(obj);
        }

        // ==================================================
        // PASSWORD
        // ==================================================

        public static void SetPassword(IGH_DocumentObject obj, string password)
        {
            SecurityObject so = GetOrCreate(obj);

            if (so != null)
                so.Password = password;
        }

        public static bool HasPassword(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            return so != null &&
                   !string.IsNullOrWhiteSpace(so.Password);
        }

        public static bool CheckPassword(IGH_DocumentObject obj, string password)
        {
            SecurityObject so = Find(obj);

            if (so == null)
                return false;

            return string.Equals(so.Password, password, StringComparison.Ordinal);
        }

        // ==================================================
        // OWNER
        // ==================================================

        public static void SetOwner(IGH_DocumentObject obj, string owner)
        {
            SecurityObject so = GetOrCreate(obj);

            if (so != null)
                so.Owner = owner;
        }

        public static string GetOwner(IGH_DocumentObject obj)
        {
            SecurityObject so = Find(obj);

            return so == null ? null : so.Owner;
        }
    }
}
