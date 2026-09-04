using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        public partial class ItemsControl
        {
            private const long ScrollBitmapCacheMaximumBytes =
                12L * 1024L * 1024L;
            private const int WmNonClientHitTest = 0x0084;

            private Bitmap _scrollBitmapCache;
            private HostedScrollBitmapSurface _scrollBitmapSurface;
            private bool _scrollBitmapCacheActive;
            private bool _scrollBitmapCacheCommitting;
            private long _scrollBitmapLogicalStart;
            private long _scrollBitmapLogicalEnd;
            private int _scrollBitmapLogicalOffset;
            private Rectangle _scrollBitmapViewport;
            private Size _scrollBitmapOwnerClientSize;
            private ArrayList _scrollBitmapRenderedItems;
            private long _scrollBitmapPublicationRevision;
            private int _scrollBitmapRefreshGeneration;
            private Orientation _scrollBitmapOrientation;
            private int _scrollBitmapMaximum;
            private ArrayList _scrollBitmapEligibilityRecords;
            private long _scrollBitmapEligibilityPublicationRevision;
            private int _scrollBitmapEligibilityRefreshGeneration;
            private bool _scrollBitmapEligibilityResult;
#if !WINFORMSXAML_PACKAGE
            private long _scrollBitmapCaptureCount;
            private long _scrollBitmapFrameCount;
            private long _scrollBitmapCommitCount;
#endif

            /// <summary>
            /// Fixed sibling surface used while an explicitly animated
            /// scroll owns a valid bitmap slice. It never joins
            /// ItemsControl.Controls and is therefore not translated by
            /// ScrollableControl.
            /// </summary>
            private sealed class HostedScrollBitmapSurface : Control
            {
                private readonly ItemsControl _owner;

                internal HostedScrollBitmapSurface(ItemsControl owner)
                {
                    _owner = owner;
                    TabStop = false;
                    SetStyle(
                        ControlStyles.UserPaint |
                        ControlStyles.AllPaintingInWmPaint |
                        ControlStyles.Opaque,
                        true);
                    SetStyle(ControlStyles.Selectable, false);
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
                            parameters.Parent = _owner.Parent.Handle;
                        }

                        return parameters;
                    }
                }

                protected override void OnPaint(PaintEventArgs e)
                {
                    if (_owner != null)
                        _owner.PaintScrollBitmapFrame(e.Graphics);
                }

                protected override void WndProc(ref Message message)
                {
                    if (message.Msg == WmNonClientHitTest)
                    {
                        // Finish the transaction before Windows selects a live
                        // child target. The real tree is then at exactly the
                        // same offset as the last image, so returning transparent
                        // preserves the original input without replaying it.
                        if (_owner != null)
                            _owner.CommitScrollBitmapCache();

                        message.Result = new IntPtr(-1);
                        return;
                    }

                    base.WndProc(ref message);
                }
            }

            private bool TryPrepareScrollBitmapCache(
                int currentOffset,
                int targetOffset)
            {
                if (_scrollBitmapCacheActive)
                {
                    if (IsScrollBitmapSnapshotCurrent() &&
                        ScrollBitmapCacheContains(targetOffset))
                        return true;

                    CommitScrollBitmapCache();
                }

                ArrayList capturedRecords = RenderedItems;
                long capturedPublicationRevision =
                    RenderedItemPublicationRevision;
                int capturedRefreshGeneration = RefreshGeneration;
                Orientation capturedOrientation = _orientation;
                Size capturedClientSize = ClientSize;

                if (!CanUseScrollBitmapCache(capturedRecords))
                    return false;

                Rectangle viewport = GetItemsViewportRectangle();
                int viewportAxis = _orientation == Orientation.Vertical
                    ? viewport.Height
                    : viewport.Width;

                if (viewport.Width <= 0 ||
                    viewport.Height <= 0 ||
                    viewportAxis <= 0)
                {
                    return false;
                }

                int maximum = GetMaximumLogicalScrollOffset();
                long contentAxis = (long)maximum + viewportAxis;
                long low = Math.Min(currentOffset, targetOffset);
                long high = Math.Max(currentOffset, targetOffset);
                long start = Math.Max(0L, low - viewportAxis);
                long end = Math.Min(
                    contentAxis,
                    high + ((long)viewportAxis * 2L));
                int crossAxis = _orientation == Orientation.Vertical
                    ? viewport.Width
                    : viewport.Height;
                long bytesPerAxis =
                    (long)Math.Max(1, crossAxis) * 4L;

                long cacheAxis = Math.Max(1L, end - start);
                long requestedBytes = cacheAxis >
                    ScrollBitmapCacheMaximumBytes / bytesPerAxis
                        ? ScrollBitmapCacheMaximumBytes + 1L
                        : cacheAxis * bytesPerAxis;

                if (start > Int32.MaxValue ||
                    cacheAxis > Int32.MaxValue ||
                    requestedBytes <= 0L ||
                    requestedBytes > ScrollBitmapCacheMaximumBytes)
                {
                    return false;
                }

                Bitmap bitmap = null;

                try
                {
                    bitmap = _orientation == Orientation.Vertical
                        ? new Bitmap(viewport.Width, (int)cacheAxis)
                        : new Bitmap((int)cacheAxis, viewport.Height);

                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(BackColor);
                    }

                    if (!CaptureRenderedItemsIntoScrollBitmap(
                            bitmap,
                            capturedRecords,
                            viewport,
                            currentOffset,
                            start))
                    {
                        bitmap.Dispose();
                        return false;
                    }
                }
                catch
                {
                    if (bitmap != null)
                        bitmap.Dispose();

                    return false;
                }

                if (IsDisposed ||
                    Disposing ||
                    ContainsFocus ||
                    _orientation != capturedOrientation ||
                    ClientSize != capturedClientSize ||
                    GetItemsViewportRectangle() != viewport ||
                    !Object.ReferenceEquals(
                        RenderedItems,
                        capturedRecords) ||
                    RenderedItemPublicationRevision !=
                        capturedPublicationRevision ||
                    RefreshGeneration != capturedRefreshGeneration ||
                    !CanUseScrollBitmapCache(capturedRecords))
                {
                    bitmap.Dispose();
                    return false;
                }

                DisposeScrollBitmapImage();
                _scrollBitmapCache = bitmap;
                _scrollBitmapLogicalStart = start;
                _scrollBitmapLogicalEnd = end;
                _scrollBitmapLogicalOffset = currentOffset;
                _scrollBitmapViewport = viewport;
                _scrollBitmapOwnerClientSize = capturedClientSize;
                _scrollBitmapRenderedItems = capturedRecords;
                _scrollBitmapPublicationRevision =
                    capturedPublicationRevision;
                _scrollBitmapRefreshGeneration =
                    capturedRefreshGeneration;
                _scrollBitmapOrientation = capturedOrientation;
                _scrollBitmapMaximum = maximum;
                _scrollBitmapCacheActive = true;
#if !WINFORMSXAML_PACKAGE
                _scrollBitmapCaptureCount++;
#endif

                try
                {
                    EnsureScrollBitmapSurface();

                    if (_scrollBitmapSurface == null ||
                        _scrollBitmapSurface.IsDisposed)
                    {
                        throw new InvalidOperationException();
                    }

                    PositionScrollBitmapSurface();
                    _scrollBitmapSurface.Visible = true;
                    _scrollBitmapSurface.Invalidate();
                    _scrollBitmapSurface.Update();
                    BringScrollBitmapSurfaceToFront();
                    BringThemedScrollBarOverlayToFront();
                    return true;
                }
                catch
                {
                    _scrollBitmapCacheActive = false;

                    if (_scrollBitmapSurface != null &&
                        !_scrollBitmapSurface.IsDisposed)
                    {
                        _scrollBitmapSurface.Visible = false;
                    }

                    DisposeScrollBitmapImage();
                    return false;
                }
            }

            private bool CanUseScrollBitmapCache(ArrayList records)
            {
                if (!AutoScroll ||
                    DirectVirtualActive ||
                    LightweightActive ||
                    Wrap ||
                    ContainsFocus ||
                    Parent == null ||
                    !Parent.IsHandleCreated ||
                    !IsHandleCreated ||
                    IsDisposed ||
                    Disposing ||
                    records == null ||
                    records.Count == 0 ||
                    BackColor.A != 255 ||
                    BackgroundImage != null ||
                    UsesInvertedHorizontalScrollMapping())
                {
                    return false;
                }

                if (Object.ReferenceEquals(
                        records,
                        _scrollBitmapEligibilityRecords) &&
                    RenderedItemPublicationRevision ==
                        _scrollBitmapEligibilityPublicationRevision &&
                    RefreshGeneration ==
                        _scrollBitmapEligibilityRefreshGeneration)
                {
                    return _scrollBitmapEligibilityResult;
                }

                bool eligible = true;
                int i;

                for (i = 0; i < records.Count; i++)
                {
                    RenderedItemRecord record =
                        records[i] as RenderedItemRecord;

                    if (record == null ||
                        record.Control == null ||
                        record.Control.IsDisposed ||
                        ContainsUnsupportedScrollBitmapControl(
                            record.Control))
                    {
                        eligible = false;
                        break;
                    }
                }

                _scrollBitmapEligibilityRecords = records;
                _scrollBitmapEligibilityPublicationRevision =
                    RenderedItemPublicationRevision;
                _scrollBitmapEligibilityRefreshGeneration =
                    RefreshGeneration;
                _scrollBitmapEligibilityResult = eligible;
                return eligible;
            }

            /// <summary>
            /// Publishes only the navigator position while a bitmap owns the
            /// visible viewport. A styled scrollbar is a separate control and
            /// can use its normal synchronization path. For native chrome,
            /// SetScrollInfo repaints the non-client thumb without changing
            /// ScrollableControl.AutoScrollPosition or moving any child HWNDs.
            /// The live tree and WinForms' managed scroll state are reconciled
            /// once by CommitScrollBitmapCache.
            /// </summary>
            private void SynchronizeScrollNavigatorForBitmapFrame(
                int logicalOffset)
            {
                if (HasActiveThemedScrollBar)
                {
                    SynchronizeThemedScrollBar();
                    return;
                }

                if (!IsHandleCreated || IsDisposed || Disposing)
                    return;

                int maximum = GetMaximumLogicalScrollOffset();
                int physical = LogicalToPhysicalScrollOffset(
                    logicalOffset,
                    maximum,
                    UsesInvertedHorizontalScrollMapping());
                NativeScrollInfo info = new NativeScrollInfo();

                info.cbSize = NativeScrollInfoSize;
                info.fMask = SIF_POS;
                info.nPos = physical;

                try
                {
                    SetScrollInfo(
                        Handle,
                        _orientation == Orientation.Vertical
                            ? SB_VERT
                            : SB_HORZ,
                        ref info,
                        true);
                }
                catch
                {
                    // A failed non-client repaint must not abort the cached
                    // transaction. The final live commit restores full state.
                }
            }

            /// <summary>
            /// Restores the live control tree before focus enters the item host.
            /// </summary>
            protected override void OnEnter(EventArgs e)
            {
                CommitScrollBitmapCache();
                base.OnEnter(e);
            }

            private static bool ContainsUnsupportedScrollBitmapControl(
                Control control)
            {
                if (control == null)
                    return true;

                if (control is RichTextBox ||
                    control is WebBrowser ||
                    control is AxHost)
                {
                    return true;
                }

                int i;

                for (i = 0; i < control.Controls.Count; i++)
                {
                    if (ContainsUnsupportedScrollBitmapControl(
                            control.Controls[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool CaptureRenderedItemsIntoScrollBitmap(
                Bitmap bitmap,
                ArrayList records,
                Rectangle viewport,
                int currentOffset,
                long logicalStart)
            {
                int i;
                Graphics targetGraphics = null;

                try
                {
                    for (i = 0; i < records.Count; i++)
                    {
                        RenderedItemRecord record =
                            records[i] as RenderedItemRecord;
                        Control control = record == null
                            ? null
                            : record.Control;

                        if (control == null ||
                            control.IsDisposed ||
                            !control.Visible ||
                            control.Width <= 0 ||
                            control.Height <= 0)
                        {
                            continue;
                        }

                        long targetLeft;
                        long targetTop;

                        if (_orientation == Orientation.Vertical)
                        {
                            long logicalTop = (long)control.Top -
                                viewport.Top + currentOffset;
                            targetLeft = (long)control.Left -
                                viewport.Left;
                            targetTop = logicalTop - logicalStart;
                        }
                        else
                        {
                            long logicalLeft = (long)control.Left -
                                viewport.Left + currentOffset;
                            targetLeft = logicalLeft - logicalStart;
                            targetTop = (long)control.Top -
                                viewport.Top;
                        }

                        long targetRight = targetLeft + control.Width;
                        long targetBottom = targetTop + control.Height;
                        long clippedLeft = Math.Max(0L, targetLeft);
                        long clippedTop = Math.Max(0L, targetTop);
                        long clippedRight = Math.Min(
                            (long)bitmap.Width,
                            targetRight);
                        long clippedBottom = Math.Min(
                            (long)bitmap.Height,
                            targetBottom);

                        if (clippedRight <= clippedLeft ||
                            clippedBottom <= clippedTop)
                        {
                            continue;
                        }

                        Rectangle clipped = new Rectangle(
                            (int)clippedLeft,
                            (int)clippedTop,
                            (int)(clippedRight - clippedLeft),
                            (int)(clippedBottom - clippedTop));

                        if (targetLeft >= 0L &&
                            targetTop >= 0L &&
                            targetRight <= bitmap.Width &&
                            targetBottom <= bitmap.Height)
                        {
                            control.DrawToBitmap(
                                bitmap,
                                new Rectangle(
                                    (int)targetLeft,
                                    (int)targetTop,
                                    control.Width,
                                    control.Height));
                            continue;
                        }

                        long bytesPerControlColumn =
                            (long)control.Height * 4L;

                        if (bytesPerControlColumn <= 0L ||
                            control.Width >
                                ScrollBitmapCacheMaximumBytes /
                                    bytesPerControlColumn)
                        {
                            return false;
                        }

                        // DrawToBitmap clipping is inconsistent across .NET 2
                        // implementations. Capture the complete item once, then
                        // copy only its visible intersection into the cache so a
                        // tall/wide row can never become an unexplained blank.
                        using (Bitmap itemBitmap = new Bitmap(
                            control.Width,
                            control.Height))
                        {
                            control.DrawToBitmap(
                                itemBitmap,
                                new Rectangle(
                                    Point.Empty,
                                    itemBitmap.Size));
                            Rectangle source = new Rectangle(
                                (int)(clippedLeft - targetLeft),
                                (int)(clippedTop - targetTop),
                                clipped.Width,
                                clipped.Height);

                            if (targetGraphics == null)
                                targetGraphics = Graphics.FromImage(bitmap);

                            targetGraphics.DrawImage(
                                itemBitmap,
                                clipped,
                                source,
                                GraphicsUnit.Pixel);
                        }
                    }
                }
                finally
                {
                    if (targetGraphics != null)
                        targetGraphics.Dispose();
                }

                return true;
            }

            private bool TryPublishScrollBitmapFrame(int logicalOffset)
            {
                if (!_scrollBitmapCacheActive ||
                    _scrollBitmapSurface == null ||
                    _scrollBitmapSurface.IsDisposed ||
                    !IsScrollBitmapSnapshotCurrent() ||
                    !ScrollBitmapCacheContains(logicalOffset))
                {
                    return false;
                }

                _scrollBitmapLogicalOffset = logicalOffset;
#if !WINFORMSXAML_PACKAGE
                _scrollBitmapFrameCount++;
#endif
                _scrollBitmapSurface.Invalidate();
                _scrollBitmapSurface.Update();

                ScrollBarControl bar = _themedScrollBar;

                if (bar != null &&
                    !bar.IsDisposed &&
                    bar.IsHandleCreated &&
                    bar.Visible)
                {
                    bar.Update();
                }

                return true;
            }

            private bool IsScrollBitmapSnapshotCurrent()
            {
                return _scrollBitmapCacheActive &&
                    !ContainsFocus &&
                    Visible &&
                    IsHandleCreated &&
                    Parent != null &&
                    Parent.IsHandleCreated &&
                    _orientation == _scrollBitmapOrientation &&
                    GetMaximumLogicalScrollOffset() ==
                        _scrollBitmapMaximum &&
                    ClientSize == _scrollBitmapOwnerClientSize &&
                    GetItemsViewportRectangle() == _scrollBitmapViewport &&
                    Object.ReferenceEquals(
                        RenderedItems,
                        _scrollBitmapRenderedItems) &&
                    RenderedItemPublicationRevision ==
                        _scrollBitmapPublicationRevision &&
                    RefreshGeneration ==
                        _scrollBitmapRefreshGeneration;
            }

            private bool ScrollBitmapCacheContains(int logicalOffset)
            {
                int viewportAxis = _orientation == Orientation.Vertical
                    ? _scrollBitmapViewport.Height
                    : _scrollBitmapViewport.Width;
                long end = (long)logicalOffset + viewportAxis;

                return _scrollBitmapCacheActive &&
                    logicalOffset >= _scrollBitmapLogicalStart &&
                    end <= _scrollBitmapLogicalEnd;
            }

            private void PaintScrollBitmapFrame(Graphics graphics)
            {
                if (graphics == null ||
                    !_scrollBitmapCacheActive ||
                    _scrollBitmapCache == null ||
                    _scrollBitmapSurface == null)
                {
                    return;
                }

                int sourceAxis = (int)Math.Max(
                    0L,
                    (long)_scrollBitmapLogicalOffset -
                        _scrollBitmapLogicalStart);
                Rectangle source = _orientation == Orientation.Vertical
                    ? new Rectangle(
                        0,
                        sourceAxis,
                        Math.Min(
                            _scrollBitmapSurface.ClientSize.Width,
                            _scrollBitmapCache.Width),
                        Math.Min(
                            _scrollBitmapSurface.ClientSize.Height,
                            _scrollBitmapCache.Height - sourceAxis))
                    : new Rectangle(
                        sourceAxis,
                        0,
                        Math.Min(
                            _scrollBitmapSurface.ClientSize.Width,
                            _scrollBitmapCache.Width - sourceAxis),
                        Math.Min(
                            _scrollBitmapSurface.ClientSize.Height,
                            _scrollBitmapCache.Height));

                if (source.Width <= 0 || source.Height <= 0)
                    return;

                // Source and destination are always pixel-identical. Use the
                // unscaled GDI+ path so every timer frame is a clipped bitmap
                // copy rather than a general image-resampling operation.
                graphics.DrawImageUnscaled(
                    _scrollBitmapCache,
                    -source.X,
                    -source.Y);
            }

            private void EnsureScrollBitmapSurface()
            {
                if (_scrollBitmapSurface != null &&
                    !_scrollBitmapSurface.IsDisposed)
                {
                    return;
                }

                _scrollBitmapSurface =
                    new HostedScrollBitmapSurface(this);
                _scrollBitmapSurface.Visible = false;
            }

            private void PositionScrollBitmapSurface()
            {
                if (_scrollBitmapSurface == null ||
                    _scrollBitmapSurface.IsDisposed ||
                    Parent == null ||
                    !Parent.IsHandleCreated ||
                    !IsHandleCreated)
                {
                    return;
                }

                Point origin = Parent.PointToClient(
                    PointToScreen(_scrollBitmapViewport.Location));
                Rectangle bounds = new Rectangle(
                    origin,
                    _scrollBitmapViewport.Size);

                if (_scrollBitmapSurface.Bounds != bounds)
                    _scrollBitmapSurface.Bounds = bounds;

                if (!_scrollBitmapSurface.IsHandleCreated)
                {
                    if (_scrollBitmapSurface.Handle == IntPtr.Zero)
                        return;
                }
            }

            private void BringScrollBitmapSurfaceToFront()
            {
                if (_scrollBitmapSurface == null ||
                    _scrollBitmapSurface.IsDisposed ||
                    !_scrollBitmapSurface.IsHandleCreated ||
                    !_scrollBitmapSurface.Visible)
                {
                    return;
                }

                SetWindowPos(
                    _scrollBitmapSurface.Handle,
                    IntPtr.Zero,
                    0,
                    0,
                    0,
                    0,
                    WindowPositionNoMove |
                    WindowPositionNoSize |
                    WindowPositionNoActivate);
            }

            private void CommitScrollBitmapCache()
            {
                if (!_scrollBitmapCacheActive ||
                    _scrollBitmapCacheCommitting)
                {
                    return;
                }

                int finalOffset = _scrollBitmapLogicalOffset;
                _scrollBitmapCacheCommitting = true;

                try
                {
                    // Stop intercepting logical-offset writes, but keep the
                    // final cached frame covering the viewport until the one
                    // real content move has synchronously painted underneath
                    // it. Hiding first exposes an avoidable blank/stale frame.
                    _scrollBitmapCacheActive = false;

                    if (AutoScroll &&
                        !IsDisposed &&
                        !Disposing)
                    {
                        bool previousFrame =
                            _applyingSmoothScrollFrame;
                        _applyingSmoothScrollFrame = true;

                        try
                        {
                            SetLogicalScrollOffset(finalOffset);
                        }
                        finally
                        {
                            _applyingSmoothScrollFrame = previousFrame;
                        }
                    }

#if !WINFORMSXAML_PACKAGE
                    _scrollBitmapCommitCount++;
#endif
                }
                finally
                {
                    if (_scrollBitmapSurface != null &&
                        !_scrollBitmapSurface.IsDisposed)
                    {
                        _scrollBitmapSurface.Visible = false;
                    }

                    DisposeScrollBitmapImage();
                    _scrollBitmapCacheCommitting = false;
                }
            }

            private void DisposeScrollBitmapImage()
            {
                Bitmap bitmap = _scrollBitmapCache;
                _scrollBitmapCache = null;
                _scrollBitmapRenderedItems = null;
                _scrollBitmapOwnerClientSize = Size.Empty;
                _scrollBitmapViewport = Rectangle.Empty;

                if (bitmap != null)
                    bitmap.Dispose();
            }

            private void DisposeScrollBitmapCache()
            {
                _scrollBitmapCacheActive = false;
                DisposeScrollBitmapImage();
                _scrollBitmapEligibilityRecords = null;

                HostedScrollBitmapSurface surface =
                    _scrollBitmapSurface;
                _scrollBitmapSurface = null;

                if (surface != null && !surface.IsDisposed)
                    surface.Dispose();
            }

#if !WINFORMSXAML_PACKAGE
            internal bool ScrollBitmapCacheActiveForTest
            {
                get { return _scrollBitmapCacheActive; }
            }

            internal long ScrollBitmapCaptureCountForTest
            {
                get { return _scrollBitmapCaptureCount; }
            }

            internal long ScrollBitmapFrameCountForTest
            {
                get { return _scrollBitmapFrameCount; }
            }

            internal long ScrollBitmapCommitCountForTest
            {
                get { return _scrollBitmapCommitCount; }
            }
#endif
        }
    }
}
