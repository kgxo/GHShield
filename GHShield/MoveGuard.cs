using Grasshopper.Kernel;

using GHShield.Core;

namespace GHShield.Security
{
    /// <summary>
    /// Thin convenience wrapper kept for call sites that only care about
    /// movement. Delegates to ProtectionService so group membership is
    /// taken into account.
    /// </summary>
    public static class MoveGuard
    {
        public static bool CanMove(IGH_DocumentObject obj)
        {
            return ProtectionService.CanMove(obj);
        }
    }
}
