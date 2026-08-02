using GHShield.Hooks;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino;

namespace GHShield
{
    public class Startup : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            RhinoApp.WriteLine("GHShield Loaded");

            DocumentHook.Initialize();

            return GH_LoadingInstruction.Proceed;
        }
    }
}