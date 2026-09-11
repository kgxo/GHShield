using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;

using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

using GHShield.Managers;

namespace GHShield.Core
{
    /// <summary>
    /// Stops protected objects being deleted at all, rather than putting them
    /// back afterwards.
    ///
    /// WHY HARMONY
    ///
    /// Grasshopper offers no pre-delete event - only ObjectsDeleted, which
    /// fires once the objects are already gone. Reacting to that means
    /// re-adding them, which flickers on screen and cannot reliably restore
    /// their wires. And GH_Document.RemoveObject / RemoveObjects /
    /// RemoveSelection are public but NOT virtual, so there is nothing to
    /// override either.
    ///
    /// The only way to make a deletion simply not happen is to intercept the
    /// call. Harmony is the established way to do that in Rhino plugins.
    ///
    /// The prefixes below run before Grasshopper's own code:
    ///   - a single protected object       -> skip the original entirely
    ///   - a list containing protected ones -> strip them from the list, so
    ///                                         unprotected objects still go
    ///   - delete-selection                -> deselect protected objects first
    ///
    /// The ObjectsDeleted rollback in DocumentHook stays as a backstop for any
    /// route this does not cover.
    /// </summary>
    public static class DeleteGuard
    {
        private const string HarmonyId = "com.oblong.ghshield";

        private static bool _installed;
        private static DateTime _lastReport = DateTime.MinValue;

        // =====================================================
        // INSTALL
        // =====================================================

        public static void Install()
        {
            if (_installed)
                return;

            try
            {
                Harmony harmony = new Harmony(HarmonyId);

                MethodInfo removalPrefix = typeof(DeleteGuard).GetMethod(
                    "RemovalPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);

                MethodInfo selectionPrefix = typeof(DeleteGuard).GetMethod(
                    "RemoveSelectionPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (removalPrefix == null || selectionPrefix == null)
                {
                    Log.Error("delete guard prefixes not found - this is a build problem, not a Harmony one.");
                    return;
                }

                int patched = 0;

                // Both RemoveObject overloads and RemoveObjects. Matched by
                // name and argument count rather than exact signature, so a
                // difference between Grasshopper builds degrades to "not
                // patched" instead of throwing.
                foreach (MethodInfo method in typeof(GH_Document).GetMethods(
                    BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.Name != "RemoveObject" &&
                        method.Name != "RemoveObjects")
                    {
                        continue;
                    }

                    if (method.GetParameters().Length != 2)
                        continue;

                    try
                    {
                        harmony.Patch(method, new HarmonyMethod(removalPrefix));
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Could not patch {method.Name}: {ex.Message}");
                    }
                }

                // ALSO PATCH THE UNDO RECORDER.
                //
                // Grasshopper writes the undo record BEFORE it calls the
                // removal method. Refusing the removal on its own therefore
                // leaves a record saying "these objects were removed, put them
                // back" - and Ctrl+Z then tries to re-add objects that never
                // left, which fails with "An entry with the same key already
                // exists".
                //
                // Stopping the record being written keeps the two halves
                // consistent. The type is resolved from the property rather
                // than named, so a namespace change cannot break it.
                MethodInfo recordPrefix = typeof(DeleteGuard).GetMethod(
                    "RecordRemovePrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);

                PropertyInfo undoUtil = typeof(GH_Document).GetProperty(
                    "UndoUtil",
                    BindingFlags.Public | BindingFlags.Instance);

                if (recordPrefix != null && undoUtil != null)
                {
                    foreach (MethodInfo method in undoUtil.PropertyType.GetMethods(
                        BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (method.Name != "RecordRemoveObjectEvent")
                            continue;

                        if (method.GetParameters().Length != 2)
                            continue;

                        try
                        {
                            harmony.Patch(method, new HarmonyMethod(recordPrefix));
                            patched++;
                        }
                        catch (Exception ex)
                        {
                            Log.Debug($"Could not patch RecordRemoveObjectEvent: {ex.Message}");
                        }
                    }
                }

                MethodInfo removeSelection = typeof(GH_Document).GetMethod(
                    "RemoveSelection",
                    BindingFlags.Public | BindingFlags.Instance);

                if (removeSelection != null)
                {
                    try
                    {
                        harmony.Patch(removeSelection, new HarmonyMethod(selectionPrefix));
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Could not patch RemoveSelection: {ex.Message}");
                    }
                }

                _installed = patched > 0;

                if (_installed)
                    Log.Debug($"Delete guard installed ({patched} interception points).");
                else
                    Log.Info("delete guard could not be installed - deletions will be reverted instead of refused.");
            }
            catch (Exception ex)
            {
                Log.Info($"delete guard unavailable ({ex.Message}) - deletions will be reverted instead of refused.");
            }
        }

        // =====================================================
        // PROTECTION TEST
        // =====================================================

        private static bool IsProtected(IGH_DocumentObject obj)
        {
            if (obj == null)
                return false;

            // Pinned counts here: a layout that can be deleted is not a
            // layout that survives a handover.
            if (SecurityManager.IsHeld(obj))
                return true;

            // The manifest carries the protection list into the saved file.
            return ManifestManager.IsProtectedManifest(obj);
        }

        /// <summary>Rate-limited so a bulk delete cannot spam the command line.</summary>
        private static void Report(IGH_DocumentObject obj)
        {
            DateTime now = DateTime.UtcNow;

            if ((now - _lastReport).TotalMilliseconds < 400)
                return;

            _lastReport = now;

            Log.Info($"'{obj.NickName}' is protected. Delete refused.");
        }

        // =====================================================
        // PREFIXES
        // =====================================================
        //
        // Returning false tells Harmony to skip Grasshopper's own method.
        // __args gives us the arguments without having to name their exact
        // types, which keeps this working across the RemoveObject overloads.

        private static bool RemovalPrefix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length == 0)
                    return true;

                object first = __args[0];

                // RemoveObject(IGH_DocumentObject, bool)
                IGH_DocumentObject single = first as IGH_DocumentObject;

                // RemoveObject(IGH_Attributes, bool)
                if (single == null)
                {
                    IGH_Attributes attributes = first as IGH_Attributes;

                    if (attributes != null)
                        single = attributes.DocObject;
                }

                if (single != null)
                {
                    if (!IsProtected(single))
                        return true;

                    Report(single);
                    return false;
                }

                // RemoveObjects(list, bool) - strip the protected entries so
                // everything else in the selection still gets deleted.
                IList list = first as IList;

                if (list == null)
                    return true;

                bool refusedAny = false;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    IGH_DocumentObject obj = list[i] as IGH_DocumentObject;

                    if (obj == null || !IsProtected(obj))
                        continue;

                    list.RemoveAt(i);
                    refusedAny = true;

                    Report(obj);
                }

                // Nothing left to delete: skip the call entirely.
                if (refusedAny && list.Count == 0)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug($"Delete guard error: {ex.Message}");

                // Never block a deletion because our own guard failed.
                return true;
            }
        }

        /// <summary>
        /// Keeps protected objects out of the undo record.
        ///
        /// Signature is RecordRemoveObjectEvent(string name, object objOrList),
        /// so the payload is the second argument. Stripping protected entries
        /// here often also fixes the removal itself, because Grasshopper
        /// commonly hands the same list straight on to RemoveObjects.
        /// </summary>
        private static bool RecordRemovePrefix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 2)
                    return true;

                object payload = __args[1];

                IGH_DocumentObject single = payload as IGH_DocumentObject;

                if (single != null)
                {
                    // Skip the record entirely for a protected object.
                    return !IsProtected(single);
                }

                IList list = payload as IList;

                if (list == null)
                    return true;

                bool strippedAny = false;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    IGH_DocumentObject obj = list[i] as IGH_DocumentObject;

                    if (obj == null || !IsProtected(obj))
                        continue;

                    list.RemoveAt(i);
                    strippedAny = true;
                }

                if (strippedAny && list.Count == 0)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug($"Undo-record guard error: {ex.Message}");
                return true;
            }
        }

        private static bool RemoveSelectionPrefix(GH_Document __instance)
        {
            try
            {
                if (__instance == null)
                    return true;

                IGH_DocumentObject reported = null;

                foreach (IGH_DocumentObject obj in __instance.Objects)
                {
                    if (obj == null || obj.Attributes == null)
                        continue;

                    if (!obj.Attributes.Selected)
                        continue;

                    if (!IsProtected(obj))
                        continue;

                    // Dropped from the selection, so the deletion that follows
                    // simply does not see it.
                    obj.Attributes.Selected = false;
                    reported = obj;
                }

                if (reported != null)
                    Report(reported);
            }
            catch (Exception ex)
            {
                Log.Debug($"Delete guard error: {ex.Message}");
            }

            return true;
        }
    }
}
