using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public abstract partial class ScrollBarControl
    {
        private SolidBrush _trackPaintBrush;
        private SolidBrush _thumbPaintBrush;
        private SolidBrush _firstArrowPaintBrush;
        private SolidBrush _lastArrowPaintBrush;
        private Pen _borderPaintPen;
        private Point[] _firstArrowPoints;
        private Point[] _lastArrowPoints;
#if !WINFORMSXAML_PACKAGE
        private long _paintResourceCreationCount;
        private long _arrowPointArrayCreationCount;
#endif

        /// <summary>Paints the track, thumb, arrows, borders, and focus cue.</summary>
        /// <param name="e">The paint event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ScrollBarGeometry geometry = CalculateGeometry();
            Rectangle clip = Rectangle.Intersect(
                ClientRectangle,
                e.ClipRectangle);

            if (clip.IsEmpty)
                return;

            SolidBrush track = GetCachedPaintBrush(
                ref _trackPaintBrush,
                _style.TrackColor);

            e.Graphics.FillRectangle(
                track,
                clip);

            Color thumbColor = _pressedPart ==
                    ScrollBarHitPart.Thumb
                ? _style.ThumbPressedColor
                : _hoverPart == ScrollBarHitPart.Thumb
                    ? _style.ThumbHoverColor
                    : _style.ThumbColor;

            if (!geometry.Thumb.IsEmpty &&
                geometry.Thumb.IntersectsWith(clip))
            {
                SolidBrush thumb = GetCachedPaintBrush(
                    ref _thumbPaintBrush,
                    thumbColor);

                e.Graphics.FillRectangle(
                    thumb,
                    geometry.Thumb);

                DrawRectangleBorder(
                    e.Graphics,
                    geometry.Thumb,
                    GetCachedBorderPen());
            }

            if (geometry.FirstButton.IntersectsWith(clip))
            {
                SolidBrush firstArrow = GetCachedPaintBrush(
                    ref _firstArrowPaintBrush,
                    GetArrowPaintColor(
                        ScrollBarHitPart.FirstButton));

                DrawArrow(
                    e.Graphics,
                    geometry.FirstButton,
                    true,
                    firstArrow);
            }

            if (geometry.LastButton.IntersectsWith(clip))
            {
                SolidBrush lastArrow = GetCachedPaintBrush(
                    ref _lastArrowPaintBrush,
                    GetArrowPaintColor(
                        ScrollBarHitPart.LastButton));

                DrawArrow(
                    e.Graphics,
                    geometry.LastButton,
                    false,
                    lastArrow);
            }

            if (!geometry.FirstButton.IsEmpty &&
                geometry.FirstButton.IntersectsWith(clip))
            {
                DrawRectangleBorder(
                    e.Graphics,
                    geometry.FirstButton,
                    GetCachedBorderPen());
            }

            if (!geometry.LastButton.IsEmpty &&
                geometry.LastButton.IntersectsWith(clip))
            {
                DrawRectangleBorder(
                    e.Graphics,
                    geometry.LastButton,
                    GetCachedBorderPen());
            }

            if (clip.Left <= ClientRectangle.Left ||
                clip.Top <= ClientRectangle.Top ||
                clip.Right >= ClientRectangle.Right ||
                clip.Bottom >= ClientRectangle.Bottom)
            {
                DrawRectangleBorder(
                    e.Graphics,
                    ClientRectangle,
                    GetCachedBorderPen());
            }

            if (Focused && ShowFocusCues &&
                geometry.InnerBounds.Width > 2 &&
                geometry.InnerBounds.Height > 2 &&
                geometry.InnerBounds.IntersectsWith(clip))
            {
                Rectangle focus = Rectangle.Inflate(
                    geometry.InnerBounds,
                    -2,
                    -2);
                ControlPaint.DrawFocusRectangle(
                    e.Graphics,
                    focus);
            }
        }

        private Color GetArrowPaintColor(
            ScrollBarHitPart part)
        {
            if (!Enabled)
                return SystemColors.GrayText;

            return _hoverPart == part ||
                _pressedPart == part
                    ? _style.ArrowHoverColor
                    : _style.ArrowColor;
        }

        private void DrawArrow(
            Graphics graphics,
            Rectangle bounds,
            bool first,
            Brush brush)
        {
            if (bounds.Width < 3 || bounds.Height < 3)
                return;

            int centerX = bounds.Left + bounds.Width / 2;
            int centerY = bounds.Top + bounds.Height / 2;
            int radius = Math.Max(
                1,
                Math.Min(bounds.Width, bounds.Height) / 4);
            Point[] points = GetArrowPoints(first);

            if (_vertical)
            {
                points[0] = first
                    ? new Point(centerX, centerY - radius)
                    : new Point(centerX, centerY + radius);
                points[1] = first
                    ? new Point(centerX - radius, centerY + radius)
                    : new Point(centerX - radius, centerY - radius);
                points[2] = first
                    ? new Point(centerX + radius, centerY + radius)
                    : new Point(centerX + radius, centerY - radius);
            }
            else
            {
                points[0] = first
                    ? new Point(centerX - radius, centerY)
                    : new Point(centerX + radius, centerY);
                points[1] = first
                    ? new Point(centerX + radius, centerY - radius)
                    : new Point(centerX - radius, centerY - radius);
                points[2] = first
                    ? new Point(centerX + radius, centerY + radius)
                    : new Point(centerX - radius, centerY + radius);
            }

            graphics.FillPolygon(brush, points);
        }

        private static void DrawRectangleBorder(
            Graphics graphics,
            Rectangle bounds,
            Pen pen)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            Rectangle border = new Rectangle(
                bounds.Left,
                bounds.Top,
                Math.Max(0, bounds.Width - 1),
                Math.Max(0, bounds.Height - 1));

            graphics.DrawRectangle(pen, border);
        }

        private SolidBrush GetCachedPaintBrush(
            ref SolidBrush brush,
            Color color)
        {
            if (brush != null &&
                brush.Color.ToArgb() == color.ToArgb())
            {
                return brush;
            }

            if (brush != null)
                brush.Dispose();

            brush = new SolidBrush(color);
#if !WINFORMSXAML_PACKAGE
            _paintResourceCreationCount++;
#endif
            return brush;
        }

        private Pen GetCachedBorderPen()
        {
            Color color = _style.BorderColor;

            if (_borderPaintPen != null &&
                _borderPaintPen.Color.ToArgb() == color.ToArgb())
            {
                return _borderPaintPen;
            }

            if (_borderPaintPen != null)
                _borderPaintPen.Dispose();

            _borderPaintPen = new Pen(color);
#if !WINFORMSXAML_PACKAGE
            _paintResourceCreationCount++;
#endif
            return _borderPaintPen;
        }

        private Point[] GetArrowPoints(bool first)
        {
            Point[] points = first
                ? _firstArrowPoints
                : _lastArrowPoints;

            if (points != null)
                return points;

            points = new Point[3];
#if !WINFORMSXAML_PACKAGE
            _arrowPointArrayCreationCount++;
#endif

            if (first)
                _firstArrowPoints = points;
            else
                _lastArrowPoints = points;

            return points;
        }

        private void DisposePaintResources()
        {
            DisposePaintBrush(ref _trackPaintBrush);
            DisposePaintBrush(ref _thumbPaintBrush);
            DisposePaintBrush(ref _firstArrowPaintBrush);
            DisposePaintBrush(ref _lastArrowPaintBrush);

            if (_borderPaintPen != null)
            {
                _borderPaintPen.Dispose();
                _borderPaintPen = null;
            }

            _firstArrowPoints = null;
            _lastArrowPoints = null;
        }

        private static void DisposePaintBrush(ref SolidBrush brush)
        {
            if (brush == null)
                return;

            brush.Dispose();
            brush = null;
        }

#if !WINFORMSXAML_PACKAGE
        internal long PaintResourceCreationCountForTest
        {
            get { return _paintResourceCreationCount; }
        }

        internal long ArrowPointArrayCreationCountForTest
        {
            get { return _arrowPointArrayCreationCount; }
        }

        internal int ActivePaintResourceCountForTest
        {
            get
            {
                int count = 0;

                if (_trackPaintBrush != null)
                    count++;
                if (_thumbPaintBrush != null)
                    count++;
                if (_firstArrowPaintBrush != null)
                    count++;
                if (_lastArrowPaintBrush != null)
                    count++;
                if (_borderPaintPen != null)
                    count++;

                return count;
            }
        }
#endif
    }
}
