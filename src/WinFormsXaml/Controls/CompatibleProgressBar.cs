using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinFormsXaml
{
    /// <summary>
    /// Uses native marquee when it is available and a native Blocks grow/drain
    /// animation on older Windows/common-controls versions.
    /// </summary>
    public sealed class CompatibleProgressBar : ProgressBar
    {
        private const int NativeMarqueeStyle = 0x00000008;
        private const int ExtendedLayoutRightToLeft = 0x00400000;
        private const int WindowStyleChild = 0x40000000;
        private const int PaintMessage = 0x000F;

        private const int SetRangeMessage = 0x0401;
        private const int SetPositionMessage = 0x0402;
        private const int SetRange32Message = 0x0406;
        private const int SetBarColorMessage = 0x0409;
        private const int SetMarqueeMessage = 0x040A;
        private const int SetBackgroundColorMessage = 0x2001;

        private const int HideWindow = 0;
        private const int ShowWithoutActivation = 4;
        private const int TrackInset = 2;
        private const int NativeBlockGap = 2;
        private const int MaxLegacyBlockCount = 100;
        private const int LegacyMarqueeSpeedDivisor = 3;
        private const int PausedTimerInterval = 250;
        private const string NativeProgressClass = "msctls_progress32";

        private Timer _animationTimer;
        private IntPtr _maskHandle;
        private int _marqueeFrame;
        private int _nativeParentPosition;
        private int _maskRevealOffset;
        private int _maskRevealWidth;
        private int _maskWindowWidth;
        private int _maskWindowHeight;
        private bool _preferMarqueeFallback;
        private bool _legacyMarqueeActive;
        private bool _marqueeFrameInitialized;
        private bool _maskVisible;
        private bool _handleReadyForFallback;
        private bool _legacyNativeStateConfigured;
        private bool _settingLegacyNativeState;
        private bool _updatingLegacyRenderingState;
        private bool _repaintingLegacyUnderlay;
        private bool _maskUnderlayNeedsRepaint;
        private bool _rendererSelectionInitialized;
        private bool _useLegacyRendererForHandle;
        private bool _resourcesDisposed;

        /// <summary>
        /// Creates a progress bar that selects native marquee or the Blocks
        /// compatibility renderer when its handle is created.
        /// </summary>
        public CompatibleProgressBar()
        {
            _nativeParentPosition = Int32.MinValue;
            _maskRevealOffset = -1;
            _maskRevealWidth = -1;
            _maskWindowWidth = -1;
            _maskWindowHeight = -1;
        }

        /// <summary>
        /// Forces the native Blocks fallback even when native marquee is
        /// available. False keeps capability-based automatic selection.
        /// </summary>
        [DefaultValue(false)]
        public bool PreferMarqueeFallback
        {
            get { return _preferMarqueeFallback; }
            set
            {
                if (_preferMarqueeFallback == value)
                    return;

                bool usedFallback = ShouldUseLegacyRenderer();
                _preferMarqueeFallback = value;
                bool usesFallback = CalculateUseLegacyRenderer();

                if (IsHandleCreated &&
                    base.Style == ProgressBarStyle.Marquee &&
                    usedFallback != usesFallback)
                {
                    RecreateHandle();
                }
                else
                {
                    UpdateLegacyRenderingState();
                }
            }
        }

        /// <summary>
        /// Keeps ProgressBar.Style as the one logical/public style while the
        /// fallback handle is an ordinary native Blocks progress control.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                bool useLegacyRenderer;

                if (IsHandleCreated && _rendererSelectionInitialized)
                {
                    useLegacyRenderer = _useLegacyRendererForHandle;
                }
                else
                {
                    useLegacyRenderer = CalculateUseLegacyRenderer();
                    _useLegacyRendererForHandle = useLegacyRenderer;
                    _rendererSelectionInitialized = true;
                }

                if (useLegacyRenderer &&
                    base.Style == ProgressBarStyle.Marquee)
                {
                    parameters.Style &= ~NativeMarqueeStyle;

                    // The fallback implements both directions itself. Do not
                    // depend on WS_EX_LAYOUTRTL, which is unavailable on Win98.
                    parameters.ExStyle &= ~ExtendedLayoutRightToLeft;
                }

                return parameters;
            }
        }

        /// <summary>Initializes the selected renderer for the new native handle.</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _handleReadyForFallback = true;
            _legacyNativeStateConfigured = false;
            _nativeParentPosition = Int32.MinValue;
            UpdateLegacyRenderingState();
        }

        /// <summary>Stops fallback rendering before the native handle is released.</summary>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            _handleReadyForFallback = false;
            _legacyNativeStateConfigured = false;

            if (!_resourcesDisposed && _animationTimer != null)
                _animationTimer.Enabled = false;

            DestroyLegacyMask();
            _nativeParentPosition = Int32.MinValue;
            base.OnHandleDestroyed(e);
            _rendererSelectionInitialized = false;
        }

        /// <summary>Synchronizes fallback animation with visibility.</summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateLegacyRenderingState();
        }

        /// <summary>Synchronizes fallback animation with enabled state.</summary>
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);

            if (_maskHandle != IntPtr.Zero)
                EnableWindow(_maskHandle, Enabled);

            UpdateLegacyRenderingState();
        }

        /// <summary>Applies native progress colors to the fallback overlay.</summary>
        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            SynchronizeMaskColors();
        }

        /// <summary>Applies the native block color to the fallback overlay.</summary>
        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            SynchronizeMaskColors();
        }

        /// <summary>Repaints the fallback after text direction changes.</summary>
        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);

            if (_legacyMarqueeActive)
                ApplyLegacyNativeVisual();
        }

        /// <summary>Repaints the fallback after layout direction changes.</summary>
        protected override void OnRightToLeftLayoutChanged(EventArgs e)
        {
            base.OnRightToLeftLayoutChanged(e);

            if (_legacyMarqueeActive)
                ApplyLegacyNativeVisual();
        }

        /// <summary>Resizes and reapplies the native fallback mask.</summary>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            if (_maskHandle != IntPtr.Zero)
            {
                SizeLegacyMaskWindow();
                ApplyLegacyNativeVisual();
            }
        }

        /// <summary>
        /// Intercepts unsupported marquee messages only while the fallback
        /// renderer owns the current native handle.
        /// </summary>
        protected override void WndProc(ref Message message)
        {
            if (ShouldUseLegacyMarquee() &&
                !_settingLegacyNativeState)
            {
                if (message.Msg == SetMarqueeMessage)
                {
                    // ProgressBar stores MarqueeAnimationSpeed before sending
                    // PBM_SETMARQUEE, so the inherited property remains the
                    // authoritative timer interval.
                    UpdateLegacyRenderingState();
                    message.Result = new IntPtr(1);
                    return;
                }

                if (message.Msg == SetRangeMessage ||
                    message.Msg == SetRange32Message ||
                    message.Msg == SetPositionMessage)
                {
                    // ProgressBar stores Minimum, Maximum, and Value before
                    // these messages. Preserve those managed values while the
                    // fallback native handle keeps its private 0..100 state.
                    message.Result = IntPtr.Zero;
                    return;
                }
            }

            if (message.Msg == SetBackgroundColorMessage ||
                message.Msg == SetBarColorMessage)
            {
                base.WndProc(ref message);

                if (_maskHandle != IntPtr.Zero)
                {
                    SendMessage(
                        _maskHandle,
                        message.Msg,
                        message.WParam,
                        message.LParam);
                }

                return;
            }

            if (message.Msg == PaintMessage)
            {
                base.WndProc(ref message);

                if (!_repaintingLegacyUnderlay)
                    RedrawLegacyMask();

                return;
            }

            base.WndProc(ref message);
        }

        /// <summary>Stops the timer and releases the private native mask.</summary>
        protected override void Dispose(bool disposing)
        {
            if (!disposing || _resourcesDisposed)
            {
                base.Dispose(disposing);
                return;
            }

            _resourcesDisposed = true;
            Timer animationTimer = _animationTimer;
            _animationTimer = null;

            if (animationTimer != null)
            {
                animationTimer.Enabled = false;
                animationTimer.Tick -= OnAnimationTick;
            }
            _legacyMarqueeActive = false;
            DestroyLegacyMask();

            try
            {
                base.Dispose(true);
            }
            finally
            {
                if (animationTimer != null)
                    animationTimer.Dispose();
            }
        }

        private bool ShouldUseLegacyMarquee()
        {
            return
                ShouldUseLegacyRenderer() &&
                base.Style == ProgressBarStyle.Marquee;
        }

        private bool ShouldUseLegacyRenderer()
        {
            if (IsHandleCreated && _rendererSelectionInitialized)
                return _useLegacyRendererForHandle;

            return CalculateUseLegacyRenderer();
        }

        private bool CalculateUseLegacyRenderer()
        {
            return
                _preferMarqueeFallback ||
                DetectNativeMarqueeUnavailable();
        }

        private void UpdateLegacyRenderingState()
        {
            if (_resourcesDisposed || _updatingLegacyRenderingState)
                return;

            _updatingLegacyRenderingState = true;

            try
            {
                bool active = ShouldUseLegacyMarquee();

                if (_legacyMarqueeActive != active)
                {
                    _legacyMarqueeActive = active;

                    if (active)
                    {
                        _marqueeFrame = 0;
                        _marqueeFrameInitialized = true;
                    }
                    else
                    {
                        _marqueeFrameInitialized = false;
                        HideLegacyMask();
                    }
                }

                if (active && !_marqueeFrameInitialized)
                {
                    _marqueeFrame = 0;
                    _marqueeFrameInitialized = true;
                }

                if (active &&
                    _handleReadyForFallback &&
                    IsHandleCreated)
                {
                    EnsureLegacyMask();
                    ConfigureLegacyNativeState();
                    ApplyLegacyNativeVisual();
                }

                bool runTimer =
                    active &&
                    _handleReadyForFallback &&
                    Visible &&
                    Enabled &&
                    IsHandleCreated &&
                    base.MarqueeAnimationSpeed > 0;

                if (runTimer)
                {
                    EnsureAnimationTimer();
                    UpdateTimerInterval();
                    _animationTimer.Enabled = true;
                }
                else if (_animationTimer != null)
                {
                    UpdateTimerInterval();
                    _animationTimer.Enabled = false;
                }
            }
            finally
            {
                _updatingLegacyRenderingState = false;
            }
        }

        private void UpdateTimerInterval()
        {
            if (_animationTimer == null)
                return;

            _animationTimer.Interval = GetLegacyTimerInterval(
                base.MarqueeAnimationSpeed);
        }

        private void EnsureAnimationTimer()
        {
            if (_animationTimer != null)
                return;

            _animationTimer = new Timer();
            _animationTimer.Tick += OnAnimationTick;
        }

        private static int GetLegacyTimerInterval(int requestedSpeed)
        {
            if (requestedSpeed <= 0)
                return PausedTimerInterval;

            if (requestedSpeed >
                Int32.MaxValue / LegacyMarqueeSpeedDivisor)
            {
                return Int32.MaxValue;
            }

            // MarqueeAnimationSpeed remains the inherited/public value. Only
            // the fallback's frame cadence is slowed to one third.
            return requestedSpeed * LegacyMarqueeSpeedDivisor;
        }

        private void OnAnimationTick(
            object sender,
            EventArgs e)
        {
            if (!ShouldUseLegacyMarquee() ||
                !_handleReadyForFallback ||
                !Visible ||
                !IsHandleCreated ||
                base.MarqueeAnimationSpeed <= 0 ||
                !Enabled)
            {
                UpdateLegacyRenderingState();
                return;
            }

            int lastFrame = GetLegacyLastFrame(
                ClientSize.Width,
                ClientSize.Height);
            _marqueeFrame = GetNextLegacyFrame(
                _marqueeFrame,
                lastFrame);
            ApplyLegacyNativeVisual();
        }

        private void ConfigureLegacyNativeState()
        {
            if (_legacyNativeStateConfigured)
                return;

            _settingLegacyNativeState = true;

            try
            {
                int packedRange = 100 << 16;

                SendMessage(
                    Handle,
                    SetRangeMessage,
                    IntPtr.Zero,
                    new IntPtr(packedRange));
                _nativeParentPosition = Int32.MinValue;
                _legacyNativeStateConfigured = true;
            }
            finally
            {
                _settingLegacyNativeState = false;
            }
        }

        private void ApplyLegacyNativeVisual()
        {
            if (!_legacyMarqueeActive ||
                !_handleReadyForFallback ||
                !IsHandleCreated)
            {
                return;
            }

            EnsureLegacyMask();

            int parentPosition;
            int maskRevealOffset;
            int maskRevealWidth;

            CalculateLegacyFrame(
                _marqueeFrame,
                IsRightToLeftVisual(),
                ClientSize.Width,
                ClientSize.Height,
                out parentPosition,
                out maskRevealOffset,
                out maskRevealWidth);

            // The parent always stays empty and owns the one visible border.
            // Both animation phases reveal part of the same 100%-filled
            // native Blocks child. Using one fill raster is important on old
            // common-controls: an intermediate parent value and a clipped
            // full value do not have identical block/end-cap shading.
            SetNativeParentPosition(parentPosition);

            if (maskRevealWidth > 0)
            {
                SetLegacyMaskRange(
                    maskRevealOffset,
                    maskRevealWidth);
            }
            else
                HideLegacyMask();
        }

        private static void CalculateLegacyFrame(
            int frame,
            bool rightToLeft,
            int clientWidth,
            int clientHeight,
            out int parentPosition,
            out int maskRevealOffset,
            out int maskRevealWidth)
        {
            int trackWidth = Math.Max(
                0,
                clientWidth - TrackInset * 2);
            int blockCount = GetLegacyBlockCount(
                clientWidth,
                clientHeight);
            int lastFrame = blockCount * 2;
            int normalized = NormalizeLegacyFrame(frame, lastFrame);
            bool growing = normalized <= blockCount;
            int phase = growing
                ? normalized
                : normalized - blockCount;
            int pitch = GetLegacyBlockPitch(clientHeight);
            int coveredWidth = GetLegacyCoveredWidth(
                phase,
                blockCount,
                trackWidth,
                pitch);
            parentPosition = 0;

            if (!rightToLeft)
            {
                maskRevealOffset = growing ? 0 : coveredWidth;
                maskRevealWidth = growing
                    ? coveredWidth
                    : trackWidth - coveredWidth;
            }
            else
            {
                maskRevealOffset = growing
                    ? trackWidth - coveredWidth
                    : 0;
                maskRevealWidth = growing
                    ? coveredWidth
                    : trackWidth - coveredWidth;
            }

            if (maskRevealWidth <= 0)
                maskRevealOffset = 0;
        }

        private static int GetLegacyLastFrame(
            int clientWidth,
            int clientHeight)
        {
            return GetLegacyBlockCount(
                clientWidth,
                clientHeight) * 2;
        }

        private static int GetLegacyBlockCount(
            int clientWidth,
            int clientHeight)
        {
            int trackWidth = Math.Max(
                0,
                clientWidth - TrackInset * 2);
            int pitch = GetLegacyBlockPitch(clientHeight);

            if (trackWidth == 0)
                return 1;

            return Math.Min(
                MaxLegacyBlockCount,
                Math.Max(
                    1,
                    (int)(((long)trackWidth + pitch - 1) /
                    pitch)));
        }

        private static int GetLegacyBlockPitch(int clientHeight)
        {
            int trackHeight = Math.Max(
                1,
                clientHeight - TrackInset * 2);
            int blockWidth = (int)Math.Max(
                1L,
                (long)trackHeight * 2 / 3);

            return blockWidth + NativeBlockGap;
        }

        private static int GetLegacyCoveredWidth(
            int phase,
            int blockCount,
            int trackWidth,
            int pitch)
        {
            if (phase <= 0 || trackWidth <= 0)
                return 0;

            if (phase >= blockCount)
                return trackWidth;

            if ((long)blockCount * pitch >= trackWidth)
            {
                return (int)Math.Min(
                    (long)trackWidth,
                    (long)phase * pitch);
            }

            // A very wide control is capped at one hundred animation steps.
            // Spread those steps over the complete track instead of leaving
            // one enormous final jump after ninety-nine native-sized blocks.
            return (int)(
                ((long)trackWidth * phase + blockCount - 1) /
                blockCount);
        }

        private static int NormalizeLegacyFrame(
            int frame,
            int lastFrame)
        {
            int normalized = frame % (lastFrame + 1);

            if (normalized < 0)
                normalized += lastFrame + 1;

            return normalized;
        }

        private static int GetNextLegacyFrame(
            int frame,
            int lastFrame)
        {
            if (lastFrame <= 0)
                return 0;

            int normalized = NormalizeLegacyFrame(frame, lastFrame);

            // Frame zero is the initial empty state. The last drain frame is
            // also empty, so a repeating cycle continues at frame one rather
            // than displaying the same visual for a second interval.
            return normalized >= lastFrame ? 1 : normalized + 1;
        }

        private bool IsRightToLeftVisual()
        {
            return
                RightToLeft == RightToLeft.Yes &&
                RightToLeftLayout;
        }

        private void SetNativeParentPosition(int position)
        {
            if (_nativeParentPosition == position ||
                !IsHandleCreated)
            {
                return;
            }

            _settingLegacyNativeState = true;

            try
            {
                SendMessage(
                    Handle,
                    SetPositionMessage,
                    new IntPtr(position),
                    IntPtr.Zero);
                _nativeParentPosition = position;
            }
            finally
            {
                _settingLegacyNativeState = false;
            }
        }

        private void EnsureLegacyMask()
        {
            if (_maskHandle != IntPtr.Zero)
                return;

            _maskHandle = CreateWindowEx(
                0,
                NativeProgressClass,
                String.Empty,
                WindowStyleChild,
                0,
                0,
                Math.Max(0, ClientSize.Width),
                Math.Max(0, ClientSize.Height),
                Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_maskHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Could not create the native progress mask.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            int packedRange = 100 << 16;

            SendMessage(
                _maskHandle,
                SetRangeMessage,
                IntPtr.Zero,
                new IntPtr(packedRange));
            SendMessage(
                _maskHandle,
                SetPositionMessage,
                new IntPtr(100),
                IntPtr.Zero);
            EnableWindow(_maskHandle, Enabled);
            _maskRevealOffset = -1;
            _maskRevealWidth = -1;
            _maskWindowWidth = -1;
            _maskWindowHeight = -1;
            _maskVisible = false;
            SizeLegacyMaskWindow();
            SynchronizeMaskColors();
        }

        private void SizeLegacyMaskWindow()
        {
            if (_maskHandle == IntPtr.Zero)
                return;

            int width = Math.Max(0, ClientSize.Width);
            int height = Math.Max(0, ClientSize.Height);

            if (_maskWindowWidth == width &&
                _maskWindowHeight == height)
            {
                return;
            }

            if (!MoveWindow(
                    _maskHandle,
                    0,
                    0,
                    width,
                    height,
                    true))
            {
                throw new InvalidOperationException(
                    "Could not size the native progress mask.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            _maskWindowWidth = width;
            _maskWindowHeight = height;
            _maskRevealOffset = -1;
            _maskRevealWidth = -1;

            if (_maskVisible)
                _maskUnderlayNeedsRepaint = true;
        }

        private void SetLegacyMaskRange(
            int revealOffset,
            int revealWidth)
        {
            EnsureLegacyMask();
            SizeLegacyMaskWindow();

            int trackWidth = Math.Max(
                0,
                ClientSize.Width - TrackInset * 2);
            int trackHeight = Math.Max(
                0,
                ClientSize.Height - TrackInset * 2);
            revealOffset = Math.Min(
                trackWidth,
                Math.Max(0, revealOffset));
            revealWidth = Math.Min(
                trackWidth - revealOffset,
                Math.Max(0, revealWidth));

            if (revealWidth <= 0 || trackHeight <= 0)
            {
                HideLegacyMask();
                return;
            }

            if (_maskRevealOffset != revealOffset ||
                _maskRevealWidth != revealWidth)
            {
                bool repaintUnderlay =
                    _maskUnderlayNeedsRepaint ||
                    _maskVisible;
                IntPtr region = CreateLegacyMaskRegion(
                    revealOffset,
                    revealWidth,
                    ClientSize.Width,
                    ClientSize.Height);

                if (SetWindowRgn(_maskHandle, region, false) == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    DeleteObject(region);

                    throw new InvalidOperationException(
                        "Could not apply the native progress mask region.",
                        new Win32Exception(error));
                }

                // SetWindowRgn owns the region after a successful call.
                _maskRevealOffset = revealOffset;
                _maskRevealWidth = revealWidth;
                _maskUnderlayNeedsRepaint = false;

                if (repaintUnderlay)
                    RepaintLegacyParentUnderlay();
            }

            if (!_maskVisible)
            {
                ShowWindow(_maskHandle, ShowWithoutActivation);
                _maskVisible = true;
            }

            RedrawLegacyMask();
        }

        private static IntPtr CreateLegacyMaskRegion(
            int revealOffset,
            int revealWidth,
            int clientWidth,
            int clientHeight)
        {
            int trackWidth = Math.Max(
                0,
                clientWidth - TrackInset * 2);
            int trackHeight = Math.Max(
                0,
                clientHeight - TrackInset * 2);
            revealOffset = Math.Min(
                trackWidth,
                Math.Max(0, revealOffset));
            revealWidth = Math.Min(
                trackWidth - revealOffset,
                Math.Max(0, revealWidth));
            int left = TrackInset + revealOffset;
            IntPtr region = CreateRectRgn(
                left,
                TrackInset,
                left + revealWidth,
                TrackInset + trackHeight);

            if (region == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Could not create the native progress mask region.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            return region;
        }

        private void HideLegacyMask()
        {
            if (_maskHandle == IntPtr.Zero || !_maskVisible)
                return;

            ShowWindow(_maskHandle, HideWindow);
            _maskVisible = false;
            _maskUnderlayNeedsRepaint = false;
            RepaintLegacyParentUnderlay();
        }

        private void RepaintLegacyParentUnderlay()
        {
            if (_repaintingLegacyUnderlay || !IsHandleCreated)
                return;

            _repaintingLegacyUnderlay = true;

            try
            {
                InvalidateRect(Handle, IntPtr.Zero, true);
                UpdateWindow(Handle);
            }
            finally
            {
                _repaintingLegacyUnderlay = false;
            }
        }

        private void RedrawLegacyMask()
        {
            if (_maskHandle == IntPtr.Zero || !_maskVisible)
                return;

            InvalidateRect(_maskHandle, IntPtr.Zero, true);
            UpdateWindow(_maskHandle);
        }

        private void SynchronizeMaskColors()
        {
            if (_maskHandle == IntPtr.Zero)
                return;

            SendMessage(
                _maskHandle,
                SetBackgroundColorMessage,
                IntPtr.Zero,
                new IntPtr(ColorTranslator.ToWin32(BackColor)));
            SendMessage(
                _maskHandle,
                SetBarColorMessage,
                IntPtr.Zero,
                new IntPtr(ColorTranslator.ToWin32(ForeColor)));
        }

        private void DestroyLegacyMask()
        {
            if (_maskHandle == IntPtr.Zero)
                return;

            IntPtr handle = _maskHandle;
            _maskHandle = IntPtr.Zero;
            _maskVisible = false;
            _maskRevealOffset = -1;
            _maskRevealWidth = -1;
            _maskWindowWidth = -1;
            _maskWindowHeight = -1;
            _maskUnderlayNeedsRepaint = false;
            DestroyWindow(handle);
        }

        private static bool DetectNativeMarqueeUnavailable()
        {
            OperatingSystem os = Environment.OSVersion;

            if (RequiresLegacyRenderer(
                os.Platform,
                os.Version.Major,
                os.Version.Minor,
                true))
            {
                return true;
            }

            try
            {
                return RequiresLegacyRenderer(
                    os.Platform,
                    os.Version.Major,
                    os.Version.Minor,
                    Application.RenderWithVisualStyles);
            }
            catch
            {
                return true;
            }
        }

        private static bool RequiresLegacyRenderer(
            PlatformID platform,
            int operatingSystemMajor,
            int operatingSystemMinor,
            bool renderWithVisualStyles)
        {
            // Native marquee requires the version 6 Common Controls activation
            // used by WinForms visual styles. Old Windows cannot provide it.
            if (platform != PlatformID.Win32NT)
                return true;

            if (operatingSystemMajor < 5 ||
                (operatingSystemMajor == 5 && operatingSystemMinor == 0))
            {
                return true;
            }

            return !renderWithVisualStyles;
        }

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Auto,
            SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int extendedStyle,
            string className,
            string windowName,
            int style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnableWindow(
            IntPtr window,
            bool enabled);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(
            IntPtr window,
            int x,
            int y,
            int width,
            int height,
            bool repaint);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(
            IntPtr window,
            int message,
            IntPtr parameter,
            IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(
            IntPtr window,
            int command);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowRgn(
            IntPtr window,
            IntPtr region,
            bool redraw);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(
            IntPtr window,
            IntPtr rectangle,
            bool erase);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr window);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRectRgn(
            int left,
            int top,
            int right,
            int bottom);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr value);
    }
}
