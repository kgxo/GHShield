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
                    {
                        Log.Debug($"using Harmony from {path}");

                        return Assembly.LoadFrom(path);
                    }
                }
            }
            catch
            {
                // Fall through: returning null just means "not found", and
                // the delete guard degrades instead of the plugin dying.
            }

            return null;
        }

        /// <summary>
        /// Where 0Harmony.dll may be, in the order to try.
        ///
        /// Harmony ships a different build for each .NET runtime, and the
        /// wrong one fails in a way that looks unrelated: the .NET Framework
        /// build on Rhino 8's modern .NET dies with "Method not found:
        /// ILGenerator.MarkSequencePoint", and every patch - delete guard and
        /// right-click menus alike - silently fails to install. So the package
        /// carries one build per runtime under harmony\<runtime>\, none of
        /// them beside the .gha (where the runtime would pick one up by itself,
        /// right or wrong), and this method points at the matching one.
        /// </summary>
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

            string[] roots =
            {
                beside,
                Path.Combine(appData, @"McNeel\Rhinoceros\packages\8.0\ghshield"),
                Path.Combine(appData, @"McNeel\Rhinoceros\packages\7.0\ghshield"),
                Path.Combine(appData, @"Grasshopper\Libraries")
            };

            bool framework = IsNetFramework();

            string[] builds = framework
                ? new[] { "net48" }
                : Environment.Version.Major >= 8
                    ? new[] { "net8.0", "net6.0" }
                    : new[] { "net6.0", "net8.0" };

            System.Collections.Generic.List<string> folders =
                new System.Collections.Generic.List<string>();

            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root))
                    continue;

                // Installed packages may sit in a version subfolder
                // (packages\8.0\ghshield\1.1.0\), so look one level down too.
                System.Collections.Generic.List<string> bases =
                    new System.Collections.Generic.List<string> { root };

                try
                {
                    if (Directory.Exists(root) && root != beside &&
                        !root.EndsWith("Libraries", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] sub = Directory.GetDirectories(root);
                        Array.Sort(sub);
                        Array.Reverse(sub); // newest version first
                        bases.AddRange(sub);
                    }
                }
                catch
                {
                    // Ignored.
                }

                foreach (string b in bases)
                    foreach (string build in builds)
                        folders.Add(Path.Combine(b, "harmony", build));

                // A loose 0Harmony.dll (a debug build, a hand install in
                // Libraries) is only ever the .NET Framework build, so it is
                // only trusted on .NET Framework.
                if (framework)
                    folders.Add(root);
            }

            return folders.ToArray();
        }

        private static bool IsNetFramework()
        {
            try
            {
                string runtime = System.Runtime.InteropServices
                    .RuntimeInformation.FrameworkDescription ?? string.Empty;

                return runtime.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }

        // =====================================================
        // LOAD
        // =====================================================

        public override GH_LoadingInstruction PriorityLoad()
        {
            // Rhino 7, and Rhino 8 on either of its runtimes.
            //
            // 1.0.0 crashed Rhino 8 as Grasshopper opened; 1.0.1 switched
            // itself off there. From 1.1, Harmony 2.4 (which supports modern
            // .NET) and a runtime-neutral way of emitting the protection
            // adapters let the same build run on both. Anything else - a
            // future Rhino 9 - is still refused until it has been tested.
            if (!IsSupportedRhino())
            {
                Log.Info("this version supports Rhino 7 and 8 only and has switched " +
                         "itself off. Nothing in your files is affected.");

                return GH_LoadingInstruction.Proceed;
            }

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

        /// <summary>
        /// True on the Rhino versions GHShield has been tested on: 7 and 8.
        /// </summary>
        private static bool IsSupportedRhino()
        {
            try
            {
                int major = Rhino.RhinoApp.ExeVersion;

                return major == 7 || major == 8;
            }
            catch
            {
                // If we cannot even tell where we are, do nothing risky.
                return false;
            }
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
