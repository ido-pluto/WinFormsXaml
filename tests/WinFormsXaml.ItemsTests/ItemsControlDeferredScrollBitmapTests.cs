using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    /// <summary>
    /// Guards the nonvirtual styled and native bitmap transactions. Tests use
    /// the real WinForms timer and observe the public/native control state;
    /// they never advance a synthetic smooth-scroll frame.
    /// </summary>
    internal static class ItemsControlDeferredScrollBitmapTests
    {
        private const int WindowNonClientHitTest = 0x0084;
        private const int WindowHorizontalScroll = 0x0114;
        private const int WindowVerticalScroll = 0x0115;
        private const int ScrollBarLineIncrement = 1;
        private const int ScrollBarPageIncrement = 3;
        private const int ScrollBarThumbTrack = 5;
        private const int NativeHorizontalScrollBar = 0;
        private const int NativeVerticalScrollBar = 1;
        private const int ScrollInfoPosition = 0x0004;
        private const int HitTransparent = -1;
        private const uint ChildWindowSkipInvisible = 0x0001;
        private const uint GetFirstChildWindow = 5;
        private const uint GetNextSiblingWindow = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr ChildWindowFromPointEx(
            IntPtr parent,
            Point point,
            uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr window,
            int message,
            IntPtr wordParameter,
            IntPtr longParameter);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(
            IntPtr window,
            uint command);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(
            IntPtr window,
            out NativeRectangle rectangle);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetScrollInfo(
            IntPtr window,
            int bar,
            ref NativeScrollInfo info);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public Rectangle ToRectangle()
            {
                return Rectangle.FromLTRB(
                    Left,
                    Top,
                    Right,
                    Bottom);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeScrollInfo
        {
            public int Size;
            public int Mask;
            public int Minimum;
            public int Maximum;
            public int Page;
            public int Position;
            public int TrackPosition;
        }

        private sealed class Row
        {
            public readonly string Id;
            public readonly string Title;

            public Row(int index)
            {
                Id = "deferred-scroll-" + index;
                Title = "Deferred scroll row " + index;
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly XamlRuntime _runtime;
            private readonly Form _form;
            private readonly Button _focusSink;

            public readonly XamlRuntime.ItemsControl Host;
            public readonly ScrollBarControl Bar;
            public readonly Control FirstRow;

            public Fixture()
                : this(44, true)
            {
            }

            public Fixture(int itemHeight)
                : this(itemHeight, true)
            {
            }

            public Fixture(int itemHeight, bool smoothScroll)
            {
                string markup =
                    "<Panel>" +
                    "  <Presets Name='Theme' Selected='Dark'>" +
                    "    <Preset Name='Dark'>" +
                    "      <Set Key='ItemBackground' Value='#262B32' />" +
                    "      <Set Key='ItemBorder' Value='#454C57' />" +
                    "      <Set Key='Accent' Value='#4F9DFF' />" +
                    "      <Set Key='Text' Value='#F3F5F7' />" +
                    "      <Set Key='Link' Value='#69AEFF' />" +
                    "    </Preset>" +
                    "  </Presets>" +
                    "<ItemsControl Name='Rows' Width='360' Height='180' " +
                    "AutoScroll='true' Virtualizing='false' " +
                    "ProgressiveRendering='false' SmoothScroll='" +
                    (smoothScroll ? "true" : "false") + "' " +
                    "SmoothScrollDuration='420' ItemKeyPath='Id' " +
                    "Spacing='2' ScrollBarGap='4'>" +
                    "  <ItemsControl.VerticalScrollStyle>" +
                    "    <ScrollBarStyle Thickness='16' " +
                    "        TrackColor='#202124' ThumbColor='#80868B' />" +
                    "  </ItemsControl.VerticalScrollStyle>" +
                    "  <ItemsControl.ItemTemplate>" +
                    "    <Border Width='320' Height='" +
                    itemHeight.ToString() + "' " +
                    "            Padding='2' " +
                    "            Background='{Preset Theme.ItemBackground}' " +
                    "            BorderBrush='{Preset Theme.ItemBorder}'>" +
                    "      <StackPanel Orientation='Horizontal' Spacing='4'>" +
                    "        <Panel Width='18' Height='18' " +
                    "               BackColor='{Preset Theme.Accent}' />" +
                    "        <StackPanel Width='286' Orientation='Vertical'>" +
                    "          <Label Height='18' " +
                    "                 ForeColor='{Preset Theme.Text}' " +
                    "                 Text='{Binding Title}' />" +
                    "          <HyperlinkLabel Height='18' Text='Open' " +
                    "                 LinkColor='{Preset Theme.Link}' />" +
                    "        </StackPanel>" +
                    "      </StackPanel>" +
                    "    </Border>" +
                    "  </ItemsControl.ItemTemplate>" +
                    "</ItemsControl>" +
                    "</Panel>";

                _runtime = XamlRuntime.Load(markup);
                _form = new Form();
                _focusSink = new Button();
                Host = _runtime.GetItemsControl("Rows");

                _form.ClientSize = new Size(520, 230);
                Host.Location = new Point(12, 12);
                _focusSink.Bounds = new Rectangle(400, 12, 90, 28);
                _focusSink.Text = "Focus";
                _form.Controls.Add(Host);
                _form.Controls.Add(_focusSink);
                Host.SetItems(CreateRows(40));
                _form.Show();
                PumpForMilliseconds(40);
                _focusSink.Focus();
                Application.DoEvents();

                Bar = Host.ThemedScrollBarForTest;
                FirstRow = FindFirstRenderedRow(Host);

                AssertTrue(
                    Bar != null && Bar.Visible,
                    "deferred-scroll fixture exposes styled chrome");
                AssertTrue(
                    FirstRow != null && FirstRow.IsHandleCreated,
                    "deferred-scroll fixture exposes native row handles");
                AssertTrue(
                    !Host.ContainsFocus,
                    "deferred-scroll fixture keeps focus outside the viewport");
                AssertTrue(
                    !Host.DirectVirtualActive && !Host.LightweightActive,
                    "deferred-scroll fixture uses the nonvirtual renderer");
            }

            public Form Form
            {
                get { return _form; }
            }

            public void KeepFocusOutsideViewport()
            {
                _focusSink.Focus();
                Application.DoEvents();
                AssertTrue(
                    !Host.ContainsFocus,
                    "test preparation keeps the bitmap path eligible");
            }

            public void Dispose()
            {
                _form.Dispose();
                _runtime.Dispose();
            }
        }

        private sealed class NativeFixture : IDisposable
        {
            private readonly XamlRuntime _runtime;
            private readonly Form _form;
            private readonly Button _focusSink;

            public readonly XamlRuntime.ItemsControl Host;
            public readonly Control FirstRow;

            public NativeFixture()
                : this(false, false, true)
            {
            }

            public NativeFixture(bool horizontal, bool rightToLeft)
                : this(horizontal, rightToLeft, true)
            {
            }

            public NativeFixture(
                bool horizontal,
                bool rightToLeft,
                bool smoothScroll)
            {
                string orientation = horizontal
                    ? "Horizontal"
                    : "Vertical";
                int itemWidth = horizontal ? 124 : 320;
                int itemHeight = horizontal ? 140 : 44;
                string markup =
                    "<Panel>" +
                    "  <Presets Name='Theme' Selected='Dark'>" +
                    "    <Preset Name='Dark'>" +
                    "      <Set Key='ItemBackground' Value='#262B32' />" +
                    "      <Set Key='ItemBorder' Value='#454C57' />" +
                    "      <Set Key='Accent' Value='#4F9DFF' />" +
                    "      <Set Key='Text' Value='#F3F5F7' />" +
                    "      <Set Key='Link' Value='#69AEFF' />" +
                    "    </Preset>" +
                    "  </Presets>" +
                    "<ItemsControl Name='Rows' Width='360' Height='180' " +
                    "AutoScroll='true' Virtualizing='false' " +
                    "ProgressiveRendering='false' SmoothScroll='" +
                    (smoothScroll ? "true" : "false") + "' " +
                    "SmoothScrollDuration='420' ItemKeyPath='Id' " +
                    "Spacing='2' Orientation='" + orientation + "'>" +
                    "  <ItemsControl.ItemTemplate>" +
                    "    <Border Width='" + itemWidth.ToString() +
                    "' Height='" + itemHeight.ToString() + "' " +
                    "            Padding='2' " +
                    "            Background='{Preset Theme.ItemBackground}' " +
                    "            BorderBrush='{Preset Theme.ItemBorder}'>" +
                    "      <StackPanel Orientation='Horizontal' Spacing='4'>" +
                    "        <Panel Width='18' Height='18' " +
                    "               BackColor='{Preset Theme.Accent}' />" +
                    "        <StackPanel Width='92' Orientation='Vertical'>" +
                    "          <Label Height='18' " +
                    "                 ForeColor='{Preset Theme.Text}' " +
                    "                 Text='{Binding Title}' />" +
                    "          <HyperlinkLabel Height='18' Text='Open' " +
                    "                 LinkColor='{Preset Theme.Link}' />" +
                    "        </StackPanel>" +
                    "      </StackPanel>" +
                    "    </Border>" +
                    "  </ItemsControl.ItemTemplate>" +
                    "</ItemsControl>" +
                    "</Panel>";

                _runtime = XamlRuntime.Load(markup);
                _form = new Form();
                _focusSink = new Button();
                Host = _runtime.GetItemsControl("Rows");
                Host.ContentRightToLeft = rightToLeft;

                _form.ClientSize = new Size(520, 230);
                Host.Location = new Point(12, 12);
                _focusSink.Bounds = new Rectangle(400, 12, 90, 28);
                _focusSink.Text = "Focus";
                _form.Controls.Add(Host);
                _form.Controls.Add(_focusSink);
                Host.SetItems(CreateRows(40));
                _form.Show();
                PumpForMilliseconds(40);
                _focusSink.Focus();
                Application.DoEvents();

                FirstRow = FindFirstRenderedRow(Host);

                AssertTrue(
                    Host.ThemedScrollBarForTest == null,
                    "native bitmap fixture does not create styled chrome");
                AssertTrue(
                    Host.ActiveNativeScrollStyleVisibleForTest,
                    "native bitmap fixture exposes the native scrollbar");
                AssertTrue(
                    FirstRow != null && FirstRow.IsHandleCreated,
                    "native bitmap fixture exposes native row handles");
                AssertTrue(
                    !Host.ContainsFocus,
                    "native bitmap fixture keeps focus outside the viewport");
            }

            public void Dispose()
            {
                _form.Dispose();
                _runtime.Dispose();
            }
        }

        private sealed class NaturalFrameProbe : IDisposable
        {
            private readonly XamlRuntime.ItemsControl _host;
            private readonly ScrollBarControl _bar;
            private readonly Control _row;
            private readonly EventHandler _valueChanged;
            private readonly int _initialPhysicalOffset;
            private readonly Rectangle _initialRowBounds;
            private int _previousLogicalOffset;

            public int ActiveFrameCount;
            public string Failure;

            public NaturalFrameProbe(
                XamlRuntime.ItemsControl host,
                ScrollBarControl bar,
                Control row)
            {
                _host = host;
                _bar = bar;
                _row = row;
                _initialPhysicalOffset = GetPhysicalOffset(host);
                _initialRowBounds = row.Bounds;
                _previousLogicalOffset = host.GetLogicalScrollOffset();
                _valueChanged = new EventHandler(OnBarValueChanged);
                _bar.ValueChanged += _valueChanged;
            }

            private void OnBarValueChanged(object sender, EventArgs e)
            {
                if (!_host.ScrollBitmapCacheActiveForTest)
                    return;

                ActiveFrameCount++;
                int logical = _host.GetLogicalScrollOffset();

                if (Failure == null && logical <= _previousLogicalOffset)
                    Failure = "logical position did not advance monotonically";
                if (Failure == null && _bar.Value != logical)
                    Failure = "styled thumb and logical offset diverged";
                if (Failure == null &&
                    GetPhysicalOffset(_host) != _initialPhysicalOffset)
                {
                    Failure = "native physical origin moved during a cached frame";
                }
                if (Failure == null && _row.Bounds != _initialRowBounds)
                    Failure = "native row bounds moved during a cached frame";

                _previousLogicalOffset = logical;
            }

            public void Dispose()
            {
                _bar.ValueChanged -= _valueChanged;
            }
        }

        private sealed class HitTestObserver : NativeWindow, IDisposable
        {
            private readonly XamlRuntime.ItemsControl _host;

            public bool Observed;
            public bool CacheActiveAtTarget;

            public HitTestObserver(
                XamlRuntime.ItemsControl host,
                IntPtr handle)
            {
                _host = host;
                AssignHandle(handle);
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == WindowNonClientHitTest)
                {
                    Observed = true;
                    CacheActiveAtTarget =
                        _host.ScrollBitmapCacheActiveForTest;
                }

                base.WndProc(ref message);
            }

            public void Dispose()
            {
                ReleaseHandle();
            }
        }

        internal static void RunAll()
        {
            TestImmediateNativeAndStyledInputUsesLiveTree();
            TestNativeScrollbarUsesDeferredBitmapTransaction();
            TestNativeLinePageAndWheelBurstRetargetsOneViewport();
            TestNativeNonRelativeCommandCommitsBeforeFallthrough();
            TestNativeRangeChangesEndAndClampTheTransaction();
            TestHorizontalNativeCacheAndRtlExclusion();
            TestNaturalFramesDeferOnePhysicalMoveUntilSettle();
            TestRapidRetargetingKeepsCacheAndLiveTreeConsistent();
            TestFocusedContentAndBackgroundImagesUseLiveScrolling();
            TestTallClippedRowsRemainEligibleForCachedScrolling();
            TestNonClientHitTestCommitsBeforeTheLiveTarget();
            TestGeometryAndPublicationInvalidationEndTheTransaction();
        }

        private static void
            TestImmediateNativeAndStyledInputUsesLiveTree()
        {
            using (NativeFixture fixture =
                new NativeFixture(false, false, false))
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                long captures = host.ScrollBitmapCaptureCountForTest;
                long commits = host.ScrollBitmapCommitCountForTest;
                long visualPublications =
                    host.ScrollVisualFramePublicationCountForTest;

                SendNativeScrollCommand(
                    host,
                    WindowVerticalScroll,
                    ScrollBarLineIncrement);
                SendNativeScrollCommand(
                    host,
                    WindowVerticalScroll,
                    ScrollBarPageIncrement);

                if (SystemInformation.MouseWheelScrollLines != 0)
                {
                    host.ProcessMouseWheelDelta(-120);
                    host.ProcessMouseWheelDelta(120);
                    host.ProcessMouseWheelDelta(-120);
                }

                AssertTrue(
                    !host.ScrollBitmapCacheActiveForTest &&
                    !host.SmoothScrollAnimationActiveForTest,
                    "default native input stays on the live control tree");
                AssertTrue(
                    host.GetLogicalScrollOffset() > 0,
                    "immediate native input publishes its destination synchronously");
                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    GetNativeVerticalThumbPosition(host),
                    "immediate native content and thumb publish together");
                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    GetPhysicalOffset(host),
                    "immediate native input moves the live display origin");
                AssertEqual(
                    captures,
                    host.ScrollBitmapCaptureCountForTest,
                    "immediate native input does not capture themed rows");
                AssertEqual(
                    commits,
                    host.ScrollBitmapCommitCountForTest,
                    "immediate native input has no deferred bitmap commit");
                AssertTrue(
                    host.ScrollVisualFramePublicationCountForTest >
                        visualPublications,
                    "immediate native input publishes its live visual frame");

                using (Bitmap background = new Bitmap(2, 2))
                {
                    host.BackgroundImage = background;
                    long publications =
                        host.ScrollVisualFramePublicationCountForTest;
                    int livePhysical = GetPhysicalOffset(host);

                    SendNativeScrollCommand(
                        host,
                        WindowVerticalScroll,
                        ScrollBarLineIncrement);

                    AssertTrue(
                        !host.ScrollBitmapCacheActiveForTest &&
                        GetPhysicalOffset(host) > livePhysical,
                        "an ineligible native row tree uses the exact live fallback");
                    AssertTrue(
                        host.ScrollVisualFramePublicationCountForTest >
                            publications,
                        "native live fallback flushes its invalidated child HWND regions synchronously");
                    host.BackgroundImage = null;
                }
            }

            using (Fixture fixture = new Fixture(44, false))
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                ScrollBarControl bar = fixture.Bar;
                long captures = host.ScrollBitmapCaptureCountForTest;
                long commits = host.ScrollBitmapCommitCountForTest;

                bar.ExecuteScrollCommand(
                    ScrollEventType.SmallIncrement);
                bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);

                if (SystemInformation.MouseWheelScrollLines != 0)
                {
                    host.ProcessMouseWheelDelta(-120);
                    host.ProcessMouseWheelDelta(120);
                    host.ProcessMouseWheelDelta(-120);
                }

                AssertTrue(
                    !host.ScrollBitmapCacheActiveForTest &&
                    !host.SmoothScrollAnimationActiveForTest,
                    "default styled input stays on the live control tree");
                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    bar.Value,
                    "immediate styled content and thumb publish together");
                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    GetPhysicalOffset(host),
                    "immediate styled input moves the live origin");
                AssertEqual(
                    captures,
                    host.ScrollBitmapCaptureCountForTest,
                    "immediate styled input does not capture themed rows");
                AssertEqual(
                    commits,
                    host.ScrollBitmapCommitCountForTest,
                    "immediate styled input has no deferred bitmap commit");
            }
        }

        private static void TestNativeScrollbarUsesDeferredBitmapTransaction()
        {
            using (NativeFixture fixture = new NativeFixture())
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                Control row = fixture.FirstRow;
                int initialPhysical = GetPhysicalOffset(host);
                Rectangle initialBounds = row.Bounds;
                int rowLocationChanges = 0;
                long captures = host.ScrollBitmapCaptureCountForTest;
                long frames = host.ScrollBitmapFrameCountForTest;
                long commits = host.ScrollBitmapCommitCountForTest;

                row.LocationChanged += delegate { rowLocationChanges++; };

                SendMessage(
                    host.Handle,
                    WindowVerticalScroll,
                    new IntPtr(ScrollBarLineIncrement),
                    IntPtr.Zero);

                AssertTrue(
                    host.ScrollBitmapCacheActiveForTest,
                    "a native arrow command begins the same bitmap transaction");
                AssertEqual(
                    captures + 1L,
                    host.ScrollBitmapCaptureCountForTest,
                    "a native arrow command captures one bounded snapshot");

                long observedFrames = frames;
                int previousThumb = GetNativeVerticalThumbPosition(host);
                Stopwatch watch = Stopwatch.StartNew();

                while (watch.ElapsedMilliseconds < 2500 &&
                       observedFrames < frames + 3L)
                {
                    Application.DoEvents();

                    if (host.ScrollBitmapFrameCountForTest != observedFrames &&
                        host.ScrollBitmapCacheActiveForTest)
                    {
                        observedFrames = host.ScrollBitmapFrameCountForTest;
                        int logical = host.GetLogicalScrollOffset();
                        int thumb = GetNativeVerticalThumbPosition(host);

                        AssertTrue(
                            logical > 0,
                            "native cached frames advance the logical viewport");
                        AssertEqual(
                            logical,
                            thumb,
                            "native cached frames publish the thumb without moving content");
                        AssertTrue(
                            thumb >= previousThumb,
                            "native cached thumb motion is monotonic");
                        AssertEqual(
                            initialPhysical,
                            GetPhysicalOffset(host),
                            "native cached frames retain the physical origin");
                        AssertEqual(
                            initialBounds,
                            row.Bounds,
                            "native cached frames retain native row bounds");
                        previousThumb = thumb;
                    }

                    Thread.Sleep(1);
                }

                AssertTrue(
                    observedFrames >= frames + 3L,
                    "a native arrow publishes multiple deferred frames");

                PumpUntil(
                    delegate
                    {
                        return
                            !host.SmoothScrollAnimationActiveForTest &&
                            !host.ScrollBitmapCacheActiveForTest;
                    },
                    3500,
                    "native deferred transaction settles");

                int finalLogical = host.GetLogicalScrollOffset();

                AssertTrue(
                    finalLogical > 0,
                    "native arrow retains its final displacement");
                AssertEqual(
                    finalLogical,
                    GetPhysicalOffset(host),
                    "native settle aligns WinForms physical state");
                AssertEqual(
                    finalLogical,
                    GetNativeVerticalThumbPosition(host),
                    "native settle leaves the thumb aligned");
                AssertEqual(
                    1,
                    rowLocationChanges,
                    "native deferred scrolling moves the live row tree once");
                AssertEqual(
                    commits + 1L,
                    host.ScrollBitmapCommitCountForTest,
                    "native deferred scrolling performs one physical commit");
            }
        }

        private static void
            TestNativeLinePageAndWheelBurstRetargetsOneViewport()
        {
            using (NativeFixture fixture = new NativeFixture())
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                int initialPhysical = GetPhysicalOffset(host);
                Rectangle initialBounds = fixture.FirstRow.Bounds;
                long captures = host.ScrollBitmapCaptureCountForTest;
                long commits = host.ScrollBitmapCommitCountForTest;

                SendNativeScrollCommand(
                    host,
                    WindowVerticalScroll,
                    ScrollBarLineIncrement);
                PumpForMilliseconds(25);
                SendNativeScrollCommand(
                    host,
                    WindowVerticalScroll,
                    ScrollBarPageIncrement);
                PumpForMilliseconds(25);
                host.ProcessMouseWheelDelta(-120);
                PumpForMilliseconds(25);

                AssertTrue(
                    host.ScrollBitmapCacheActiveForTest,
                    "native line/page/wheel input retains a deferred viewport");
                AssertTrue(
                    host.ScrollBitmapCaptureCountForTest <= captures + 2L,
                    "a native input burst uses at most two bounded viewport slices");
                AssertTrue(
                    host.ScrollBitmapCommitCountForTest <= commits + 1L,
                    "crossing one bounded slice performs at most one intermediate commit");

                int currentPhysical = GetPhysicalOffset(host);
                Rectangle currentBounds = fixture.FirstRow.Bounds;
                long currentFrames = host.ScrollBitmapFrameCountForTest;

                PumpUntil(
                    delegate
                    {
                        return host.ScrollBitmapFrameCountForTest >
                            currentFrames;
                    },
                    2000,
                    "the final native burst slice publishes another frame");
                AssertEqual(
                    currentPhysical,
                    GetPhysicalOffset(host),
                    "one active bitmap slice keeps the managed origin frozen");
                AssertEqual(
                    currentBounds,
                    fixture.FirstRow.Bounds,
                    "one active bitmap slice keeps child HWNDs frozen");
                AssertTrue(
                    currentPhysical == initialPhysical ||
                    fixture.FirstRow.Bounds != initialBounds,
                    "any physical movement corresponds to the bounded slice transition");

                int target = host.SmoothScrollTargetOffsetForTest;

                AssertTrue(
                    target > 0,
                    "line/page/wheel commands coalesce into a forward target");

                PumpUntil(
                    delegate
                    {
                        return
                            !host.SmoothScrollAnimationActiveForTest &&
                            !host.ScrollBitmapCacheActiveForTest;
                    },
                    5000,
                    "native line/page/wheel burst settles");

                AssertEqual(
                    target,
                    host.GetLogicalScrollOffset(),
                    "native input burst settles at its final coalesced target");
                AssertEqual(
                    target,
                    GetPhysicalOffset(host),
                    "native input burst commits its target to WinForms once settled");
                AssertEqual(
                    target,
                    GetNativeThumbPosition(host, true),
                    "native input burst leaves content and thumb aligned");
            }
        }

        private static void
            TestNativeNonRelativeCommandCommitsBeforeFallthrough()
        {
            using (NativeFixture fixture = new NativeFixture())
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                long frames = host.ScrollBitmapFrameCountForTest;
                long commits = host.ScrollBitmapCommitCountForTest;

                SendNativeScrollCommand(
                    host,
                    WindowVerticalScroll,
                    ScrollBarPageIncrement);
                PumpUntil(
                    delegate
                    {
                        return host.ScrollBitmapCacheActiveForTest &&
                            host.ScrollBitmapFrameCountForTest > frames;
                    },
                    2000,
                    "native thumb fallthrough begins from a cached frame");

                SendNativeScrollCommand(
                    host,
                    WindowVerticalScroll,
                    ScrollBarThumbTrack);

                AssertTrue(
                    !host.ScrollBitmapCacheActiveForTest,
                    "native thumb tracking sees the committed live tree");
                AssertEqual(
                    commits + 1L,
                    host.ScrollBitmapCommitCountForTest,
                    "native thumb tracking commits the preceding cache exactly once");
                host.StopSmoothScrollAnimation();
            }
        }

        private static void
            TestNativeRangeChangesEndAndClampTheTransaction()
        {
            using (NativeFixture fixture = new NativeFixture())
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                long frames = host.ScrollBitmapFrameCountForTest;
                long commits = host.ScrollBitmapCommitCountForTest;

                SendNativeScrollCommand(
                    host,
                    WindowVerticalScroll,
                    ScrollBarPageIncrement);
                PumpUntil(
                    delegate
                    {
                        return host.ScrollBitmapCacheActiveForTest &&
                            host.ScrollBitmapFrameCountForTest > frames;
                    },
                    2000,
                    "native range-change fixture begins from a cached frame");

                host.SetItems(CreateRows(1));
                Application.DoEvents();

                AssertTrue(
                    !host.ScrollBitmapCacheActiveForTest,
                    "a native range change cannot retain an obsolete bitmap slice");
                AssertTrue(
                    host.ScrollBitmapCommitCountForTest >= commits + 1L,
                    "a native range change commits its last visible cached frame");
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "a collapsed native range clamps the logical offset");
                AssertEqual(
                    0,
                    GetPhysicalOffset(host),
                    "a collapsed native range clamps the managed origin");
                AssertEqual(
                    0,
                    GetNativeThumbPosition(host, true),
                    "a collapsed native range clamps the native thumb");
                host.StopSmoothScrollAnimation();
            }
        }

        private static void TestHorizontalNativeCacheAndRtlExclusion()
        {
            using (NativeFixture fixture =
                new NativeFixture(true, false))
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                int initialPhysical = GetPhysicalOffset(host);
                Rectangle initialBounds = fixture.FirstRow.Bounds;

                AssertTrue(
                    host.ActiveNativeScrollStyleVisibleForTest,
                    "horizontal LTR fixture exposes native horizontal chrome");
                SendNativeScrollCommand(
                    host,
                    WindowHorizontalScroll,
                    ScrollBarLineIncrement);

                AssertTrue(
                    host.ScrollBitmapCacheActiveForTest,
                    "horizontal LTR native scrolling can use the bitmap transaction");
                PumpForMilliseconds(30);
                AssertEqual(
                    initialPhysical,
                    GetPhysicalOffset(host),
                    "horizontal cached frames retain the managed X origin");
                AssertEqual(
                    initialBounds,
                    fixture.FirstRow.Bounds,
                    "horizontal cached frames retain native item bounds");
                host.StopSmoothScrollAnimation();
            }

            using (NativeFixture fixture =
                new NativeFixture(true, true))
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                long captures = host.ScrollBitmapCaptureCountForTest;

                host.ScrollBy(ScrollEventType.SmallIncrement);
                PumpForMilliseconds(30);

                AssertTrue(
                    !host.ScrollBitmapCacheActiveForTest,
                    "inverted horizontal RTL mapping stays on the live renderer");
                AssertEqual(
                    captures,
                    host.ScrollBitmapCaptureCountForTest,
                    "horizontal RTL scrolling does not allocate an ambiguous bitmap slice");
                host.StopSmoothScrollAnimation();
            }
        }

        private static void
            TestRapidRetargetingKeepsCacheAndLiveTreeConsistent()
        {
            using (Fixture fixture = new Fixture())
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                ScrollBarControl bar = fixture.Bar;
                long captures = host.ScrollBitmapCaptureCountForTest;
                long commits = host.ScrollBitmapCommitCountForTest;
                int commandCount = 8;
                int i;

                for (i = 0; i < commandCount; i++)
                {
                    bar.ExecuteScrollCommand(
                        ScrollEventType.LargeIncrement);
                    PumpForMilliseconds(35);

                    AssertTrue(
                        host.GetLogicalScrollOffset() == bar.Value,
                        "retargeted cached frames keep the thumb synchronized");
                    AssertTrue(
                        !host.ScrollBitmapCacheActiveForTest ||
                        GetPhysicalOffset(host) <=
                            host.GetLogicalScrollOffset(),
                        "retargeting never moves the live tree past its cached frame");
                }

                int finalTarget =
                    host.SmoothScrollTargetOffsetForTest;

                AssertTrue(
                    finalTarget > 0,
                    "rapid relative commands retain a forward destination");
                AssertTrue(
                    host.ScrollBitmapCaptureCountForTest - captures <
                        commandCount,
                    "rapid retargeting reuses bounded snapshots instead of recapturing every input");

                PumpUntil(
                    delegate
                    {
                        return
                            !host.SmoothScrollAnimationActiveForTest &&
                            !host.ScrollBitmapCacheActiveForTest;
                    },
                    5000,
                    "rapid retargeted transaction settles");

                AssertEqual(
                    finalTarget,
                    host.GetLogicalScrollOffset(),
                    "rapid retargeting settles at the last requested target");
                AssertEqual(
                    finalTarget,
                    GetPhysicalOffset(host),
                    "rapid retargeting commits one matching live-tree position");
                AssertTrue(
                    host.ScrollBitmapCommitCountForTest > commits,
                    "rapid retargeting commits every completed cache slice");
            }
        }

        private static void
            TestFocusedContentAndBackgroundImagesUseLiveScrolling()
        {
            using (Fixture fixture = new Fixture())
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                ScrollBarControl bar = fixture.Bar;
                TextBox editor = new TextBox();

                editor.Bounds = new Rectangle(4, 4, 120, 24);
                fixture.FirstRow.Controls.Add(editor);
                editor.Focus();
                Application.DoEvents();

                AssertTrue(
                    host.ContainsFocus,
                    "focus fallback fixture places a live editor inside the viewport");

                long captures = host.ScrollBitmapCaptureCountForTest;
                int physical = GetPhysicalOffset(host);
                bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);

                PumpUntil(
                    delegate
                    {
                        return GetPhysicalOffset(host) > physical;
                    },
                    2000,
                    "focused scrolling publishes live native frames");

                AssertTrue(
                    !host.ScrollBitmapCacheActiveForTest,
                    "focused content never receives a bitmap overlay");
                AssertEqual(
                    captures,
                    host.ScrollBitmapCaptureCountForTest,
                    "focused content does not allocate a scroll snapshot");
                host.StopSmoothScrollAnimation();
                editor.Dispose();

                fixture.KeepFocusOutsideViewport();
                host.SetLogicalScrollOffset(0);

                using (Bitmap background = new Bitmap(2, 2))
                {
                    host.BackgroundImage = background;
                    captures = host.ScrollBitmapCaptureCountForTest;
                    bar.ExecuteScrollCommand(
                        ScrollEventType.LargeIncrement);
                    PumpForMilliseconds(40);

                    AssertTrue(
                        !host.ScrollBitmapCacheActiveForTest,
                        "background-image content stays on the exact live renderer");
                    AssertEqual(
                        captures,
                        host.ScrollBitmapCaptureCountForTest,
                        "background-image content does not capture a flat-color substitute");
                    host.StopSmoothScrollAnimation();
                    host.BackgroundImage = null;
                }
            }
        }

        private static void
            TestTallClippedRowsRemainEligibleForCachedScrolling()
        {
            using (Fixture fixture = new Fixture(640))
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                long captures = host.ScrollBitmapCaptureCountForTest;
                long frames = host.ScrollBitmapFrameCountForTest;

                fixture.Bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);

                AssertTrue(
                    host.ScrollBitmapCacheActiveForTest,
                    "a row taller than the viewport captures through the clipped-item path");
                AssertEqual(
                    captures + 1L,
                    host.ScrollBitmapCaptureCountForTest,
                    "a tall clipped row creates one bounded snapshot");

                PumpUntil(
                    delegate
                    {
                        return host.ScrollBitmapFrameCountForTest > frames;
                    },
                    2000,
                    "a tall clipped row publishes a cached frame");
                host.StopSmoothScrollAnimation();

                AssertEqual(
                    host.GetLogicalScrollOffset(),
                    GetPhysicalOffset(host),
                    "a tall clipped row settles with the live tree aligned");
            }
        }

        private static void
            TestNaturalFramesDeferOnePhysicalMoveUntilSettle()
        {
            using (Fixture fixture = new Fixture())
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                ScrollBarControl bar = fixture.Bar;
                Control row = fixture.FirstRow;
                int initialLogical = host.GetLogicalScrollOffset();
                int initialPhysical = GetPhysicalOffset(host);
                Rectangle initialBounds = row.Bounds;
                int rowLocationChanges = 0;
                long captures = host.ScrollBitmapCaptureCountForTest;
                long frames = host.ScrollBitmapFrameCountForTest;
                long commits = host.ScrollBitmapCommitCountForTest;

                row.LocationChanged += delegate { rowLocationChanges++; };

                using (NaturalFrameProbe probe =
                    new NaturalFrameProbe(host, bar, row))
                {
                    bar.ExecuteScrollCommand(
                        ScrollEventType.LargeIncrement);

                    AssertTrue(
                        host.ScrollBitmapCacheActiveForTest,
                        "smooth styled command begins one bitmap transaction");
                    AssertEqual(
                        captures + 1L,
                        host.ScrollBitmapCaptureCountForTest,
                        "smooth styled command captures exactly one bitmap");

                    PumpUntil(
                        delegate
                        {
                            return
                                host.ScrollBitmapFrameCountForTest >=
                                    frames + 3L &&
                                host.ScrollBitmapCacheActiveForTest;
                        },
                        2500,
                        "natural timer publishes multiple cached frames");

                    AssertTrue(
                        host.GetLogicalScrollOffset() > initialLogical,
                        "cached natural frames advance the logical viewport");
                    AssertEqual(
                        host.GetLogicalScrollOffset(),
                        bar.Value,
                        "cached natural frames advance the styled thumb");
                    AssertEqual(
                        initialPhysical,
                        GetPhysicalOffset(host),
                        "cached natural frames retain the physical origin");
                    AssertEqual(
                        initialBounds,
                        row.Bounds,
                        "cached natural frames retain native row bounds");
                    AssertEqual(
                        0,
                        rowLocationChanges,
                        "no native row move occurs before transaction settle");
                    AssertEqual(
                        null,
                        probe.Failure,
                        "every natural cached frame preserves the transaction invariant");
                    AssertTrue(
                        probe.ActiveFrameCount >= 3,
                        "thumb observation sees multiple natural cached frames");

                    PumpUntil(
                        delegate
                        {
                            return
                                !host.SmoothScrollAnimationActiveForTest &&
                                !host.ScrollBitmapCacheActiveForTest;
                        },
                        3500,
                        "natural smooth transaction settles");
                }

                int finalLogical = host.GetLogicalScrollOffset();

                AssertTrue(
                    finalLogical > initialLogical,
                    "settled transaction retains its forward displacement");
                AssertEqual(
                    finalLogical,
                    GetPhysicalOffset(host),
                    "settle commits the final logical offset physically");
                AssertEqual(
                    initialBounds.Top -
                        (finalLogical - initialPhysical),
                    row.Top,
                    "settle moves the real row tree to the final bitmap frame");
                AssertEqual(
                    1,
                    rowLocationChanges,
                    "settle performs exactly one real physical row move");
                AssertEqual(
                    captures + 1L,
                    host.ScrollBitmapCaptureCountForTest,
                    "one natural transaction performs one capture");
                AssertTrue(
                    host.ScrollBitmapFrameCountForTest >= frames + 3L,
                    "one natural transaction publishes multiple bitmap frames");
                AssertEqual(
                    commits + 1L,
                    host.ScrollBitmapCommitCountForTest,
                    "one natural transaction performs one physical commit");
            }
        }

        private static void
            TestNonClientHitTestCommitsBeforeTheLiveTarget()
        {
            using (Fixture fixture = new Fixture())
            {
                XamlRuntime.ItemsControl host = fixture.Host;
                ScrollBarControl bar = fixture.Bar;
                long frames = host.ScrollBitmapFrameCountForTest;
                long commits = host.ScrollBitmapCommitCountForTest;

                bar.ExecuteScrollCommand(
                    ScrollEventType.LargeIncrement);
                PumpUntil(
                    delegate
                    {
                        return
                            host.ScrollBitmapCacheActiveForTest &&
                            host.ScrollBitmapFrameCountForTest > frames;
                    },
                    2000,
                    "hit-test fixture reaches a cached natural frame");

                Rectangle viewport = host.ItemsViewportRectangleForTest;
                Point clientPoint = new Point(
                    viewport.Left + Math.Max(1, viewport.Width / 2),
                    viewport.Top + Math.Max(1, viewport.Height / 2));
                Point screenPoint = host.PointToScreen(clientPoint);
                IntPtr liveTarget = ChildWindowFromPointEx(
                    host.Handle,
                    clientPoint,
                    ChildWindowSkipInvisible);
                Rectangle surfaceBounds = new Rectangle(
                    host.PointToScreen(viewport.Location),
                    viewport.Size);
                IntPtr cachedSurface = FindVisibleSiblingWindow(
                    fixture.Form.Handle,
                    surfaceBounds,
                    host.Handle,
                    bar.Handle);

                AssertTrue(
                    liveTarget != IntPtr.Zero &&
                    cachedSurface != IntPtr.Zero &&
                    cachedSurface != liveTarget &&
                    cachedSurface != host.Handle &&
                    cachedSurface != bar.Handle,
                    "fixed bitmap surface covers a distinct live item target");

                using (HitTestObserver observer =
                    new HitTestObserver(host, liveTarget))
                {
                    IntPtr result = SendMessage(
                        cachedSurface,
                        WindowNonClientHitTest,
                        IntPtr.Zero,
                        PackScreenPoint(screenPoint));

                    AssertEqual(
                        HitTransparent,
                        result.ToInt32(),
                        "bitmap surface releases the native hit target");
                    AssertTrue(
                        !host.ScrollBitmapCacheActiveForTest,
                        "WM_NCHITTEST commits before returning its target result");
                    AssertEqual(
                        commits + 1L,
                        host.ScrollBitmapCommitCountForTest,
                        "WM_NCHITTEST performs exactly one transaction commit");
                    AssertEqual(
                        host.GetLogicalScrollOffset(),
                        GetPhysicalOffset(host),
                        "WM_NCHITTEST exposes the live tree at the cached offset");
                    AssertTrue(
                        WindowFromPoint(screenPoint) != cachedSurface,
                        "committed surface no longer intercepts the screen point");

                    SendMessage(
                        liveTarget,
                        WindowNonClientHitTest,
                        IntPtr.Zero,
                        PackScreenPoint(screenPoint));

                    AssertTrue(
                        observer.Observed &&
                        !observer.CacheActiveAtTarget,
                        "the live hit target observes an already committed tree");
                }

                host.StopSmoothScrollAnimation();
            }
        }

        private static void
            TestGeometryAndPublicationInvalidationEndTheTransaction()
        {
            using (Fixture fixture = new Fixture())
            {
                XamlRuntime.ItemsControl host = fixture.Host;

                BeginBitmapTransaction(fixture, "resize invalidation");
                long commits = host.ScrollBitmapCommitCountForTest;
                host.Width += 3;
                AssertTrue(
                    !host.ScrollBitmapCacheActiveForTest,
                    "resize cannot leave a stale bitmap transaction active");
                AssertEqual(
                    commits + 1L,
                    host.ScrollBitmapCommitCountForTest,
                    "resize commits the cached logical position once");
                ResetTransaction(fixture);

                BeginBitmapTransaction(fixture, "layout invalidation");
                commits = host.ScrollBitmapCommitCountForTest;
                host.PerformLayout();
                AssertTrue(
                    !host.ScrollBitmapCacheActiveForTest,
                    "layout cannot leave a stale bitmap transaction active");
                AssertEqual(
                    commits + 1L,
                    host.ScrollBitmapCommitCountForTest,
                    "layout commits the cached logical position once");
                ResetTransaction(fixture);

                BeginBitmapTransaction(fixture, "publication invalidation");
                commits = host.ScrollBitmapCommitCountForTest;
                host.PublishRenderedItemRecords(host.RenderedItems);
                PumpUntil(
                    delegate
                    {
                        return !host.ScrollBitmapCacheActiveForTest;
                    },
                    2000,
                    "record publication invalidates the next cached frame");
                AssertEqual(
                    commits + 1L,
                    host.ScrollBitmapCommitCountForTest,
                    "record publication commits the stale snapshot once");
                host.StopSmoothScrollAnimation();
                ResetTransaction(fixture);

                BeginBitmapTransaction(fixture, "native-parent invalidation");
                commits = host.ScrollBitmapCommitCountForTest;
                fixture.Form.Controls.Remove(host);

                AssertTrue(
                    !host.ScrollBitmapCacheActiveForTest,
                    "moving the control cannot retain a sibling bitmap owned by its old parent");
                AssertEqual(
                    commits + 1L,
                    host.ScrollBitmapCommitCountForTest,
                    "native-parent removal commits the cached logical position once");

                fixture.Form.Controls.Add(host);
                Application.DoEvents();

                AssertTrue(
                    host.Parent == fixture.Form,
                    "native-parent invalidation fixture can safely attach again");
            }
        }

        private static void BeginBitmapTransaction(
            Fixture fixture,
            string operation)
        {
            fixture.KeepFocusOutsideViewport();
            long captures =
                fixture.Host.ScrollBitmapCaptureCountForTest;
            fixture.Bar.ExecuteScrollCommand(
                ScrollEventType.LargeIncrement);

            AssertTrue(
                fixture.Host.ScrollBitmapCacheActiveForTest,
                operation + " begins with an active bitmap transaction");
            AssertEqual(
                captures + 1L,
                fixture.Host.ScrollBitmapCaptureCountForTest,
                operation + " captures one new snapshot");
        }

        private static void ResetTransaction(Fixture fixture)
        {
            fixture.Host.StopSmoothScrollAnimation();
            fixture.Host.SetLogicalScrollOffset(0);
            PumpForMilliseconds(20);
            fixture.KeepFocusOutsideViewport();
            AssertTrue(
                !fixture.Host.ScrollBitmapCacheActiveForTest,
                "transaction reset leaves no cached surface active");
        }

        private static ArrayList CreateRows(int count)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
                rows.Add(new Row(i));

            return rows;
        }

        private static Control FindFirstRenderedRow(
            XamlRuntime.ItemsControl host)
        {
            Control first = null;
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                Control candidate = host.Controls[i];

                if (candidate == null)
                    continue;

                if (first == null || candidate.Top < first.Top)
                    first = candidate;
            }

            return first;
        }

        private static int GetPhysicalOffset(
            XamlRuntime.ItemsControl host)
        {
            Point origin = host.AutoScrollPosition;
            int value = host.Orientation == Orientation.Vertical
                ? origin.Y
                : origin.X;

            return Math.Max(0, -value);
        }

        private static int GetNativeVerticalThumbPosition(
            XamlRuntime.ItemsControl host)
        {
            return GetNativeThumbPosition(host, true);
        }

        private static int GetNativeThumbPosition(
            XamlRuntime.ItemsControl host,
            bool vertical)
        {
            NativeScrollInfo info = new NativeScrollInfo();

            info.Size = Marshal.SizeOf(typeof(NativeScrollInfo));
            info.Mask = ScrollInfoPosition;

            if (!GetScrollInfo(
                    host.Handle,
                    vertical
                        ? NativeVerticalScrollBar
                        : NativeHorizontalScrollBar,
                    ref info))
            {
                throw new InvalidOperationException(
                    "Could not read the native scrollbar position.");
            }

            return info.Position;
        }

        private static void SendNativeScrollCommand(
            XamlRuntime.ItemsControl host,
            int message,
            int command)
        {
            SendMessage(
                host.Handle,
                message,
                new IntPtr(command),
                IntPtr.Zero);
        }

        private static IntPtr PackScreenPoint(Point point)
        {
            int packed = (point.X & 0xffff) |
                ((point.Y & 0xffff) << 16);
            return new IntPtr(packed);
        }

        private static IntPtr FindVisibleSiblingWindow(
            IntPtr parent,
            Rectangle bounds,
            IntPtr excludedFirst,
            IntPtr excludedSecond)
        {
            IntPtr child = GetWindow(parent, GetFirstChildWindow);

            while (child != IntPtr.Zero)
            {
                NativeRectangle nativeBounds;

                if (child != excludedFirst &&
                    child != excludedSecond &&
                    IsWindowVisible(child) &&
                    GetWindowRect(child, out nativeBounds) &&
                    nativeBounds.ToRectangle() == bounds)
                {
                    return child;
                }

                child = GetWindow(child, GetNextSiblingWindow);
            }

            return IntPtr.Zero;
        }

        private static void PumpUntil(
            Predicate predicate,
            int timeoutMilliseconds,
            string message)
        {
            Stopwatch watch = Stopwatch.StartNew();

            while (!predicate() &&
                   watch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }

            Application.DoEvents();

            if (!predicate())
                throw new InvalidOperationException(message);
        }

        private static void PumpForMilliseconds(int milliseconds)
        {
            Stopwatch watch = Stopwatch.StartNew();

            while (watch.ElapsedMilliseconds < milliseconds)
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ". Expected " + expected +
                    ", actual " + actual + ".");
            }
        }

        private delegate bool Predicate();
    }
}
