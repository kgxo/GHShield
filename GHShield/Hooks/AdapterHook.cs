using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

using GHShield.Core;
using GHShield.UI;

namespace GHShield.Hooks
{
    /// <summary>
    /// Installs GHShield's protection adapters onto every interactive object
    /// in a document, and re-applies protection to objects Grasshopper has
    /// rebuilt.
    ///
    /// Why adapters at all: Grasshopper 7 has no universal "may this be
    /// edited?" hook, and each interactive object implements its editing
    /// differently. A Number Slider raises a value-changed event we can catch
    /// centrally; a Panel, Toggle, Value List, Gene Pool or Graph Mapper edits
    /// itself inside its own attributes, where nothing outside can see it. For
    /// those, the only reliable interception point is the attributes object -
    /// so GHShield replaces it with a subclass that refuses interaction while
    /// the object is frozen.
    /// </summary>
    public static class AdapterHook
    {
        private static readonly HashSet<GH_Document> AttachedDocuments =
            new HashSet<GH_Document>();

        public static void Attach(GH_Document document)
        {
            if (document == null)
                return;

            if (AttachedDocuments.Contains(document))
                return;

            AttachedDocuments.Add(document);

            foreach (IGH_DocumentObject obj in document.Objects)
                Process(obj);

            document.ObjectsAdded += Document_ObjectsAdded;

            Log.Debug("Adapter hook attached.");
        }

        private static void Document_ObjectsAdded(
            object sender,
            GH_DocObjectEventArgs e)
        {
            if (e == null || e.Objects == null)
                return;

            foreach (IGH_DocumentObject obj in e.Objects)
                Process(obj);
        }

        private static void Process(IGH_DocumentObject obj)
        {
            if (obj == null)
                return;

            // Objects arriving here may be rebuilt instances from an undo, a
            // redo or a file load. They keep their InstanceGuid, so any
            // protection recorded against that Guid must be re-applied.
            //
            // NOTHING ELSE HAPPENS TO AN UNFROZEN OBJECT.
            //
            // Earlier versions installed a protection adapter on every
            // eligible object in every document, frozen or not, on the theory
            // that an inert adapter costs nothing. It is not inert: swapping
            // an object's attributes replaces the instance that owns its
            // bounds, and a Panel whose attributes were replaced could end up
            // drawn in one place and clickable in another - so panels became
            // intermittently impossible to select or move, in documents where
            // GHShield had never been used at all.
            //
            // Protection is now attached only when something is actually
            // frozen. A definition nobody has protected is untouched by this
            // plugin.
            SecurityManager.ReattachIfFrozen(obj);
        }

        // =====================================================
        // ADAPTER SELECTION
        // =====================================================

        /// <summary>
        /// Installs a hand-written protection adapter for one of the types
        /// below. Called only from SecurityManager, and only when the runtime
        /// proxy could not be generated for that object's attributes class.
        /// </summary>
        public static void InstallAdapter(IGH_DocumentObject obj)
        {
            // Already carrying one of ours.
            if (obj.Attributes is IGHShieldAttributes)
                return;

            // Every type below was verified against Grasshopper.dll for
            // Rhino 7. Note that the Gene Pool is deliberately absent: it
            // belongs to Galapagos, not Grasshopper, so protecting it would
            // mean taking a dependency on a second assembly.

            Grasshopper.Kernel.Special.GH_GraphMapper graphMapper = obj as Grasshopper.Kernel.Special.GH_GraphMapper;

            if (graphMapper != null)
            {
                Swap(graphMapper, new GHShield.GHShieldGraphMapperAttributes(graphMapper), "Graph Mapper");
                return;
            }

            Grasshopper.Kernel.Special.GH_Panel panel = obj as Grasshopper.Kernel.Special.GH_Panel;

            if (panel != null)
            {
                Swap(panel, new GHShieldPanelAttributes(panel), "Panel");
                return;
            }

            Grasshopper.Kernel.Special.GH_BooleanToggle booleanToggle = obj as Grasshopper.Kernel.Special.GH_BooleanToggle;

            if (booleanToggle != null)
            {
                Swap(booleanToggle, new GHShieldToggleAttributes(booleanToggle), "Toggle");
                return;
            }

            Grasshopper.Kernel.Special.GH_ValueList valueList = obj as Grasshopper.Kernel.Special.GH_ValueList;

            if (valueList != null)
            {
                Swap(valueList, new GHShieldValueListAttributes(valueList), "Value List");
                return;
            }

            Grasshopper.Kernel.Special.GH_ColourSwatch colourSwatch = obj as Grasshopper.Kernel.Special.GH_ColourSwatch;

            if (colourSwatch != null)
            {
                Swap(colourSwatch, new GHShieldColourSwatchAttributes(colourSwatch), "Colour Swatch");
                return;
            }

            Grasshopper.Kernel.Special.GH_ColourPickerObject colourPicker = obj as Grasshopper.Kernel.Special.GH_ColourPickerObject;

            if (colourPicker != null)
            {
                Swap(colourPicker, new GHShieldColourPickerAttributes(colourPicker), "Colour Picker");
                return;
            }

            Grasshopper.Kernel.Special.GH_ColourWheel colourWheel = obj as Grasshopper.Kernel.Special.GH_ColourWheel;

            if (colourWheel != null)
            {
                Swap(colourWheel, new GHShieldColourWheelAttributes(colourWheel), "Colour Wheel");
                return;
            }

            Grasshopper.Kernel.Special.GH_ButtonObject buttonObject = obj as Grasshopper.Kernel.Special.GH_ButtonObject;

            if (buttonObject != null)
            {
                Swap(buttonObject, new GHShieldButtonAttributes(buttonObject), "Button");
                return;
            }

            Grasshopper.Kernel.Special.GH_DigitScroller digitScroller = obj as Grasshopper.Kernel.Special.GH_DigitScroller;

            if (digitScroller != null)
            {
                Swap(digitScroller, new GHShieldDigitScrollerAttributes(digitScroller), "Digit Scroller");
                return;
            }

            Grasshopper.Kernel.Special.GH_MultiDimensionalSlider multiDimensionalSlider = obj as Grasshopper.Kernel.Special.GH_MultiDimensionalSlider;

            if (multiDimensionalSlider != null)
            {
                Swap(multiDimensionalSlider, new GHShieldMDSliderAttributes(multiDimensionalSlider), "MD Slider");
                return;
            }

            Grasshopper.Kernel.Special.GH_GradientControl gradientControl = obj as Grasshopper.Kernel.Special.GH_GradientControl;

            if (gradientControl != null)
            {
                Swap(gradientControl, new GHShieldGradientAttributes(gradientControl), "Gradient");
                return;
            }

            Grasshopper.Kernel.Special.GH_ImageSampler imageSampler = obj as Grasshopper.Kernel.Special.GH_ImageSampler;

            if (imageSampler != null)
            {
                Swap(imageSampler, new GHShieldImageSamplerAttributes(imageSampler), "Image Sampler");
                return;
            }
        }

        // =====================================================
        // SWAP
        // =====================================================

        /// <summary>
        /// Replaces an object's attributes, carrying position and selection
        /// across. A fresh attributes instance starts at the canvas origin,
        /// so without this the object would teleport the moment GHShield
        /// touched it.
        /// </summary>
        private static void Swap(
            IGH_DocumentObject obj,
            IGH_Attributes replacement,
            string label)
        {
            try
            {
                PointF pivot = PointF.Empty;
                bool selected = false;

                if (obj.Attributes != null)
                {
                    pivot = obj.Attributes.Pivot;
                    selected = obj.Attributes.Selected;
                }

                obj.Attributes = replacement;
                obj.Attributes.Pivot = pivot;
                obj.Attributes.Selected = selected;
                obj.Attributes.ExpireLayout();

                Log.Debug($"{label} protection attached to '{obj.NickName}'.");
            }
            catch (Exception ex)
            {
                Log.Debug(
                    $"Could not attach {label} protection to '{obj.NickName}': {ex.Message}");
            }
        }
    }
}
