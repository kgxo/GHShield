using System.Collections.Generic;
using Grasshopper.Kernel;

namespace GHShield.Core
{
    public static class SecurityManager
    {
        private static readonly Dictionary<IGH_DocumentObject, SecurityObject> Objects
            = new Dictionary<IGH_DocumentObject, SecurityObject>();

        private static SecurityObject GetOrCreate(IGH_DocumentObject obj)
        {
            if (obj == null)
                return null;

            if (!Objects.ContainsKey(obj))
            {
                Objects[obj] = new SecurityObject()
                {
                    Object = obj
                };
            }

            return Objects[obj];
        }

        // -----------------------------
        // Freeze
        // -----------------------------

        public static void Freeze(IGH_DocumentObject obj)
        {
            var so = GetOrCreate(obj);

            so.IsFrozen = true;
            so.CanMove = false;
        }

        public static void Unfreeze(IGH_DocumentObject obj)
        {
            var so = GetOrCreate(obj);

            so.IsFrozen = false;
            so.CanMove = true;
        }

        public static bool IsFrozen(IGH_DocumentObject obj)
        {
            if (obj == null)
                return false;

            return Objects.ContainsKey(obj) &&
                   Objects[obj].IsFrozen;
        }

        // -----------------------------
        // Lock
        // -----------------------------

        public static void Lock(IGH_DocumentObject obj)
        {
            var so = GetOrCreate(obj);

            so.IsLocked = true;
        }

        public static void Unlock(IGH_DocumentObject obj)
        {
            var so = GetOrCreate(obj);

            so.IsLocked = false;
        }

        public static bool IsLocked(IGH_DocumentObject obj)
        {
            if (obj == null)
                return false;

            return Objects.ContainsKey(obj) &&
                   Objects[obj].IsLocked;
        }

        // -----------------------------
        // Permissions
        // -----------------------------

        public static bool CanMove(IGH_DocumentObject obj)
        {
            var so = GetOrCreate(obj);

            return so.CanMove;
        }

        public static bool CanDelete(IGH_DocumentObject obj)
        {
            var so = GetOrCreate(obj);

            return so.CanDelete;
        }

        public static bool CanCopy(IGH_DocumentObject obj)
        {
            var so = GetOrCreate(obj);

            return so.CanCopy;
        }

        // -----------------------------
        // Password
        // -----------------------------

        public static void SetPassword(IGH_DocumentObject obj, string password)
        {
            var so = GetOrCreate(obj);

            so.Password = password;
        }

        public static bool HasPassword(IGH_DocumentObject obj)
        {
            var so = GetOrCreate(obj);

            return !string.IsNullOrEmpty(so.Password);
        }

        public static bool CheckPassword(IGH_DocumentObject obj, string password)
        {
            var so = GetOrCreate(obj);

            return so.Password == password;
        }

        // -----------------------------
        // Owner
        // -----------------------------

        public static void SetOwner(IGH_DocumentObject obj, string owner)
        {
            var so = GetOrCreate(obj);

            so.Owner = owner;
        }

        public static string GetOwner(IGH_DocumentObject obj)
        {
            var so = GetOrCreate(obj);

            return so.Owner;
        }
    }
}