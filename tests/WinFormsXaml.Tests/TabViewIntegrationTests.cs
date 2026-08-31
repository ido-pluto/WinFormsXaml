using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    internal static class TabViewIntegrationTests
    {
        private sealed class SelectionBindingState
        {
            public readonly PropertyBinding<int> ActiveIndex;

            public SelectionBindingState(int activeIndex)
            {
                ActiveIndex =
                    new PropertyBinding<int>(activeIndex);
            }
        }

        private sealed class MarkupState
        {
            public string CurrentStyle;
            public int IndexChangedCount;
            public int ItemChangedCount;
            public int SelectionChangedCount;
            public TabViewSelectionChangedEventArgs LastChange;

            public void Tabs_SelectedIndexChanged(
                object sender,
                EventArgs e)
            {
                IndexChangedCount++;
            }

            public void Tabs_SelectedItemChanged(
                object sender,
                EventArgs e)
            {
                ItemChangedCount++;
            }

            public void Tabs_SelectionChanged(
                object sender,
                TabViewSelectionChangedEventArgs e)
            {
                SelectionChangedCount++;
                LastChange = e;
            }

            public void Reset()
            {
                IndexChangedCount = 0;
                ItemChangedCount = 0;
                SelectionChangedCount = 0;
                LastChange = null;
            }
        }

        public static void Run()
        {
            TestMarkupStylesResourcesAndSelectionEvents();
            TestDeferredSelectedIndexAndPropertyCollection();
            TestTwoWaySelectedIndex();
            TestCollectionMutationAndOwnership();
            TestOwnerVisibilityPreservesRequestedVisibility();
            TestSelectionEventReselectionIsReentrant();
            TestSelectionEventRemovalIsReentrant();
            TestSingleContentAndChildValidation();
            TestNativeCustomRenderingTransitions();
            TestNativeRightToLeftDefault();
            TestRoundedHeadersInBothDirections();
        }

        private static void TestNativeRightToLeftDefault()
        {
            TabView tabs = new TabView();
            TabViewItem first = CreateItem("First");
            TabViewItem second = CreateItem("Second");

            try
            {
                tabs.Size = new Size(260, 140);
                tabs.RightToLeft = RightToLeft.Yes;
                tabs.TabItems.Add(first);
                tabs.TabItems.Add(second);
                tabs.CreateControl();
                tabs.PerformLayout();

                AssertTrue(
                    tabs.UsesNativeTabs,
                    "RTL alone keeps native TabControl rendering");
                AssertEqual(
                    RightToLeft.Yes,
                    tabs.NativeTabControl.RightToLeft,
                    "native TabControl receives RTL direction");
                AssertTrue(
                    tabs.NativeTabControl.RightToLeftLayout,
                    "native TabControl enables mirrored layout");

                Rectangle firstBounds = tabs.GetHeaderBounds(first);
                Rectangle secondBounds = tabs.GetHeaderBounds(second);
                AssertTrue(
                    firstBounds.Left > secondBounds.Left,
                    "native RTL headers expose mirrored logical geometry");
                AssertSame(first, tabs.SelectedItem, "native RTL keeps selection");

                tabs.TabCornerRadius = 6;
                tabs.PerformLayout();
                AssertTrue(
                    !tabs.UsesNativeTabs,
                    "rounded appearance activates custom RTL tabs");
                AssertTrue(
                    tabs.GetHeaderBounds(first).Left >
                    tabs.GetHeaderBounds(second).Left,
                    "custom RTL keeps mirrored logical geometry");
            }
            finally
            {
                tabs.Dispose();

                if (!first.IsDisposed)
                    first.Dispose();
                if (!second.IsDisposed)
                    second.Dispose();
            }
        }

        private static void TestNativeCustomRenderingTransitions()
        {
            Form form = new Form();
            TabView tabs = new TabView();
            TabViewItem first = new TabViewItem();
            TabViewItem second = CreateItem("Second");
            Panel content = new Panel();
            TextBox editor = new TextBox();
            int selectionChanges = 0;
            int forceNativeChanges = 0;

            first.Header = "First";
            content.Controls.Add(editor);
            first.Controls.Add(content);
            tabs.Dock = DockStyle.Fill;
            tabs.TabItems.Add(first);
            tabs.TabItems.Add(second);
            tabs.SelectionChanged +=
                delegate { selectionChanges++; };
            tabs.ForceNativeTabsChanged +=
                delegate { forceNativeChanges++; };
            form.Controls.Add(tabs);

            try
            {
                form.Show();
                Application.DoEvents();

                AssertTrue(tabs.UsesNativeTabs, "TabView is native by default");
                AssertTrue(
                    tabs.NativeTabControl != null &&
                    tabs.NativeTabControl.Visible,
                    "native TabControl surface is visible");
                AssertEqual(
                    2,
                    tabs.NativeTabControl.TabPages.Count,
                    "native surface mirrors logical pages");
                AssertSame(
                    first,
                    tabs.NativeTabControl.SelectedTab.Tag,
                    "native surface mirrors selected identity");

                TabPage nativeFirstPage =
                    tabs.NativeTabControl.TabPages[0];
                TabPage nativeSecondPage =
                    tabs.NativeTabControl.TabPages[1];
                TabControl nativeHost = tabs.NativeTabControl;
                int tabViewControlCount = tabs.Controls.Count;
                int nativeControlCount = nativeHost.Controls.Count;
                IntPtr nativeHandle = nativeHost.Handle;
                IntPtr firstHandle = first.Handle;
                IntPtr contentHandle = content.Handle;
                editor.Focus();
                Application.DoEvents();
                AssertTrue(editor.Focused, "content receives focus before switch");

                tabs.TabBackground = Color.FromArgb(20, 30, 40);
                Application.DoEvents();
                AssertTrue(
                    !tabs.UsesNativeTabs,
                    "effective appearance activates framework tabs");
                AssertTrue(
                    !tabs.NativeTabControl.Visible,
                    "framework mode hides native surface");
                AssertSame(first, tabs.SelectedItem, "custom mode keeps selection");
                AssertEqual(firstHandle, first.Handle, "custom mode keeps page handle");
                AssertEqual(
                    contentHandle,
                    content.Handle,
                    "custom mode keeps content handle");
                AssertTrue(editor.Focused, "custom mode keeps content focus");
                AssertEqual(0, selectionChanges, "mode switch raises no selection event");

                tabs.ForceNativeTabs = true;
                Application.DoEvents();
                AssertTrue(tabs.UsesNativeTabs, "ForceNativeTabs wins");
                AssertTrue(
                    tabs.NativeTabControl.Visible,
                    "forced native surface is visible");
                AssertSame(first, tabs.SelectedItem, "forced native keeps selection");
                AssertSame(
                    first,
                    tabs.NativeTabControl.SelectedTab.Tag,
                    "forced native keeps native selection");
                AssertSame(
                    nativeFirstPage,
                    tabs.NativeTabControl.TabPages[0],
                    "mode switch reuses native proxy page");
                AssertEqual(firstHandle, first.Handle, "forced native keeps page handle");
                AssertEqual(
                    contentHandle,
                    content.Handle,
                    "forced native keeps content handle");
                AssertTrue(editor.Focused, "forced native keeps content focus");
                AssertEqual(0, selectionChanges, "forced native is selection-quiet");

                tabs.TabForeground = Color.Red;
                AssertTrue(
                    tabs.UsesNativeTabs,
                    "custom values stay dormant while native is forced");
                tabs.ForceNativeTabs = false;
                AssertTrue(
                    !tabs.UsesNativeTabs,
                    "stored custom values reactivate framework tabs");

                int transition;
                forceNativeChanges = 0;

                for (transition = 0; transition < 64; transition++)
                {
                    tabs.ForceNativeTabs = true;
                    tabs.ForceNativeTabs = false;

                    if ((transition % 8) == 0)
                        Application.DoEvents();
                }

                tabs.ForceNativeTabs = true;
                Application.DoEvents();
                AssertSame(
                    nativeFirstPage,
                    tabs.NativeTabControl.TabPages[0],
                    "repeated transitions reuse native proxy page");
                AssertSame(
                    nativeSecondPage,
                    tabs.NativeTabControl.TabPages[1],
                    "repeated transitions reuse every native proxy page");
                AssertSame(
                    nativeHost,
                    tabs.NativeTabControl,
                    "repeated transitions reuse native host");
                AssertEqual(
                    tabViewControlCount,
                    tabs.Controls.Count,
                    "repeated transitions do not accumulate TabView children");
                AssertEqual(
                    nativeControlCount,
                    nativeHost.Controls.Count,
                    "repeated transitions do not accumulate native controls");
                AssertEqual(
                    2,
                    nativeHost.TabPages.Count,
                    "repeated transitions keep the proxy page count bounded");
                AssertEqual(
                    nativeHandle,
                    nativeHost.Handle,
                    "repeated transitions preserve native host handle");
                AssertEqual(firstHandle, first.Handle, "repeated transitions keep page");
                AssertEqual(
                    contentHandle,
                    content.Handle,
                    "repeated transitions keep content handle");
                AssertEqual(0, tabs.SelectedIndex, "repeated transitions keep index");
                AssertSame(first, tabs.SelectedItem, "repeated transitions keep item");
                AssertTrue(editor.Focused, "repeated transitions keep focus");
                AssertEqual(
                    129,
                    forceNativeChanges,
                    "rapid transitions raise one ForceNativeTabs event each");
                AssertEqual(
                    0,
                    CountOwnedTimers(tabs),
                    "rapid transitions do not create TabView timers");
                AssertEqual(0, selectionChanges, "rapid transitions remain selection-quiet");

                nativeHost.SelectedIndex = 1;
                Application.DoEvents();
                AssertEqual(1, tabs.SelectedIndex, "native selection remains connected");
                AssertSame(second, tabs.SelectedItem, "native selection keeps item identity");
                AssertEqual(
                    1,
                    selectionChanges,
                    "native selection raises one consolidated event after stress");

                tabs.SelectedIndex = 0;
                selectionChanges = 0;
                tabs.ForceNativeTabs = false;

                tabs.TabBackground = SystemColors.Control;
                tabs.TabForeground = SystemColors.ControlText;
                AssertTrue(
                    tabs.UsesNativeTabs,
                    "restoring effective defaults restores native tabs");
                AssertEqual(0, selectionChanges, "all mode transitions are quiet");
            }
            finally
            {
                form.Close();
                form.Dispose();

                if (!tabs.IsDisposed)
                    tabs.Dispose();
                if (!first.IsDisposed)
                    first.Dispose();
                if (!second.IsDisposed)
                    second.Dispose();
            }
        }

        private static void TestRoundedHeadersInBothDirections()
        {
            TabView tabs = new TabView();
            TabViewItem first = CreateItem("First");
            TabViewItem second = CreateItem("Second");

            try
            {
                tabs.Size = new Size(260, 140);
                tabs.TabItems.Add(first);
                tabs.TabItems.Add(second);
                tabs.TabCornerRadius = 30;
                tabs.SelectedTabCornerRadius = 9;
                tabs.CreateControl();
                tabs.PerformLayout();

                AssertTrue(
                    !tabs.UsesNativeTabs,
                    "rounded corners activate framework tabs");
                Rectangle ltrFirst = tabs.GetHeaderBounds(first);
                Rectangle ltrSecond = tabs.GetHeaderBounds(second);
                AssertTrue(
                    ltrFirst.Left < ltrSecond.Left,
                    "rounded headers keep LTR order");
                AssertSame(
                    null,
                    HitTestHeader(tabs, ltrFirst.Location),
                    "rounded LTR corner is outside hit geometry");
                AssertSame(
                    first,
                    HitTestHeader(
                        tabs,
                        new Point(
                            ltrFirst.Left + ltrFirst.Width / 2,
                            ltrFirst.Top + ltrFirst.Height / 2)),
                    "rounded LTR center remains clickable");

                tabs.RightToLeft = RightToLeft.Yes;
                tabs.PerformLayout();
                Rectangle rtlFirst = tabs.GetHeaderBounds(first);
                Rectangle rtlSecond = tabs.GetHeaderBounds(second);
                AssertTrue(
                    tabs.NativeTabControl.RightToLeftLayout,
                    "native adapter keeps RTL state while dormant");
                AssertTrue(
                    rtlFirst.Left > rtlSecond.Left,
                    "rounded headers mirror in RTL");
                AssertSame(
                    null,
                    HitTestHeader(tabs, rtlFirst.Location),
                    "rounded RTL corner is outside hit geometry");

                tabs.ForceNativeTabs = true;
                AssertTrue(tabs.UsesNativeTabs, "rounded properties are dormant natively");
                AssertTrue(
                    tabs.NativeTabControl.RightToLeftLayout,
                    "forced native tabs retain RTL layout");
                tabs.ForceNativeTabs = false;
                AssertTrue(
                    !tabs.UsesNativeTabs,
                    "rounded framework tabs return after force is removed");

                using (Bitmap bitmap = new Bitmap(260, 140))
                {
                    int paint;

                    for (paint = 0; paint < 128; paint++)
                    {
                        tabs.DrawToBitmap(
                            bitmap,
                            new Rectangle(0, 0, 260, 140));
                    }
                }

                AssertEqual(
                    5,
                    tabs.PaintResourceCountForTest,
                    "repeat rounded painting reuses five cached brushes");
                tabs.Dispose();
                AssertEqual(
                    0,
                    tabs.PaintResourceCountForTest,
                    "disposing rounded TabView releases cached brushes");
            }
            finally
            {
                if (!tabs.IsDisposed)
                    tabs.Dispose();

                if (!first.IsDisposed)
                    first.Dispose();
                if (!second.IsDisposed)
                    second.Dispose();
            }
        }

        private static TabViewItem HitTestHeader(TabView tabs, Point point)
        {
            MethodInfo method = typeof(TabView).GetMethod(
                "HitTestHeader",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
                throw new InvalidOperationException("Missing TabView hit-test hook.");

            return (TabViewItem)method.Invoke(
                tabs,
                new object[] { point });
        }

        private static int CountOwnedTimers(TabView tabs)
        {
            FieldInfo[] fields = typeof(TabView).GetFields(
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public |
                BindingFlags.DeclaredOnly);
            int count = 0;
            int i;

            for (i = 0; i < fields.Length; i++)
            {
                object value = fields[i].GetValue(tabs);

                if (value is System.Windows.Forms.Timer ||
                    value is System.Threading.Timer)
                {
                    count++;
                }
            }

            return count;
        }

        private static void TestMarkupStylesResourcesAndSelectionEvents()
        {
            MarkupState state = new MarkupState();
            state.CurrentStyle = "DarkTabs";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='DarkTabs' TargetType='TabView'>" +
                "      <Setter Property='TabBackground' Value='{Preset Theme.TabSurface}' />" +
                "      <Setter Property='SelectedTabBackground' Value='{Preset Theme.TabSelected}' />" +
                "      <Setter Property='TabForeground' Value='{Preset Theme.TabText}' />" +
                "      <Setter Property='SelectedTabForeground' Value='{Preset Theme.TabTextSelected}' />" +
                "      <Setter Property='TabBorderBrush' Value='{Preset Theme.Border}' />" +
                "      <Setter Property='TabBorderThickness' Value='1,2,3,4' />" +
                "      <Setter Property='TabPadding' Value='5,6,7,8' />" +
                "      <Setter Property='HeaderSpacing' Value='9' />" +
                "      <Setter Property='ContentBackground' Value='{Preset Theme.Content}' />" +
                "      <Setter Property='ContentBorderBrush' Value='{Preset Theme.ContentBorder}' />" +
                "      <Setter Property='ContentBorderThickness' Value='2,3,4,5' />" +
                "      <Setter Property='ContentPadding' Value='10,11,12,13' />" +
                "    </Style>" +
                "    <Style Key='LightTabs' TargetType='TabView'>" +
                "      <Setter Property='TabBackground' Value='#E1E2E3' />" +
                "      <Setter Property='SelectedTabBackground' Value='#F1F2F3' />" +
                "      <Setter Property='TabForeground' Value='#212223' />" +
                "      <Setter Property='SelectedTabForeground' Value='#111213' />" +
                "      <Setter Property='TabBorderBrush' Value='#A1A2A3' />" +
                "      <Setter Property='TabBorderThickness' Value='4' />" +
                "      <Setter Property='TabPadding' Value='8' />" +
                "      <Setter Property='HeaderSpacing' Value='2' />" +
                "      <Setter Property='ContentBackground' Value='#FAFBFC' />" +
                "      <Setter Property='ContentBorderBrush' Value='#B1B2B3' />" +
                "      <Setter Property='ContentBorderThickness' Value='3' />" +
                "      <Setter Property='ContentPadding' Value='6' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Presets Name='Theme' Selected='Dark'>" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='TabSurface' Value='#101112' />" +
                "      <Set Key='TabSelected' Value='#202122' />" +
                "      <Set Key='TabText' Value='#C1C2C3' />" +
                "      <Set Key='TabTextSelected' Value='#F1F2F3' />" +
                "      <Set Key='Border' Value='#303132' />" +
                "      <Set Key='Content' Value='#404142' />" +
                "      <Set Key='ContentBorder' Value='#505152' />" +
                "      <Set Key='ItemSurface' Value='#606162' />" +
                "      <Set Key='ItemText' Value='#D1D2D3' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <TabView Name='Tabs' Style='{Binding CurrentStyle}' " +
                "           SelectedIndex='1' " +
                "           SelectedIndexChanged='Tabs_SelectedIndexChanged' " +
                "           SelectedItemChanged='Tabs_SelectedItemChanged' " +
                "           SelectionChanged='Tabs_SelectionChanged'>" +
                "    <TabView.Resources>" +
                "      <Style TargetType='TabViewItem'>" +
                "        <Setter Property='BackColor' Value='{Preset Theme.ItemSurface}' />" +
                "      </Style>" +
                "    </TabView.Resources>" +
                "    <TabViewItem Name='FirstTab' Header='First'>" +
                "      <TabViewItem.Resources>" +
                "        <Style TargetType='Label'>" +
                "          <Setter Property='Foreground' Value='{Preset Theme.ItemText}' />" +
                "        </Style>" +
                "      </TabViewItem.Resources>" +
                "      <Label Name='FirstContent' Text='First content' />" +
                "    </TabViewItem>" +
                "    <TabViewItem Name='SecondTab' Header='Second'>" +
                "      <Panel Name='SecondContent' />" +
                "    </TabViewItem>" +
                "  </TabView>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                TabView tabs = runtime.Get<TabView>("Tabs");
                TabViewItem first = runtime.Get<TabViewItem>("FirstTab");
                TabViewItem second = runtime.Get<TabViewItem>("SecondTab");
                Label firstContent = runtime.Get<Label>("FirstContent");

                AssertEqual(2, tabs.TabItems.Count, "direct TabItems count");
                AssertSame(first, tabs.TabItems[0], "first direct TabViewItem");
                AssertSame(second, tabs.TabItems[1], "second direct TabViewItem");
                AssertEqual("First", first.Header, "Header maps to first item");
                AssertEqual("Second", second.Header, "Header maps to second item");
                AssertEqual(1, tabs.SelectedIndex, "deferred selected index");
                AssertSame(second, tabs.SelectedItem, "deferred selected item");

                AssertDarkStyle(tabs);
                AssertColor(
                    Color.FromArgb(0x60, 0x61, 0x62),
                    first.BackColor,
                    "TabView.Resources implicit item style");
                AssertColor(
                    Color.FromArgb(0xD1, 0xD2, 0xD3),
                    firstContent.ForeColor,
                    "TabViewItem.Resources implicit content style");

                state.CurrentStyle = "LightTabs";
                runtime.ReloadBinding("Tabs", "Style");
                AssertLightStyle(tabs);

                state.Reset();
                tabs.SelectedIndex = 0;

                AssertEqual(1, state.IndexChangedCount, "index event count");
                AssertEqual(1, state.ItemChangedCount, "item event count");
                AssertEqual(
                    1,
                    state.SelectionChangedCount,
                    "consolidated selection event count");
                AssertTrue(state.LastChange != null, "selection event args");
                AssertEqual(1, state.LastChange.OldIndex, "old selection index");
                AssertEqual(0, state.LastChange.NewIndex, "new selection index");
                AssertSame(second, state.LastChange.OldItem, "old selection item");
                AssertSame(first, state.LastChange.NewItem, "new selection item");

                state.Reset();
                tabs.SelectedIndex = 0;
                AssertEqual(0, state.IndexChangedCount, "same index is quiet");
                AssertEqual(0, state.ItemChangedCount, "same item is quiet");
                AssertEqual(
                    0,
                    state.SelectionChangedCount,
                    "same selection is quiet");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestDeferredSelectedIndexAndPropertyCollection()
        {
            XamlRuntime propertyRuntime = XamlRuntime.Load(
                "<TabView Name='Tabs' SelectedIndex='1'>" +
                "  <TabView.TabItems>" +
                "    <TabViewItem Name='First' Header='First'><Panel /></TabViewItem>" +
                "    <TabViewItem Name='Second' Header='Second'><Panel /></TabViewItem>" +
                "  </TabView.TabItems>" +
                "</TabView>");

            try
            {
                TabView tabs = propertyRuntime.Get<TabView>("Tabs");
                TabViewItem second =
                    propertyRuntime.Get<TabViewItem>("Second");

                AssertEqual(2, tabs.TabItems.Count, "property TabItems count");
                AssertEqual(
                    1,
                    tabs.SelectedIndex,
                    "property collection deferred selection");
                AssertSame(
                    second,
                    tabs.SelectedItem,
                    "property collection selected item");
            }
            finally
            {
                DisposeRuntime(propertyRuntime);
            }

            XamlRuntime noSelectionRuntime = XamlRuntime.Load(
                "<TabView Name='Tabs' SelectedIndex='-1'>" +
                "  <TabViewItem Header='First'><Panel /></TabViewItem>" +
                "  <TabViewItem Header='Second'><Panel /></TabViewItem>" +
                "</TabView>");

            try
            {
                TabView tabs =
                    noSelectionRuntime.Get<TabView>("Tabs");

                AssertEqual(
                    -1,
                    tabs.SelectedIndex,
                    "explicit negative selection suppresses auto selection");
                AssertSame(
                    null,
                    tabs.SelectedItem,
                    "explicit negative selection has no selected item");
            }
            finally
            {
                DisposeRuntime(noSelectionRuntime);
            }
        }

        private static void TestTwoWaySelectedIndex()
        {
            SelectionBindingState state =
                new SelectionBindingState(1);
            XamlRuntime runtime = XamlRuntime.Load(
                "<TabView Name='Tabs' " +
                "         SelectedIndex='{Binding ActiveIndex, Mode=TwoWay}'>" +
                "  <TabViewItem Header='First'><Panel /></TabViewItem>" +
                "  <TabViewItem Header='Second'><Panel /></TabViewItem>" +
                "</TabView>",
                state);

            try
            {
                TabView tabs = runtime.Get<TabView>("Tabs");
                CreateHandleAndDrain(runtime.RootControl);

                AssertEqual(1, tabs.SelectedIndex, "initial bound index");

                state.ActiveIndex.Value = 0;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(0, tabs.SelectedIndex, "source changes selection");

                tabs.SelectedIndex = 1;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    1,
                    state.ActiveIndex.Value,
                    "selection changes two-way source");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestCollectionMutationAndOwnership()
        {
            TabView tabs = new TabView();
            TabViewItem first = CreateItem("First");
            TabViewItem second = CreateItem("Second");
            TabViewItem inserted = CreateItem("Inserted");

            try
            {
                tabs.TabItems.Add(first);
                AssertEqual(0, tabs.SelectedIndex, "first add auto-selects");
                AssertSame(first, tabs.SelectedItem, "first add selected item");

                tabs.TabItems.Add(second);
                tabs.SelectedItem = second;
                tabs.TabItems.Insert(0, inserted);
                AssertSame(
                    second,
                    tabs.SelectedItem,
                    "insert before selection preserves selected item");
                AssertEqual(
                    2,
                    tabs.SelectedIndex,
                    "insert before selection updates selected index");

                tabs.TabItems.Move(2, 0);
                AssertSame(
                    second,
                    tabs.SelectedItem,
                    "moving selected item preserves selection");
                AssertEqual(0, tabs.SelectedIndex, "move updates selected index");

                tabs.TabItems.Remove(inserted);
                AssertSame(
                    second,
                    tabs.SelectedItem,
                    "remove after selection preserves selection");

                tabs.TabItems.Remove(second);
                AssertSame(
                    first,
                    tabs.SelectedItem,
                    "removing selected item chooses same logical slot");
                AssertEqual(0, tabs.SelectedIndex, "replacement selection index");

                AssertTrue(
                    tabs.TabItems.Contains(first),
                    "collection contains retained item");
                AssertEqual(
                    0,
                    tabs.TabItems.IndexOf(first),
                    "collection index query");

                ExpectException(
                    delegate { tabs.TabItems.Add(null); },
                    "null item");
                ExpectException(
                    delegate { tabs.TabItems.Add(first); },
                    "duplicate item");

                TabView other = new TabView();

                try
                {
                    ExpectException(
                        delegate { other.TabItems.Add(first); },
                        "item owned by another TabView");
                }
                finally
                {
                    other.Dispose();
                }

                tabs.TabItems.Clear();
                AssertEqual(-1, tabs.SelectedIndex, "clear selected index");
                AssertSame(null, tabs.SelectedItem, "clear selected item");

                tabs.SelectedIndex = -1;
                TabViewItem afterExplicitNone = CreateItem("AfterNone");
                tabs.TabItems.Add(afterExplicitNone);
                AssertEqual(
                    -1,
                    tabs.SelectedIndex,
                    "explicit none disables later auto-selection");
                AssertSame(
                    null,
                    tabs.SelectedItem,
                    "explicit none keeps selected item null");

                tabs.TabItems.RemoveAt(0);
                AssertEqual(0, tabs.TabItems.Count, "remove-at updates count");
                afterExplicitNone.Dispose();
            }
            finally
            {
                tabs.Dispose();

                if (!first.IsDisposed)
                    first.Dispose();
                if (!second.IsDisposed)
                    second.Dispose();
                if (!inserted.IsDisposed)
                    inserted.Dispose();
            }
        }

        private static void TestSingleContentAndChildValidation()
        {
            XamlRuntime emptyItemRuntime = XamlRuntime.Load(
                "<TabView Name='Tabs'>" +
                "  <TabViewItem Name='Empty' Header='Empty' />" +
                "</TabView>");

            try
            {
                AssertEqual(
                    1,
                    emptyItemRuntime.Get<TabView>("Tabs").TabItems.Count,
                    "empty TabViewItem is valid");
            }
            finally
            {
                DisposeRuntime(emptyItemRuntime);
            }

            ExpectMarkupError(
                "<TabView><Panel /></TabView>",
                "TabViewItem");
            ExpectMarkupError(
                "<TabView><TabViewItem Header='Invalid'>" +
                "<Panel /><Label /></TabViewItem></TabView>",
                "TabViewItem");
            ExpectMarkupError(
                "<TabView><TabView.TabItems>" +
                "<Panel /></TabView.TabItems></TabView>",
                "TabViewItem");
        }

        private static void TestOwnerVisibilityPreservesRequestedVisibility()
        {
            TabView tabs = new TabView();
            TabViewItem first = CreateItem("First");
            TabViewItem second = CreateItem("Second");

            try
            {
                tabs.TabItems.Add(first);
                tabs.TabItems.Add(second);

                AssertSame(first, tabs.SelectedItem, "first item initially selected");
                AssertTrue(first.RequestedVisible, "selected item requested visible");
                AssertTrue(second.RequestedVisible, "unselected item requested visible");
                AssertTrue(first.Visible, "selected item is owner-visible");
                AssertTrue(!second.Visible, "owner hides unselected item");

                tabs.SelectedItem = second;
                AssertTrue(first.RequestedVisible, "owner hiding keeps first request");
                AssertTrue(!first.Visible, "first becomes owner-hidden");
                AssertTrue(second.Visible, "second becomes owner-visible");

                second.Visible = false;
                AssertTrue(
                    !second.RequestedVisible,
                    "application hide updates requested visibility");
                AssertSame(
                    first,
                    tabs.SelectedItem,
                    "hiding selected item chooses nearest visible item");
                AssertEqual(0, tabs.SelectedIndex, "nearest visible item index");
                AssertTrue(first.Visible, "replacement selected item is visible");

                second.Visible = true;
                AssertTrue(
                    second.RequestedVisible,
                    "application restore updates requested visibility");
                AssertSame(
                    first,
                    tabs.SelectedItem,
                    "restored item does not steal selection");
                AssertTrue(
                    !second.Visible,
                    "restored unselected item remains owner-hidden");
            }
            finally
            {
                tabs.Dispose();

                if (!first.IsDisposed)
                    first.Dispose();
                if (!second.IsDisposed)
                    second.Dispose();
            }
        }

        private static void TestSelectionEventReselectionIsReentrant()
        {
            TabView tabs = new TabView();
            TabViewItem first = CreateItem("First");
            TabViewItem second = CreateItem("Second");
            TabViewItem third = CreateItem("Third");
            int indexChanged = 0;
            int itemChanged = 0;
            int selectionChanged = 0;
            TabViewSelectionChangedEventArgs lastChange = null;

            try
            {
                tabs.TabItems.Add(first);
                tabs.TabItems.Add(second);
                tabs.TabItems.Add(third);

                tabs.SelectedIndexChanged +=
                    delegate
                    {
                        indexChanged++;

                        if (tabs.SelectedIndex == 1)
                            tabs.SelectedIndex = 2;
                    };
                tabs.SelectedItemChanged +=
                    delegate { itemChanged++; };
                tabs.SelectionChanged +=
                    delegate(
                        object sender,
                        TabViewSelectionChangedEventArgs e)
                    {
                        selectionChanged++;
                        lastChange = e;
                    };

                tabs.SelectedIndex = 1;

                AssertSame(third, tabs.SelectedItem, "reentrant final selection");
                AssertEqual(2, tabs.SelectedIndex, "reentrant final index");
                AssertEqual(
                    2,
                    indexChanged,
                    "outer and nested index events are observed");
                AssertEqual(
                    1,
                    itemChanged,
                    "outer transition emits no stale item event");
                AssertEqual(
                    1,
                    selectionChanged,
                    "outer transition emits no stale consolidated event");
                AssertTrue(lastChange != null, "nested selection event args");
                AssertEqual(1, lastChange.OldIndex, "nested old index");
                AssertEqual(2, lastChange.NewIndex, "nested new index");
                AssertSame(second, lastChange.OldItem, "nested old item");
                AssertSame(third, lastChange.NewItem, "nested new item");
            }
            finally
            {
                tabs.Dispose();

                if (!first.IsDisposed)
                    first.Dispose();
                if (!second.IsDisposed)
                    second.Dispose();
                if (!third.IsDisposed)
                    third.Dispose();
            }
        }

        private static void TestSelectionEventRemovalIsReentrant()
        {
            TabView tabs = new TabView();
            TabViewItem first = CreateItem("First");
            TabViewItem second = CreateItem("Second");
            TabViewItem third = CreateItem("Third");
            int indexChanged = 0;
            int itemChanged = 0;
            int selectionChanged = 0;
            TabViewSelectionChangedEventArgs lastChange = null;

            try
            {
                tabs.TabItems.Add(first);
                tabs.TabItems.Add(second);
                tabs.TabItems.Add(third);

                tabs.SelectedIndexChanged +=
                    delegate
                    {
                        indexChanged++;

                        if (tabs.SelectedItem == second)
                            tabs.TabItems.Remove(second);
                    };
                tabs.SelectedItemChanged +=
                    delegate { itemChanged++; };
                tabs.SelectionChanged +=
                    delegate(
                        object sender,
                        TabViewSelectionChangedEventArgs e)
                    {
                        selectionChanged++;
                        lastChange = e;
                    };

                tabs.SelectedIndex = 1;

                AssertSame(
                    third,
                    tabs.SelectedItem,
                    "removal reentrancy selects the same logical slot");
                AssertEqual(1, tabs.SelectedIndex, "removal replacement index");
                AssertEqual(1, indexChanged, "outer index event only");
                AssertEqual(
                    1,
                    itemChanged,
                    "nested removal item event only");
                AssertEqual(
                    1,
                    selectionChanged,
                    "nested removal consolidated event only");
                AssertTrue(lastChange != null, "removal selection event args");
                AssertEqual(1, lastChange.OldIndex, "removal old index");
                AssertEqual(1, lastChange.NewIndex, "removal new index");
                AssertSame(second, lastChange.OldItem, "removed old item");
                AssertSame(third, lastChange.NewItem, "replacement new item");
            }
            finally
            {
                tabs.Dispose();

                if (!first.IsDisposed)
                    first.Dispose();
                if (!second.IsDisposed)
                    second.Dispose();
                if (!third.IsDisposed)
                    third.Dispose();
            }
        }

        private static TabViewItem CreateItem(string header)
        {
            TabViewItem item = new TabViewItem();
            item.Header = header;
            item.Controls.Add(new Panel());
            return item;
        }

        private static void AssertDarkStyle(TabView tabs)
        {
            AssertColor(
                Color.FromArgb(0x10, 0x11, 0x12),
                tabs.TabBackground,
                "dark TabBackground");
            AssertColor(
                Color.FromArgb(0x20, 0x21, 0x22),
                tabs.SelectedTabBackground,
                "dark SelectedTabBackground");
            AssertColor(
                Color.FromArgb(0xC1, 0xC2, 0xC3),
                tabs.TabForeground,
                "dark TabForeground");
            AssertColor(
                Color.FromArgb(0xF1, 0xF2, 0xF3),
                tabs.SelectedTabForeground,
                "dark SelectedTabForeground");
            AssertColor(
                Color.FromArgb(0x30, 0x31, 0x32),
                tabs.TabBorderBrush,
                "dark TabBorderBrush");
            AssertEqual(
                new Padding(1, 2, 3, 4),
                tabs.TabBorderThickness,
                "dark TabBorderThickness");
            AssertEqual(
                new Padding(5, 6, 7, 8),
                tabs.TabPadding,
                "dark TabPadding");
            AssertEqual(9, tabs.HeaderSpacing, "dark HeaderSpacing");
            AssertColor(
                Color.FromArgb(0x40, 0x41, 0x42),
                tabs.ContentBackground,
                "dark ContentBackground");
            AssertColor(
                Color.FromArgb(0x50, 0x51, 0x52),
                tabs.ContentBorderBrush,
                "dark ContentBorderBrush");
            AssertEqual(
                new Padding(2, 3, 4, 5),
                tabs.ContentBorderThickness,
                "dark ContentBorderThickness");
            AssertEqual(
                new Padding(10, 11, 12, 13),
                tabs.ContentPadding,
                "dark ContentPadding");
        }

        private static void AssertLightStyle(TabView tabs)
        {
            AssertColor(
                Color.FromArgb(0xE1, 0xE2, 0xE3),
                tabs.TabBackground,
                "light TabBackground");
            AssertColor(
                Color.FromArgb(0xF1, 0xF2, 0xF3),
                tabs.SelectedTabBackground,
                "light SelectedTabBackground");
            AssertColor(
                Color.FromArgb(0x21, 0x22, 0x23),
                tabs.TabForeground,
                "light TabForeground");
            AssertColor(
                Color.FromArgb(0x11, 0x12, 0x13),
                tabs.SelectedTabForeground,
                "light SelectedTabForeground");
            AssertColor(
                Color.FromArgb(0xA1, 0xA2, 0xA3),
                tabs.TabBorderBrush,
                "light TabBorderBrush");
            AssertEqual(
                new Padding(4),
                tabs.TabBorderThickness,
                "light TabBorderThickness");
            AssertEqual(new Padding(8), tabs.TabPadding, "light TabPadding");
            AssertEqual(2, tabs.HeaderSpacing, "light HeaderSpacing");
            AssertColor(
                Color.FromArgb(0xFA, 0xFB, 0xFC),
                tabs.ContentBackground,
                "light ContentBackground");
            AssertColor(
                Color.FromArgb(0xB1, 0xB2, 0xB3),
                tabs.ContentBorderBrush,
                "light ContentBorderBrush");
            AssertEqual(
                new Padding(3),
                tabs.ContentBorderThickness,
                "light ContentBorderThickness");
            AssertEqual(
                new Padding(6),
                tabs.ContentPadding,
                "light ContentPadding");
        }

        private static void CreateHandleAndDrain(Control root)
        {
            AssertTrue(root != null, "reactive root exists");

            if (!root.IsHandleCreated)
                root.CreateControl();

            if (!root.IsHandleCreated)
            {
                IntPtr handle = root.Handle;
                AssertTrue(handle != IntPtr.Zero, "reactive root handle");
            }

            DrainReactiveCallbacks(root);
        }

        private static void DrainReactiveCallbacks(Control root)
        {
            int round;

            for (round = 0; round < 6; round++)
            {
                bool reached = false;

                root.BeginInvoke(
                    new MethodInvoker(
                        delegate { reached = true; }));

                int iterations = 0;

                while (!reached && iterations < 1024)
                {
                    Application.DoEvents();
                    iterations++;
                }

                AssertTrue(reached, "reactive dispatch sentinel");
            }
        }

        private static void ExpectMarkupError(
            string markup,
            string expectedText)
        {
            XamlRuntime runtime = null;
            Exception failure = null;

            try
            {
                runtime = XamlRuntime.Load(markup);
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                DisposeRuntime(runtime);
            }

            AssertTrue(failure != null, "invalid TabView markup rejected");
            AssertTrue(
                ExceptionContains(failure, expectedText),
                "TabView markup error mentions " + expectedText);
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

        private static void ExpectException(
            MethodInvoker action,
            string message)
        {
            bool rejected = false;

            try
            {
                action();
            }
            catch (Exception)
            {
                rejected = true;
            }

            AssertTrue(rejected, message + " rejected");
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

        private static void AssertColor(
            Color expected,
            Color actual,
            string message)
        {
            if (expected.ToArgb() != actual.ToArgb())
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
    }
}
