using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    /// <summary>
    /// Source-level contract coverage for the explicit owner-drawn backend.
    /// The suite is intentionally not a scrolling benchmark; interactive
    /// performance and Windows 98 validation remain separate acceptance gates.
    /// </summary>
    internal static class LightweightVirtualizationTests
    {
        private sealed class Row
        {
            public readonly string Title;
            public readonly string Url;
            public readonly PropertyBinding<bool> Enabled;
            public readonly Image Picture;

            public Row(string title, string url, bool enabled)
            {
                Title = title;
                Url = url;
                Enabled = new PropertyBinding<bool>(enabled);
                Picture = null;
            }
        }

        private sealed class State
        {
            public readonly ArrayList Rows;

            public State()
            {
                Rows = new ArrayList();
                Rows.Add(new Row("First", "https://example.com/1", true));
                Rows.Add(new Row("Second", "https://example.com/2", false));
            }
        }

        private sealed class PaletteRow
        {
            public readonly string Title;
            public readonly string Url;
            public readonly string Background;
            public readonly PropertyBinding<bool> Enabled;

            public PaletteRow(int index)
            {
                Title = "Row " + index.ToString();
                Url = "https://example.com/" + index.ToString();
                Background = (index & 1) == 0
                    ? "#112233"
                    : "#223344";
                Enabled = new PropertyBinding<bool>((index & 1) == 0);
            }
        }

        private sealed class PaletteState
        {
            public readonly ArrayList Rows = new ArrayList();

            public PaletteState(int count)
            {
                int i;

                for (i = 0; i < count; i++)
                    Rows.Add(new PaletteRow(i));
            }
        }

        private sealed class NullTextRow
        {
            public int ReadCount;

            public string Title
            {
                get
                {
                    ReadCount++;
                    return null;
                }
            }
        }

        private sealed class NullTextState
        {
            public readonly ArrayList Rows = new ArrayList();

            public NullTextState(NullTextRow row)
            {
                Rows.Add(row);
            }
        }

        private sealed class IconRow
        {
            public readonly Icon Picture;

            public IconRow(Icon picture)
            {
                Picture = picture;
            }
        }

        private sealed class IconState
        {
            public readonly ArrayList Rows;

            public IconState()
            {
                Rows = new ArrayList();
                Rows.Add(new IconRow(SystemIcons.Application));
            }
        }

        private sealed class EncodedImageRow
        {
            public readonly byte[] Picture;

            public EncodedImageRow(byte[] picture)
            {
                Picture = picture;
            }
        }

        private sealed class EncodedImageState
        {
            public readonly ArrayList Rows = new ArrayList();

            public EncodedImageState(byte[][] pictures)
            {
                int i;

                for (i = 0; i < pictures.Length; i++)
                    Rows.Add(new EncodedImageRow(pictures[i]));
            }
        }

        private sealed class ExternalImageRow
        {
            public readonly Image Picture;

            public ExternalImageRow(Image picture)
            {
                Picture = picture;
            }
        }

        private sealed class ExternalImageState
        {
            public readonly ArrayList Rows = new ArrayList();

            public ExternalImageState(Image picture)
            {
                Rows.Add(new ExternalImageRow(picture));
            }
        }

        private abstract class NotifyObject : INotifyPropertyChanged
        {
            private PropertyChangedEventHandler _propertyChanged;

            public event PropertyChangedEventHandler PropertyChanged
            {
                add { _propertyChanged += value; }
                remove { _propertyChanged -= value; }
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

            protected void RaisePropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = _propertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private sealed class ReactiveDetails : NotifyObject
        {
            private string _title;
            public int ReadCount;

            public ReactiveDetails(string title)
            {
                _title = title;
            }

            public string Title
            {
                get
                {
                    ReadCount++;
                    return _title;
                }
                set
                {
                    if (String.Equals(
                            _title,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _title = value;
                    RaisePropertyChanged("Title");
                }
            }
        }

        private sealed class ReactiveRow : NotifyObject
        {
            private ReactiveDetails _details;
            private string _other;
            private string _url;

            public readonly int Id;
            public readonly PropertyBinding<bool> Enabled;

            public ReactiveRow(int id, string title)
            {
                Id = id;
                _details = new ReactiveDetails(title);
                _other = null;
                _url = "https://example.com/" + id.ToString();
                Enabled = new PropertyBinding<bool>(true);
            }

            public ReactiveDetails Details
            {
                get { return _details; }
                set
                {
                    if (Object.ReferenceEquals(_details, value))
                        return;

                    _details = value;
                    RaisePropertyChanged("Details");
                }
            }

            public string Other
            {
                get { return _other; }
                set
                {
                    if (String.Equals(
                            _other,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _other = value;
                    RaisePropertyChanged("Other");
                }
            }

            public string Url
            {
                get { return _url; }
                set
                {
                    if (String.Equals(
                            _url,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _url = value;
                    RaisePropertyChanged("Url");
                }
            }

            public void NotifyAll()
            {
                RaisePropertyChanged(String.Empty);
            }
        }

        private sealed class ReactiveState
        {
            public readonly ArrayList Rows = new ArrayList();

            public ReactiveState(int count)
            {
                int i;

                for (i = 0; i < count; i++)
                {
                    Rows.Add(new ReactiveRow(
                        i,
                        "Row " + i.ToString()));
                }
            }

            public string FormatTitle(string value)
            {
                return value;
            }
        }

        private sealed class ReadOnlyCheckRow : NotifyObject
        {
            public bool Enabled
            {
                get { return true; }
            }
        }

        private sealed class SilentWritableCheckRow
        {
            private bool _enabled = true;

            public bool Enabled
            {
                get { return _enabled; }
                set { _enabled = value; }
            }
        }

        private sealed class CheckState
        {
            public readonly ArrayList Rows = new ArrayList();

            public CheckState(object row)
            {
                Rows.Add(row);
            }
        }

        internal static void RunAll()
        {
            TestControlsIsTheDefault();
            TestFinalLightweightEligibilityIsStrictAndOrderIndependent();
            TestFinalizedLightweightConfigurationMutationsAreTransactional();
            TestExplicitLightweightTemplateLoadsWithoutRowControls();
            TestIndexedSnapshotStateSharesBrushesAcrossTenThousandRows();
            TestCachedNullValueIsNotReevaluatedWhilePainting();
            TestIndexedPresetStateReloadsDefaultAndUnsetValues();
            TestUnsupportedElementHasLocationDiagnostic();
            TestEnabledCheckboxRequiresTwoWayBinding();
            TestLightweightTwoWaySourceValidation();
            TestRejectedActivationKeepsCommittedControls();
            TestImageRequiresOwnedSafeSourceShape();
            TestImageRequiresOneCompleteExpression();
            TestRuntimeOwnedImagesUseBoundedThumbnailCache();
            TestCachedImageStretchSemantics();
            TestCallerOwnedImagesBypassThumbnailCache();
            TestStaleSnapshotsReleaseDecodedImages();
            TestOverscanPreparesAheadButPaintRangeStaysVisible();
            TestObservableRowsRebuildIndependentlyAndDetach();
            TestObservableTwoWayCheckBoxStillWritesThrough();
            TestVisitedLinkStateIsBoundedAndStable();
            TestVisibleRangeIsFixedStrideAndBounded();
        }

        private static void TestControlsIsTheDefault()
        {
            using (ItemsControl host = new ItemsControl())
            {
                AssertEqual(
                    ItemsControlVirtualizationMode.Controls,
                    host.VirtualizationMode,
                    "Controls remains the default virtualization backend");
                AssertTrue(
                    !host.Virtualizing,
                    "viewport virtualization is disabled by default");

                bool invalidRejected = false;

                try
                {
                    host.VirtualizationMode =
                        (ItemsControlVirtualizationMode)999;
                }
                catch (ArgumentOutOfRangeException)
                {
                    invalidRejected = true;
                }

                AssertTrue(
                    invalidRejected,
                    "unknown virtualization modes are rejected by the API");
                AssertEqual(
                    ItemsControlVirtualizationMode.Controls,
                    host.VirtualizationMode,
                    "a rejected mode does not mutate the active policy");
            }
        }

        private static void TestFinalLightweightEligibilityIsStrictAndOrderIndependent()
        {
            string template =
                "<ItemsControl.ItemTemplate>" +
                "<Label Text='Ready' />" +
                "</ItemsControl.ItemTemplate>";
            string[] valid = new string[]
            {
                "<Form><ItemsControl Name='Rows' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='24'>" + template +
                "</ItemsControl></Form>",
                "<Form><ItemsControl Name='Rows' FixedItemSize='24' " +
                "Orientation='Vertical' AutoScroll='true' " +
                "Virtualizing='true' VirtualizationMode='Lightweight'>" +
                template + "</ItemsControl></Form>"
            };
            int i;

            for (i = 0; i < valid.Length; i++)
            {
                using (XamlRuntime runtime = XamlRuntime.Load(valid[i]))
                {
                    ItemsControl host = runtime.Get<ItemsControl>("Rows");
                    AssertTrue(host != null, "valid null-source lightweight host loads");
                    AssertTrue(
                        !host.IsVirtualizing,
                        "a null source validates without activating a backend");
                }
            }

            string[] invalidAttributes = new string[]
            {
                "Virtualizing='false' AutoScroll='true' " +
                    "Orientation='Vertical' FixedItemSize='24'",
                "Virtualizing='true' AutoScroll='false' " +
                    "Orientation='Vertical' FixedItemSize='24'",
                "Virtualizing='true' AutoScroll='true' " +
                    "Orientation='Horizontal' FixedItemSize='24'",
                "Virtualizing='true' AutoScroll='true' " +
                    "Orientation='Vertical' FixedItemSize='0'"
            };

            for (i = 0; i < invalidAttributes.Length; i++)
            {
                bool rejected = false;

                try
                {
                    XamlRuntime runtime = XamlRuntime.Load(
                        "<Form>\n  <ItemsControl " +
                        "VirtualizationMode='Lightweight' " +
                        invalidAttributes[i] + ">" + template +
                        "</ItemsControl>\n</Form>");
                    runtime.Dispose();
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                AssertTrue(
                    rejected,
                    "final null-source lightweight host eligibility is strict");
            }

            WinFormsXamlLoadException diagnostic = null;

            try
            {
                XamlRuntime runtime = XamlRuntime.Load(
                    "<Form>\n  <ItemsControl VirtualizationMode='Lightweight' " +
                    "Virtualizing='true' AutoScroll='true' " +
                    "Orientation='Vertical' FixedItemSize='0'>" +
                    template + "</ItemsControl>\n</Form>");
                runtime.Dispose();
            }
            catch (WinFormsXamlLoadException ex)
            {
                diagnostic = ex;
            }

            AssertTrue(diagnostic != null, "final host failure keeps markup diagnostics");
            AssertEqual(
                "FixedItemSize",
                diagnostic.PropertyName,
                "final host failure identifies the rejected property");
            AssertTrue(
                diagnostic.LineNumber > 0 &&
                diagnostic.ElementPath.IndexOf("ItemsControl") >= 0,
                "final host failure points at the ItemsControl declaration");

            bool missingTemplateRejected = false;

            try
            {
                XamlRuntime runtime = XamlRuntime.Load(
                    "<Form><ItemsControl VirtualizationMode='Lightweight' " +
                    "Virtualizing='true' AutoScroll='true' " +
                    "Orientation='Vertical' FixedItemSize='24' />" +
                    "</Form>");
                runtime.Dispose();
            }
            catch (InvalidOperationException)
            {
                missingTemplateRejected = true;
            }

            AssertTrue(
                missingTemplateRejected,
                "final lightweight host requires an ItemTemplate without a source");
        }

        private static void TestFinalizedLightweightConfigurationMutationsAreTransactional()
        {
            string xml =
                "<Form><ItemsControl Name='Rows' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='24'>" +
                "<ItemsControl.ItemTemplate><Label Text='Ready' />" +
                "</ItemsControl.ItemTemplate></ItemsControl></Form>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");
                AssertRejectedConfigurationMutation(
                    delegate { host.Orientation = Orientation.Horizontal; },
                    "Orientation");
                AssertEqual(
                    Orientation.Vertical,
                    host.Orientation,
                    "rejected orientation restores the previous value");
                AssertRejectedConfigurationMutation(
                    delegate { host.Virtualizing = false; },
                    "Virtualizing");
                AssertTrue(
                    host.Virtualizing,
                    "rejected virtualization mutation restores true");
                AssertRejectedConfigurationMutation(
                    delegate { host.FixedItemSize = 0; },
                    "FixedItemSize");
                AssertEqual(
                    24,
                    host.FixedItemSize,
                    "rejected fixed size restores the previous value");
                AssertRejectedConfigurationMutation(
                    delegate { host.AutoScroll = false; },
                    "AutoScroll");
                AssertTrue(
                    host.AutoScroll,
                    "rejected scrolling mutation restores true");
            }

            string controlsXml =
                "<Form><ItemsControl Name='Rows' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' FixedItemSize='0'>" +
                "<ItemsControl.ItemTemplate><Label Text='Ready' />" +
                "</ItemsControl.ItemTemplate></ItemsControl></Form>";

            using (XamlRuntime runtime = XamlRuntime.Load(controlsXml))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");
                AssertRejectedConfigurationMutation(
                    delegate
                    {
                        host.VirtualizationMode =
                            ItemsControlVirtualizationMode.Lightweight;
                    },
                    "VirtualizationMode");
                AssertEqual(
                    ItemsControlVirtualizationMode.Controls,
                    host.VirtualizationMode,
                    "rejected mode entry restores Controls");
            }
        }

        private delegate void ConfigurationMutation();

        private static void AssertRejectedConfigurationMutation(
            ConfigurationMutation mutation,
            string name)
        {
            bool rejected = false;

            try
            {
                mutation();
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            AssertTrue(rejected, "invalid " + name + " mutation is rejected");
        }

        private static void TestExplicitLightweightTemplateLoadsWithoutRowControls()
        {
            State state = new State();
            string xml =
                "<Form Width='360' Height='220'>" +
                "<ItemsControl Name='Rows' Width='320' Height='160' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='48' Spacing='2'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Border Padding='6' BorderBrush='#CCCCCC' BorderThickness='1'>" +
                "<StackPanel Orientation='Horizontal' Spacing='6'>" +
                "<Image Width='24' Source='{Binding Picture}' Stretch='Uniform' />" +
                "<CheckBox Width='24' " +
                "Checked='{Binding Enabled, Mode=TwoWay}' />" +
                "<Label Text='{Binding Title}' AutoEllipsis='true' />" +
                "<HyperlinkLabel Width='70' Text='Open' " +
                "NavigateUri='{Binding Url}' />" +
                "</StackPanel>" +
                "</Border>" +
                "</ItemsControl.ItemTemplate>" +
                "</ItemsControl>" +
                "</Form>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml, state))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");

                AssertTrue(host != null, "named lightweight host is available");
                AssertEqual(2, host.Count, "lightweight source count");
                AssertTrue(host.IsVirtualizing, "lightweight backend is active");
                AssertEqual(
                    ItemsControlVirtualizationMode.Lightweight,
                    host.VirtualizationMode,
                    "explicit backend remains visible through the API");
                AssertEqual(
                    1,
                    host.Controls.Count,
                    "only the ItemsControl scroll-extent marker exists; no row controls");
                AssertEqual(0, host.VirtualCacheCount, "no detached row trees exist");

                host.ReloadItems();
                host.ScrollToIndex(1);
                AssertEqual(2, host.Count, "reload and scroll preserve logical rows");
            }
        }

        private static void TestIndexedSnapshotStateSharesBrushesAcrossTenThousandRows()
        {
            const int rowCount = 10000;
            PaletteState state = new PaletteState(rowCount);
            string xml =
                "<Form Width='260' Height='150'>" +
                "<ItemsControl Name='Rows' Width='220' Height='96' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='24' OverscanItems='2'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Border Background='{Binding Background}' " +
                "BorderBrush='#445566' BorderThickness='1' Padding='2'>" +
                "<StackPanel Orientation='Horizontal' Spacing='3'>" +
                "<CheckBox Width='80' Text='{Binding Title}' " +
                "Checked='{Binding Enabled, Mode=TwoWay}' " +
                "Foreground='#778899' />" +
                "<HyperlinkLabel Width='80' Text='Open' " +
                "Background='#334455' LinkColor='#0066CC' " +
                "NavigateUri='{Binding Url}' />" +
                "</StackPanel></Border>" +
                "</ItemsControl.ItemTemplate>" +
                "</ItemsControl></Form>";
            XamlRuntime runtime = XamlRuntime.Load(xml, state);
            ItemsControl host = runtime.Get<ItemsControl>("Rows");
            long createdBrushes = 0L;

            try
            {
                host.CreateControl();

                using (Bitmap canvas = new Bitmap(220, 96))
                {
                    host.DrawToBitmap(
                        canvas,
                        new Rectangle(0, 0, 220, 96));

                    int i;

                    for (i = 0; i < rowCount; i++)
                        host.ScrollToIndex(i);

                    host.DrawToBitmap(
                        canvas,
                        new Rectangle(0, 0, 220, 96));
                }

                AssertEqual(rowCount, host.Count, "the stress source remains intact");
                AssertTrue(
                    host.LightweightRowCache.Count <=
                        host.RealizedCount + (host.OverscanItems * 2),
                    "ten-thousand-row scrolling retains only the visible cache budget");
                AssertEqual(
                    4,
                    host.LightweightSharedBrushCountForTest,
                    "four template colors share four host brushes");
                AssertEqual(
                    4L,
                    host.LightweightBrushCreateCountForTest,
                    "brush creation is independent of realized row churn");

                object snapshot = null;

                foreach (DictionaryEntry entry in host.LightweightRowCache)
                {
                    snapshot = entry.Value;
                    break;
                }

                AssertTrue(snapshot != null, "a realized snapshot is available");
                Type snapshotType = snapshot.GetType();
                FieldInfo values = snapshotType.GetField(
                    "Values",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                FieldInfo converted = snapshotType.GetField(
                    "ConvertedValues",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                FieldInfo text = snapshotType.GetField(
                    "TextValues",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                FieldInfo images = snapshotType.GetField(
                    "Images",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                FieldInfo links = snapshotType.GetField(
                    "LinkKeys",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                FieldInfo thumbnailSources = snapshotType.GetField(
                    "ThumbnailSources",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                AssertEqual(typeof(object[]), values.FieldType, "values use slot arrays");
                AssertEqual(
                    typeof(object[]),
                    converted.FieldType,
                    "converted values use slot arrays");
                AssertEqual(
                    typeof(object[]),
                    text.FieldType,
                    "text values use slot arrays");
                AssertEqual(typeof(object[]), images.FieldType, "images use node arrays");
                AssertTrue(links.FieldType.IsArray, "link keys use link-id arrays");
                AssertEqual(
                    typeof(Image[]),
                    thumbnailSources.FieldType,
                    "thumbnail ownership uses node arrays");
                AssertTrue(
                    snapshotType.GetField(
                        "Brushes",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic) == null,
                    "snapshots no longer own per-row brush stores");

                Array valueSlots = values.GetValue(snapshot) as Array;
                AssertEqual(
                    host.LightweightPlan.NextValueSlotId,
                    valueSlots.Length,
                    "row value arrays exactly match the compiled slot plan");
                AssertEqual(
                    host.LightweightPlan.NextNodeId,
                    ((Array)images.GetValue(snapshot)).Length,
                    "image arrays exactly match the compiled node plan");
                AssertEqual(
                    host.LightweightPlan.NextLinkId,
                    ((Array)links.GetValue(snapshot)).Length,
                    "link arrays exactly match the compiled link plan");

                int arrayStoreCount = 0;
                int hashtableStoreCount = 0;
                FieldInfo[] snapshotFields = snapshotType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                int fieldIndex;

                for (fieldIndex = 0;
                     fieldIndex < snapshotFields.Length;
                     fieldIndex++)
                {
                    if (snapshotFields[fieldIndex].FieldType.IsArray)
                        arrayStoreCount++;
                    else if (snapshotFields[fieldIndex].FieldType ==
                        typeof(Hashtable))
                    {
                        hashtableStoreCount++;
                    }
                }

                AssertEqual(
                    6,
                    arrayStoreCount,
                    "six plan-sized arrays replace per-row slot collections");
                AssertEqual(
                    1,
                    hashtableStoreCount,
                    "only expression-keyed function memoization remains hashed");
                createdBrushes = host.LightweightBrushCreateCountForTest;
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                createdBrushes,
                host.LightweightBrushDisposeCountForTest,
                "template disposal releases every shared brush exactly once");
            AssertEqual(
                0,
                host.LightweightSharedBrushCountForTest,
                "disposed templates retain no shared brush cache");
        }

        private static void TestCachedNullValueIsNotReevaluatedWhilePainting()
        {
            NullTextRow row = new NullTextRow();
            NullTextState state = new NullTextState(row);
            string xml =
                "<ItemsControl Name='Rows' Width='120' Height='48' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='24'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Label Text='{Binding Title}' />" +
                "</ItemsControl.ItemTemplate></ItemsControl>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml, state))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");
                int readsAfterPreparation = row.ReadCount;
                AssertTrue(
                    readsAfterPreparation > 0,
                    "the null binding is evaluated during row preparation");

                using (Bitmap canvas = new Bitmap(120, 48))
                {
                    host.DrawToBitmap(
                        canvas,
                        new Rectangle(0, 0, 120, 48));
                    host.DrawToBitmap(
                        canvas,
                        new Rectangle(0, 0, 120, 48));
                }

                AssertEqual(
                    readsAfterPreparation,
                    row.ReadCount,
                    "cached null values are distinct from uncached slots");
            }
        }

        private static void TestIndexedPresetStateReloadsDefaultAndUnsetValues()
        {
            State state = new State();
            string xml =
                "<Form Width='160' Height='80'>" +
                "<Presets Name='Theme' Selected='Dark' Default='Base'>" +
                "<Preset Name='Base'>" +
                "<Set Key='DefaultBackground' Value='#ABCDEF' />" +
                "</Preset>" +
                "<Preset Name='Light' />" +
                "<Preset Name='Dark'>" +
                "<Set Key='SelectedBackground' Value='Red' />" +
                "</Preset>" +
                "</Presets>" +
                "<ItemsControl Name='Rows' Width='120' Height='48' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='24' OverscanItems='0'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Border Background='{Preset Theme.SelectedBackground}'>" +
                "<Label Text='{Binding Title}' " +
                "Background='{Preset Theme.DefaultBackground}' />" +
                "</Border>" +
                "</ItemsControl.ItemTemplate></ItemsControl></Form>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml, state))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");
                CreateHandleAndDrain(runtime.RootControl);
                int selectedSlot = host.LightweightPlan.Root.BackColor.Id;
                XamlRuntime.LightweightTemplateNode label =
                    host.LightweightPlan.Root.Children[0]
                        as XamlRuntime.LightweightTemplateNode;
                int defaultSlot = label.BackColor.Id;

                AssertEqual(
                    Color.Red,
                    GetLightweightConvertedColor(host, 0, selectedSlot),
                    "the selected preset initially populates its indexed slot");
                AssertEqual(
                    Color.FromArgb(0xAB, 0xCD, 0xEF),
                    GetLightweightConvertedColor(host, 0, defaultSlot),
                    "a selected miss initially resolves through the default preset");

                runtime.Presets.Select("Theme", "Light");
                Drain(runtime.RootControl);

                AssertEqual(
                    Color.Transparent,
                    GetLightweightConvertedColor(host, 0, selectedSlot),
                    "an unresolved selected slot restores the paint fallback");
                AssertEqual(
                    Color.FromArgb(0xAB, 0xCD, 0xEF),
                    GetLightweightConvertedColor(host, 0, defaultSlot),
                    "the configured default remains resolved after reload");

                runtime.Presets.Select("Theme", "Dark");
                runtime.Presets.Select("Theme", "Light");
                Drain(runtime.RootControl);
                AssertEqual(
                    Color.Transparent,
                    GetLightweightConvertedColor(host, 0, selectedSlot),
                    "repeated preset reloads cannot retain a stale slot value");
            }
        }

        private static Color GetLightweightConvertedColor(
            ItemsControl host,
            int rowIndex,
            int slotId)
        {
            object snapshot = host.LightweightRowCache[rowIndex];
            AssertTrue(snapshot != null, "the requested lightweight row is prepared");
            FieldInfo field = snapshot.GetType().GetField(
                "ConvertedValues",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            object[] values = field.GetValue(snapshot) as object[];
            AssertTrue(
                values != null && values[slotId] is Color,
                "the requested lightweight color slot is converted");
            return (Color)values[slotId];
        }

        private static void TestUnsupportedElementHasLocationDiagnostic()
        {
            string xml =
                "<Form>\n" +
                "  <ItemsControl VirtualizationMode='Lightweight' " +
                "Virtualizing='true' AutoScroll='true' " +
                "Orientation='Vertical' FixedItemSize='32'>\n" +
                "    <ItemsControl.ItemTemplate>\n" +
                "      <Button Text='{Binding Title}' />\n" +
                "    </ItemsControl.ItemTemplate>\n" +
                "  </ItemsControl>\n" +
                "</Form>";
            WinFormsXamlLoadException observed = null;

            try
            {
                XamlRuntime runtime = XamlRuntime.Load(xml, new State());
                runtime.Dispose();
            }
            catch (WinFormsXamlLoadException ex)
            {
                observed = ex;
            }

            AssertTrue(observed != null, "unsupported lightweight element fails");
            AssertTrue(observed.LineNumber > 0, "failure retains source line");
            AssertTrue(
                observed.ElementPath != null &&
                observed.ElementPath.IndexOf("Button") >= 0,
                "failure identifies the unsupported element");
        }

        private static void TestEnabledCheckboxRequiresTwoWayBinding()
        {
            string xml =
                "<Form>\n" +
                "  <ItemsControl VirtualizationMode='Lightweight' " +
                "Virtualizing='true' AutoScroll='true' " +
                "Orientation='Vertical' FixedItemSize='32'>\n" +
                "    <ItemsControl.ItemTemplate>\n" +
                "      <CheckBox Checked='{Binding Enabled}' />\n" +
                "    </ItemsControl.ItemTemplate>\n" +
                "  </ItemsControl>\n" +
                "</Form>";
            InvalidOperationException observed = null;

            try
            {
                XamlRuntime runtime = XamlRuntime.Load(xml, new State());
                runtime.Dispose();
            }
            catch (InvalidOperationException ex)
            {
                observed = ex;
            }

            AssertTrue(observed != null, "one-way lightweight checkbox fails");
            AssertTrue(
                observed.Message.IndexOf("Mode=TwoWay") >= 0,
                "checkbox diagnostic explains the durable-state requirement");
        }

        private static void TestLightweightTwoWaySourceValidation()
        {
            AssertLightweightLoadRejected(
                "<Form>\n  <ItemsControl VirtualizationMode='Lightweight' " +
                "Virtualizing='true' AutoScroll='true' " +
                "Orientation='Vertical' FixedItemSize='24'>" +
                "<ItemsControl.ItemTemplate>" +
                "<CheckBox Checked='{Binding !Enabled, Mode=TwoWay}' />" +
                "</ItemsControl.ItemTemplate></ItemsControl>\n</Form>",
                null,
                "Checked",
                "negated TwoWay checkbox path");
            AssertLightweightLoadRejected(
                "<Form>\n  <ItemsControl VirtualizationMode='Lightweight' " +
                "Virtualizing='true' AutoScroll='true' " +
                "Orientation='Vertical' FixedItemSize='24'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Label Text='{Binding Title, Mode=TwoWay}' />" +
                "</ItemsControl.ItemTemplate></ItemsControl>\n</Form>",
                null,
                "Text",
                "TwoWay binding on a paint-only slot");

            string endpointXml =
                "<Form>\n  <ItemsControl ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='24'>" +
                "<ItemsControl.ItemTemplate>" +
                "<CheckBox Checked='{Binding Enabled, Mode=TwoWay}' />" +
                "</ItemsControl.ItemTemplate></ItemsControl>\n</Form>";
            AssertLightweightLoadRejected(
                endpointXml,
                new CheckState(new ReadOnlyCheckRow()),
                "Checked",
                "readonly notifying checkbox endpoint");
            AssertLightweightLoadRejected(
                endpointXml,
                new CheckState(new SilentWritableCheckRow()),
                "Checked",
                "writable non-notifying checkbox endpoint");
        }

        private static void AssertLightweightLoadRejected(
            string xml,
            object dataContext,
            string propertyName,
            string message)
        {
            WinFormsXamlLoadException observed = null;

            try
            {
                XamlRuntime runtime = XamlRuntime.Load(xml, dataContext);
                runtime.Dispose();
            }
            catch (WinFormsXamlLoadException ex)
            {
                observed = ex;
            }

            AssertTrue(observed != null, message + " is rejected");
            AssertEqual(
                propertyName,
                observed.PropertyName,
                message + " identifies the property");
            AssertTrue(
                observed.LineNumber > 0,
                message + " retains source location");
        }

        private static void TestRejectedActivationKeepsCommittedControls()
        {
            string xml =
                "<Form Width='240' Height='160'>" +
                "<ItemsControl Name='Rows' Width='200' Height='120' " +
                "ItemsSource='{Binding Rows}' ProgressiveRendering='false' " +
                "Virtualizing='true' AutoScroll='true' Orientation='Vertical'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Label Text='{Binding Title}' />" +
                "</ItemsControl.ItemTemplate>" +
                "</ItemsControl>" +
                "</Form>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml, new State()))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");
                Control[] committed = new Control[host.Controls.Count];
                int i;

                for (i = 0; i < committed.Length; i++)
                    committed[i] = host.Controls[i];

                InvalidOperationException observed = null;

                try
                {
                    // FixedItemSize remains zero, so explicit Lightweight
                    // activation must fail before native rows are retired.
                    host.VirtualizationMode =
                        ItemsControlVirtualizationMode.Lightweight;
                }
                catch (InvalidOperationException ex)
                {
                    observed = ex;
                }

                AssertTrue(observed != null, "ineligible activation fails");
                AssertEqual(
                    ItemsControlVirtualizationMode.Controls,
                    host.VirtualizationMode,
                    "rejected activation restores the previous backend mode");
                AssertEqual(
                    committed.Length,
                    host.Controls.Count,
                    "failed activation retains the committed Control count");

                for (i = 0; i < committed.Length; i++)
                {
                    AssertTrue(
                        Object.ReferenceEquals(committed[i], host.Controls[i]) &&
                        !committed[i].IsDisposed,
                        "failed activation retains each committed Control");
                }
            }
        }

        private static void TestImageRequiresOwnedSafeSourceShape()
        {
            string xml =
                "<Form>\n" +
                "  <ItemsControl VirtualizationMode='Lightweight' " +
                "Virtualizing='true' AutoScroll='true' " +
                "Orientation='Vertical' FixedItemSize='32'>\n" +
                "    <ItemsControl.ItemTemplate>\n" +
                "      <Image Source='photo.png' Stretch='Uniform' />\n" +
                "    </ItemsControl.ItemTemplate>\n" +
                "  </ItemsControl>\n" +
                "</Form>";
            InvalidOperationException observed = null;

            try
            {
                XamlRuntime runtime = XamlRuntime.Load(xml, new State());
                runtime.Dispose();
            }
            catch (InvalidOperationException ex)
            {
                observed = ex;
            }

            AssertTrue(observed != null, "URI image loading is rejected");
            AssertTrue(
                observed.Message.IndexOf("Image, Icon, or encoded byte[]") >= 0,
                "image diagnostic routes URI loading to Controls mode");
        }

        private static void TestImageRequiresOneCompleteExpression()
        {
            string xml =
                "<Form>\n" +
                "  <ItemsControl VirtualizationMode='Lightweight' " +
                "Virtualizing='true' AutoScroll='true' " +
                "Orientation='Vertical' FixedItemSize='32'>\n" +
                "    <ItemsControl.ItemTemplate>\n" +
                "      <Image Source='prefix-{Binding Picture}' />\n" +
                "    </ItemsControl.ItemTemplate>\n" +
                "  </ItemsControl>\n" +
                "</Form>";
            InvalidOperationException observed = null;

            try
            {
                XamlRuntime runtime = XamlRuntime.Load(xml, new State());
                runtime.Dispose();
            }
            catch (InvalidOperationException ex)
            {
                observed = ex;
            }

            AssertTrue(observed != null, "interpolated Image.Source fails");
            AssertTrue(
                observed.Message.IndexOf("one complete") >= 0,
                "image diagnostic requires one complete object expression");
        }

        private static void TestStaleSnapshotsReleaseDecodedImages()
        {
            string xml =
                "<Form Width='180' Height='100'>" +
                "<ItemsControl Name='Rows' Width='120' Height='48' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='32'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Image Width='16' Height='16' " +
                "Source='{Binding Picture}' Stretch='Uniform' />" +
                "</ItemsControl.ItemTemplate>" +
                "</ItemsControl>" +
                "</Form>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml, new IconState()))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");

                using (Bitmap canvas = new Bitmap(120, 48))
                    host.DrawToBitmap(canvas, new Rectangle(0, 0, 120, 48));

                object generationSnapshot = host.LightweightRowCache[0];
                Image generationThumbnail =
                    GetFirstLightweightThumbnail(host);
                Hashtable owned = GetOwnedPropertyValues(runtime);

                AssertTrue(
                    generationSnapshot != null &&
                    owned.ContainsKey(generationSnapshot),
                    "decoded Icon conversion is owned by its row snapshot");

                host.LightweightGeneration++;

                using (Bitmap canvas = new Bitmap(120, 48))
                    host.DrawToBitmap(canvas, new Rectangle(0, 0, 120, 48));

                AssertTrue(
                    !owned.ContainsKey(generationSnapshot),
                    "generation replacement releases the stale snapshot");
                AssertTrue(
                    IsDisposedImage(generationThumbnail),
                    "generation replacement disposes its resized thumbnail");

                object itemSnapshot = host.LightweightRowCache[0];
                Image itemThumbnail = GetFirstLightweightThumbnail(host);
                host.ItemValues[0] = new IconRow(SystemIcons.Warning);

                using (Bitmap canvas = new Bitmap(120, 48))
                    host.DrawToBitmap(canvas, new Rectangle(0, 0, 120, 48));

                AssertTrue(
                    !owned.ContainsKey(itemSnapshot),
                    "item replacement releases the stale snapshot");
                AssertTrue(
                    IsDisposedImage(itemThumbnail),
                    "source replacement disposes its resized thumbnail");
            }
        }

        private static void TestRuntimeOwnedImagesUseBoundedThumbnailCache()
        {
            byte[] shared = CreatePngBytes(32, 32, Color.Orange);
            EncodedImageState sharedState = new EncodedImageState(
                new byte[][] { shared, shared });
            string sharedXml =
                "<Form Width='80' Height='100'>" +
                "<ItemsControl Name='Rows' Width='40' Height='42' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='20'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Image Width='16' Height='16' " +
                "Source='{Binding Picture}' Stretch='Uniform' />" +
                "</ItemsControl.ItemTemplate>" +
                "</ItemsControl>" +
                "</Form>";

            using (XamlRuntime runtime =
                XamlRuntime.Load(sharedXml, sharedState))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");

                using (Bitmap canvas = new Bitmap(40, 42))
                    host.DrawToBitmap(canvas, new Rectangle(0, 0, 40, 42));

                AssertEqual(
                    1,
                    host.LightweightThumbnailCache.Count,
                    "rows sharing one runtime decode share one thumbnail");

                object cacheEntry = host.LightweightThumbnailCache[0];
                Image thumbnail = GetFirstLightweightThumbnail(host);

                AssertTrue(
                    thumbnail != null &&
                    thumbnail.Width == 16 &&
                    thumbnail.Height == 16,
                    "eligible downscaling stores the exact rendered size");

                using (Bitmap canvas = new Bitmap(40, 42))
                    host.DrawToBitmap(canvas, new Rectangle(0, 0, 40, 42));

                AssertSame(
                    cacheEntry,
                    host.LightweightThumbnailCache[0],
                    "an unchanged paint reuses the cached thumbnail");

                host.ReloadItems();

                AssertEqual(
                    0,
                    host.LightweightThumbnailCache.Count,
                    "logical refresh clears the thumbnail cache");
                AssertTrue(
                    IsDisposedImage(thumbnail),
                    "logical refresh disposes the retired thumbnail");
            }

            int sourceCount =
                XamlRuntime.LightweightThumbnailCacheLimit + 2;
            byte[][] distinct = new byte[sourceCount][];
            int i;

            for (i = 0; i < distinct.Length; i++)
            {
                distinct[i] = CreatePngBytes(
                    32,
                    32,
                    Color.FromArgb(
                        255,
                        (i * 37) % 256,
                        (i * 71) % 256,
                        (i * 113) % 256));
            }

            EncodedImageState distinctState =
                new EncodedImageState(distinct);
            string boundedXml =
                "<Form Width='80' Height='440'>" +
                "<ItemsControl Name='Rows' Width='40' Height='380' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='20'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Image Width='16' Height='16' " +
                "Source='{Binding Picture}' Stretch='Uniform' />" +
                "</ItemsControl.ItemTemplate>" +
                "</ItemsControl>" +
                "</Form>";

            using (XamlRuntime runtime =
                XamlRuntime.Load(boundedXml, distinctState))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");

                using (Bitmap canvas = new Bitmap(40, 380))
                    host.DrawToBitmap(canvas, new Rectangle(0, 0, 40, 380));

                AssertEqual(
                    XamlRuntime.LightweightThumbnailCacheLimit,
                    host.LightweightThumbnailCache.Count,
                    "distinct thumbnails stop at the per-host entry limit");
            }
        }

        private static void TestCallerOwnedImagesBypassThumbnailCache()
        {
            using (Bitmap source = new Bitmap(32, 32))
            {
                using (Graphics graphics = Graphics.FromImage(source))
                    graphics.Clear(Color.Red);

                ExternalImageState state = new ExternalImageState(source);
                string xml =
                    "<Form Width='80' Height='80'>" +
                    "<ItemsControl Name='Rows' Width='24' Height='20' " +
                    "ItemsSource='{Binding Rows}' " +
                    "VirtualizationMode='Lightweight' Virtualizing='true' " +
                    "AutoScroll='true' Orientation='Vertical' " +
                    "FixedItemSize='20'>" +
                    "<ItemsControl.ItemTemplate>" +
                    "<Image Width='16' Height='16' " +
                    "Source='{Binding Picture}' Stretch='Uniform' />" +
                    "</ItemsControl.ItemTemplate>" +
                    "</ItemsControl>" +
                    "</Form>";

                using (XamlRuntime runtime = XamlRuntime.Load(xml, state))
                {
                    ItemsControl host = runtime.Get<ItemsControl>("Rows");

                    using (Bitmap canvas = new Bitmap(24, 20))
                    {
                        host.DrawToBitmap(
                            canvas,
                            new Rectangle(0, 0, 24, 20));
                        AssertEqual(
                            Color.Red.ToArgb(),
                            canvas.GetPixel(8, 8).ToArgb(),
                            "the caller image paints its current pixels");
                    }

                    AssertTrue(
                        host.LightweightThumbnailCache == null ||
                        host.LightweightThumbnailCache.Count == 0,
                        "caller-owned Image values never enter the thumbnail cache");

                    using (Graphics graphics = Graphics.FromImage(source))
                        graphics.Clear(Color.Blue);

                    using (Bitmap canvas = new Bitmap(24, 20))
                    {
                        host.DrawToBitmap(
                            canvas,
                            new Rectangle(0, 0, 24, 20));
                        AssertEqual(
                            Color.Blue.ToArgb(),
                            canvas.GetPixel(8, 8).ToArgb(),
                            "in-place caller mutations remain visible immediately");
                    }
                }

                AssertEqual(
                    32,
                    source.Width,
                    "runtime disposal preserves caller image ownership");
            }
        }

        private static void TestCachedImageStretchSemantics()
        {
            byte[] stripes = CreateStripedPngBytes();
            int cacheCount;

            using (Bitmap fill = RenderEncodedImage(
                stripes,
                "Fill",
                20,
                10,
                out cacheCount))
            {
                AssertEqual(
                    1,
                    cacheCount,
                    "Fill downscaling uses one thumbnail");
                AssertEqual(
                    Color.Red.ToArgb(),
                    fill.GetPixel(2, 5).ToArgb(),
                    "cached Fill preserves the left source stripe");
                AssertEqual(
                    Color.Yellow.ToArgb(),
                    fill.GetPixel(17, 5).ToArgb(),
                    "cached Fill preserves the right source stripe");
            }

            using (Bitmap cover = RenderEncodedImage(
                stripes,
                "UniformToFill",
                10,
                10,
                out cacheCount))
            {
                AssertEqual(
                    1,
                    cacheCount,
                    "UniformToFill downscaling uses one thumbnail");
                AssertEqual(
                    Color.Green.ToArgb(),
                    cover.GetPixel(1, 5).ToArgb(),
                    "cached UniformToFill crops the outer-left stripe");
                AssertEqual(
                    Color.Blue.ToArgb(),
                    cover.GetPixel(8, 5).ToArgb(),
                    "cached UniformToFill crops the outer-right stripe");
            }

            byte[] partialUpscale =
                CreatePngBytes(1000, 1, Color.Purple);

            using (Bitmap fill = RenderEncodedImage(
                partialUpscale,
                "Fill",
                10,
                10,
                out cacheCount))
            {
                AssertEqual(
                    0,
                    cacheCount,
                    "Fill bypasses the cache when either axis is upscaled");
                AssertEqual(
                    Color.Purple.ToArgb(),
                    fill.GetPixel(5, 5).ToArgb(),
                    "partial-upscale fallback still paints directly");
            }
        }

        private static Bitmap RenderEncodedImage(
            byte[] source,
            string stretch,
            int width,
            int height,
            out int cacheCount)
        {
            EncodedImageState state = new EncodedImageState(
                new byte[][] { source });
            string widthText = width.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            string heightText = height.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            string xml =
                "<Form Width='400' Height='100'>" +
                "<ItemsControl Name='Rows' Width='" + widthText +
                "' Height='" + heightText + "' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='" + heightText + "'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Image Width='" + widthText + "' Height='" +
                heightText + "' Source='{Binding Picture}' Stretch='" +
                stretch + "' />" +
                "</ItemsControl.ItemTemplate>" +
                "</ItemsControl>" +
                "</Form>";
            Bitmap canvas = new Bitmap(width, height);

            try
            {
                using (XamlRuntime runtime = XamlRuntime.Load(xml, state))
                {
                    ItemsControl host = runtime.Get<ItemsControl>("Rows");
                    host.DrawToBitmap(
                        canvas,
                        new Rectangle(0, 0, width, height));
                    cacheCount = host.LightweightThumbnailCache == null
                        ? 0
                        : host.LightweightThumbnailCache.Count;
                }
            }
            catch
            {
                canvas.Dispose();
                throw;
            }

            return canvas;
        }

        private static Image GetFirstLightweightThumbnail(ItemsControl host)
        {
            AssertTrue(
                host.LightweightThumbnailCache != null &&
                host.LightweightThumbnailCache.Count > 0,
                "a lightweight thumbnail is cached");
            object entry = host.LightweightThumbnailCache[0];
            FieldInfo field = entry.GetType().GetField(
                "Thumbnail",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            AssertTrue(field != null, "thumbnail cache entry exposes its bitmap");
            return field.GetValue(entry) as Image;
        }

        private static bool IsDisposedImage(Image image)
        {
            if (image == null)
                return false;

            try
            {
                int width = image.Width;
                return width < 0;
            }
            catch
            {
                return true;
            }
        }

        private static byte[] CreatePngBytes(
            int width,
            int height,
            Color color)
        {
            using (MemoryStream stream = new MemoryStream())
            using (Bitmap bitmap = new Bitmap(width, height))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                    graphics.Clear(color);

                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private static byte[] CreateStripedPngBytes()
        {
            using (MemoryStream stream = new MemoryStream())
            using (Bitmap bitmap = new Bitmap(40, 20))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.FillRectangle(Brushes.Red, 0, 0, 10, 20);
                    graphics.FillRectangle(Brushes.Green, 10, 0, 10, 20);
                    graphics.FillRectangle(Brushes.Blue, 20, 0, 10, 20);
                    graphics.FillRectangle(Brushes.Yellow, 30, 0, 10, 20);
                }

                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private static Hashtable GetOwnedPropertyValues(
            XamlRuntime runtime)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                "_ownedPropertyValues",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Hashtable values = field == null
                ? null
                : field.GetValue(runtime) as Hashtable;

            AssertTrue(values != null, "runtime ownership table is available");
            return values;
        }

        private static void TestOverscanPreparesAheadButPaintRangeStaysVisible()
        {
            ReactiveState state = new ReactiveState(30);
            string xml =
                "<ItemsControl Name='Rows' Width='180' Height='60' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='20' OverscanItems='2'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Label Text='{Function FormatTitle(Details.Title)}' />" +
                "</ItemsControl.ItemTemplate></ItemsControl>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml, state))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");
                host.CreateControl();
                AssertPreparedRangeMatchesPolicy(host, "initial symmetric overscan");

                int visibleFirst;
                int visibleLast;
                XamlRuntime.GetLightweightVisibleRange(
                    host,
                    out visibleFirst,
                    out visibleLast);
                int preparedFirst;
                int preparedLast;
                XamlRuntime.GetLightweightPreparedRange(
                    host,
                    visibleFirst,
                    visibleLast,
                    out preparedFirst,
                    out preparedLast);

                AssertEqual(
                    visibleLast - visibleFirst + 1,
                    host.RealizedCount,
                    "overscan does not expand the painted realized count");
                AssertTrue(
                    ((ReactiveRow)state.Rows[preparedLast]).Details.ReadCount > 0,
                    "the trailing overscan row is eagerly evaluated");

                if (preparedLast + 1 < state.Rows.Count)
                {
                    AssertEqual(
                        0,
                        ((ReactiveRow)state.Rows[preparedLast + 1]).
                            Details.ReadCount,
                        "a row outside overscan is not evaluated");
                }

                host.AutoScrollPosition = new Point(0, 160);
                int settled = Math.Max(0, -host.AutoScrollPosition.Y);

                if (host.LightweightLastViewportOffset != settled)
                    runtime.HandleLightweightViewportChanged(host);

                AssertTrue(
                    host.LightweightOverscanDirection > 0,
                    "a downward viewport change records forward direction");
                XamlRuntime.GetLightweightVisibleRange(
                    host,
                    out visibleFirst,
                    out visibleLast);
                XamlRuntime.GetLightweightPreparedRange(
                    host,
                    visibleFirst,
                    visibleLast,
                    out preparedFirst,
                    out preparedLast);
                AssertEqual(
                    visibleFirst,
                    preparedFirst,
                    "forward overscan moves the full budget ahead");
                AssertEqual(
                    Math.Min(host.Count - 1, visibleLast + 4),
                    preparedLast,
                    "forward overscan retains the same two-sided budget");
                AssertPreparedRangeMatchesPolicy(host, "directional overscan");

                runtime.HandleLightweightViewportChanged(host);
                AssertEqual(
                    0,
                    host.LightweightOverscanDirection,
                    "a stationary viewport returns to symmetric overscan");
                AssertPreparedRangeMatchesPolicy(host, "stationary overscan");

                host.OverscanItems = Int32.MaxValue;
                AssertPreparedRangeMatchesPolicy(
                    host,
                    "maximum overscan saturates without wrapping");
                AssertEqual(
                    host.Count,
                    host.LightweightRowCache.Count,
                    "maximum overscan remains bounded by the source count");
            }
        }

        private static void AssertPreparedRangeMatchesPolicy(
            ItemsControl host,
            string message)
        {
            int visibleFirst;
            int visibleLast;
            XamlRuntime.GetLightweightVisibleRange(
                host,
                out visibleFirst,
                out visibleLast);
            int preparedFirst;
            int preparedLast;
            XamlRuntime.GetLightweightPreparedRange(
                host,
                visibleFirst,
                visibleLast,
                out preparedFirst,
                out preparedLast);
            int i;

            for (i = preparedFirst; i <= preparedLast; i++)
            {
                AssertTrue(
                    host.LightweightRowCache.ContainsKey(i),
                    message + " prepares every policy row");
            }

            foreach (DictionaryEntry entry in host.LightweightRowCache)
            {
                int index = (int)entry.Key;
                AssertTrue(
                    index >= preparedFirst && index <= preparedLast,
                    message + " retains no row outside policy");
            }
        }

        private static void TestObservableRowsRebuildIndependentlyAndDetach()
        {
            ReactiveState state = new ReactiveState(20);
            string xml =
                "<ItemsControl Name='Rows' Width='180' Height='42' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='20' OverscanItems='0'>" +
                "<ItemsControl.ItemTemplate>" +
                "<Label Text='{Function FormatTitle(Details.Title)}' />" +
                "</ItemsControl.ItemTemplate></ItemsControl>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml, state))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");
                CreateHandleAndDrain(host);
                object firstSnapshot = host.LightweightRowCache[0];
                object secondSnapshot = host.LightweightRowCache[1];
                int generation = host.LightweightGeneration;
                ReactiveRow first = (ReactiveRow)state.Rows[0];
                ReactiveDetails retiredDetails = first.Details;

                AssertEqual(
                    host.LightweightRowCache.Count,
                    host.ActiveItemBindingSubscriptionCount,
                    "each cached reactive lightweight row owns one aggregate subscription");

                first.Details.Title = "Changed";
                Drain(runtime.RootControl);
                AssertTrue(
                    !Object.ReferenceEquals(
                        firstSnapshot,
                        host.LightweightRowCache[0]),
                    "an INPC Function argument change rebuilds its live row snapshot");
                AssertSame(
                    secondSnapshot,
                    host.LightweightRowCache[1],
                    "an INPC leaf change leaves another row snapshot intact");
                AssertEqual(
                    generation,
                    host.LightweightGeneration,
                    "an observable row update does not reset the host generation");

                firstSnapshot = host.LightweightRowCache[0];
                first.Other = "Unrelated";
                Drain(runtime.RootControl);
                AssertSame(
                    firstSnapshot,
                    host.LightweightRowCache[0],
                    "an unrelated INPC member does not rebuild the row");

                first.NotifyAll();
                Drain(runtime.RootControl);
                AssertTrue(
                    !Object.ReferenceEquals(
                        firstSnapshot,
                        host.LightweightRowCache[0]),
                    "a wildcard INPC notification rebuilds the row");

                firstSnapshot = host.LightweightRowCache[0];
                ReactiveDetails replacement =
                    new ReactiveDetails("Replacement");
                first.Details = replacement;
                Drain(runtime.RootControl);
                AssertTrue(
                    !Object.ReferenceEquals(
                        firstSnapshot,
                        host.LightweightRowCache[0]),
                    "a nested dependency replacement rebuilds its row");
                AssertEqual(
                    0,
                    retiredDetails.SubscriberCount,
                    "a replaced nested dependency is detached");
                AssertTrue(
                    replacement.SubscriberCount > 0,
                    "the replacement nested dependency is observed");

                host.ScrollToIndex(12);
                Drain(runtime.RootControl);
                AssertTrue(
                    Math.Max(0, -host.AutoScrollPosition.Y) > 0 &&
                    host.LightweightRealizedStart > 0 &&
                    !host.LightweightRowCache.ContainsKey(0),
                    "the native scroll chain moves and retires the first row " +
                    "before subscription cleanup is asserted");
                AssertEqual(
                    0,
                    first.SubscriberCount,
                    "retiring a row detaches its parent INPC registration " +
                    "(offset=" +
                    Math.Max(0, -host.AutoScrollPosition.Y) +
                    ", realized=" + host.LightweightRealizedStart + ".." +
                    host.LightweightRealizedEnd +
                    ", cache=" + DescribeLightweightCache(host) +
                    ", active=" +
                    host.ActiveItemBindingSubscriptionCount + ")");
                AssertEqual(
                    0,
                    replacement.SubscriberCount,
                    "retiring a row detaches its nested INPC registration");

                ReactiveRow current = (ReactiveRow)state.Rows[12];
                AssertTrue(
                    current.SubscriberCount > 0,
                    "a current lightweight row remains subscribed before disposal");

                runtime.Dispose();
                AssertEqual(
                    0,
                    host.ActiveItemBindingSubscriptionCount,
                    "runtime disposal clears lightweight subscription diagnostics");
                AssertEqual(
                    0,
                    current.SubscriberCount,
                    "runtime disposal detaches the current row registration");
            }
        }

        private static void TestObservableTwoWayCheckBoxStillWritesThrough()
        {
            ReactiveState state = new ReactiveState(1);
            string xml =
                "<Form Width='120' Height='80'>" +
                "<ItemsControl Name='Rows' Width='80' Height='24' " +
                "ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='20' OverscanItems='0'>" +
                "<ItemsControl.ItemTemplate>" +
                "<CheckBox Checked='{Binding Enabled, Mode=TwoWay}' />" +
                "</ItemsControl.ItemTemplate></ItemsControl></Form>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml, state))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");
                ReactiveRow row = (ReactiveRow)state.Rows[0];
                object previous = host.LightweightRowCache[0];
                CreateHandleAndDrain(runtime.RootControl);

                Thread sourceChange = new Thread(
                    (ThreadStart)delegate
                    {
                        row.Enabled.Value = false;
                    });
                sourceChange.Start();
                sourceChange.Join();

                runtime.ActivateLightweightHitTarget(
                    host,
                    new Point(5, 5));
                Drain(runtime.RootControl);
                AssertTrue(
                    row.Enabled.Value,
                    "the owner-drawn TwoWay checkbox writes its fresh source value");
                AssertTrue(
                    !Object.ReferenceEquals(
                        previous,
                        host.LightweightRowCache[0]),
                    "the TwoWay source notification rebuilds the edited row");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(row.Enabled),
                    "the rebuilt row retains one pooled source subscription");

                object externalSnapshot = host.LightweightRowCache[0];
                row.Enabled.Value = false;
                Drain(runtime.RootControl);
                AssertTrue(
                    !Object.ReferenceEquals(
                        externalSnapshot,
                        host.LightweightRowCache[0]),
                    "an external PropertyBinding change rebuilds the live row");
            }
        }

        private static void TestVisitedLinkStateIsBoundedAndStable()
        {
            int count = XamlRuntime.LightweightVisitedLinkLimit + 2;
            ReactiveState state = new ReactiveState(count);
            string xml =
                "<Form Width='120' Height='80'>" +
                "<ItemsControl Name='Rows' Width='80' Height='24' " +
                "ItemKeyPath='Id' ItemsSource='{Binding Rows}' " +
                "VirtualizationMode='Lightweight' Virtualizing='true' " +
                "AutoScroll='true' Orientation='Vertical' " +
                "FixedItemSize='20' OverscanItems='0'>" +
                "<ItemsControl.ItemTemplate>" +
                "<HyperlinkLabel Text='Open' NavigateUri='{Binding Url}' />" +
                "</ItemsControl.ItemTemplate></ItemsControl></Form>";

            using (XamlRuntime runtime = XamlRuntime.Load(xml, state))
            {
                ItemsControl host = runtime.Get<ItemsControl>("Rows");
                MethodInfo getSnapshot = typeof(XamlRuntime).GetMethod(
                    "GetLightweightRowSnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo prepareSnapshot = typeof(XamlRuntime).GetMethod(
                    "EnsureLightweightRowSnapshotPrepared",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo getKey = typeof(XamlRuntime).GetMethod(
                    "GetLightweightLinkKey",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo mark = typeof(XamlRuntime).GetMethod(
                    "MarkLightweightLinkVisited",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo release = typeof(XamlRuntime).GetMethod(
                    "ReleaseLightweightRowSnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                AssertTrue(
                    getSnapshot != null && prepareSnapshot != null &&
                    getKey != null && mark != null && release != null,
                    "visited-link implementation hooks are discoverable");
                object node = host.LightweightPlan.Root;
                object firstKey = null;
                object lastKey = null;
                int i;

                for (i = 0; i < count; i++)
                {
                    object snapshot = getSnapshot.Invoke(
                        runtime,
                        new object[] { host, i });
                    prepareSnapshot.Invoke(
                        runtime,
                        new object[] { host, snapshot });
                    object key = getKey.Invoke(
                        runtime,
                        new object[] { snapshot, node });

                    if (i == 0)
                    {
                        AssertSame(
                            key,
                            getKey.Invoke(
                                runtime,
                                new object[] { snapshot, node }),
                            "a prepared snapshot reuses its link key");
                    }

                    if (i == 0)
                        firstKey = key;
                    lastKey = key;
                    mark.Invoke(
                        runtime,
                        new object[] { host, snapshot, node });
                    host.LightweightRowCache.Remove(i);
                    release.Invoke(
                        runtime,
                        new object[] { host, snapshot });
                }

                AssertEqual(
                    XamlRuntime.LightweightVisitedLinkLimit,
                    host.LightweightVisitedLinks.Count,
                    "visited-link state is bounded per host");
                AssertEqual(
                    XamlRuntime.LightweightVisitedLinkLimit,
                    host.LightweightVisitedLinkOrder.Count,
                    "visited-link eviction order has the same bound");
                AssertTrue(
                    !host.LightweightVisitedLinks.ContainsKey(firstKey) &&
                    host.LightweightVisitedLinks.ContainsKey(lastKey),
                    "visited-link FIFO eviction is deterministic");

                ((ReactiveRow)state.Rows[0]).Url =
                    "https://example.com/changed";
                object changedSnapshot = getSnapshot.Invoke(
                    runtime,
                    new object[] { host, 0 });
                prepareSnapshot.Invoke(
                    runtime,
                    new object[] { host, changedSnapshot });
                object changedKey = getKey.Invoke(
                    runtime,
                    new object[] { changedSnapshot, node });
                AssertTrue(
                    !Object.Equals(firstKey, changedKey),
                    "a changed destination has distinct visited semantics");
                host.LightweightRowCache.Remove(0);
                release.Invoke(
                    runtime,
                    new object[] { host, changedSnapshot });

                runtime.DeactivateLightweightItemsControl(host);
                AssertTrue(
                    host.LightweightVisitedLinks == null &&
                    host.LightweightVisitedLinkOrder == null,
                    "deactivation releases visited-link storage");
            }
        }

        private static void TestVisibleRangeIsFixedStrideAndBounded()
        {
            using (ItemsControl host = new ItemsControl())
            {
                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 100; i++)
                    rows.Add(i);

                host.FixedItemSize = 20;
                host.Spacing = 4;
                host.Padding = new Padding(0, 8, 0, 0);
                host.ClientSize = new System.Drawing.Size(200, 55);
                host.ItemValues = rows;

                int first;
                int last;
                XamlRuntime.GetLightweightVisibleRange(
                    host,
                    out first,
                    out last);

                AssertEqual(0, first, "fixed range begins at first row");
                AssertEqual(1, last, "spacing is not mistaken for another row");
                AssertTrue(
                    last - first + 1 < rows.Count,
                    "visible realization remains bounded by the viewport");

                host.FixedItemSize = Int32.MaxValue;
                host.Spacing = Int32.MaxValue;
                XamlRuntime.GetLightweightVisibleRange(
                    host,
                    out first,
                    out last);
                AssertEqual(
                    0,
                    first,
                    "large fixed size plus spacing does not overflow the stride");
                AssertEqual(
                    0,
                    last,
                    "large fixed size exposes only the first row");
            }
        }

        private static int GetPropertyBindingSubscriberCount<T>(
            PropertyBinding<T> binding)
        {
            FieldInfo field = typeof(PropertyBinding<T>).GetField(
                "_valueChangedSubscribers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Delegate[] subscribers = field == null
                ? null
                : field.GetValue(binding) as Delegate[];

            return subscribers == null ? 0 : subscribers.Length;
        }

        private static string DescribeLightweightCache(ItemsControl host)
        {
            string result = String.Empty;

            foreach (DictionaryEntry entry in host.LightweightRowCache)
            {
                if (result.Length > 0)
                    result += ",";

                result += entry.Key;
            }

            return result;
        }

        private static void CreateHandleAndDrain(Control root)
        {
            AssertTrue(root != null, "observable dispatch root exists");

            if (!root.IsHandleCreated)
                root.CreateControl();

            if (!root.IsHandleCreated)
            {
                IntPtr handle = root.Handle;
                AssertTrue(
                    handle != IntPtr.Zero,
                    "observable dispatch root handle exists");
            }

            Drain(root);
        }

        private static void Drain(Control root)
        {
            int round;

            for (round = 0; round < 8; round++)
            {
                bool reached = false;

                root.BeginInvoke(
                    (MethodInvoker)delegate
                    {
                        reached = true;
                    });

                int iterations = 0;

                while (!reached && iterations < 1024)
                {
                    Application.DoEvents();
                    iterations++;
                }

                AssertTrue(reached, "observable dispatch sentinel reached");
            }
        }

        private static void AssertTrue(bool condition, string message)
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
                    message + ": expected " + expected + ", got " + actual + ".");
            }
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
                throw new InvalidOperationException(message);
        }
    }
}
