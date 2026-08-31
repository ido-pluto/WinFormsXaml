using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    public sealed class PresetBooleanProbeControl : Control
    {
        private bool _active;

        public int ActiveSetCount;

        public bool Active
        {
            get { return _active; }
            set
            {
                ActiveSetCount++;
                _active = value;
            }
        }
    }

    internal static class PresetConditionExpressionTests
    {
        private sealed class CountingBooleanSource : INotifyPropertyChanged
        {
            private bool _value;
            private PropertyChangedEventHandler _propertyChanged;

            public int AddCount;
            public int RemoveCount;

            public CountingBooleanSource(bool value)
            {
                _value = value;
            }

            public bool Value
            {
                get { return _value; }
                set
                {
                    if (_value == value)
                        return;

                    _value = value;
                    PropertyChangedEventHandler handler = _propertyChanged;

                    if (handler != null)
                    {
                        handler(
                            this,
                            new PropertyChangedEventArgs("Value"));
                    }
                }
            }

            public int SubscriberCount
            {
                get
                {
                    return _propertyChanged == null
                        ? 0
                        : _propertyChanged.GetInvocationList().Length;
                }
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add
                {
                    AddCount++;
                    _propertyChanged += value;
                }
                remove
                {
                    RemoveCount++;
                    _propertyChanged -= value;
                }
            }
        }

        private sealed class RapidSwitchState
        {
            public readonly CountingBooleanSource ThemeFlag;

            public RapidSwitchState()
            {
                ThemeFlag = new CountingBooleanSource(true);
            }
        }

        public static void Run()
        {
            TestSelectedNameExpressionsOnBooleanProperties();
            TestCompoundExpressionAndKeyReactivity();
            TestRapidSwitchDependencyFilteringAndSubscriptionStability();
            TestItemTemplateSelectionReactivity();
            TestUnknownCollectionDiagnostic();
        }

#if PRESET_CONDITION_EXPRESSION_STANDALONE
        public static void Main()
        {
            Run();
            Console.WriteLine(
                "PASS preset selected-name Boolean expressions");
        }
#endif

        private static void TestSelectedNameExpressionsOnBooleanProperties()
        {
            const string markup =
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Light'>" +
                "    <Preset Name='Light'><Set Key='CanEdit' Value='true' /></Preset>" +
                "    <Preset Name='Dark'><Set Key='CanEdit' Value='false' /></Preset>" +
                "    <Preset Name='High Contrast'><Set Key='CanEdit' Value='true' /></Preset>" +
                "  </Presets>" +
                "  <Label Name='LightOnly' Condition='{Preset Theme == light}' />" +
                "  <Button Name='NotDark' Enabled='{Preset Theme != DARK}' />" +
                "  <CheckBox Name='DarkCheck' Checked='{Preset Theme == Dark}' />" +
                "  <Button Name='LightTabStop' TabStop='{Preset !(Theme != Light)}' />" +
                "  <Label Name='QuotedName' Condition='{Preset Theme == &quot;High Contrast&quot;}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                CreateHandle(runtime.RootControl);
                AssertEqual(true, runtime.Get<Label>("LightOnly").Visible,
                    "case-insensitive selected preset comparison");
                AssertEqual(true, runtime.Get<Button>("NotDark").Enabled,
                    "not-equal selected preset comparison");
                AssertEqual(false, runtime.Get<CheckBox>("DarkCheck").Checked,
                    "checked receives initial preset Boolean");
                AssertEqual(true, runtime.Get<Button>("LightTabStop").TabStop,
                    "unary negation and parentheses");
                AssertEqual(false, runtime.Get<Label>("QuotedName").Visible,
                    "quoted preset name initially false");

                runtime.Presets.Select("Theme", "Dark");

                AssertEqual(false, runtime.Get<Label>("LightOnly").Visible,
                    "Condition reacts to selected preset");
                AssertEqual(false, runtime.Get<Button>("NotDark").Enabled,
                    "Enabled reacts to selected preset");
                AssertEqual(true, runtime.Get<CheckBox>("DarkCheck").Checked,
                    "Checked reacts to selected preset");
                AssertEqual(false, runtime.Get<Button>("LightTabStop").TabStop,
                    "TabStop reacts to selected preset");

                runtime.Presets.Select("Theme", "High Contrast");
                AssertEqual(true, runtime.Get<Label>("QuotedName").Visible,
                    "quoted preset name with spaces");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestCompoundExpressionAndKeyReactivity()
        {
            const string markup =
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Light'>" +
                "    <Preset Name='Light'><Set Key='CanEdit' Value='true' /></Preset>" +
                "    <Preset Name='Dark'><Set Key='CanEdit' Value='false' /></Preset>" +
                "  </Presets>" +
                "  <Presets Name='Density' Selected='Compact'>" +
                "    <Preset Name='Compact'><Set Key='Unused' Value='1' /></Preset>" +
                "    <Preset Name='Comfortable'><Set Key='Unused' Value='2' /></Preset>" +
                "  </Presets>" +
                "  <Button Name='Compound' " +
                "      Enabled='{Preset Theme.CanEdit &amp;&amp; (Theme == Light || Density != Compact)}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                CreateHandle(runtime.RootControl);
                Button compound = runtime.Get<Button>("Compound");
                AssertEqual(true, compound.Enabled,
                    "compound selector and preset-key expression");
                runtime.Presets.Select("Density", "Comfortable");
                AssertEqual(true, compound.Enabled,
                    "second referenced collection refreshes expression");

                runtime.Presets.Select("Theme", "Dark");
                AssertEqual(false, compound.Enabled,
                    "selected key value refreshes compound expression");

                runtime.Presets.SetValue(
                    "Theme",
                    "Dark",
                    "CanEdit",
                    true);
                AssertEqual(true, compound.Enabled,
                    "referenced preset key mutation refreshes expression");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestItemTemplateSelectionReactivity()
        {
            const string markup =
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Light'>" +
                "    <Preset Name='Light'><Set Key='Unused' Value='1' /></Preset>" +
                "    <Preset Name='Dark'><Set Key='Unused' Value='2' /></Preset>" +
                "  </Presets>" +
                "  <ItemsControl Name='Rows' Virtualizing='false' ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Label Condition='{Preset Theme == Dark}' Text='{Binding .}' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl items =
                    runtime.Get<XamlRuntime.ItemsControl>("Rows");
                ArrayList rows = new ArrayList();
                rows.Add("Row");
                items.SetItems(rows);
                CreateHandle(runtime.RootControl);

                Label label = FindFirstLabel(items);
                AssertTrue(label != null, "preset expression item label exists");
                AssertEqual(false, label.Visible,
                    "item selector expression initially false");

                runtime.Presets.Select("Theme", "Dark");
                AssertEqual(true, label.Visible,
                    "item selector expression reacts without rebuilding item");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestRapidSwitchDependencyFilteringAndSubscriptionStability()
        {
            XamlRuntime.Register(
                "PresetBooleanProbe",
                typeof(PresetBooleanProbeControl));

            const string markup =
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Light'>" +
                "    <Preset Name='Light'>" +
                "      <Set Key='CanEdit' Value='{Binding ThemeFlag.Value}' />" +
                "      <Set Key='Unrelated' Value='Light value' />" +
                "    </Preset>" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='CanEdit' Value='{Binding ThemeFlag.Value}' />" +
                "      <Set Key='Unrelated' Value='Dark value' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Presets Name='Density' Selected='Compact'>" +
                "    <Preset Name='Compact'><Set Key='Unused' Value='1' /></Preset>" +
                "    <Preset Name='Comfortable'><Set Key='Unused' Value='2' /></Preset>" +
                "  </Presets>" +
                "  <Presets Name='Unrelated' Selected='One'>" +
                "    <Preset Name='One'><Set Key='Value' Value='1' /></Preset>" +
                "    <Preset Name='Two'><Set Key='Value' Value='2' /></Preset>" +
                "  </Presets>" +
                "  <PresetBooleanProbe Name='ThemeSelection' " +
                "      Active='{Preset Theme == Dark}' />" +
                "  <PresetBooleanProbe Name='ThemeKey' " +
                "      Active='{Preset Theme.CanEdit == true}' />" +
                "  <PresetBooleanProbe Name='DensitySelection' " +
                "      Active='{Preset Density == Compact}' />" +
                "</Panel>";

            RapidSwitchState state = new RapidSwitchState();
            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                CreateHandle(runtime.RootControl);

                PresetBooleanProbeControl themeSelection =
                    runtime.Get<PresetBooleanProbeControl>("ThemeSelection");
                PresetBooleanProbeControl themeKey =
                    runtime.Get<PresetBooleanProbeControl>("ThemeKey");
                PresetBooleanProbeControl densitySelection =
                    runtime.Get<PresetBooleanProbeControl>("DensitySelection");
                int themeSelectionCount = themeSelection.ActiveSetCount;
                int themeKeyCount = themeKey.ActiveSetCount;
                int densityCount = densitySelection.ActiveSetCount;
                int sourceAddCount = state.ThemeFlag.AddCount;
                int sourceRemoveCount = state.ThemeFlag.RemoveCount;

                AssertEqual(1, state.ThemeFlag.SubscriberCount,
                    "one shared source subscription before rapid switching");

                runtime.Presets.Select("Unrelated", "Two");
                runtime.Presets.SetValue(
                    "Theme",
                    "Light",
                    "Unrelated",
                    "Changed");

                AssertEqual(themeSelectionCount, themeSelection.ActiveSetCount,
                    "unrelated collection/key skips Theme selector");
                AssertEqual(themeKeyCount, themeKey.ActiveSetCount,
                    "unrelated collection/key skips Theme key expression");
                AssertEqual(densityCount, densitySelection.ActiveSetCount,
                    "unrelated collection/key skips Density selector");

                int i;

                for (i = 0; i < 40; i++)
                {
                    runtime.Presets.Select(
                        "Theme",
                        (i & 1) == 0 ? "Dark" : "Light");
                }

                AssertEqual(
                    themeSelectionCount + 40,
                    themeSelection.ActiveSetCount,
                    "each relevant Theme selection evaluates once");
                AssertEqual(
                    themeKeyCount + 40,
                    themeKey.ActiveSetCount,
                    "referenced Theme key expression evaluates once per selection");
                AssertEqual(
                    densityCount,
                    densitySelection.ActiveSetCount,
                    "Theme switching skips Density expression");
                AssertEqual(1, state.ThemeFlag.SubscriberCount,
                    "rapid switching leaves one source subscription");
                AssertEqual(sourceAddCount, state.ThemeFlag.AddCount,
                    "rapid switching does not add duplicate subscriptions");
                AssertEqual(sourceRemoveCount, state.ThemeFlag.RemoveCount,
                    "rapid switching does not churn the stable subscription");

                themeSelectionCount = themeSelection.ActiveSetCount;
                themeKeyCount = themeKey.ActiveSetCount;
                densityCount = densitySelection.ActiveSetCount;
                state.ThemeFlag.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(themeSelectionCount, themeSelection.ActiveSetCount,
                    "key source notification skips selector-only expression");
                AssertEqual(themeKeyCount + 1, themeKey.ActiveSetCount,
                    "one source event produces one key-expression update");
                AssertEqual(densityCount, densitySelection.ActiveSetCount,
                    "key source notification skips other collection");

                runtime.Presets.Select("Density", "Comfortable");
                AssertEqual(themeSelectionCount, themeSelection.ActiveSetCount,
                    "Density selection skips Theme selector");
                AssertEqual(themeKeyCount + 1, themeKey.ActiveSetCount,
                    "Density selection skips Theme key expression");
                AssertEqual(densityCount + 1, densitySelection.ActiveSetCount,
                    "referenced Density selector evaluates once");
            }
            finally
            {
                DisposeRuntime(runtime);
            }

            AssertEqual(0, state.ThemeFlag.SubscriberCount,
                "runtime disposal removes the source subscription");
            AssertEqual(
                state.ThemeFlag.AddCount,
                state.ThemeFlag.RemoveCount,
                "source subscription adds and removes balance after disposal");
        }

        private static void TestUnknownCollectionDiagnostic()
        {
            Exception failure = null;
            XamlRuntime runtime = null;

            try
            {
                runtime = XamlRuntime.Load(
                    "<Panel><Label Condition='{Preset MissingTheme == Dark}' /></Panel>");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                DisposeRuntime(runtime);
            }

            AssertTrue(failure != null, "unknown preset collection rejected");
            AssertTrue(
                ExceptionContains(failure, "MissingTheme") &&
                ExceptionContains(failure, "was not found"),
                "unknown preset collection diagnostic is clear");
        }

        private static Label FindFirstLabel(Control root)
        {
            if (root == null)
                return null;

            Label label = root as Label;

            if (label != null)
                return label;

            int i;

            for (i = 0; i < root.Controls.Count; i++)
            {
                label = FindFirstLabel(root.Controls[i]);

                if (label != null)
                    return label;
            }

            return null;
        }

        private static void CreateHandle(Control root)
        {
            if (root == null)
                throw new InvalidOperationException("Test root is missing.");

            if (!root.IsHandleCreated)
                root.CreateControl();

            if (!root.IsHandleCreated)
            {
                IntPtr handle = root.Handle;

                if (handle == IntPtr.Zero)
                    throw new InvalidOperationException("Test root handle failed.");
            }
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

                AssertTrue(reached, "reactive callback queue drained");
            }
        }

        private static bool ExceptionContains(Exception failure, string value)
        {
            while (failure != null)
            {
                if (failure.Message != null &&
                    failure.Message.IndexOf(
                        value,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                failure = failure.InnerException;
            }

            return false;
        }

        private static void DisposeRuntime(XamlRuntime runtime)
        {
            if (runtime == null)
                return;

            Control root = runtime.RootControl;
            runtime.Dispose();

            if (root != null && !root.IsDisposed)
                root.Dispose();
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message + ". Expected '" +
                    expected + "', got '" + actual + "'.");
            }
        }
    }
}
