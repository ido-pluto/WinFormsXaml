using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public partial class TabView
    {
        private Rectangle[] _headerBounds;
        private int[] _headerWidths;
        private Rectangle _headerViewport;
        private Rectangle _contentBounds;
        private Rectangle _contentDisplayBounds;
        private int _headerExtent;
        private int _headerHeight;
        private int _headerScrollOffset;
        private int _layoutSelectedIndex;
        private bool _tabLayoutDirty;
        private bool _headerMetricsDirty;
        private bool _revealSelectedHeader;
        private SolidBrush _tabBackgroundBrush;
        private SolidBrush _selectedTabBackgroundBrush;
        private SolidBrush _tabBorderPaintBrush;
        private SolidBrush _contentBackgroundBrush;
        private SolidBrush _contentBorderPaintBrush;

#if !WINFORMSXAML_PACKAGE
        internal int PaintResourceCountForTest
        {
            get
            {
                int count = 0;

                if (_tabBackgroundBrush != null)
                    count++;
                if (_selectedTabBackgroundBrush != null)
                    count++;
                if (_tabBorderPaintBrush != null)
                    count++;
                if (_contentBackgroundBrush != null)
                    count++;
                if (_contentBorderPaintBrush != null)
                    count++;

                return count;
            }
        }
#endif

        internal Rectangle GetHeaderBounds(TabViewItem item)
        {
            if (_usesNativeTabs)
                return GetNativeHeaderBounds(item);

            EnsureTabLayout();
            int index = IndexOfItem(item);

            if (index < 0 ||
                _headerBounds == null ||
                index >= _headerBounds.Length)
            {
                return Rectangle.Empty;
            }

            return _headerBounds[index];
        }

        internal Rectangle ContentDisplayBounds
        {
            get
            {
                if (_usesNativeTabs)
                    return GetNativeContentDisplayBounds();

                EnsureTabLayout();
                return _contentDisplayBounds;
            }
        }

        internal void InvalidateTabLayout()
        {
            _tabLayoutDirty = true;
            _revealSelectedHeader = true;

            if (_tabItems == null)
                return;

            PerformLayout();
            Invalidate();
        }

        internal void InvalidateTabMetrics()
        {
            _headerMetricsDirty = true;
            InvalidateTabLayout();
        }

        /// <summary>
        /// Measures headers and content, then places every page in the shared
        /// content rectangle.
        /// </summary>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);

            if (_tabItems == null)
                return;

            if (_usesNativeTabs)
            {
                UpdateNativeTabBounds();
                Rectangle nativeContent = GetNativeContentDisplayBounds();
                int nativeIndex;

                for (nativeIndex = 0;
                     nativeIndex < TabItems.Count;
                     nativeIndex++)
                {
                    TabViewItem nativeItem = TabItems[nativeIndex];

                    if (nativeItem.Bounds != nativeContent)
                        nativeItem.Bounds = nativeContent;

                    if (Object.ReferenceEquals(nativeItem, _selectedItem))
                        nativeItem.BringToFront();
                }

                return;
            }

            EnsureTabLayout();

            int i;

            for (i = 0; i < TabItems.Count; i++)
            {
                TabViewItem item = TabItems[i];

                if (item.Bounds != _contentDisplayBounds)
                    item.Bounds = _contentDisplayBounds;
            }
        }

        /// <summary>
        /// Returns a preferred size large enough for the header strip and the
        /// largest tab content root.
        /// </summary>
        public override Size GetPreferredSize(Size proposedSize)
        {
            EnsureTabLayout();
            int contentWidth = 0;
            int contentHeight = 0;
            int i;

            for (i = 0; i < TabItems.Count; i++)
            {
                TabViewItem item = TabItems[i];

                if (!item.RequestedVisible)
                    continue;

                Size preferred = item.GetPreferredSize(proposedSize);
                contentWidth = Math.Max(contentWidth, preferred.Width);
                contentHeight = Math.Max(contentHeight, preferred.Height);
            }

            contentWidth +=
                _contentBorderThickness.Horizontal +
                _contentPadding.Horizontal;
            contentHeight +=
                _contentBorderThickness.Vertical +
                _contentPadding.Vertical;

            int width = Math.Max(_headerExtent, contentWidth) + Padding.Horizontal;
            int height = _headerViewport.Height + contentHeight + Padding.Vertical;

            return new Size(
                Math.Max(0, width),
                Math.Max(0, height));
        }

        /// <summary>Paints the content frame and tab headers.</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (e == null || e.Graphics == null)
            {
                base.OnPaint(e);
                return;
            }

            if (!_usesNativeTabs)
            {
                EnsureTabLayout();
                PaintContentFrame(e.Graphics);
                PaintHeaders(e.Graphics);
            }

            base.OnPaint(e);
        }

        /// <summary>Selects the header under the primary mouse button.</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!_usesNativeTabs &&
                e != null &&
                e.Button == MouseButtons.Left)
            {
                Focus();
                TabViewItem item = HitTestHeader(new Point(e.X, e.Y));

                if (item != null && item.Enabled && Enabled)
                    SetSelection(item, true);
            }

            base.OnMouseDown(e);
        }

        /// <summary>
        /// Scrolls an overflowing header strip without changing logical item
        /// order or the selected page.
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_usesNativeTabs)
            {
                base.OnMouseWheel(e);
                return;
            }

            EnsureTabLayout();

            if (e != null &&
                _headerExtent > _headerViewport.Width &&
                _headerViewport.Contains(e.X, e.Y))
            {
                int lines = SystemInformation.MouseWheelScrollLines;

                if (lines <= 0)
                    lines = 3;

                int delta = Math.Max(24, Font.Height * lines);
                int offset = _headerScrollOffset +
                    (e.Delta < 0 ? delta : -delta);
                int maximum = Math.Max(0, _headerExtent - _headerViewport.Width);

                if (offset < 0)
                    offset = 0;

                if (offset > maximum)
                    offset = maximum;

                if (_headerScrollOffset != offset)
                {
                    _headerScrollOffset = offset;
                    _tabLayoutDirty = true;
                    _revealSelectedHeader = false;
                    Invalidate(_headerViewport);
                }

                return;
            }

            base.OnMouseWheel(e);
        }

        /// <summary>Re-measures headers after the font changes.</summary>
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            InvalidateTabMetrics();
        }

        /// <summary>Mirrors header geometry after direction changes.</summary>
        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            SynchronizeNativeDirection();
            UpdateTabRenderingMode();
            InvalidateTabLayout();
        }

        /// <summary>Recomputes the inner tab viewport after Padding changes.</summary>
        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            UpdateNativeTabBounds();
            InvalidateTabLayout();
        }

        /// <summary>Recomputes header and content rectangles after resizing.</summary>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateNativeTabBounds();
            InvalidateTabLayout();
        }

        /// <summary>Shows the keyboard focus cue on the selected header.</summary>
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate(_headerViewport);
        }

        /// <summary>Removes the keyboard focus cue.</summary>
        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate(_headerViewport);
        }

        private void EnsureTabLayout()
        {
            if (_tabItems == null)
                return;

            int count = TabItems.Count;
            int selectedIndex = SelectedIndex;

            if (_headerBounds == null || _headerBounds.Length != count)
            {
                _headerBounds = new Rectangle[count];
                _headerWidths = new int[count];
                _headerMetricsDirty = true;
                _tabLayoutDirty = true;
                _revealSelectedHeader = true;
            }

            if (_layoutSelectedIndex != selectedIndex)
            {
                _layoutSelectedIndex = selectedIndex;
                _tabLayoutDirty = true;
                _revealSelectedHeader = true;
            }

            if (!_tabLayoutDirty)
                return;

            _tabLayoutDirty = false;
            Rectangle outer = DeflateRectangle(ClientRectangle, Padding);

            if (_headerMetricsDirty)
            {
                MeasureHeaders();
                _headerMetricsDirty = false;
            }

            _headerViewport = new Rectangle(
                outer.Left,
                outer.Top,
                outer.Width,
                Math.Min(outer.Height, _headerHeight));

            _contentBounds = new Rectangle(
                outer.Left,
                outer.Top + _headerViewport.Height,
                outer.Width,
                Math.Max(0, outer.Height - _headerViewport.Height));

            Rectangle contentInsideBorder = DeflateRectangle(
                _contentBounds,
                _contentBorderThickness);
            _contentDisplayBounds = DeflateRectangle(
                contentInsideBorder,
                _contentPadding);

            if (_revealSelectedHeader)
            {
                ClampAndRevealSelectedHeader();
                _revealSelectedHeader = false;
            }
            else
            {
                ClampHeaderScrollOffset();
            }

            PositionHeaders();
        }

        private void MeasureHeaders()
        {
            int headerHeight = 0;
            int extent = 0;
            int visibleCount = 0;
            TextFormatFlags flags =
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine;
            int i;

            for (i = 0; i < TabItems.Count; i++)
            {
                TabViewItem item = TabItems[i];

                if (!item.RequestedVisible)
                {
                    _headerWidths[i] = 0;
                    _headerBounds[i] = Rectangle.Empty;
                    continue;
                }

                string text = item.Header == null
                    ? String.Empty
                    : item.Header;
                Size textSize = TextRenderer.MeasureText(
                    text,
                    Font,
                    new Size(Int32.MaxValue, Int32.MaxValue),
                    flags);
                int width = Math.Max(
                    1,
                    textSize.Width +
                    _tabPadding.Horizontal +
                    _tabBorderThickness.Horizontal);
                int height = Math.Max(
                    1,
                    textSize.Height +
                    _tabPadding.Vertical +
                    _tabBorderThickness.Vertical);

                _headerWidths[i] = width;
                headerHeight = Math.Max(headerHeight, height);

                if (visibleCount != 0)
                    extent += _headerSpacing;

                extent += width;
                visibleCount++;
            }

            _headerExtent = extent;
            _headerHeight = headerHeight;
        }

        private void ClampAndRevealSelectedHeader()
        {
            int maximum = ClampHeaderScrollOffset();

            int selectedIndex = SelectedIndex;

            if (selectedIndex < 0 ||
                selectedIndex >= _headerWidths.Length ||
                _headerWidths[selectedIndex] == 0)
            {
                return;
            }

            int start = GetLogicalHeaderStart(selectedIndex);
            int end = start + _headerWidths[selectedIndex];

            if (start < _headerScrollOffset)
                _headerScrollOffset = start;
            else if (end > _headerScrollOffset + _headerViewport.Width)
                _headerScrollOffset = end - _headerViewport.Width;

            if (_headerScrollOffset < 0)
                _headerScrollOffset = 0;

            if (_headerScrollOffset > maximum)
                _headerScrollOffset = maximum;
        }

        private int ClampHeaderScrollOffset()
        {
            int maximum = Math.Max(0, _headerExtent - _headerViewport.Width);

            if (_headerScrollOffset > maximum)
                _headerScrollOffset = maximum;

            if (_headerScrollOffset < 0)
                _headerScrollOffset = 0;

            return maximum;
        }

        private int GetLogicalHeaderStart(int targetIndex)
        {
            int position = 0;
            int visibleCount = 0;
            int i;

            for (i = 0; i < targetIndex; i++)
            {
                if (_headerWidths[i] == 0)
                    continue;

                if (visibleCount != 0)
                    position += _headerSpacing;

                position += _headerWidths[i];
                visibleCount++;
            }

            if (_headerWidths[targetIndex] != 0 && visibleCount != 0)
                position += _headerSpacing;

            return position;
        }

        private void PositionHeaders()
        {
            int logicalPosition = 0;
            int visibleCount = 0;
            bool rtl = RightToLeft == RightToLeft.Yes;
            int i;

            for (i = 0; i < TabItems.Count; i++)
            {
                int width = _headerWidths[i];

                if (width == 0)
                {
                    _headerBounds[i] = Rectangle.Empty;
                    continue;
                }

                if (visibleCount != 0)
                    logicalPosition += _headerSpacing;

                int x;

                if (rtl)
                {
                    x = _headerViewport.Right -
                        (logicalPosition - _headerScrollOffset) -
                        width;
                }
                else
                {
                    x = _headerViewport.Left +
                        logicalPosition -
                        _headerScrollOffset;
                }

                _headerBounds[i] = new Rectangle(
                    x,
                    _headerViewport.Top,
                    width,
                    _headerViewport.Height);
                logicalPosition += width;
                visibleCount++;
            }
        }

        private void PaintContentFrame(Graphics graphics)
        {
            if (_contentBounds.Width <= 0 || _contentBounds.Height <= 0)
                return;

            graphics.FillRectangle(
                GetPaintBrush(ref _contentBackgroundBrush, _contentBackground),
                _contentBounds);

            PaintBorder(
                graphics,
                _contentBounds,
                GetPaintBrush(
                    ref _contentBorderPaintBrush,
                    _contentBorderBrush),
                _contentBorderThickness);
        }

        private void PaintHeaders(Graphics graphics)
        {
            if (_headerViewport.Width <= 0 || _headerViewport.Height <= 0)
                return;

            GraphicsState state = graphics.Save();

            try
            {
                graphics.SetClip(_headerViewport, CombineMode.Intersect);
                int i;

                for (i = 0; i < TabItems.Count; i++)
                    PaintHeader(graphics, i);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void PaintHeader(Graphics graphics, int index)
        {
            Rectangle bounds = _headerBounds[index];

            if (bounds.IsEmpty || !bounds.IntersectsWith(_headerViewport))
                return;

            TabViewItem item = TabItems[index];
            bool selected = Object.ReferenceEquals(item, _selectedItem);
            Color background = selected
                ? _selectedTabBackground
                : _tabBackground;
            Color foreground = selected
                ? _selectedTabForeground
                : _tabForeground;

            SolidBrush backgroundBrush = selected
                ? GetPaintBrush(
                    ref _selectedTabBackgroundBrush,
                    background)
                : GetPaintBrush(ref _tabBackgroundBrush, background);
            int radius = selected && _selectedTabCornerRadius >= 0
                ? _selectedTabCornerRadius
                : _tabCornerRadius;
            PaintHeaderSurface(
                graphics,
                bounds,
                backgroundBrush,
                GetPaintBrush(ref _tabBorderPaintBrush, _tabBorderBrush),
                _tabBorderThickness,
                radius);

            Rectangle textBounds = DeflateRectangle(
                DeflateRectangle(bounds, _tabBorderThickness),
                _tabPadding);
            TextFormatFlags flags =
                TextFormatFlags.SingleLine |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix;

            if (RightToLeft == RightToLeft.Yes)
                flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
            else
                flags |= TextFormatFlags.Left;

            TextRenderer.DrawText(
                graphics,
                item.Header == null ? String.Empty : item.Header,
                Font,
                textBounds,
                item.Enabled && Enabled ? foreground : SystemColors.GrayText,
                flags);

            if (selected && Focused && ShowFocusCues)
            {
                Rectangle focusBounds = bounds;
                focusBounds.Inflate(-3, -3);

                if (focusBounds.Width > 0 && focusBounds.Height > 0)
                {
                    if (radius <= 0)
                    {
                        ControlPaint.DrawFocusRectangle(
                            graphics,
                            focusBounds,
                            foreground,
                            background);
                    }
                    else
                    {
                        using (GraphicsPath focusPath =
                            CreateRoundedRectanglePath(
                                focusBounds,
                                Math.Max(0, radius - 3)))
                        using (Pen focusPen = new Pen(foreground))
                        {
                            focusPen.DashStyle = DashStyle.Dot;
                            graphics.DrawPath(focusPen, focusPath);
                        }
                    }
                }
            }
        }

        private TabViewItem HitTestHeader(Point point)
        {
            EnsureTabLayout();

            if (!_headerViewport.Contains(point))
                return null;

            int i;

            for (i = TabItems.Count - 1; i >= 0; i--)
            {
                if (!_headerBounds[i].IsEmpty &&
                    _headerBounds[i].Contains(point))
                {
                    TabViewItem item = TabItems[i];
                    bool selected = Object.ReferenceEquals(item, _selectedItem);
                    int radius = selected && _selectedTabCornerRadius >= 0
                        ? _selectedTabCornerRadius
                        : _tabCornerRadius;

                    if (radius <= 0)
                        return item;

                    using (GraphicsPath path = CreateRoundedRectanglePath(
                        _headerBounds[i],
                        radius))
                    {
                        if (path.IsVisible(point))
                            return item;
                    }
                }
            }

            return null;
        }

        private static Rectangle DeflateRectangle(
            Rectangle rectangle,
            Padding padding)
        {
            int width = rectangle.Width - padding.Horizontal;
            int height = rectangle.Height - padding.Vertical;

            return new Rectangle(
                rectangle.Left + padding.Left,
                rectangle.Top + padding.Top,
                Math.Max(0, width),
                Math.Max(0, height));
        }

        private static void PaintBorder(
            Graphics graphics,
            Rectangle bounds,
            Brush brush,
            Padding thickness)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            if (thickness.Top > 0)
            {
                graphics.FillRectangle(
                    brush,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    Math.Min(thickness.Top, bounds.Height));
            }

            if (thickness.Bottom > 0)
            {
                int height = Math.Min(thickness.Bottom, bounds.Height);
                graphics.FillRectangle(
                    brush,
                    bounds.Left,
                    bounds.Bottom - height,
                    bounds.Width,
                    height);
            }

            if (thickness.Left > 0)
            {
                graphics.FillRectangle(
                    brush,
                    bounds.Left,
                    bounds.Top,
                    Math.Min(thickness.Left, bounds.Width),
                    bounds.Height);
            }

            if (thickness.Right > 0)
            {
                int width = Math.Min(thickness.Right, bounds.Width);
                graphics.FillRectangle(
                    brush,
                    bounds.Right - width,
                    bounds.Top,
                    width,
                    bounds.Height);
            }
        }

        private static void PaintHeaderSurface(
            Graphics graphics,
            Rectangle bounds,
            Brush background,
            Brush border,
            Padding thickness,
            int radius)
        {
            int clampedRadius = ClampCornerRadius(bounds, radius);

            if (clampedRadius <= 0)
            {
                graphics.FillRectangle(background, bounds);
                PaintBorder(graphics, bounds, border, thickness);
                return;
            }

            using (GraphicsPath outer = CreateRoundedRectanglePath(
                bounds,
                clampedRadius))
            {
                graphics.FillPath(border, outer);
            }

            Rectangle inner = DeflateRectangle(bounds, thickness);

            if (inner.Width <= 0 || inner.Height <= 0)
                return;

            int inset = Math.Max(
                Math.Max(thickness.Left, thickness.Right),
                Math.Max(thickness.Top, thickness.Bottom));
            int innerRadius = Math.Max(0, clampedRadius - inset);

            if (innerRadius <= 0)
            {
                graphics.FillRectangle(background, inner);
                return;
            }

            using (GraphicsPath innerPath = CreateRoundedRectanglePath(
                inner,
                innerRadius))
            {
                graphics.FillPath(background, innerPath);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(
            Rectangle bounds,
            int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int clamped = ClampCornerRadius(bounds, radius);

            if (clamped <= 0)
            {
                path.AddRectangle(bounds);
                path.CloseFigure();
                return path;
            }

            int diameter = clamped * 2;
            int right = bounds.Right - diameter;
            int bottom = bounds.Bottom - diameter;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(right, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(right, bottom, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bottom, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static int ClampCornerRadius(Rectangle bounds, int radius)
        {
            if (radius <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
                return 0;

            return Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
        }

        private static SolidBrush GetPaintBrush(
            ref SolidBrush brush,
            Color color)
        {
            if (brush == null || brush.Color != color)
            {
                if (brush != null)
                    brush.Dispose();

                brush = new SolidBrush(color);
            }

            return brush;
        }

        private void DisposePaintResources()
        {
            DisposeBrush(ref _tabBackgroundBrush);
            DisposeBrush(ref _selectedTabBackgroundBrush);
            DisposeBrush(ref _tabBorderPaintBrush);
            DisposeBrush(ref _contentBackgroundBrush);
            DisposeBrush(ref _contentBorderPaintBrush);
        }

        private static void DisposeBrush(ref SolidBrush brush)
        {
            if (brush == null)
                return;

            brush.Dispose();
            brush = null;
        }
    }
}
