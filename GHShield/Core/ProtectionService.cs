using Grasshopper.Kernel;

using GHShield.Managers;

namespace GHShield.Core
{
    /// <summary>
    /// The decision layer. UI and hooks ask this, never SecurityManager
    /// directly, so richer permission rules can land in one place later.
    ///
    /// Move is the odd one out: it uses IsProtected rather than IsFrozen,
    /// because dragging a group must be refused when any of its children
    /// are frozen, even if the group itself is not.
    /// </summary>
    public static class ProtectionService
    {
        public static bool CanMove(IGH_DocumentObject obj)
        {
            if (obj == null)
                return true;

            return !FreezeManager.IsProtected(obj);
        }

        public static bool CanDelete(IGH_DocumentObject obj)
        {
            if (obj == null)
                return true;

            if (FreezeManager.IsProtected(obj))
                return false;

            return SecurityManager.CanDelete(obj);
        }

        public static bool CanCopy(IGH_DocumentObject obj)
        {
            if (obj == null)
                return true;

            if (FreezeManager.IsProtected(obj))
                return false;

            return SecurityManager.CanCopy(obj);
        }

        public static bool CanCut(IGH_DocumentObject obj)
        {
            return CanCopy(obj) && CanDelete(obj);
        }

        public static bool CanEdit(IGH_DocumentObject obj)
        {
            if (obj == null)
                return true;

            return SecurityManager.CanEdit(obj);
        }
    }
}
