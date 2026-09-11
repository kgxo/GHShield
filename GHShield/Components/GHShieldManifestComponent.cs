using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;

using GH_IO.Serialization;

using Grasshopper.Kernel;

using GHShield.Core;

namespace GHShield.Components
{
    /// <summary>
    /// The manifest: a real component that lives inside the definition and
    /// carries the list of protected objects.
    ///
    /// Grasshopper offers no way for a plugin to write its own data into a
    /// .gh file unless it owns an object in that file. This component is that
    /// object. Its Write/Read are ours to override, so the protection table
    /// travels with the definition when it is saved, copied or emailed.
    ///
    /// It is deliberately visible and labelled. Someone opening a protected
    /// definition should be able to see why parts of it will not move.
    /// </summary>
    public class GHShieldManifestComponent : GH_Component
    {
        private const string DataKey = "GHShieldProtectedIds";
        private const string FormatKey = "GHShieldFormat";
        private const string OwnerKey = "GHShieldLockedBy";
        private const string DateKey = "GHShieldLockedOn";
        private const string ContactKey = "GHShieldContact";
        private const string PinnedKey = "GHShieldPinnedIds";
        private const int FormatVersion = 1;

        /// <summary>Who locked this definition. Travels with the file.</summary>
        public string LockedBy { get; set; }

        /// <summary>When it was first locked. Travels with the file.</summary>
        public DateTime LockedOn { get; set; }

        /// <summary>
        /// How to reach whoever locked it. Optional, and travels with the file.
        ///
        /// A recipient who cannot edit the locked logic needs somewhere to go.
        /// Without this the stamp names a person and offers no way to reach
        /// them, which turns a helpful notice into a dead end.
        /// </summary>
        public string Contact { get; set; }

        /// <summary>
        /// Stamps the definition the first time anything is protected.
        /// Never overwrites: the file keeps its original author even if
        /// someone else opens it and freezes something more.
        /// </summary>
        public void StampIfUnset()
        {
            if (string.IsNullOrWhiteSpace(LockedBy))
                LockedBy = OwnerSettings.Owner;

            if (string.IsNullOrWhiteSpace(Contact))
                Contact = OwnerSettings.Contact;

            if (LockedOn == DateTime.MinValue)
                LockedOn = DateTime.Now;
        }

        public GHShieldManifestComponent()
            : base(
                "GHShield",
                "GHShield",
                "Records which objects in this definition are protected by GHShield. " +
                "Deleting this component removes protection from the saved file.",
                "GHShield",
                "Protection")
        {
        }

        public override Guid ComponentGuid =>
            new Guid("b0d1e7a4-3c5f-4a92-9f28-6c1d54e7ab30");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override Bitmap Icon => IconLoader.Load("ShieldIcon24.png");

        /// <summary>
        /// Swaps in attributes that open the GHShield panel on double-click.
        /// </summary>
        public override void CreateAttributes()
        {
            m_attributes = new GHShieldManifestAttributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Intentionally none. This component records state, it does not
            // compute anything from inputs.
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Plug a panel into Info and the recipient sees where the
            // definition came from and exactly what is sealed.
            pManager.AddTextParameter(
                "Info",
                "I",
                "Who protected this definition, when, and what is locked.",
                GH_ParamAccess.item);

            pManager.AddIntegerParameter(
                "Count",
                "N",
                "Number of protected objects in this definition.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int count = CountProtected();

            Message = count == 1
                ? "1 object frozen"
                : $"{count} objects frozen";

            DA.SetData(0, BuildReport(count));
            DA.SetData(1, count);
        }

        // =====================================================
        // THE STAMP
        // =====================================================

        /// <summary>
        /// The notice this component publishes, for anything that wants to
        /// show it outside the canvas - the report window, for instance.
        /// </summary>
        public string Report()
        {
            return BuildReport(CountProtected());
        }

        /// <summary>
        /// The notice a recipient reads.
        ///
        /// It answers, in order, the four questions somebody asks when a
        /// component refuses to move: what is this, who did it, does my file
        /// still work, and what am I allowed to touch. Anything that does not
        /// answer one of those is noise in a panel that has to stay small
        /// enough to read on the canvas.
        /// </summary>
        private string BuildReport(int count)
        {
            StringBuilder text = new StringBuilder();

            // One header line carrying the three things that identify this
            // seal: the tool, who makes it, and when the file was sealed.
            // Kept to a single line on purpose - as separate labelled rows
            // they pushed the list of frozen objects halfway down the panel.
            string version = "1.0.0";
            string maker = "ALVA";

            try
            {
                Assembly assembly = GetType().Assembly;

                version = assembly.GetName().Version.ToString(3);

                AssemblyCompanyAttribute company = (AssemblyCompanyAttribute)
                    Attribute.GetCustomAttribute(assembly, typeof(AssemblyCompanyAttribute));

                if (company != null && !string.IsNullOrWhiteSpace(company.Company))
                    maker = company.Company;
            }
            catch
            {
                // Fall back to the literals above.
            }

            string header = $"GHShield {version}  /  {maker}";

            if (LockedOn != DateTime.MinValue)
            {
                header += "  /  " + LockedOn.ToString(
                    "d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
            }

            text.AppendLine(header);
            text.AppendLine();

            text.AppendLine(count == 1
                ? "1 object frozen"
                : $"{count} objects frozen");

            int pinned = CountPinned();

            if (pinned > 0)
            {
                text.AppendLine(pinned == 1
                    ? "1 object pinned in place"
                    : $"{pinned} objects pinned in place");
            }

            if (count > 0)
            {
                text.AppendLine();
                text.AppendLine("Frozen:");

                foreach (string name in ProtectedNames())
                    text.AppendLine("  " + name);

                // Short sentences on their own lines. A panel re-wraps
                // anything longer than its own width, and a hard-wrapped
                // paragraph wrapped twice looks broken.
                if (pinned > 0)
                {
                    text.AppendLine();
                    text.AppendLine("Pinned objects can still be edited.");
                    text.AppendLine("They just cannot be moved or deleted.");
                }

                text.AppendLine();
                text.AppendLine("These still solve.");
                text.AppendLine("Their results feed everything downstream.");
                text.AppendLine("They cannot be moved, edited, copied or deleted.");
                text.AppendLine("Everything else here is yours to change.");

                if (!string.IsNullOrWhiteSpace(Contact))
                {
                    text.AppendLine();
                    text.AppendLine("Need one changed? Ask " + Contact + ".");
                }
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>
        /// The protected objects, by name, with duplicates grouped.
        ///
        /// Real definitions contain eight components all nicknamed "Slider",
        /// and eight identical lines tell the reader nothing. "Slider x 8"
        /// does.
        /// </summary>
        private List<string> ProtectedNames()
        {
            List<string> lines = new List<string>();

            GH_Document document = OnPingDocument();

            if (document == null)
                return lines;

            Dictionary<string, int> counts = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            List<string> order = new List<string>();

            foreach (IGH_DocumentObject obj in document.Objects)
            {
                if (obj == null || !SecurityManager.IsFrozen(obj))
                    continue;

                string name = obj.NickName;

                if (string.IsNullOrWhiteSpace(name))
                    name = obj.Name;

                if (string.IsNullOrWhiteSpace(name))
                    name = "(unnamed)";

                // A component renamed to a sentence - "Create circles based
                // on modified, normalised distance" - turns the list into
                // paragraphs. The name is only there to be recognised on the
                // canvas, and the first few words do that.
                if (name.Length > 30)
                    name = name.Substring(0, 29).TrimEnd() + "\u2026";

                if (counts.ContainsKey(name))
                {
                    counts[name]++;
                }
                else
                {
                    counts[name] = 1;
                    order.Add(name);
                }
            }

            order.Sort(StringComparer.OrdinalIgnoreCase);

            // A definition with sixty locked objects would produce a panel
            // taller than the canvas. The list is orientation, not an
            // inventory - the exact objects are obvious on screen, because
            // they are the ones wearing a padlock.
            const int Limit = 12;

            int shown = Math.Min(order.Count, Limit);

            for (int i = 0; i < shown; i++)
            {
                string name = order[i];

                lines.Add(counts[name] > 1
                    ? $"{name}  x {counts[name]}"
                    : name);
            }

            if (order.Count > shown)
                lines.Add($"and {order.Count - shown} more");

            return lines;
        }

        private int CountPinned()
        {
            GH_Document document = OnPingDocument();

            if (document == null)
                return 0;

            int count = 0;

            foreach (IGH_DocumentObject obj in document.Objects)
            {
                if (obj != null && SecurityManager.IsPinned(obj))
                    count++;
            }

            return count;
        }

        private int CountProtected()
        {
            GH_Document document = OnPingDocument();

            if (document == null)
                return 0;

            int count = 0;

            foreach (IGH_DocumentObject obj in document.Objects)
            {
                if (obj != null && SecurityManager.IsFrozen(obj))
                    count++;
            }

            return count;
        }

        // =====================================================
        // SAVE
        // =====================================================

        public override bool Write(GH_IWriter writer)
        {
            try
            {
                writer.SetInt32(FormatKey, FormatVersion);
                writer.SetString(DataKey, Encode(SecurityManager.ExportFrozenIds()));

                writer.SetString(OwnerKey, LockedBy ?? string.Empty);
                writer.SetString(ContactKey, Contact ?? string.Empty);
                writer.SetString(PinnedKey, Encode(SecurityManager.ExportPinnedIds()));

                writer.SetString(DateKey,
                    LockedOn == DateTime.MinValue
                        ? string.Empty
                        : LockedOn.ToString("o", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Log.Error($"could not save protection data: {ex.Message}");
            }

            return base.Write(writer);
        }

        // =====================================================
        // LOAD
        // =====================================================

        public override bool Read(GH_IReader reader)
        {
            bool result = base.Read(reader);

            try
            {
                string owner = null;

                if (reader.TryGetString(OwnerKey, ref owner) &&
                    !string.IsNullOrWhiteSpace(owner))
                {
                    LockedBy = owner;
                }

                string contact = null;

                if (reader.TryGetString(ContactKey, ref contact) &&
                    !string.IsNullOrWhiteSpace(contact))
                {
                    Contact = contact;
                }

                string when = null;

                if (reader.TryGetString(DateKey, ref when) &&
                    !string.IsNullOrWhiteSpace(when))
                {
                    DateTime parsed;

                    if (DateTime.TryParse(when, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out parsed))
                    {
                        LockedOn = parsed;
                    }
                }

                string pinnedData = null;

                if (reader.TryGetString(PinnedKey, ref pinnedData) &&
                    !string.IsNullOrEmpty(pinnedData))
                {
                    foreach (Guid id in Decode(pinnedData))
                        SecurityManager.RegisterPinned(id);
                }

                string data = null;

                if (reader.TryGetString(DataKey, ref data) &&
                    !string.IsNullOrEmpty(data))
                {
                    List<Guid> ids = Decode(data);

                    foreach (Guid id in ids)
                        SecurityManager.RegisterFrozen(id);

                    if (ids.Count > 0)
                    {
                        Log.Info(
                            $"loaded protection for {ids.Count} object(s) from this file.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"could not read protection data: {ex.Message}");
            }

            return result;
        }

        // =====================================================
        // APPLY AFTER LOAD
        // =====================================================

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);

            if (document == null)
                return;

            // Read() runs before the rest of the definition exists, so the
            // registry holds Guids with nothing attached to them yet. Defer
            // until the document is fully populated, then let every frozen
            // object adopt its entry.
            try
            {
                var canvas = Grasshopper.Instances.ActiveCanvas;

                if (canvas == null)
                    return;

                canvas.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        SecurityManager.Reattach(document);
                        ExpireSolution(true);
                        canvas.Refresh();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Deferred reattach failed: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Log.Debug($"AddedToDocument: {ex.Message}");
            }
        }

        // =====================================================
        // ENCODING
        // =====================================================
        //
        // Guids are stored as one ';'-separated string rather than as indexed
        // archive items. It is a single Set/TryGet pair, which keeps the
        // format trivial to inspect and to version.

        private static string Encode(List<Guid> ids)
        {
            if (ids == null || ids.Count == 0)
                return string.Empty;

            string[] parts = new string[ids.Count];

            for (int i = 0; i < ids.Count; i++)
                parts[i] = ids[i].ToString("N");

            return string.Join(";", parts);
        }

        private static List<Guid> Decode(string data)
        {
            List<Guid> ids = new List<Guid>();

            if (string.IsNullOrEmpty(data))
                return ids;

            string[] parts = data.Split(';');

            foreach (string part in parts)
            {
                string trimmed = part.Trim();

                if (trimmed.Length == 0)
                    continue;

                Guid id;

                if (Guid.TryParseExact(trimmed, "N", out id))
                    ids.Add(id);
            }

            return ids;
        }
    }
}
