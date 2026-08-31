using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    public sealed class PresetAuditNonControlRoot
    {
        private string _text;

        public string Text
        {
            get { return _text; }
            set { _text = value; }
        }
    }

    public sealed class PresetAuditRetryControl : Control
    {
        private string _value;

        public bool RejectUpdated = true;
        public int UpdatedAttempts;

        public string Value
        {
            get { return _value; }
            set
            {
                if (String.Equals(
                    value,
                    "Updated",
                    StringComparison.Ordinal))
                {
                    UpdatedAttempts++;

                    if (RejectUpdated)
                    {
                        throw new InvalidOperationException(
                            "Preset retry fixture rejected Updated.");
                    }
                }

                _value = value;
            }
        }
    }

    internal sealed class PresetAuditFunctionState
    {
        public int CallCount;

        public string GetCaption()
        {
            CallCount++;
            return "Caption " + CallCount;
        }
    }

    internal sealed class PresetIndexState
    {
        public string OrdinaryText = "Ordinary";
        public ArrayList Items = new ArrayList();

        public PresetIndexState()
        {
            Items.Add("Row");
        }
    }

    internal sealed class PresetUnsetBindingState
    {
        public readonly PropertyBinding<string> Accent;

        public PresetUnsetBindingState(string accent)
        {
            Accent = new PropertyBinding<string>(accent);
        }
    }

    internal sealed class TransparentFormBackgroundState
    {
        public readonly PropertyBinding<Color> Background;

        public TransparentFormBackgroundState(Color background)
        {
            Background = new PropertyBinding<Color>(background);
        }
    }

    internal sealed class ThrowingPresetEqualsValue
    {
        public int CallCount;

        public override bool Equals(object value)
        {
            CallCount++;
            throw new InvalidOperationException(
                "Preset import invoked a stored value's Equals method.");
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }

    internal static class PresetAuditRegressionTests
    {
        private delegate void TestAction();

        public static void Run()
        {
            TestChangedSubscribersAreIsolated();
            TestChangedSubscriberSnapshotIsReused();
            TestReentrantChangedSubscribersAreIsolated();
            TestDeferredChangedSubscribersAreIsolated();
            TestNonControlRootRefreshesSynchronously();
            TestFailedPresetRefreshRetainsExplicitRetry();
            TestPresetRefreshSkipsUnrelatedComponentProperties();
            TestPresetDependentIndexesSkipOrdinaryCandidates();
            TestPresetDependencyMemoHandlesCycles();
            TestPresetResolutionFallbackOrder();
            TestMarkupPresetResolutionUsesOnlySelectedAndDefault();
            TestQualifiedFrameworkColors();
            TestTransparentFormBackgroundRestoresNativeDefault();
            TestMissingSelectedPresetValuesRestoreTargets();
            TestDarkOnlyValuesResetFromInitiallySelectedDarkPreset();
            TestMissingPresetBackgroundRestoresCapturedBaseline();
            TestMissingPresetValuesRestoreItemTemplates(false);
            TestMissingPresetValuesRestoreItemTemplates(true);
            TestMissingPresetBackgroundRestoresItemBaseline(false);
            TestMissingPresetBackgroundRestoresItemBaseline(true);
            TestMissingBindingValuedPresetDetachesAndReattaches();
            TestXmlImportDoesNotInvokeStoredEquals();
            TestRemovedPresetHandlesAreRetired();
            TestStrictPresetImportStructure();
            TestIdenticalReplacePreservesIdentity();
        }

        private static void TestQualifiedFrameworkColors()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel Name='Surface' " +
                "BackColor='SystemColors.Control'>" +
                "  <Presets Name='Theme' Selected='Active'>" +
                "    <Preset Name='Active'>" +
                "      <Set Key='Accent' Value='Color.Red' />" +
                "      <Set Key='Text' " +
                "Value='System.Drawing.SystemColors.ControlText' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Label Name='Target' " +
                "BackColor='System.Drawing.Color.Transparent' " +
                "ForeColor='{Preset Theme.Text}' />" +
                "  <Label Name='Accent' " +
                "ForeColor='{Preset Theme.Accent}' />" +
                "</Panel>");

            try
            {
                Panel surface = runtime.Get<Panel>("Surface");
                Label target = runtime.Get<Label>("Target");
                Label accent = runtime.Get<Label>("Accent");

                AssertTrue(
                    surface.BackColor.ToArgb() ==
                        SystemColors.Control.ToArgb() &&
                    target.BackColor.ToArgb() ==
                        Color.Transparent.ToArgb() &&
                    target.ForeColor.ToArgb() ==
                        SystemColors.ControlText.ToArgb() &&
                    accent.ForeColor.ToArgb() == Color.Red.ToArgb(),
                    "qualified Color and SystemColors values work in literals and presets");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestTransparentFormBackgroundRestoresNativeDefault()
        {
            Color expectedDefault;
            Form reference = new Form();

            try
            {
                expectedDefault = reference.BackColor;
            }
            finally
            {
                reference.Dispose();
            }

            XamlRuntime literalRuntime = XamlRuntime.Load(
                "<Form Name='Target' Background='Transparent' />");

            try
            {
                Form literalTarget = literalRuntime.Get<Form>("Target");

                AssertTrue(
                    literalTarget.BackColor.ToArgb() ==
                        expectedDefault.ToArgb(),
                    "transparent Form background restores the native default");
            }
            finally
            {
                literalRuntime.Dispose();
            }

            TransparentFormBackgroundState state =
                new TransparentFormBackgroundState(Color.Red);
            XamlRuntime boundRuntime = XamlRuntime.Load(
                "<Form Name='Target' " +
                "Background='{Binding Background}' />",
                state);

            try
            {
                Form boundTarget = boundRuntime.Get<Form>("Target");

                AssertTrue(
                    boundTarget.BackColor.ToArgb() ==
                        Color.Red.ToArgb(),
                    "bound Form background initially applies an opaque color");

                CreateControlAndDrain(boundTarget);

                if (!boundTarget.IsHandleCreated)
                {
                    IntPtr handle = boundTarget.Handle;
                    AssertTrue(
                        handle != IntPtr.Zero,
                        "reactive Form background test creates its native handle");
                }

                state.Background.Value = Color.Transparent;
                DrainMessages();

                AssertTrue(
                    boundTarget.BackColor.ToArgb() ==
                        expectedDefault.ToArgb(),
                    "reactive transparent Form background restores the native default");

                state.Background.Value = Color.Blue;
                DrainMessages();

                AssertTrue(
                    boundTarget.BackColor.ToArgb() ==
                        Color.Blue.ToArgb(),
                    "an opaque Form background can be applied after the reset");
            }
            finally
            {
                boundRuntime.Dispose();
            }
        }

        private static void TestPresetResolutionFallbackOrder()
        {
            PresetManager withDefault = new PresetManager();
            PresetSet defaultSet = withDefault.AddSet("WithDefault");
            defaultSet.AddPreset("Selected");
            Preset fallback = defaultSet.AddPreset("Fallback");
            Preset unrelated = defaultSet.AddPreset("Unrelated");
            fallback.AddValue("Surface", "Default surface");
            unrelated.AddValue("Other", "Other value");
            defaultSet.SetDefault("Fallback");
            defaultSet.Select("Selected");

            AssertEqual(
                "Default surface",
                withDefault.Resolve("WithDefault", "Surface") as string,
                "selected miss uses the configured default preset");

            unrelated.AddValue("Accent", "Unrelated accent");
            bool configuredDefaultMissThrew = false;

            try
            {
                withDefault.Resolve("WithDefault", "Accent");
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                configuredDefaultMissThrew = true;
            }

            AssertTrue(
                configuredDefaultMissThrew,
                "configured default miss does not search unrelated presets");

            PresetManager imported = new PresetManager();
            imported.LoadXml(
                "<Presets Name='Imported' Selected='Selected'>" +
                "  <Preset Name='Selected' />" +
                "  <Preset Name='First'>" +
                "    <Set Key='Surface' Value='First value' />" +
                "  </Preset>" +
                "</Presets>");
            imported.LoadXml(
                "<Presets Name='Imported'>" +
                "  <Preset Name='Later'>" +
                "    <Set Key='Surface' Value='Later value' />" +
                "  </Preset>" +
                "</Presets>");

            object unresolvedValue;
            bool noDefaultResolved = imported.TryResolve(
                "Imported",
                "Surface",
                out unresolvedValue);

            AssertTrue(
                !noDefaultResolved && unresolvedValue == null,
                "TryResolve does not scan declared presets without a default");

            bool noDefaultMissThrew = false;

            try
            {
                imported.Resolve("Imported", "Surface");
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                noDefaultMissThrew = true;
            }

            AssertTrue(
                noDefaultMissThrew,
                "strict Resolve throws instead of scanning declared presets");

            bool missingEverywhereThrew = false;

            try
            {
                imported.Resolve("Imported", "Missing");
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                missingEverywhereThrew = true;
            }

            AssertTrue(
                missingEverywhereThrew,
                "missing key in every preset fails visibly");
        }

        private static void TestMarkupPresetResolutionUsesOnlySelectedAndDefault()
        {
            PresetManager manager = new PresetManager();
            manager.LoadXml(
                "<Presets Name='Theme' Selected='Selected' Default='Base'>" +
                "  <Preset Name='Selected'>" +
                "    <Set Key='SelectedValue' Value='Selected value' />" +
                "    <Set Key='ChangingValue' Value='Resolved value' />" +
                "  </Preset>" +
                "  <Preset Name='Base'>" +
                "    <Set Key='DefaultValue' Value='Default value' />" +
                "  </Preset>" +
                "  <Preset Name='Missing' />" +
                "  <Preset Name='Unrelated'>" +
                "    <Set Key='UnresolvedValue' Value='Wrong value' />" +
                "    <Set Key='ChangingValue' Value='Wrong value' />" +
                "  </Preset>" +
                "</Presets>");
            manager.LoadXml(
                "<Presets Name='NoDefault' Selected='Selected'>" +
                "  <Preset Name='Selected' />" +
                "  <Preset Name='Unrelated'>" +
                "    <Set Key='Value' Value='Wrong value' />" +
                "  </Preset>" +
                "</Presets>");
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style TargetType='Button'>" +
                "      <Setter Property='Text' " +
                "              Value='{Preset Theme.ChangingValue}' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='SelectedConsumer' " +
                "         Text='{Preset Theme.SelectedValue}' />" +
                "  <Label Name='DefaultConsumer' " +
                "         Text='{Preset Theme.DefaultValue}' />" +
                "  <Label Name='UnresolvedConsumer' " +
                "         Text='{Preset Theme.UnresolvedValue}' />" +
                "  <Label Name='ChangingConsumer' " +
                "         Text='{Preset Theme.ChangingValue}' />" +
                "  <Label Name='NoDefaultConsumer' " +
                "         Text='{Preset NoDefault.Value}' />" +
                "  <Button Name='StyledChangingConsumer' />" +
                "</Panel>",
                null,
                null,
                manager);

            try
            {
                Label selected = runtime.Get<Label>("SelectedConsumer");
                Label fallback = runtime.Get<Label>("DefaultConsumer");
                Label unresolved = runtime.Get<Label>("UnresolvedConsumer");
                Label changing = runtime.Get<Label>("ChangingConsumer");
                Label noDefault = runtime.Get<Label>("NoDefaultConsumer");
                Button styledChanging =
                    runtime.Get<Button>("StyledChangingConsumer");

                AssertEqual(
                    "Selected value",
                    selected.Text,
                    "markup reads the selected preset first");
                AssertEqual(
                    "Default value",
                    fallback.Text,
                    "markup reads the configured default after a selected miss");
                AssertEqual(
                    String.Empty,
                    unresolved.Text,
                    "markup does not scan an unrelated preset");
                AssertEqual(
                    "Resolved value",
                    changing.Text,
                    "markup initially applies a resolved preset value");
                AssertEqual(
                    "Resolved value",
                    styledChanging.Text,
                    "a style setter initially applies a resolved preset value");
                AssertEqual(
                    String.Empty,
                    noDefault.Text,
                    "markup without a default does not scan declared presets");

                Control root = runtime.RootControl;

                if (!root.IsHandleCreated)
                    root.CreateControl();

                if (!root.IsHandleCreated)
                {
                    IntPtr handle = root.Handle;
                    AssertTrue(
                        handle != IntPtr.Zero,
                        "markup preset baseline test creates the root handle");
                }

                manager.Select("Theme", "Missing");

                AssertEqual(
                    String.Empty,
                    selected.Text,
                    "an unresolved selected value restores the native baseline");
                AssertEqual(
                    "Default value",
                    fallback.Text,
                    "the configured default remains available after selection");
                AssertEqual(
                    String.Empty,
                    changing.Text,
                    "a resolved preset becoming unresolved restores the baseline");
                AssertEqual(
                    String.Empty,
                    styledChanging.Text,
                    "an unresolved style setter removes the previous preset value");

                manager.Select("Theme", "Selected");

                AssertEqual(
                    "Resolved value",
                    changing.Text,
                    "a preset value can resolve again after the baseline restore");
                AssertEqual(
                    "Resolved value",
                    styledChanging.Text,
                    "a style setter resolves again after its value was unset");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestMissingSelectedPresetValuesRestoreTargets()
        {
            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='DarkStyle' TargetType='Button'>" +
                "      <Setter Property='Background' " +
                "              Value='{Preset Theme.StyleBackground}' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Presets Name='Theme' Selected='Dark' Default='Base'>" +
                "    <Preset Name='Base'>" +
                "      <Set Key='DefaultText' Value='Base value' />" +
                "    </Preset>" +
                "    <Preset Name='Light' />" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='DirectText' Value='Dark text' />" +
                "      <Set Key='DirectBackground' Value='Red' />" +
                "      <Set Key='StyleBackground' Value='Blue' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Label Name='DirectText' " +
                "         Text='{Preset Theme.DirectText}' />" +
                "  <Label Name='DefaultText' " +
                "         Text='{Preset Theme.DefaultText}' />" +
                "  <Button Name='DirectMapped' " +
                "          Background='{Preset Theme.DirectBackground}' />" +
                "  <Button Name='StyledMapped' Style='DarkStyle' />" +
                "  <Button Name='NativeReference' />" +
                "</Panel>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                Label directText = runtime.Get<Label>("DirectText");
                Label defaultText = runtime.Get<Label>("DefaultText");
                Button directMapped =
                    runtime.Get<Button>("DirectMapped");
                Button styledMapped =
                    runtime.Get<Button>("StyledMapped");
                Button nativeReference =
                    runtime.Get<Button>("NativeReference");

                AssertEqual(
                    "Dark text",
                    directText.Text,
                    "the selected preset initially supplies its direct value");
                AssertEqual(
                    "Base value",
                    defaultText.Text,
                    "a selected miss initially uses the configured default");
                AssertTrue(
                    directMapped.BackColor.ToArgb() == Color.Red.ToArgb(),
                    "the selected preset initially supplies a mapped value");
                AssertTrue(
                    styledMapped.BackColor.ToArgb() == Color.Blue.ToArgb(),
                    "the selected preset initially supplies a style value");

                CreateControlAndDrain(runtime.RootControl);
                runtime.Presets.Select("Theme", "Light");
                DrainMessages();

                AssertEqual(
                    String.Empty,
                    directText.Text,
                    "a direct selected-only value is unset in Light");
                AssertEqual(
                    "Base value",
                    defaultText.Text,
                    "Light still resolves a key from the configured default");
                AssertTrue(
                    directMapped.BackColor.ToArgb() ==
                        nativeReference.BackColor.ToArgb() &&
                    directMapped.UseVisualStyleBackColor ==
                        nativeReference.UseVisualStyleBackColor,
                    "an absent mapped value restores the native Button state");
                AssertTrue(
                    styledMapped.BackColor.ToArgb() ==
                        nativeReference.BackColor.ToArgb() &&
                    styledMapped.UseVisualStyleBackColor ==
                        nativeReference.UseVisualStyleBackColor,
                    "an absent style value restores the native Button state");

                runtime.Presets.Select("Theme", "Dark");
                runtime.Presets.Select("Theme", "Light");
                DrainMessages();

                AssertEqual(
                    String.Empty,
                    directText.Text,
                    "repeated Dark-to-Light switching cannot retain the old value");
                AssertTrue(
                    directMapped.BackColor.ToArgb() ==
                        nativeReference.BackColor.ToArgb() &&
                    styledMapped.BackColor.ToArgb() ==
                        nativeReference.BackColor.ToArgb(),
                    "repeated switching keeps mapped and styled values unset");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestMissingPresetBackgroundRestoresCapturedBaseline()
        {
            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='LightBaseline' TargetType='Label'>" +
                "      <Setter Property='Background' Value='Yellow' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Presets Name='Theme' Selected='Light' Default='Light'>" +
                "    <Preset Name='Light' />" +
                "    <Preset Name='OtherLight' />" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='Background' Value='#23272E' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Label Name='Target' Style='LightBaseline' " +
                "         Background='{Preset Theme.Background}' />" +
                "</Panel>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                Label target = runtime.Get<Label>("Target");

                AssertTrue(
                    target.BackColor.ToArgb() == Color.Yellow.ToArgb(),
                    "a missing initial preset key reveals the lower style");

                CreateControlAndDrain(runtime.RootControl);
                runtime.Presets.Select("Theme", "Dark");
                DrainMessages();

                AssertTrue(
                    target.BackColor.ToArgb() ==
                        Color.FromArgb(0x23, 0x27, 0x2E).ToArgb(),
                    "the Dark preset overlays the lower style");

                runtime.Presets.Select("Theme", "Light");
                DrainMessages();

                AssertTrue(
                    target.BackColor.ToArgb() == Color.Yellow.ToArgb(),
                    "removing the Dark-only key restores the captured style");

                runtime.Presets.Select("Theme", "OtherLight");
                DrainMessages();

                AssertTrue(
                    target.BackColor.ToArgb() == Color.Yellow.ToArgb(),
                    "another missing variant cannot reset the restored style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestDarkOnlyValuesResetFromInitiallySelectedDarkPreset()
        {
            const string markup =
                "<Form Name='TargetForm' " +
                "      Background='{Preset Theme.Background}'>" +
                "  <Presets Name='Theme' Selected='Dark'>" +
                "    <Preset Name='Light' />" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='Background' Value='#23272E' />" +
                "      <Set Key='Foreground' Value='Red' />" +
                "      <Set Key='Caption' Value='Dark caption' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Border Name='TargetBorder' " +
                "          Background='{Preset Theme.Background}'>" +
                "    <Label Name='TargetLabel' " +
                "           Text='{Preset Theme.Caption}' " +
                "           Foreground='{Preset Theme.Foreground}' />" +
                "  </Border>" +
                "</Form>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                Form form = runtime.Get<Form>("TargetForm");
                Control border = runtime.Get<Control>("TargetBorder");
                Label label = runtime.Get<Label>("TargetLabel");
                Form nativeForm = new Form();
                Label nativeLabel = new Label();

                try
                {
                    AssertTrue(
                        form.BackColor.ToArgb() ==
                            Color.FromArgb(0x23, 0x27, 0x2E).ToArgb() &&
                        border.BackColor.ToArgb() ==
                            Color.FromArgb(0x23, 0x27, 0x2E).ToArgb() &&
                        label.ForeColor.ToArgb() == Color.Red.ToArgb() &&
                        label.Text == "Dark caption",
                        "the initially selected Dark preset applies all values");

                    IntPtr handle = form.Handle;
                    CreateControlAndDrain(runtime.RootControl);
                    runtime.Presets.Select("Theme", "Light");
                    DrainMessages();

                    AssertTrue(
                        form.BackColor.ToArgb() ==
                            nativeForm.BackColor.ToArgb(),
                        "the Dark-only Form background is removed");
                    AssertTrue(
                        border.BackColor.ToArgb() ==
                            SystemColors.Control.ToArgb(),
                        "the Dark-only Border background is removed");
                    AssertTrue(
                        label.ForeColor.ToArgb() ==
                            nativeLabel.ForeColor.ToArgb(),
                        "the Dark-only foreground is removed");
                    AssertEqual(
                        String.Empty,
                        label.Text,
                        "the Dark-only text is removed");

                    runtime.Presets.Select("Theme", "Dark");
                    runtime.Presets.Select("Theme", "Light");
                    DrainMessages();

                    AssertTrue(
                        form.BackColor.ToArgb() ==
                            nativeForm.BackColor.ToArgb() &&
                        border.BackColor.ToArgb() ==
                            SystemColors.Control.ToArgb() &&
                        label.ForeColor.ToArgb() ==
                            nativeLabel.ForeColor.ToArgb() &&
                        label.Text == String.Empty,
                        "repeated Dark-to-Light transitions cannot retain values");
                }
                finally
                {
                    nativeForm.Dispose();
                    nativeLabel.Dispose();
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestMissingPresetValuesRestoreItemTemplates(
            bool virtualizing)
        {
            string markup =
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Dark'>" +
                "    <Preset Name='Light' />" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='ItemBackground' Value='Red' />" +
                "      <Set Key='ItemStyleBackground' Value='Blue' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <ItemsControl Name='Rows' Width='180' Height='60' " +
                "      AutoScroll='true' ProgressiveRendering='false' " +
                "      Virtualizing='" +
                    (virtualizing ? "true" : "false") + "' " +
                "      VirtualizationThreshold='1' OverscanItems='0' " +
                "      FixedItemSize='24'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Panel Height='24'>" +
                "        <Panel.Resources>" +
                "          <Style Key='ItemStyle' TargetType='Label'>" +
                "            <Setter Property='Background' " +
                "              Value='{Preset Theme.ItemStyleBackground}' />" +
                "          </Style>" +
                "        </Panel.Resources>" +
                "        <Label Name='PresetItem' Height='24' " +
                "               Background='{Preset Theme.ItemBackground}' />" +
                "        <Label Name='StyledItem' Height='24' " +
                "               Style='ItemStyle' />" +
                "        <Label Name='NativeItem' Height='24' />" +
                "      </Panel>" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ArrayList values = new ArrayList();
                values.Add("Row");
                rows.CreateControl();
                rows.SetItems(values);

                AssertTrue(
                    rows.IsVirtualizing == virtualizing,
                    virtualizing
                        ? "the virtual preset fixture activates virtualization"
                        : "the nonvirtual preset fixture stays nonvirtual");

                Label label = FindNamedLabel(rows, "PresetItem");
                Label styled = FindNamedLabel(rows, "StyledItem");
                Label nativeReference =
                    FindNamedLabel(rows, "NativeItem");

                AssertTrue(
                    label != null && styled != null &&
                    nativeReference != null &&
                    label.BackColor.ToArgb() == Color.Red.ToArgb() &&
                    styled.BackColor.ToArgb() == Color.Blue.ToArgb(),
                    (virtualizing ? "virtual" : "nonvirtual") +
                    " item initially receives the selected preset value");

                CreateControlAndDrain(runtime.RootControl);
                runtime.Presets.Select("Theme", "Light");
                DrainMessages();

                Label current = FindNamedLabel(rows, "PresetItem");
                styled = FindNamedLabel(rows, "StyledItem");
                nativeReference = FindNamedLabel(rows, "NativeItem");

                AssertTrue(
                    current != null && styled != null &&
                    nativeReference != null &&
                    current.BackColor.ToArgb() ==
                        nativeReference.BackColor.ToArgb() &&
                    styled.BackColor.ToArgb() ==
                        nativeReference.BackColor.ToArgb(),
                    (virtualizing ? "virtual" : "nonvirtual") +
                    " item removes an absent selected-preset value");

                runtime.Presets.Select("Theme", "Dark");
                runtime.Presets.Select("Theme", "Light");
                DrainMessages();
                current = FindNamedLabel(rows, "PresetItem");
                styled = FindNamedLabel(rows, "StyledItem");
                nativeReference = FindNamedLabel(rows, "NativeItem");

                AssertTrue(
                    current != null && styled != null &&
                    nativeReference != null &&
                    current.BackColor.ToArgb() ==
                        nativeReference.BackColor.ToArgb() &&
                    styled.BackColor.ToArgb() ==
                        nativeReference.BackColor.ToArgb(),
                    (virtualizing ? "virtual" : "nonvirtual") +
                    " item does not retain a repeated Dark value");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestMissingPresetBackgroundRestoresItemBaseline(
            bool virtualizing)
        {
            string markup =
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Light' Default='Light'>" +
                "    <Preset Name='Light' />" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='Background' Value='#23272E' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <ItemsControl Name='Rows' Width='180' Height='60' " +
                "      AutoScroll='true' ProgressiveRendering='false' " +
                "      Virtualizing='" +
                    (virtualizing ? "true" : "false") + "' " +
                "      VirtualizationThreshold='1' OverscanItems='0' " +
                "      FixedItemSize='24'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Panel Height='24'>" +
                "        <Panel.Resources>" +
                "          <Style Key='LightBaseline' TargetType='Label'>" +
                "            <Setter Property='Background' Value='Yellow' />" +
                "          </Style>" +
                "        </Panel.Resources>" +
                "        <Label Name='Target' Height='24' " +
                "          Style='LightBaseline' " +
                "          Background='{Preset Theme.Background}' />" +
                "      </Panel>" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ArrayList values = new ArrayList();
                values.Add("Row");
                rows.CreateControl();
                rows.SetItems(values);

                Label target = FindNamedLabel(rows, "Target");

                AssertTrue(
                    target != null &&
                    target.BackColor.ToArgb() == Color.Yellow.ToArgb(),
                    (virtualizing ? "virtual" : "nonvirtual") +
                    " item starts from its lower style");

                CreateControlAndDrain(runtime.RootControl);
                runtime.Presets.Select("Theme", "Dark");
                DrainMessages();
                target = FindNamedLabel(rows, "Target");

                AssertTrue(
                    target != null &&
                    target.BackColor.ToArgb() ==
                        Color.FromArgb(0x23, 0x27, 0x2E).ToArgb(),
                    (virtualizing ? "virtual" : "nonvirtual") +
                    " item receives the Dark overlay");

                runtime.Presets.Select("Theme", "Light");
                DrainMessages();
                target = FindNamedLabel(rows, "Target");

                AssertTrue(
                    target != null &&
                    target.BackColor.ToArgb() == Color.Yellow.ToArgb(),
                    (virtualizing ? "virtual" : "nonvirtual") +
                    " item restores its captured lower style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestMissingBindingValuedPresetDetachesAndReattaches()
        {
            PresetUnsetBindingState state =
                new PresetUnsetBindingState("Bound base");
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Dark' Default='Base'>" +
                "    <Preset Name='Base'>" +
                "      <Set Key='Caption' Value='{Binding Accent}' />" +
                "    </Preset>" +
                "    <Preset Name='Light' />" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='Caption' Value='Dark literal' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Label Name='Target' Text='{Preset Theme.Caption}' />" +
                "</Panel>",
                state);

            try
            {
                Label target = runtime.Get<Label>("Target");

                AssertEqual(
                    "Dark literal",
                    target.Text,
                    "the selected literal wins over a binding-valued default");
                AssertTrue(
                    GetPropertyBindingSubscriberCount(state.Accent) == 0,
                    "the hidden default binding is not subscribed");

                CreateControlAndDrain(runtime.RootControl);
                runtime.Presets.Select("Theme", "Light");
                DrainMessages();

                AssertEqual(
                    "Bound base",
                    target.Text,
                    "a Light miss resolves the binding-valued default");
                AssertTrue(
                    GetPropertyBindingSubscriberCount(state.Accent) == 1,
                    "the effective default binding has one subscription");

                state.Accent.Value = "Updated base";
                DrainMessages();
                AssertEqual(
                    "Updated base",
                    target.Text,
                    "the fallback binding remains reactive");

                runtime.Presets.Select("Theme", "Dark");
                DrainMessages();
                AssertEqual(
                    "Dark literal",
                    target.Text,
                    "the selected literal replaces the fallback binding");
                AssertTrue(
                    GetPropertyBindingSubscriberCount(state.Accent) == 0,
                    "the inactive fallback binding detaches");

                runtime.Presets.Select("Theme", "Light");
                DrainMessages();
                AssertEqual(
                    "Updated base",
                    target.Text,
                    "the fallback binding reattaches with its latest value");
                AssertTrue(
                    GetPropertyBindingSubscriberCount(state.Accent) == 1,
                    "reattachment does not duplicate the binding subscription");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertTrue(
                GetPropertyBindingSubscriberCount(state.Accent) == 0,
                "runtime disposal releases the fallback binding subscription");
        }

        private static void TestChangedSubscriberSnapshotIsReused()
        {
            PresetManager manager = new PresetManager();
            EventHandler<PresetChangedEventArgs> first =
                delegate(object sender, PresetChangedEventArgs e)
                {
                };
            EventHandler<PresetChangedEventArgs> second =
                delegate(object sender, PresetChangedEventArgs e)
                {
                };
            FieldInfo snapshotField =
                typeof(PresetManager).GetField(
                    "_changedSubscribers",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(
                snapshotField != null,
                "preset subscriber snapshot storage is available internally");

            manager.Changed += first;
            manager.Changed += second;

            Delegate[] subscribed =
                snapshotField.GetValue(manager) as Delegate[];

            AssertTrue(
                subscribed != null && subscribed.Length == 2,
                "preset subscriptions publish one cached snapshot");

            manager.AddSet("First");
            AssertTrue(
                Object.ReferenceEquals(
                    subscribed,
                    snapshotField.GetValue(manager)),
                "preset mutation reuses its subscriber snapshot");

            manager.AddSet("Second");
            AssertTrue(
                Object.ReferenceEquals(
                    subscribed,
                    snapshotField.GetValue(manager)),
                "later preset mutation still avoids rebuilding subscribers");

            manager.Changed -= second;
            Delegate[] reduced =
                snapshotField.GetValue(manager) as Delegate[];
            AssertTrue(
                reduced != null &&
                reduced.Length == 1 &&
                !Object.ReferenceEquals(subscribed, reduced),
                "subscription changes replace the cached snapshot");

            manager.Changed -= first;
            AssertTrue(
                snapshotField.GetValue(manager) == null,
                "last removal releases the cached preset snapshot");
        }

        private static void TestRemovedPresetHandlesAreRetired()
        {
            PresetManager manager = new PresetManager();
            PresetSet set = manager.AddSet("Detached");
            Preset removed = set.AddPreset("Removed");
            removed.AddValue("Value", "Initial");
            int changeCount = 0;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    changeCount++;
                };

            AssertTrue(
                set.RemovePreset("Removed"),
                "preset removal succeeds");
            AssertTrue(
                changeCount == 1,
                "preset removal publishes one change");

            ExpectInvalidOperation(
                delegate
                {
                    removed.SetValue("Value", "Stale");
                },
                "removed preset rejects stale mutation");
            AssertTrue(
                changeCount == 1,
                "removed preset cannot publish a stale change");

            Preset retained = set.AddPreset("Retained");
            retained.AddValue("Value", "Live");
            int beforeSetRemoval = changeCount;

            AssertTrue(
                manager.RemoveSet("Detached"),
                "preset-set removal succeeds");

            ExpectInvalidOperation(
                delegate
                {
                    set.AddPreset("Stale");
                },
                "removed preset set rejects stale mutation");
            ExpectInvalidOperation(
                delegate
                {
                    retained.SetValue("Value", "Stale");
                },
                "preset retained from a removed set is retired");
            AssertTrue(
                changeCount == beforeSetRemoval + 1,
                "removed set and children cannot publish stale changes");
        }

        private static void TestStrictPresetImportStructure()
        {
            PresetManager manager = CreateThemeManager("Initial");
            int changeCount = 0;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    changeCount++;
                };

            ExpectInvalidOperation(
                delegate
                {
                    manager.LoadXml(
                        "<Presets Name='Theme' Seleced='Current'>" +
                        " <Preset Name='Current'>" +
                        "  <Set Key='Surface' Value='Wrong' />" +
                        " </Preset>" +
                        "</Presets>");
                },
                "unknown preset-set attribute is rejected");
            ExpectInvalidOperation(
                delegate
                {
                    manager.LoadXml(
                        "<Presets Name='Theme'>" +
                        " <Preset Name='Current'>" +
                        "  <Sett Key='Surface' Value='Wrong' />" +
                        " </Preset>" +
                        "</Presets>");
                },
                "unknown preset child element is rejected");
            ExpectInvalidOperation(
                delegate
                {
                    manager.LoadXml(
                        "<PresetDocument>" +
                        " <Presets Name='Other' />" +
                        " <Unexpected />" +
                        "</PresetDocument>");
                },
                "unknown preset-document child is rejected");

            AssertEqual(
                "Initial",
                manager.Resolve("Theme", "Surface") as string,
                "rejected preset imports leave live values untouched");
            AssertTrue(
                !manager.Contains("Other") && changeCount == 0,
                "rejected preset imports are transactional and silent");

            manager.LoadXml(
                "<Presets Name='SchemaMetadata' " +
                " xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'" +
                " xsi:noNamespaceSchemaLocation='WinFormsXaml.xsd'>" +
                " <Preset Name='Current'>" +
                "  <Set Key='Value' Value='Accepted' />" +
                " </Preset>" +
                "</Presets>");

            AssertEqual(
                "Accepted",
                manager.Resolve("SchemaMetadata", "Value") as string,
                "schema-association metadata remains valid preset XML");
        }

        private static void TestIdenticalReplacePreservesIdentity()
        {
            const string InitialXml =
                "<Presets Name='Theme' Selected='Current'>" +
                " <Preset Name='Current'>" +
                "  <Set Key='Surface' Value='Initial' />" +
                " </Preset>" +
                "</Presets>";
            PresetManager manager = new PresetManager();
            manager.LoadXml(InitialXml);
            PresetSet originalSet = manager["Theme"];
            Preset originalPreset = originalSet["Current"];
            int changeCount = 0;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    changeCount++;
                };

            manager.LoadXml(InitialXml, PresetImportMode.Replace);

            AssertTrue(
                Object.ReferenceEquals(originalSet, manager["Theme"]) &&
                Object.ReferenceEquals(
                    originalPreset,
                    manager["Theme"]["Current"]),
                "identical replace preserves set and preset identity");
            AssertTrue(
                changeCount == 0,
                "identical replace does not publish a broad refresh");

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Current'>" +
                " <Preset Name='Current'>" +
                "  <Set Key='Surface' Value='Changed' />" +
                " </Preset>" +
                "</Presets>",
                PresetImportMode.Replace);

            AssertTrue(
                !Object.ReferenceEquals(originalSet, manager["Theme"]) &&
                changeCount == 1,
                "changed replace swaps the set and publishes once");
            ExpectInvalidOperation(
                delegate
                {
                    originalPreset.SetValue("Surface", "Stale");
                },
                "replaced preset rejects stale mutation");
            AssertTrue(
                changeCount == 1,
                "replaced preset cannot trigger another broad refresh");
        }

        private static void TestChangedSubscribersAreIsolated()
        {
            PresetManager manager = new PresetManager();
            int firstCount = 0;
            int secondCount = 0;
            int thirdCount = 0;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    firstCount++;
                    throw new InvalidOperationException("first subscriber");
                };
            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    secondCount++;
                    throw new ApplicationException("second subscriber");
                };
            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    thirdCount++;
                };

            Exception failure = null;

            try
            {
                manager.AddSet("Direct");
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure is InvalidOperationException &&
                failure.Message == "first subscriber",
                "direct notification rethrows the first subscriber failure");
            AssertTrue(
                firstCount == 1 && secondCount == 1 && thirdCount == 1,
                "direct notification reaches every subscriber");
            AssertTrue(
                manager.Contains("Direct"),
                "subscriber failure does not undo the completed mutation");
        }

        private static void TestDeferredChangedSubscribersAreIsolated()
        {
            PresetManager manager = new PresetManager();
            int firstCount = 0;
            int secondCount = 0;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    firstCount++;
                    throw new InvalidOperationException("deferred subscriber");
                };
            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    secondCount++;
                };

            IDisposable deferral = manager.DeferNotifications();
            manager.AddSet("Deferred");
            Exception failure = null;

            try
            {
                deferral.Dispose();
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure is InvalidOperationException &&
                failure.Message == "deferred subscriber",
                "deferred notification rethrows its first failure");
            AssertTrue(
                firstCount == 1 && secondCount == 1,
                "deferred notification reaches every subscriber");
        }

        private static void TestReentrantChangedSubscribersAreIsolated()
        {
            PresetManager manager = CreateThemeManager("Initial");
            ArrayList observedKeys = new ArrayList();
            bool nestedMutation = false;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    if (!nestedMutation && e.Key == "Surface")
                    {
                        nestedMutation = true;
                        manager.SetValue(
                            "Theme",
                            "Current",
                            "Accent",
                            "Blue");
                        throw new InvalidOperationException(
                            "reentrant outer subscriber");
                    }
                };
            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    observedKeys.Add(e.Key);
                };

            Exception failure = null;

            try
            {
                manager.SetValue(
                    "Theme",
                    "Current",
                    "Surface",
                    "Updated");
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure is InvalidOperationException &&
                failure.Message == "reentrant outer subscriber",
                "reentrant notification rethrows the outer subscriber failure");
            AssertTrue(
                observedKeys.Count == 2 &&
                String.Equals(
                    observedKeys[0] as string,
                    "Accent",
                    StringComparison.Ordinal) &&
                String.Equals(
                    observedKeys[1] as string,
                    "Surface",
                    StringComparison.Ordinal),
                "later subscribers observe nested and outer mutations");
            AssertEqual(
                "Blue",
                manager.Resolve("Theme", "Accent") as string,
                "reentrant mutation remains committed");
        }

        private static void TestNonControlRootRefreshesSynchronously()
        {
            XamlRuntime.Register(
                "PresetAuditNonControlRoot",
                typeof(PresetAuditNonControlRoot));

            PresetManager manager = CreateThemeManager("Initial");
            XamlRuntime runtime = XamlRuntime.Load(
                "<PresetAuditNonControlRoot " +
                "Text='{Preset Theme.Surface}' />",
                null,
                null,
                manager);

            try
            {
                PresetAuditNonControlRoot root =
                    runtime.Root as PresetAuditNonControlRoot;

                AssertTrue(root != null, "registered non-Control root loads");
                AssertTrue(
                    runtime.RootControl == null,
                    "registered root remains outside the Control hierarchy");
                AssertEqual("Initial", root.Text, "initial preset value");

                manager.SetValue(
                    "Theme",
                    "Current",
                    "Surface",
                    "Updated");

                AssertEqual(
                    "Updated",
                    root.Text,
                    "non-Control root applies a preset change synchronously");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestPresetRefreshSkipsUnrelatedComponentProperties()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ReactiveCard.xml");

            PresetManager manager = CreateThemeManager("Initial");
            PresetAuditFunctionState state =
                new PresetAuditFunctionState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <ReactiveCard Caption='{Function GetCaption()}' />" +
                "  <Label Name='PresetConsumer' " +
                "         Text='{Preset Theme.Surface}' />" +
                "</Panel>",
                state,
                null,
                manager);

            try
            {
                Control root = runtime.RootControl;

                if (!root.IsHandleCreated)
                    root.CreateControl();

                if (!root.IsHandleCreated)
                {
                    IntPtr handle = root.Handle;
                    AssertTrue(
                        handle != IntPtr.Zero,
                        "preset component test creates the root handle");
                }

                int callsAfterLoad = state.CallCount;
                Label consumer =
                    runtime.Get<Label>("PresetConsumer");

                manager.SetValue(
                    "Theme",
                    "Current",
                    "Surface",
                    "Updated");

                AssertEqual(
                    "Updated",
                    consumer.Text,
                    "preset-dependent sibling refreshes");
                AssertTrue(
                    state.CallCount == callsAfterLoad,
                    "preset refresh skips a component property without that dependency");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestPresetDependentIndexesSkipOrdinaryCandidates()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ReactiveCard.xml");
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ForwardingCard.xml");

            PresetManager manager = new PresetManager();
            manager.LoadXml(
                "<Root>" +
                "  <Presets Name='Theme' Selected='Current'>" +
                "    <Preset Name='Current'>" +
                "      <Set Key='Surface' Value='Initial' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Presets Name='Alias' Selected='Current'>" +
                "    <Preset Name='Current'>" +
                "      <Set Key='Surface' " +
                "           Value='{Preset Theme.Surface}' />" +
                "    </Preset>" +
                "  </Presets>" +
                "</Root>");

            StringBuilder markup = new StringBuilder();
            markup.Append("<Panel>");
            int i;

            for (i = 0; i < 24; i++)
            {
                markup.Append("<Label Name='OrdinaryBinding");
                markup.Append(i);
                markup.Append("' Text='{Binding OrdinaryText}' />");
            }

            markup.Append(
                "<Label Name='DirectPreset' " +
                "Text='{Preset Theme.Surface}' />" +
                "<Label Name='NestedPreset' " +
                "Text='{Preset Alias.Surface}' />" +
                "<ForwardingCard Name='CascadeCard' " +
                "OuterCaption='{Preset Theme.Surface}' />");

            for (i = 0; i < 8; i++)
            {
                markup.Append("<ItemsControl Name='OrdinaryItems");
                markup.Append(i);
                markup.Append(
                    "' ItemsSource='{Binding Items}' " +
                    "Virtualizing='false' ProgressiveRendering='false'>" +
                    "<ItemsControl.ItemTemplate>" +
                    "<Label Text='{Binding .}' />" +
                    "</ItemsControl.ItemTemplate>" +
                    "</ItemsControl>");
            }

            markup.Append(
                "<ItemsControl Name='PresetItems' " +
                "ItemsSource='{Binding Items}' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Label Text='{Preset Theme.Surface}' />" +
                "</ItemsControl.ItemTemplate>" +
                "</ItemsControl>" +
                "</Panel>");

            PresetIndexState state = new PresetIndexState();
            XamlRuntime runtime = XamlRuntime.Load(
                markup.ToString(),
                state,
                null,
                manager);
            bool disposed = false;

            try
            {
                Control root = runtime.RootControl;

                if (!root.IsHandleCreated)
                    root.CreateControl();

                if (!root.IsHandleCreated)
                {
                    IntPtr handle = root.Handle;
                    AssertTrue(
                        handle != IntPtr.Zero,
                        "preset index test creates the root handle");
                }

                ArrayList allBindings = GetPrivateArrayList(
                    runtime,
                    "_dynamicPropertyBindings");
                ArrayList presetBindings = GetPrivateArrayList(
                    runtime,
                    "_presetDynamicPropertyBindings");
                ArrayList allItems = GetPrivateArrayList(
                    runtime,
                    "_itemsControls");
                ArrayList presetItems = GetPrivateArrayList(
                    runtime,
                    "_presetItemsControls");

                AssertTrue(
                    allBindings.Count - presetBindings.Count >= 20,
                    "preset binding index excludes many ordinary bindings");
                AssertPresetBindingIndexOrder(
                    allBindings,
                    presetBindings);
                AssertTrue(
                    allItems.Count == 9 && presetItems.Count == 1,
                    "preset item index excludes ordinary ItemTemplates");

                Label direct = runtime.Get<Label>("DirectPreset");
                Label nested = runtime.Get<Label>("NestedPreset");
                Panel cascade = runtime.Get<Panel>("CascadeCard");
                XamlRuntime.ItemsControl ordinaryItems =
                    runtime.Get<XamlRuntime.ItemsControl>("OrdinaryItems0");
                XamlRuntime.ItemsControl dependentItems =
                    runtime.Get<XamlRuntime.ItemsControl>("PresetItems");
                Label ordinaryItem =
                    ordinaryItems.Controls[0] as Label;
                Label dependentItem =
                    dependentItems.Controls[0] as Label;
                Label cascadeLabel =
                    cascade.Controls[0] as Label;

                AssertTrue(
                    ordinaryItem != null &&
                    dependentItem != null &&
                    cascadeLabel != null,
                    "preset index fixtures render their expected labels");

                manager.SetValue(
                    "Theme",
                    "Current",
                    "Surface",
                    "Updated");

                AssertEqual(
                    "Updated",
                    direct.Text,
                    "direct preset binding refreshes from its index");
                AssertEqual(
                    "Updated",
                    nested.Text,
                    "transitive preset binding keeps exact dependency checks");
                AssertEqual(
                    "Updated",
                    dependentItem.Text,
                    "preset ItemTemplate refreshes from its index");
                AssertEqual(
                    "Row",
                    ordinaryItem.Text,
                    "ordinary ItemTemplate remains outside the preset pass");
                AssertEqual(
                    "Updated",
                    cascadeLabel.Text,
                    "preset-derived component properties retain nested cascades");

                MethodInfo setTemplate =
                    typeof(XamlRuntime.ItemsControl).GetMethod(
                        "SetTemplate",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        new Type[]
                        {
                            typeof(XmlElement),
                            typeof(object)
                        },
                        null);
                AssertTrue(
                    setTemplate != null,
                    "internal ItemTemplate replacement hook is available");

                XmlDocument replacement = new XmlDocument();
                replacement.LoadXml(
                    "<Label Text='{Preset Theme.Surface}' />");
                int indexedItemsBeforeReplacement = presetItems.Count;
                setTemplate.Invoke(
                    ordinaryItems,
                    new object[]
                    {
                        replacement.DocumentElement,
                        state
                    });
                AssertTrue(
                    presetItems.Count == indexedItemsBeforeReplacement + 1,
                    "preset index adds a replaced preset ItemTemplate");
                AssertTrue(
                    Object.ReferenceEquals(
                        presetItems[0],
                        ordinaryItems) &&
                    Object.ReferenceEquals(
                        presetItems[1],
                        dependentItems),
                    "replaced ItemTemplate keeps primary registration order");

                replacement.LoadXml("<Label Text='{Binding .}' />");
                setTemplate.Invoke(
                    ordinaryItems,
                    new object[]
                    {
                        replacement.DocumentElement,
                        state
                    });
                AssertTrue(
                    presetItems.Count == indexedItemsBeforeReplacement,
                    "preset index removes a replaced ordinary ItemTemplate");

                int indexedBindingsBeforeDispose = presetBindings.Count;
                direct.Dispose();
                AssertTrue(
                    presetBindings.Count ==
                        indexedBindingsBeforeDispose - 1,
                    "target disposal removes its preset binding index entry");

                dependentItems.Dispose();
                AssertTrue(
                    presetItems.Count == 0,
                    "ItemsControl disposal removes its preset index entry");

                runtime.Dispose();
                disposed = true;
                AssertTrue(
                    allBindings.Count == 0 &&
                    presetBindings.Count == 0 &&
                    allItems.Count == 0 &&
                    presetItems.Count == 0,
                    "runtime disposal clears both primary and preset indexes");
            }
            finally
            {
                if (!disposed)
                    runtime.Dispose();
            }
        }

        private static void TestFailedPresetRefreshRetainsExplicitRetry()
        {
            XamlRuntime.Register(
                "PresetAuditRetryControl",
                typeof(PresetAuditRetryControl));

            PresetManager manager = CreateThemeManager("Initial");
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Name='PendingConsumer' " +
                "         Text='{Preset Theme.Surface}' />" +
                "  <PresetAuditRetryControl Name='RetryTarget' " +
                "         Value='{Preset Theme.Surface}' />" +
                "</Panel>",
                null,
                null,
                manager);

            try
            {
                Control root = runtime.RootControl;

                if (!root.IsHandleCreated)
                    root.CreateControl();

                if (!root.IsHandleCreated)
                {
                    IntPtr handle = root.Handle;
                    AssertTrue(
                        handle != IntPtr.Zero,
                        "preset retry test creates the root handle");
                }

                Label consumer =
                    runtime.Get<Label>("PendingConsumer");
                PresetAuditRetryControl retryTarget =
                    runtime.Get<PresetAuditRetryControl>("RetryTarget");
                Exception failure = null;

                try
                {
                    manager.SetValue(
                        "Theme",
                        "Current",
                        "Surface",
                        "Updated");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                AssertTrue(
                    failure is WinFormsXamlLoadException,
                    "failed preset application surfaces its structured error");
                AssertEqual(
                    "Initial",
                    consumer.Text,
                    "a dependent after the failure remains stale until retry");
                AssertTrue(
                    retryTarget.UpdatedAttempts == 1,
                    "failed preset refresh does not enter an automatic retry loop");

                manager.SetValue(
                    "Theme",
                    "Current",
                    "Surface",
                    "Mutation retry");

                AssertEqual(
                    "Mutation retry",
                    retryTarget.Value,
                    "a later preset mutation receives one fresh retained-scope attempt");
                AssertEqual(
                    "Mutation retry",
                    consumer.Text,
                    "the fresh mutation attempt completes previously skipped dependents");

                failure = null;

                try
                {
                    manager.SetValue(
                        "Theme",
                        "Current",
                        "Surface",
                        "Updated");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                AssertTrue(
                    failure is WinFormsXamlLoadException,
                    "a later failure still surfaces its structured error");
                AssertTrue(
                    retryTarget.UpdatedAttempts == 2,
                    "each failed mutation receives exactly one automatic attempt");

                retryTarget.RejectUpdated = false;
                runtime.ReloadBindings();

                AssertEqual(
                    "Updated",
                    retryTarget.Value,
                    "explicit reload retries the retained failing target");
                AssertEqual(
                    "Updated",
                    consumer.Text,
                    "explicit reload completes dependents skipped after the failure");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestXmlImportDoesNotInvokeStoredEquals()
        {
            PresetManager manager = CreateThemeManager("Initial");
            ThrowingPresetEqualsValue stored =
                new ThrowingPresetEqualsValue();
            manager.SetValue(
                "Theme",
                "Current",
                "Surface",
                stored);

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Current'>" +
                "  <Preset Name='Current'>" +
                "    <Set Key='Surface' Value='Imported' />" +
                "  </Preset>" +
                "</Presets>");

            AssertTrue(
                stored.CallCount == 0,
                "XML string import does not invoke a stored object's Equals");
            AssertEqual(
                "Imported",
                manager.Resolve("Theme", "Surface") as string,
                "XML string replaces a non-string stored value");
        }

        private static void TestPresetDependencyMemoHandlesCycles()
        {
            PresetManager manager = new PresetManager();
            manager.LoadXml(
                "<Root>" +
                "  <Presets Name='Theme' Selected='Current'>" +
                "    <Preset Name='Current'>" +
                "      <Set Key='A' " +
                "           Value='{Preset Theme.B} {Preset Target.Hit}' />" +
                "      <Set Key='B' Value='{Preset Theme.A}' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Presets Name='Target' Selected='Current'>" +
                "    <Preset Name='Current'>" +
                "      <Set Key='Hit' Value='Initial' />" +
                "      <Set Key='Other' Value='Initial' />" +
                "    </Preset>" +
                "  </Presets>" +
                "</Root>");

            PresetChangedEventArgs change = null;
            EventHandler<PresetChangedEventArgs> capture =
                delegate(object sender, PresetChangedEventArgs e)
                {
                    change = e;
                };
            manager.Changed += capture;
            manager.SetValue(
                "Target",
                "Current",
                "Hit",
                "Changed");
            manager.Changed -= capture;

            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel />",
                null,
                null,
                manager);

            try
            {
                FieldInfo memoField = typeof(XamlRuntime).GetField(
                    "_activePresetDependencyMemo",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo depends = typeof(XamlRuntime).GetMethod(
                    "ExpressionDependsOnPreset",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new Type[]
                    {
                        typeof(string),
                        typeof(PresetChangedEventArgs)
                    },
                    null);

                AssertTrue(memoField != null, "preset memo field is available");
                AssertTrue(depends != null, "preset dependency filter is available");
                AssertTrue(change != null, "scoped preset change was captured");

                Hashtable memo = new Hashtable(
                    StringComparer.OrdinalIgnoreCase);
                memoField.SetValue(runtime, memo);

                bool aDepends = InvokePresetDependencyFilter(
                    depends,
                    runtime,
                    "{Preset Theme.A}",
                    change);
                bool bDepends = InvokePresetDependencyFilter(
                    depends,
                    runtime,
                    "{Preset Theme.B}",
                    change);
                bool interpolatedDepends = InvokePresetDependencyFilter(
                    depends,
                    runtime,
                    "before {pReSeT   Theme.A  } after",
                    change);

                AssertTrue(
                    aDepends && bDepends && interpolatedDepends,
                    "range parsing preserves interpolated, case-insensitive, " +
                    "cycle-safe preset dependencies");
                AssertTrue(
                    memo.Count >= 2,
                    "one refresh memo retains transitive preset results");

                PresetChangedEventArgs unrelatedChange = null;
                EventHandler<PresetChangedEventArgs> captureUnrelated =
                    delegate(object sender, PresetChangedEventArgs e)
                    {
                        unrelatedChange = e;
                    };
                manager.Changed += captureUnrelated;
                manager.SetValue(
                    "Target",
                    "Current",
                    "Other",
                    "Changed");
                manager.Changed -= captureUnrelated;

                AssertTrue(
                    unrelatedChange != null,
                    "unrelated scoped preset change was captured");

                Hashtable cycleMemo = new Hashtable(
                    StringComparer.OrdinalIgnoreCase);
                memoField.SetValue(runtime, cycleMemo);
                bool aDependsOnOther = InvokePresetDependencyFilter(
                    depends,
                    runtime,
                    "{Preset Theme.A}",
                    unrelatedChange);
                int entriesAfterA = cycleMemo.Count;
                bool bDependsOnOther = InvokePresetDependencyFilter(
                    depends,
                    runtime,
                    "{Preset Theme.B}",
                    unrelatedChange);

                AssertTrue(
                    !aDependsOnOther && !bDependsOnOther,
                    "unrelated changes remain false through a preset cycle");
                AssertTrue(
                    entriesAfterA >= 2 &&
                    cycleMemo.Count == entriesAfterA,
                    "false cycle results are memoized for later consumers");

                manager.SetValue(
                    "Target",
                    "Current",
                    "Hit",
                    "Changed again");

                AssertTrue(
                    !Object.ReferenceEquals(
                        cycleMemo,
                        memoField.GetValue(runtime)),
                    "reentrant preset mutation invalidates the active dependency memo");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static bool InvokePresetDependencyFilter(
            MethodInfo method,
            XamlRuntime runtime,
            string expression,
            PresetChangedEventArgs change)
        {
            try
            {
                return (bool)method.Invoke(
                    runtime,
                    new object[]
                    {
                        expression,
                        change
                    });
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException == null
                    ? ex
                    : ex.InnerException;
            }
        }

        private static ArrayList GetPrivateArrayList(
            XamlRuntime runtime,
            string fieldName)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(
                field != null,
                "private runtime index '" + fieldName + "' is available");

            ArrayList value = field.GetValue(runtime) as ArrayList;
            AssertTrue(
                value != null,
                "private runtime index '" + fieldName + "' is initialized");
            return value;
        }

        private static void CreateControlAndDrain(Control root)
        {
            if (root != null && !root.IsDisposed && !root.IsHandleCreated)
                root.CreateControl();

            DrainMessages();
        }

        private static void DrainMessages()
        {
            int i;

            for (i = 0; i < 6; i++)
                Application.DoEvents();
        }

        private static Label FindNamedLabel(
            Control root,
            string name)
        {
            if (root == null)
                return null;

            Label label = root as Label;

            if (label != null &&
                String.Equals(
                    label.Name,
                    name,
                    StringComparison.Ordinal))
            {
                return label;
            }

            int i;

            for (i = 0; i < root.Controls.Count; i++)
            {
                label = FindNamedLabel(root.Controls[i], name);

                if (label != null)
                    return label;
            }

            return null;
        }

        private static int GetPropertyBindingSubscriberCount(
            object binding)
        {
            if (binding == null)
                return 0;

            FieldInfo subscribers = binding.GetType().GetField(
                "_valueChangedSubscribers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Delegate[] snapshot = subscribers == null
                ? null
                : subscribers.GetValue(binding) as Delegate[];

            return snapshot == null ? 0 : snapshot.Length;
        }

        private static void AssertPresetBindingIndexOrder(
            ArrayList allBindings,
            ArrayList presetBindings)
        {
            int presetIndex = 0;
            int i;

            for (i = 0; i < allBindings.Count; i++)
            {
                object binding = allBindings[i];
                FieldInfo mayUsePreset = binding.GetType().GetField(
                    "MayUsePreset",
                    BindingFlags.Instance | BindingFlags.Public);

                AssertTrue(
                    mayUsePreset != null,
                    "dynamic binding exposes its internal preset capability");

                if (!(bool)mayUsePreset.GetValue(binding))
                    continue;

                AssertTrue(
                    presetIndex < presetBindings.Count &&
                    Object.ReferenceEquals(
                        binding,
                        presetBindings[presetIndex]),
                    "preset binding index preserves primary binding order");
                presetIndex++;
            }

            AssertTrue(
                presetIndex == presetBindings.Count,
                "preset binding index has no stale or duplicate entries");
        }

        private static PresetManager CreateThemeManager(string surface)
        {
            PresetManager manager = new PresetManager();
            manager.LoadXml(
                "<Presets Name='Theme' Selected='Current'>" +
                "  <Preset Name='Current'>" +
                "    <Set Key='Surface' Value='" + surface + "' />" +
                "  </Preset>" +
                "</Presets>");
            return manager;
        }

        private static void AssertEqual(
            string expected,
            string actual,
            string message)
        {
            if (!String.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    message + ": expected '" + expected +
                    "', actual '" + actual + "'.");
            }
        }

        private static void ExpectInvalidOperation(
            TestAction action,
            string message)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new InvalidOperationException(message + ".");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message + ".");
        }
    }
}
