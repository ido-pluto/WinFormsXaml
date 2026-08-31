using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal static class FrameworkScrollBarTests
    {
        private delegate void TestAction();

        private sealed class VerticalProbe : VerticalScrollBar
        {
            public void Press(Point point)
            {
                OnMouseDown(new MouseEventArgs(
                    MouseButtons.Left,
                    1,
                    point.X,
                    point.Y,
                    0));
            }

            public void MovePointer(Point point)
            {
                OnMouseMove(new MouseEventArgs(
                    MouseButtons.None,
                    0,
                    point.X,
                    point.Y,
                    0));
            }

            public void Release(Point point)
            {
                OnMouseUp(new MouseEventArgs(
                    MouseButtons.Left,
                    1,
                    point.X,
                    point.Y,
                    0));
            }

            public HandledMouseEventArgs Wheel(int delta)
            {
                HandledMouseEventArgs args =
                    new HandledMouseEventArgs(
                        MouseButtons.None,
                        0,
                        0,
                        0,
                        delta);
                OnMouseWheel(args);
                return args;
            }

            public KeyEventArgs Key(Keys key)
            {
                KeyEventArgs args = new KeyEventArgs(key);
                OnKeyDown(args);
                return args;
            }

            public void LoseCapture()
            {
                Capture = false;
                OnMouseCaptureChanged(EventArgs.Empty);
            }

            public void PaintTo(Graphics graphics)
            {
                OnPaint(new PaintEventArgs(
                    graphics,
                    ClientRectangle));
            }
        }

        private sealed class HorizontalProbe : HorizontalScrollBar
        {
            public void Press(Point point)
            {
                OnMouseDown(new MouseEventArgs(
                    MouseButtons.Left,
                    1,
                    point.X,
                    point.Y,
                    0));
            }

            public void MovePointer(Point point)
            {
                OnMouseMove(new MouseEventArgs(
                    MouseButtons.None,
                    0,
                    point.X,
                    point.Y,
                    0));
            }

            public void Release(Point point)
            {
                OnMouseUp(new MouseEventArgs(
                    MouseButtons.Left,
                    1,
                    point.X,
                    point.Y,
                    0));
            }

            public HandledMouseEventArgs Wheel(int delta)
            {
                HandledMouseEventArgs args =
                    new HandledMouseEventArgs(
                        MouseButtons.None,
                        0,
                        0,
                        0,
                        delta);
                OnMouseWheel(args);
                return args;
            }

            public KeyEventArgs Key(Keys key)
            {
                KeyEventArgs args = new KeyEventArgs(key);
                OnKeyDown(args);
                return args;
            }
        }

        internal static void RunAll()
        {
            TestXmlConstructionAndNestedStyle();
            TestStyleDefaultsForwardingAndInvalidation();
            TestRangeAndEventContract();
            TestGeometryAndOverflowSafety();
            TestArrowPageRepeatAndCaptureCleanup();
            TestThumbDragAndReleaseEvents();
            TestWheelAndKeyboardCommands();
            TestHorizontalRightToLeftMapping();
            TestValueChangesInvalidateOnlyThumbTravel();
            TestOwnerPaintingAndLifecycle();
        }

        private static void TestValueChangesInvalidateOnlyThumbTravel()
        {
            using (VerticalProbe bar = new VerticalProbe())
            {
                bar.Size = new Size(20, 400);
                bar.Maximum = 2000;
                bar.LargeChange = 50;
                bar.CreateControl();
                Rectangle invalidated = Rectangle.Empty;

                bar.Invalidated +=
                    delegate(object sender, InvalidateEventArgs e)
                    {
                        invalidated = e.InvalidRect;
                    };

                bar.Value = 20;

                AssertTrue(
                    !invalidated.IsEmpty &&
                    invalidated.Width == bar.ClientSize.Width &&
                    invalidated.Height < bar.ClientSize.Height,
                    "a value-only change repaints thumb travel instead of the complete scrollbar");
            }
        }

        private static void TestXmlConstructionAndNestedStyle()
        {
            const string markup =
                "<Panel>" +
                "  <VerticalScrollBar Name='Vertical' Maximum='250' " +
                "      LargeChange='25' Value='40' TrackColor='#112233' />" +
                "  <HorizontalScrollBar Name='Horizontal' Maximum='400' " +
                "      LargeChange='40' Value='80' RightToLeft='Yes'>" +
                "    <HorizontalScrollBar.Style>" +
                "      <ScrollBarStyle TrackColor='#202124' " +
                "          ThumbColor='#80868B' Thickness='18' />" +
                "    </HorizontalScrollBar.Style>" +
                "  </HorizontalScrollBar>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                VerticalScrollBar vertical =
                    runtime.Get<VerticalScrollBar>("Vertical");
                HorizontalScrollBar horizontal =
                    runtime.Get<HorizontalScrollBar>("Horizontal");

                AssertTrue(vertical != null, "XML creates VerticalScrollBar");
                AssertTrue(horizontal != null, "XML creates HorizontalScrollBar");
                AssertEqual(40, vertical.Value, "XML assigns vertical Value");
                AssertEqual(
                    Color.FromArgb(0x11, 0x22, 0x33),
                    vertical.TrackColor,
                    "XML assigns direct style convenience colors");
                AssertEqual(80, horizontal.Value, "XML assigns horizontal Value");
                AssertEqual(
                    RightToLeft.Yes,
                    horizontal.RightToLeft,
                    "XML assigns horizontal RTL direction");
                AssertEqual(
                    Color.FromArgb(0x20, 0x21, 0x24),
                    horizontal.Style.TrackColor,
                    "nested ScrollBarStyle assigns track color");
                AssertEqual(
                    Color.FromArgb(0x80, 0x86, 0x8B),
                    horizontal.Style.ThumbColor,
                    "nested ScrollBarStyle assigns thumb color");
                AssertEqual(
                    18,
                    horizontal.Thickness,
                    "nested ScrollBarStyle assigns thickness");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestStyleDefaultsForwardingAndInvalidation()
        {
            ScrollBarStyle style = new ScrollBarStyle();

            AssertEqual(
                SystemColors.ScrollBar,
                style.TrackColor,
                "default track uses the Windows scroll color");
            AssertEqual(
                SystemColors.Control,
                style.ThumbColor,
                "default thumb uses the Windows control color");
            AssertEqual(16, style.Thickness, "default thickness");
            AssertEqual(
                8,
                style.MinimumThumbLength,
                "default minimum thumb length");

            int changed = 0;
            style.Changed += delegate { changed++; };
            style.TrackColor = Color.Navy;
            style.ThumbColor = Color.Silver;
            style.ThumbHoverColor = Color.White;
            style.ThumbPressedColor = Color.Gray;
            style.ArrowColor = Color.Yellow;
            style.ArrowHoverColor = Color.Orange;
            style.BorderColor = Color.Black;
            AssertEqual(7, changed, "each changed color notifies once");
            style.BorderColor = Color.Black;
            AssertEqual(7, changed, "same color does not notify again");

            AssertThrowsArgumentOutOfRange(
                delegate { style.Thickness = 0; },
                "zero thickness is rejected");
            AssertThrowsArgumentOutOfRange(
                delegate { style.MinimumThumbLength = 0; },
                "zero minimum thumb length is rejected");

            using (VerticalProbe vertical = new VerticalProbe())
            using (HorizontalProbe horizontal = new HorizontalProbe())
            {
                vertical.CreateControl();
                horizontal.CreateControl();
                vertical.Style = style;
                horizontal.Style = style;
                int verticalInvalidated = 0;
                int horizontalInvalidated = 0;
                vertical.Invalidated +=
                    delegate { verticalInvalidated++; };
                horizontal.Invalidated +=
                    delegate { horizontalInvalidated++; };

                style.TrackColor = Color.Purple;
                AssertTrue(
                    verticalInvalidated > 0 &&
                    horizontalInvalidated > 0,
                    "one shared style invalidates every attached control");

                vertical.ThumbColor = Color.Lime;
                AssertEqual(
                    Color.Lime,
                    style.ThumbColor,
                    "direct convenience color forwards to Style");

                int oldWidth = vertical.Width;
                int oldHeight = horizontal.Height;
                style.Thickness = 19;
                AssertTrue(
                    vertical.Width != oldWidth &&
                    vertical.Width == 19,
                    "default vertical width follows style thickness");
                AssertTrue(
                    horizontal.Height != oldHeight &&
                    horizontal.Height == 19,
                    "default horizontal height follows style thickness");

                vertical.Width = 31;
                style.Thickness = 21;
                AssertEqual(
                    31,
                    vertical.Width,
                    "explicit vertical width is not overwritten by style");
            }
        }

        private static void TestRangeAndEventContract()
        {
            using (VerticalProbe bar = new VerticalProbe())
            {
                bar.Minimum = -100;
                bar.Maximum = 100;
                bar.LargeChange = 20;
                bar.SmallChange = 5;
                AssertEqual(
                    81,
                    bar.EffectiveMaximumForTest,
                    "effective maximum matches WinForms viewport semantics");

                ArrayList order = new ArrayList();
                ArrayList types = new ArrayList();
                int valueChanged = 0;
                bar.Scroll += delegate(object sender, ScrollEventArgs e)
                {
                    order.Add("Scroll");
                    types.Add(e.Type);

                    if (e.Type == ScrollEventType.SmallIncrement)
                        e.NewValue += 2;
                };
                bar.ValueChanged += delegate
                {
                    order.Add("ValueChanged");
                    valueChanged++;
                };

                bar.Value = 10;
                AssertEqual(1, valueChanged, "programmatic value changes notify");
                AssertEqual(
                    1,
                    order.Count,
                    "programmatic Value does not raise Scroll");

                order.Clear();
                bar.ExecuteScrollCommand(
                    ScrollEventType.SmallIncrement);
                AssertEqual(
                    17,
                    bar.Value,
                    "Scroll handler can adjust NewValue before commit");
                AssertEqual("Scroll", order[0], "Scroll is raised first");
                AssertEqual(
                    "ValueChanged",
                    order[1],
                    "ValueChanged follows a committed input value");

                bar.ExecuteScrollCommand(ScrollEventType.Last);
                AssertEqual(81, bar.Value, "Last reaches effective maximum");
                int beforeBoundaryValueChanged = valueChanged;
                int beforeBoundaryScrolls = types.Count;
                bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);
                AssertEqual(
                    beforeBoundaryValueChanged,
                    valueChanged,
                    "boundary command does not raise ValueChanged");
                AssertEqual(
                    beforeBoundaryScrolls + 1,
                    types.Count,
                    "boundary command still reports Scroll input");

                bar.Maximum = 25;
                AssertEqual(
                    6,
                    bar.Value,
                    "range shrink coerces Value to its new effective maximum");

                AssertThrowsArgumentOutOfRange(
                    delegate { bar.LargeChange = -1; },
                    "negative LargeChange is rejected");
                AssertThrowsArgumentOutOfRange(
                    delegate { bar.SmallChange = -1; },
                    "negative SmallChange is rejected");
                AssertThrowsArgumentOutOfRange(
                    delegate { bar.Value = 100; },
                    "Value outside effective range is rejected");
            }
        }

        private static void TestGeometryAndOverflowSafety()
        {
            using (VerticalProbe vertical = new VerticalProbe())
            {
                vertical.Size = new Size(16, 220);
                vertical.Minimum = 0;
                vertical.Maximum = 999;
                vertical.LargeChange = 100;
                vertical.MinimumThumbLength = 9;
                ScrollBarGeometry minimum =
                    vertical.GetScrollBarGeometryForTest();

                AssertTrue(
                    minimum.FirstButton.Bottom == minimum.Track.Top &&
                    minimum.Track.Bottom == minimum.LastButton.Top,
                    "vertical buttons and track are contiguous");
                AssertTrue(
                    minimum.ThumbLength >= 9 &&
                    minimum.ThumbLength <= minimum.TrackLength,
                    "thumb length obeys configured and track limits");
                AssertEqual(
                    minimum.TrackStart,
                    minimum.ThumbStart,
                    "minimum value places vertical thumb first");

                vertical.Value = vertical.EffectiveMaximumForTest;
                ScrollBarGeometry maximum =
                    vertical.GetScrollBarGeometryForTest();
                AssertEqual(
                    maximum.TrackStart + maximum.ThumbTravel,
                    maximum.ThumbStart,
                    "effective maximum places vertical thumb last");

                vertical.Minimum = Int32.MinValue;
                vertical.Maximum = Int32.MaxValue;
                vertical.LargeChange = 1;
                vertical.Value = Int32.MaxValue;
                ScrollBarGeometry extreme =
                    vertical.GetScrollBarGeometryForTest();
                AssertTrue(
                    extreme.Track.Contains(Center(extreme.Thumb)) &&
                    extreme.Thumb.Bottom <= extreme.Track.Bottom,
                    "full Int32 range geometry stays inside the track");

                vertical.Size = new Size(1, 1);
                ScrollBarGeometry tiny =
                    vertical.GetScrollBarGeometryForTest();
                AssertTrue(
                    tiny.Track.Width >= 0 &&
                    tiny.Track.Height >= 0 &&
                    tiny.Thumb.Width >= 0 &&
                    tiny.Thumb.Height >= 0,
                    "tiny controls never produce negative geometry");
            }

            using (HorizontalProbe horizontal = new HorizontalProbe())
            {
                horizontal.Size = new Size(240, 16);
                horizontal.Minimum = 0;
                horizontal.Maximum = 100;
                horizontal.LargeChange = 10;
                ScrollBarGeometry ltrMinimum =
                    horizontal.GetScrollBarGeometryForTest();

                horizontal.Value =
                    horizontal.EffectiveMaximumForTest;
                ScrollBarGeometry ltrMaximum =
                    horizontal.GetScrollBarGeometryForTest();
                AssertTrue(
                    ltrMinimum.ThumbStart < ltrMaximum.ThumbStart,
                    "LTR horizontal values grow from left to right");

                horizontal.RightToLeft = RightToLeft.Yes;
                horizontal.Value = 0;
                ScrollBarGeometry rtlMinimum =
                    horizontal.GetScrollBarGeometryForTest();
                horizontal.Value =
                    horizontal.EffectiveMaximumForTest;
                ScrollBarGeometry rtlMaximum =
                    horizontal.GetScrollBarGeometryForTest();
                AssertTrue(
                    rtlMinimum.ThumbStart > rtlMaximum.ThumbStart,
                    "RTL horizontal values grow from right to left");
            }
        }

        private static void TestArrowPageRepeatAndCaptureCleanup()
        {
            VerticalProbe bar = new VerticalProbe();

            try
            {
                bar.Size = new Size(16, 220);
                bar.Minimum = 0;
                bar.Maximum = 300;
                bar.LargeChange = 30;
                bar.SmallChange = 5;
                bar.Value = 100;
                bar.CreateControl();
                ArrayList types = new ArrayList();
                bar.Scroll += delegate(object sender, ScrollEventArgs e)
                {
                    types.Add(e.Type);
                };

                ScrollBarGeometry geometry =
                    bar.GetScrollBarGeometryForTest();
                Point first = Center(geometry.FirstButton);
                bar.Press(first);
                AssertEqual(95, bar.Value, "first arrow decrements once");
                object timer = bar.RepeatTimerIdentityForTest;
                AssertTrue(
                    timer != null && bar.InteractionActiveForTest,
                    "arrow press captures interaction and creates repeat timer");

                bar.ApplyRepeatTickForTest();
                AssertEqual(90, bar.Value, "repeat tick repeats arrow command");
                AssertTrue(
                    Object.ReferenceEquals(
                        timer,
                        bar.RepeatTimerIdentityForTest),
                    "repeat uses one timer instance");
                bar.Release(first);
                AssertTrue(
                    !bar.InteractionActiveForTest && !bar.Capture,
                    "mouse release clears press and capture");
                AssertEqual(
                    ScrollEventType.EndScroll,
                    types[types.Count - 1],
                    "mouse interaction ends with EndScroll");

                geometry = bar.GetScrollBarGeometryForTest();
                Point beforeThumb = new Point(
                    geometry.Track.Left +
                    Math.Max(0, geometry.Track.Width / 2),
                    geometry.Thumb.Top - 1);
                int beforePage = bar.Value;
                bar.Press(beforeThumb);
                AssertTrue(
                    bar.Value < beforePage,
                    "track before thumb performs LargeDecrement");
                bar.Release(beforeThumb);

                geometry = bar.GetScrollBarGeometryForTest();
                Point last = Center(geometry.LastButton);
                bar.Press(last);
                AssertTrue(
                    bar.InteractionActiveForTest,
                    "second arrow press starts an interaction");
                bar.LoseCapture();
                AssertTrue(
                    !bar.InteractionActiveForTest,
                    "capture loss cleans up press-repeat state");
            }
            finally
            {
                bar.Dispose();
            }

            AssertTrue(
                bar.RepeatTimerIdentityForTest == null,
                "disposal releases the repeat timer");
        }

        private static void TestThumbDragAndReleaseEvents()
        {
            using (VerticalProbe bar = new VerticalProbe())
            {
                bar.Size = new Size(16, 240);
                bar.Maximum = 1000;
                bar.LargeChange = 100;
                bar.CreateControl();
                ArrayList types = new ArrayList();
                bar.Scroll += delegate(object sender, ScrollEventArgs e)
                {
                    types.Add(e.Type);
                };

                ScrollBarGeometry geometry =
                    bar.GetScrollBarGeometryForTest();
                Point thumb = Center(geometry.Thumb);
                bar.Press(thumb);
                Point nearEnd = new Point(
                    thumb.X,
                    geometry.Track.Bottom - 1);
                bar.MovePointer(nearEnd);
                AssertTrue(
                    bar.Value > 0,
                    "thumb drag maps pointer travel into a logical value");
                bar.Release(nearEnd);

                AssertTrue(
                    types.Contains(ScrollEventType.ThumbTrack),
                    "drag movement raises ThumbTrack");
                AssertEqual(
                    ScrollEventType.ThumbPosition,
                    types[types.Count - 2],
                    "release raises ThumbPosition before completion");
                AssertEqual(
                    ScrollEventType.EndScroll,
                    types[types.Count - 1],
                    "release finishes with EndScroll");
            }
        }

        private static void TestWheelAndKeyboardCommands()
        {
            using (VerticalProbe bar = new VerticalProbe())
            {
                bar.Maximum = 500;
                bar.LargeChange = 50;
                bar.SmallChange = 4;
                bar.Value = 100;
                bar.CreateControl();

                KeyEventArgs down = bar.Key(Keys.Down);
                AssertEqual(104, bar.Value, "Down performs SmallIncrement");
                AssertTrue(
                    down.Handled && down.SuppressKeyPress,
                    "handled navigation key suppresses native key processing");
                bar.Key(Keys.Up);
                AssertEqual(100, bar.Value, "Up performs SmallDecrement");
                bar.Key(Keys.PageDown);
                AssertEqual(150, bar.Value, "PageDown performs LargeIncrement");
                bar.Key(Keys.Home);
                AssertEqual(0, bar.Value, "Home reaches Minimum");
                bar.Key(Keys.End);
                AssertEqual(
                    bar.EffectiveMaximumForTest,
                    bar.Value,
                    "End reaches effective maximum");

                if (SystemInformation.MouseWheelScrollLines != 0)
                {
                    bar.Value = 100;
                    HandledMouseEventArgs wheel = bar.Wheel(-120);
                    AssertTrue(
                        bar.Value > 100 && wheel.Handled,
                        "negative wheel delta increments and is marked handled");

                    bar.Value = 100;
                    wheel = bar.Wheel(-30);
                    AssertTrue(
                        bar.Value > 100 && wheel.Handled,
                        "precision wheel input moves before a complete notch");
                }

                bar.Value = 100;
                bar.MouseWheel += delegate(object sender, MouseEventArgs e)
                {
                    HandledMouseEventArgs handled =
                        e as HandledMouseEventArgs;

                    if (handled != null)
                        handled.Handled = true;
                };
                bar.Wheel(-120);
                AssertEqual(
                    100,
                    bar.Value,
                    "handled MouseWheel subscriber suppresses scrollbar input");
            }
        }

        private static void TestHorizontalRightToLeftMapping()
        {
            using (HorizontalProbe bar = new HorizontalProbe())
            {
                bar.Size = new Size(260, 16);
                bar.Maximum = 500;
                bar.LargeChange = 50;
                bar.SmallChange = 5;
                bar.RightToLeft = RightToLeft.Yes;
                bar.Value = 200;
                bar.CreateControl();

                bar.Key(Keys.Left);
                AssertEqual(
                    205,
                    bar.Value,
                    "RTL Left key increments logical value toward physical left");
                bar.Key(Keys.Right);
                AssertEqual(
                    200,
                    bar.Value,
                    "RTL Right key decrements logical value toward physical right");

                ScrollBarGeometry geometry =
                    bar.GetScrollBarGeometryForTest();
                Point leftButton = Center(geometry.FirstButton);
                bar.Press(leftButton);
                AssertEqual(
                    205,
                    bar.Value,
                    "RTL physical left arrow increments logical value");
                bar.Release(leftButton);

                geometry = bar.GetScrollBarGeometryForTest();
                Point thumb = Center(geometry.Thumb);
                int beforeDrag = bar.Value;
                bar.Press(thumb);
                bar.MovePointer(new Point(
                    geometry.Track.Left,
                    thumb.Y));
                AssertTrue(
                    bar.Value > beforeDrag,
                    "RTL drag toward physical left increases logical value");
                bar.Release(new Point(
                    geometry.Track.Left,
                    thumb.Y));

                if (SystemInformation.MouseWheelScrollLines != 0)
                {
                    bar.Value = 200;
                    bar.Wheel(-120);
                    AssertTrue(
                        bar.Value > 200,
                        "RTL horizontal wheel preserves logical forward direction");
                }
            }
        }

        private static void TestOwnerPaintingAndLifecycle()
        {
            ScrollBarStyle style = new ScrollBarStyle();
            style.TrackColor = Color.Magenta;
            style.ThumbColor = Color.Lime;
            style.ThumbHoverColor = Color.Cyan;
            style.ThumbPressedColor = Color.Red;
            style.ArrowColor = Color.Yellow;
            style.ArrowHoverColor = Color.Orange;
            style.BorderColor = Color.Blue;
            VerticalProbe bar = new VerticalProbe();

            try
            {
                bar.Style = style;
                bar.Size = new Size(20, 220);
                bar.Maximum = 200;
                bar.LargeChange = 40;
                bar.CreateControl();
                AssertEqual(
                    0,
                    bar.Controls.Count,
                    "framework scrollbar owns no child controls");
                AssertEqual(
                    AccessibleRole.ScrollBar,
                    bar.AccessibleRole,
                    "framework scrollbar exposes its MSAA role");
                ScrollBarGeometry geometry =
                    bar.GetScrollBarGeometryForTest();

                using (Bitmap bitmap = new Bitmap(
                    bar.Width,
                    bar.Height))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    bar.PaintTo(graphics);
                    long paintResources =
                        bar.PaintResourceCreationCountForTest;
                    long pointArrays =
                        bar.ArrowPointArrayCreationCountForTest;
                    int paintIndex;

                    AssertEqual(
                        5,
                        bar.ActivePaintResourceCountForTest,
                        "first paint creates one resource per paint role");
                    AssertEqual(
                        2L,
                        pointArrays,
                        "first paint creates two reusable arrow point arrays");

                    for (paintIndex = 0; paintIndex < 128; paintIndex++)
                        bar.PaintTo(graphics);

                    AssertEqual(
                        paintResources,
                        bar.PaintResourceCreationCountForTest,
                        "stable smooth paints allocate no additional GDI resources");
                    AssertEqual(
                        pointArrays,
                        bar.ArrowPointArrayCreationCountForTest,
                        "stable smooth paints allocate no additional point arrays");
                    AssertEqual(
                        Color.Lime.ToArgb(),
                        bitmap.GetPixel(
                            Center(geometry.Thumb).X,
                            Center(geometry.Thumb).Y).ToArgb(),
                        "normal thumb paints ThumbColor");

                    Point trackPoint = new Point(
                        geometry.Track.Left +
                        geometry.Track.Width / 2,
                        geometry.Thumb.Bottom + 2);
                    AssertEqual(
                        Color.Magenta.ToArgb(),
                        bitmap.GetPixel(
                            trackPoint.X,
                            trackPoint.Y).ToArgb(),
                        "track paints TrackColor");

                    Point thumb = Center(geometry.Thumb);
                    bar.MovePointer(thumb);
                    bar.PaintTo(graphics);
                    AssertEqual(
                        Color.Cyan.ToArgb(),
                        bitmap.GetPixel(thumb.X, thumb.Y).ToArgb(),
                        "hovered thumb paints ThumbHoverColor");

                    bar.Press(thumb);
                    bar.PaintTo(graphics);
                    AssertEqual(
                        Color.Red.ToArgb(),
                        bitmap.GetPixel(thumb.X, thumb.Y).ToArgb(),
                        "pressed thumb paints ThumbPressedColor");
                    bar.Release(thumb);
                }

                ScrollBarGeometry repeatGeometry =
                    bar.GetScrollBarGeometryForTest();
                bar.Press(Center(repeatGeometry.LastButton));
                AssertTrue(
                    bar.RepeatTimerIdentityForTest != null,
                    "lifecycle fixture creates the repeat timer");
            }
            finally
            {
                bar.Dispose();
            }

            AssertEqual(
                0,
                bar.ActivePaintResourceCountForTest,
                "disposal releases every cached paint resource");

            int invalidatedAfterDispose = 0;
            bar.Invalidated += delegate { invalidatedAfterDispose++; };
            style.TrackColor = Color.Black;
            AssertEqual(
                0,
                invalidatedAfterDispose,
                "disposed scrollbar detaches from a shared style");
            AssertTrue(
                bar.RepeatTimerIdentityForTest == null &&
                !bar.InteractionActiveForTest,
                "disposal releases repeat and capture state");
        }

        private static Point Center(Rectangle rectangle)
        {
            return new Point(
                rectangle.Left + Math.Max(0, rectangle.Width / 2),
                rectangle.Top + Math.Max(0, rectangle.Height / 2));
        }

        private static void AssertThrowsArgumentOutOfRange(
            TestAction action,
            string message)
        {
            try
            {
                action();
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Assertion failed: " + message + ".");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message + ".");
            }
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message +
                    ". Expected <" + expected +
                    ">, actual <" + actual + ">.");
            }
        }
    }
}
