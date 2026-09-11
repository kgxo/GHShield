using System;
using System.Drawing;

using Grasshopper.Kernel;

namespace GHShield
{
    public class GHShieldInfo : GH_AssemblyInfo
    {
        public override string Name => "GHShield";

        public override Bitmap Icon => Core.IconLoader.Load("ShieldIcon24.png");

        public override string Description =>
            "Protection and access control for existing Grasshopper components. " +
            "Frozen objects keep solving and feeding downstream, but can no longer " +
            "be moved, deleted, copied or edited.";

        public override Guid Id => new Guid("9e3a6e69-18e4-4eec-a8ec-b5795e3a39cc");

        public override string AuthorName => "Nipun, ALVA";

        public override string AuthorContact => "alva.built@gmail.com";

        public override string AssemblyVersion =>
            GetType().Assembly.GetName().Version.ToString();
    }
}
