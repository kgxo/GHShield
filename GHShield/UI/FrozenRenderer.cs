using System.Drawing;
using System.Drawing.Drawing2D;

namespace GHShield.UI
{
    /// <summary>
    /// Draws the frozen state over a Grasshopper object.
    ///
    /// The object itself is still painted normally by Grasshopper - GHShield
    /// only washes over the top. That matters: a frozen component is still
    /// live and still solving, so it should read as "sealed", not as
    /// "disabled". Grasshopper already greys out objects it has switched off,
    /// and looking like that would be actively misleading.
    ///
    /// The look: a pale frost wash, a cool border following the capsule's
    /// rounded corners, and a padlock in the top-right.
    ///
    /// All GDI resources are static. This runs for every frozen object on
    /// every repaint, so allocating pens and brushes here would show up as
    /// stutter while panning.
    /// </summary>
    public static class FrozenRenderer
    {
        private static readonly Color Ice = Color.FromArgb(120, 176, 214, 240);
        private static readonly Color Edge = Color.FromArgb(225, 58, 116, 170);

        private static readonly Brush FrostBrush = new SolidBrush(Color.FromArgb(112, 196, 224, 246));
        private static readonly Pen EdgePen = new Pen(Edge, 1.6f);
        private static readonly Pen SheenPen = new Pen(Color.FromArgb(90, 255, 255, 255), 1.0f);

        private static readonly Brush BadgeBrush = new SolidBrush(Color.FromArgb(235, 42, 96, 148));
        private static readonly Pen BadgeEdgePen = new Pen(Color.FromArgb(140, 255, 255, 255), 1.0f);
        private static readonly Pen ShacklePen = new Pen(Color.White, 1.4f);
        private static readonly Brush LockBodyBrush = new SolidBrush(Color.White);

        private const float CornerRadius = 3.0f;
        private const float BadgeSize = 16.0f;

        public static void Draw(Graphics graphics, RectangleF bounds)
        {
            if (graphics == null)
                return;

            if (bounds.Width < 1.0f || bounds.Height < 1.0f)
                return;

            SmoothingMode previous = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            try
            {
                DrawFrost(graphics, bounds);
                DrawPadlock(graphics, bounds);
            }
            finally
            {
                graphics.SmoothingMode = previous;
            }
        }

        // =====================================================
        // PINNED
        // =====================================================

        private static readonly Pen PinEdgePen =
            new Pen(Color.FromArgb(180, 96, 104, 118), 1.3f) { DashStyle = DashStyle.Dash };

        private static readonly Brush PinBadgeBrush =
            new SolidBrush(Color.FromArgb(225, 88, 96, 110));

        /// <summary>
        /// A pinned object gets an outline and a small stud - no frost.
        ///
        /// The difference has to be obvious at a glance and without reading
        /// anything: frost says "this is sealed", a dashed edge says "this
        /// stays where it is". Filling a pinned object with the same wash
        /// would tell the reader it cannot be touched, which is wrong - it
        /// can still be used.
        /// </summary>
        public static void DrawPinned(Graphics graphics, RectangleF bounds)
        {
            if (graphics == null)
                return;

            if (bounds.Width < 1.0f || bounds.Height < 1.0f)
                return;

            SmoothingMode previous = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            try
            {
                using (GraphicsPath path = RoundedRect(bounds, CornerRadius))
                    graphics.DrawPath(PinEdgePen, path);

                float size = 9.0f;

                RectangleF stud = new RectangleF(
                    bounds.Right - size / 2.0f - 1.0f,
                    bounds.Top - size / 2.0f + 1.0f,
                    size,
                    size);

                graphics.FillEllipse(PinBadgeBrush, stud);
                graphics.DrawEllipse(BadgeEdgePen, stud);
            }
            finally
            {
                graphics.SmoothingMode = previous;
            }
        }

        // =====================================================
        // FROST
        // =====================================================

        private static void DrawFrost(Graphics graphics, RectangleF bounds)
        {
            using (GraphicsPath path = RoundedRect(bounds, CornerRadius))
            {
                graphics.FillPath(FrostBrush, path);
                graphics.DrawPath(EdgePen, path);
            }

            // A single highlight just inside the top edge. Reads as glaze
            // rather than as a second border.
            if (bounds.Width > 8.0f)
            {
                graphics.DrawLine(
                    SheenPen,
                    bounds.Left + CornerRadius + 1.0f,
                    bounds.Top + 1.5f,
                    bounds.Right - CornerRadius - 1.0f,
                    bounds.Top + 1.5f);
            }
        }

        // =====================================================
        // PADLOCK
        // =====================================================

        private static void DrawPadlock(Graphics graphics, RectangleF bounds)
        {
            // Sits half outside the capsule so it stays legible even when the
            // object is small or densely wired.
            RectangleF badge = new RectangleF(
                bounds.Right - BadgeSize * 0.45f,
                bounds.Top - BadgeSize * 0.55f,
                BadgeSize,
                BadgeSize);

            graphics.FillEllipse(BadgeBrush, badge);
            graphics.DrawEllipse(BadgeEdgePen, badge);

            // Shackle: a half-circle sitting on top of the body.
            float shackleWidth = BadgeSize * 0.34f;
            float shackleHeight = BadgeSize * 0.30f;

            graphics.DrawArc(
                ShacklePen,
                badge.X + (BadgeSize - shackleWidth) / 2.0f,
                badge.Y + BadgeSize * 0.26f,
                shackleWidth,
                shackleHeight,
                180.0f,
                180.0f);

            // Body.
            RectangleF body = new RectangleF(
                badge.X + BadgeSize * 0.28f,
                badge.Y + BadgeSize * 0.44f,
                BadgeSize * 0.44f,
                BadgeSize * 0.32f);

            using (GraphicsPath path = RoundedRect(body, 1.2f))
            {
                graphics.FillPath(LockBodyBrush, path);
            }
        }

        // =====================================================
        // GEOMETRY
        // =====================================================

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();

            float r = radius;

            // Degenerate on very small or very zoomed-out capsules.
            if (r * 2.0f > rect.Width || r * 2.0f > rect.Height)
            {
                path.AddRectangle(rect);
                return path;
            }

            float d = r * 2.0f;

            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}
