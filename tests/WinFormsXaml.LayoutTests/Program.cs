using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.LayoutTests
{
    internal sealed class Program
    {
        private delegate void TestMethod();

        private sealed class TestCase
        {
            private readonly string _name;
            private readonly TestMethod _method;

            public TestCase(string name, TestMethod method)
            {
                _name = name;
                _method = method;
            }

            public string Name
            {
                get { return _name; }
            }

            public TestMethod Method
            {
                get { return _method; }
            }
        }

        private sealed class BindingState
        {
            public string Alignment;
            public bool FillLastChild;
            public string ForegroundColor;
        }

        [STAThread]
        private static int Main()
        {
            TestCase[] tests = new TestCase[]
            {
                new TestCase("grid pixel, star, and span layout", TestGridLayout),
                new TestCase("horizontal stack layout", TestStackPanelLayout),
                new TestCase("dock layout and last-child fill", TestDockPanelLayout),
                new TestCase("canvas edge anchors", TestCanvasLayout),
                new TestCase("border content layout", TestBorderLayout),
                new TestCase("right-to-left grid columns", TestRightToLeftGridLayout),
                new TestCase(
                    "alignment binding reload updates layout",
                    TestDynamicAlignmentReload),
                new TestCase(
                    "last-child-fill binding reload updates layout",
                    TestDynamicLastChildFillReload),
                new TestCase(
                    "foreground binding reload updates inheritance",
                    TestDynamicForegroundInheritanceReload),
                new TestCase(
                    "TabView content stretch and bidirectional header layout",
                    TabViewLayoutTests.Run),
                new TestCase(
                    "FlexPanel row and column wrapping",
                    FlexPanelLayoutTests.RowAndColumnWrap),
                new TestCase(
                    "FlexPanel constrained preferred size",
                    FlexPanelLayoutTests.ConstrainedPreferredSizeWraps),
                new TestCase(
                    "FlexPanel collapsed and oversized line planning",
                    FlexPanelLayoutTests.CollapsedAndOversizedItemsDoNotCreateEmptyLines),
                new TestCase(
                    "FlexPanel grow basis and bounded redistribution",
                    FlexPanelLayoutTests.GrowUsesTheWrapBasisAndRedistributesBounds),
                new TestCase(
                    "FlexPanel logical RTL placement",
                    FlexPanelLayoutTests.RightToLeftKeepsLogicalOrder),
                new TestCase(
                    "FlexPanel bounded scratch survives stress",
                    FlexPanelLayoutTests.ReusesBoundedScratchAcrossStressLayouts),
                new TestCase(
                    "FlexPanel reentrant layout isolates scratch",
                    FlexPanelLayoutTests.ReentrantLayoutDoesNotStealScratch),
                new TestCase(
                    "preferred size is reused within one layout pass",
                    PreferredSizePassCacheTests.ReusesMeasurementWithinOneFlexPass),
                new TestCase(
                    "nested custom layouts preserve bounds",
                    PreferredSizePassCacheTests.PreservesNestedCustomLayoutSemantics),
                new TestCase(
                    "preferred size cache separates proposed sizes",
                    PreferredSizePassCacheTests.KeepsDifferentProposedSizesSeparate),
                new TestCase(
                    "preferred size is fresh between layout passes",
                    PreferredSizePassCacheTests.ClearsMeasurementsBetweenOuterPasses),
                new TestCase(
                    "preferred size cache preserves reentrancy",
                    PreferredSizePassCacheTests.PreservesReentrantMeasurement),
                new TestCase(
                    "failed preferred size is not cached",
                    PreferredSizePassCacheTests.DoesNotCacheFailedMeasurements),
                new TestCase(
                    "preferred size cache clears after layout exception",
                    PreferredSizePassCacheTests.ClearsCacheWhenArrangementThrows)
            };

            int failed = 0;
            int i;

            for (i = 0; i < tests.Length; i++)
            {
                try
                {
                    tests[i].Method();
                    Console.WriteLine("PASS  " + tests[i].Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL  " + tests[i].Name);
                    Console.Error.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine(
                "WinFormsXaml layout: " +
                (tests.Length - failed) +
                " passed, " +
                failed +
                " failed.");

            return failed == 0 ? 0 : 1;
        }

        private static void TestGridLayout()
        {
            const string markup =
                "<Grid Width='300' Height='160' Padding='10'>" +
                "  <Grid.ColumnDefinitions>" +
                "    <ColumnDefinition Width='80' />" +
                "    <ColumnDefinition Width='*' />" +
                "  </Grid.ColumnDefinitions>" +
                "  <Grid.RowDefinitions>" +
                "    <RowDefinition Height='40' />" +
                "    <RowDefinition Height='*' />" +
                "  </Grid.RowDefinitions>" +
                "  <Panel Name='Header' Grid.Row='0' Grid.ColumnSpan='2' />" +
                "  <Panel Name='Navigation' Grid.Row='1' Grid.Column='0' />" +
                "  <Panel Name='Content' Grid.Row='1' Grid.Column='1' />" +
                "</Grid>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                AssertBounds(
                    runtime.Get<Panel>("Header"),
                    10,
                    10,
                    280,
                    40,
                    "spanning header");

                AssertBounds(
                    runtime.Get<Panel>("Navigation"),
                    10,
                    50,
                    80,
                    100,
                    "pixel column");

                AssertBounds(
                    runtime.Get<Panel>("Content"),
                    90,
                    50,
                    200,
                    100,
                    "star column and row");
            }
            finally
            {
                DisposeRuntime(runtime);
            }

            Exception invalidLength = null;
            XamlRuntime invalidRuntime = null;

            try
            {
                invalidRuntime = XamlRuntime.Load(
                    "<Grid>" +
                    "  <Grid.ColumnDefinitions>" +
                    "    <ColumnDefinition Width='-1*' />" +
                    "  </Grid.ColumnDefinitions>" +
                    "</Grid>");
            }
            catch (Exception ex)
            {
                invalidLength = ex;
            }
            finally
            {
                DisposeRuntime(invalidRuntime);
            }

            if (invalidLength == null ||
                !ExceptionContains(invalidLength, "finite and non-negative"))
            {
                throw new InvalidOperationException(
                    "Negative grid weights must be rejected before layout.");
            }
        }

        private static void TestStackPanelLayout()
        {
            const string markup =
                "<StackPanel Width='220' Height='100' Padding='10' " +
                "Orientation='Horizontal'>" +
                "  <Panel Name='First' Width='40' Height='30' Margin='2' />" +
                "  <Panel Name='Second' Width='50' Height='20' Margin='3' />" +
                "</StackPanel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                AssertBounds(
                    runtime.Get<Panel>("First"),
                    12,
                    12,
                    40,
                    30,
                    "first stacked child");

                AssertBounds(
                    runtime.Get<Panel>("Second"),
                    57,
                    13,
                    50,
                    20,
                    "second stacked child");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestDockPanelLayout()
        {
            const string markup =
                "<DockPanel Width='300' Height='160' Padding='10' " +
                "LastChildFill='true'>" +
                "  <Panel Name='Navigation' Width='50' DockPanel.Dock='Left' />" +
                "  <Panel Name='Toolbar' Height='30' DockPanel.Dock='Top' />" +
                "  <Panel Name='Workspace' />" +
                "</DockPanel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                AssertBounds(
                    runtime.Get<Panel>("Navigation"),
                    10,
                    10,
                    50,
                    140,
                    "left dock");

                AssertBounds(
                    runtime.Get<Panel>("Toolbar"),
                    60,
                    10,
                    230,
                    30,
                    "top dock after left dock");

                AssertBounds(
                    runtime.Get<Panel>("Workspace"),
                    60,
                    40,
                    230,
                    110,
                    "last-child fill");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestCanvasLayout()
        {
            const string markup =
                "<Canvas Width='200' Height='120' Padding='10'>" +
                "  <Panel Name='Leading' Width='30' Height='20' " +
                "Canvas.Left='15' Canvas.Top='7' />" +
                "  <Panel Name='Trailing' Width='40' Height='25' " +
                "Canvas.Right='12' Canvas.Bottom='8' />" +
                "</Canvas>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                AssertBounds(
                    runtime.Get<Panel>("Leading"),
                    25,
                    17,
                    30,
                    20,
                    "left and top anchors");

                AssertBounds(
                    runtime.Get<Panel>("Trailing"),
                    138,
                    77,
                    40,
                    25,
                    "right and bottom anchors");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestBorderLayout()
        {
            const string markup =
                "<Border Width='160' Height='90' Padding='7' " +
                "BorderThickness='2' BorderBrush='#123456'>" +
                "  <Panel Name='BorderContent' />" +
                "</Border>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                AssertBounds(
                    runtime.Get<Panel>("BorderContent"),
                    7,
                    7,
                    146,
                    76,
                    "border content slot");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestRightToLeftGridLayout()
        {
            const string markup =
                "<Grid Width='240' Height='80' FlowDirection='RightToLeft'>" +
                "  <Grid.ColumnDefinitions>" +
                "    <ColumnDefinition Width='60' />" +
                "    <ColumnDefinition Width='*' />" +
                "  </Grid.ColumnDefinitions>" +
                "  <Panel Name='LogicalFirst' Grid.Column='0' />" +
                "  <Panel Name='LogicalSecond' Grid.Column='1' />" +
                "</Grid>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                AssertBounds(
                    runtime.Get<Panel>("LogicalFirst"),
                    180,
                    0,
                    60,
                    80,
                    "first logical RTL column");

                AssertBounds(
                    runtime.Get<Panel>("LogicalSecond"),
                    0,
                    0,
                    180,
                    80,
                    "remaining RTL column");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestDynamicAlignmentReload()
        {
            BindingState state = new BindingState();
            state.Alignment = "Left";

            const string markup =
                "<Grid Width='200' Height='60'>" +
                "  <Panel Name='Aligned' Width='40' Height='20' " +
                "HorizontalAlignment='{Binding Alignment}' " +
                "VerticalAlignment='Top' />" +
                "</Grid>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Panel aligned = runtime.Get<Panel>("Aligned");

                AssertBounds(
                    aligned,
                    0,
                    0,
                    40,
                    20,
                    "initial left alignment");

                state.Alignment = "Right";
                runtime.ReloadBinding("Aligned", "HorizontalAlignment");

                AssertBounds(
                    aligned,
                    160,
                    0,
                    40,
                    20,
                    "right alignment after binding reload");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestDynamicLastChildFillReload()
        {
            BindingState state = new BindingState();
            state.FillLastChild = false;

            const string markup =
                "<DockPanel Name='Dock' Width='300' Height='100' " +
                "LastChildFill='{Binding FillLastChild}'>" +
                "  <Panel Name='Leading' Width='50' DockPanel.Dock='Left' />" +
                "  <Border Name='Last' DockPanel.Dock='Right'>" +
                "    <Panel Width='30' Height='10' />" +
                "  </Border>" +
                "</DockPanel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Panel last = runtime.Get<Panel>("Last");

                AssertBounds(
                    last,
                    270,
                    0,
                    30,
                    100,
                    "initial right-docked last child");

                state.FillLastChild = true;
                runtime.ReloadBinding("Dock", "LastChildFill");

                AssertBounds(
                    last,
                    50,
                    0,
                    250,
                    100,
                    "last-child fill after binding reload");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestDynamicForegroundInheritanceReload()
        {
            BindingState state = new BindingState();
            state.ForegroundColor = "Red";

            const string markup =
                "<Panel Name='ColorParent' Width='120' Height='40' " +
                "Foreground='{Binding ForegroundColor}'>" +
                "  <Label Name='ColorChild' Width='80' Height='20' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Panel parent = runtime.Get<Panel>("ColorParent");
                Label child = runtime.Get<Label>("ColorChild");

                AssertColor(Color.Red, parent.ForeColor, "initial parent foreground");
                AssertColor(Color.Red, child.ForeColor, "initial inherited foreground");

                state.ForegroundColor = "Blue";
                runtime.ReloadBinding("ColorParent", "Foreground");

                AssertColor(Color.Blue, parent.ForeColor, "reloaded parent foreground");
                AssertColor(Color.Blue, child.ForeColor, "reloaded inherited foreground");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
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
                new Rectangle(x, y, width, height);

            if (control.Bounds != expected)
            {
                throw new InvalidOperationException(
                    "Assertion failed: " +
                    message +
                    ". Expected <" +
                    expected +
                    ">, actual <" +
                    control.Bounds +
                    ">.");
            }
        }

        private static void AssertColor(
            Color expected,
            Color actual,
            string message)
        {
            if (actual.ToArgb() != expected.ToArgb())
            {
                throw new InvalidOperationException(
                    "Assertion failed: " +
                    message +
                    ". Expected <" +
                    expected +
                    ">, actual <" +
                    actual +
                    ">.");
            }
        }

        private static bool ExceptionContains(
            Exception error,
            string text)
        {
            while (error != null)
            {
                if (error.Message != null &&
                    error.Message.IndexOf(
                        text,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                error = error.InnerException;
            }

            return false;
        }

        private static void DisposeRuntime(XamlRuntime runtime)
        {
            if (runtime == null)
                return;

            Control root = runtime.RootControl;

            if (root != null && !root.IsDisposed)
                root.Dispose();

            runtime.Dispose();
        }
    }
}
