using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;

namespace GHShield.Managers
{
    public static class FreezeManager
    {
        // Last selected component
        public static IGH_DocumentObject LastClickedObject;

        // Frozen components
        private static readonly HashSet<IGH_DocumentObject> FrozenObjects =
            new HashSet<IGH_DocumentObject>();

        // Original positions of frozen components
        public static readonly Dictionary<IGH_DocumentObject, PointF> FrozenPositions =
            new Dictionary<IGH_DocumentObject, PointF>();

        public static void Freeze(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            if (!FrozenObjects.Contains(obj))
            {
                FrozenObjects.Add(obj);

                FrozenPositions[obj] = obj.Attributes.Pivot;
            }
        }

        public static void Unfreeze(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            FrozenObjects.Remove(obj);
            FrozenPositions.Remove(obj);
        }

        public static bool IsFrozen(IGH_DocumentObject obj)
        {
            if (obj == null)
                return false;

            return FrozenObjects.Contains(obj);
        }

        public static void FreezeLast()
        {
            if (LastClickedObject == null)
                return;

            Freeze(LastClickedObject);
        }
    }
}