using System;
using System.Drawing;
using System.Windows.Forms;

using Grasshopper;
using Grasshopper.Kernel;

using GHShield.Components;
using GHShield.Core;
using GHShield.Managers;

namespace GHShield.UI
{
    /// <summary>
    /// Shows the protection report in a window, with a button to copy it.
    ///
    /// It used to print to the Rhino command line. That was the wrong place:
    /// the user is looking at the Grasshopper window, which covers Rhino, so
    /// the report they asked for appeared somewhere they could not see - and
    /// thirty lines of it pushed everything else out of the command history.
    /// This is the same text the GHShield component puts on its Info output,
    /// so there is one wording to maintain and one thing to recognise.
    /// </summary>
    public static class ReportDialog
    {
        public static void Show(object sender, EventArgs e)
        {
            try
            {
                ShowText(BuildText());
            }
            catch (Exception ex)
            {
                Log.Error($"could not build the report: {ex.Message}");
            }
        }

        private static string BuildText()
        {
            GH_Document document =
                Instances.ActiveCanvas != null
                    ? Instances.ActiveCanvas.Document
                    : null;

            if (document == null)
                return "No definition is open.";

            GHShieldManifestComponent manifest = ManifestManager.Find(document);

            if (manifest != null)
                return manifest.Report();

            return "Nothing in this definition is protected.\r\n\r\n" +
                   "Select the objects you want to lock and use Freeze Selection.";
        }

        private static void ShowText(string text)
        {
            using (Form form = new Form())
            {
                form.Text = "GHShield - protection report";
                form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new Size(430, 400);
                form.ShowInTaskbar = false;
                form.Font = new Font("Segoe UI", 8.25f);

                TextBox body = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    WordWrap = false,
                    Dock = DockStyle.Fill,
                    BackColor = Color.White,
                    Font = new Font("Consolas", 9.0f),
                    Text = text.Replace("\n", "\r\n")
                };

                // Right-to-left flow so the buttons stay anchored to the
                // bottom-right corner however the window is resized, without
                // any arithmetic on a width that is not settled yet.
                FlowLayoutPanel buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 42,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(0, 8, 8, 0)
                };

                Button close = new Button
                {
                    Text = "Close",
                    Size = new Size(80, 26),
                    DialogResult = DialogResult.OK
                };

                Button copy = new Button
                {
                    Text = "Copy",
                    Size = new Size(80, 26)
                };

                copy.Click += (s, e) =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(body.Text))
                        {
                            Clipboard.SetText(body.Text);
                            copy.Text = "Copied";
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Clipboard refused the report: {ex.Message}");
                    }
                };

                buttons.Controls.Add(close);
                buttons.Controls.Add(copy);

                form.Controls.Add(body);
                form.Controls.Add(buttons);
                form.AcceptButton = close;

                // Fill first, so the text sits above the button strip.
                body.BringToFront();

                Form parent = Instances.DocumentEditor;

                if (parent != null && !parent.IsDisposed)
                    form.ShowDialog(parent);
                else
                    form.ShowDialog();
            }
        }
    }
}
