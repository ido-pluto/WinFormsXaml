using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    public sealed class IncludesBindingTarget
    {
        public readonly PropertyBinding<string> Caption =
            new PropertyBinding<string>("included binding");

        public int IncludedActionClickCount;

        public void IncludedAction_Click(object sender, EventArgs e)
        {
            IncludedActionClickCount++;
        }
    }

    public sealed class ProgrammaticIncludesForm : XmlForm
    {
        public ProgrammaticIncludesForm()
            : base(
                typeof(ProgrammaticIncludesForm).Assembly,
                "WinFormsXaml.Tests.Fixtures.Includes.Hosts.ProgrammaticHost.xml")
        {
            Include("ProgrammaticContent");
            Include("ProgrammaticSecond");
        }

        public Label IncludedLabel
        {
            get { return Get<Label>("ProgrammaticIncluded"); }
        }

        public Label LocalLabel
        {
            get { return Get<Label>("ProgrammaticLocal"); }
        }

        public Label SecondIncludedLabel
        {
            get { return Get<Label>("ProgrammaticSecond"); }
        }

        public void IncludeAfterLoad(string source)
        {
            Include(source);
        }
    }

    public sealed class IncludedItem
    {
        public string Text;

        public IncludedItem(string text)
        {
            Text = text;
        }
    }

    public sealed class ClassCreatedIncludesForm : XmlForm
    {
        public static ClassCreatedIncludesForm LastInstance;

        public ClassCreatedIncludesForm()
        {
            LastInstance = this;
            Include("ProgrammaticContent");
        }

        public Label IncludedLabel
        {
            get { return Get<Label>("ProgrammaticIncluded"); }
        }
    }

    public sealed class IncludesItemsTarget
    {
        public readonly IncludedItem[] Items;

        public IncludesItemsTarget()
        {
            Items = new IncludedItem[]
            {
                new IncludedItem("first included item"),
                new IncludedItem("second included item"),
                new IncludedItem("third included item")
            };
        }
    }

    public sealed class ConditionalIncludedItem
    {
        public readonly PropertyBinding<string> Text;

        public ConditionalIncludedItem(string text)
        {
            Text = new PropertyBinding<string>(text);
        }
    }

    public sealed class CountingIncludedItems : IEnumerable
    {
        private readonly ConditionalIncludedItem[] _items;

        public int EnumerationCount;

        public CountingIncludedItems(ConditionalIncludedItem[] items)
        {
            _items = items;
        }

        public IEnumerator GetEnumerator()
        {
            EnumerationCount++;
            return _items.GetEnumerator();
        }
    }

    public sealed class ConditionalIncludesItemsTarget
    {
        public readonly ConditionalIncludedItem[] Values;
        public readonly CountingIncludedItems Items;

        public ConditionalIncludesItemsTarget()
        {
            Values = new ConditionalIncludedItem[]
            {
                new ConditionalIncludedItem("conditional first"),
                new ConditionalIncludedItem("conditional second")
            };
            Items = new CountingIncludedItems(Values);
        }
    }

    public sealed class IncludedThemeResetItem
    {
        public string Text = "Theme item";
    }

    public sealed class IncludedThemeResetTarget
    {
        public readonly IncludedThemeResetItem[] Items =
            new IncludedThemeResetItem[]
            {
                new IncludedThemeResetItem()
            };
    }

    public sealed class HiddenTabThemeResetTarget
    {
        public readonly ItemsBinding<IncludedThemeResetItem> Items;

        public HiddenTabThemeResetTarget()
        {
            IncludedThemeResetItem[] values =
                new IncludedThemeResetItem[48];
            int i;

            for (i = 0; i < values.Length; i++)
                values[i] = new IncludedThemeResetItem();

            Items = new ItemsBinding<IncludedThemeResetItem>(values);
        }
    }

    internal static class IncludesIntegrationTests
    {
        private const string RegisteredResourcePrefix =
            "WinFormsXaml.Tests.Fixtures.Includes.Registered";
        private const string HostResourcePrefix =
            "WinFormsXaml.Tests.Fixtures.Includes.Hosts.";

        public static void Run()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            XamlRuntime.Register(
                assembly,
                RegisteredResourcePrefix);

            TestRegisteredVisualInsertionBindingsAndEvents(assembly);
            TestIncludedResourcesAndPresetMerge(assembly);
            TestIncludedThemeRemovesDarkOnlyValues(assembly);
            TestIncludedThemeRefreshesHiddenTabItems(false);
            TestIncludedThemeRefreshesHiddenTabItems(true);
            TestSuppliedPresetManagerPrecedence(assembly);
            TestNestedRegisteredIncludes(assembly);
            TestProgrammaticXmlFormInclude();
            TestClassCreatedXmlFormInclude(assembly);
            TestDirectEmbeddedResourceInclude(assembly);
            TestIncludeInsideRegisteredComponent(assembly);
            TestIncludeResourcesAtComponentRoot(assembly);
            TestIncludeInsideItemTemplate(assembly);
            TestConditionalIncludesPreserveIdentityOrderAndScopes(assembly);
            TestConditionalIncludeRegisteredComponent(assembly);
            TestConditionalIncludeInsideItemTemplate(assembly);
            TestStaticConditionalIncludes(assembly);
            TestConditionalIncludeDiagnostics(assembly);
            TestArbitraryResourceScopeMerge(assembly);
            TestRelativeFileInclude();
            TestInvalidFileIncludeFailures();
            TestUnknownIncludeAttributes();
            TestInvalidMergedPresetChild();
            TestIncludedSourceDiagnostics();
        }

#if INCLUDES_INTEGRATION_STANDALONE
        public static void Main()
        {
            Run();
            Console.WriteLine("PASS conditional and reusable XML includes");
        }
#endif

        private static void TestConditionalIncludeInsideItemTemplate(
            Assembly assembly)
        {
            ConditionalIncludesItemsTarget target =
                new ConditionalIncludesItemsTarget();
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ConditionalItemsHost.xml",
                target);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.Get<XamlRuntime.ItemsControl>("ConditionalRows");
                Panel[] roots = new Panel[target.Values.Length];
                Label[] labels = new Label[target.Values.Length];
                int rendered = 0;
                int i;

                for (i = 0; i < rows.Controls.Count; i++)
                {
                    Panel itemRoot = FindControl<Panel>(
                        rows.Controls[i],
                        "ConditionalItemRoot");

                    if (itemRoot == null)
                        continue;

                    roots[rendered] = itemRoot;
                    labels[rendered] = FindControl<Label>(
                        itemRoot,
                        "ConditionalItemText");
                    AssertTrue(labels[rendered] != null,
                        "conditional item include retains nested binding target");
                    AssertEqual(target.Values[rendered].Text.Value,
                        labels[rendered].Text,
                        "conditional item include resolves nested binding");
                    AssertEqual(false, itemRoot.Visible,
                        "conditional item include initially collapses row root");
                    rendered++;
                }

                AssertEqual(target.Values.Length, rendered,
                    "conditional item include realizes every row once");

                int enumerationCount = target.Items.EnumerationCount;
                CreateHandleAndDrainCallbacks(runtime.RootControl);
                runtime.Presets.Select("Feature", "Enabled");
                DrainCallbacks(runtime.RootControl);

                AssertEqual(enumerationCount, target.Items.EnumerationCount,
                    "preset switch does not enumerate or rebuild item source");

                for (i = 0; i < roots.Length; i++)
                {
                    AssertEqual(true, roots[i].Visible,
                        "preset switch activates realized row in place");
                    AssertTrue(FindControl<Panel>(rows, "ConditionalItemRoot") != null,
                        "conditional item root remains attached");
                }

                target.Values[0].Text.Value = "conditional updated";
                DrainCallbacks(runtime.RootControl);

                AssertEqual("conditional updated", labels[0].Text,
                    "nested item binding remains reactive after include activation");
                AssertEqual(roots[0], labels[0].Parent,
                    "conditional item switch preserves realized root identity");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestConditionalIncludeRegisteredComponent(
            Assembly assembly)
        {
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ConditionalComponentHost.xml",
                null);

            try
            {
                Panel root = runtime.RootControl as Panel;
                Panel component =
                    runtime.Get<Panel>("ConditionalComponentInstance");
                Label child =
                    FindControl<Label>(component, "IncludedComponentLabel");

                AssertTrue(root != null,
                    "conditional registered component host root");
                AssertTrue(component != null,
                    "conditional include builds registered component once");
                AssertTrue(child != null,
                    "conditional registered component retains its content");
                AssertEqual(false, component.Visible,
                    "conditional registered component initially collapses");

                CreateHandleAndDrainCallbacks(root);
                runtime.Presets.Select("Feature", "Enabled");
                DrainCallbacks(root);

                AssertEqual(true, component.Visible,
                    "conditional registered component activates in place");
                AssertEqual(component, root.Controls[0],
                    "conditional registered component keeps marker position");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestConditionalIncludesPreserveIdentityOrderAndScopes(
            Assembly assembly)
        {
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ConditionalHost.xml",
                null);

            try
            {
                Panel root = runtime.RootControl as Panel;
                Label before = runtime.Get<Label>("ConditionalBefore");
                Label included = runtime.Get<Label>("ConditionalIncluded");
                Label after = runtime.Get<Label>("ConditionalAfter");
                Label consumer =
                    runtime.Get<Label>("ConditionalStyleConsumer");
                Color includedBaseline = included.BackColor;
                Color consumerBaseline = consumer.BackColor;

                AssertTrue(root != null, "conditional include root");
                AssertEqual(4, root.Controls.Count,
                    "conditional include composes the visual once");
                AssertEqual(before, root.Controls[0],
                    "conditional include preserves leading sibling order");
                AssertEqual(included, root.Controls[1],
                    "conditional include preserves marker position");
                AssertEqual(after, root.Controls[2],
                    "conditional include preserves trailing sibling order");
                AssertEqual(consumer, root.Controls[3],
                    "conditional include preserves later style consumer order");
                AssertEqual(false, included.Visible,
                    "outer false condition initially collapses included visual");
                AssertEqual(includedBaseline, included.BackColor,
                    "inactive included style leaves visual native baseline");
                AssertEqual(consumerBaseline, consumer.BackColor,
                    "inactive included resource style leaves external baseline");
                AssertEqual("included-light",
                    runtime.Presets.Resolve("Theme", "IncludedKey"),
                    "conditional include presets remain available as catalogs");

                CreateHandleAndDrainCallbacks(root);
                runtime.Presets.Select("Theme", "Dark");
                DrainCallbacks(root);

                AssertEqual(false, included.Visible,
                    "nested false condition keeps included visual collapsed");
                AssertEqual(consumerBaseline, consumer.BackColor,
                    "nested false condition keeps included resource style inactive");

                runtime.Presets.Select("Feature", "Enabled");
                DrainCallbacks(root);

                AssertEqual(true, included.Visible,
                    "both nested conditions activate included visual");
                AssertEqual(Color.FromArgb(255, 35, 39, 46),
                    included.BackColor,
                    "conditional resource style activates on included visual");
                AssertEqual(Color.FromArgb(255, 35, 39, 46),
                    consumer.BackColor,
                    "conditional resource style activates on external consumer");
                AssertEqual(Color.White, consumer.ForeColor,
                    "all conditional resource style setters activate together");
                AssertEqual("included-dark",
                    runtime.Presets.Resolve("Theme", "IncludedKey"),
                    "active selected preset resolves included catalog value");

                IntPtr beforeHandle = before.Handle;
                IntPtr includedHandle = included.Handle;
                int i;

                for (i = 0; i < 6; i++)
                {
                    runtime.Presets.Select("Theme", "Light");
                    DrainCallbacks(root);
                    AssertEqual(false, included.Visible,
                        "repeated switch collapses included visual");
                    AssertEqual(includedBaseline, included.BackColor,
                        "repeated switch restores included visual baseline");
                    AssertEqual(consumerBaseline, consumer.BackColor,
                        "repeated switch restores external style baseline");

                    runtime.Presets.Select("Theme", "Dark");
                    DrainCallbacks(root);
                    AssertEqual(true, included.Visible,
                        "repeated switch reactivates included visual");
                }

                AssertEqual(4, root.Controls.Count,
                    "repeated switches never add or remove controls");
                AssertEqual(before, root.Controls[0],
                    "repeated switches preserve leading control identity");
                AssertEqual(included, root.Controls[1],
                    "repeated switches preserve included control identity");
                AssertEqual(after, root.Controls[2],
                    "repeated switches preserve trailing control identity");
                AssertEqual(beforeHandle, before.Handle,
                    "unrelated sibling native handle is not rebuilt");
                AssertEqual(includedHandle, included.Handle,
                    "included visual native handle is retained");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestStaticConditionalIncludes(Assembly assembly)
        {
            XamlRuntime falseRuntime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "StaticConditionalFalseHost.xml",
                null);

            try
            {
                Panel root = falseRuntime.RootControl as Panel;

                AssertTrue(root != null, "static false include root");
                AssertEqual(2, root.Controls.Count,
                    "static false include does not build visual content");
                AssertEqual("StaticBefore", root.Controls[0].Name,
                    "static false include preserves leading sibling");
                AssertEqual("StaticAfter", root.Controls[1].Name,
                    "static false include preserves trailing sibling");
                AssertEqual("included-light",
                    falseRuntime.Presets.Resolve("Theme", "IncludedKey"),
                    "static false include still imports preset catalogs");
            }
            finally
            {
                falseRuntime.Dispose();
            }

            XamlRuntime trueRuntime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "StaticConditionalTrueHost.xml",
                null);

            try
            {
                Panel root = trueRuntime.RootControl as Panel;
                Label included = trueRuntime.Get<Label>("ConditionalIncluded");

                AssertTrue(root != null, "static true include root");
                AssertEqual(3, root.Controls.Count,
                    "static true include builds visual content");
                AssertEqual(included, root.Controls[1],
                    "static true include keeps marker position");
                AssertEqual(Color.FromArgb(255, 229, 231, 235),
                    included.BackColor,
                    "static true include enables resource style");
            }
            finally
            {
                trueRuntime.Dispose();
            }
        }

        private static void TestConditionalIncludeDiagnostics(Assembly assembly)
        {
            XamlRuntime runtime = null;
            WinFormsXamlLoadException failure = null;

            try
            {
                runtime = XamlRuntime.LoadEmbedded(
                    assembly,
                    HostResourcePrefix + "ConditionalUnknownHost.xml",
                    null);
            }
            catch (WinFormsXamlLoadException ex)
            {
                failure = ex;
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();
            }

            AssertTrue(failure != null,
                "unknown conditional include preset raises a load diagnostic");
            AssertEqual("Condition", failure.PropertyName,
                "conditional include diagnostic identifies Condition");
            AssertContains(failure.ToString(), "MissingTheme",
                "conditional include diagnostic identifies missing preset set");

            failure = ExpectLoadFailure(
                "<Panel>" +
                "  <Includes Source='ConditionalTheme' " +
                "Condition='{Binding Flag, Mode=TwoWay}' />" +
                "</Panel>",
                null);

            AssertEqual("Condition", failure.PropertyName,
                "two-way conditional include diagnostic identifies Condition");
            AssertContains(failure.ToString(), "OneWay",
                "conditional include rejects two-way mode clearly");

            failure = ExpectLoadFailure(
                "<Grid>" +
                "  <Includes Source='ConditionalStructuralContent' " +
                "Condition='true' />" +
                "</Grid>",
                null);

            AssertContains(failure.ToString(), "top-level",
                "conditional include rejects structural owner properties clearly");
            AssertContains(failure.ToString(), "Grid.RowDefinitions",
                "conditional include structural diagnostic identifies property");
        }

        private static void TestRegisteredVisualInsertionBindingsAndEvents(
            Assembly assembly)
        {
            IncludesBindingTarget target =
                new IncludesBindingTarget();
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "VisualHost.xml",
                target);

            try
            {
                Panel root = runtime.RootControl as Panel;

                AssertTrue(root != null, "visual include host root");
                AssertEqual(4, root.Controls.Count, "visual include child count");
                AssertEqual(
                    "AuthoredBefore",
                    root.Controls[0].Name,
                    "content before include remains first");
                AssertEqual(
                    "IncludedCaption",
                    root.Controls[1].Name,
                    "first included child keeps marker position");
                AssertEqual(
                    "IncludedAction",
                    root.Controls[2].Name,
                    "second included child keeps source order");
                AssertEqual(
                    "AuthoredAfter",
                    root.Controls[3].Name,
                    "content after include remains last");

                Label caption = runtime.Get<Label>("IncludedCaption");
                Button action = runtime.Get<Button>("IncludedAction");

                AssertEqual(
                    "included binding",
                    caption.Text,
                    "included binding initial value");

                CreateHandleAndDrainCallbacks(runtime.RootControl);
                target.Caption.Value = "updated included binding";
                DrainCallbacks(runtime.RootControl);

                AssertEqual(
                    "updated included binding",
                    caption.Text,
                    "included binding remains reactive");

                action.PerformClick();

                AssertEqual(
                    1,
                    target.IncludedActionClickCount,
                    "included event uses destination code-behind");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestIncludedResourcesAndPresetMerge(
            Assembly assembly)
        {
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ThemeHost.xml",
                null);

            try
            {
                Label label = runtime.Get<Label>("ThemedLabel");

                AssertEqual(
                    "local-wins",
                    runtime.Presets.Resolve("Theme", "Shared"),
                    "local preset declaration overrides included value");
                AssertEqual(
                    "include-only",
                    runtime.Presets.Resolve("Theme", "IncludeOnly"),
                    "included preset retains non-conflicting value");
                AssertEqual(
                    "local-only",
                    runtime.Presets.Resolve("Theme", "LocalOnly"),
                    "local preset retains non-conflicting value");
                AssertEqual(
                    "local-wins",
                    label.Text,
                    "local preset winner reaches target");
                AssertEqual(
                    "include-only",
                    label.Tag as string,
                    "included preset value reaches target");
                AssertEqual(
                    Color.FromArgb(255, 68, 85, 102),
                    label.ForeColor,
                    "later local resource setter overrides included setter");
                AssertEqual(
                    Color.FromArgb(255, 221, 238, 255),
                    label.BackColor,
                    "include-only resource setter remains effective");
                AssertEqual(
                    BorderStyle.Fixed3D,
                    label.BorderStyle,
                    "later include resource setter overrides earlier include");

                CreateHandleAndDrainCallbacks(runtime.RootControl);
                runtime.Presets.Select("Theme", "Dark");
                DrainCallbacks(runtime.RootControl);

                AssertEqual(
                    "dark-include-shared",
                    label.Text,
                    "included presets remain reactive after selection");
                AssertEqual(
                    "dark-include-only",
                    label.Tag as string,
                    "included non-style preset target refreshes");
                AssertEqual(
                    Color.FromArgb(255, 68, 85, 102),
                    label.ForeColor,
                    "local resource winner remains after preset selection");
                AssertEqual(
                    Color.FromArgb(255, 34, 51, 68),
                    label.BackColor,
                    "included resource setter refreshes with its preset");
                AssertEqual(
                    BorderStyle.Fixed3D,
                    label.BorderStyle,
                    "later include resource winner remains after refresh");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestIncludedThemeRemovesDarkOnlyValues(
            Assembly assembly)
        {
            IncludedThemeResetTarget target =
                new IncludedThemeResetTarget();
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ThemeResetHost.xml",
                target);

            Form nativeForm = new Form();
            Button nativeButton = new Button();
            TextBox nativeTextBox = new TextBox();
            PictureBox nativePicture = new PictureBox();
            ProgressBar nativeProgress = new ProgressBar();
            TabView nativeTabs = new TabView();

            try
            {
                Form form = runtime.Get<Form>("ThemeResetForm");
                Control raised = runtime.Get<Control>("SurfaceRaised");
                Button button = runtime.Get<Button>("ThemeButton");
                TextBox textBox = runtime.Get<TextBox>("ThemeTextBox");
                PictureBox picture =
                    runtime.Get<PictureBox>("ThemePicture");
                ProgressBar progress =
                    runtime.Get<ProgressBar>("ThemeProgress");
                TabView tabs = runtime.Get<TabView>("ThemeTabs");
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("ThemeRows");
                Control item = FindControl<Control>(rows, "ThemeItem");

                AssertTrue(item != null,
                    "included theme fixture realizes its item template");
                AssertEqual(nativeForm.BackColor, form.BackColor,
                    "Light Transparent restores the native Form background");
                AssertEqual(SystemColors.Control, raised.BackColor,
                    "a Light-missing direct background starts native");
                AssertEqual(nativeButton.BackColor, button.BackColor,
                    "inactive conditional Button style starts native");
                AssertEqual(nativeButton.FlatStyle, button.FlatStyle,
                    "inactive conditional Button shape starts native");
                AssertEqual(
                    nativeButton.UseVisualStyleBackColor,
                    button.UseVisualStyleBackColor,
                    "inactive conditional Button visual style starts native");
                AssertEqual(nativeTextBox.BackColor, textBox.BackColor,
                    "a Light-missing TextBox style value starts native");
                AssertEqual(nativePicture.BackColor, picture.BackColor,
                    "a Light-missing PictureBox style value starts native");
                AssertEqual(nativeProgress.BackColor, progress.BackColor,
                    "a Light-missing ProgressBar style value starts native");
                AssertEqual(nativeTabs.TabBackground, tabs.TabBackground,
                    "a Light-missing TabView color starts native");
                AssertEqual(nativeTabs.ContentBackground,
                    tabs.ContentBackground,
                    "a Light-missing TabView content color starts native");
                AssertEqual(null, rows.VerticalScrollStyle,
                    "the Dark-only styled scrollbar starts absent");
                AssertTrue(item.BackColor.A == 0,
                    "the Light item background is transparent");

                CreateHandleAndDrainCallbacks(form);
                runtime.Presets.Select("Theme", "Dark");
                DrainCallbacks(form);

                AssertEqual(Color.FromArgb(0x1B, 0x1E, 0x23),
                    form.BackColor,
                    "Dark applies the Form background");
                AssertEqual(Color.FromArgb(0x2B, 0x30, 0x38),
                    raised.BackColor,
                    "Dark applies the direct surface background");
                AssertEqual(Color.FromArgb(0x3A, 0x41, 0x4B),
                    button.BackColor,
                    "Dark activates the conditional Button background");
                AssertEqual(FlatStyle.Popup, button.FlatStyle,
                    "Dark activates the conditional Button shape");
                AssertEqual(false, button.UseVisualStyleBackColor,
                    "Dark activates the conditional Button rendering mode");
                AssertEqual(Color.FromArgb(0x17, 0x1A, 0x1F),
                    textBox.BackColor,
                    "Dark applies the TextBox-only background key");
                AssertEqual(Color.FromArgb(0x23, 0x27, 0x2E),
                    picture.BackColor,
                    "Dark applies the PictureBox-only background key");
                AssertEqual(Color.FromArgb(0x17, 0x1A, 0x1F),
                    progress.BackColor,
                    "Dark applies the ProgressBar-only background key");
                AssertEqual(Color.FromArgb(0x1F, 0x23, 0x29),
                    tabs.TabBackground,
                    "Dark applies the TabView header background");
                AssertEqual(Color.FromArgb(0x2B, 0x30, 0x38),
                    tabs.ContentBackground,
                    "Dark applies the TabView content background");
                AssertTrue(rows.VerticalScrollStyle != null,
                    "Dark activates the styled scrollbar property element");
                AssertEqual(Color.FromArgb(0x26, 0x2B, 0x32),
                    item.BackColor,
                    "Dark applies the item-template background");

                runtime.Presets.Select("Theme", "Light");
                DrainCallbacks(form);

                AssertEqual(nativeForm.BackColor, form.BackColor,
                    "Light removes the Dark Form background");
                AssertEqual(SystemColors.Control, raised.BackColor,
                    "Light removes the Dark-only direct background");
                AssertEqual(nativeButton.BackColor, button.BackColor,
                    "Light removes the conditional Button background");
                AssertEqual(form.ForeColor, button.ForeColor,
                    "Light removes the conditional Button foreground and " +
                    "reveals the Light inherited foreground");
                AssertEqual(nativeButton.FlatStyle, button.FlatStyle,
                    "Light removes the conditional Button shape");
                AssertEqual(
                    nativeButton.UseVisualStyleBackColor,
                    button.UseVisualStyleBackColor,
                    "Light restores native Button visual styles");
                AssertEqual(nativeTextBox.BackColor, textBox.BackColor,
                    "Light removes the Dark-only TextBox background");
                AssertEqual(nativePicture.BackColor, picture.BackColor,
                    "Light removes the Dark-only PictureBox background");
                AssertEqual(nativeProgress.BackColor, progress.BackColor,
                    "Light removes the Dark-only ProgressBar background");
                AssertEqual(nativeTabs.TabBackground, tabs.TabBackground,
                    "Light removes the Dark-only TabView header background");
                AssertEqual(nativeTabs.ContentBackground,
                    tabs.ContentBackground,
                    "Light removes the Dark-only TabView content background");
                AssertEqual(null, rows.VerticalScrollStyle,
                    "Light removes the Dark-only styled scrollbar");
                AssertTrue(item.BackColor.A == 0,
                    "Light restores the transparent item background");

                runtime.Presets.Select("Theme", "Dark");
                DrainCallbacks(form);
                runtime.Presets.Select("Theme", "Light");
                DrainCallbacks(form);

                AssertEqual(nativeForm.BackColor, form.BackColor,
                    "repeated switching keeps the Form in Light");
                AssertEqual(SystemColors.Control, raised.BackColor,
                    "repeated switching cannot pin an inherited Dark surface");
                AssertEqual(nativeButton.BackColor, button.BackColor,
                    "repeated switching cannot retain the conditional Button style");
                AssertEqual(nativeTextBox.BackColor, textBox.BackColor,
                    "repeated switching cannot retain the TextBox background");
                AssertEqual(nativePicture.BackColor, picture.BackColor,
                    "repeated switching cannot retain the PictureBox background");
                AssertEqual(nativeProgress.BackColor, progress.BackColor,
                    "repeated switching cannot retain the ProgressBar background");
                AssertEqual(nativeTabs.TabBackground, tabs.TabBackground,
                    "repeated switching cannot retain the TabView background");
                AssertEqual(null, rows.VerticalScrollStyle,
                    "repeated switching cannot retain the styled scrollbar");
                AssertTrue(item.BackColor.A == 0,
                    "repeated switching keeps the item background transparent");
            }
            finally
            {
                nativeTabs.Dispose();
                nativeProgress.Dispose();
                nativePicture.Dispose();
                nativeTextBox.Dispose();
                nativeButton.Dispose();
                nativeForm.Dispose();
                runtime.Dispose();
            }
        }

        private static void TestIncludedThemeRefreshesHiddenTabItems(
            bool virtualizing)
        {
            HiddenTabThemeResetTarget target =
                new HiddenTabThemeResetTarget();
            string virtualization = virtualizing
                ? " Virtualizing='true' VirtualizationThreshold='1'" +
                  " EstimatedItemSize='70' ProgressiveRendering='false'"
                : " Virtualizing='false' ProgressiveRendering='false'";
            string markup =
                "<Form Name='HiddenThemeForm' Width='640' Height='480'" +
                " ShowInTaskbar='false' StartPosition='Manual'" +
                " Location='-20000,-20000'" +
                " Background='{Preset Theme.Background}'" +
                " Foreground='{Preset Theme.TextPrimary}'>" +
                "  <Includes Source='ThemeResetContent' />" +
                "  <TabView Name='HiddenThemeTabs' Dock='Fill'" +
                "           SelectedIndex='0'" +
                "           ForceNativeTabs='{Preset Theme == Light}'" +
                "           BackColor='{Preset Theme.TabBackground}'" +
                "           TabBackground='{Preset Theme.TabBackground}'" +
                "           SelectedTabBackground='{Preset Theme.TabSelectedBackground}'" +
                "           TabForeground='{Preset Theme.TabHeaderText}'" +
                "           SelectedTabForeground='{Preset Theme.TabSelectedText}'" +
                "           ContentBackground='{Preset Theme.TabSelectedBackground}'>" +
                "    <TabViewItem Name='VisiblePage' Header='Visible'>" +
                "      <Panel />" +
                "    </TabViewItem>" +
                "    <TabViewItem Name='HiddenRowsPage' Header='Rows'>" +
                "      <FlexPanel Name='HiddenThemeSurface' Direction='Column'" +
                "                 Background='{Preset Theme.TabSelectedBackground}'>" +
                "        <DockPanel Name='HiddenInheritedSurface' Height='20' />" +
                "        <ItemsControl Name='HiddenThemeRows' FlexGrow='1'" +
                "                    AutoScroll='true'" +
                "                    ItemsSource='{Binding Items}'" +
                "                    Background='{Preset Theme.TabSelectedBackground}'" +
                                     virtualization + ">" +
                "        <ItemsControl.VerticalScrollStyle Condition='{Preset Theme == Dark}'>" +
                "          <ScrollBarStyle TrackColor='{Preset Theme.ScrollTrack}'" +
                "                          ThumbColor='{Preset Theme.ScrollThumb}'" +
                "                          ArrowColor='{Preset Theme.ScrollArrow}' />" +
                "        </ItemsControl.VerticalScrollStyle>" +
                "        <ItemsControl.ItemTemplate>" +
                "          <Border Name='HiddenThemeItem' Height='70'" +
                "                  Background='{Preset Theme.ItemBackground}'" +
                "                  BorderBrush='{Preset Theme.ItemBorder}'" +
                "                  BorderThickness='1'>" +
                "            <DockPanel Name='HiddenThemeSurface'>" +
                "              <Label Name='HiddenThemeText' Text='{Binding Text}' />" +
                "            </DockPanel>" +
                "          </Border>" +
                "        </ItemsControl.ItemTemplate>" +
                "      </ItemsControl>" +
                "      </FlexPanel>" +
                "    </TabViewItem>" +
                "  </TabView>" +
                "</Form>";
            XamlRuntime runtime = XamlRuntime.Load(markup, target);

            try
            {
                Form form = runtime.Get<Form>("HiddenThemeForm");
                TabView tabs = runtime.Get<TabView>("HiddenThemeTabs");
                TabViewItem hiddenPage =
                    runtime.Get<TabViewItem>("HiddenRowsPage");
                Control inheritedSurface =
                    runtime.Get<Control>("HiddenInheritedSurface");
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("HiddenThemeRows");
                Color lightRowsBackground = rows.BackColor;
                Color lightInheritedBackground =
                    inheritedSurface.BackColor;

                CreateHandleAndDrainCallbacks(form);
                form.Show();
                DrainCallbacks(form);
                AssertEqual(0, tabs.SelectedIndex,
                    "the item page starts unselected");
                AssertEqual(false, hiddenPage.Visible,
                    "the item page is genuinely hidden before the theme switch");

                runtime.Presets.Select("Theme", "Dark");
                DrainCallbacks(form);

                AssertEqual(false, hiddenPage.Visible,
                    "the Dark refresh does not select the hidden item page");
                AssertEqual(Color.FromArgb(0x2B, 0x30, 0x38),
                    rows.BackColor,
                    "Dark refreshes the hidden ItemsControl background");
                AssertEqual(Color.FromArgb(0x2B, 0x30, 0x38),
                    inheritedSurface.BackColor,
                    "Dark reaches an unconfigured child inside the hidden page");
                AssertTrue(rows.VerticalScrollStyle != null,
                    "Dark activates the hidden ItemsControl scrollbar style");

                IncludedThemeResetItem[] replacement =
                    new IncludedThemeResetItem[49];
                int replacementIndex;

                for (replacementIndex = 0;
                     replacementIndex < replacement.Length;
                     replacementIndex++)
                {
                    replacement[replacementIndex] =
                        new IncludedThemeResetItem();
                }

                replacement[0].Text = "replaced while hidden";
                target.Items.Replace(replacement);
                DrainCallbacks(form);

                AssertEqual(false, hiddenPage.Visible,
                    "replacing ItemsBinding does not reveal the hidden page");

                tabs.SelectedIndex = 1;
                DrainCallbacks(form);

                Control item =
                    FindControl<Control>(rows, "HiddenThemeItem");
                AssertTrue(item != null,
                    "selecting the item page realizes an item");
                AssertEqual(Color.FromArgb(0x26, 0x2B, 0x32),
                    item.BackColor,
                    "an item revealed after the hidden Dark refresh is Dark");
                Control itemSurface =
                    FindControl<Control>(item, "HiddenThemeSurface");
                AssertTrue(itemSurface != null,
                    "the hidden-tab item contains an inherited fill surface");
                AssertEqual(Color.FromArgb(0x26, 0x2B, 0x32),
                    itemSurface.BackColor,
                    "the revealed fill surface inherits the Dark item background");
                Label itemText =
                    FindControl<Label>(item, "HiddenThemeText");
                AssertTrue(itemText != null,
                    "the item revealed after replacement retains its binding");
                AssertEqual("replaced while hidden", itemText.Text,
                    "ItemsBinding replacement while hidden is visible on reveal");

                tabs.SelectedIndex = 0;
                DrainCallbacks(form);
                AssertEqual(false, hiddenPage.Visible,
                    "the item page is hidden again before the Light refresh");

                runtime.Presets.Select("Theme", "Light");
                DrainCallbacks(form);

                AssertEqual(false, hiddenPage.Visible,
                    "the Light refresh does not select the hidden item page");
                AssertEqual(lightRowsBackground, rows.BackColor,
                    "Light resets the hidden ItemsControl background");
                AssertEqual(lightInheritedBackground,
                    inheritedSurface.BackColor,
                    "Light resets inherited Dark state inside the hidden page");
                AssertEqual(null, rows.VerticalScrollStyle,
                    "Light removes the hidden ItemsControl scrollbar style");

                tabs.SelectedIndex = 1;
                DrainCallbacks(form);

                item = FindControl<Control>(rows, "HiddenThemeItem");
                AssertTrue(item != null,
                    "selecting the Light item page keeps an item realized");
                AssertTrue(item.BackColor.A == 0,
                    "an item revealed after the hidden Light refresh is reset");
                itemSurface =
                    FindControl<Control>(item, "HiddenThemeSurface");
                AssertTrue(itemSurface != null,
                    "the Light item keeps its inherited fill surface");
                AssertTrue(itemSurface.BackColor.A == 0,
                    "Light removes the stale Dark inherited fill background");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestSuppliedPresetManagerPrecedence(
            Assembly assembly)
        {
            PresetManager manager = new PresetManager();
            manager.LoadXml(
                "<Presets Name='Theme' Selected='App' Default='Default'>" +
                "  <Preset Name='App'>" +
                "    <Set Key='Shared' Value='app-wins' />" +
                "    <Set Key='IncludeOnly' Value='app-include-only' />" +
                "    <Set Key='SurfaceColor' Value='#FF778899' />" +
                "  </Preset>" +
                "  <Preset Name='Default'>" +
                "    <Set Key='ManagerOnly' Value='manager-only' />" +
                "  </Preset>" +
                "</Presets>");

            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ThemeHost.xml",
                null,
                manager);

            try
            {
                Label label = runtime.Get<Label>("ThemedLabel");

                AssertEqual(
                    "App",
                    manager["Theme"].SelectedName,
                    "application selection remains highest priority");
                AssertEqual(
                    "Default",
                    manager["Theme"].DefaultName,
                    "application default remains highest priority");
                AssertEqual(
                    "app-wins",
                    manager.Resolve("Theme", "Shared"),
                    "application value remains highest priority");
                AssertEqual(
                    "local-layer",
                    manager.Resolve("Theme", "XmlLayer"),
                    "XML-only key still follows include then local precedence");
                AssertEqual(
                    "app-wins",
                    label.Text,
                    "application value reaches composed target");
                AssertEqual(
                    "app-include-only",
                    label.Tag as string,
                    "application value replaces included target value");
                AssertEqual(
                    Color.FromArgb(255, 119, 136, 153),
                    label.BackColor,
                    "application value reaches included resource style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestNestedRegisteredIncludes(
            Assembly assembly)
        {
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "NestedHost.xml",
                null);

            try
            {
                Panel root = runtime.RootControl as Panel;

                AssertTrue(root != null, "nested include host root");
                AssertEqual(3, root.Controls.Count, "nested include child count");
                AssertEqual(
                    "NestedOuterBefore",
                    root.Controls[0].Name,
                    "nested outer content before leaf");
                AssertEqual(
                    "NestedLeaf",
                    root.Controls[1].Name,
                    "nested registered include expands in place");
                AssertEqual(
                    "NestedOuterAfter",
                    root.Controls[2].Name,
                    "nested outer content after leaf");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestProgrammaticXmlFormInclude()
        {
            ProgrammaticIncludesForm form =
                new ProgrammaticIncludesForm();

            try
            {
                Form nativeForm = form.WinForm;

                AssertTrue(nativeForm != null, "programmatic XmlForm native Form");
                AssertEqual(
                    "programmatic include",
                    form.IncludedLabel.Text,
                    "XmlForm.Include composes queued content");
                AssertEqual(
                    "local content",
                    form.LocalLabel.Text,
                    "programmatic include preserves local content");
                AssertEqual(
                    "second programmatic include",
                    form.SecondIncludedLabel.Text,
                    "second XmlForm.Include composes queued content");
                AssertEqual(
                    3,
                    nativeForm.Controls.Count,
                    "programmatic and local child count");
                AssertEqual(
                    "ProgrammaticIncluded",
                    nativeForm.Controls[0].Name,
                    "programmatic include is prepended");
                AssertEqual(
                    "ProgrammaticSecond",
                    nativeForm.Controls[1].Name,
                    "programmatic includes preserve call order");
                AssertEqual(
                    "ProgrammaticLocal",
                    nativeForm.Controls[2].Name,
                    "local XML follows programmatic include");

                AssertThrowsInvalidOperation(
                    delegate
                    {
                        form.IncludeAfterLoad("ProgrammaticContent");
                    },
                    "Include after XmlForm load is rejected");
            }
            finally
            {
                form.Dispose();
            }
        }

        private static void TestDirectEmbeddedResourceInclude(
            Assembly assembly)
        {
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "EmbeddedHost.xml",
                null);

            try
            {
                AssertEqual(
                    "direct embedded include",
                    runtime.Get<Label>("DirectEmbeddedLabel").Text,
                    "SourceKind EmbeddedResource bypasses registration lookup");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestClassCreatedXmlFormInclude(
            Assembly assembly)
        {
            ClassCreatedIncludesForm.LastInstance = null;
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ClassCreatedHost.xml",
                null);

            try
            {
                ClassCreatedIncludesForm instance =
                    ClassCreatedIncludesForm.LastInstance;

                AssertTrue(
                    instance != null,
                    "Form Class creates its XmlForm code-behind");
                AssertEqual(
                    "programmatic include",
                    runtime.Get<Label>("ProgrammaticIncluded").Text,
                    "Class-created XmlForm constructor include is composed");
                AssertTrue(
                    Object.ReferenceEquals(
                        runtime.Form,
                        instance.WinForm),
                    "Class-created XmlForm adopts the composing runtime");
            }
            finally
            {
                runtime.Dispose();
                ClassCreatedIncludesForm.LastInstance = null;
            }
        }

        private static void TestIncludeInsideRegisteredComponent(
            Assembly assembly)
        {
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ComponentHost.xml",
                null);

            try
            {
                Panel component =
                    runtime.Get<Panel>("IncludedComponentInstance");
                Label label = FindControl<Label>(
                    component,
                    "IncludedComponentLabel");

                AssertTrue(
                    label != null,
                    "registered component expands its include before validation");
                AssertEqual(
                    "component include",
                    label.Text,
                    "component include content");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestIncludeResourcesAtComponentRoot(
            Assembly assembly)
        {
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "RootResourceComponentHost.xml",
                null);

            try
            {
                Panel component =
                    runtime.Get<Panel>("RootResourceComponentInstance");
                Label label = FindControl<Label>(
                    component,
                    "RootResourceComponentLabel");

                AssertTrue(
                    label != null,
                    "component root include does not become a visual root");
                AssertEqual(
                    Color.FromArgb(255, 171, 205, 239),
                    label.BackColor,
                    "Component.Resources imported by include applies style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestIncludeInsideItemTemplate(
            Assembly assembly)
        {
            IncludesItemsTarget target = new IncludesItemsTarget();
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ItemsHost.xml",
                target);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.Get<XamlRuntime.ItemsControl>("IncludedRows");

                int renderedItemCount = 0;
                int i;

                for (i = 0; i < rows.Controls.Count; i++)
                {
                    Label label = FindControl<Label>(
                        rows.Controls[i],
                        "IncludedItemText");

                    if (label == null)
                        continue;

                    AssertTrue(
                        renderedItemCount < target.Items.Length,
                        "included ItemTemplate renders no extra item roots");
                    AssertEqual(
                        target.Items[renderedItemCount].Text,
                        label.Text,
                        "included ItemTemplate binding " +
                        renderedItemCount.ToString());
                    AssertEqual(
                        Color.FromArgb(255, 164, 208, 255),
                        label.BackColor,
                        "included ItemTemplate resource style " +
                        renderedItemCount.ToString());
                    renderedItemCount++;
                }

                AssertEqual(
                    target.Items.Length,
                    renderedItemCount,
                    "included ItemTemplate renders every item");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestArbitraryResourceScopeMerge(
            Assembly assembly)
        {
            PresetManager manager = new PresetManager();
            manager.LoadXml(
                "<Presets Name='ArbitraryTheme' Selected='Default'>" +
                "  <Preset Name='Default'>" +
                "    <Set Key='Protected' Value='application-wins' />" +
                "  </Preset>" +
                "</Presets>");

            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                assembly,
                HostResourcePrefix + "ArbitraryResourcesHost.xml",
                null,
                manager);

            try
            {
                Label label = runtime.Get<Label>("ArbitraryResourcesLabel");

                AssertEqual(
                    "application-wins",
                    manager.Resolve("ArbitraryTheme", "Protected"),
                    "supplied preset value survives arbitrary resource merge");
                AssertEqual(
                    "local-wins",
                    manager.Resolve("ArbitraryTheme", "Layered"),
                    "local arbitrary resource preset overrides include");
                AssertEqual(
                    "included-only",
                    manager.Resolve("ArbitraryTheme", "IncludedOnly"),
                    "included arbitrary resource preset is retained");
                AssertEqual(
                    "local-only",
                    manager.Resolve("ArbitraryTheme", "LocalOnly"),
                    "local arbitrary resource preset is retained");
                AssertEqual(
                    "local-wins",
                    label.Text,
                    "merged arbitrary resource preset reaches target");
                AssertEqual(
                    Color.FromArgb(255, 213, 225, 237),
                    label.BackColor,
                    "included arbitrary resource style is retained");
                AssertEqual(
                    Color.FromArgb(255, 33, 49, 65),
                    label.ForeColor,
                    "local owner resource style is retained");
                AssertEqual(
                    BorderStyle.Fixed3D,
                    label.BorderStyle,
                    "local owner resource style follows included style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestRelativeFileInclude()
        {
            string directory = CreateTemporaryDirectory();
            string nestedDirectory = Path.Combine(directory, "nested");
            Directory.CreateDirectory(nestedDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(directory, "outer.xml"),
                    "<Includes>\n" +
                    "  <Label Name='FileOuterBefore' Text='outer-before' />\n" +
                    "  <Includes Source='nested/inner.xml' SourceKind='File' />\n" +
                    "  <Label Name='FileOuterAfter' Text='outer-after' />\n" +
                    "</Includes>");
                File.WriteAllText(
                    Path.Combine(nestedDirectory, "inner.xml"),
                    "<Includes>\n" +
                    "  <Label Name='FileInner' Text='inner' />\n" +
                    "</Includes>");

                XamlRuntime runtime = XamlRuntime.Load(
                    "<Panel>" +
                    "  <Includes Source='outer.xml' SourceKind='File' />" +
                    "</Panel>",
                    null,
                    directory);

                try
                {
                    Panel root = runtime.RootControl as Panel;

                    AssertTrue(root != null, "file include host root");
                    AssertEqual(3, root.Controls.Count, "file include child count");
                    AssertEqual(
                        "FileOuterBefore",
                        root.Controls[0].Name,
                        "file outer before nested content");
                    AssertEqual(
                        "FileInner",
                        root.Controls[1].Name,
                        "nested file resolves relative to containing include");
                    AssertEqual(
                        "FileOuterAfter",
                        root.Controls[2].Name,
                        "file outer after nested content");
                }
                finally
                {
                    runtime.Dispose();
                }
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static void TestInvalidFileIncludeFailures()
        {
            string directory = CreateTemporaryDirectory();

            try
            {
                File.WriteAllText(
                    Path.Combine(directory, "cycle-a.xml"),
                    "<Includes>" +
                    "  <Includes Source='cycle-b.xml' SourceKind='File' />" +
                    "</Includes>");
                File.WriteAllText(
                    Path.Combine(directory, "cycle-b.xml"),
                    "<Includes>" +
                    "  <Includes Source='cycle-a.xml' SourceKind='File' />" +
                    "</Includes>");
                File.WriteAllText(
                    Path.Combine(directory, "wrong-root.xml"),
                    "<Panel />");

                WinFormsXamlLoadException cycleFailure =
                    ExpectLoadFailure(
                        "<Panel>" +
                        "  <Includes Source='cycle-a.xml' SourceKind='File' />" +
                        "</Panel>",
                        directory);
                AssertContains(
                    cycleFailure.ToString(),
                    "Circular XML include chain",
                    "cycle reports the complete include problem");
                AssertContains(
                    cycleFailure.ToString(),
                    "cycle-a.xml",
                    "cycle reports first source");
                AssertContains(
                    cycleFailure.ToString(),
                    "cycle-b.xml",
                    "cycle reports nested source");

                WinFormsXamlLoadException missingFailure =
                    ExpectLoadFailure(
                        "<Panel>" +
                        "  <Includes Source='missing.xml' SourceKind='File' />" +
                        "</Panel>",
                        directory);
                AssertContains(
                    missingFailure.ToString(),
                    "missing.xml",
                    "missing include identifies requested file");

                WinFormsXamlLoadException wrongRootFailure =
                    ExpectLoadFailure(
                        "<Panel>" +
                        "  <Includes Source='wrong-root.xml' SourceKind='File' />" +
                        "</Panel>",
                        directory);
                AssertContains(
                    wrongRootFailure.ToString(),
                    "must have an <Includes> root",
                    "wrong include root reports the required contract");
                AssertEqual(
                    Path.GetFullPath(
                        Path.Combine(directory, "wrong-root.xml")),
                    wrongRootFailure.MarkupSource,
                    "wrong-root diagnostic points at included file");
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static void TestUnknownIncludeAttributes()
        {
            string directory = CreateTemporaryDirectory();

            try
            {
                File.WriteAllText(
                    Path.Combine(directory, "valid.xml"),
                    "<Includes>" +
                    "  <Label Name='ValidInclude' />" +
                    "</Includes>");
                File.WriteAllText(
                    Path.Combine(directory, "unknown-root-attribute.xml"),
                    "<Includes SourceKnd='File'>" +
                    "  <Label Name='InvalidRootInclude' />" +
                    "</Includes>");

                WinFormsXamlLoadException markerFailure =
                    ExpectLoadFailure(
                        "<Panel>" +
                        "  <Includes Source='valid.xml' SourceKind='File' " +
                        "SourceKnd='File' />" +
                        "</Panel>",
                        directory);
                AssertContains(
                    markerFailure.ToString(),
                    "SourceKnd",
                    "unknown include marker attribute is rejected");

                string rootPath = Path.GetFullPath(
                    Path.Combine(
                        directory,
                        "unknown-root-attribute.xml"));
                WinFormsXamlLoadException rootFailure =
                    ExpectLoadFailure(
                        "<Panel>" +
                        "  <Includes Source='unknown-root-attribute.xml' " +
                        "SourceKind='File' />" +
                        "</Panel>",
                        directory);
                AssertContains(
                    rootFailure.ToString(),
                    "SourceKnd",
                    "unknown reusable Includes root attribute is rejected");
                AssertEqual(
                    rootPath,
                    rootFailure.MarkupSource,
                    "unknown root attribute diagnostic identifies include");
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static void TestIncludedSourceDiagnostics()
        {
            string directory = CreateTemporaryDirectory();
            string includedPath = Path.GetFullPath(
                Path.Combine(directory, "broken-content.xml"));

            try
            {
                File.WriteAllText(
                    includedPath,
                    "<Includes>\n" +
                    "  <Label Name='BrokenIncluded'\n" +
                    "         Text='{Binding MissingValue}' />\n" +
                    "</Includes>");

                WinFormsXamlLoadException failure =
                    ExpectLoadFailure(
                        "<Panel Name='DiagnosticHost'>\n" +
                        "  <Includes Source='broken-content.xml' " +
                        "SourceKind='File' />\n" +
                        "</Panel>",
                        directory,
                        new IncludesBindingTarget());

                AssertEqual(
                    includedPath,
                    failure.MarkupSource,
                    "semantic failure retains included source");
                AssertEqual(
                    "Text",
                    failure.PropertyName,
                    "semantic failure retains included property");
                AssertEqual(
                    3,
                    failure.LineNumber,
                    "semantic failure retains included line");
                AssertTrue(
                    failure.LinePosition > 0,
                    "semantic failure retains included position");
                AssertContains(
                    failure.ElementPath,
                    "BrokenIncluded",
                    "semantic failure retains included element name");
                AssertContains(
                    failure.ElementPath,
                    "broken-content.xml",
                    "semantic failure retains include-chain context");
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static void TestInvalidMergedPresetChild()
        {
            string directory = CreateTemporaryDirectory();
            string includedPath = Path.GetFullPath(
                Path.Combine(directory, "invalid-preset.xml"));

            try
            {
                File.WriteAllText(
                    includedPath,
                    "<Includes>\n" +
                    "  <Presets Name='Theme'>\n" +
                    "    <Preset Name='Default'>\n" +
                    "      <Unexpected />\n" +
                    "    </Preset>\n" +
                    "  </Presets>\n" +
                    "</Includes>");

                WinFormsXamlLoadException failure =
                    ExpectLoadFailure(
                        "<Panel>\n" +
                        "  <Presets Name='Theme' Selected='Default'>\n" +
                        "    <Preset Name='Default'>\n" +
                        "      <Set Key='Valid' Value='valid' />\n" +
                        "    </Preset>\n" +
                        "  </Presets>\n" +
                        "  <Includes Source='invalid-preset.xml' " +
                        "SourceKind='File' />\n" +
                        "</Panel>",
                        directory);

                AssertEqual(
                    includedPath,
                    failure.MarkupSource,
                    "merged invalid preset retains included source");
                AssertContains(
                    failure.ToString(),
                    "Unexpected",
                    "merged invalid preset child is not discarded");
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static WinFormsXamlLoadException ExpectLoadFailure(
            string markup,
            string basePath)
        {
            return ExpectLoadFailure(
                markup,
                basePath,
                null);
        }

        private static WinFormsXamlLoadException ExpectLoadFailure(
            string markup,
            string basePath,
            object target)
        {
            XamlRuntime runtime = null;

            try
            {
                runtime = XamlRuntime.Load(
                    markup,
                    target,
                    basePath);
            }
            catch (WinFormsXamlLoadException ex)
            {
                return ex;
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();
            }

            throw new InvalidOperationException(
                "Expected an XML include load failure was not raised.");
        }

        private static string CreateTemporaryDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "WinFormsXaml-Includes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteTemporaryDirectory(string directory)
        {
            if (!String.IsNullOrEmpty(directory) &&
                Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static T FindControl<T>(
            Control root,
            string name)
            where T : Control
        {
            if (root == null)
                return null;

            T match = root as T;

            if (match != null &&
                String.Equals(
                    match.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return match;
            }

            int i;

            for (i = 0; i < root.Controls.Count; i++)
            {
                match = FindControl<T>(root.Controls[i], name);

                if (match != null)
                    return match;
            }

            return null;
        }

        private static void CreateHandleAndDrainCallbacks(Control root)
        {
            AssertTrue(root != null, "include reactive root");

            if (!root.IsHandleCreated)
                root.CreateControl();

            if (!root.IsHandleCreated)
            {
                IntPtr handle = root.Handle;

                AssertTrue(
                    handle != IntPtr.Zero,
                    "include reactive native handle");
            }

            DrainCallbacks(root);
        }

        private static void DrainCallbacks(Control root)
        {
            int round;

            for (round = 0; round < 6; round++)
            {
                bool reached = false;

                root.BeginInvoke(
                    new MethodInvoker(
                        delegate
                        {
                            reached = true;
                        }));

                int iterations = 0;

                while (!reached && iterations < 1024)
                {
                    Application.DoEvents();
                    iterations++;
                }

                AssertTrue(reached, "include reactive dispatch sentinel");
            }
        }

        private static void AssertThrowsInvalidOperation(
            MethodInvoker action,
            string message)
        {
            bool threw = false;

            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            AssertTrue(threw, message);
        }

        private static void AssertContains(
            string actual,
            string expected,
            string message)
        {
            if (actual == null ||
                actual.IndexOf(
                    expected,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException(
                    message + ". Expected text containing '" + expected +
                    "', actual '" + actual + "'.");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message + ".");
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ". Expected '" + expected +
                    "', actual '" + actual + "'.");
            }
        }
    }
}
