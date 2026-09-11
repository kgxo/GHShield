using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using Grasshopper;
using Grasshopper.Kernel;

using GHShield.Components;
using GHShield.Core;

namespace GHShield.UI
{
    /// <summary>
    /// Asks for the name and contact address that get stamped into protected
    /// definitions.
    ///
    /// Saving here also re-stamps definitions that are already open and
    /// already carry THIS machine's name. Without that, the first file you
    /// protected would keep whatever the default was - typically the Windows
    /// account name - and no amount of setting the owner afterwards would
    /// change it, because the stamp is deliberately write-once.
    /// </summary>
    public static class OwnerPrompt
    {
        public static void Show(object sender, EventArgs e)
        {
            try
            {
                string previousOwner = OwnerSettings.Owner;
                string previousContact = OwnerSettings.Contact;

                using (Form form = new Form())
                {
                    form.Text = "GHShield - protection owner";
                    form.FormBorderStyle = FormBorderStyle.FixedDialog;
                    form.StartPosition = FormStartPosition.CenterParent;
                    form.ClientSize = new Size(400, 226);
                    form.MinimizeBox = false;
                    form.MaximizeBox = false;
                    form.ShowInTaskbar = false;
                    form.Font = new Font("Segoe UI", 8.25f);

                    Label intro = new Label
                    {
                        Location = new Point(12, 12),
                        Size = new Size(376, 34),
                        Text = "This is written into every definition you protect and " +
                               "travels with the file. Whoever opens it will see it."
                    };

                    Label nameLabel = new Label
                    {
                        Location = new Point(12, 54),
                        Size = new Size(376, 16),
                        Text = "Name or studio"
                    };

                    TextBox nameBox = new TextBox
                    {
                        Location = new Point(12, 72),
                        Size = new Size(376, 22),
                        Text = previousOwner
                    };

                    Label contactLabel = new Label
                    {
                        Location = new Point(12, 104),
                        Size = new Size(376, 16),
                        Text = "Contact  (optional - how a recipient asks for a change)"
                    };

                    TextBox contactBox = new TextBox
                    {
                        Location = new Point(12, 122),
                        Size = new Size(376, 22),
                        Text = previousContact
                    };

                    Label hint = new Label
                    {
                        Location = new Point(12, 152),
                        Size = new Size(376, 16),
                        ForeColor = SystemColors.GrayText,
                        Text = "Example:  Nipun, ALVA  /  alva.built@gmail.com"
                    };

                    Button ok = new Button
                    {
                        Text = "Save",
                        DialogResult = DialogResult.OK,
                        Location = new Point(230, 184),
                        Size = new Size(75, 26)
                    };

                    Button cancel = new Button
                    {
                        Text = "Cancel",
                        DialogResult = DialogResult.Cancel,
                        Location = new Point(313, 184),
                        Size = new Size(75, 26)
                    };

                    form.Controls.Add(intro);
                    form.Controls.Add(nameLabel);
                    form.Controls.Add(nameBox);
                    form.Controls.Add(contactLabel);
                    form.Controls.Add(contactBox);
                    form.Controls.Add(hint);
                    form.Controls.Add(ok);
                    form.Controls.Add(cancel);
                    form.AcceptButton = ok;
                    form.CancelButton = cancel;

                    form.Shown += (s2, e2) =>
                    {
                        nameBox.Focus();
                        nameBox.SelectAll();
                    };

                    Form parent = Instances.DocumentEditor;

                    DialogResult result = parent != null && !parent.IsDisposed
                        ? form.ShowDialog(parent)
                        : form.ShowDialog();

                    if (result != DialogResult.OK)
                        return;

                    OwnerSettings.Owner = nameBox.Text;
                    OwnerSettings.Contact = contactBox.Text;
                }

                string owner = OwnerSettings.Owner;
                string contact = OwnerSettings.Contact;

                bool unchanged =
                    string.Equals(previousOwner, owner, StringComparison.Ordinal) &&
                    string.Equals(previousContact, contact, StringComparison.Ordinal);

                if (unchanged)
                    return;

                int restamped = Restamp(previousOwner, owner, contact);

                Log.Info(restamped > 0
                    ? $"protection owner set to \"{owner}\"; {restamped} open definition(s) restamped."
                    : $"protection owner set to \"{owner}\".");
            }
            catch (Exception ex)
            {
                Log.Error($"could not set the owner: {ex.Message}");
            }
        }

        /// <summary>
        /// Asks the first time a definition is about to be stamped on this
        /// machine, and never again.
        ///
        /// Asking once, at the moment it first matters, is the difference
        /// between a file that says "Nipun, ALVA" and one that says whatever
        /// the Windows account happens to be called.
        /// </summary>
        public static void EnsureConfigured()
        {
            try
            {
                if (OwnerSettings.IsConfigured)
                    return;

                Show(null, EventArgs.Empty);

                // Cancelled. Record that the question was asked, so it is
                // not asked again on every freeze. The file simply carries no
                // name, which is honest.
                if (!OwnerSettings.IsConfigured)
                    OwnerSettings.Owner = string.Empty;
            }
            catch (Exception ex)
            {
                Log.Debug($"Owner prompt skipped: {ex.Message}");
            }
        }

        // =====================================================
        // RESTAMP
        // =====================================================

        /// <summary>
        /// Rewrites the stamp on every open definition that this machine
        /// stamped, and only those.
        ///
        /// The test is deliberately narrow: the existing name must be blank or
        /// must match the name we were using until a moment ago. A definition
        /// somebody else protected keeps their name, which is the whole point
        /// of the stamp - it says who sealed the file, not who last opened it.
        /// </summary>
        private static int Restamp(string previousOwner, string owner, string contact)
        {
            int changed = 0;

            foreach (GHShieldManifestComponent manifest in Manifests())
            {
                string stamped = manifest.LockedBy;

                bool ours =
                    string.IsNullOrWhiteSpace(stamped) ||
                    string.Equals(stamped, previousOwner, StringComparison.OrdinalIgnoreCase);

                if (!ours)
                    continue;

                manifest.LockedBy = owner;
                manifest.Contact = contact;

                if (manifest.LockedOn == DateTime.MinValue)
                    manifest.LockedOn = DateTime.Now;

                try
                {
                    manifest.ExpireSolution(true);
                }
                catch (Exception ex)
                {
                    Log.Debug($"Could not refresh manifest: {ex.Message}");
                }

                changed++;
            }

            if (changed > 0)
            {
                try
                {
                    if (Instances.ActiveCanvas != null)
                        Instances.ActiveCanvas.Refresh();
                }
                catch
                {
                    // Cosmetic only.
                }
            }

            return changed;
        }

        private static List<GHShieldManifestComponent> Manifests()
        {
            List<GHShieldManifestComponent> found =
                new List<GHShieldManifestComponent>();

            try
            {
                foreach (GH_Document document in Instances.DocumentServer)
                {
                    if (document == null)
                        continue;

                    foreach (IGH_DocumentObject obj in document.Objects)
                    {
                        GHShieldManifestComponent manifest =
                            obj as GHShieldManifestComponent;

                        if (manifest != null)
                            found.Add(manifest);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not enumerate documents: {ex.Message}");
            }

            return found;
        }
    }
}
