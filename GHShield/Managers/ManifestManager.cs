using System;
using System.Drawing;

using Grasshopper.Kernel;

using GHShield.Components;
using GHShield.Core;

namespace GHShield.Managers
{
    /// <summary>
    /// Keeps exactly one GHShield manifest component in a document that has
    /// anything protected, and keeps its label current.
    /// </summary>
    public static class ManifestManager
    {
        public static GHShieldManifestComponent Find(GH_Document document)
        {
            if (document == null)
                return null;

            foreach (IGH_DocumentObject obj in document.Objects)
            {
                GHShieldManifestComponent manifest =
                    obj as GHShieldManifestComponent;

                if (manifest != null)
                    return manifest;
            }

            return null;
        }

        /// <summary>
        /// Called after any freeze or unfreeze. Adds the manifest the first
        /// time something is protected, and refreshes its count afterwards.
        /// </summary>
        public static void Sync(GH_Document document)
        {
            if (document == null)
                return;

            try
            {
                GHShieldManifestComponent manifest = Find(document);

                if (manifest == null)
                {
                    if (!AnythingProtected(document))
                        return;

                    manifest = Create(document);
                }

                if (manifest != null)
                {
                    // First stamp on this machine: ask who is protecting this,
                    // rather than silently writing the Windows account name
                    // into a file that is about to be sent to a client.
                    if (string.IsNullOrWhiteSpace(manifest.LockedBy))
                        GHShield.UI.OwnerPrompt.EnsureConfigured();

                    // Records who locked it and when, once. Never overwritten,
                    // so a file keeps its original author even if someone else
                    // opens it and protects more.
                    manifest.StampIfUnset();

                    manifest.ExpireSolution(true);
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Manifest sync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// True when this object is the manifest AND the document still has
        /// protected objects.
        ///
        /// The manifest is what carries the protected-Guid list into the saved
        /// file, so deleting it and saving would strip every protection in the
        /// document. It therefore has to be as undeletable as the objects it
        /// describes.
        ///
        /// Note it is guarded rather than frozen. Freezing it would route it
        /// through the ObjectChanged restore path, and this component rewrites
        /// its own Message on every solution - which risks a restore loop.
        /// </summary>
        public static bool IsProtectedManifest(IGH_DocumentObject obj)
        {
            return obj is GHShieldManifestComponent &&
                   SecurityManager.FrozenCount > 0;
        }

        /// <summary>True if the current selection includes the guarded manifest.</summary>
        public static bool SelectionContainsProtectedManifest(GH_Document document)
        {
            if (document == null)
                return false;

            foreach (IGH_DocumentObject obj in document.SelectedObjects())
            {
                if (IsProtectedManifest(obj))
                    return true;
            }

            return false;
        }

        private static bool AnythingProtected(GH_Document document)
        {
            foreach (IGH_DocumentObject obj in document.Objects)
            {
                // Pinned counts: a document with only pins still needs the
                // manifest, because the manifest is what carries the pinned
                // Guids into the saved file.
                if (obj != null && SecurityManager.IsHeld(obj))
                    return true;
            }

            return false;
        }

        private static GHShieldManifestComponent Create(GH_Document document)
        {
            try
            {
                GHShieldManifestComponent manifest =
                    new GHShieldManifestComponent();

                manifest.CreateAttributes();
                manifest.Attributes.Pivot = ChoosePivot(document);

                document.AddObject(manifest, false);

                Log.Info(
                    "added a GHShield component so protection is saved with the file.");

                return manifest;
            }
            catch (Exception ex)
            {
                Log.Error($"could not add the GHShield component: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Places the manifest above and left of the first protected object,
        /// so it sits near what it describes rather than at the canvas origin.
        /// </summary>
        private static PointF ChoosePivot(GH_Document document)
        {
            foreach (IGH_DocumentObject obj in document.Objects)
            {
                if (obj == null || obj.Attributes == null)
                    continue;

                if (!SecurityManager.IsFrozen(obj))
                    continue;

                PointF pivot = obj.Attributes.Pivot;

                return new PointF(pivot.X - 60.0f, pivot.Y - 100.0f);
            }

            return new PointF(0.0f, 0.0f);
        }
    }
}
