using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    internal enum ScrollBarHitPart
    {
        None,
        FirstButton,
        LastButton,
        Thumb,
        BeforeThumb,
        AfterThumb
    }

    internal struct ScrollBarGeometry
    {
        public Rectangle InnerBounds;
        public Rectangle FirstButton;
        public Rectangle LastButton;
        public Rectangle Track;
        public Rectangle Thumb;
        public int TrackStart;
        public int TrackLength;
        public int ThumbStart;
        public int ThumbLength;
        public int ThumbTravel;
    }

    public abstract partial class ScrollBarControl
    {
#if !WINFORMSXAML_PACKAGE
        internal ScrollBarGeometry GetScrollBarGeometryForTest()
        {
            return CalculateGeometry();
        }

        internal ScrollBarHitPart HitTestForTest(Point point)
        {
            return HitTest(point, CalculateGeometry());
        }
#endif

        internal ScrollBarGeometry CalculateGeometry()
        {
            ScrollBarGeometry geometry = new ScrollBarGeometry();
            Rectangle client = ClientRectangle;

            if (client.Width <= 0 || client.Height <= 0)
                return geometry;

            int inset = client.Width > 2 && client.Height > 2
                ? 1
                : 0;
            Rectangle inner = Rectangle.Inflate(
                client,
                -inset,
                -inset);

            if (inner.Width < 0)
                inner.Width = 0;
            if (inner.Height < 0)
                inner.Height = 0;

            geometry.InnerBounds = inner;
            int axisLength = _vertical
                ? inner.Height
                : inner.Width;
            int crossLength = _vertical
                ? inner.Width
                : inner.Height;
            int buttonLength = Math.Min(
                axisLength / 2,
                Math.Max(0, crossLength));
            int trackLength = Math.Max(
                0,
                axisLength - (buttonLength * 2));

            if (_vertical)
            {
                geometry.FirstButton = new Rectangle(
                    inner.Left,
                    inner.Top,
                    inner.Width,
                    buttonLength);
                geometry.LastButton = new Rectangle(
                    inner.Left,
                    inner.Bottom - buttonLength,
                    inner.Width,
                    buttonLength);
                geometry.Track = new Rectangle(
                    inner.Left,
                    inner.Top + buttonLength,
                    inner.Width,
                    trackLength);
            }
            else
            {
                geometry.FirstButton = new Rectangle(
                    inner.Left,
                    inner.Top,
                    buttonLength,
                    inner.Height);
                geometry.LastButton = new Rectangle(
                    inner.Right - buttonLength,
                    inner.Top,
                    buttonLength,
                    inner.Height);
                geometry.Track = new Rectangle(
                    inner.Left + buttonLength,
                    inner.Top,
                    trackLength,
                    inner.Height);
            }

            geometry.TrackStart = _vertical
                ? geometry.Track.Top
                : geometry.Track.Left;
            geometry.TrackLength = trackLength;

            if (trackLength <= 0)
                return geometry;

            long completeRange =
                (long)_maximum - (long)_minimum + 1L;
            int thumbLength;

            if (completeRange <= 0L ||
                _largeChange >= completeRange)
            {
                thumbLength = trackLength;
            }
            else
            {
                long requestedLength = _largeChange <= 0
                    ? 0L
                    : ((long)trackLength *
                       (long)_largeChange) /
                      completeRange;
                thumbLength = requestedLength >= Int32.MaxValue
                    ? Int32.MaxValue
                    : (int)requestedLength;
                thumbLength = Math.Max(
                    _style.MinimumThumbLength,
                    thumbLength);
                thumbLength = Math.Min(
                    trackLength,
                    thumbLength);
            }

            geometry.ThumbLength = thumbLength;
            geometry.ThumbTravel = Math.Max(
                0,
                trackLength - thumbLength);
            int effectiveMaximum = GetEffectiveMaximum();
            long logicalRange =
                (long)effectiveMaximum - (long)_minimum;
            int logicalPosition = 0;

            if (logicalRange > 0L &&
                geometry.ThumbTravel > 0)
            {
                long numerator =
                    ((long)_value - (long)_minimum) *
                    (long)geometry.ThumbTravel;
                logicalPosition = (int)(
                    (numerator + (logicalRange / 2L)) /
                    logicalRange);
            }

            int physicalPosition = IsHorizontalRightToLeft
                ? geometry.ThumbTravel - logicalPosition
                : logicalPosition;
            geometry.ThumbStart =
                geometry.TrackStart + physicalPosition;
            geometry.Thumb = _vertical
                ? new Rectangle(
                    geometry.Track.Left,
                    geometry.ThumbStart,
                    geometry.Track.Width,
                    thumbLength)
                : new Rectangle(
                    geometry.ThumbStart,
                    geometry.Track.Top,
                    thumbLength,
                    geometry.Track.Height);
            return geometry;
        }

        private bool IsHorizontalRightToLeft
        {
            get
            {
                return !_vertical &&
                    RightToLeft == RightToLeft.Yes;
            }
        }

        private ScrollBarHitPart HitTest(
            Point point,
            ScrollBarGeometry geometry)
        {
            if (!ClientRectangle.Contains(point))
                return ScrollBarHitPart.None;

            if (geometry.FirstButton.Contains(point))
                return ScrollBarHitPart.FirstButton;

            if (geometry.LastButton.Contains(point))
                return ScrollBarHitPart.LastButton;

            if (geometry.Thumb.Contains(point))
                return ScrollBarHitPart.Thumb;

            if (!geometry.Track.Contains(point))
                return ScrollBarHitPart.None;

            int axis = GetAxisCoordinate(point);

            return axis < geometry.ThumbStart
                ? ScrollBarHitPart.BeforeThumb
                : ScrollBarHitPart.AfterThumb;
        }

        private int GetAxisCoordinate(Point point)
        {
            return _vertical ? point.Y : point.X;
        }

        private ScrollEventType GetCommandForPart(
            ScrollBarHitPart part)
        {
            bool firstPhysical =
                part == ScrollBarHitPart.FirstButton ||
                part == ScrollBarHitPart.BeforeThumb;
            bool increment = firstPhysical &&
                IsHorizontalRightToLeft;

            if (!firstPhysical)
                increment = !IsHorizontalRightToLeft;

            bool large =
                part == ScrollBarHitPart.BeforeThumb ||
                part == ScrollBarHitPart.AfterThumb;

            if (large)
            {
                return increment
                    ? ScrollEventType.LargeIncrement
                    : ScrollEventType.LargeDecrement;
            }

            return increment
                ? ScrollEventType.SmallIncrement
                : ScrollEventType.SmallDecrement;
        }
    }
}
