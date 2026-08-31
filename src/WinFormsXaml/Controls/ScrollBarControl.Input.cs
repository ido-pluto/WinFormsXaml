using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public abstract partial class ScrollBarControl
    {
        private const int MouseWheelDelta = 120;
        private const int RepeatInitialDelay = 400;
        private const int RepeatInterval = 50;

        private ScrollBarHitPart _hoverPart;
        private ScrollBarHitPart _pressedPart;
        private bool _draggingThumb;
        private int _dragOffset;
        private Point _lastMousePoint;
        private Timer _repeatTimer;
        private bool _repeatInitialTick;
        private bool _suppressCaptureCleanup;
        private int _mouseWheelRemainder;

#if !WINFORMSXAML_PACKAGE
        internal object RepeatTimerIdentityForTest
        {
            get { return _repeatTimer; }
        }

        internal bool InteractionActiveForTest
        {
            get { return _pressedPart != ScrollBarHitPart.None; }
        }

        internal void ApplyRepeatTickForTest()
        {
            RepeatTimerTick(this, EventArgs.Empty);
        }
#endif

        /// <summary>Begins arrow, page, or thumb interaction.</summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!Enabled || e.Button != MouseButtons.Left)
                return;

            Focus();
            _lastMousePoint = new Point(e.X, e.Y);
            ScrollBarGeometry geometry = CalculateGeometry();
            ScrollBarHitPart part = HitTest(
                _lastMousePoint,
                geometry);

            if (part == ScrollBarHitPart.None)
                return;

            _pressedPart = part;
            _hoverPart = part;
            Capture = true;

            if (part == ScrollBarHitPart.Thumb)
            {
                _draggingThumb = true;
                _dragOffset = GetAxisCoordinate(
                    _lastMousePoint) - geometry.ThumbStart;
            }
            else
            {
                ExecuteScrollCommand(GetCommandForPart(part));
                StartRepeatTimer();
            }

            Invalidate();
        }

        /// <summary>Updates hover state or an active thumb drag.</summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _lastMousePoint = new Point(e.X, e.Y);
            ScrollBarGeometry geometry = CalculateGeometry();
            ScrollBarHitPart hover = HitTest(
                _lastMousePoint,
                geometry);

            if (_hoverPart != hover)
            {
                _hoverPart = hover;
                Invalidate();
            }

            if (!_draggingThumb ||
                _pressedPart != ScrollBarHitPart.Thumb)
            {
                return;
            }

            int physicalPosition = GetAxisCoordinate(
                _lastMousePoint) -
                _dragOffset -
                geometry.TrackStart;
            physicalPosition = Math.Max(
                0,
                Math.Min(
                    geometry.ThumbTravel,
                    physicalPosition));
            int logicalPosition = IsHorizontalRightToLeft
                ? geometry.ThumbTravel - physicalPosition
                : physicalPosition;
            int effectiveMaximum = GetEffectiveMaximum();
            long logicalRange =
                (long)effectiveMaximum - (long)_minimum;
            long requested = _minimum;

            if (geometry.ThumbTravel > 0 && logicalRange > 0L)
            {
                long numerator =
                    (long)logicalPosition * logicalRange;
                requested +=
                    (numerator +
                     (geometry.ThumbTravel / 2L)) /
                    geometry.ThumbTravel;
            }

            SetValueFromInput(
                requested,
                ScrollEventType.ThumbTrack);
        }

        /// <summary>Completes the current mouse interaction.</summary>
        /// <param name="e">The mouse event data.</param>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButtons.Left ||
                _pressedPart == ScrollBarHitPart.None)
            {
                return;
            }

            if (_draggingThumb)
            {
                SetValueFromInput(
                    _value,
                    ScrollEventType.ThumbPosition);
            }

            ReleaseInteraction(true);
        }

        /// <summary>Clears hover state when the pointer leaves.</summary>
        /// <param name="e">The event data.</param>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (!Capture &&
                _hoverPart != ScrollBarHitPart.None)
            {
                _hoverPart = ScrollBarHitPart.None;
                Invalidate();
            }
        }

        /// <summary>Cancels an interaction when mouse capture is lost.</summary>
        /// <param name="e">The event data.</param>
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);

            if (!_suppressCaptureCleanup &&
                !Capture &&
                _pressedPart != ScrollBarHitPart.None)
            {
                ReleaseInteraction(true);
            }
        }

        /// <summary>Scrolls by the configured mouse-wheel amount.</summary>
        /// <param name="e">The mouse-wheel event data.</param>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            HandledMouseEventArgs handled =
                e as HandledMouseEventArgs;

            if (!Enabled ||
                (handled != null && handled.Handled) ||
                e.Delta == 0)
            {
                return;
            }

            int lines = SystemInformation.MouseWheelScrollLines;

            if (lines == 0)
                return;

            long unit;
            ScrollEventType type;

            if (lines == -1)
            {
                unit = _largeChange;
            }
            else
            {
                unit = (long)_smallChange *
                    (long)Math.Max(0, lines);
            }

            long scaled =
                (long)_mouseWheelRemainder +
                ((long)e.Delta * unit);
            long pixelDelta = scaled / MouseWheelDelta;
            _mouseWheelRemainder = (int)(scaled % MouseWheelDelta);

            if (pixelDelta == 0L)
                return;

            bool increment = pixelDelta < 0L;
            type = lines == -1
                ? increment
                    ? ScrollEventType.LargeIncrement
                    : ScrollEventType.LargeDecrement
                : increment
                    ? ScrollEventType.SmallIncrement
                    : ScrollEventType.SmallDecrement;

            bool moved = SetValueFromInput(
                (long)_value - pixelDelta,
                type);

            if (handled != null && moved)
                handled.Handled = true;
        }

        /// <summary>
        /// Treats scrollbar navigation keys as input keys.
        /// </summary>
        /// <param name="keyData">The key and modifier data.</param>
        /// <returns>true for a scrollbar navigation key.</returns>
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;

            if (key == Keys.Home ||
                key == Keys.End ||
                key == Keys.PageUp ||
                key == Keys.PageDown ||
                key == Keys.Up ||
                key == Keys.Down ||
                key == Keys.Left ||
                key == Keys.Right)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        /// <summary>Handles keyboard line, page, first, and last commands.</summary>
        /// <param name="e">The keyboard event data.</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!Enabled || e.Handled)
                return;

            ScrollEventType type;

            if (e.KeyCode == Keys.Home)
                type = ScrollEventType.First;
            else if (e.KeyCode == Keys.End)
                type = ScrollEventType.Last;
            else if (e.KeyCode == Keys.PageUp)
                type = ScrollEventType.LargeDecrement;
            else if (e.KeyCode == Keys.PageDown)
                type = ScrollEventType.LargeIncrement;
            else if (_vertical && e.KeyCode == Keys.Up)
                type = ScrollEventType.SmallDecrement;
            else if (_vertical && e.KeyCode == Keys.Down)
                type = ScrollEventType.SmallIncrement;
            else if (!_vertical && e.KeyCode == Keys.Left)
            {
                type = IsHorizontalRightToLeft
                    ? ScrollEventType.SmallIncrement
                    : ScrollEventType.SmallDecrement;
            }
            else if (!_vertical && e.KeyCode == Keys.Right)
            {
                type = IsHorizontalRightToLeft
                    ? ScrollEventType.SmallDecrement
                    : ScrollEventType.SmallIncrement;
            }
            else
            {
                return;
            }

            ExecuteScrollCommand(type);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void StartRepeatTimer()
        {
            if (_repeatTimer == null)
            {
                _repeatTimer = new Timer();
                _repeatTimer.Tick +=
                    new EventHandler(RepeatTimerTick);
            }

            _repeatInitialTick = true;
            _repeatTimer.Interval = RepeatInitialDelay;
            _repeatTimer.Start();
        }

        private void RepeatTimerTick(object sender, EventArgs e)
        {
            if (_repeatTimer == null ||
                _pressedPart == ScrollBarHitPart.None ||
                _draggingThumb ||
                !Enabled ||
                IsDisposed ||
                Disposing)
            {
                StopRepeatTimer();
                return;
            }

            if (_repeatInitialTick)
            {
                _repeatInitialTick = false;
                _repeatTimer.Interval = RepeatInterval;
            }

            ScrollBarHitPart current = HitTest(
                _lastMousePoint,
                CalculateGeometry());

            if (current == _pressedPart)
            {
                ExecuteScrollCommand(
                    GetCommandForPart(_pressedPart));
            }
        }

        private void StopRepeatTimer()
        {
            _repeatInitialTick = false;

            if (_repeatTimer != null)
                _repeatTimer.Stop();
        }

        private void ReleaseInteraction(bool raiseEndScroll)
        {
            bool hadInteraction =
                _pressedPart != ScrollBarHitPart.None;
            StopRepeatTimer();
            _pressedPart = ScrollBarHitPart.None;
            _draggingThumb = false;
            _dragOffset = 0;

            if (Capture)
            {
                _suppressCaptureCleanup = true;

                try
                {
                    Capture = false;
                }
                finally
                {
                    _suppressCaptureCleanup = false;
                }
            }

            Invalidate();

            if (raiseEndScroll && hadInteraction)
                RaiseEndScroll();
        }
    }
}
