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
            private const int MouseWheelDelta = 120;
            private const int DefaultSmoothScrollDuration = 120;
            private const int SmoothScrollTimerInterval = 15;
            private const int WmHorizontalScroll = 0x0114;
            private const int WmVerticalScroll = 0x0115;
            private const int ScrollBarLineDecrement = 0;
            private const int ScrollBarLineIncrement = 1;
            private const int ScrollBarPageDecrement = 2;
            private const int ScrollBarPageIncrement = 3;
            private const uint RedrawWindowAllChildren = 0x0080;
            private const uint RedrawWindowUpdateNow = 0x0100;

            private int _mouseWheelDeltaRemainder;
            private bool _applyingLogicalScrollCommand;
            private bool _legacyMouseWheelRegistered;
            private bool _smoothScroll;
            private int _smoothScrollDuration =
                DefaultSmoothScrollDuration;
            private Timer _smoothScrollTimer;
            internal bool _smoothScrollActive;
            private bool _smoothScrollForced;
            private bool _applyingSmoothScrollFrame;
            private int _smoothScrollStartOffset;
            private int _smoothScrollTargetOffset;
            private int _smoothScrollRequestedOffset;
            private int _smoothScrollStartTick;
            private int _smoothScrollLastFrameTick;
            private double _smoothScrollPosition;
            private double _smoothScrollVelocity;
            private int _interceptedNativeScrollDispatchDepth;
            private bool _logicalScrollMappingInitialized;
            private Orientation _logicalScrollMappingOrientation;
            private bool _logicalScrollMappingRightToLeft;
            private int _logicalScrollMappingMaximum;
            private int _savedLogicalScrollOffset;
            private bool _observingExternalScrollOrigin;
#if !WINFORMSXAML_PACKAGE
            private long _scrollVisualFramePublicationCount;
#endif

            /// <summary>
            /// Gets or sets whether wheel and line/page commands interpolate
            /// their logical offset on the UI thread. The default is false.
            /// </summary>
            [DefaultValue(false)]
            public bool SmoothScroll
            {
                get { return _smoothScroll; }
                set
                {
                    if (_smoothScroll == value)
                        return;

                    _smoothScroll = value;

                    if (!value)
                        StopSmoothScrollAnimation();
                }
            }

            /// <summary>
            /// Gets or sets the duration in milliseconds of one coalesced
            /// smooth-scroll transition. The default is 120 milliseconds.
            /// </summary>
            [DefaultValue(DefaultSmoothScrollDuration)]
            public int SmoothScrollDuration
            {
                get { return _smoothScrollDuration; }
                set
                {
                    if (value <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            "value",
                            "SmoothScrollDuration must be greater than zero.");
                    }

                    _smoothScrollDuration = value;

                    if (_smoothScrollTimer != null)
                    {
                        _smoothScrollTimer.Interval = Math.Max(
                            1,
                            Math.Min(
                                SmoothScrollTimerInterval,
                                value));
                    }
                }
            }

            /// <summary>
            /// Returns the nonnegative logical offset on the configured item axis.
            /// </summary>
            internal int GetLogicalScrollOffset()
            {
                if (!AutoScroll)
                    return Math.Max(0, _savedLogicalScrollOffset);

                if (_scrollBitmapCacheActive)
                {
                    return ClampLogicalScrollOffsetToMaximum(
                        _savedLogicalScrollOffset,
                        GetMaximumLogicalScrollOffset());
                }

                int maximum = GetMaximumLogicalScrollOffset();

                // A framework command owns L until its complete viewport
                // publication finishes. Direct virtualization can replace
                // native range M while realizing the destination; reading the
                // transient P with that new M would turn a requested RTL start
                // into one item of unintended forward movement.
                if (_applyingLogicalScrollCommand)
                {
                    return ClampLogicalScrollOffsetToMaximum(
                        _savedLogicalScrollOffset,
                        maximum);
                }

                bool rightToLeft = UsesInvertedHorizontalScrollMapping();

                // AutoScrollMinSize and viewport changes can replace M while
                // leaving the native physical origin P untouched. P cannot be
                // interpreted with the new M: doing so would turn a preserved
                // logical start into an arbitrary middle/end position. Until
                // reconciliation publishes P=M-L, the saved L is authoritative.
                if (!_logicalScrollMappingInitialized ||
                    _logicalScrollMappingOrientation != _orientation ||
                    _logicalScrollMappingRightToLeft != rightToLeft ||
                    _logicalScrollMappingMaximum != maximum)
                {
                    return ClampLogicalScrollOffsetToMaximum(
                        _savedLogicalScrollOffset,
                        maximum);
                }

                int physical = GetPhysicalScrollOffset();
                int logical = PhysicalToLogicalScrollOffset(
                    physical,
                    maximum,
                    rightToLeft);

                _savedLogicalScrollOffset = logical;
                return logical;
            }

            /// <summary>
            /// Clamps and publishes one logical position without rebuilding items.
            /// </summary>
            internal bool SetLogicalScrollOffset(int requestedOffset)
            {
                if (_smoothScrollActive &&
                    !_scrollBitmapCacheCommitting &&
                    !_applyingSmoothScrollFrame)
                {
                    StopSmoothScrollAnimation();
                }

                if (_scrollBitmapCacheActive &&
                    !_scrollBitmapCacheCommitting &&
                    !_applyingSmoothScrollFrame)
                {
                    CommitScrollBitmapCache();
                }

                if (!AutoScroll)
                    return false;

                int previous = GetLogicalScrollOffset();
                int maximum = GetMaximumLogicalScrollOffset();
                int normalized = ClampLogicalScrollOffsetToMaximum(
                    requestedOffset,
                    maximum);

                if (_applyingSmoothScrollFrame &&
                    _scrollBitmapCacheActive)
                {
                    if (IsScrollBitmapSnapshotCurrent() &&
                        ScrollBitmapCacheContains(normalized))
                    {
                        _savedLogicalScrollOffset = normalized;
                        RecordLogicalScrollMapping(
                            maximum,
                            UsesInvertedHorizontalScrollMapping());
                        SynchronizeScrollNavigatorForBitmapFrame(normalized);

                        if (TryPublishScrollBitmapFrame(normalized))
                            return normalized != previous;
                    }

                    CommitScrollBitmapCache();
                }

                bool rightToLeft = UsesInvertedHorizontalScrollMapping();
                int physical = LogicalToPhysicalScrollOffset(
                    normalized,
                    maximum,
                    rightToLeft);
                int previousPhysical = GetPhysicalScrollOffset();

                _savedLogicalScrollOffset = normalized;
                RecordLogicalScrollMapping(maximum, rightToLeft);

                if (previous == normalized &&
                    previousPhysical == physical &&
                    GetSecondaryPhysicalScrollOffset() == 0)
                {
                    bool virtualViewportNeedsPublication =
                        !IsVirtualViewportPublishedAtLogicalOffset(
                            normalized);

                    ReconcileVirtualViewportAtLogicalOffset(normalized);
                    SynchronizeThemedScrollBar();

                    // A repeated request at an already-published position has
                    // not invalidated any content. Forcing RDW_UPDATENOW here
                    // repaints every retained child for no visual change (most
                    // noticeably while an arrow is held at a range boundary).
                    // A virtual viewport that was not published is different:
                    // its reconciliation can realize or reposition rows, so
                    // flush that actual visual update immediately.
                    if (virtualViewportNeedsPublication)
                        PublishScrollVisualFrame();

                    return false;
                }

                bool previousApplying = _applyingLogicalScrollCommand;

                _applyingLogicalScrollCommand = true;

                try
                {
                    if (!TrySetThemedScrollDisplayOffset(physical))
                    {
                        AutoScrollPosition =
                            _orientation == Orientation.Vertical
                                ? new Point(0, physical)
                                : new Point(physical, 0);
                    }

                    HandleDirectVirtualViewportChanged();
                    SynchronizeThemedScrollBar();
                    EnsureThemedNativeChromeHiddenAfterScroll();
                }
                finally
                {
                    _applyingLogicalScrollCommand = previousApplying;
                }

                if (!previousApplying &&
                    !_applyingSmoothScrollFrame)
                {
                    ReconcileThemedNativeChrome();
                }

                PublishScrollVisualFrame();

                return normalized != previous;
            }

            /// <summary>
            /// Flushes only the paint work already invalidated by the native
            /// viewport move. ScrollableControl moves child HWNDs before their
            /// exposed regions and non-client scrollbar are necessarily
            /// painted; under sustained input that can leave blank rows or a
            /// stale thumb visible for several messages. RDW_UPDATENOW keeps
            /// the content and chrome in one published visual frame without
            /// invalidating the complete retained item tree.
            /// </summary>
            private void PublishScrollVisualFrame()
            {
                if (!IsHandleCreated ||
                    IsDisposed ||
                    Disposing)
                {
                    return;
                }

#if !WINFORMSXAML_PACKAGE
                _scrollVisualFramePublicationCount++;
#endif
                RedrawWindow(
                    Handle,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    RedrawWindowAllChildren |
                    RedrawWindowUpdateNow);

                // Native ScrollableControl and every realized child use
                // separate HWND paint queues. Flush only the regions those
                // windows already invalidated so a completed scroll message
                // cannot expose half-moved text or stale rows. No additional
                // region is invalidated here. The framework scrollbar is a
                // fixed sibling HWND, so publish its small surface separately.
                ScrollBarControl bar = _themedScrollBar;

                if (bar != null &&
                    !bar.IsDisposed &&
                    bar.IsHandleCreated &&
                    bar.Visible)
                {
                    bar.Update();
                }
            }

            [DllImport("user32.dll")]
            private static extern bool RedrawWindow(
                IntPtr window,
                IntPtr updateRectangle,
                IntPtr updateRegion,
                uint flags);

            private bool IsVirtualViewportPublishedAtLogicalOffset(
                int logicalOffset)
            {
                if (DirectVirtualActive)
                {
                    return DirectVirtualHasPublishedScrollAxis &&
                        DirectVirtualLastPublishedScrollAxis ==
                            logicalOffset;
                }

                if (LightweightActive)
                {
                    return LightweightHasViewportOffset &&
                        LightweightLastViewportOffset == logicalOffset;
                }

                return true;
            }

            private void ReconcileVirtualViewportAtLogicalOffset(
                int logicalOffset)
            {
                if ((!DirectVirtualActive && !LightweightActive) ||
                    IsVirtualViewportPublishedAtLogicalOffset(
                        logicalOffset))
                {
                    return;
                }

                HandleDirectVirtualViewportChanged();
            }

            internal void AttachScrollOriginObserver(Control marker)
            {
                if (marker == null)
                    return;

                marker.LocationChanged -=
                    new EventHandler(ScrollOriginMarkerLocationChanged);
                marker.LocationChanged +=
                    new EventHandler(ScrollOriginMarkerLocationChanged);
            }

            internal void DetachScrollOriginObserver(Control marker)
            {
                if (marker == null)
                    return;

                marker.LocationChanged -=
                    new EventHandler(ScrollOriginMarkerLocationChanged);
            }

            private void ScrollOriginMarkerLocationChanged(
                object sender,
                EventArgs e)
            {
                bool observesVirtualViewport =
                    DirectVirtualActive || LightweightActive;

                // Native ScrollableControl moves every child when its display
                // rectangle changes. The lazily-created extent/origin marker gives
                // one O(1) notification for native WM_SCROLL and inherited
                // AutoScrollPosition/Scroll.Value writes. Virtual modes use it
                // to publish their viewport; an ordinary host with framework
                // chrome uses it only to keep the fixed thumb synchronized.
                if ((!observesVirtualViewport &&
                     !HasActiveThemedScrollBar) ||
                    _observingExternalScrollOrigin ||
                    _applyingLogicalScrollCommand ||
                    _applyingSmoothScrollFrame ||
                    _runtimeLayoutInProgress ||
                    DirectVirtualSuppressScrollRefresh ||
                    DirectVirtualRefreshRunning ||
                    !AutoScroll ||
                    (observesVirtualViewport &&
                     (Runtime == null || Runtime.IsDisposed)) ||
                    IsDisposed ||
                    Disposing)
                {
                    return;
                }

                _observingExternalScrollOrigin = true;

                try
                {
                    int maximum = GetMaximumLogicalScrollOffset();
                    bool rightToLeft =
                        UsesInvertedHorizontalScrollMapping();
                    int logical = PhysicalToLogicalScrollOffset(
                        GetPhysicalScrollOffset(),
                        maximum,
                        rightToLeft);

                    _savedLogicalScrollOffset = logical;
                    RecordLogicalScrollMapping(
                        maximum,
                        rightToLeft);
                    ReconcileVirtualViewportAtLogicalOffset(logical);
                    SynchronizeThemedScrollBar();
                }
                finally
                {
                    _observingExternalScrollOrigin = false;
                }
            }

            /// <summary>
            /// Captures the current logical position before a property changes
            /// the active axis or RTL mapping. The next layout/range pass keeps
            /// this value instead of reinterpreting the old physical origin.
            /// </summary>
            private int CaptureLogicalScrollOffsetForTransition()
            {
                int logical = GetLogicalScrollOffset();
                _savedLogicalScrollOffset = logical;
                return logical;
            }

            private void RestoreSavedLogicalScrollOffset(int logical)
            {
                _savedLogicalScrollOffset = Math.Max(0, logical);
                _logicalScrollMappingInitialized = false;

                if (AutoScroll)
                    ReconcileLogicalScrollOffsetAfterRangeChange();
            }

            /// <summary>
            /// Re-clamps the saved logical origin against the current range and
            /// republishes its physical representation. This is required even
            /// when L itself did not change because horizontal RTL uses P=M-L.
            /// </summary>
            private void ReconcileLogicalScrollOffsetAfterRangeChange()
            {
                if (!AutoScroll)
                    return;

                int maximum = GetMaximumLogicalScrollOffset();
                int logical = GetLogicalScrollOffset();
                int normalized = ClampLogicalScrollOffsetToMaximum(
                    logical,
                    maximum);
                bool rightToLeft = UsesInvertedHorizontalScrollMapping();
                int physical = LogicalToPhysicalScrollOffset(
                    normalized,
                    maximum,
                    rightToLeft);

                _savedLogicalScrollOffset = normalized;
                RecordLogicalScrollMapping(maximum, rightToLeft);

                if (GetPhysicalScrollOffset() == physical &&
                    GetSecondaryPhysicalScrollOffset() == 0)
                {
                    return;
                }

                bool previousApplying = _applyingLogicalScrollCommand;
                _applyingLogicalScrollCommand = true;

                try
                {
                    if (!TrySetThemedScrollDisplayOffset(physical))
                    {
                        AutoScrollPosition =
                            _orientation == Orientation.Vertical
                                ? new Point(0, physical)
                                : new Point(physical, 0);
                    }
                }
                finally
                {
                    _applyingLogicalScrollCommand = previousApplying;
                }
            }

            private void RecordLogicalScrollMapping(
                int maximum,
                bool rightToLeft)
            {
                _logicalScrollMappingInitialized = true;
                _logicalScrollMappingOrientation = _orientation;
                _logicalScrollMappingRightToLeft = rightToLeft;
                _logicalScrollMappingMaximum = Math.Max(0, maximum);
            }

            private bool UsesInvertedHorizontalScrollMapping()
            {
                return _orientation == Orientation.Horizontal &&
                    ContentRightToLeft;
            }

            private int GetPhysicalScrollOffset()
            {
                Point current = AutoScrollPosition;
                int value = _orientation == Orientation.Vertical
                    ? current.Y
                    : current.X;

                return NegateNativeScrollCoordinate(value);
            }

            private int GetSecondaryPhysicalScrollOffset()
            {
                Point current = AutoScrollPosition;
                int value = _orientation == Orientation.Vertical
                    ? current.X
                    : current.Y;

                return NegateNativeScrollCoordinate(value);
            }

            private static int NegateNativeScrollCoordinate(int value)
            {
                if (value >= 0)
                    return 0;

                return value == Int32.MinValue
                    ? Int32.MaxValue
                    : -value;
            }

            private static int PhysicalToLogicalScrollOffset(
                int physical,
                int maximum,
                bool rightToLeft)
            {
                int normalized = ClampLogicalScrollOffsetToMaximum(
                    physical,
                    maximum);

                return rightToLeft
                    ? maximum - normalized
                    : normalized;
            }

            private static int LogicalToPhysicalScrollOffset(
                int logical,
                int maximum,
                bool rightToLeft)
            {
                int normalized = ClampLogicalScrollOffsetToMaximum(
                    logical,
                    maximum);

                return rightToLeft
                    ? maximum - normalized
                    : normalized;
            }

            private static int ClampLogicalScrollOffsetToMaximum(
                int value,
                int maximum)
            {
                if (value <= 0 || maximum <= 0)
                    return 0;

                return value > maximum
                    ? maximum
                    : value;
            }

            /// <summary>
            /// Applies a native line, page, first, or last command through the
            /// same logical position setter used by mouse-wheel input.
            /// </summary>
            internal bool ScrollBy(ScrollEventType type)
            {
                int current = IsSmoothScrollRelativeCommand(type)
                    ? GetLogicalScrollCommandBaseOffset()
                    : GetLogicalScrollOffset();
                int requested = GetRelativeScrollTarget(type, current);

                return IsSmoothScrollRelativeCommand(type)
                    ? ApplyLogicalScrollTarget(requested, true)
                    : SetLogicalScrollOffset(requested);
            }

            internal bool ProcessMouseWheelDelta(int delta)
            {
                if (!AutoScroll || delta == 0)
                    return false;

                int lines = SystemInformation.MouseWheelScrollLines;

                if (lines == 0)
                    return false;

                int unit = lines == -1
                    ? GetLargeScrollChange()
                    : SaturatingMultiply(
                        GetSmallScrollChange(),
                        Math.Max(0, lines));
                long scaled =
                    (long)_mouseWheelDeltaRemainder +
                    ((long)delta * (long)unit);
                int pixelDelta = ClampLongToInt(
                    scaled / MouseWheelDelta);
                _mouseWheelDeltaRemainder = (int)(
                    scaled % MouseWheelDelta);

                if (pixelDelta == 0)
                    return false;

                long requested =
                    (long)GetLogicalScrollCommandBaseOffset() - pixelDelta;

                return ApplyLogicalScrollTarget(
                    ClampLongToNonnegativeInt(requested),
                    true);
            }

            private bool ApplyLogicalScrollTarget(
                int requestedOffset,
                bool allowSmoothScroll)
            {
                if (allowSmoothScroll && _smoothScroll)
                {
                    return BeginSmoothScrollAnimation(
                        requestedOffset);
                }

                return SetLogicalScrollOffset(requestedOffset);
            }

            private bool BeginSmoothScrollAnimation(
                int requestedOffset)
            {
                return BeginSmoothScrollAnimation(
                    requestedOffset,
                    false);
            }

            private bool BeginSmoothScrollAnimation(
                int requestedOffset,
                bool forceAnimation)
            {
                return BeginSmoothScrollAnimation(
                    requestedOffset,
                    forceAnimation,
                    true);
            }

            private bool BeginSmoothScrollAnimation(
                int requestedOffset,
                bool forceAnimation,
                bool advanceActiveFrame)
            {
                ClearActiveItemScrollRequest();

                int requested = Math.Max(0, requestedOffset);
                int target = ClampLogicalScrollOffset(requested);

                if ((!_smoothScroll && !forceAnimation) ||
                    !AutoScroll ||
                    !IsHandleCreated ||
                    IsDisposed ||
                    Disposing)
                {
                    return SetLogicalScrollOffset(target);
                }

                if (_smoothScrollActive &&
                    requested == _smoothScrollRequestedOffset &&
                    target == _smoothScrollTargetOffset)
                {
                    return true;
                }

                bool changedRequest = _smoothScrollActive
                    ? requested != _smoothScrollRequestedOffset ||
                        target != _smoothScrollTargetOffset
                    : target != GetLogicalScrollOffset();
                int now = Environment.TickCount;

                if (_smoothScrollActive && advanceActiveFrame)
                    AdvanceSmoothScrollFrame(now);

                target = ClampLogicalScrollOffset(requested);
                int current = GetLogicalScrollOffset();

                if (current == target)
                {
                    StopSmoothScrollAnimation();
                    return changedRequest;
                }

                _smoothScrollStartOffset = current;
                _smoothScrollTargetOffset = target;
                _smoothScrollRequestedOffset = requested;
                _smoothScrollStartTick = now;
                _smoothScrollLastFrameTick = now;
                _smoothScrollPosition = current;

                TryPrepareScrollBitmapCache(current, target);

                if (!_smoothScrollActive)
                    _smoothScrollVelocity = 0.0;

                _smoothScrollActive = true;
                _smoothScrollForced = forceAnimation;

                Timer timer = EnsureSmoothScrollTimer();
                timer.Start();
                return true;
            }

            private Timer EnsureSmoothScrollTimer()
            {
                if (_smoothScrollTimer == null)
                {
                    _smoothScrollTimer = new Timer();
                    _smoothScrollTimer.Tick +=
                        new EventHandler(SmoothScrollTimerTick);
                }

                _smoothScrollTimer.Interval = Math.Max(
                    1,
                    Math.Min(
                        SmoothScrollTimerInterval,
                        _smoothScrollDuration));
                return _smoothScrollTimer;
            }

            private void SmoothScrollTimerTick(
                object sender,
                EventArgs e)
            {
                AdvanceSmoothScrollFrame(Environment.TickCount);
            }

            private bool AdvanceSmoothScrollFrame(int nowTick)
            {
                if (!_smoothScrollActive)
                    return false;

                if ((!_smoothScroll && !_smoothScrollForced) ||
                    !AutoScroll ||
                    !IsHandleCreated ||
                    IsDisposed ||
                    Disposing)
                {
                    StopSmoothScrollAnimation();
                    return false;
                }

                int target = ClampLogicalScrollOffset(
                    _smoothScrollRequestedOffset);

                if (target != _smoothScrollTargetOffset)
                    _smoothScrollTargetOffset = target;

                if (_smoothScrollStartOffset == target)
                {
                    ApplySmoothScrollFrameOffset(target);
                    StopSmoothScrollAnimation();
                    return true;
                }

                int elapsed = GetSmoothScrollElapsedMilliseconds(
                    _smoothScrollStartTick,
                    nowTick);

                if (elapsed >= _smoothScrollDuration)
                {
                    ApplySmoothScrollFrameOffset(target);
                    StopSmoothScrollAnimation();
                    return true;
                }

                int frameElapsed = GetSmoothScrollElapsedMilliseconds(
                    _smoothScrollLastFrameTick,
                    nowTick);

                if (frameElapsed > 0)
                {
                    double seconds =
                        Math.Min(frameElapsed, _smoothScrollDuration) /
                        1000.0;
                    double responseSeconds = Math.Max(
                        0.001,
                        _smoothScrollDuration / 1000.0);
                    double omega = 6.5 / responseSeconds;
                    double previousPosition =
                        _smoothScrollPosition;
                    double displacement =
                        _smoothScrollPosition - target;
                    double coupledVelocity =
                        _smoothScrollVelocity +
                        (omega * displacement);
                    double decay = Math.Exp(-omega * seconds);

                    _smoothScrollPosition = target +
                        ((displacement +
                          (coupledVelocity * seconds)) * decay);
                    _smoothScrollVelocity =
                        (_smoothScrollVelocity -
                         (omega * coupledVelocity * seconds)) * decay;

                    if ((previousPosition < target &&
                         _smoothScrollPosition >= target) ||
                        (previousPosition > target &&
                         _smoothScrollPosition <= target))
                    {
                        _smoothScrollPosition = target;
                        _smoothScrollVelocity = 0.0;
                    }

                    int maximum =
                        GetMaximumLogicalScrollOffset();

                    if (_smoothScrollPosition <= 0.0)
                    {
                        _smoothScrollPosition = 0.0;

                        if (_smoothScrollVelocity < 0.0)
                            _smoothScrollVelocity = 0.0;
                    }
                    else if (_smoothScrollPosition >= maximum)
                    {
                        _smoothScrollPosition = maximum;

                        if (_smoothScrollVelocity > 0.0)
                            _smoothScrollVelocity = 0.0;
                    }

                    _smoothScrollLastFrameTick = nowTick;
                }

                int next = _smoothScrollPosition <= 0.0
                    ? 0
                    : _smoothScrollPosition >= Int32.MaxValue
                        ? Int32.MaxValue
                        : (int)(_smoothScrollPosition + 0.5);

                ApplySmoothScrollFrameOffset(next);
                return true;
            }

            private void ApplySmoothScrollFrameOffset(int offset)
            {
                if (GetLogicalScrollOffset() == offset)
                    return;

                bool previous = _applyingSmoothScrollFrame;
                _applyingSmoothScrollFrame = true;

                try
                {
                    SetLogicalScrollOffset(offset);

                    // Variable-height virtualization can refine measured row
                    // extents and preserve an item anchor while publishing this
                    // frame. That correction is the committed visual position;
                    // continuing from the pre-correction floating trajectory
                    // makes the next timer tick visibly jump back. Rebase the
                    // oscillator after every complete frame and discard only a
                    // velocity that now points away from the active target.
                    int committed = GetLogicalScrollOffset();
                    _smoothScrollPosition = committed;

                    if ((_smoothScrollTargetOffset > committed &&
                         _smoothScrollVelocity < 0.0) ||
                        (_smoothScrollTargetOffset < committed &&
                         _smoothScrollVelocity > 0.0))
                    {
                        _smoothScrollVelocity = 0.0;
                    }
                }
                finally
                {
                    _applyingSmoothScrollFrame = previous;
                }
            }

#if !WINFORMSXAML_PACKAGE
            internal bool ApplySmoothScrollFrameForTest(
                int elapsedMilliseconds)
            {
                if (!_smoothScrollActive)
                    return false;

                int elapsed = Math.Max(0, elapsedMilliseconds);
                int nowTick = unchecked(
                    _smoothScrollStartTick + elapsed);

                AdvanceSmoothScrollFrame(nowTick);
                return _smoothScrollActive;
            }

            internal bool SmoothScrollAnimationActiveForTest
            {
                get { return _smoothScrollActive; }
            }

            internal int SmoothScrollTargetOffsetForTest
            {
                get { return _smoothScrollTargetOffset; }
            }

            internal long ScrollVisualFramePublicationCountForTest
            {
                get { return _scrollVisualFramePublicationCount; }
            }

            internal object SmoothScrollTimerIdentityForTest
            {
                get { return _smoothScrollTimer; }
            }
#endif

            internal static int GetSmoothScrollElapsedMilliseconds(
                int startTick,
                int currentTick)
            {
                uint elapsed = unchecked(
                    (uint)(currentTick - startTick));

                return elapsed >= (uint)Int32.MaxValue
                    ? Int32.MaxValue
                    : (int)elapsed;
            }

            internal void StopSmoothScrollAnimation()
            {
                bool wasActive = _smoothScrollActive;

                if (_smoothScrollTimer != null)
                    _smoothScrollTimer.Stop();

                _smoothScrollActive = false;
                CommitScrollBitmapCache();
                FlushDeferredDirectVirtualScrollExtent();

                if (wasActive && HasActiveThemedScrollBar)
                {
                    EnsureThemedNativeChromeHiddenAfterScroll();
                    PositionThemedScrollBar();
                }

                _smoothScrollForced = false;
                _smoothScrollStartOffset = 0;
                _smoothScrollTargetOffset = 0;
                _smoothScrollRequestedOffset = 0;
                _smoothScrollStartTick = 0;
                _smoothScrollLastFrameTick = 0;
                _smoothScrollPosition = 0.0;
                _smoothScrollVelocity = 0.0;
                ClearActiveItemScrollRequest();
            }

            internal void RetargetSmoothScrollAnimation(
                int requestedOffset)
            {
                if (!_smoothScrollActive)
                    return;

                int requested = Math.Max(0, requestedOffset);
                int target = ClampLogicalScrollOffset(requested);

                if (requested == _smoothScrollRequestedOffset &&
                    target == _smoothScrollTargetOffset)
                {
                    return;
                }

                int current = GetLogicalScrollOffset();

                if (current == target)
                {
                    StopSmoothScrollAnimation();
                    return;
                }

                _smoothScrollStartOffset = current;
                _smoothScrollTargetOffset = target;
                _smoothScrollRequestedOffset = requested;
                _smoothScrollStartTick = Environment.TickCount;
                _smoothScrollLastFrameTick = _smoothScrollStartTick;
                _smoothScrollPosition = current;
            }

            internal void DisposeSmoothScrollAnimation()
            {
                Timer timer = _smoothScrollTimer;
                _smoothScrollTimer = null;
                StopSmoothScrollAnimation();

                if (timer == null)
                    return;

                timer.Stop();
                timer.Tick -=
                    new EventHandler(SmoothScrollTimerTick);
                timer.Dispose();
            }

            private int GetLogicalScrollCommandBaseOffset()
            {
                return _smoothScroll && _smoothScrollActive
                    ? _smoothScrollTargetOffset
                    : GetLogicalScrollOffset();
            }

            private static bool IsSmoothScrollRelativeCommand(
                ScrollEventType type)
            {
                return type == ScrollEventType.SmallDecrement ||
                    type == ScrollEventType.SmallIncrement ||
                    type == ScrollEventType.LargeDecrement ||
                    type == ScrollEventType.LargeIncrement;
            }

            internal bool ProcessLegacyMouseWheel(int delta)
            {
                Point mouse = PointToClient(Control.MousePosition);
                HandledMouseEventArgs args = new HandledMouseEventArgs(
                    MouseButtons.None,
                    0,
                    mouse.X,
                    mouse.Y,
                    delta);
                int previous = GetLogicalScrollOffset();

                OnMouseWheel(args);

                return args.Handled ||
                    GetLogicalScrollOffset() != previous;
            }

            /// <summary>
            /// Intercepts line and page commands for this control's own native
            /// scrollbar before ScrollableControl moves every retained child.
            /// Interception is used only for explicit smooth scrolling; the
            /// default immediate path stays with native live-control movement.
            /// </summary>
            protected override void WndProc(ref Message message)
            {
                SuppressNativeScrollStyleChange(ref message);

                if (TryHandleNativeRelativeScrollMessage(ref message))
                    return;

                // Thumb tracking/position, First/Last, and EndScroll retain
                // the native ScrollableControl behavior. If a preceding line,
                // page, or wheel gesture owns a cached viewport, expose the
                // real tree at that exact frame before native code reads or
                // changes its display rectangle. This also keeps a drag from
                // operating on the old physical origin behind the bitmap.
                if (_scrollBitmapCacheActive &&
                    IsOwnerScrollMessageForConfiguredAxis(message))
                {
                    CommitScrollBitmapCache();
                }

                base.WndProc(ref message);
            }

            private bool IsOwnerScrollMessageForConfiguredAxis(
                Message message)
            {
                if (message.LParam != IntPtr.Zero)
                    return false;

                return (_orientation == Orientation.Vertical &&
                        message.Msg == WmVerticalScroll) ||
                    (_orientation == Orientation.Horizontal &&
                        message.Msg == WmHorizontalScroll);
            }

            private bool TryHandleNativeRelativeScrollMessage(
                ref Message message)
            {
                if (!AutoScroll ||
                    !IsHandleCreated ||
                    IsDisposed ||
                    Disposing ||
                    HasActiveThemedScrollBar ||
                    _applyingLogicalScrollCommand ||
                    _applyingSmoothScrollFrame ||
                    message.LParam != IntPtr.Zero)
                {
                    return false;
                }

                ScrollOrientation scrollOrientation;

                if (message.Msg == WmVerticalScroll &&
                    _orientation == Orientation.Vertical)
                {
                    scrollOrientation =
                        ScrollOrientation.VerticalScroll;
                }
                else if (message.Msg == WmHorizontalScroll &&
                         _orientation == Orientation.Horizontal)
                {
                    scrollOrientation =
                        ScrollOrientation.HorizontalScroll;
                }
                else
                {
                    return false;
                }

                int command = unchecked(
                    (int)((long)message.WParam & 0xffffL));
                ScrollEventType type;

                if (command == ScrollBarLineDecrement)
                    type = ScrollEventType.SmallDecrement;
                else if (command == ScrollBarLineIncrement)
                    type = ScrollEventType.SmallIncrement;
                else if (command == ScrollBarPageDecrement)
                    type = ScrollEventType.LargeDecrement;
                else if (command == ScrollBarPageIncrement)
                    type = ScrollEventType.LargeIncrement;
                else
                {
                    return false;
                }

                int oldPhysical = GetPhysicalScrollOffset();
                int proposedPhysical =
                    GetNativeScrollMessageProposedValue(
                        type,
                        oldPhysical,
                        scrollOrientation);
                ScrollEventType logicalType =
                    GetLogicalNativeRelativeScrollType(type);
                int target = GetRelativeScrollTarget(
                    logicalType,
                    GetLogicalScrollCommandBaseOffset());

                if (_smoothScroll)
                {
                    BeginSmoothScrollAnimation(
                        target,
                        false,
                        false);
                }
                else
                    return false;

                SetScrollState(ScrollStateUserHasScrolled, true);

                ScrollEventArgs args = new ScrollEventArgs(
                    type,
                    oldPhysical,
                    proposedPhysical,
                    scrollOrientation);

                _interceptedNativeScrollDispatchDepth++;

                try
                {
                    OnScroll(args);
                }
                finally
                {
                    _interceptedNativeScrollDispatchDepth--;
                }

                message.Result = IntPtr.Zero;
                return true;
            }

            private int GetNativeScrollMessageProposedValue(
                ScrollEventType type,
                int current,
                ScrollOrientation orientation)
            {
                ScrollProperties managed =
                    orientation == ScrollOrientation.VerticalScroll
                        ? (ScrollProperties)VerticalScroll
                        : (ScrollProperties)HorizontalScroll;
                int change = type == ScrollEventType.SmallDecrement ||
                    type == ScrollEventType.SmallIncrement
                        ? Math.Max(0, managed.SmallChange)
                        : Math.Max(0, managed.LargeChange);
                long proposed = current;

                if (type == ScrollEventType.SmallDecrement ||
                    type == ScrollEventType.LargeDecrement)
                {
                    proposed -= change;
                }
                else
                {
                    proposed += change;
                }

                int maximum = Math.Max(
                    0,
                    managed.Maximum -
                    Math.Max(0, managed.LargeChange) + 1);
                int normalized = ClampLongToNonnegativeInt(proposed);

                return normalized > maximum
                    ? maximum
                    : normalized;
            }

            internal void RegisterLegacyMouseWheelRouting()
            {
                if (_legacyMouseWheelRegistered)
                    return;

                _legacyMouseWheelRegistered =
                    LegacyMouseWheelRouter.Register(this);
            }

            internal void UnregisterLegacyMouseWheelRouting()
            {
                if (!_legacyMouseWheelRegistered)
                    return;

                _legacyMouseWheelRegistered = false;
                LegacyMouseWheelRouter.Unregister(this);
            }

            /// <summary>
            /// Handles modern WM_MOUSEWHEEL delivery without relying on the
            /// platform ScrollableControl implementation to move the viewport.
            /// </summary>
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                if (e == null)
                    return;

                // ScrollableControl normally moves by the raw wheel delta
                // before raising MouseWheel. Suppress only its protected
                // visibility state bits while subscribers receive the event.
                // Assigning HScroll/VScroll here used to modify native styles,
                // client geometry, and layout twice per wheel message; the
                // custom bar was then repositioned against those transient
                // client sizes and visibly jumped.
                bool horizontal = GetScrollState(
                    ScrollStateHScrollVisible);
                bool vertical = GetScrollState(
                    ScrollStateVScrollVisible);

                SetScrollState(ScrollStateHScrollVisible, false);
                SetScrollState(ScrollStateVScrollVisible, false);

                try
                {
                    base.OnMouseWheel(e);
                }
                finally
                {
                    // Restore only the configured scrolling-axis state bit. A
                    // transient cross-axis flag inferred from complex child
                    // bounds must not be resurrected after every wheel message.
                    SetScrollState(
                        ScrollStateHScrollVisible,
                        !HasActiveThemedScrollBar &&
                        _orientation == Orientation.Horizontal &&
                            horizontal);
                    SetScrollState(
                        ScrollStateVScrollVisible,
                        !HasActiveThemedScrollBar &&
                        _orientation == Orientation.Vertical &&
                            vertical);

                    if (!HasActiveThemedScrollBar)
                    {
                        HideSecondaryNativeScrollBar();
                    }
                }

                HandledMouseEventArgs handled =
                    e as HandledMouseEventArgs;

                if (handled != null && handled.Handled)
                    return;

                bool moved = ProcessMouseWheelDelta(e.Delta);

                if (handled != null && moved)
                    handled.Handled = true;
            }

            /// <summary>
            /// Lets an unhandled navigation key from any focused item child
            /// scroll the nearest ItemsControl. Native editors and other child
            /// controls keep the first opportunity to consume their input key;
            /// command routing reaches this parent only when they do not.
            /// </summary>
            protected override bool ProcessDialogKey(Keys keyData)
            {
                Keys modifiers = keyData & Keys.Modifiers;

                if (AutoScroll &&
                    Enabled &&
                    modifiers == Keys.None)
                {
                    Keys key = keyData & Keys.KeyCode;
                    ScrollEventType type;
                    bool recognized = true;

                    if (key == Keys.Home)
                        type = ScrollEventType.First;
                    else if (key == Keys.End)
                        type = ScrollEventType.Last;
                    else if (key == Keys.PageUp)
                        type = ScrollEventType.LargeDecrement;
                    else if (key == Keys.PageDown)
                        type = ScrollEventType.LargeIncrement;
                    else if (_orientation == Orientation.Vertical &&
                             key == Keys.Up)
                    {
                        type = ScrollEventType.SmallDecrement;
                    }
                    else if (_orientation == Orientation.Vertical &&
                             key == Keys.Down)
                    {
                        type = ScrollEventType.SmallIncrement;
                    }
                    else if (_orientation == Orientation.Horizontal &&
                             key == Keys.Left)
                    {
                        type = ContentRightToLeft
                            ? ScrollEventType.SmallIncrement
                            : ScrollEventType.SmallDecrement;
                    }
                    else if (_orientation == Orientation.Horizontal &&
                             key == Keys.Right)
                    {
                        type = ContentRightToLeft
                            ? ScrollEventType.SmallDecrement
                            : ScrollEventType.SmallIncrement;
                    }
                    else
                    {
                        type = ScrollEventType.EndScroll;
                        recognized = false;
                    }

                    if (recognized && ScrollBy(type))
                        return true;
                }

                return base.ProcessDialogKey(keyData);
            }

            internal int GetNativeScrollEventTarget(ScrollEventArgs e)
            {
                if (e == null)
                    return GetLogicalScrollOffset();

                if (e.Type == ScrollEventType.First)
                {
                    return UsesInvertedHorizontalScrollMapping()
                        ? GetMaximumLogicalScrollOffset()
                        : 0;
                }

                if (e.Type == ScrollEventType.Last)
                {
                    return UsesInvertedHorizontalScrollMapping()
                        ? 0
                        : GetMaximumLogicalScrollOffset();
                }

                if (e.Type == ScrollEventType.ThumbTrack)
                {
                    int physical = !_liveScroll
                        // With full-drag disabled, ThumbTrack reports the
                        // proposed thumb value while the display rectangle is
                        // intentionally still at its committed position.
                        // Preserve that actual origin until ThumbPosition.
                        ? GetPhysicalScrollOffset()
                        : Math.Max(
                            0,
                            GetNativeTrackPosition(
                                e.ScrollOrientation,
                                e.NewValue));

                    return NativePhysicalToLogicalScrollOffset(
                        physical);
                }

                if (e.Type == ScrollEventType.ThumbPosition)
                {
                    return NativePhysicalToLogicalScrollOffset(
                        Math.Max(0, e.NewValue));
                }

                if (e.Type == ScrollEventType.SmallDecrement ||
                    e.Type == ScrollEventType.SmallIncrement ||
                    e.Type == ScrollEventType.LargeDecrement ||
                    e.Type == ScrollEventType.LargeIncrement)
                {
                    return GetRelativeScrollTarget(
                        GetLogicalNativeRelativeScrollType(e.Type),
                        _smoothScroll && _smoothScrollActive
                            ? _smoothScrollTargetOffset
                            : NativePhysicalToLogicalScrollOffset(
                                Math.Max(0, e.OldValue)));
                }

                return GetLogicalScrollOffset();
            }

            private int NativePhysicalToLogicalScrollOffset(
                int physical)
            {
                int maximum = GetMaximumLogicalScrollOffset();

                return PhysicalToLogicalScrollOffset(
                    physical,
                    maximum,
                    UsesInvertedHorizontalScrollMapping());
            }

            private int LogicalToNativePhysicalScrollOffset(
                int logical)
            {
                int maximum = GetMaximumLogicalScrollOffset();

                return LogicalToPhysicalScrollOffset(
                    logical,
                    maximum,
                    UsesInvertedHorizontalScrollMapping());
            }

            private ScrollEventType GetLogicalNativeRelativeScrollType(
                ScrollEventType type)
            {
                if (!UsesInvertedHorizontalScrollMapping())
                    return type;

                if (type == ScrollEventType.SmallDecrement)
                    return ScrollEventType.SmallIncrement;

                if (type == ScrollEventType.SmallIncrement)
                    return ScrollEventType.SmallDecrement;

                if (type == ScrollEventType.LargeDecrement)
                    return ScrollEventType.LargeIncrement;

                if (type == ScrollEventType.LargeIncrement)
                    return ScrollEventType.LargeDecrement;

                return type;
            }

            private int GetRelativeScrollTarget(
                ScrollEventType type,
                int current)
            {
                long requested = current;

                if (type == ScrollEventType.SmallDecrement)
                    requested -= GetSmallScrollChange();
                else if (type == ScrollEventType.SmallIncrement)
                    requested += GetSmallScrollChange();
                else if (type == ScrollEventType.LargeDecrement)
                    requested -= GetLargeScrollChange();
                else if (type == ScrollEventType.LargeIncrement)
                    requested += GetLargeScrollChange();
                else if (type == ScrollEventType.First)
                    requested = 0L;
                else if (type == ScrollEventType.Last)
                    requested = GetMaximumLogicalScrollOffset();

                int normalized = ClampLongToNonnegativeInt(requested);
                int maximum = GetMaximumLogicalScrollOffset();

                return normalized > maximum
                    ? maximum
                    : normalized;
            }

            private int ClampLogicalScrollOffset(
                int requestedOffset)
            {
                int maximum = GetMaximumLogicalScrollOffset();

                return ClampLogicalScrollOffsetToMaximum(
                    requestedOffset,
                    maximum);
            }

            private int GetMaximumLogicalScrollOffset()
            {
                if (HasActiveThemedScrollBar && AutoScroll)
                {
                    // A framework-owned scrollbar must have exactly one range
                    // authority. ItemsControl layout publishes the complete
                    // logical content extent through AutoScrollMinSize; the
                    // client axis is its matching viewport. Never substitute
                    // VerticalScroll/HorizontalScroll here: ScrollableControl
                    // can transiently change those native values while its
                    // chrome is being hidden, which used to make the custom
                    // thumb jump between two unrelated denominators.
                    int themedExtent =
                        _orientation == Orientation.Vertical
                            ? AutoScrollMinSize.Height
                            : AutoScrollMinSize.Width;
                    int themedViewport =
                        _orientation == Orientation.Vertical
                            ? ClientSize.Height
                            : ClientSize.Width;
                    long themedMaximum =
                        (long)Math.Max(0, themedExtent) -
                        (long)Math.Max(0, themedViewport);

                    if (themedMaximum <= 0L)
                        return 0;

                    return themedMaximum >= Int32.MaxValue
                        ? Int32.MaxValue
                        : (int)themedMaximum;
                }

                if ((HasActiveThemedScrollBar ||
                     (UsesInvertedHorizontalScrollMapping() &&
                      IsHandleCreated)) &&
                    AutoScroll)
                {
                    ScrollProperties managed =
                        _orientation == Orientation.Vertical
                            ? (ScrollProperties)VerticalScroll
                            : (ScrollProperties)HorizontalScroll;
                    long managedMaximum =
                        (long)managed.Maximum -
                        (long)Math.Max(0, managed.LargeChange) +
                        1L;

                    if (managedMaximum <= 0L)
                        return 0;

                    return managedMaximum >= Int32.MaxValue
                        ? Int32.MaxValue
                        : (int)managedMaximum;
                }

                int extent = _orientation == Orientation.Vertical
                    ? AutoScrollMinSize.Height
                    : AutoScrollMinSize.Width;
                int viewport = UsesInvertedHorizontalScrollMapping()
                    ? ClientSize.Width
                    : _orientation == Orientation.Vertical
                        ? ClientSize.Height - Padding.Top - Padding.Bottom
                        : ClientSize.Width - Padding.Left - Padding.Right;
                long maximum = (long)Math.Max(0, extent) -
                    (long)Math.Max(0, viewport);

                if (maximum <= 0L)
                    return 0;

                return maximum >= Int32.MaxValue
                    ? Int32.MaxValue
                    : (int)maximum;
            }

#if !WINFORMSXAML_PACKAGE
            internal int GetMaximumLogicalScrollOffsetForTest
            {
                get { return GetMaximumLogicalScrollOffset(); }
            }
#endif

            private int GetSmallScrollChange()
            {
                if ((DirectVirtualActive || LightweightActive) &&
                    _fixedItemSize > 0)
                {
                    long stride =
                        (long)_fixedItemSize + (long)Math.Max(0, _spacing);

                    if (stride >= Int32.MaxValue)
                        return Int32.MaxValue;

                    return Math.Max(1, (int)stride);
                }

                return Math.Max(1, Font == null ? 1 : Font.Height);
            }

            private int GetLargeScrollChange()
            {
                if (HasActiveThemedScrollBar)
                {
                    Rectangle themedViewport =
                        GetItemsViewportRectangle();
                    int length = _orientation == Orientation.Vertical
                        ? themedViewport.Height
                        : themedViewport.Width;

                    return Math.Max(
                        GetSmallScrollChange(),
                        Math.Max(0, length));
                }

                int viewport = _orientation == Orientation.Vertical
                    ? ClientSize.Height - Padding.Top - Padding.Bottom
                    : ClientSize.Width - Padding.Left - Padding.Right;

                return Math.Max(GetSmallScrollChange(), viewport);
            }

            private static int SaturatingMultiply(int left, int right)
            {
                long value = (long)left * (long)right;

                return value >= Int32.MaxValue
                    ? Int32.MaxValue
                    : (int)Math.Max(0L, value);
            }

            private static int ClampLongToNonnegativeInt(long value)
            {
                if (value <= 0L)
                    return 0;

                return value >= Int32.MaxValue
                    ? Int32.MaxValue
                    : (int)value;
            }

            private static int ClampLongToInt(long value)
            {
                if (value <= Int32.MinValue)
                    return Int32.MinValue;

                return value >= Int32.MaxValue
                    ? Int32.MaxValue
                    : (int)value;
            }
        }
    }
}
