using System;
using System.Windows.Forms;

using Grasshopper;

using GHShield.Core;
using GHShield.Managers;

namespace GHShield.UI
{
    /// <summary>
    /// Adds a GHShield menu to the Grasshopper window's menu bar.
    ///
    /// This is the reliable route to every command. Keyboard shortcuts can be
    /// intercepted by the host; a menu item cannot. It is also how the user
    /// discovers that the commands exist at all.
    /// </summary>
    public static class GHShieldMenu
    {
        private static bool _installed;
        private static Timer _retryTimer;
        private static int _attempts;
        private static ToolStripMenuItem _verboseItem;

        public static void Install()
        {
            if (_installed)
                return;

            if (TryInstall())
                return;

            // Grasshopper's editor window does not exist during PriorityLoad.
            // Poll briefly until it does, then give up quietly.
            _retryTimer = new Timer { Interval = 1000 };

            _retryTimer.Tick += (sender, e) =>
            {
                _attempts++;

                if (TryInstall() || _attempts > 60)
                    StopRetrying();
            };

            _retryTimer.Start();
        }

        private static void StopRetrying()
        {
            if (_retryTimer == null)
                return;

            _retryTimer.Stop();
            _retryTimer.Dispose();
            _retryTimer = null;
        }

        private static bool TryInstall()
        {
            try
            {
                Form editor = Instances.DocumentEditor;

                if (editor == null || editor.IsDisposed)
                    return false;

                MenuStrip menuStrip = editor.MainMenuStrip;

                if (menuStrip == null)
                    return false;

                if (menuStrip.InvokeRequired)
                    return false;

                ToolStripMenuItem root = new ToolStripMenuItem("GHShield");

                root.DropDownItems.Add(
                    Item("GHShield Panel...", null,
                        GHShieldPanel.ClickedFromMenu));

                root.DropDownItems.Add(new ToolStripSeparator());

                root.DropDownItems.Add(
                    Item("Freeze Selection", "Ctrl+Shift+F",
                        ContextMenuManager.FreezeClicked));

                root.DropDownItems.Add(
                    Item("Unfreeze Selection", "Ctrl+Shift+U",
                        ContextMenuManager.UnfreezeClicked));

                root.DropDownItems.Add(
                    Item("Unfreeze Everything", null,
                        ContextMenuManager.UnfreezeAllClicked));

                root.DropDownItems.Add(new ToolStripSeparator());

                root.DropDownItems.Add(
                    Item("Toggle Freeze on Selection", "Ctrl+Shift+F",
                        ContextMenuManager.ToggleFreezeClicked));

                root.DropDownItems.Add(new ToolStripSeparator());

                root.DropDownItems.Add(
                    Item("Show Report...", null,
                        ReportDialog.Show));

                root.DropDownItems.Add(
                    Item("Set Protection Owner...", null,
                        OwnerPrompt.Show));

                root.DropDownItems.Add(new ToolStripSeparator());

                _verboseItem = Item("Verbose Logging (diagnostics)", null,
                    ContextMenuManager.ToggleVerboseClicked);

                _verboseItem.CheckOnClick = false;
                _verboseItem.Checked = Log.Verbose;

                root.DropDownItems.Add(_verboseItem);

                root.DropDownOpening += (sender, e) =>
                {
                    if (_verboseItem != null)
                        _verboseItem.Checked = Log.Verbose;

                };

                menuStrip.Items.Add(root);

                _installed = true;

                Log.Debug("Menu installed.");

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug($"Menu install attempt failed: {ex.Message}");
                return false;
            }
        }

        private static ToolStripMenuItem Item(
            string text,
            string shortcutText,
            EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);

            if (!string.IsNullOrEmpty(shortcutText))
            {
                // Display only. The actual shortcut is handled by
                // ShortcutFilter, so registering it here too would
                // double-fire the command.
                item.ShortcutKeyDisplayString = shortcutText;
            }

            item.Click += handler;

            return item;
        }
    }
}
