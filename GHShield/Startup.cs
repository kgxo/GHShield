using System;
using System.IO;
using System.Reflection;

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
        // =====================================================
        // FINDING 0Harmony.dll
        // =====================================================
        //
        // Grasshopper has a setting - "Memory load *.GHA assemblies using COFF
        // byte arrays" - which loads each .gha from memory instead of from
        // disk. When it is on, .NET no longer knows which folder the plugin
        // came from, so it looks for the plugin's helper DLLs in Rhino's own
        // program folder and fails to find them. The symptom is brutal and
        // gives nothing away: "Priority: AssemblyPriority - Exception has been
        // thrown by the target of an invocation."
        //
        // Any plugin that ships a helper DLL hits this; Nautilus fails the
        // same way over its own LDLIB.dll. Rather than tell every user to go
        // and change a setting they have never heard of, GHShield resolves
        // 0Harmony itself, by looking where it would actually have been
        // installed.
        //
        // The handler is registered from a static constructor so it is in
        // place before PriorityLoad is compiled - which is when the runtime
        // first tries to resolve Harmony, and too early for any try/catch
        // inside the method to help.

        static Startup()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveHelper;
            }
            catch
            {
                // Nothing useful to do here, and throwing would take the
                // whole plugin down before it started.
            }
        }

        private static Assembly ResolveHelper(object sender, ResolveEventArgs args)
        {
            try
            {
                string wanted = new AssemblyName(args.Name).Name;

                if (!wanted.Equals("0Harmony", StringComparison.OrdinalIgnoreCase))
                    return null;

                foreach (string folder in CandidateFolders())
                {
                    if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                        continue;

                    string path = Path.Combine(folder, "0Harmony.dll");

                    if (File.Exists(path))
                        return Assembly.LoadFrom(path);
                }
            }
            catch
            {
                // Fall through: returning null just means "not found", and
                // the delete guard degrades instead of the plugin dying.
            }

            return null;
        }

        private static string[] CandidateFolders()
        {
            string beside = null;

            try
            {
                // Empty when the .gha was memory-loaded, which is exactly the
                // case this method exists for - but free when it is not.
                string location = typeof(Startup).Assembly.Location;

                if (!string.IsNullOrEmpty(location))
                    beside = Path.GetDirectoryName(location);
            }
            catch
            {
                // Ignored.
            }

            string appData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);

            return new[]
            {
                beside,
                Path.Combine(appData, @"Grasshopper\Libraries"),
                Path.Combine(appData, @"McNeel\Rhinoceros\packages\7.0\ghshield")
            };
        }

        // =====================================================
        // LOAD
        // =====================================================

        public override GH_LoadingInstruction PriorityLoad()
        {
            // Each step is a lambda rather than a method group on purpose.
            // A method group has to be resolved when THIS method is compiled,
            // before the first line runs - so a missing dependency would
            // escape the try/catch inside Step. A lambda body is compiled
            // when it is invoked, which is inside the guard.
            Step("canvas hooks", () => DocumentHook.Initialize());
            Step("delete guard", () => DeleteGuard.Install());
            Step("keyboard filter", () => ShortcutFilter.Install());
            Step("menu", () => GHShieldMenu.Install());

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
                Log.Error($"{what} could not be initialised: {ex.Message}");
            }
        }
    }
}
