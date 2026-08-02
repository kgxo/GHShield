using Grasshopper.Kernel;

namespace GHShield.Security
{
    public static class MoveGuard
    {
        public static bool CanMove(IGH_DocumentObject obj)
        {
            if (obj == null)
                return true;

            // Temporary
            return true;
        }
    }
}