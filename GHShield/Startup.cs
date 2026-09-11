using System;

using Grasshopper.Kernel;

using GHShield.Core;
using GHShield.Hooks;
using GHShield.UI;

namespace GHShield
{
    /// <summary>
    /// Entry point. GH_AssemblyPriority runs before any document is loaded,
    /// which is what lets GHShield attach to canvases as they are created.
    ///
    /// Each step is isolated deliberately. A single shared try/catch meant
    /// that if the delete guard failed - which it will on any machine where
    /// 0Harmony.dll is missing - the menu and shortcuts were never installed
    /// either, and GHShield loaded with no user interface at all.
    ///
    /// Every part degrades on its own now:
    ///   no Harmony  -> deletions are reverted rather than refused
    ///   no filter   -> shortcuts still work through the canvas handler
    ///   no menu     -> the panel is still reachable by double-clicking
    /// </summary>
    public class Startup : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            Step("canvas hooks", DocumentHook.Initialize);
            Step("delete guard", DeleteGuard.Install);
            Step("keyboard filter", ShortcutFilter.Install);
            Step("menu", GHShieldMenu.Install);

            return GH_LoadingInstruction.Proceed;
        }

        private static void Step(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                // A missing dependency surfaces here as the JIT resolves the
                // types, so this catches more than the action's own faults.
                Log.Error($"{what} could not be initialised: {ex.Message}");
            }
        }
    }
}
