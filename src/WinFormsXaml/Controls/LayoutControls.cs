using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        // ============================================================
        // CUSTOM HOSTS
        // ============================================================

        private sealed class GridHost : Panel
        {
            internal XamlRuntime Runtime;

            internal List<GridDefinition> Rows;
            internal List<GridDefinition> Columns;

            public GridHost()
            {
                Rows =
                    new List<GridDefinition>();

                Columns =
                    new List<GridDefinition>();
            }

            protected override void OnLayout(
                LayoutEventArgs e)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    base.OnLayout(e);
                    return;
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    base.OnLayout(e);

                    if (Runtime != null)
                    {
                        Runtime.LayoutGrid(
                            this);
                    }
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            public override Size GetPreferredSize(
                Size proposedSize)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    return base.GetPreferredSize(
                        proposedSize);
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    return runtime.GetPreferredGridSize(
                        this,
                        proposedSize);
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Runtime = null;

                base.Dispose(disposing);
            }
        }

        private sealed class StackHost : Panel
        {
            internal XamlRuntime Runtime;

            internal Orientation StackOrientation;

            internal int StackSpacing;

            public StackHost()
            {
                StackOrientation =
                    Orientation.Vertical;

                StackSpacing = 0;
            }

            protected override void OnLayout(
                LayoutEventArgs e)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    base.OnLayout(e);
                    return;
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    base.OnLayout(e);

                    if (Runtime != null)
                    {
                        Runtime.LayoutStack(
                            this);
                    }
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            public override Size GetPreferredSize(
                Size proposedSize)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    return base.GetPreferredSize(
                        proposedSize);
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    return runtime.GetPreferredStackSize(
                        this,
                        proposedSize);
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Runtime = null;

                base.Dispose(disposing);
            }
        }

        private sealed class DockHost : Panel
        {
            internal XamlRuntime Runtime;

            internal bool LastChildFill;

            public DockHost()
            {
                LastChildFill =
                    true;
            }

            protected override void OnLayout(
                LayoutEventArgs e)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    base.OnLayout(e);
                    return;
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    base.OnLayout(e);

                    if (Runtime != null)
                    {
                        Runtime.LayoutDock(
                            this);
                    }
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            public override Size GetPreferredSize(
                Size proposedSize)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    return base.GetPreferredSize(
                        proposedSize);
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    return runtime.GetPreferredDockSize(
                        this,
                        proposedSize);
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Runtime = null;

                base.Dispose(disposing);
            }
        }

        private sealed class CanvasHost : Panel
        {
            internal XamlRuntime Runtime;

            protected override void OnLayout(
                LayoutEventArgs e)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    base.OnLayout(e);
                    return;
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    base.OnLayout(e);

                    if (Runtime != null)
                    {
                        Runtime.LayoutCanvas(
                            this);
                    }
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            public override Size GetPreferredSize(
                Size proposedSize)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    return base.GetPreferredSize(
                        proposedSize);
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    return runtime.GetPreferredCanvasSize(
                        this,
                        proposedSize);
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Runtime = null;

                base.Dispose(disposing);
            }
        }

        private class SingleHost : Panel
        {
            internal XamlRuntime Runtime;

            protected override void OnLayout(
                LayoutEventArgs e)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    base.OnLayout(e);
                    return;
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    base.OnLayout(e);

                    if (Runtime != null)
                    {
                        Runtime.LayoutSingle(
                            this);
                    }
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            public override Size GetPreferredSize(
                Size proposedSize)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    return base.GetPreferredSize(
                        proposedSize);
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    return runtime.GetPreferredSingleSize(
                        this,
                        proposedSize);
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    Runtime = null;

                base.Dispose(disposing);
            }
        }

        private sealed class ScrollHost : SingleHost
        {
            public ScrollHost()
            {
                AutoScroll =
                    true;
            }
        }

        private sealed class BorderHost : SingleHost
        {
            public Color BorderColor;
            public Padding BorderThickness;

            private Pen _cachedBorderPen;
            private Color _cachedBorderPenColor;

            public BorderHost()
            {
                BorderColor =
                    SystemColors.ControlDark;

                BorderThickness =
                    new Padding(1);

                _cachedBorderPen = null;
                _cachedBorderPenColor = Color.Empty;
            }

            private Pen GetBorderPen()
            {
                if (_cachedBorderPen == null ||
                    _cachedBorderPenColor.ToArgb() != BorderColor.ToArgb())
                {
                    if (_cachedBorderPen != null)
                        _cachedBorderPen.Dispose();

                    _cachedBorderPen = new Pen(BorderColor);
                    _cachedBorderPenColor = BorderColor;
                }

                return _cachedBorderPen;
            }

            protected override void OnPaint(
                PaintEventArgs e)
            {
                base.OnPaint(e);

                if (BorderThickness.Left <= 0 &&
                    BorderThickness.Top <= 0 &&
                    BorderThickness.Right <= 0 &&
                    BorderThickness.Bottom <= 0)
                {
                    return;
                }

                Pen pen = GetBorderPen();
                int i;

                int max =
                    Math.Max(
                        Math.Max(
                            BorderThickness.Left,
                            BorderThickness.Right),
                        Math.Max(
                            BorderThickness.Top,
                            BorderThickness.Bottom));

                for (i = 0;
                     i < max;
                     i++)
                {
                    Rectangle rect =
                        new Rectangle(
                            i,
                            i,
                            Math.Max(
                                0,
                                ClientSize.Width -
                                1 -
                                (i * 2)),
                            Math.Max(
                                0,
                                ClientSize.Height -
                                1 -
                                (i * 2)));

                    e.Graphics.DrawRectangle(
                        pen,
                        rect);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _cachedBorderPen != null)
                {
                    _cachedBorderPen.Dispose();
                    _cachedBorderPen = null;
                }

                base.Dispose(disposing);
            }
        }

        /// <summary>Specifies the main axis used by a FlexPanel.</summary>
        public enum FlexDirection
        {
            /// <summary>Arranges children horizontally.</summary>
            Row,

            /// <summary>Arranges children vertically.</summary>
            Column
        }

        /// <summary>
        /// Specifies how unused main-axis space is distributed by a FlexPanel.
        /// </summary>
        public enum FlexJustifyContent
        {
            /// <summary>Places children at the start of the main axis.</summary>
            Start,

            /// <summary>Centers the complete child group.</summary>
            Center,

            /// <summary>Places children at the end of the main axis.</summary>
            End,

            /// <summary>Distributes unused space between adjacent children.</summary>
            SpaceBetween,

            /// <summary>Distributes unused space around every child.</summary>
            SpaceAround
        }

        /// <summary>
        /// Specifies how children are aligned on a FlexPanel's cross axis.
        /// </summary>
        public enum FlexAlignItems
        {
            /// <summary>Aligns children to the cross-axis start.</summary>
            Start,

            /// <summary>Centers children on the cross axis.</summary>
            Center,

            /// <summary>Aligns children to the cross-axis end.</summary>
            End,

            /// <summary>Stretches children without an explicit cross-axis size.</summary>
            Stretch
        }

        /// <summary>
        /// A native Panel that arranges ordinary WinForms controls using a
        /// one-dimensional, optionally wrapping flex layout.
        /// </summary>
        public sealed class FlexPanel : Panel
        {
            internal XamlRuntime Runtime;
            internal FlexLayoutPlan LayoutScratchPlan;
            internal bool LayoutScratchInUse;

            private FlexDirection _direction;
            private FlexJustifyContent _justifyContent;
            private FlexAlignItems _alignItems;
            private bool _wrap;
            private int _gap;
#if !WINFORMSXAML_PACKAGE
            private long _layoutPlanAllocationCount;
            private long _layoutArrayAllocationCount;
            private long _layoutScratchReuseCount;
#endif

            /// <summary>Creates a horizontal, non-wrapping flex panel.</summary>
            public FlexPanel()
            {
                _direction = FlexDirection.Row;
                _justifyContent = FlexJustifyContent.Start;
                _alignItems = FlexAlignItems.Stretch;
                _wrap = false;
                _gap = 0;
            }

            /// <summary>Gets or sets the main layout axis.</summary>
            public FlexDirection Direction
            {
                get { return _direction; }
                set
                {
                    if (_direction == value)
                        return;

                    _direction = value;
                    PerformLayout();
                }
            }

            /// <summary>Gets or sets main-axis free-space distribution.</summary>
            public FlexJustifyContent JustifyContent
            {
                get { return _justifyContent; }
                set
                {
                    if (_justifyContent == value)
                        return;

                    _justifyContent = value;
                    PerformLayout();
                }
            }

            /// <summary>Gets or sets cross-axis child alignment.</summary>
            public FlexAlignItems AlignItems
            {
                get { return _alignItems; }
                set
                {
                    if (_alignItems == value)
                        return;

                    _alignItems = value;
                    PerformLayout();
                }
            }

            /// <summary>Gets or sets whether children continue on additional lines.</summary>
            public bool Wrap
            {
                get { return _wrap; }
                set
                {
                    if (_wrap == value)
                        return;

                    _wrap = value;
                    PerformLayout();
                }
            }

            /// <summary>Gets or sets non-negative pixels between children and lines.</summary>
            public int Gap
            {
                get { return _gap; }
                set
                {
                    int normalized = Math.Max(0, value);

                    if (_gap == normalized)
                        return;

                    _gap = normalized;
                    PerformLayout();
                }
            }

#if !WINFORMSXAML_PACKAGE
            internal long LayoutPlanAllocationCountForTest
            {
                get { return _layoutPlanAllocationCount; }
            }

            internal long LayoutArrayAllocationCountForTest
            {
                get { return _layoutArrayAllocationCount; }
            }

            internal long LayoutScratchReuseCountForTest
            {
                get { return _layoutScratchReuseCount; }
            }

            internal object LayoutScratchIdentityForTest
            {
                get { return LayoutScratchPlan; }
            }

            internal void ResetLayoutScratchDiagnosticsForTest()
            {
                _layoutPlanAllocationCount = 0L;
                _layoutArrayAllocationCount = 0L;
                _layoutScratchReuseCount = 0L;
            }

            internal void RecordLayoutStorageForTest(
                bool reusedPlan,
                int arrayAllocationCount)
            {
                if (reusedPlan)
                {
                    _layoutScratchReuseCount++;
                }
                else
                {
                    _layoutPlanAllocationCount++;
                }

                _layoutArrayAllocationCount +=
                    arrayAllocationCount;
            }
#endif

            /// <summary>Runs the owning runtime's flex layout pass.</summary>
            protected override void OnLayout(
                LayoutEventArgs e)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    base.OnLayout(e);
                    return;
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    base.OnLayout(e);

                    if (Runtime != null)
                        Runtime.LayoutFlexPanel(this);
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            /// <summary>Calculates the preferred size of the flex arrangement.</summary>
            public override Size GetPreferredSize(
                Size proposedSize)
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null)
                {
                    return base.GetPreferredSize(
                        proposedSize);
                }

                runtime.BeginPreferredSizePass();

                try
                {
                    return runtime.GetPreferredFlexPanelSize(
                        this,
                        proposedSize);
                }
                finally
                {
                    runtime.EndPreferredSizePass();
                }
            }

            /// <summary>Releases the link to the owning runtime.</summary>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Runtime = null;
                    LayoutScratchPlan = null;
                    LayoutScratchInUse = false;
                }

                base.Dispose(disposing);
            }
        }
    }
}
