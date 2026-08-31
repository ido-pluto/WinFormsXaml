using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.InteractiveBenchmarks
{
    internal enum BenchmarkProfile
    {
        Controls,
        Lightweight,
        Recycling,
        NonVirtual
    }

    internal static class BenchmarkProfiles
    {
        private const int VirtualizedRowCount = 10000;
        private const int NonVirtualRowCount = 192;

        public static string GetDisplayName(BenchmarkProfile profile)
        {
            if (profile == BenchmarkProfile.Lightweight)
                return "Lightweight owner-drawn rows";
            if (profile == BenchmarkProfile.Recycling)
                return "Controls with explicit cross-item recycling";
            if (profile == BenchmarkProfile.NonVirtual)
                return "Fully materialized native Controls";

            return "Controls with compiled construction";
        }

        public static int GetRowCount(BenchmarkProfile profile)
        {
            return profile == BenchmarkProfile.NonVirtual
                ? NonVirtualRowCount
                : VirtualizedRowCount;
        }

        public static string GetResourceName(BenchmarkProfile profile)
        {
            const string prefix =
                "WinFormsXaml.InteractiveBenchmarks.Fixtures.";

            if (profile == BenchmarkProfile.Lightweight)
                return prefix + "InteractiveLightweightForm.xml";
            if (profile == BenchmarkProfile.Recycling)
                return prefix + "InteractiveRecyclingForm.xml";
            if (profile == BenchmarkProfile.NonVirtual)
                return prefix + "InteractiveNonVirtualForm.xml";

            return prefix + "InteractiveBenchmarkForm.xml";
        }
    }

    /// <summary>
    /// Participates in the benchmark's explicit native-row reset contract.
    /// </summary>
    public sealed class BenchmarkRecyclableRow : Panel,
        IRecyclableItemControl
    {
        /// <summary>Resets transient state before a different item is applied.</summary>
        public bool TryPrepareForRecycle(ItemRecycleContext context)
        {
            Cursor = Cursors.Default;
            Tag = null;
            return true;
        }
    }

    internal class BenchmarkRow
    {
        public readonly int Id;
        public readonly int Version;
        public readonly string Title;
        public readonly string Detail;
        public readonly PropertyBinding<bool> Selected;
        public readonly int Progress;
        public readonly string Url;

        public BenchmarkRow(int id)
        {
            Id = id;
            Version = 1;
            Title = "Interactive row " + id.ToString();
            Detail =
                "A nested panel/label/check/progress template used " +
                "for real message-loop scrolling.";
            Selected = new PropertyBinding<bool>(id % 7 == 0);
            Progress = id % 101;
            Url = "https://github.com/";
        }
    }

    internal sealed class NonVirtualBenchmarkRow : BenchmarkRow
    {
        public readonly Image Thumbnail;

        public NonVirtualBenchmarkRow(
            int id,
            Image thumbnail)
            : base(id)
        {
            Thumbnail = thumbnail;
        }
    }

    internal sealed class BenchmarkState : IDisposable
    {
        public readonly ArrayList Rows;
        private readonly Image _thumbnail;
        private bool _disposed;

        public BenchmarkState(
            int count,
            BenchmarkProfile profile)
        {
            if (profile == BenchmarkProfile.NonVirtual)
                _thumbnail = DeterministicBenchmarkImage.Create();

            Rows = new ArrayList(count);
            int i;

            for (i = 0; i < count; i++)
            {
                if (profile == BenchmarkProfile.NonVirtual)
                    Rows.Add(new NonVirtualBenchmarkRow(i, _thumbnail));
                else
                    Rows.Add(new BenchmarkRow(i));
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_thumbnail != null)
                _thumbnail.Dispose();
        }
    }

    internal delegate void FirstFrameHandler(
        BenchmarkSession session,
        long elapsedMilliseconds);

    internal sealed class BenchmarkSession : IDisposable
    {
        private readonly BenchmarkState _state;
        private readonly BenchmarkProfile _profile;
        private readonly BenchmarkScrollWorkload _scrollWorkload;
        private XamlRuntime _runtime;
        private Form _form;
        private XamlRuntime.ItemsControl _items;
        private Stopwatch _startupWatch;
        private FirstFrameHandler _firstFrameHandler;
        private bool _openStarted;
        private bool _disposed;
        private long _scrollEventCount;
        private long _mouseWheelEventCount;

        public BenchmarkSession(
            BenchmarkState state,
            BenchmarkProfile profile,
            BenchmarkScrollWorkload scrollWorkload)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (scrollWorkload == null)
                throw new ArgumentNullException("scrollWorkload");

            _state = state;
            _profile = profile;
            _scrollWorkload = scrollWorkload;
        }

        public Form Form
        {
            get { return _form; }
        }

        public XamlRuntime.ItemsControl Items
        {
            get { return _items; }
        }

        public long ScrollEventCount
        {
            get { return _scrollEventCount; }
        }

        public long MouseWheelEventCount
        {
            get { return _mouseWheelEventCount; }
        }

        public void Open(
            Form owner,
            FirstFrameHandler firstFrameHandler)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
            if (_openStarted)
                throw new InvalidOperationException("The session is already open.");

            _openStarted = true;
            _firstFrameHandler = firstFrameHandler;
            _startupWatch = Stopwatch.StartNew();
            _runtime = XamlRuntime.LoadEmbedded(
                Assembly.GetExecutingAssembly(),
                BenchmarkProfiles.GetResourceName(_profile),
                _state);
            _form = _runtime.Form;
            _items = _runtime.GetItemsControl("BenchmarkRows");
            _scrollWorkload.Configure(_items);
            _items.Scroll += new ScrollEventHandler(OnItemsScroll);
            _items.MouseWheel += new MouseEventHandler(OnItemsMouseWheel);
            _form.Shown += new EventHandler(OnFormShown);

            if (owner == null)
                _form.Show();
            else
                _form.Show(owner);
        }

        public void Close()
        {
            if (_disposed)
                return;

            _disposed = true;
            Form form = _form;
            XamlRuntime runtime = _runtime;
            XamlRuntime.ItemsControl items = _items;
            _form = null;
            _items = null;
            _runtime = null;
            _firstFrameHandler = null;

            Exception closeError = null;

            if (items != null)
            {
                items.Scroll -= new ScrollEventHandler(OnItemsScroll);
                items.MouseWheel -=
                    new MouseEventHandler(OnItemsMouseWheel);
            }

            if (form != null)
            {
                form.Shown -= new EventHandler(OnFormShown);

                try
                {
                    if (!form.IsDisposed)
                        form.Close();
                }
                catch (Exception ex)
                {
                    closeError = ex;
                }

                try
                {
                    if (!form.IsDisposed)
                        form.Dispose();
                }
                catch (Exception ex)
                {
                    if (closeError == null)
                        closeError = ex;
                }
            }

            try
            {
                if (runtime != null)
                    runtime.Dispose();
            }
            catch (Exception ex)
            {
                if (closeError == null)
                    closeError = ex;
            }

            if (closeError != null)
                throw closeError;
        }

        public void Dispose()
        {
            Close();
        }

        private void OnFormShown(object sender, EventArgs e)
        {
            Form form = _form;

            if (form == null || form.IsDisposed)
                return;

            form.Shown -= new EventHandler(OnFormShown);
            form.BeginInvoke(new MethodInvoker(PublishFirstFrame));
        }

        private void OnItemsScroll(object sender, ScrollEventArgs e)
        {
            _scrollEventCount++;
        }

        private void OnItemsMouseWheel(object sender, MouseEventArgs e)
        {
            _mouseWheelEventCount++;
        }

        private void PublishFirstFrame()
        {
            if (_disposed || _form == null || _form.IsDisposed)
                return;

            // Force the complete native child tree through one presented frame.
            // This is intentionally inside the message loop, not a headless
            // CreateControl approximation.
            _form.Invalidate(true);
            _form.Update();

            long elapsed = _startupWatch.ElapsedMilliseconds;
            FirstFrameHandler handler = _firstFrameHandler;
            _firstFrameHandler = null;

            if (handler != null)
                handler(this, elapsed);
        }
    }
}
