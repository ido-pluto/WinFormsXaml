using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.LayoutTests
{
    internal static class TabViewLayoutTests
    {
        public sealed class DirectionComponent : Panel
        {
        }

        private sealed class DirectionState
        {
            public string Direction;
        }

        public static void Run()
        {
            TestSelectedContentStretches();
            TestLeftToRightHeaderOrder();
            TestFormInheritedRightToLeftHeaderOrder();
            TestRegisteredComponentStyleInheritsRightToLeft();
            TestLiveInheritedFlowDirectionReload();
        }

        private static void TestSelectedContentStretches()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<TabView Name='Tabs' Width='320' Height='190' " +
                "         TabPadding='10,6' HeaderSpacing='4' " +
                "         ContentBorderThickness='2,3,4,5' " +
                "         ContentPadding='7,11,13,17'>" +
                "  <TabViewItem Name='SelectedPage' Header='Selected'>" +
                "    <Panel Name='SelectedContent' />" +
                "  </TabViewItem>" +
                "  <TabViewItem Name='OtherPage' Header='Other'>" +
                "    <Panel />" +
                "  </TabViewItem>" +
                "</TabView>");

            try
            {
                TabView tabs = runtime.Get<TabView>("Tabs");
                TabViewItem page =
                    runtime.Get<TabViewItem>("SelectedPage");
                TabViewItem other =
                    runtime.Get<TabViewItem>("OtherPage");
                Panel content = runtime.Get<Panel>("SelectedContent");

                PerformLayout(tabs);
                AssertTrue(page.Visible, "selected page is visible");
                AssertTrue(!other.Visible, "unselected page is hidden");
                AssertEqual(
                    DockStyle.Fill,
                    content.Dock,
                    "single page content stretches");
                AssertBoundsEqual(
                    page.DisplayRectangle,
                    content.Bounds,
                    "content fills selected page display rectangle");

                Size originalSize = content.Size;
                tabs.Size = new Size(460, 270);
                PerformLayout(tabs);

                AssertBoundsEqual(
                    page.DisplayRectangle,
                    content.Bounds,
                    "content remains stretched after resize");
                AssertTrue(
                    content.Width > originalSize.Width,
                    "content width follows TabView resize");
                AssertTrue(
                    content.Height > originalSize.Height,
                    "content height follows TabView resize");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestLeftToRightHeaderOrder()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                CreateTabsMarkup(
                    "<TabView Name='Tabs' Width='360' Height='180' " +
                    "FlowDirection='LeftToRight'>",
                    "</TabView>"));

            try
            {
                TabView tabs = runtime.Get<TabView>("Tabs");
                PerformLayout(tabs);
                AssertHeaderOrder(runtime, tabs, false, "explicit LTR");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestFormInheritedRightToLeftHeaderOrder()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<Form Name='RtlForm' Width='420' Height='240' " +
                "      FlowDirection='RightToLeft'>" +
                CreateTabsMarkup(
                    "<TabView Name='Tabs' Dock='Fill'>",
                    "</TabView>") +
                "</Form>");

            try
            {
                Form form = runtime.Get<Form>("RtlForm");
                TabView tabs = runtime.Get<TabView>("Tabs");
                PerformLayout(form);
                PerformLayout(tabs);

                AssertEqual(
                    RightToLeft.Yes,
                    tabs.RightToLeft,
                    "TabView inherits RTL from Form");
                AssertHeaderOrder(runtime, tabs, true, "Form-inherited RTL");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestRegisteredComponentStyleInheritsRightToLeft()
        {
            XamlRuntime.Register(
                "TabViewDirectionComponent",
                typeof(DirectionComponent));

            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel Width='420' Height='220'>" +
                "  <Panel.Resources>" +
                "    <Style TargetType='TabViewDirectionComponent'>" +
                "      <Setter Property='FlowDirection' Value='RightToLeft' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <TabViewDirectionComponent Name='ComponentHost' Dock='Fill'>" +
                CreateTabsMarkup(
                    "<TabView Name='Tabs' Dock='Fill'>",
                    "</TabView>") +
                "  </TabViewDirectionComponent>" +
                "</Panel>");

            try
            {
                DirectionComponent host =
                    runtime.Get<DirectionComponent>("ComponentHost");
                TabView tabs = runtime.Get<TabView>("Tabs");
                PerformLayout(runtime.RootControl);
                PerformLayout(host);
                PerformLayout(tabs);

                AssertEqual(
                    RightToLeft.Yes,
                    host.RightToLeft,
                    "registered component receives RTL style");
                AssertEqual(
                    RightToLeft.Yes,
                    tabs.RightToLeft,
                    "TabView inherits RTL through registered component");
                AssertHeaderOrder(
                    runtime,
                    tabs,
                    true,
                    "registered-component inherited RTL");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestLiveInheritedFlowDirectionReload()
        {
            DirectionState state = new DirectionState();
            state.Direction = "LeftToRight";

            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel Name='DirectionHost' Width='420' Height='220' " +
                "       FlowDirection='{Binding Direction}'>" +
                CreateTabsMarkup(
                    "<TabView Name='Tabs' Dock='Fill'>",
                    "</TabView>") +
                "</Panel>",
                state);

            try
            {
                Panel host = runtime.Get<Panel>("DirectionHost");
                TabView tabs = runtime.Get<TabView>("Tabs");
                PerformLayout(host);
                PerformLayout(tabs);
                AssertHeaderOrder(
                    runtime,
                    tabs,
                    false,
                    "initial inherited LTR");

                TabViewItem first = runtime.Get<TabViewItem>("FirstTab");
                TabViewItem second = runtime.Get<TabViewItem>("SecondTab");

                state.Direction = "RightToLeft";
                runtime.ReloadBinding("DirectionHost", "FlowDirection");
                PerformLayout(host);
                PerformLayout(tabs);

                AssertEqual(
                    RightToLeft.Yes,
                    tabs.RightToLeft,
                    "live direction reload reaches TabView");
                AssertHeaderOrder(
                    runtime,
                    tabs,
                    true,
                    "live inherited RTL");
                AssertSame(
                    first,
                    tabs.TabItems[0],
                    "live RTL keeps logical first item");
                AssertSame(
                    second,
                    tabs.TabItems[1],
                    "live RTL keeps logical second item");
                AssertEqual(
                    0,
                    tabs.SelectedIndex,
                    "live RTL keeps logical selected index");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static string CreateTabsMarkup(
            string opening,
            string closing)
        {
            return opening +
                "  <TabViewItem Name='FirstTab' Header='First'><Panel /></TabViewItem>" +
                "  <TabViewItem Name='SecondTab' Header='Second'><Panel /></TabViewItem>" +
                "  <TabViewItem Name='ThirdTab' Header='Third'><Panel /></TabViewItem>" +
                closing;
        }

        private static void AssertHeaderOrder(
            XamlRuntime runtime,
            TabView tabs,
            bool rightToLeft,
            string message)
        {
            Rectangle first = GetHeaderBounds(
                tabs,
                runtime.Get<TabViewItem>("FirstTab"));
            Rectangle second = GetHeaderBounds(
                tabs,
                runtime.Get<TabViewItem>("SecondTab"));
            Rectangle third = GetHeaderBounds(
                tabs,
                runtime.Get<TabViewItem>("ThirdTab"));

            AssertTrue(first.Width > 0, message + " first header width");
            AssertTrue(second.Width > 0, message + " second header width");
            AssertTrue(third.Width > 0, message + " third header width");

            if (rightToLeft)
            {
                AssertTrue(
                    first.Left > second.Left && second.Left > third.Left,
                    message + " logical order is visually right-to-left");
            }
            else
            {
                AssertTrue(
                    first.Left < second.Left && second.Left < third.Left,
                    message + " logical order is visually left-to-right");
            }
        }

        private static Rectangle GetHeaderBounds(
            TabView tabs,
            TabViewItem item)
        {
            MethodInfo method = typeof(TabView).GetMethod(
                "GetHeaderBounds",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                new Type[] { typeof(TabViewItem) },
                null);

            if (method == null || method.ReturnType != typeof(Rectangle))
            {
                throw new InvalidOperationException(
                    "TabView must expose its internal Rectangle " +
                    "GetHeaderBounds(TabViewItem) layout test hook.");
            }

            return (Rectangle)method.Invoke(
                tabs,
                new object[] { item });
        }

        private static void PerformLayout(Control control)
        {
            control.CreateControl();
            control.PerformLayout();

            int i;

            for (i = 0; i < control.Controls.Count; i++)
                control.Controls[i].PerformLayout();
        }

        private static void AssertBoundsEqual(
            Rectangle expected,
            Rectangle actual,
            string message)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message +
                    ". Expected <" + expected +
                    ">, actual <" + actual + ">.");
            }
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message +
                    ". Expected the same instance.");
            }
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
