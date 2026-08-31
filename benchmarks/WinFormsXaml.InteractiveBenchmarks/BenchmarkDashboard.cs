using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.InteractiveBenchmarks
{
    internal sealed class BenchmarkDashboard : Form
    {
        private const int CalibrationTickCount = 12;
        private const int ScrollOperationCount = 120;
        private const int LifecycleIterationCount = 10;

        private readonly Button _startButton;
        private readonly Label _statusLabel;
        private readonly TextBox _output;
        private readonly Font _outputFont;
        private readonly Timer _heartbeatTimer;
        private readonly BenchmarkState _state;
        private readonly bool _autoStart;
        private readonly BenchmarkProfile _profile;
        private readonly int _rowCount;
        private readonly BenchmarkScrollWorkload _scrollWorkload;

        private BenchmarkSession _session;
        private Stopwatch _heartbeatWatch;
        private SampleSeries _calibrationIntervals;
        private SampleSeries _heartbeatDelays;
        private SampleSeries _scrollDurations;
        private SampleSeries _lifecycleOpenDurations;
        private SampleSeries _lifecycleCloseDurations;
        private ResourceSnapshot _runResourcesBefore;
        private ResourceSnapshot _scrollResourcesBefore;
        private ResourceSnapshot _lifecycleResourcesBefore;
        private long _lastHeartbeatMilliseconds;
        private long _expectedHeartbeatMilliseconds;
        private long _virtualCreatedBefore;
        private long _virtualRetainedBefore;
        private long _virtualCacheBefore;
        private long _virtualCrossItemBefore;
        private long _virtualCrossItemRejectedBefore;
        private long _itemTemplateBlueprintBefore;
        private long _itemTemplateFallbackBefore;
        private long _itemControlDisposedBefore;
        private long _progressiveBatchBefore;
        private long _scrollEventCountBefore;
        private long _mouseWheelEventCountBefore;
        private int _activeItemSubscriptionsBefore;
        private int _calibrationTicks;
        private int _scrollOperation;
        private int _smoothSettleTicksRemaining;
        private int _lifecycleIteration;
        private bool _scrollSessionVirtualizing;
        private bool _running;
        private bool _closing;
        private int _exitCode;

        public BenchmarkDashboard(
            bool autoStart,
            BenchmarkProfile profile,
            bool smoothScroll,
            bool styledScrollBar)
        {
            _autoStart = autoStart;
            _profile = profile;
            _rowCount = BenchmarkProfiles.GetRowCount(profile);
            _scrollWorkload = new BenchmarkScrollWorkload(
                profile,
                _rowCount,
                smoothScroll,
                styledScrollBar);
            _state = new BenchmarkState(
                _rowCount,
                profile);
            _startButton = new Button();
            _statusLabel = new Label();
            _output = new TextBox();
            _outputFont = new Font("Courier New", 9.0F);
            _heartbeatTimer = new Timer();

            Text = "WinFormsXaml interactive performance harness";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 560);
            MinimumSize = new Size(640, 420);

            _startButton.Text = "Run interactive measurements";
            _startButton.Dock = DockStyle.Top;
            _startButton.Height = 36;
            _startButton.Click += new EventHandler(OnStartClick);

            _statusLabel.Text = "Ready. No benchmark runs automatically.";
            _statusLabel.Dock = DockStyle.Top;
            _statusLabel.Height = 28;
            _statusLabel.Padding = new Padding(8, 6, 8, 4);

            _output.Multiline = true;
            _output.ReadOnly = true;
            _output.ScrollBars = ScrollBars.Both;
            _output.WordWrap = false;
            _output.Dock = DockStyle.Fill;
            _output.Font = _outputFont;

            Controls.Add(_output);
            Controls.Add(_statusLabel);
            Controls.Add(_startButton);

            _heartbeatTimer.Interval = 16;
            _heartbeatTimer.Tick += new EventHandler(OnHeartbeatTick);

            Shown += new EventHandler(OnDashboardShown);
        }

        public int ExitCode
        {
            get { return _exitCode; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _closing = true;
                _heartbeatTimer.Stop();
                _heartbeatTimer.Tick -= new EventHandler(OnHeartbeatTick);
                _heartbeatTimer.Dispose();

                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;
                }

                _state.Dispose();
                _outputFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnDashboardShown(object sender, EventArgs e)
        {
            Shown -= new EventHandler(OnDashboardShown);

            if (_autoStart)
                BeginInvoke(new MethodInvoker(StartRun));
        }

        private void OnStartClick(object sender, EventArgs e)
        {
            StartRun();
        }

        private void StartRun()
        {
            if (_running || _closing)
                return;

            _running = true;
            _exitCode = 0;
            _startButton.Enabled = false;
            _output.Clear();
            _runResourcesBefore = ResourceSnapshot.Capture();

            Append("WinFormsXaml interactive performance harness");
            Append(
                "Runtime=" + Environment.Version.ToString() +
                ", OS=" + Environment.OSVersion.ToString());
            Append(
                "Process=" + (IntPtr.Size * 8).ToString() +
                "-bit, logical processors=" +
                Environment.ProcessorCount.ToString() +
                ", visual styles=" +
                Application.RenderWithVisualStyles.ToString());
            Append(
                "WinFormsXaml=" +
                GetAssemblyIdentity(typeof(XamlRuntime).Assembly));
            Append(
                "Harness=" +
                GetAssemblyIdentity(GetType().Assembly));
            Append(
                "Rows=" + _rowCount.ToString() +
                ", timer interval=" +
                _heartbeatTimer.Interval.ToString() + " ms");
            Append(
                "Profile=" +
                BenchmarkProfiles.GetDisplayName(_profile));
            Append(
                "Scroll workload=" +
                _scrollWorkload.Description);
            Append("No GC.Collect is performed. All Forms use a real message loop.");
            Append(String.Empty);

            SetStatus("Measuring cold first presented frame...");
            OpenSession(new FirstFrameHandler(OnColdFirstFrame));
        }

        private void OnColdFirstFrame(
            BenchmarkSession session,
            long elapsedMilliseconds)
        {
            try
            {
                if (_closing)
                    return;

                Append(
                    "Cold time to first presented frame: " +
                    elapsedMilliseconds.ToString() + " ms");
                CloseCurrentSession();
                BeginInvoke(new MethodInvoker(StartWarmFirstFrame));
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void StartWarmFirstFrame()
        {
            try
            {
                if (_closing)
                    return;

                SetStatus("Measuring warm first presented frame...");
                OpenSession(new FirstFrameHandler(OnWarmFirstFrame));
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void OnWarmFirstFrame(
            BenchmarkSession session,
            long elapsedMilliseconds)
        {
            try
            {
                if (_closing)
                    return;

                Append(
                    "Warm time to first presented frame: " +
                    elapsedMilliseconds.ToString() + " ms");
                CloseCurrentSession();
                BeginInvoke(new MethodInvoker(StartScrollScenario));
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void StartScrollScenario()
        {
            try
            {
                if (_closing)
                    return;

                SetStatus("Opening the scrolling scenario...");
                OpenSession(new FirstFrameHandler(OnScrollSessionReady));
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void OnScrollSessionReady(
            BenchmarkSession session,
            long elapsedMilliseconds)
        {
            try
            {
                if (_closing)
                    return;

                _calibrationIntervals = new SampleSeries();
                _heartbeatDelays = new SampleSeries();
                _scrollDurations = new SampleSeries();
                _calibrationTicks = 0;
                _scrollOperation = 0;
                _smoothSettleTicksRemaining = 0;
                _expectedHeartbeatMilliseconds = 0;
                _scrollResourcesBefore = ResourceSnapshot.Capture();
                _scrollSessionVirtualizing =
                    session.Items.IsVirtualizing;

                if (_scrollSessionVirtualizing)
                {
                    _virtualCreatedBefore =
                        session.Items.VirtualCreatedCount;
                    _virtualRetainedBefore =
                        session.Items.VirtualRetainedReuseCount;
                    _virtualCacheBefore =
                        session.Items.VirtualCacheReuseCount;
                    _virtualCrossItemBefore =
                        session.Items.VirtualCrossItemRecycleCount;
                    _virtualCrossItemRejectedBefore =
                        session.Items.VirtualCrossItemRecycleRejectedCount;
                }

                _itemTemplateBlueprintBefore =
                    session.Items.ItemTemplateBlueprintBuildCount;
                _itemTemplateFallbackBefore =
                    session.Items.ItemTemplateFallbackBuildCount;
                _itemControlDisposedBefore =
                    session.Items.ItemControlTreeDisposedCount;
                _progressiveBatchBefore =
                    session.Items.ProgressiveBatchCount;
                _scrollEventCountBefore =
                    session.ScrollEventCount;
                _mouseWheelEventCountBefore =
                    session.MouseWheelEventCount;
                _activeItemSubscriptionsBefore =
                    session.Items.ActiveItemBindingSubscriptionCount;
                Append(
                    "Renderer before scrolling: virtualizing=" +
                    _scrollSessionVirtualizing.ToString() +
                    ", logical items=" +
                    session.Items.Count.ToString() +
                    ", rendered item trees=" +
                    session.Items.RealizedCount.ToString());
                Append(
                    "Item counters before scrolling: blueprint=" +
                    _itemTemplateBlueprintBefore.ToString() +
                    ", complete-renderer=" +
                    _itemTemplateFallbackBefore.ToString() +
                    ", disposed=" +
                    _itemControlDisposedBefore.ToString() +
                    ", active bindings=" +
                    _activeItemSubscriptionsBefore.ToString());
                _heartbeatWatch = Stopwatch.StartNew();
                _lastHeartbeatMilliseconds = 0;
                SetStatus("Calibrating the legacy-safe UI heartbeat...");
                _heartbeatTimer.Start();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void OnHeartbeatTick(object sender, EventArgs e)
        {
            if (!_running || _session == null || _closing)
                return;

            try
            {
                long now = _heartbeatWatch.ElapsedMilliseconds;

                if (_lastHeartbeatMilliseconds != 0)
                {
                    long interval = now - _lastHeartbeatMilliseconds;

                    if (_calibrationTicks < CalibrationTickCount)
                    {
                        _calibrationIntervals.Add(interval);
                    }
                    else
                    {
                        _heartbeatDelays.Add(
                            Math.Max(
                                0L,
                                interval -
                                _expectedHeartbeatMilliseconds));
                    }
                }

                _lastHeartbeatMilliseconds = now;

                if (_calibrationTicks < CalibrationTickCount)
                {
                    _calibrationTicks++;

                    if (_calibrationTicks == CalibrationTickCount)
                    {
                        _expectedHeartbeatMilliseconds = Math.Max(
                            1L,
                            _calibrationIntervals.Median);
                        SetStatus(
                            "Running real message-loop scroll input...");
                    }

                    return;
                }

                if (_scrollOperation >= ScrollOperationCount)
                {
                    if (_smoothSettleTicksRemaining > 0)
                    {
                        _smoothSettleTicksRemaining--;

                        if (_smoothSettleTicksRemaining == 0)
                            CompleteScrollScenario();
                    }

                    return;
                }

                Stopwatch action = Stopwatch.StartNew();
                _scrollWorkload.Apply(
                    _session.Items,
                    _scrollOperation);
                _session.Form.Update();
                _scrollDurations.Add(action.ElapsedMilliseconds);
                _scrollOperation++;

                if (_scrollOperation >= ScrollOperationCount)
                {
                    if (_scrollWorkload.UsesSmoothScroll)
                    {
                        long settleDuration =
                            (long)_scrollWorkload.SmoothSettleDuration +
                            (2L * _expectedHeartbeatMilliseconds);
                        _smoothSettleTicksRemaining = (int)Math.Max(
                            2L,
                            (settleDuration +
                             _expectedHeartbeatMilliseconds - 1L) /
                            _expectedHeartbeatMilliseconds);
                        SetStatus(
                            "Waiting for the final smooth-scroll " +
                            "transition to settle...");
                    }
                    else
                    {
                        CompleteScrollScenario();
                    }
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void CompleteScrollScenario()
        {
            _heartbeatTimer.Stop();
            ResourceSnapshot after = ResourceSnapshot.Capture();
            WinFormsXaml.XamlRuntime.ItemsControl items = _session.Items;

            Append(String.Empty);
            Append(
                "Calibrated heartbeat interval: " +
                _expectedHeartbeatMilliseconds.ToString() + " ms");
            AppendSeries("Heartbeat delay", _heartbeatDelays);
            AppendSeries("Scroll command dispatch", _scrollDurations);
            Append(
                "Native Scroll notifications during workload: " +
                (_session.ScrollEventCount -
                 _scrollEventCountBefore).ToString());
            Append(
                "Native MouseWheel messages observed during workload: " +
                (_session.MouseWheelEventCount -
                 _mouseWheelEventCountBefore).ToString());

            if (_scrollWorkload.UsesSmoothScroll)
            {
                Append(
                    "Heartbeat samples include the complete final " +
                    "smooth-scroll settling interval.");
            }

            if (_scrollSessionVirtualizing)
            {
                Append(
                    "Virtual counters delta: created=" +
                    (items.VirtualCreatedCount -
                     _virtualCreatedBefore).ToString() +
                    ", retained=" +
                    (items.VirtualRetainedReuseCount -
                     _virtualRetainedBefore).ToString() +
                    ", detached-cache=" +
                    (items.VirtualCacheReuseCount -
                     _virtualCacheBefore).ToString() +
                    ", cross-item=" +
                    (items.VirtualCrossItemRecycleCount -
                     _virtualCrossItemBefore).ToString() +
                    ", recycle-rejected=" +
                    (items.VirtualCrossItemRecycleRejectedCount -
                     _virtualCrossItemRejectedBefore).ToString());
            }
            else
            {
                Append(
                    "Non-virtual renderer after scrolling: logical items=" +
                    items.Count.ToString() +
                    ", retained native item trees=" +
                    items.RealizedCount.ToString() +
                    ", virtual diagnostics=not applicable");
            }

            Append(
                "Item-template construction during scrolling: blueprint=" +
                (items.ItemTemplateBlueprintBuildCount -
                 _itemTemplateBlueprintBefore).ToString() +
                ", complete-renderer=" +
                (items.ItemTemplateFallbackBuildCount -
                 _itemTemplateFallbackBefore).ToString() +
                " (lifetime " +
                items.ItemTemplateBlueprintBuildCount.ToString() +
                "/" +
                items.ItemTemplateFallbackBuildCount.ToString() + ")");
            Append(
                "Item control trees disposed during scrolling: " +
                (items.ItemControlTreeDisposedCount -
                 _itemControlDisposedBefore).ToString());
            Append(
                "Progressive render batches during scrolling: " +
                (items.ProgressiveBatchCount -
                 _progressiveBatchBefore).ToString());
            Append(
                "Active item binding subscriptions: before=" +
                _activeItemSubscriptionsBefore.ToString() +
                ", after=" +
                items.ActiveItemBindingSubscriptionCount.ToString() +
                ", delta=" +
                (items.ActiveItemBindingSubscriptionCount -
                 _activeItemSubscriptionsBefore).ToString());

            if (_scrollSessionVirtualizing)
            {
                Append(
                    "Realized range=" +
                    items.VirtualRealizedStartIndex.ToString() +
                    ".." +
                    items.VirtualRealizedEndIndex.ToString() +
                    ", realized=" + items.RealizedCount.ToString() +
                    ", cached=" + items.VirtualCacheCount.ToString());
            }

            Append(
                "Scroll resources: " +
                ResourceSnapshot.FormatDelta(
                    _scrollResourcesBefore,
                    after));

            CloseCurrentSession();
            BeginInvoke(new MethodInvoker(StartLifecycleScenario));
        }

        private void StartLifecycleScenario()
        {
            if (_closing)
                return;

            _lifecycleIteration = 0;
            _lifecycleOpenDurations = new SampleSeries();
            _lifecycleCloseDurations = new SampleSeries();
            _lifecycleResourcesBefore = ResourceSnapshot.Capture();
            SetStatus("Running repeated real Form open/close cycles...");
            StartNextLifecycleIteration();
        }

        private void StartNextLifecycleIteration()
        {
            if (_closing)
                return;

            if (_lifecycleIteration >= LifecycleIterationCount)
            {
                CompleteRun();
                return;
            }

            try
            {
                OpenSession(new FirstFrameHandler(OnLifecycleFirstFrame));
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void OnLifecycleFirstFrame(
            BenchmarkSession session,
            long elapsedMilliseconds)
        {
            try
            {
                if (_closing)
                    return;

                _lifecycleOpenDurations.Add(elapsedMilliseconds);
                BeginInvoke(new MethodInvoker(CloseLifecycleIteration));
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void CloseLifecycleIteration()
        {
            try
            {
                if (_closing)
                    return;

                Stopwatch closeWatch = Stopwatch.StartNew();
                CloseCurrentSession();
                _lifecycleCloseDurations.Add(
                    closeWatch.ElapsedMilliseconds);
                _lifecycleIteration++;
                BeginInvoke(new MethodInvoker(StartNextLifecycleIteration));
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void CompleteRun()
        {
            if (_closing)
                return;

            ResourceSnapshot runAfter = ResourceSnapshot.Capture();

            Append(String.Empty);
            AppendSeries("Repeated open first frame", _lifecycleOpenDurations);
            AppendSeries("Repeated close/dispose", _lifecycleCloseDurations);
            Append(
                "Lifecycle resources: " +
                ResourceSnapshot.FormatDelta(
                    _lifecycleResourcesBefore,
                    runAfter));
            Append(
                "Complete run resources: " +
                ResourceSnapshot.FormatDelta(
                    _runResourcesBefore,
                    runAfter));
            Append(String.Empty);
            Append("Interactive measurement complete.");
            SetStatus("Complete. Results are descriptive, not a pass/fail gate.");
            _running = false;
            _startButton.Enabled = true;
        }

        private void OpenSession(FirstFrameHandler handler)
        {
            if (_session != null)
                throw new InvalidOperationException("A benchmark Form is already open.");

            _session = new BenchmarkSession(
                _state,
                _profile,
                _scrollWorkload);
            _session.Open(this, handler);
        }

        private void CloseCurrentSession()
        {
            BenchmarkSession session = _session;
            _session = null;

            if (session != null)
                session.Dispose();
        }

        private void AppendSeries(string name, SampleSeries samples)
        {
            Append(
                name + ": median=" + samples.Median.ToString() +
                " ms, p95=" + samples.Percentile95.ToString() +
                " ms, max=" + samples.Maximum.ToString() +
                " ms, samples=" + samples.Count.ToString());
        }

        private void SetStatus(string value)
        {
            _statusLabel.Text = value;
        }

        private void Append(string value)
        {
            _output.AppendText(value + Environment.NewLine);
            Console.WriteLine(value);
        }

        private static string GetAssemblyIdentity(Assembly assembly)
        {
            if (assembly == null)
                return "unavailable";

            string identity = assembly.GetName().FullName;

            try
            {
                if (!String.IsNullOrEmpty(assembly.Location))
                    identity += ", location=" + assembly.Location;
            }
            catch (Exception)
            {
                // Assembly location is optional for some hosted runtimes.
            }

            return identity;
        }

        private void Fail(Exception error)
        {
            _heartbeatTimer.Stop();
            _exitCode = 1;
            _running = false;
            _startButton.Enabled = true;
            SetStatus("Measurement failed. See the complete exception below.");
            Append(String.Empty);
            Append(error.ToString());

            try
            {
                CloseCurrentSession();
            }
            catch (Exception cleanupError)
            {
                Append("Cleanup also failed: " + cleanupError.ToString());
            }
        }
    }
}
