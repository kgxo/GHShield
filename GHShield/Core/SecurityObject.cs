using Grasshopper.Kernel;

namespace GHShield.Core
{
    public class SecurityObject
    {
        public IGH_DocumentObject Object { get; set; }

        public bool IsFrozen { get; set; }

        public bool IsLocked { get; set; }

        public bool CanMove { get; set; } = true;

        public bool CanDelete { get; set; } = true;

        public bool CanCopy { get; set; } = true;

        public bool CanEdit { get; set; } = true;

        public string Password { get; set; }

        public string Owner { get; set; }

        public string Notes { get; set; }
    }
}