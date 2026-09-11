using System;
using System.Drawing;
using System.Windows.Forms;

using Grasshopper;
using Grasshopper.Kernel;

using GHShield.Core;
using GHShield.Managers;

namespace GHShield.UI
{
    /// <summary>
    /// The control panel.
    ///
    /// Keyboard shortcuts turned out to be unreliable inside Rhino 7: the
    /// Rhino command line claims letter keystrokes and starts typing a command
    /// name with them, so Ctrl+Shift+R was quietly launching RECTANGLE rather
    /// than reaching Grasshopper. Buttons cannot be intercepted, so this is
    /// the dependable way to drive GHShield.
    ///
    /// Opened by double-clicking the GHShield component, or from the menu.
    /// </summary>
    public class GHShieldPanel : Form
    {
        private static GHShieldPanel _instance;

        private Label _status;
        private CheckBox _verbose;

        // =====================================================
        // SHOW
        // =====================================================

        public static void ShowPanel()
        {
            try
            {
                if (_instance == null || _instance.IsDisposed)
                    _instance = new GHShieldPanel();

                if (!_instance.Visible)
                {
                    Form owner = Instances.DocumentEditor;

                    if (owner != null && !owner.IsDisposed)
                        _instance.Show(owner);
                    else
                        _instance.Show();
                }

                _instance.UpdateStatus();
                _instance.BringToFront();
                _instance.Activate();
            }
            catch (Exception ex)
            {
                Log.Error($"could not open the panel: {ex.Message}");
            }
        }

        public static void ClickedFromMenu(object sender, EventArgs e)
        {
            ShowPanel();
        }

        // =====================================================
        // LAYOUT
        // =====================================================

        private GHShieldPanel()
        {
            Text = "GHShield";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(250, 232);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 8.25f);

            _status = new Label
            {
                Location = new Point(12, 12),
                Size = new Size(226, 20),
                Text = "-",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            Controls.Add(_status);

            Controls.Add(MakeButton("Freeze Selection", 40, FreezeClicked));
            Controls.Add(MakeButton("Unfreeze Selection", 72, UnfreezeClicked));

            Controls.Add(MakeButton("Unfreeze Everything", 104, UnfreezeAllClicked));
            Controls.Add(MakeButton("Show Report...", 136, ReportClicked));

            _verbose = new CheckBox
            {
                Location = new Point(14, 172),
                Size = new Size(160, 20),
                Text = "Verbose logging",
                Checked = Log.Verbose
            };

            _verbose.CheckedChanged += (s, e) => Log.Verbose = _verbose.Checked;

            ToolTip tips = new ToolTip();

            tips.SetToolTip(_verbose,
                "Prints step-by-step diagnostics to the Rhino command line.\r\n" +
                "Only useful when something is misbehaving. Leave it off.");

            Controls.Add(_verbose);

            Button close = MakeButton("Close", 198, (s, e) => Hide());
            close.Width = 80;
            close.Left = 158;

            Controls.Add(close);
        }

        private Button MakeButton(string text, int top, EventHandler handler)
        {
            Button button = new Button
            {
                Location = new Point(12, top),
                Size = new Size(226, 26),
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter
            };

            button.Click += handler;

            return button;
        }

        // =====================================================
        // COMMANDS
        // =====================================================
        //
        // Sender is null so ContextMenuManager resolves the active canvas
        // itself - the panel is a separate window and has no canvas of its own.

        private void FreezeClicked(object sender, EventArgs e)
        {
            ContextMenuManager.FreezeClicked(null, EventArgs.Empty);
            UpdateStatus();
        }

        private void UnfreezeClicked(object sender, EventArgs e)
        {
            ContextMenuManager.UnfreezeClicked(null, EventArgs.Empty);
            UpdateStatus();
        }

        private void UnfreezeAllClicked(object sender, EventArgs e)
        {
            ContextMenuManager.UnfreezeAllClicked(null, EventArgs.Empty);
            UpdateStatus();
        }

        private void ReportClicked(object sender, EventArgs e)
        {
            ReportDialog.Show(null, EventArgs.Empty);
            UpdateStatus();
        }

        // =====================================================
        // STATUS
        // =====================================================

        public void UpdateStatus()
        {
            try
            {
                int protectedCount = 0;
                int pinnedCount = 0;
                int selected = 0;

                GH_Document document =
                    Instances.ActiveCanvas != null
                        ? Instances.ActiveCanvas.Document
                        : null;

                if (document != null)
                {
                    foreach (IGH_DocumentObject obj in document.Objects)
                    {
                        if (obj == null)
                            continue;

                        if (SecurityManager.IsFrozen(obj))
                            protectedCount++;
                        else if (SecurityManager.IsPinned(obj))
                            pinnedCount++;

                        if (obj.Attributes != null && obj.Attributes.Selected)
                            selected++;
                    }
                }

                _status.Text = pinnedCount > 0
                    ? $"{protectedCount} frozen  ·  {pinnedCount} pinned  ·  {selected} selected"
                    : $"{protectedCount} frozen   ·   {selected} selected";

                _verbose.Checked = Log.Verbose;
            }
            catch (Exception ex)
            {
                Log.Debug($"Panel status update failed: {ex.Message}");
            }
        }

        // =====================================================
        // CLOSING
        // =====================================================

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Keep the instance alive so reopening is instant and the
            // window remembers where the user put it.
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }
    }
}
