using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        public partial class ItemsControl
        {
            private const int WindowStyleHorizontalScroll = 0x00100000;
            private const int WindowStyleVerticalScroll = 0x00200000;
            private const int WindowLongStyle = -16;
            private const int WindowMessageStyleChanging = 0x007C;
            private const uint WindowPositionNoActivate = 0x0010;
            private const uint WindowPositionNoMove = 0x0002;
            private const uint WindowPositionNoSize = 0x0001;

            private ScrollBarStyle _verticalScrollStyle;
            private ScrollBarStyle _horizontalScrollStyle;
            private ScrollBarControl _themedScrollBar;
            private Control _themedScrollBarNativeParent;
            private bool _synchronizingThemedScrollBar;
            private bool _positioningThemedScrollBar;
            private bool _movingThemedScrollDisplayOffset;
            private bool _disposingThemedScrollBar;
            private bool _themedThumbTracking;
            private bool _themedOrientationTransition;
            private bool _themedNativeChromeHidden;
            private bool _secondaryNativeChromeHidden;
            private int _themedScrollBarLayoutThickness;
            private bool _hasThemedNativeRangeSignature;
            private Orientation _themedNativeSignatureOrientation;
            private bool _themedNativeSignatureVisible;
            private int _themedNativeSignatureMaximum;
            private int _themedNativeSignatureLargeChange;
            private Size _themedNativeSignatureClientSize;
#if !WINFORMSXAML_PACKAGE
            private long _themedNativeHideAttemptCount;
            private long _secondaryNativeHideAttemptCount;
            private long _themedScrollBarSynchronizationCount;
            private long _themedNativeChromeProbeCount;
#endif
            private bool _themedScrollBarSynchronizationPending;
            private bool _suppressOwnedInfrastructureLayout;
            private bool _themedScrollBarRangeVisible;

            [StructLayout(LayoutKind.Sequential)]
            private struct WindowStyleChange
            {
                public int OldStyle;
                public int NewStyle;
            }

            /// <summary>
            /// Hosts the scrollbar HWND beside ItemsControl's HWND instead of
            /// inside its scroll-translated Controls collection. The native
            /// parent is ItemsControl.Parent, while the managed Parent remains
            /// null so surrounding layout engines never treat the overlay as
            /// application content.
            /// </summary>
            private sealed class HostedVerticalScrollBar
                : VerticalScrollBar
            {
                private readonly ItemsControl _owner;

                internal HostedVerticalScrollBar(ItemsControl owner)
                {
                    _owner = owner;
                }

                protected override CreateParams CreateParams
                {
                    get
                    {
                        CreateParams parameters = base.CreateParams;

                        if (_owner != null &&
                            _owner.Parent != null &&
                            _owner.Parent.IsHandleCreated)
                        {
                            parameters.Parent =
                                _owner.Parent.Handle;
                        }

                        return parameters;
                    }
                }
            }

            private sealed class HostedHorizontalScrollBar
                : HorizontalScrollBar
            {
                private readonly ItemsControl _owner;

                internal HostedHorizontalScrollBar(ItemsControl owner)
                {
                    _owner = owner;
                }

                protected override CreateParams CreateParams
                {
                    get
                    {
                        CreateParams parameters = base.CreateParams;

                        if (_owner != null &&
                            _owner.Parent != null &&
                            _owner.Parent.IsHandleCreated)
                        {
                            parameters.Parent =
                                _owner.Parent.Handle;
                        }

                        return parameters;
                    }
                }
            }

            [DllImport("user32.dll")]
            private static extern bool ShowScrollBar(
                IntPtr window,
                int bar,
                bool show);

            [DllImport("user32.dll")]
            private static extern int GetWindowLong(
                IntPtr window,
                int index);

            [DllImport("user32.dll")]
            private static extern bool SetWindowPos(
                IntPtr window,
                IntPtr insertAfter,
                int x,
                int y,
                int width,
                int height,
                uint flags);

            private void SuppressNativeScrollStyleChange(
                ref Message message)
            {
                if (message.Msg != WindowMessageStyleChanging ||
                    message.WParam.ToInt32() != WindowLongStyle ||
                    message.LParam == IntPtr.Zero)
                {
                    return;
                }

                WindowStyleChange change =
                    (WindowStyleChange)Marshal.PtrToStructure(
                        message.LParam,
                        typeof(WindowStyleChange));
                int forbidden = _orientation == Orientation.Vertical
                    ? WindowStyleHorizontalScroll
                    : WindowStyleVerticalScroll;

                if (HasActiveThemedScrollBar)
                {
                    forbidden |= _orientation == Orientation.Vertical
                        ? WindowStyleVerticalScroll
                        : WindowStyleHorizontalScroll;
                }

                int allowed = change.NewStyle & ~forbidden;

                if (allowed == change.NewStyle)
                    return;

                change.NewStyle = allowed;
                Marshal.StructureToPtr(
                    change,
                    message.LParam,
                    false);
            }

            /// <summary>
            /// Gets or sets the framework-owned vertical scrollbar appearance.
            /// A null value preserves the native WinForms scrollbar. The style
            /// becomes active only while Orientation is Vertical.
            /// </summary>
            [DefaultValue(null)]
            public ScrollBarStyle VerticalScrollStyle
            {
                get { return _verticalScrollStyle; }
                set { SetThemedScrollStyle(true, value); }
            }

            /// <summary>
            /// Gets or sets the framework-owned horizontal scrollbar appearance.
            /// A null value preserves the native WinForms scrollbar. The style
            /// becomes active only while Orientation is Horizontal.
            /// </summary>
            [DefaultValue(null)]
            public ScrollBarStyle HorizontalScrollStyle
            {
                get { return _horizontalScrollStyle; }
                set { SetThemedScrollStyle(false, value); }
            }

            /// <summary>
            /// Gets or sets the empty pixels between repeated content and the
            /// active native or framework-owned scrollbar.
            /// </summary>
            [DefaultValue(0)]
            public int ScrollBarGap
            {
                get { return _scrollBarGap; }
                set
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            "value",
                            "ScrollBarGap cannot be negative.");
                    }

                    if (_scrollBarGap == value)
                        return;

                    int logicalScroll =
                        CaptureLogicalScrollOffsetForTransition();
                    _scrollBarGap = value;
                    _themedScrollBarLayoutThickness =
                        GetActiveThemedScrollBarLayoutThickness();

                    if (!IsDisposed && !Disposing)
                    {
                        SynchronizeThemedScrollBar();
                        PerformLayout();
                        RestoreSavedLogicalScrollOffset(logicalScroll);
                        Invalidate(false);
                    }
                }
            }

            private ScrollBarStyle ActiveThemedScrollStyle
            {
                get
                {
                    return _orientation == Orientation.Vertical
                        ? _verticalScrollStyle
                        : _horizontalScrollStyle;
                }
            }

            private bool HasActiveThemedScrollBar
            {
                get { return ActiveThemedScrollStyle != null; }
            }

            private void SetThemedScrollStyle(
                bool vertical,
                ScrollBarStyle value)
            {
                ScrollBarStyle previous = vertical
                    ? _verticalScrollStyle
                    : _horizontalScrollStyle;

                if (Object.ReferenceEquals(previous, value))
                    return;

                bool active = vertical
                    ? _orientation == Orientation.Vertical
                    : _orientation == Orientation.Horizontal;
                int logicalScroll = active
                    ? CaptureLogicalScrollOffsetForTransition()
                    : 0;

                if (active && value != null)
                    ValidateThemedScrollBarAxisInvariant();

                if (previous != null)
                {
                    previous.Changed -= vertical
                        ? new EventHandler(VerticalScrollStyleChanged)
                        : new EventHandler(HorizontalScrollStyleChanged);
                }

                if (vertical)
                    _verticalScrollStyle = value;
                else
                    _horizontalScrollStyle = value;

                if (value != null)
                {
                    value.Changed += vertical
                        ? new EventHandler(VerticalScrollStyleChanged)
                        : new EventHandler(HorizontalScrollStyleChanged);
                }

                if (!active)
                    return;

                if (value != null && AutoScroll)
                    EnsureScrollOriginObserverMarker();

                _themedThumbTracking = false;
                RebuildThemedScrollBar();
                ApplyThemedScrollBarConfigurationChange();
                RestoreSavedLogicalScrollOffset(logicalScroll);
            }

            private void VerticalScrollStyleChanged(
                object sender,
                EventArgs e)
            {
                if (_orientation == Orientation.Vertical &&
                    Object.ReferenceEquals(
                        sender,
                        _verticalScrollStyle))
                {
                    ApplyThemedScrollBarStyleChange();
                }
            }

            private void HorizontalScrollStyleChanged(
                object sender,
                EventArgs e)
            {
                if (_orientation == Orientation.Horizontal &&
                    Object.ReferenceEquals(
                        sender,
                        _horizontalScrollStyle))
                {
                    ApplyThemedScrollBarStyleChange();
                }
            }

            private void ApplyThemedScrollBarStyleChange()
            {
                if (_disposingThemedScrollBar ||
                    IsDisposed ||
                    Disposing)
                {
                    return;
                }

                bool missingInfrastructure =
                    _themedScrollBar == null ||
                    _themedScrollBar.IsDisposed;
                int layoutThickness =
                    GetActiveThemedScrollBarLayoutThickness();
                bool geometryChanged =
                    layoutThickness !=
                    _themedScrollBarLayoutThickness;

                _themedScrollBarLayoutThickness = layoutThickness;
                EnsureThemedScrollBar();

                if (missingInfrastructure || geometryChanged)
                {
                    PositionThemedScrollBar();
                    SynchronizeThemedScrollBar();
                    PerformLayout();
                }
                else if (_themedScrollBar != null)
                {
                    // ScrollBarControl also observes the shared style and
                    // repaints itself. Keep this explicit invalidation so the
                    // host remains correct regardless of subscriber order,
                    // without remeasuring every rendered row for a color or
                    // thumb-paint metric change.
                    _themedScrollBar.Invalidate();
                }
            }

            private int GetActiveThemedScrollBarLayoutThickness()
            {
                ScrollBarStyle style = ActiveThemedScrollStyle;

                if (style == null)
                    return 0;

                int available = _orientation == Orientation.Vertical
                    ? Math.Max(0, ClientSize.Width)
                    : Math.Max(0, ClientSize.Height);
                long requested =
                    (long)Math.Max(1, style.Thickness) +
                    (long)Math.Max(0, _scrollBarGap);

                return Math.Min(
                    available,
                    requested >= Int32.MaxValue
                        ? Int32.MaxValue
                        : (int)requested);
            }

            private void ApplyThemedScrollBarConfigurationChange()
            {
                if (_disposingThemedScrollBar ||
                    IsDisposed ||
                    Disposing)
                {
                    return;
                }

                if (IsHandleCreated)
                {
                    InvalidateThemedNativeChromeState();
                    UpdateStyles();
                }

                PositionThemedScrollBar();
                SynchronizeThemedScrollBar();
                ReconcileThemedNativeChrome();
                PerformLayout();
            }

            private void RebuildThemedScrollBar()
            {
                DisposeActiveThemedScrollBar();
                EnsureThemedScrollBar();
            }

            private void EnsureThemedScrollBar()
            {
                ScrollBarStyle style = ActiveThemedScrollStyle;

                if (style == null ||
                    _disposingThemedScrollBar ||
                    IsDisposed ||
                    Disposing)
                {
                    return;
                }

                bool requireVertical =
                    _orientation == Orientation.Vertical;

                if (_themedScrollBar != null &&
                    !_themedScrollBar.IsDisposed &&
                    _themedScrollBar.IsVertical == requireVertical)
                {
                    if (!Object.ReferenceEquals(
                            _themedScrollBar.Style,
                            style))
                    {
                        _themedScrollBar.Style = style;
                    }

                    return;
                }

                DisposeActiveThemedScrollBar();

                ScrollBarControl bar = requireVertical
                    ? (ScrollBarControl)new HostedVerticalScrollBar(this)
                    : (ScrollBarControl)new HostedHorizontalScrollBar(this);

                bar.Visible = false;
                bar.Style = style;
                bar.Scroll +=
                    new ScrollEventHandler(ThemedScrollBarScrolled);
                _themedScrollBar = bar;
                _themedScrollBarNativeParent = Parent;
                PositionThemedScrollBar();
            }

            private void DisposeActiveThemedScrollBar()
            {
                ScrollBarControl bar = _themedScrollBar;
                _themedScrollBar = null;
                _themedScrollBarNativeParent = null;
                _themedThumbTracking = false;
                _themedScrollBarRangeVisible = false;
                _themedScrollBarLayoutThickness = 0;

                if (bar == null)
                    return;

                bar.Scroll -=
                    new ScrollEventHandler(ThemedScrollBarScrolled);

                if (!bar.IsDisposed)
                    bar.Dispose();
            }

            private void DisposeThemedScrollBarIntegration()
            {
                if (_disposingThemedScrollBar)
                    return;

                _disposingThemedScrollBar = true;

                try
                {
                    if (_verticalScrollStyle != null)
                    {
                        _verticalScrollStyle.Changed -=
                            new EventHandler(VerticalScrollStyleChanged);
                    }

                    if (_horizontalScrollStyle != null)
                    {
                        _horizontalScrollStyle.Changed -=
                            new EventHandler(HorizontalScrollStyleChanged);
                    }

                    _verticalScrollStyle = null;
                    _horizontalScrollStyle = null;
                    DisposeActiveThemedScrollBar();
                }
                finally
                {
                    _disposingThemedScrollBar = false;
                }
            }

            private void ThemedScrollBarScrolled(
                object sender,
                ScrollEventArgs e)
            {
                if (_synchronizingThemedScrollBar ||
                    _disposingThemedScrollBar ||
                    e == null ||
                    !Object.ReferenceEquals(sender, _themedScrollBar) ||
                    IsDisposed ||
                    Disposing)
                {
                    return;
                }

                if (e.Type == ScrollEventType.EndScroll)
                {
                    _themedThumbTracking = false;
                    SynchronizeThemedScrollBar();
                    return;
                }

                // Stationary thumb movement and autorepeat against a range
                // boundary can report the same value many times. With no
                // position change there is no content or thumb frame to
                // publish, so discard that input noise here.
                if (e.NewValue == e.OldValue &&
                    e.Type != ScrollEventType.ThumbPosition)
                {
                    e.NewValue = GetLogicalScrollOffset();
                    return;
                }

                if (e.Type == ScrollEventType.ThumbTrack &&
                    !_liveScroll)
                {
                    // The owner-painted thumb publishes its new Value after
                    // this event returns. Keep the content fixed until the
                    // subsequent ThumbPosition event.
                    _themedThumbTracking = true;
                    return;
                }

                _themedThumbTracking = false;

                if (IsSmoothScrollRelativeCommand(e.Type))
                {
                    long delta =
                        (long)e.NewValue - (long)e.OldValue;
                    long requested =
                        (long)GetLogicalScrollCommandBaseOffset() +
                        delta;
                    int target = ClampLongToNonnegativeInt(
                        requested);

                    if (_smoothScroll)
                    {
                        // Arrow autorepeat and wheel bursts only change the
                        // destination here. The animation timer owns every
                        // visible frame, avoiding a second synchronous row move
                        // inside the input timer callback.
                        BeginSmoothScrollAnimation(
                            target,
                            false,
                            false);
                    }
                    else
                        SetLogicalScrollOffset(target);
                }
                else if (e.Type == ScrollEventType.First)
                {
                    SetLogicalScrollOffset(0);
                }
                else if (e.Type == ScrollEventType.Last)
                {
                    SetLogicalScrollOffset(
                        GetMaximumLogicalScrollOffset());
                }
                else if (e.Type == ScrollEventType.ThumbTrack ||
                         e.Type == ScrollEventType.ThumbPosition)
                {
                    SetLogicalScrollOffset(Math.Max(0, e.NewValue));
                }

                // ScrollBarControl raises Scroll before changing its own Value.
                // Pin that pending update to the actual frame; smooth commands
                // therefore animate the thumb and content together.
                e.NewValue = GetLogicalScrollOffset();

                // Every command that moves content publishes the matching bar
                // state through SetLogicalScrollOffset. A smooth command keeps
                // the current value until its timer publishes the first frame,
                // so the existing bar value is already the required pin. Do
                // not repeat the complete range/value synchronization here.
            }

            private bool ShouldSuppressThemedInfrastructureLayout(
                LayoutEventArgs e)
            {
                if (_movingThemedScrollDisplayOffset &&
                    (DirectVirtualActive || LightweightActive))
                {
                    // SetDisplayRectLocation translates every realized child.
                    // The synchronous virtual refresh immediately publishes
                    // their final logical slots, so per-child native layout
                    // callbacks during the translation are redundant.
                    return true;
                }

                return false;
            }

            private void SynchronizeThemedScrollBar()
            {
                // Native scrolling has no framework bar state to publish. This
                // method is called from the shared logical scroll primitive, so
                // keep the native path allocation-free and avoid entering the
                // synchronization transaction on every smooth frame.
                if (!HasActiveThemedScrollBar &&
                    _themedScrollBar == null)
                {
                    _themedScrollBarSynchronizationPending = false;
                    return;
                }

                if ((DirectVirtualSuppressScrollRefresh ||
                     DirectVirtualRefreshRunning) &&
                    !_disposingThemedScrollBar)
                {
                    _themedScrollBarSynchronizationPending = true;
                    return;
                }

                if (_synchronizingThemedScrollBar ||
                    _movingThemedScrollDisplayOffset ||
                    _disposingThemedScrollBar ||
                    _themedOrientationTransition ||
                    IsDisposed ||
                    Disposing)
                {
                    return;
                }

                _synchronizingThemedScrollBar = true;
                _themedScrollBarSynchronizationPending = false;
#if !WINFORMSXAML_PACKAGE
                _themedScrollBarSynchronizationCount++;
#endif

                try
                {
                    if (!HasActiveThemedScrollBar)
                    {
                        DisposeActiveThemedScrollBar();
                        return;
                    }

                    EnsureThemedScrollBar();

                    if (_themedScrollBar == null ||
                        _themedScrollBar.IsDisposed)
                    {
                        return;
                    }

                    ValidateThemedScrollBarAxisInvariant();

                    int actualEffectiveMaximum =
                        GetMaximumLogicalScrollOffset();
                    int actualLargeChange =
                        GetLargeScrollChange();
                    int currentValue = _themedThumbTracking
                        ? _themedScrollBar.Value
                        : GetLogicalScrollOffset();
                    int effectiveMaximum =
                        actualEffectiveMaximum;
                    int largeChange = actualLargeChange;
                    int maximum;

                    if (effectiveMaximum >=
                        Int32.MaxValue - largeChange + 1)
                    {
                        maximum = Int32.MaxValue;
                        largeChange = Math.Max(
                            1,
                            Int32.MaxValue - effectiveMaximum + 1);
                    }
                    else
                    {
                        maximum = effectiveMaximum + largeChange - 1;
                    }

                    bool visible = AutoScroll &&
                        effectiveMaximum > 0;

                    _themedScrollBarRangeVisible = visible;

                    _themedScrollBar.SynchronizeState(
                        0,
                        maximum,
                        largeChange,
                        GetSmallScrollChange(),
                        currentValue,
                        !_themedThumbTracking);

                    bool showOverlay = visible &&
                        (Parent == null ||
                         (Visible && IsHandleCreated));

                    bool visibilityChanged =
                        _themedScrollBar.Visible != showOverlay;

                    if (visibilityChanged)
                        _themedScrollBar.Visible = showOverlay;

                    // Positioning establishes the fixed sibling's z-order.
                    // Reassert it only when a hidden overlay becomes visible;
                    // SetWindowPos on every smooth frame adds a native window
                    // transaction even though its bounds and z-order are fixed.
                    if (visibilityChanged && showOverlay)
                        BringThemedScrollBarOverlayToFront();
                }
                finally
                {
                    _synchronizingThemedScrollBar = false;
                }
            }

            internal void FlushPendingThemedScrollBarSynchronization()
            {
                if (!_themedScrollBarSynchronizationPending ||
                    DirectVirtualSuppressScrollRefresh ||
                    DirectVirtualRefreshRunning ||
                    _applyingLogicalScrollCommand ||
                    _applyingSmoothScrollFrame)
                {
                    return;
                }

                SynchronizeThemedScrollBar();
            }

            private void PositionThemedScrollBar()
            {
                ScrollBarControl bar = _themedScrollBar;

                if (bar == null ||
                    bar.IsDisposed ||
                    _positioningThemedScrollBar)
                {
                    return;
                }

                // The fixed chrome is an invariant of one active gesture. A
                // transient native client-size notification must never move
                // its arrows or track while the thumb is in motion; the final
                // settled geometry is published once by StopSmoothScrollAnimation.
                if (_smoothScrollActive &&
                    bar.Width > 0 &&
                    bar.Height > 0)
                {
                    return;
                }

                _positioningThemedScrollBar = true;

                try
                {
                    int clientWidth = Math.Max(0, ClientSize.Width);
                    int clientHeight = Math.Max(0, ClientSize.Height);
                    int requestedThickness = Math.Max(
                        1,
                        ActiveThemedScrollStyle == null
                            ? 1
                            : ActiveThemedScrollStyle.Thickness);
                    Rectangle bounds;
                    Point overlayOrigin = Point.Empty;

                    if (Parent != null &&
                        Parent.IsHandleCreated &&
                        IsHandleCreated)
                    {
                        overlayOrigin = Parent.PointToClient(
                            PointToScreen(Point.Empty));
                    }

                    if (_orientation == Orientation.Vertical)
                    {
                        int width = Math.Min(
                            clientWidth,
                            requestedThickness);
                        bool placeRight =
                            _keepScrollBarOnRight ||
                            !ContentRightToLeft;
                        int x = placeRight
                            ? clientWidth - width
                            : 0;
                        bounds = new Rectangle(
                            overlayOrigin.X + x,
                            overlayOrigin.Y,
                            width,
                            clientHeight);
                        bar.RightToLeft = ContentRightToLeft
                            ? RightToLeft.Yes
                            : RightToLeft.No;
                    }
                    else
                    {
                        int height = Math.Min(
                            clientHeight,
                            requestedThickness);
                        bounds = new Rectangle(
                            overlayOrigin.X,
                            overlayOrigin.Y + clientHeight - height,
                            clientWidth,
                            height);
                        bar.RightToLeft = ContentRightToLeft
                            ? RightToLeft.Yes
                            : RightToLeft.No;
                    }

                    if (bar.Bounds != bounds)
                        bar.Bounds = bounds;

                    EnsureThemedScrollBarOverlayHandle();
                    BringThemedScrollBarOverlayToFront();

                    int available = _orientation == Orientation.Vertical
                        ? clientWidth
                        : clientHeight;
                    long reserved =
                        (long)(_orientation == Orientation.Vertical
                            ? bounds.Width
                            : bounds.Height) +
                        (long)Math.Max(0, _scrollBarGap);
                    _themedScrollBarLayoutThickness = Math.Min(
                        available,
                        reserved >= Int32.MaxValue
                            ? Int32.MaxValue
                            : (int)reserved);
                }
                finally
                {
                    _positioningThemedScrollBar = false;
                }
            }

            private void EnsureThemedScrollBarOverlayHandle()
            {
                ScrollBarControl bar = _themedScrollBar;

                if (bar == null ||
                    bar.IsDisposed ||
                    bar.IsHandleCreated ||
                    Parent == null ||
                    !Parent.IsHandleCreated ||
                    !IsHandleCreated ||
                    IsDisposed ||
                    Disposing)
                {
                    return;
                }

                // Reading Handle deliberately creates the detached managed
                // control's HWND. Hosted*ScrollBar.CreateParams assigns the
                // surrounding container as its native parent, so this window
                // is a fixed viewport overlay and never participates in
                // ItemsControl's ScrollWindowEx content translation.
                if (bar.Handle == IntPtr.Zero)
                    return;
            }

            private void BringThemedScrollBarOverlayToFront()
            {
                ScrollBarControl bar = _themedScrollBar;

                if (bar == null ||
                    bar.IsDisposed ||
                    !bar.IsHandleCreated ||
                    !bar.Visible)
                {
                    return;
                }

                SetWindowPos(
                    bar.Handle,
                    IntPtr.Zero,
                    0,
                    0,
                    0,
                    0,
                    WindowPositionNoMove |
                    WindowPositionNoSize |
                    WindowPositionNoActivate);
            }

            /// <summary>
            /// Moves the native display rectangle to a nonnegative physical
            /// offset without asking ScrollableControl to re-show its native
            /// scrollbar chrome. The caller owns logical RTL mapping and range
            /// clamping; this helper accepts physical coordinates only.
            /// </summary>
            internal bool TrySetThemedScrollDisplayOffset(
                int physicalOffset)
            {
                if (!HasActiveThemedScrollBar ||
                    !AutoScroll)
                {
                    return false;
                }

                int normalized = Math.Max(0, physicalOffset);

                // Arrow, page, thumb, and programmatic commands do not pass
                // through OnMouseWheel. Suppress the native visibility state at
                // this common display-origin boundary so ScrollableControl
                // cannot recreate system chrome for the first such command.
                SetScrollState(
                    _orientation == Orientation.Vertical
                        ? ScrollStateVScrollVisible
                        : ScrollStateHScrollVisible,
                    false);

                _movingThemedScrollDisplayOffset = true;

                try
                {
                    if (_orientation == Orientation.Vertical)
                        SetDisplayRectLocation(0, -normalized);
                    else
                        SetDisplayRectLocation(-normalized, 0);

                    // SetDisplayRectLocation moves the display origin without
                    // updating native scrollbar chrome. Direct-virtual child
                    // translations suppress their redundant intermediate
                    // layout callbacks in ItemsControl.OnLayout.
                }
                finally
                {
                    _movingThemedScrollDisplayOffset = false;
                }

                return true;
            }

            internal Rectangle GetItemsViewportRectangle()
            {
                return GetItemsViewportRectangleCore(true);
            }

            private Rectangle GetItemsViewportRectangleCore(
                bool reserveScrollBarStrip)
            {
                Rectangle result = ClientRectangle;
                int left = Math.Max(0, Padding.Left);
                int top = Math.Max(0, Padding.Top);
                int right = Math.Max(0, Padding.Right);
                int bottom = Math.Max(0, Padding.Bottom);

                result.X += left;
                result.Y += top;
                result.Width = Math.Max(
                    0,
                    result.Width - left - right);
                result.Height = Math.Max(
                    0,
                    result.Height - top - bottom);

                if (!reserveScrollBarStrip)
                {
                    return result;
                }

                bool frameworkBarVisible =
                    HasActiveThemedScrollBar &&
                    _themedScrollBar != null &&
                    !_themedScrollBar.IsDisposed &&
                    _themedScrollBarRangeVisible &&
                    _themedScrollBar.IsVertical ==
                        (_orientation == Orientation.Vertical);
                bool nativeBarVisible =
                    !HasActiveThemedScrollBar &&
                    (_orientation == Orientation.Vertical
                        ? VScroll
                        : HScroll);

                if (!frameworkBarVisible && !nativeBarVisible)
                    return result;

                if (_orientation == Orientation.Vertical)
                {
                    int barThickness = frameworkBarVisible
                        ? Math.Max(0, _themedScrollBar.Width)
                        : 0;
                    int gap = Math.Max(0, _scrollBarGap);
                    long requested =
                        (long)barThickness + (long)gap;
                    int thickness = Math.Min(
                        result.Width,
                        requested >= Int32.MaxValue
                            ? Int32.MaxValue
                            : (int)requested);

                    if (!_keepScrollBarOnRight &&
                        ContentRightToLeft)
                    {
                        result.X += thickness;
                    }

                    result.Width -= thickness;
                }
                else
                {
                    int barThickness = frameworkBarVisible
                        ? Math.Max(0, _themedScrollBar.Height)
                        : 0;
                    int gap = Math.Max(0, _scrollBarGap);
                    long requested =
                        (long)barThickness + (long)gap;
                    int thickness = Math.Min(
                        result.Height,
                        requested >= Int32.MaxValue
                            ? Int32.MaxValue
                            : (int)requested);
                    result.Height -= thickness;
                }

                return result;
            }

            private void HideActiveNativeScrollBar()
            {
                if (!HasActiveThemedScrollBar ||
                    !IsHandleCreated)
                {
                    return;
                }

                // The framework bar owns this axis. Keep ScrollableControl's
                // internal visibility bit false as well as hiding the HWND
                // style; otherwise a later virtual child translation can use
                // the stale true bit to recreate native chrome mid-gesture.
                SetScrollState(
                    _orientation == Orientation.Vertical
                        ? ScrollStateVScrollVisible
                        : ScrollStateHScrollVisible,
                    false);

                if (_themedNativeChromeHidden)
                    return;

                try
                {
#if !WINFORMSXAML_PACKAGE
                    _themedNativeHideAttemptCount++;
#endif
                    ShowScrollBar(
                        Handle,
                        _orientation == Orientation.Vertical
                            ? SB_VERT
                            : SB_HORZ,
                        false);

                    int style = GetWindowLong(
                        Handle,
                        WindowLongStyle);
                    int activeFlag =
                        _orientation == Orientation.Vertical
                            ? WindowStyleVerticalScroll
                            : WindowStyleHorizontalScroll;

                    // ShowScrollBar can fail without throwing. Cache the
                    // hidden state only after the active native style bit is
                    // actually absent so a later reconciliation can retry.
                    _themedNativeChromeHidden =
                        (style & activeFlag) == 0;
                }
                catch
                {
                    // CreateParams still masks handle creation. Older or
                    // alternative WinForms implementations can omit this API;
                    // layout synchronization will retry on the next pass.
                }
            }

            private void EnsureThemedNativeChromeHiddenAfterScroll()
            {
                HideSecondaryNativeScrollBar();

                if (!HasActiveThemedScrollBar ||
                    !IsHandleCreated)
                {
                    return;
                }

                // WM_STYLECHANGING rejects the native active-axis style, and
                // every layout/range/handle transition invalidates this flag
                // before its reconciliation pass. Once hidden inside a stable
                // transaction, probing user32 on every animation frame cannot
                // discover any new state.
                if (_themedNativeChromeHidden)
                    return;

                try
                {
#if !WINFORMSXAML_PACKAGE
                    _themedNativeChromeProbeCount++;
#endif
                    int style = GetWindowLong(
                        Handle,
                        WindowLongStyle);
                    int activeFlag =
                        _orientation == Orientation.Vertical
                            ? WindowStyleVerticalScroll
                            : WindowStyleHorizontalScroll;

                    if ((style & activeFlag) == 0)
                    {
                        _themedNativeChromeHidden = true;
                        return;
                    }
                }
                catch
                {
                    // Fall through to the established compatibility hide path.
                }

                _themedNativeChromeHidden = false;
                HideActiveNativeScrollBar();
            }

            private void ReconcileThemedNativeChrome()
            {
                HideSecondaryNativeScrollBar();

                if (!HasActiveThemedScrollBar ||
                    !IsHandleCreated)
                {
                    InvalidateThemedNativeChromeState();
                    return;
                }

                ScrollProperties managed =
                    _orientation == Orientation.Vertical
                        ? (ScrollProperties)VerticalScroll
                        : (ScrollProperties)HorizontalScroll;
                bool visible = _orientation == Orientation.Vertical
                    ? VScroll
                    : HScroll;

                if (!_hasThemedNativeRangeSignature ||
                    _themedNativeSignatureOrientation != _orientation ||
                    _themedNativeSignatureVisible != visible ||
                    _themedNativeSignatureMaximum != managed.Maximum ||
                    _themedNativeSignatureLargeChange !=
                        managed.LargeChange ||
                    _themedNativeSignatureClientSize != ClientSize)
                {
                    _themedNativeChromeHidden = false;
                }

                HideActiveNativeScrollBar();

                _hasThemedNativeRangeSignature = true;
                _themedNativeSignatureOrientation = _orientation;
                _themedNativeSignatureVisible = visible;
                _themedNativeSignatureMaximum = managed.Maximum;
                _themedNativeSignatureLargeChange =
                    managed.LargeChange;
                _themedNativeSignatureClientSize = ClientSize;
            }

            private void InvalidateThemedNativeChromeState()
            {
                _themedNativeChromeHidden = false;
                _hasThemedNativeRangeSignature = false;
            }

            /// <summary>
            /// ItemsControl owns exactly one scrolling axis. ScrollableControl
            /// can temporarily infer overflow on the cross axis from child
            /// bounds during a convergence pass, especially with complex RTL
            /// rows. Suppress that transient chrome instead of allowing it to
            /// resize and laterally translate the viewport for one frame.
            /// </summary>
            private void HideSecondaryNativeScrollBar()
            {
                if (!AutoScroll || !IsHandleCreated)
                    return;

                bool managedVisible;

                if (_orientation == Orientation.Vertical)
                {
                    managedVisible = HScroll;

                    if (managedVisible)
                        HScroll = false;
                }
                else
                {
                    managedVisible = VScroll;

                    if (managedVisible)
                        VScroll = false;
                }

                // ScrollableControl can only infer the cross-axis chrome in a
                // layout/range pass. Once that pass has proved the native bit
                // absent, ordinary wheel, arrow, and animation frames cannot
                // make it reappear and must not call user32 again.
                if (_secondaryNativeChromeHidden && !managedVisible)
                    return;

                try
                {
                    int style = GetWindowLong(
                        Handle,
                        WindowLongStyle);
                    int secondaryFlag =
                        _orientation == Orientation.Vertical
                            ? WindowStyleHorizontalScroll
                            : WindowStyleVerticalScroll;

                    if (!managedVisible &&
                        (style & secondaryFlag) == 0)
                    {
                        _secondaryNativeChromeHidden = true;
                        return;
                    }

#if !WINFORMSXAML_PACKAGE
                    _secondaryNativeHideAttemptCount++;
#endif
                    ShowScrollBar(
                        Handle,
                        _orientation == Orientation.Vertical
                            ? SB_HORZ
                            : SB_VERT,
                        false);

                    style = GetWindowLong(
                        Handle,
                        WindowLongStyle);
                    _secondaryNativeChromeHidden =
                        (style & secondaryFlag) == 0;
                }
                catch
                {
                    _secondaryNativeChromeHidden = false;
                    // CreateParams also masks this axis. Some legacy or
                    // alternative WinForms implementations can omit the API.
                }
            }

            private void ValidateThemedScrollBarAxisInvariant()
            {
                if (!AutoScroll)
                    return;

                // ScrollableControl can expose the opposite managed flag for
                // one convergence pass while the active bar changes client
                // width. CreateParams deliberately masks that native axis, so
                // its HScroll/VScroll flag is not authoritative here. Reject
                // only a real two-dimensional logical extent;
                // ItemsControl's normal one-axis extent is (1, H) or (W, 1)
                // and settles without a secondary bar on the next pass.
                int secondaryExtent =
                    _orientation == Orientation.Vertical
                        ? AutoScrollMinSize.Width
                        : AutoScrollMinSize.Height;
                int secondaryViewport =
                    _orientation == Orientation.Vertical
                        ? ClientSize.Width
                        : ClientSize.Height;

                if (secondaryExtent <= 1 ||
                    secondaryExtent <= Math.Max(0, secondaryViewport))
                    return;

                throw new InvalidOperationException(
                    "ItemsControl framework scrollbars support the configured " +
                    "Orientation axis only. The native secondary scrollbar became " +
                    "visible; remove the secondary-axis extent or use a dedicated " +
                    "two-dimensional scrolling control.");
            }

            private void BeginThemedScrollBarOrientationTransition()
            {
                if (_themedScrollBar == null &&
                    _verticalScrollStyle == null &&
                    _horizontalScrollStyle == null)
                {
                    return;
                }

                _themedOrientationTransition = true;
                _themedThumbTracking = false;
                InvalidateThemedNativeChromeState();

                if (_themedScrollBar != null &&
                    !_themedScrollBar.IsDisposed)
                {
                    _themedScrollBar.Visible = false;
                }
            }

            private void CompleteThemedScrollBarOrientationTransition()
            {
                _themedOrientationTransition = false;

                if (_disposingThemedScrollBar)
                    return;

                if (_themedScrollBar == null &&
                    ActiveThemedScrollStyle == null)
                {
                    return;
                }

                _themedThumbTracking = false;
                RebuildThemedScrollBar();
                ApplyThemedScrollBarConfigurationChange();
            }

            private void OnItemsControlFlowDirectionChanged()
            {
                if (_themedScrollBar == null ||
                    _themedScrollBar.IsDisposed)
                {
                    return;
                }

                PositionThemedScrollBar();
                SynchronizeThemedScrollBar();
                PerformLayout();
            }

            private void HideThemedScrollBarOverlay()
            {
                if (_themedScrollBar != null &&
                    !_themedScrollBar.IsDisposed)
                {
                    _themedScrollBar.Visible = false;
                }
            }

            /// <summary>
            /// Rebuilds the native overlay when ItemsControl moves between
            /// containers. The scrollbar's native parent is fixed at handle
            /// creation and must always match the current surrounding HWND.
            /// </summary>
            protected override void OnParentChanged(EventArgs e)
            {
                CommitScrollBitmapCache();
                DisposeScrollBitmapCache();
                base.OnParentChanged(e);

                if (_disposingThemedScrollBar ||
                    IsDisposed ||
                    Disposing ||
                    _themedScrollBar == null)
                {
                    return;
                }

                if (!Object.ReferenceEquals(
                        _themedScrollBarNativeParent,
                        Parent))
                {
                    RebuildThemedScrollBar();
                }

                SynchronizeThemedScrollBar();
            }

            /// <summary>Moves the fixed overlay with its viewport owner.</summary>
            protected override void OnLocationChanged(EventArgs e)
            {
                base.OnLocationChanged(e);
                PositionThemedScrollBar();

                if (_scrollBitmapCacheActive)
                    PositionScrollBitmapSurface();
            }

            /// <summary>Mirrors ItemsControl visibility onto the sibling HWND.</summary>
            protected override void OnVisibleChanged(EventArgs e)
            {
                base.OnVisibleChanged(e);

                if (!Visible)
                    CommitScrollBitmapCache();

                if (_themedScrollBar == null ||
                    _themedScrollBar.IsDisposed)
                {
                    return;
                }

                _themedScrollBar.Visible =
                    _themedScrollBarRangeVisible &&
                    (Parent == null ||
                     (Visible && IsHandleCreated));

                if (_themedScrollBar.Visible)
                {
                    PositionThemedScrollBar();
                    BringThemedScrollBarOverlayToFront();
                }
            }

            /// <summary>
            /// Prevents the one-dimensional host's secondary native axis and
            /// removes the active native scrollbar style when a framework
            /// appearance is configured.
            /// </summary>
            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams parameters = base.CreateParams;

                    // ItemsControl is deliberately one-dimensional. Never let
                    // a provisional child bound expose cross-axis native
                    // chrome or change the viewport while layout converges.
                    parameters.Style &=
                        _orientation == Orientation.Vertical
                            ? ~WindowStyleHorizontalScroll
                            : ~WindowStyleVerticalScroll;

                    if (HasActiveThemedScrollBar)
                    {
                        parameters.Style &=
                            _orientation == Orientation.Vertical
                                ? ~WindowStyleVerticalScroll
                                : ~WindowStyleHorizontalScroll;
                    }

                    return parameters;
                }
            }

            /// <summary>Repositions a horizontal themed bar after direction changes.</summary>
            protected override void OnRightToLeftChanged(EventArgs e)
            {
                base.OnRightToLeftChanged(e);

                if (!_keepScrollBarOnRight)
                {
                    ContentRightToLeft =
                        RightToLeft == RightToLeft.Yes;
                }

                OnItemsControlFlowDirectionChanged();
            }

#if !WINFORMSXAML_PACKAGE
            internal ScrollBarControl ThemedScrollBarForTest
            {
                get { return _themedScrollBar; }
            }

            internal Rectangle ItemsViewportRectangleForTest
            {
                get { return GetItemsViewportRectangle(); }
            }

            internal bool ActiveNativeScrollStyleVisibleForTest
            {
                get
                {
                    if (!IsHandleCreated)
                        return false;

                    int style = GetWindowLong(
                        Handle,
                        WindowLongStyle);
                    int flag = _orientation == Orientation.Vertical
                        ? WindowStyleVerticalScroll
                        : WindowStyleHorizontalScroll;
                    return (style & flag) != 0;
                }
            }

            internal bool SecondaryNativeScrollStyleVisibleForTest
            {
                get
                {
                    if (!IsHandleCreated)
                        return false;

                    int style = GetWindowLong(
                        Handle,
                        WindowLongStyle);
                    int flag = _orientation == Orientation.Vertical
                        ? WindowStyleHorizontalScroll
                        : WindowStyleVerticalScroll;
                    return (style & flag) != 0;
                }
            }

            internal long ThemedNativeHideAttemptCountForTest
            {
                get { return _themedNativeHideAttemptCount; }
            }

            internal long SecondaryNativeHideAttemptCountForTest
            {
                get { return _secondaryNativeHideAttemptCount; }
            }

            internal long ThemedScrollBarSynchronizationCountForTest
            {
                get { return _themedScrollBarSynchronizationCount; }
            }

            internal long ThemedNativeChromeProbeCountForTest
            {
                get { return _themedNativeChromeProbeCount; }
            }

            internal void ValidateThemedScrollBarAxisInvariantForTest()
            {
                ValidateThemedScrollBarAxisInvariant();
            }
#endif
        }
    }
}
