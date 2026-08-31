using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal static class ItemsControlScrollCommandTests
    {
        private sealed class ScrollRow
        {
            public readonly string Id;
            public readonly string Detail;
            public readonly bool Checked;
            private readonly string _title;
            public int TitleReadCount;

            public ScrollRow(int index)
            {
                Id = "scroll-" + index;
                _title = "Row " + index;
                Detail = "Nested detail " + index;
                Checked = (index & 1) == 0;
            }

            public string Title
            {
                get
                {
                    TitleReadCount++;
                    return _title;
                }
            }
        }

        private sealed class MouseWheelProbe : ItemsControl
        {
            public bool NativeHorizontalScrollVisible
            {
                get { return HScroll; }
            }

            public bool NativeVerticalScrollVisible
            {
                get { return VScroll; }
            }

            public void RaiseMouseWheel(int delta)
            {
                OnMouseWheel(
                    new HandledMouseEventArgs(
                        MouseButtons.None,
                        0,
                        0,
                        0,
                        delta));
            }
        }

        internal static void RunAll()
        {
            TestAutoScrollDefaultsTrueAndExplicitFalseWins();
            TestLogicalCommandsClampOnBothAxes();
            TestModernMouseWheelUsesLogicalCommandPath();
            TestFocusedDescendantNavigationUsesLogicalCommandPath();
            TestNonVirtualRapidScrollKeepsEveryItemTree();
        }

        private static void
            TestAutoScrollDefaultsTrueAndExplicitFalseWins()
        {
            using (ItemsControl host = new ItemsControl())
            {
                AssertTrue(
                    host.AutoScroll,
                    "ItemsControl enables AutoScroll by default");

                host.AutoScroll = false;

                AssertTrue(
                    !host.AutoScroll,
                    "the CLR property accepts an explicit false value");
            }

            const string markup =
                "<ItemsControl Name='Rows' AutoScroll='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                AssertTrue(
                    !runtime.GetItemsControl("Rows").AutoScroll,
                    "markup preserves an explicit AutoScroll=false value");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestLogicalCommandsClampOnBothAxes()
        {
            using (ItemsControl vertical = CreateScrollHost(
                Orientation.Vertical))
            {
                AssertLogicalCommands(vertical, "vertical");
            }

            using (ItemsControl horizontal = CreateScrollHost(
                Orientation.Horizontal))
            {
                AssertLogicalCommands(horizontal, "horizontal");
            }
        }

        private static ItemsControl CreateScrollHost(
            Orientation orientation)
        {
            ItemsControl host = new ItemsControl();
            host.Orientation = orientation;
            host.SmoothScroll = false;
            host.Size = new Size(120, 80);
            host.AutoScrollMinSize = orientation == Orientation.Vertical
                ? new Size(120, 1200)
                : new Size(1200, 80);
            host.CreateControl();
            return host;
        }

        private static void AssertLogicalCommands(
            ItemsControl host,
            string axis)
        {
            AssertEqual(
                0,
                host.GetLogicalScrollOffset(),
                axis + " starts at the first position");

            AssertTrue(
                host.ScrollBy(ScrollEventType.SmallIncrement),
                axis + " small increment moves");
            int small = host.GetLogicalScrollOffset();
            AssertTrue(small > 0, axis + " small increment is positive");

            host.ScrollBy(ScrollEventType.LargeIncrement);
            AssertTrue(
                host.GetLogicalScrollOffset() > small,
                axis + " large increment moves farther");

            host.ScrollBy(ScrollEventType.Last);
            int last = host.GetLogicalScrollOffset();
            AssertTrue(last > 0, axis + " last reaches the end");

            AssertTrue(
                !host.ScrollBy(ScrollEventType.LargeIncrement),
                axis + " large increment clamps at the end");
            AssertEqual(
                last,
                host.GetLogicalScrollOffset(),
                axis + " end clamp remains stable");

            host.SetLogicalScrollOffset(Int32.MaxValue);
            AssertEqual(
                last,
                host.GetLogicalScrollOffset(),
                axis + " direct thumb target clamps at the end");

            host.ScrollBy(ScrollEventType.LargeDecrement);
            AssertTrue(
                host.GetLogicalScrollOffset() < last,
                axis + " large decrement moves backward");

            host.ScrollBy(ScrollEventType.First);
            AssertEqual(
                0,
                host.GetLogicalScrollOffset(),
                axis + " first reaches zero");

            AssertTrue(
                !host.ScrollBy(ScrollEventType.SmallDecrement),
                axis + " small decrement clamps at zero");
            AssertTrue(
                !host.SetLogicalScrollOffset(-1),
                axis + " negative thumb target clamps at zero");
        }

        private static void TestModernMouseWheelUsesLogicalCommandPath()
        {
            using (MouseWheelProbe host = new MouseWheelProbe())
            {
                host.SmoothScroll = false;
                host.Size = new Size(120, 80);
                host.AutoScrollMinSize = new Size(120, 1200);
                host.CreateControl();
                host.PerformLayout();

                Size extent = host.AutoScrollMinSize;
                bool horizontalVisible =
                    host.NativeHorizontalScrollVisible;
                bool verticalVisible =
                    host.NativeVerticalScrollVisible;
                int verticalMaximum = host.VerticalScroll.Maximum;

                int raised = 0;
                host.MouseWheel += delegate
                {
                    raised++;
                };

                host.RaiseMouseWheel(-120);

                AssertEqual(
                    1,
                    raised,
                    "modern wheel input still raises MouseWheel once");
                AssertEqual(
                    extent,
                    host.AutoScrollMinSize,
                    "wheel handling preserves the native scroll extent");
                AssertEqual(
                    horizontalVisible,
                    host.NativeHorizontalScrollVisible,
                    "wheel handling preserves horizontal scrollbar visibility");
                AssertEqual(
                    verticalVisible,
                    host.NativeVerticalScrollVisible,
                    "wheel handling preserves vertical scrollbar visibility");
                AssertEqual(
                    verticalMaximum,
                    host.VerticalScroll.Maximum,
                    "wheel handling preserves the native scroll range");

                if (SystemInformation.MouseWheelScrollLines != 0)
                {
                    AssertTrue(
                        host.GetLogicalScrollOffset() > 0,
                        "modern wheel input moves through the logical setter");

                    host.RaiseMouseWheel(120);
                    AssertEqual(
                        0,
                        host.GetLogicalScrollOffset(),
                        "opposite modern wheel input returns toward zero");

                    host.RaiseMouseWheel(-30);
                    AssertTrue(
                        host.GetLogicalScrollOffset() > 0,
                        "precision wheel input moves before a complete notch");
                }

                AssertEqual(
                    -120,
                    LegacyMouseWheelRouter.DecodeWheelDelta(
                        new IntPtr(-120)),
                    "legacy Win9x wheel payload preserves signed decrement");
                AssertEqual(
                    120,
                    LegacyMouseWheelRouter.DecodeWheelDelta(
                        new IntPtr(120)),
                    "legacy Win9x wheel payload preserves signed increment");
            }
        }

        private static void
            TestFocusedDescendantNavigationUsesLogicalCommandPath()
        {
            using (ItemsControl host = CreateScrollHost(
                Orientation.Vertical))
            using (Panel item = new Panel())
            using (Button child = new Button())
            {
                item.Controls.Add(child);
                host.Controls.Add(item);
                host.CreateControl();
                item.CreateControl();
                child.CreateControl();

                AssertTrue(
                    PreprocessKey(child, Keys.Down),
                    "a nested item child routes an unhandled Down key");
                AssertTrue(
                    host.GetLogicalScrollOffset() > 0,
                    "a nested item child scrolls its nearest vertical host");

                host.SetLogicalScrollOffset(0);
                host.SmoothScroll = true;

                AssertTrue(
                    PreprocessKey(child, Keys.PageDown),
                    "a nested item child routes an unhandled PageDown key");
                AssertEqual(
                    0,
                    host.GetLogicalScrollOffset(),
                    "focused descendant smooth input does not jump synchronously");
                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest &&
                    host.SmoothScrollTargetOffsetForTest > 0,
                    "focused descendant input starts the shared smooth transition");

                int target = host.SmoothScrollTargetOffsetForTest;
                host.ApplySmoothScrollFrameForTest(
                    host.SmoothScrollDuration);
                AssertEqual(
                    target,
                    host.GetLogicalScrollOffset(),
                    "focused descendant smooth input settles exactly");
            }

            using (ItemsControl host = CreateScrollHost(
                Orientation.Horizontal))
            using (Button child = new Button())
            {
                host.Controls.Add(child);
                host.CreateControl();
                child.CreateControl();

                AssertTrue(
                    PreprocessKey(child, Keys.Right),
                    "a nested item child routes a horizontal Right key");
                AssertTrue(
                    host.GetLogicalScrollOffset() > 0,
                    "LTR Right advances horizontal item content");

                host.SetLogicalScrollOffset(0);
                host.ContentRightToLeft = true;
                host.PerformLayout();
                AssertTrue(
                    PreprocessKey(child, Keys.Left),
                    "a nested RTL item child routes a horizontal Left key");
                AssertTrue(
                    host.GetLogicalScrollOffset() > 0,
                    "RTL Left advances logical horizontal item content");
            }

            using (ItemsControl host = CreateScrollHost(
                Orientation.Vertical))
            using (TextBox editor = new TextBox())
            {
                editor.Multiline = true;
                editor.Text = "first\r\nsecond";
                editor.SelectionStart = editor.Text.Length;
                host.Controls.Add(editor);
                host.CreateControl();
                editor.CreateControl();
                host.SetLogicalScrollOffset(80);
                int before = host.GetLogicalScrollOffset();

                PreprocessKey(editor, Keys.Up);
                AssertEqual(
                    before,
                    host.GetLogicalScrollOffset(),
                    "an editor keeps the navigation keys it consumes");
            }

            using (ItemsControl outer = CreateScrollHost(
                Orientation.Vertical))
            using (ItemsControl inner = CreateScrollHost(
                Orientation.Vertical))
            using (Button child = new Button())
            {
                inner.Controls.Add(child);
                outer.Controls.Add(inner);
                outer.CreateControl();
                inner.CreateControl();
                child.CreateControl();

                AssertTrue(
                    PreprocessKey(child, Keys.Down),
                    "nested item content routes a vertical navigation key");
                AssertTrue(
                    inner.GetLogicalScrollOffset() > 0,
                    "the nearest nested ItemsControl scrolls first");
                AssertEqual(
                    0,
                    outer.GetLogicalScrollOffset(),
                    "the outer ItemsControl does not double-scroll");
            }
        }

        private static bool PreprocessKey(Control control, Keys key)
        {
            Message message = Message.Create(
                control.Handle,
                0x0100,
                new IntPtr((int)key),
                IntPtr.Zero);
            return control.PreProcessMessage(ref message);
        }

        private static void
            TestNonVirtualRapidScrollKeepsEveryItemTree()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='360' Height='100' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "SmoothScroll='false' " +
                "ItemKeyPath='Id' Spacing='1'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel Height='36' Orientation='Horizontal' " +
                "                Padding='2' Spacing='4'>" +
                "      <Label Width='88' Text='{Binding Title}' />" +
                "      <CheckBox Width='64' Text='Flag' " +
                "                Checked='{Binding Checked}' />" +
                "      <Panel Width='176' Height='28'>" +
                "        <Label Width='116' Text='{Binding Detail}' />" +
                "        <Button Left='120' Width='52' Height='22' " +
                "                Text='Open' />" +
                "      </Panel>" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 64; i++)
                    rows.Add(new ScrollRow(i));

                host.CreateControl();
                host.SetItems(rows);

                AssertTrue(
                    !host.Virtualizing && !host.IsVirtualizing,
                    "identity fixture uses the ordinary nonvirtual renderer");
                AssertEqual(
                    rows.Count,
                    host.RenderedItems.Count,
                    "identity fixture realizes every item");
                AssertTrue(
                    host.AutoScrollMinSize.Height > host.ClientSize.Height,
                    "identity fixture has a scrollable vertical extent");

                Control[] originalControls =
                    CaptureRenderedControls(host);
                long blueprintBuilds =
                    host.ItemTemplateBlueprintBuildCount;
                long fallbackBuilds =
                    host.ItemTemplateFallbackBuildCount;
                long disposals =
                    host.ItemControlTreeDisposedCount;
                long runtimeBlueprintBuilds =
                    runtime.CompiledControlBlueprintBuildCount;
                int[] titleReadCounts = new int[rows.Count];

                for (i = 0; i < rows.Count; i++)
                {
                    titleReadCounts[i] =
                        ((ScrollRow)rows[i]).TitleReadCount;
                }

                int refreshCompleted = 0;

                host.RefreshCompleted += delegate
                {
                    refreshCompleted++;
                };

                int maximum = Math.Max(
                    1,
                    host.AutoScrollMinSize.Height -
                    host.ClientSize.Height);

                for (i = 0; i < 512; i++)
                {
                    host.ScrollBy(ScrollEventType.SmallIncrement);
                    host.ProcessMouseWheelDelta(-120);
                    host.ScrollBy(ScrollEventType.LargeIncrement);
                    host.SetLogicalScrollOffset(maximum);
                    host.ScrollBy(ScrollEventType.LargeDecrement);
                    host.ProcessMouseWheelDelta(120);
                    host.ScrollBy(ScrollEventType.SmallDecrement);
                    host.SetLogicalScrollOffset(
                        (i * 37) % maximum);
                }

                Control[] finalControls =
                    CaptureRenderedControls(host);

                AssertEqual(
                    originalControls.Length,
                    finalControls.Length,
                    "rapid scroll keeps the realized record count");

                for (i = 0; i < originalControls.Length; i++)
                {
                    AssertTrue(
                        Object.ReferenceEquals(
                            originalControls[i],
                            finalControls[i]),
                        "rapid scroll preserves item Control identity at " + i);
                }

                AssertEqual(
                    blueprintBuilds,
                    host.ItemTemplateBlueprintBuildCount,
                    "rapid scroll does not build another blueprint item tree");
                AssertEqual(
                    fallbackBuilds,
                    host.ItemTemplateFallbackBuildCount,
                    "rapid scroll does not build a fallback item tree");
                AssertEqual(
                    disposals,
                    host.ItemControlTreeDisposedCount,
                    "rapid scroll does not dispose an item tree");
                AssertEqual(
                    runtimeBlueprintBuilds,
                    runtime.CompiledControlBlueprintBuildCount,
                    "rapid scroll does not invoke the runtime blueprint builder");

                for (i = 0; i < rows.Count; i++)
                {
                    AssertEqual(
                        titleReadCounts[i],
                        ((ScrollRow)rows[i]).TitleReadCount,
                        "rapid scroll does not reevaluate the bound Title at " + i);
                }

                AssertEqual(
                    0,
                    refreshCompleted,
                    "rapid scroll does not complete an item refresh");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static Control[] CaptureRenderedControls(
            XamlRuntime.ItemsControl host)
        {
            Control[] controls =
                new Control[host.RenderedItems.Count];
            int i;

            for (i = 0; i < controls.Length; i++)
            {
                object record = host.RenderedItems[i];
                FieldInfo field = record == null
                    ? null
                    : record.GetType().GetField(
                        "Control",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                Control control = field == null
                    ? null
                    : field.GetValue(record) as Control;

                AssertTrue(
                    control != null && !control.IsDisposed,
                    "rendered item exposes a live Control at " + i);
                controls[i] = control;
            }

            return controls;
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
    }
}
