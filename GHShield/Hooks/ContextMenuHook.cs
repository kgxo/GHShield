using System;
using System.Reflection;
using System.Windows.Forms;

using HarmonyLib;

using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

using GHShield.Core;
using GHShield.Managers;

namespace GHShield.Hooks
{
    /// <summary>
    /// Adds "Freeze with GHShield" to the right-click menu of any object that
    /// is not already frozen.
    ///
    /// This is the counterpart to FrozenMenu, which handles the frozen case by
    /// replacing Grasshopper's menu entirely. Here the real menu is left alone
    /// and one item is appended to the end of it.
    ///
    /// Harmony is used because AppendMenuItems is called on every object type
    /// there is, including ones from plugins that were compiled years ago.
    /// Patching the two base implementations reaches all of them at once.
    /// </summary>
    public static class ContextMenuHook
    {
        private const string HarmonyId = "com.oblong.ghshield.menu";
        private const string ItemKey = "GHShieldFreezeItem";
        private const string CanvasKey = "GHShieldCanvasFreezeItem";

        private static bool _installed;

        public static void Attach(GH_Canvas canvas)
        {
            Install();
        }

        public static void Install()
        {
            if (_installed)
                return;

            try
            {
                Harmony harmony = new Harmony(HarmonyId);

                MethodInfo postfix = typeof(ContextMenuHook).GetMethod(
                    "MenuPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (postfix == null)
                {
                    Log.Debug("Context menu postfix not found.");
                    return;
                }

                int patched = 0;

                // GH_DocumentObject declares it; GH_ActiveObject overrides it,
                // and that is what components inherit. An override that calls
                // base would otherwise run the postfix twice - hence the
                // duplicate check inside it.
                // GH_Group declares its own AppendMenuItems and does not go
                // through either base, so right-clicking a group's edge saw
                // nothing from GHShield until it was patched directly.
                Type[] targets =
                {
                    typeof(GH_DocumentObject),
                    typeof(GH_ActiveObject),
                    typeof(GH_Group)
                };

                foreach (Type target in targets)
                {
                    MethodInfo method = target.GetMethod(
                        "AppendMenuItems",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                        null,
                        new[] { typeof(ToolStripDropDown) },
                        null);

                    if (method == null)
                        continue;

                    try
                    {
                        harmony.Patch(method, null, new HarmonyMethod(postfix));
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Could not patch {target.Name}.AppendMenuItems: {ex.Message}");
                    }
                }

                // The canvas's own right-click menu - Preview, Bake, Zoom,
                // Group - is built by GH_Canvas.CanvasOldSchoolMenu. It is
                // where a user right-clicks when they have a selection and
                // are not pointing at any one object, which is exactly when
                // "freeze this lot" is the thing they want.
                try
                {
                    MethodInfo canvasMenu = AccessTools.Method(
                        typeof(GH_Canvas), "CanvasOldSchoolMenu");

                    MethodInfo canvasPostfix = typeof(ContextMenuHook).GetMethod(
                        "CanvasMenuPostfix",
                        BindingFlags.Static | BindingFlags.NonPublic);

                    if (canvasMenu != null &&
                        canvasPostfix != null &&
                        canvasMenu.ReturnType != typeof(void))
                    {
                        harmony.Patch(canvasMenu, null, new HarmonyMethod(canvasPostfix));
                        patched++;
                    }
                    else
                    {
                        Log.Debug("Canvas menu not patchable in this build.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"Could not patch the canvas menu: {ex.Message}");
                }

                _installed = patched > 0;

                Log.Debug($"Context menu patched on {patched} type(s).");
            }
            catch (Exception ex)
            {
                // A missing menu item is a inconvenience; a throwing patch
                // would take Grasshopper's right-click with it.
                Log.Debug($"Context menu hook failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds GHShield's commands to the canvas right-click menu.
        ///
        /// Right-clicking empty canvas is how people act on a whole selection,
        /// and until now that menu was the one place in Grasshopper where
        /// GHShield could not be reached at all.
        /// </summary>
        private static void CanvasMenuPostfix(object __result)
        {
            try
            {
                ToolStrip menu = __result as ToolStrip;

                if (menu == null)
                    return;

                if (menu.Items.Find(CanvasKey, false).Length > 0)
                    return;

                GH_Document document =
                    Instances.ActiveCanvas != null
                        ? Instances.ActiveCanvas.Document
                        : null;

                if (document == null)
                    return;

                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem freeze =
                    new ToolStripMenuItem("Freeze Selection");

                freeze.Name = CanvasKey;
                freeze.Click += ContextMenuManager.FreezeClicked;

                ToolStripMenuItem unfreeze =
                    new ToolStripMenuItem("Unfreeze Selection");

                unfreeze.Click += ContextMenuManager.UnfreezeClicked;

                menu.Items.Add(freeze);
                menu.Items.Add(unfreeze);
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not extend the canvas menu: {ex.Message}");
            }
        }

        private static void MenuPostfix(object __instance, ToolStripDropDown menu)
        {
            try
            {
                IGH_DocumentObject obj = __instance as IGH_DocumentObject;

                if (obj == null || menu == null)
                    return;

                if (obj is Components.GHShieldManifestComponent)
                    return;

                if (menu.Items.Find(ItemKey, false).Length > 0)
                    return;

                GH_Group group = obj as GH_Group;

                ToolStripMenuItem item;

                if (group != null)
                {
                    // A group is a container, not a component: freezing it
                    // means freezing what is inside it. And unlike a single
                    // object, its menu is safe to show either way - Ungroup
                    // and Colour cannot edit the logic - so this item toggles
                    // rather than only offering one direction.
                    int members = Members(group);

                    if (members == 0)
                        return;

                    bool frozen = FreezeManager.IsProtected(group);

                    item = new ToolStripMenuItem(frozen
                        ? $"Unfreeze This Group ({members})"
                        : $"Freeze This Group ({members})");

                    item.Click += (sender, e) => SetGroup(group, !frozen);

                    if (frozen)
                        DisableMembershipItems(menu);
                }
                else
                {
                    // Frozen single objects never reach here - their menu is
                    // replaced wholesale by FrozenMenu, because Grasshopper's
                    // own menu is where they would be edited.
                    if (FreezeManager.IsProtected(obj))
                        return;

                    item = new ToolStripMenuItem("Freeze with GHShield");

                    item.Click += (sender, e) => Freeze(obj);
                }

                item.Name = ItemKey;

                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(item);

            }
            catch (Exception ex)
            {
                Log.Debug($"Could not append the freeze item: {ex.Message}");
            }
        }

        /// <summary>
        /// Greys out the commands that would change which objects belong to a
        /// frozen group.
        ///
        /// A group is drawn around wherever its members are, so ungrouping,
        /// adding or removing members changes the group - and taking a member
        /// out releases it entirely. Colour and the outline styles are left
        /// alone: they change how it looks, not what it holds.
        /// </summary>
        private static void DisableMembershipItems(ToolStripDropDown menu)
        {
            try
            {
                foreach (ToolStripItem entry in menu.Items)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Text))
                        continue;

                    string text = entry.Text.Trim();

                    bool changesMembership =
                        text.Equals("Ungroup", StringComparison.OrdinalIgnoreCase) ||
                        text.Equals("Add to group", StringComparison.OrdinalIgnoreCase) ||
                        text.Equals("Remove from group", StringComparison.OrdinalIgnoreCase);

                    if (changesMembership)
                        entry.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not disable group membership items: {ex.Message}");
            }
        }

        private static int Members(GH_Group group)
        {
            int count = 0;

            try
            {
                foreach (IGH_DocumentObject child in group.ObjectsRecursive())
                {
                    if (child != null && !(child is GH_Group))
                        count++;
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not read group members: {ex.Message}");
            }

            return count;
        }

        private static void SetGroup(GH_Group group, bool freeze)
        {
            // Logged at Info, not Debug, and before anything can fail: if this
            // line never appears, the menu item is not reaching us at all,
            // which is a different problem from the freeze not working.
            Log.Info(freeze
                ? "group menu: freeze requested."
                : "group menu: unfreeze requested.");

            try
            {
                int changed = 0;

                foreach (IGH_DocumentObject child in group.ObjectsRecursive())
                {
                    if (child == null || child is GH_Group)
                        continue;

                    if (child is Components.GHShieldManifestComponent)
                        continue;

                    if (freeze)
                        FreezeManager.Freeze(child);
                    else
                        FreezeManager.Unfreeze(child);

                    changed++;
                }

                // AND THE GROUP ITSELF.
                //
                // Leaving this out is what made "Unfreeze This Group" look
                // broken: every component inside came free, but the group
                // object stayed frozen, so it kept its padlock, the count
                // stayed at 1, and the menu still offered to unfreeze it.
                if (freeze)
                    SecurityManager.Freeze(group);
                else
                    SecurityManager.Unfreeze(group);

                GH_Document document = group.OnPingDocument();

                if (document != null)
                    ManifestManager.Sync(document);

                if (Instances.ActiveCanvas != null)
                    Instances.ActiveCanvas.Refresh();

                Log.Info(freeze
                    ? $"froze {changed} object(s) in '{group.NickName}'."
                    : $"unfroze {changed} object(s) in '{group.NickName}'.");
            }
            catch (Exception ex)
            {
                Log.Error($"could not change the group: {ex.Message}");
            }
        }

        private static void Refresh(GH_Document document)
        {
            if (document != null)
                ManifestManager.Sync(document);

            if (Instances.ActiveCanvas != null)
                Instances.ActiveCanvas.Refresh();
        }

        private static void Freeze(IGH_DocumentObject obj)
        {
            try
            {
                FreezeManager.Freeze(obj);

                GH_Document document = obj.OnPingDocument();

                if (document != null)
                    ManifestManager.Sync(document);

                if (Instances.ActiveCanvas != null)
                    Instances.ActiveCanvas.Refresh();

                Log.Info($"froze '{obj.NickName}'.");
            }
            catch (Exception ex)
            {
                Log.Error($"could not freeze: {ex.Message}");
            }
        }
    }
}
