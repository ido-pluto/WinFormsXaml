using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal static class ItemTemplateBlueprintTests
    {
        private sealed class BlueprintRow
        {
            public readonly PropertyBinding<string> Text;
            public readonly PropertyBinding<string> Foreground;

            public BlueprintRow(string text)
            {
                Text = new PropertyBinding<string>(text);
                Foreground = new PropertyBinding<string>("Blue");
            }
        }

        private sealed class BlueprintCodeBehind
        {
            public int ClickCount;

            public string Format(BlueprintRow row)
            {
                return "formatted:" + row.Text.Value;
            }

            public void Row_Click(object sender, EventArgs e)
            {
                ClickCount++;
            }
        }

        public static void RunAll()
        {
            TestDirectBlueprintPreservesRuntimeSemantics();
            TestCompiledTabPageAttachment();
            TestMappedPropertyUsesBlueprint();
            TestCommonMappedBlueprintMatchesFallback();
            TestApplicableStyleSelectsFallbackBeforeConstruction();
            TestPropertyElementSelectsFallbackBeforeConstruction();
        }

        private static void TestDirectBlueprintPreservesRuntimeSemantics()
        {
            const string markup =
                "<StackPanel>" +
                "  <Presets Name='Theme' Selected='Warm'>" +
                "    <Preset Name='Warm'>" +
                "      <Set Key='Caption' Value='warm' />" +
                "    </Preset>" +
                "    <Preset Name='Cool'>" +
                "      <Set Key='Caption' Value='cool' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <ItemsControl Name='Rows' Virtualizing='false' " +
                "      ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <StackPanel Name='RowRoot'>" +
                "        <TextBox Name='Editor' " +
                "          Text='{Binding Text, Mode=TwoWay}' />" +
                "        <Panel Name='NativePanel' AutoSize='true'>" +
                "          <Label Name='FunctionText' AutoSize='true' " +
                "            Text='{Function Format(.)}' />" +
                "          <Label Name='PresetText' AutoSize='true' " +
                "            Text='{Preset Theme.Caption}' />" +
                "          <Button Name='Action' Text='Run' AutoSize='true' " +
                "            Click='Row_Click' />" +
                "        </Panel>" +
                "      </StackPanel>" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</StackPanel>";

            BlueprintCodeBehind codeBehind =
                new BlueprintCodeBehind();
            BlueprintRow row = new BlueprintRow("alpha");
            BlueprintRow second = new BlueprintRow("beta");
            BlueprintRow third = new BlueprintRow("gamma");
            XamlRuntime runtime = XamlRuntime.Load(
                markup,
                codeBehind);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList items = new ArrayList();
                items.Add(row);
                items.Add(second);
                items.Add(third);
                EnsureHandle(runtime.RootControl);
                host.SetItems(items);

                AssertTrue(
                    runtime.CompiledControlBlueprintBuildCount == 3L,
                    "every eligible row used the compiled blueprint");
                AssertTrue(
                    host.ItemTemplateBlueprintBuildCount == 3L &&
                    host.ItemTemplateFallbackBuildCount == 0L,
                    "host diagnostics classified every successful row build");
                AssertTrue(
                    runtime.CompiledControlBlueprintPropertyAssignmentCount == 24L,
                    "eligible rows used pre-resolved property assignments");
                AssertTrue(
                    runtime.CompiledControlBlueprintEventBindingCount == 3L,
                    "eligible rows used the pre-resolved event assignment");
                AssertTrue(
                    runtime.CompiledControlBlueprintChildAttachmentCount == 15L,
                    "eligible rows used pre-resolved layout and normal child edges");
                AssertTrue(
                    runtime.CompiledControlBlueprintGenericAttributeDispatchCount == 0L,
                    "eligible rows avoided generic attribute dispatch");
                AssertTrue(
                    runtime.CompiledControlBlueprintStringConversionCount == 0L,
                    "eligible rows avoided per-row string property conversion");
                AssertTrue(
                    runtime.CompiledControlBlueprintGenericChildDispatchCount == 0L,
                    "eligible rows avoided generic child dispatch");
                AssertTrue(
                    runtime.CompiledControlBlueprintMemberLookupCount == 0L,
                    "eligible rows avoided generic property/event lookup");

                Control rowRoot = FindByName(host, "RowRoot");
                TextBox editor = FindByName(rowRoot, "Editor") as TextBox;
                Label functionText =
                    FindByName(rowRoot, "FunctionText") as Label;
                Label presetText =
                    FindByName(rowRoot, "PresetText") as Label;
                Button action = FindByName(rowRoot, "Action") as Button;

                AssertNotNull(editor, "blueprint preserved the row namescope");
                AssertNotNull(functionText, "function label was constructed");
                AssertNotNull(presetText, "preset label was constructed");
                AssertNotNull(action, "event button was constructed");
                AssertEqual("alpha", editor.Text, "initial binding value");
                AssertEqual(
                    "formatted:alpha",
                    functionText.Text,
                    "item function result");
                AssertEqual("warm", presetText.Text, "initial preset value");

                row.Text.Value = "source-change";
                DrainMessages();
                AssertEqual(
                    "source-change",
                    editor.Text,
                    "retained source-to-target binding");

                editor.Text = "target-change";
                AssertEqual(
                    "target-change",
                    row.Text.Value,
                    "retained target-to-source binding");

                action.PerformClick();
                AssertTrue(
                    codeBehind.ClickCount == 1,
                    "compiled event metadata retained the item event target");

                runtime.Presets.Select("Theme", "Cool");
                DrainMessages();
                AssertEqual(
                    "cool",
                    presetText.Text,
                    "retained preset slot refreshed the existing control");

                AssertTrue(
                    host.ActiveItemBindingSubscriptionCount > 0,
                    "realized rows expose active observable subscriptions");
                host.SetItems(new ArrayList());
                AssertTrue(
                    host.ActiveItemBindingSubscriptionCount == 0,
                    "removing rows releases their observable subscriptions");
                AssertTrue(
                    host.ItemControlTreeDisposedCount == 3L,
                    "removing rows records each disposed item control tree");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestCompiledTabPageAttachment()
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <TabControl Name='Tabs'>" +
                "      <TabPage Name='Page' Text='General'>" +
                "        <Label Name='PageText' Text='{Binding Text}' />" +
                "      </TabPage>" +
                "    </TabControl>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList items = new ArrayList();
                items.Add(new BlueprintRow("tab"));
                host.SetItems(items);

                TabControl tabs = FindByName(host, "Tabs") as TabControl;
                TabPage page = FindByName(tabs, "Page") as TabPage;
                Label label = FindByName(page, "PageText") as Label;

                AssertTrue(
                    runtime.CompiledControlBlueprintBuildCount == 1L,
                    "TabControl template remained blueprint eligible");
                AssertNotNull(tabs, "compiled TabControl was constructed");
                AssertNotNull(page, "compiled TabPage was constructed");
                AssertTrue(
                    tabs.TabPages.Count == 1 &&
                    Object.ReferenceEquals(tabs.TabPages[0], page),
                    "compiled child edge used TabPages attachment");
                AssertEqual("tab", label.Text, "nested tab binding value");
                AssertTrue(
                    runtime.CompiledControlBlueprintGenericChildDispatchCount == 0L,
                    "TabPage attachment avoided generic child dispatch");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestMappedPropertyUsesBlueprint()
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Name='MappedLabel' " +
                "      Foreground='{Binding Foreground}' " +
                "      Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList items = new ArrayList();
                items.Add(new BlueprintRow("mapped"));
                host.SetItems(items);

                AssertTrue(
                    runtime.CompiledControlBlueprintBuildCount == 1L,
                    "supported mapped alias used the compiled blueprint");
                AssertTrue(
                    host.ItemTemplateBlueprintBuildCount == 1L &&
                    host.ItemTemplateFallbackBuildCount == 0L,
                    "host diagnostics classified the mapped blueprint build");

                Label label = FindByName(host, "MappedLabel") as Label;
                AssertNotNull(label, "blueprint renderer built the mapped row");
                AssertEqual(Color.Blue, label.ForeColor, "mapped alias semantics");
                AssertEqual("mapped", label.Text, "mapped blueprint binding");

                BlueprintRow mapped = items[0] as BlueprintRow;
                mapped.Foreground.Value = "Red";
                DrainMessages();
                AssertEqual(
                    Color.Red,
                    label.ForeColor,
                    "retained mapped binding updates the compiled row");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestCommonMappedBlueprintMatchesFallback()
        {
            XamlRuntime blueprint = XamlRuntime.Load(
                CreateCommonMappedPropertyMarkup(false));
            XamlRuntime fallback = XamlRuntime.Load(
                CreateCommonMappedPropertyMarkup(true));

            try
            {
                ArrayList blueprintItems = new ArrayList();
                blueprintItems.Add(new BlueprintRow("mapped comparison"));
                ArrayList fallbackItems = new ArrayList();
                fallbackItems.Add(new BlueprintRow("mapped comparison"));
                XamlRuntime.ItemsControl blueprintHost =
                    blueprint.GetItemsControl("Rows");
                XamlRuntime.ItemsControl fallbackHost =
                    fallback.GetItemsControl("Rows");

                blueprintHost.SetItems(blueprintItems);
                fallbackHost.SetItems(fallbackItems);

                AssertEqual(
                    1L,
                    blueprintHost.ItemTemplateBlueprintBuildCount,
                    "realistic mapped template used the blueprint");
                AssertEqual(
                    0L,
                    blueprintHost.ItemTemplateFallbackBuildCount,
                    "realistic mapped template avoided XML fallback");
                AssertEqual(
                    0L,
                    fallbackHost.ItemTemplateBlueprintBuildCount,
                    "property-element comparison template forced fallback");
                AssertEqual(
                    1L,
                    fallbackHost.ItemTemplateFallbackBuildCount,
                    "comparison template used the authoritative renderer");

                Control blueprintRoot =
                    FindByName(blueprintHost, "MappedRoot");
                Control fallbackRoot =
                    FindByName(fallbackHost, "MappedRoot");
                Control blueprintFrame =
                    FindByName(blueprintHost, "MappedFrame");
                Control fallbackFrame =
                    FindByName(fallbackHost, "MappedFrame");
                Control blueprintCaption =
                    FindByName(blueprintHost, "MappedCaption");
                Control fallbackCaption =
                    FindByName(fallbackHost, "MappedCaption");

                AssertNotNull(blueprintRoot, "compiled mapped root");
                AssertNotNull(fallbackRoot, "fallback mapped root");
                AssertNotNull(blueprintFrame, "compiled mapped border");
                AssertNotNull(fallbackFrame, "fallback mapped border");
                AssertNotNull(blueprintCaption, "compiled mapped label");
                AssertNotNull(fallbackCaption, "fallback mapped label");

                AssertMappedControlStateEqual(
                    blueprint,
                    blueprintRoot,
                    fallback,
                    fallbackRoot,
                    "root");
                AssertMappedControlStateEqual(
                    blueprint,
                    blueprintFrame,
                    fallback,
                    fallbackFrame,
                    "border");
                AssertMappedControlStateEqual(
                    blueprint,
                    blueprintCaption,
                    fallback,
                    fallbackCaption,
                    "caption");
                AssertEqual(
                    GetFieldValue(fallbackFrame, "BorderColor"),
                    GetFieldValue(blueprintFrame, "BorderColor"),
                    "compiled BorderBrush matches fallback");
                AssertEqual(
                    GetFieldValue(fallbackFrame, "BorderThickness"),
                    GetFieldValue(blueprintFrame, "BorderThickness"),
                    "compiled BorderThickness matches fallback");
            }
            finally
            {
                blueprint.Dispose();
                fallback.Dispose();
            }
        }

        private static string CreateCommonMappedPropertyMarkup(
            bool forceFallback)
        {
            string fallbackTag = forceFallback
                ? "<StackPanel.Tag>mapped-row</StackPanel.Tag>"
                : String.Empty;
            string directTag = forceFallback
                ? String.Empty
                : " Tag='mapped-row'";

            return
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false' AutoScroll='false' " +
                "    Width='320' Height='180'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel Name='MappedRoot'" + directTag +
                "      Width='210' Height='90' MinWidth='180' MinHeight='70' " +
                "      MaxWidth='240' MaxHeight='110' Margin='1,2,3,4' " +
                "      Padding='5,6,7,8' HorizontalAlignment='Center' " +
                "      VerticalAlignment='Bottom' Background='#102030' " +
                "      Foreground='#E0D0C0'>" +
                       fallbackTag +
                "      <Border Name='MappedFrame' Width='160' Height='50' " +
                "        Margin='2' Padding='3' Background='#203040' " +
                "        Foreground='#D0C0B0' BorderBrush='#A0B0C0' " +
                "        BorderThickness='1,2,3,4'>" +
                "        <Label Name='MappedCaption' Text='{Binding Text}' " +
                "          MinWidth='100' MinHeight='12' MaxWidth='140' " +
                "          MaxHeight='30' Margin='1' Padding='2' " +
                "          HorizontalAlignment='Right' " +
                "          VerticalAlignment='Center' Background='#304050' " +
                "          Foreground='#C0B0A0' />" +
                "      </Border>" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
        }

        private static void AssertMappedControlStateEqual(
            XamlRuntime blueprintRuntime,
            Control blueprint,
            XamlRuntime fallbackRuntime,
            Control fallback,
            string description)
        {
            AssertEqual(fallback.Size, blueprint.Size, description + " size");
            AssertEqual(
                fallback.MinimumSize,
                blueprint.MinimumSize,
                description + " minimum size");
            AssertEqual(
                fallback.MaximumSize,
                blueprint.MaximumSize,
                description + " maximum size");
            AssertEqual(fallback.Margin, blueprint.Margin, description + " margin");
            AssertEqual(fallback.Padding, blueprint.Padding, description + " padding");
            AssertEqual(
                fallback.BackColor,
                blueprint.BackColor,
                description + " background");
            AssertEqual(
                fallback.ForeColor,
                blueprint.ForeColor,
                description + " foreground");

            object blueprintInfo = GetElementInfo(
                blueprintRuntime,
                blueprint);
            object fallbackInfo = GetElementInfo(
                fallbackRuntime,
                fallback);

            AssertEqual(
                GetFieldValue(fallbackInfo, "HorizontalAlignment"),
                GetFieldValue(blueprintInfo, "HorizontalAlignment"),
                description + " horizontal alignment metadata");
            AssertEqual(
                GetFieldValue(fallbackInfo, "VerticalAlignment"),
                GetFieldValue(blueprintInfo, "VerticalAlignment"),
                description + " vertical alignment metadata");
            AssertLocalValuesEqual(
                GetFieldValue(fallbackInfo, "LocalValueProperties") as
                    ArrayList,
                GetFieldValue(blueprintInfo, "LocalValueProperties") as
                    ArrayList,
                description + " local-value ownership");
        }

        private static object GetElementInfo(
            XamlRuntime runtime,
            object target)
        {
            MethodInfo method = typeof(XamlRuntime).GetMethod(
                "GetInfo",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertNotNull(method, "runtime element-info accessor");
            return method.Invoke(runtime, new object[] { target });
        }

        private static object GetFieldValue(
            object target,
            string name)
        {
            AssertNotNull(target, "field target for " + name);
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            AssertNotNull(field, "field " + name);
            return field.GetValue(target);
        }

        private static void AssertLocalValuesEqual(
            ArrayList expected,
            ArrayList actual,
            string message)
        {
            AssertNotNull(expected, message + " expected set");
            AssertNotNull(actual, message + " actual set");
            AssertEqual(expected.Count, actual.Count, message + " count");

            int i;

            for (i = 0; i < expected.Count; i++)
            {
                AssertTrue(
                    actual.Contains(expected[i]),
                    message + " missing '" + expected[i] + "'");
            }
        }

        private static void TestApplicableStyleSelectsFallbackBeforeConstruction()
        {
            const string markup =
                "<StackPanel>" +
                "  <StackPanel.Resources>" +
                "    <Style TargetType='Label'>" +
                "      <Setter Property='ForeColor' Value='Blue' />" +
                "    </Style>" +
                "  </StackPanel.Resources>" +
                "  <ItemsControl Name='Rows' Virtualizing='false' " +
                "      ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Label Name='StyledLabel' Text='{Binding Text}' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</StackPanel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList items = new ArrayList();
                items.Add(new BlueprintRow("styled"));
                host.SetItems(items);

                AssertTrue(
                    runtime.CompiledControlBlueprintBuildCount == 0L,
                    "applicable style rejected the blueprint before build");

                Label label = FindByName(host, "StyledLabel") as Label;
                AssertNotNull(label, "fallback renderer built the styled row");
                AssertEqual(Color.Blue, label.ForeColor, "style semantics");
                AssertEqual("styled", label.Text, "styled fallback binding");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestPropertyElementSelectsFallbackBeforeConstruction()
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Name='FallbackLabel'>" +
                "      <Label.Text>{Binding Text}</Label.Text>" +
                "    </Label>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            BlueprintRow row = new BlueprintRow("fallback");
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList items = new ArrayList();
                items.Add(row);
                host.SetItems(items);

                AssertTrue(
                    runtime.CompiledControlBlueprintBuildCount == 0L,
                    "property element rejected the whole blueprint before build");

                Label label =
                    FindByName(host, "FallbackLabel") as Label;
                AssertNotNull(label, "fallback renderer still built the row");
                AssertEqual(
                    "fallback",
                    label.Text,
                    "fallback renderer preserved the property-element binding");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void EnsureHandle(Control control)
        {
            if (control != null && !control.IsHandleCreated)
                control.CreateControl();

            DrainMessages();
        }

        private static void DrainMessages()
        {
            int i;

            for (i = 0; i < 8; i++)
                Application.DoEvents();
        }

        private static Control FindByName(
            Control root,
            string name)
        {
            if (root == null)
                return null;

            if (String.Equals(
                    root.Name,
                    name,
                    StringComparison.Ordinal))
            {
                return root;
            }

            int i;

            for (i = 0; i < root.Controls.Count; i++)
            {
                Control found = FindByName(
                    root.Controls[i],
                    name);

                if (found != null)
                    return found;
            }

            return null;
        }

        private static void AssertNotNull(
            object value,
            string message)
        {
            if (value == null)
                throw new InvalidOperationException(message);
        }

        private static void AssertTrue(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message +
                    ": expected '" +
                    expected +
                    "', actual '" +
                    actual +
                    "'.");
            }
        }
    }
}
