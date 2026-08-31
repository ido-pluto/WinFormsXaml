using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Benchmarks
{
    internal sealed class Program
    {
        private const int NonVirtualItemCount = 250;
        private const int NonVirtualReloadCount = 12;
        private const int VirtualItemCount = 10000;
        private const int RegisteredComponentInstanceCount = 150;
        private const int RegisteredComponentLoadCount = 5;
        private const int EventTargetCount = 600;
        private const int EventLoadCount = 5;
        private const int BindingTargetCount = 400;
        private const int BindingReloadCount = 25;
        private const int PresetTargetCount = 400;
        private const int PresetSelectionCount = 20;

        private delegate void BenchmarkAction();

        private sealed class BenchmarkMeasurement
        {
            public TimeSpan Elapsed;
            public long LiveBytesBefore;
            public long LiveBytesAfter;
        }

        private sealed class ItemRow
        {
            public string Id;
            public int Version;
            public string Text;

            public ItemRow(
                string id,
                int version,
                string text)
            {
                Id = id;
                Version = version;
                Text = text;
            }
        }

        private sealed class ConditionalItemRow : INotifyPropertyChanged
        {
            private bool _show;
            private PropertyChangedEventHandler _propertyChanged;

            public readonly string Id;
            public readonly string Text;

            public ConditionalItemRow(
                string id,
                string text)
            {
                Id = id;
                Text = text;
                _show = true;
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add { _propertyChanged += value; }
                remove { _propertyChanged -= value; }
            }

            public bool Show
            {
                get { return _show; }
                set
                {
                    if (_show == value)
                        return;

                    _show = value;
                    PropertyChangedEventHandler handler =
                        _propertyChanged;

                    if (handler != null)
                    {
                        handler(
                            this,
                            new PropertyChangedEventArgs("Show"));
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
        }

        private sealed class BindingState
        {
            public string Text;
        }

        private sealed class EventBenchmarkState
        {
            public void Action_Click(object sender, EventArgs e)
            {
            }
        }

        [STAThread]
        private static int Main()
        {
            Console.WriteLine("WinFormsXaml focused benchmarks");
            Console.WriteLine(
                "Runtime=" + Environment.Version +
                ", OS=" + Environment.OSVersion +
                ", StopwatchFrequency=" + Stopwatch.Frequency +
                ", HighResolution=" + Stopwatch.IsHighResolution);
            Console.WriteLine(
                "Timings are descriptive, non-gating measurements. " +
                "No form or message loop is started.");
            Console.WriteLine();

            try
            {
                RunNonVirtualItemsBenchmark();
                RunVirtualItemsBenchmark();
                RunReactiveVirtualConditionBenchmark();
                RunRegisteredComponentBenchmark();
                RunEventConstructionBenchmark();
                RunBindingFanOutBenchmark();
                RunPresetFanOutBenchmark();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Benchmark failed:");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static void RunNonVirtualItemsBenchmark()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='320' Height='240' " +
                "ItemKeyPath='Id' ItemVersionPath='Version' " +
                "Virtualizing='false' ProgressiveRendering='false' " +
                "ReevaluateFunctionsOnRefresh='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Border Width='280' Height='28' MinWidth='240' " +
                "        MaxWidth='300' Margin='1' Padding='3' " +
                "        HorizontalAlignment='Stretch' " +
                "        VerticalAlignment='Center' Background='#F8FAFC' " +
                "        BorderBrush='#CBD5E1' BorderThickness='1'>" +
                "      <Label Text='{Binding Text}' MinHeight='18' " +
                "          MaxHeight='22' Foreground='#0F172A' />" +
                "    </Border>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(NonVirtualItemCount);

                Console.WriteLine("[1] Non-virtual keyed ItemsControl");

                BenchmarkMeasurement initial =
                    Measure(
                        delegate
                        {
                            host.SetItems(rows);
                        });

                PrintMeasurement(
                    "initial build",
                    initial,
                    NonVirtualItemCount,
                    "rows");

                if (host.ItemTemplateBlueprintBuildCount !=
                        NonVirtualItemCount ||
                    host.ItemTemplateFallbackBuildCount != 0L)
                {
                    throw new InvalidOperationException(
                        "The realistic non-virtual item template did not stay " +
                        "on the compiled blueprint path.");
                }

                Console.WriteLine(
                    "    blueprint-builds=" +
                    host.ItemTemplateBlueprintBuildCount +
                    ", fallback-builds=" +
                    host.ItemTemplateFallbackBuildCount);

                BenchmarkMeasurement noOp =
                    Measure(
                        delegate
                        {
                            int i;

                            for (i = 0; i < NonVirtualReloadCount; i++)
                                host.ReloadItems();
                        });

                PrintMeasurement(
                    "unchanged keyed reload",
                    noOp,
                    NonVirtualReloadCount,
                    "reloads");

                int patchIndex = 0;
                BenchmarkMeasurement oneItemPatch =
                    Measure(
                        delegate
                        {
                            int i;

                            for (i = 0; i < NonVirtualReloadCount; i++)
                            {
                                ItemRow row =
                                    (ItemRow)rows[patchIndex];

                                row.Version++;
                                row.Text =
                                    "Patched " +
                                    i.ToString(CultureInfo.InvariantCulture);

                                host.ReloadItems();
                                patchIndex =
                                    (patchIndex + 17) % rows.Count;
                            }
                        });

                PrintMeasurement(
                    "one changed version per reload",
                    oneItemPatch,
                    NonVirtualReloadCount,
                    "reloads");

                Console.WriteLine(
                    "    count=" + host.Count +
                    ", realized=" + host.RealizedCount);
                Console.WriteLine();
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void RunVirtualItemsBenchmark()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='320' Height='220' AutoScroll='true' " +
                "ItemKeyPath='Id' ItemVersionPath='Version' " +
                "Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='2' FixedItemSize='20' " +
                "VirtualizationCacheItems='24' ProgressiveRendering='false' " +
                "ReevaluateFunctionsOnRefresh='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Text}' Width='280' Height='20' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            int[] jumpIndexes = new int[]
            {
                0,
                500,
                9999,
                2500,
                7500,
                125,
                9000,
                4200,
                50,
                6000,
                999,
                0
            };

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(VirtualItemCount);

                // A hidden native handle gives ScrollableControl a real viewport.
                // The benchmark never calls Show or Application.Run.
                host.CreateControl();

                Console.WriteLine("[2] Fixed-size virtual ItemsControl");

                BenchmarkMeasurement initial =
                    Measure(
                        delegate
                        {
                            host.SetItems(rows);
                        });

                PrintMeasurement(
                    "initial data model and realization",
                    initial,
                    VirtualItemCount,
                    "rows");

                if (!host.IsVirtualizing)
                {
                    throw new InvalidOperationException(
                        "The virtual benchmark did not activate virtualization.");
                }

                int initialRealized = host.RealizedCount;
                int initialCached = host.VirtualCacheCount;
                int maximumRealized = initialRealized;
                int maximumCached = initialCached;

                BenchmarkMeasurement jumps =
                    Measure(
                        delegate
                        {
                            int i;

                            for (i = 0; i < jumpIndexes.Length; i++)
                            {
                                host.ScrollToIndex(jumpIndexes[i]);
                                maximumRealized = Math.Max(
                                    maximumRealized,
                                    host.RealizedCount);
                                maximumCached = Math.Max(
                                    maximumCached,
                                    host.VirtualCacheCount);
                            }
                        });

                PrintMeasurement(
                    "deterministic ScrollToIndex jumps",
                    jumps,
                    jumpIndexes.Length,
                    "jumps");

                Console.WriteLine(
                    "    count=" + host.Count +
                    ", initial-realized=" + initialRealized +
                    ", initial-cache=" + initialCached +
                    ", final-realized=" + host.RealizedCount +
                    ", max-realized=" + maximumRealized +
                    ", final-cache=" + host.VirtualCacheCount +
                    ", max-cache=" + maximumCached);
                Console.WriteLine();
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void RunReactiveVirtualConditionBenchmark()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='320' Height='220' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='2' FixedItemSize='20' " +
                "VirtualizationCacheItems='24' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Width='280' Height='20'>" +
                "      <Label Condition='{Binding Show}' Text='{Binding Text}' " +
                "             Width='280' Height='20' />" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);
            ArrayList rows = CreateConditionalRows(VirtualItemCount);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.CreateControl();

                Console.WriteLine(
                    "[3] Reactive virtual nested-condition aggregate");

                BenchmarkMeasurement initial =
                    Measure(
                        delegate
                        {
                            host.SetItems(rows);
                        });

                PrintMeasurement(
                    "initial condition graph and realization",
                    initial,
                    VirtualItemCount,
                    "rows");

                if (!host.IsVirtualizing)
                {
                    throw new InvalidOperationException(
                        "The reactive condition benchmark did not activate virtualization.");
                }

                AssertConditionalRowSubscriberCount(rows, 0, 1);
                AssertConditionalRowSubscriberCount(
                    rows,
                    rows.Count / 2,
                    1);
                AssertConditionalRowSubscriberCount(
                    rows,
                    rows.Count - 1,
                    1);

                ArrayList rotatedRows =
                    new ArrayList(rows.Count);
                int rowIndex;

                for (rowIndex = 1;
                     rowIndex < rows.Count;
                     rowIndex++)
                {
                    rotatedRows.Add(rows[rowIndex]);
                }

                rotatedRows.Add(rows[0]);
                BenchmarkMeasurement rotation =
                    Measure(
                        delegate
                        {
                            host.SetItems(rotatedRows);
                        });

                PrintMeasurement(
                    "source-indexed condition graph rotation",
                    rotation,
                    VirtualItemCount,
                    "rows");
                AssertConditionalRowSubscriberCount(rows, 0, 1);
                AssertConditionalRowSubscriberCount(
                    rows,
                    rows.Count / 2,
                    1);
                AssertConditionalRowSubscriberCount(
                    rows,
                    rows.Count - 1,
                    1);

                int[] changeIndexes = new int[]
                {
                    rows.Count / 2,
                    rows.Count - 1,
                    rows.Count / 3
                };
                int changeIndex = 0;
                BenchmarkMeasurement changes =
                    Measure(
                        delegate
                        {
                            for (changeIndex = 0;
                                 changeIndex < changeIndexes.Length;
                                 changeIndex++)
                            {
                                ConditionalItemRow row =
                                    (ConditionalItemRow)
                                        rows[changeIndexes[changeIndex]];
                                row.Show = false;
                                DrainReactiveCallbacks(
                                    runtime.RootControl);
                            }
                        });

                PrintMeasurement(
                    "off-screen notifying condition changes",
                    changes,
                    changeIndexes.Length,
                    "changes");
                Console.WriteLine(
                    "    count=" + host.Count +
                    ", realized=" + host.RealizedCount +
                    ", cache=" + host.VirtualCacheCount);
                Console.WriteLine();
            }
            finally
            {
                DisposeRuntime(runtime);
            }

            AssertConditionalRowSubscriberCount(rows, 0, 0);
            AssertConditionalRowSubscriberCount(
                rows,
                rows.Count / 2,
                0);
            AssertConditionalRowSubscriberCount(
                rows,
                rows.Count - 1,
                0);
        }

        private static void RunRegisteredComponentBenchmark()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Benchmarks.Fixtures.BenchmarkCard.xml");

            string markup = CreateRegisteredComponentMarkup(
                RegisteredComponentInstanceCount);
            XamlRuntime warmup = XamlRuntime.Load(markup);

            try
            {
                if (warmup.RootControl.Controls.Count !=
                    RegisteredComponentInstanceCount)
                {
                    throw new InvalidOperationException(
                        "The component benchmark warm-up built an unexpected " +
                        "number of component roots.");
                }
            }
            finally
            {
                DisposeRuntime(warmup);
            }

            Console.WriteLine("[4] Repeated registered XML components");

            BenchmarkMeasurement loads =
                Measure(
                    delegate
                    {
                        int i;

                        for (i = 0;
                             i < RegisteredComponentLoadCount;
                             i++)
                        {
                            XamlRuntime runtime = XamlRuntime.Load(markup);

                            try
                            {
                                if (runtime.RootControl.Controls.Count !=
                                    RegisteredComponentInstanceCount)
                                {
                                    throw new InvalidOperationException(
                                        "The component benchmark built an " +
                                        "unexpected number of component roots.");
                                }
                            }
                            finally
                            {
                                DisposeRuntime(runtime);
                            }
                        }
                    });

            int totalInstances =
                RegisteredComponentInstanceCount *
                RegisteredComponentLoadCount;

            PrintMeasurement(
                "fresh-runtime component construction",
                loads,
                totalInstances,
                "component instances");
            Console.WriteLine(
                "    runtimes=" + RegisteredComponentLoadCount +
                ", instances-per-runtime=" +
                RegisteredComponentInstanceCount);
            Console.WriteLine();
        }

        private static void RunEventConstructionBenchmark()
        {
            EventBenchmarkState state = new EventBenchmarkState();
            string markup = CreateEventFanOutMarkup(EventTargetCount);
            XamlRuntime warmup = XamlRuntime.Load(markup, state);

            try
            {
                if (warmup.RootControl.Controls.Count != EventTargetCount)
                {
                    throw new InvalidOperationException(
                        "The event benchmark warm-up built an unexpected " +
                        "number of controls.");
                }
            }
            finally
            {
                DisposeRuntime(warmup);
            }

            Console.WriteLine("[5] Event-heavy XAML construction");

            BenchmarkMeasurement loads =
                Measure(
                    delegate
                    {
                        int i;

                        for (i = 0; i < EventLoadCount; i++)
                        {
                            XamlRuntime runtime =
                                XamlRuntime.Load(markup, state);

                            try
                            {
                                if (runtime.RootControl.Controls.Count !=
                                    EventTargetCount)
                                {
                                    throw new InvalidOperationException(
                                        "The event benchmark built an " +
                                        "unexpected number of controls.");
                                }
                            }
                            finally
                            {
                                DisposeRuntime(runtime);
                            }
                        }
                    });

            int registrations = EventTargetCount * EventLoadCount;

            PrintMeasurement(
                "fresh-runtime event registration and cleanup",
                loads,
                registrations,
                "event registrations");
            Console.WriteLine(
                "    runtimes=" + EventLoadCount +
                ", event-targets-per-runtime=" + EventTargetCount);
            Console.WriteLine();
        }

        private static void RunBindingFanOutBenchmark()
        {
            BindingState state = new BindingState();
            state.Text = "Initial";

            string markup =
                CreateFanOutMarkup(
                    BindingTargetCount,
                    "{Binding Text}",
                    null);

            XamlRuntime runtime =
                XamlRuntime.Load(markup, state);

            try
            {
                Console.WriteLine("[6] Ordinary binding fan-out");

                // Match the preset scenario's hidden-handle state so the two fan-out
                // measurements exercise comparable WinForms invalidation behavior.
                runtime.RootControl.CreateControl();
                state.Text = "Warm-up";
                runtime.ReloadBindings();

                BenchmarkMeasurement reloads =
                    Measure(
                        delegate
                        {
                            int i;

                            for (i = 0; i < BindingReloadCount; i++)
                            {
                                state.Text =
                                    "Binding pass " +
                                    i.ToString(CultureInfo.InvariantCulture);
                                runtime.ReloadBindings();
                            }
                        });

                PrintMeasurement(
                    "ReloadBindings fan-out",
                    reloads,
                    BindingReloadCount,
                    "reloads");

                Console.WriteLine(
                    "    labels=" + BindingTargetCount +
                    ", property-applications=" +
                    (BindingTargetCount * BindingReloadCount));
                Console.WriteLine();
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void RunPresetFanOutBenchmark()
        {
            const string presetMarkup =
                "<Presets Name='Theme' Selected='Light'>" +
                "  <Preset Name='Light'>" +
                "    <Set Key='Caption' Value='Light caption' />" +
                "  </Preset>" +
                "  <Preset Name='Dark'>" +
                "    <Set Key='Caption' Value='Dark caption' />" +
                "  </Preset>" +
                "</Presets>";

            string markup =
                CreateFanOutMarkup(
                    PresetTargetCount,
                    "{Preset Theme.Caption}",
                    presetMarkup);

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                // Preset changes are delivered immediately on the owning thread once
                // the hidden root handle exists. No message loop or visible Form is used.
                runtime.RootControl.CreateControl();
                runtime.Presets.Select("Theme", "Dark");

                Console.WriteLine("[7] Preset selection fan-out");

                BenchmarkMeasurement selections =
                    Measure(
                        delegate
                        {
                            int i;

                            for (i = 0; i < PresetSelectionCount; i++)
                            {
                                runtime.Presets.Select(
                                    "Theme",
                                    (i & 1) == 0 ? "Light" : "Dark");
                            }
                        });

                PrintMeasurement(
                    "theme selection fan-out",
                    selections,
                    PresetSelectionCount,
                    "selections");

                Console.WriteLine(
                    "    labels=" + PresetTargetCount +
                    ", property-applications=" +
                    (PresetTargetCount * PresetSelectionCount));
                Console.WriteLine();
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static ArrayList CreateRows(int count)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
            {
                rows.Add(
                    new ItemRow(
                        "row-" + i.ToString(CultureInfo.InvariantCulture),
                        1,
                        "Row " + i.ToString(CultureInfo.InvariantCulture)));
            }

            return rows;
        }

        private static ArrayList CreateConditionalRows(int count)
        {
            ArrayList rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
            {
                string index = i.ToString(
                    CultureInfo.InvariantCulture);
                rows.Add(
                    new ConditionalItemRow(
                        "conditional-" + index,
                        "Conditional " + index));
            }

            return rows;
        }

        private static void AssertConditionalRowSubscriberCount(
            ArrayList rows,
            int index,
            int expected)
        {
            ConditionalItemRow row =
                rows == null || index < 0 || index >= rows.Count
                    ? null
                    : rows[index] as ConditionalItemRow;

            if (row == null || row.SubscriberCount != expected)
            {
                throw new InvalidOperationException(
                    "Conditional row subscription count mismatch at index " +
                    index.ToString(CultureInfo.InvariantCulture) +
                    ". Expected " +
                    expected.ToString(CultureInfo.InvariantCulture) +
                    ", actual " +
                    (row == null
                        ? "<missing>"
                        : row.SubscriberCount.ToString(
                            CultureInfo.InvariantCulture)) +
                    ".");
            }
        }

        private static void DrainReactiveCallbacks(Control root)
        {
            if (root == null || root.IsDisposed)
            {
                throw new InvalidOperationException(
                    "The reactive benchmark root is unavailable.");
            }

            int round;

            for (round = 0; round < 8; round++)
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

                if (!reached)
                {
                    throw new InvalidOperationException(
                        "The reactive benchmark callback queue did not drain.");
                }
            }
        }

        private static string CreateFanOutMarkup(
            int labelCount,
            string expression,
            string resources)
        {
            StringBuilder markup =
                new StringBuilder(labelCount * 48);

            markup.Append("<Panel Width='640' Height='480'>");

            if (!String.IsNullOrEmpty(resources))
                markup.Append(resources);

            int i;

            for (i = 0; i < labelCount; i++)
            {
                markup.Append("<Label Text='");
                markup.Append(expression);
                markup.Append("' />");
            }

            markup.Append("</Panel>");
            return markup.ToString();
        }

        private static string CreateEventFanOutMarkup(int targetCount)
        {
            StringBuilder markup =
                new StringBuilder(targetCount * 48);
            markup.Append("<Panel Width='640' Height='480'>");

            int i;

            for (i = 0; i < targetCount; i++)
                markup.Append("<Button Click='Action_Click' />");

            markup.Append("</Panel>");
            return markup.ToString();
        }

        private static string CreateRegisteredComponentMarkup(
            int componentCount)
        {
            StringBuilder markup =
                new StringBuilder(componentCount * 72);
            markup.Append("<Panel Width='640' Height='480'>");

            int i;

            for (i = 0; i < componentCount; i++)
            {
                markup.Append("<BenchmarkCard Caption='Card ");
                markup.Append(i.ToString(CultureInfo.InvariantCulture));
                markup.Append("' />");
            }

            markup.Append("</Panel>");
            return markup.ToString();
        }

        private static BenchmarkMeasurement Measure(
            BenchmarkAction action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            BenchmarkMeasurement result =
                new BenchmarkMeasurement();

            result.LiveBytesBefore = GC.GetTotalMemory(true);

            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();

            result.Elapsed = stopwatch.Elapsed;
            result.LiveBytesAfter = GC.GetTotalMemory(true);
            return result;
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

        private static void PrintMeasurement(
            string name,
            BenchmarkMeasurement measurement,
            int operations,
            string operationName)
        {
            double milliseconds = measurement.Elapsed.TotalMilliseconds;
            double millisecondsPerOperation =
                operations == 0
                    ? 0.0
                    : milliseconds / operations;
            long liveByteDelta =
                measurement.LiveBytesAfter -
                measurement.LiveBytesBefore;

            Console.WriteLine("  " + name + ":");
            Console.WriteLine(
                "    " + operations + " " + operationName +
                ", total=" + FormatMilliseconds(milliseconds) + " ms" +
                ", per-op=" +
                FormatMilliseconds(millisecondsPerOperation) + " ms" +
                ", live-memory-delta=" + FormatKilobytes(liveByteDelta) + " KiB");
        }

        private static string FormatMilliseconds(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string FormatKilobytes(long bytes)
        {
            return
                (bytes / 1024.0).ToString(
                    "+0.0;-0.0;0.0",
                    CultureInfo.InvariantCulture);
        }
    }
}
