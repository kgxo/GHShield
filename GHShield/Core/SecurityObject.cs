using System;
using System.Drawing;

namespace GHShield.Core
{
    /// <summary>
    /// Protection state for one Grasshopper object.
    ///
    /// IMPORTANT: this class deliberately holds NO reference to the
    /// IGH_DocumentObject itself. It is identified by InstanceGuid only.
    ///
    /// Why: Grasshopper rebuilds objects as brand new instances on undo,
    /// redo and file reload. A stored reference goes stale the moment the
    /// user presses Ctrl+Z, which silently unfreezes the object. A Guid
    /// survives all of that - and unlike a reference, it can be written
    /// into the .gh file when persistence lands.
    /// </summary>
    public class SecurityObject
    {
        // ==================================================
        // IDENTITY
        // ==================================================

        /// <summary>The owning object's InstanceGuid.</summary>
        public Guid Id { get; set; }

        /// <summary>Last known nickname. Display only - never used as a key.</summary>
        public string DisplayName { get; set; }

        // ==================================================
        // PERMISSIONS
        // ==================================================

        public bool IsFrozen { get; set; }

        /// <summary>
        /// Pinned: held in place, but still editable.
        ///
        /// The middle setting between frozen and free. The layout cannot be
        /// rearranged and nothing can be deleted, but sliders still slide and
        /// panels still take text - which is what most handovers actually
        /// need. Freezing is for logic that must not change at all.
        /// </summary>
        public bool IsPinned { get; set; }

        public bool IsLocked { get; set; }

        public bool CanMove { get; set; } = true;

        public bool CanDelete { get; set; } = true;

        public bool CanCopy { get; set; } = true;

        public bool CanEdit { get; set; } = true;

        // ==================================================
        // OWNERSHIP
        // ==================================================

        public string Password { get; set; }

        public string Owner { get; set; }

        public string Notes { get; set; }

        /// <summary>
        /// True when this entry carries information that must outlive
        /// an unfreeze (ownership, notes, a lock). Entries without any
        /// of it are dropped on unfreeze so the registry stays clean.
        /// </summary>
        public bool HasStandingMetadata
        {
            get
            {
                return IsPinned ||
                       IsLocked ||
                       !string.IsNullOrWhiteSpace(Password) ||
                       !string.IsNullOrWhiteSpace(Owner) ||
                       !string.IsNullOrWhiteSpace(Notes);
            }
        }

        // ==================================================
        // FROZEN STATE
        // ==================================================

        /// <summary>Full GH_Archive snapshot, used as the last-resort restore.</summary>
        public byte[] FrozenState { get; set; }

        public bool HasFrozenState
        {
            get
            {
                return FrozenState != null &&
                       FrozenState.Length > 0;
            }
        }

        /// <summary>
        /// Canvas position at freeze time. Restoring just the pivot is far
        /// cheaper than a full archive round-trip and - unlike ReadFull - it
        /// cannot drop the object's wires.
        /// </summary>
        public PointF FrozenPivot { get; set; }

        public bool HasFrozenPivot { get; set; }

        /// <summary>Number Slider value at freeze time.</summary>
        public decimal? FrozenSliderValue { get; set; }

        // ==================================================
        // RESTORATION GUARD
        // ==================================================

        /// <summary>
        /// Stops GHShield treating its own restoration as a user edit.
        /// </summary>
        public bool IsRestoring { get; set; }
    }
}
