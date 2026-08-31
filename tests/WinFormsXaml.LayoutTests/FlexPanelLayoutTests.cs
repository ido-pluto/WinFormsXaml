using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.LayoutTests
{
    internal static class FlexPanelLayoutTests
    {
        public static void RowAndColumnWrap()
        {
            XamlRuntime rowRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Width='100' Height='80' " +
                    "Direction='Row' Wrap='true' AlignItems='Start' Gap='5'>" +
                    "  <Panel Name='First' Width='40' Height='10' />" +
                    "  <Panel Name='Second' Width='40' Height='10' />" +
                    "  <Panel Name='Third' Width='40' Height='10' />" +
                    "</FlexPanel>");

            try
            {
                PerformLayout(rowRuntime);
                AssertBounds(
                    rowRuntime.Get<Panel>("First"),
                    0,
                    0,
                    40,
                    10,
                    "row-wrap first item");
                AssertBounds(
                    rowRuntime.Get<Panel>("Second"),
                    45,
                    0,
                    40,
                    10,
                    "row-wrap second item");
                AssertBounds(
                    rowRuntime.Get<Panel>("Third"),
                    0,
                    15,
                    40,
                    10,
                    "row-wrap next line");
            }
            finally
            {
                DisposeRuntime(rowRuntime);
            }

            XamlRuntime columnRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Width='80' Height='55' " +
                    "Direction='Column' Wrap='true' AlignItems='Start' Gap='5'>" +
                    "  <Panel Name='First' Width='20' Height='25' />" +
                    "  <Panel Name='Second' Width='20' Height='25' />" +
                    "  <Panel Name='Third' Width='20' Height='25' />" +
                    "</FlexPanel>");

            try
            {
                PerformLayout(columnRuntime);
                AssertBounds(
                    columnRuntime.Get<Panel>("First"),
                    0,
                    0,
                    20,
                    25,
                    "column-wrap first item");
                AssertBounds(
                    columnRuntime.Get<Panel>("Second"),
                    0,
                    30,
                    20,
                    25,
                    "column-wrap second item");
                AssertBounds(
                    columnRuntime.Get<Panel>("Third"),
                    25,
                    0,
                    20,
                    25,
                    "column-wrap next column");
            }
            finally
            {
                DisposeRuntime(columnRuntime);
            }
        }

        public static void ConstrainedPreferredSizeWraps()
        {
            XamlRuntime rowRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Direction='Row' Wrap='true' " +
                    "Padding='5' Gap='5'>" +
                    "  <Panel Width='40' Height='10' />" +
                    "  <Panel Width='40' Height='10' />" +
                    "  <Panel Width='40' Height='10' />" +
                    "</FlexPanel>");

            try
            {
                XamlRuntime.FlexPanel panel =
                    rowRuntime.Get<XamlRuntime.FlexPanel>("Root");

                Size preferred =
                    panel.GetPreferredSize(
                        new Size(100, 200));

                AssertSize(
                    preferred,
                    95,
                    35,
                    "constrained row preferred size");
            }
            finally
            {
                DisposeRuntime(rowRuntime);
            }

            XamlRuntime columnRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Direction='Column' Wrap='true' " +
                    "Padding='5' Gap='5'>" +
                    "  <Panel Width='20' Height='25' />" +
                    "  <Panel Width='20' Height='25' />" +
                    "  <Panel Width='20' Height='25' />" +
                    "</FlexPanel>");

            try
            {
                XamlRuntime.FlexPanel panel =
                    columnRuntime.Get<XamlRuntime.FlexPanel>("Root");

                Size preferred =
                    panel.GetPreferredSize(
                        new Size(200, 75));

                AssertSize(
                    preferred,
                    55,
                    65,
                    "constrained column preferred size");
            }
            finally
            {
                DisposeRuntime(columnRuntime);
            }
        }

        public static void CollapsedAndOversizedItemsDoNotCreateEmptyLines()
        {
            XamlRuntime collapsedRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Width='85' Height='80' " +
                    "Direction='Row' Wrap='true' AlignItems='Start' Gap='5'>" +
                    "  <Panel Name='Collapsed' Width='80' Height='30' " +
                    "Visibility='Collapsed' />" +
                    "  <Panel Name='First' Width='40' Height='10' />" +
                    "  <Panel Name='Second' Width='40' Height='10' />" +
                    "</FlexPanel>");

            try
            {
                PerformLayout(collapsedRuntime);
                AssertBounds(
                    collapsedRuntime.Get<Panel>("Collapsed"),
                    0,
                    0,
                    0,
                    0,
                    "collapsed child bounds");
                AssertBounds(
                    collapsedRuntime.Get<Panel>("First"),
                    0,
                    0,
                    40,
                    10,
                    "collapsed-leading item must not add a gap");
                AssertBounds(
                    collapsedRuntime.Get<Panel>("Second"),
                    45,
                    0,
                    40,
                    10,
                    "collapsed-leading item must not add a line");
            }
            finally
            {
                DisposeRuntime(collapsedRuntime);
            }

            XamlRuntime oversizedRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Width='100' Height='80' " +
                    "Direction='Row' Wrap='true' AlignItems='Start' Gap='5'>" +
                    "  <Panel Name='Oversized' Width='120' Height='10' />" +
                    "  <Panel Name='Next' Width='40' Height='10' />" +
                    "</FlexPanel>");

            try
            {
                PerformLayout(oversizedRuntime);
                AssertBounds(
                    oversizedRuntime.Get<Panel>("Oversized"),
                    0,
                    0,
                    120,
                    10,
                    "oversized first item owns the first line");
                AssertBounds(
                    oversizedRuntime.Get<Panel>("Next"),
                    0,
                    15,
                    40,
                    10,
                    "item after oversized first item uses the next line");
            }
            finally
            {
                DisposeRuntime(oversizedRuntime);
            }
        }

        public static void GrowUsesTheWrapBasisAndRedistributesBounds()
        {
            XamlRuntime wrapRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Width='80' Height='80' " +
                    "Direction='Row' Wrap='true' AlignItems='Start' Gap='10'>" +
                    "  <Panel Name='First' Width='40' Height='10' FlexGrow='1' />" +
                    "  <Panel Name='Second' MinWidth='20' Height='10' FlexGrow='1' />" +
                    "  <Panel Name='Third' Width='30' Height='10' />" +
                    "</FlexPanel>");

            try
            {
                PerformLayout(wrapRuntime);
                AssertBounds(
                    wrapRuntime.Get<Panel>("First"),
                    0,
                    0,
                    45,
                    10,
                    "explicit grow basis receives its share");
                AssertBounds(
                    wrapRuntime.Get<Panel>("Second"),
                    55,
                    0,
                    25,
                    10,
                    "minimum grow basis participates in wrapping and growth");
                AssertBounds(
                    wrapRuntime.Get<Panel>("Third"),
                    0,
                    20,
                    30,
                    10,
                    "natural item wraps after the same grow bases");
            }
            finally
            {
                DisposeRuntime(wrapRuntime);
            }

            XamlRuntime boundedRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Width='300' Height='30' " +
                    "Direction='Row' AlignItems='Start'>" +
                    "  <Panel Name='First' FlexGrow='1' MaxWidth='40' " +
                    "MaxHeight='1000' Height='10' />" +
                    "  <Panel Name='Second' FlexGrow='1' MaxWidth='80' " +
                    "MaxHeight='1000' Height='10' />" +
                    "  <Panel Name='Third' Height='10' FlexGrow='1' />" +
                    "</FlexPanel>");

            try
            {
                PerformLayout(boundedRuntime);
                AssertBounds(
                    boundedRuntime.Get<Panel>("First"),
                    0,
                    0,
                    40,
                    10,
                    "first maximum grow bound");
                AssertBounds(
                    boundedRuntime.Get<Panel>("Second"),
                    40,
                    0,
                    80,
                    10,
                    "second maximum grow bound");
                AssertBounds(
                    boundedRuntime.Get<Panel>("Third"),
                    120,
                    0,
                    180,
                    10,
                    "remaining grow space is redistributed");
            }
            finally
            {
                DisposeRuntime(boundedRuntime);
            }

            XamlRuntime minimumRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Width='300' Height='30' " +
                    "Direction='Row' AlignItems='Start'>" +
                    "  <Panel Name='First' MinWidth='100' Height='10' FlexGrow='1' />" +
                    "  <Panel Name='Second' Height='10' FlexGrow='1' />" +
                    "</FlexPanel>");

            try
            {
                PerformLayout(minimumRuntime);
                AssertBounds(
                    minimumRuntime.Get<Panel>("First"),
                    0,
                    0,
                    200,
                    10,
                    "minimum size is part of the grow basis");
                AssertBounds(
                    minimumRuntime.Get<Panel>("Second"),
                    200,
                    0,
                    100,
                    10,
                    "free space follows the bounded basis");
            }
            finally
            {
                DisposeRuntime(minimumRuntime);
            }
        }

        public static void RightToLeftKeepsLogicalOrder()
        {
            XamlRuntime rowRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Width='100' Height='80' " +
                    "Direction='Row' FlowDirection='RightToLeft' " +
                    "Wrap='true' AlignItems='Start' Gap='5'>" +
                    "  <Panel Name='First' Width='40' Height='10' />" +
                    "  <Panel Name='Second' Width='40' Height='10' />" +
                    "  <Panel Name='Third' Width='40' Height='10' />" +
                    "</FlexPanel>");

            try
            {
                PerformLayout(rowRuntime);
                AssertBounds(
                    rowRuntime.Get<Panel>("First"),
                    60,
                    0,
                    40,
                    10,
                    "RTL logical row start");
                AssertBounds(
                    rowRuntime.Get<Panel>("Second"),
                    15,
                    0,
                    40,
                    10,
                    "RTL logical row continuation");
                AssertBounds(
                    rowRuntime.Get<Panel>("Third"),
                    60,
                    15,
                    40,
                    10,
                    "RTL wrapped row keeps logical order");
            }
            finally
            {
                DisposeRuntime(rowRuntime);
            }

            XamlRuntime columnRuntime =
                XamlRuntime.Load(
                    "<FlexPanel Name='Root' Width='80' Height='55' " +
                    "Direction='Column' FlowDirection='RightToLeft' " +
                    "Wrap='true' AlignItems='Start' Gap='5'>" +
                    "  <Panel Name='First' Width='20' Height='25' />" +
                    "  <Panel Name='Second' Width='20' Height='25' />" +
                    "  <Panel Name='Third' Width='20' Height='25' />" +
                    "</FlexPanel>");

            try
            {
                PerformLayout(columnRuntime);
                AssertBounds(
                    columnRuntime.Get<Panel>("First"),
                    60,
                    0,
                    20,
                    25,
                    "RTL column starts at the logical cross start");
                AssertBounds(
                    columnRuntime.Get<Panel>("Second"),
                    60,
                    30,
                    20,
                    25,
                    "RTL column main-axis continuation");
                AssertBounds(
                    columnRuntime.Get<Panel>("Third"),
                    35,
                    0,
                    20,
                    25,
                    "RTL wrapped columns progress toward the left");
            }
            finally
            {
                DisposeRuntime(columnRuntime);
            }
        }

        public static void ReusesBoundedScratchAcrossStressLayouts()
        {
            StringBuilder markup = new StringBuilder();
            markup.Append(
                "<FlexPanel Name='Root' Width='420' Height='260' " +
                "Direction='Row' FlowDirection='RightToLeft' " +
                "Wrap='true' AlignItems='Start' Gap='2'>");
            int i;

            for (i = 0; i < 256; i++)
            {
                markup.Append(
                    "<Panel Name='Item" + i + "' Width='12' Height='8' " +
                    "FlexGrow='1' MaxWidth='24' " +
                    (i % 31 == 0
                        ? "Visibility='Collapsed' "
                        : String.Empty) +
                    "/>");
            }

            markup.Append("</FlexPanel>");
            XamlRuntime runtime =
                XamlRuntime.Load(markup.ToString());

            try
            {
                XamlRuntime.FlexPanel panel =
                    runtime.Get<XamlRuntime.FlexPanel>("Root");
                panel.PerformLayout();

                object scratch =
                    panel.LayoutScratchIdentityForTest;
                Rectangle initialFirst =
                    runtime.Get<Panel>("Item1").Bounds;
                Rectangle initialSecond =
                    runtime.Get<Panel>("Item2").Bounds;

                AssertTrue(
                    scratch != null,
                    "the warm flex pass retains bounded scratch storage");
                AssertBounds(
                    runtime.Get<Panel>("Item0"),
                    0,
                    0,
                    0,
                    0,
                    "collapsed stress child");
                AssertTrue(
                    initialFirst.Left > initialSecond.Left,
                    "the stress fixture preserves RTL logical order");

                panel.ResetLayoutScratchDiagnosticsForTest();

                int pass;

                for (pass = 0; pass < 100; pass++)
                {
                    panel.Width = pass % 2 == 0 ? 421 : 420;
                    panel.PerformLayout();
                    panel.GetPreferredSize(
                        new Size(panel.Width, panel.Height));

                    AssertSame(
                        scratch,
                        panel.LayoutScratchIdentityForTest,
                        "stress pass retains the same flex scratch identity");
                }

                panel.Width = 420;
                panel.PerformLayout();

                AssertEqual(
                    0L,
                    panel.LayoutPlanAllocationCountForTest,
                    "warm flex stress allocates no new plan");
                AssertEqual(
                    0L,
                    panel.LayoutArrayAllocationCountForTest,
                    "warm flex stress allocates no layout arrays");
                AssertTrue(
                    panel.LayoutScratchReuseCountForTest >= 200L,
                    "layout and preferred-size stress reuse bounded storage");
                AssertEqual(
                    initialFirst,
                    runtime.Get<Panel>("Item1").Bounds,
                    "restoring the viewport restores the first grow item");
                AssertEqual(
                    initialSecond,
                    runtime.Get<Panel>("Item2").Bounds,
                    "restoring the viewport restores the second grow item");
                AssertBounds(
                    runtime.Get<Panel>("Item0"),
                    0,
                    0,
                    0,
                    0,
                    "collapsed child stays collapsed throughout scratch reuse");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        public static void ReentrantLayoutDoesNotStealScratch()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<FlexPanel Name='Root' Width='240' Height='80'>" +
                "  <PreferredSizeProbe Name='Probe' " +
                "PreferredWidth='30' PreferredHeight='20' />" +
                "</FlexPanel>");

            try
            {
                XamlRuntime.FlexPanel panel =
                    runtime.Get<XamlRuntime.FlexPanel>("Root");
                PreferredSizeProbe probe =
                    runtime.Get<PreferredSizeProbe>("Probe");
                panel.PerformLayout();

                object outerScratch =
                    panel.LayoutScratchIdentityForTest;

                panel.ResetLayoutScratchDiagnosticsForTest();
                probe.ResetMeasurementCount();
                probe.ReenterParentOnNextMeasurement = true;
                panel.PerformLayout();

                AssertSame(
                    outerScratch,
                    panel.LayoutScratchIdentityForTest,
                    "the outer reentrant owner recovers its original scratch");
                AssertEqual(
                    1L,
                    panel.LayoutPlanAllocationCountForTest,
                    "the nested pass allocates an independent plan");
                AssertEqual(
                    3L,
                    panel.LayoutArrayAllocationCountForTest,
                    "the nested pass allocates only its independent arrays");
                AssertEqual(
                    1L,
                    panel.LayoutScratchReuseCountForTest,
                    "the outer pass reuses the retained plan");
                AssertEqual(
                    2,
                    probe.PreferredSizeCallCount,
                    "the nested preferred-size request still completes");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void PerformLayout(
            XamlRuntime runtime)
        {
            XamlRuntime.FlexPanel panel =
                runtime.RootControl as
                    XamlRuntime.FlexPanel;

            if (panel == null)
            {
                throw new InvalidOperationException(
                    "The test root must be a FlexPanel.");
            }

            panel.PerformLayout();
        }

        private static void AssertBounds(
            Control control,
            int x,
            int y,
            int width,
            int height,
            string message)
        {
            Rectangle expected =
                new Rectangle(
                    x,
                    y,
                    width,
                    height);

            if (control.Bounds != expected)
            {
                throw new InvalidOperationException(
                    message +
                    ": expected " +
                    expected.ToString() +
                    ", got " +
                    control.Bounds.ToString() +
                    ".");
            }
        }

        private static void AssertSize(
            Size actual,
            int width,
            int height,
            string message)
        {
            Size expected =
                new Size(
                    width,
                    height);

            if (actual != expected)
            {
                throw new InvalidOperationException(
                    message +
                    ": expected " +
                    expected.ToString() +
                    ", got " +
                    actual.ToString() +
                    ".");
            }
        }

        private static void AssertTrue(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message + ".");
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ": object identity changed.");
            }
        }

        private static void AssertEqual(
            long expected,
            long actual,
            string message)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    message +
                    ": expected " +
                    expected +
                    ", got " +
                    actual +
                    ".");
            }
        }

        private static void AssertEqual(
            int expected,
            int actual,
            string message)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    message +
                    ": expected " +
                    expected +
                    ", got " +
                    actual +
                    ".");
            }
        }

        private static void AssertEqual(
            Rectangle expected,
            Rectangle actual,
            string message)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    message +
                    ": expected " +
                    expected +
                    ", got " +
                    actual +
                    ".");
            }
        }

        private static void DisposeRuntime(
            XamlRuntime runtime)
        {
            if (runtime != null)
                runtime.Dispose();
        }
    }
}
