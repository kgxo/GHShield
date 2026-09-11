using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

using GHShield.Core;

namespace GHShield.Managers
{
    /// <summary>
    /// Operations layer on top of SecurityManager.
    ///
    /// SecurityManager knows about single objects. FreezeManager knows about
    /// user-level actions: freeze a selection, freeze a group and everything
    /// inside it, ask whether an object is protected by association.
    /// </summary>
    public static class FreezeManager
    {
        /// <summary>
        /// The object under the cursor when the left button went down.
        /// Cleared on mouse-up so a stale value cannot block later drags.
        /// </summary>
        public static IGH_DocumentObject LastClickedObject;

        // =====================================================
        // FREEZE
        // =====================================================

        public static void Freeze(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            SecurityManager.Freeze(obj);

            GH_Group group = obj as GH_Group;

            if (group == null)
                return;

            foreach (IGH_DocumentObject child in group.ObjectsRecursive())
            {
                if (child != null)
                    SecurityManager.Freeze(child);
            }
        }

        // =====================================================
        // UNFREEZE
        // =====================================================

        public static void Unfreeze(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            SecurityManager.Unfreeze(obj);

            GH_Group group = obj as GH_Group;

            if (group == null)
                return;

            foreach (IGH_DocumentObject child in group.ObjectsRecursive())
            {
                if (child != null)
                    SecurityManager.Unfreeze(child);
            }
        }

        // =====================================================
        // SELECTION HELPERS
        // =====================================================

        public static int FreezeSelection(GH_Document document)
        {
            if (document == null)
                return 0;

            int count = 0;

            foreach (IGH_DocumentObject obj in document.SelectedObjects())
            {
                if (obj == null)
                    continue;

                Freeze(obj);
                count++;
            }

            return count;
        }

        public static int UnfreezeSelection(GH_Document document)
        {
            if (document == null)
                return 0;

            int count = 0;
            int sealedInGroup = 0;

            List<IGH_DocumentObject> selection =
                new List<IGH_DocumentObject>();

            foreach (IGH_DocumentObject obj in document.SelectedObjects())
            {
                if (obj != null)
                    selection.Add(obj);
            }

            foreach (IGH_DocumentObject obj in selection)
            {
                if (obj == null)
                    continue;

                // A member of a frozen group stays frozen unless the group is
                // being unfrozen too. Letting one component out would let it
                // move, and a group's outline follows its members - so the
                // "frozen" group would change shape.
                GH_Group owner = FrozenGroupOf(obj);

                if (owner != null && !selection.Contains(owner))
                {
                    sealedInGroup++;
                    continue;
                }

                Unfreeze(obj);
                count++;
            }

            if (sealedInGroup > 0)
            {
                Log.Info(sealedInGroup == 1
                    ? "1 object is sealed inside a frozen group - unfreeze the group instead."
                    : $"{sealedInGroup} objects are sealed inside a frozen group - unfreeze the group instead.");
            }

            return count;
        }

        /// <summary>
        /// Escape hatch: clears protection from every object in the document.
        /// Bound to Ctrl+Shift+U when nothing is selected, so it is always
        /// possible to recover a definition during development.
        /// </summary>
        public static int UnfreezeAll(GH_Document document)
        {
            if (document == null)
                return 0;

            int count = 0;

            foreach (IGH_DocumentObject obj in document.Objects)
            {
                if (obj == null)
                    continue;

                if (!SecurityManager.IsFrozen(obj))
                    continue;

                SecurityManager.Unfreeze(obj);
                count++;
            }

            return count;
        }

        // =====================================================
        // PIN
        // =====================================================

        public static int PinSelection(GH_Document document)
        {
            if (document == null)
                return 0;

            int count = 0;

            foreach (IGH_DocumentObject obj in document.SelectedObjects())
            {
                if (obj == null || SecurityManager.IsFrozen(obj))
                    continue;

                SecurityManager.Pin(obj);
                count++;
            }

            return count;
        }

        public static int UnpinSelection(GH_Document document)
        {
            if (document == null)
                return 0;

            int count = 0;

            foreach (IGH_DocumentObject obj in document.SelectedObjects())
            {
                if (obj == null || !SecurityManager.IsPinned(obj))
                    continue;

                SecurityManager.Unpin(obj);
                count++;
            }

            return count;
        }

        public static bool IsPinned(IGH_DocumentObject obj)
        {
            return SecurityManager.IsPinned(obj);
        }

        /// <summary>Frozen or pinned - either way it stays where it is.</summary>
        public static bool IsHeld(IGH_DocumentObject obj)
        {
            return SecurityManager.IsHeld(obj);
        }

        // =====================================================
        // GROUP MEMBERSHIP
        // =====================================================

        /// <summary>
        /// The frozen group this object sits inside, if any.
        ///
        /// A group's outline is drawn from wherever its members happen to be,
        /// so one member moving changes the shape of the group. That makes a
        /// frozen group only as fixed as its loosest member - which is why a
        /// member cannot be unfrozen on its own while the group is frozen.
        /// </summary>
        public static GH_Group FrozenGroupOf(IGH_DocumentObject obj)
        {
            if (obj == null || obj is GH_Group)
                return null;

            GH_Document document = obj.OnPingDocument();

            if (document == null)
                return null;

            foreach (IGH_DocumentObject candidate in document.Objects)
            {
                GH_Group group = candidate as GH_Group;

                if (group == null || !SecurityManager.IsFrozen(group))
                    continue;

                foreach (IGH_DocumentObject child in group.ObjectsRecursive())
                {
                    if (child != null && child.InstanceGuid == obj.InstanceGuid)
                        return group;
                }
            }

            return null;
        }

        public static int MemberCount(GH_Group group)
        {
            if (group == null)
                return 0;

            int count = 0;

            foreach (IGH_DocumentObject child in group.ObjectsRecursive())
            {
                if (child != null && !(child is GH_Group))
                    count++;
            }

            return count;
        }

        // =====================================================
        // QUERIES
        // =====================================================

        public static bool IsFrozen(IGH_DocumentObject obj)
        {
            return SecurityManager.IsFrozen(obj);
        }

        /// <summary>
        /// True if the object itself is frozen, or - for a group - if
        /// anything inside it is. Dragging a group must not drag frozen
        /// children along with it.
        /// </summary>
        public static bool IsProtected(IGH_DocumentObject obj)
        {
            if (obj == null)
                return false;

            if (SecurityManager.IsFrozen(obj))
                return true;

            GH_Group group = obj as GH_Group;

            if (group == null)
                return false;

            foreach (IGH_DocumentObject child in group.ObjectsRecursive())
            {
                if (child != null && SecurityManager.IsFrozen(child))
                    return true;
            }

            return false;
        }

        /// <summary>True if any object in the current selection is frozen.</summary>
        public static bool SelectionContainsFrozen(GH_Document document)
        {
            if (document == null)
                return false;

            foreach (IGH_DocumentObject obj in document.SelectedObjects())
            {
                if (obj != null && IsProtected(obj))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True when there is a selection and every object in it is frozen.
        ///
        /// This is what makes one key safe to use for both directions: a
        /// mixed selection always freezes, so the shortcut can never quietly
        /// unlock something the user meant to lock.
        /// </summary>
        public static bool SelectionAllFrozen(GH_Document document)
        {
            if (document == null)
                return false;

            int selected = 0;

            foreach (IGH_DocumentObject obj in document.SelectedObjects())
            {
                if (obj == null)
                    continue;

                selected++;

                if (!IsProtected(obj))
                    return false;
            }

            return selected > 0;
        }

        // =====================================================
        // LAST CLICKED
        // =====================================================

        public static void FreezeLast()
        {
            if (LastClickedObject != null)
                Freeze(LastClickedObject);
        }
    }
}
