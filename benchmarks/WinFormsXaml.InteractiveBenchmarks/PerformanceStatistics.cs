using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinFormsXaml.InteractiveBenchmarks
{
    internal sealed class SampleSeries
    {
        private readonly ArrayList _values = new ArrayList();

        public int Count
        {
            get { return _values.Count; }
        }

        public void Add(long milliseconds)
        {
            _values.Add(Math.Max(0L, milliseconds));
        }

        public long Median
        {
            get { return GetPercentile(50); }
        }

        public long Percentile95
        {
            get { return GetPercentile(95); }
        }

        public long Maximum
        {
            get { return GetPercentile(100); }
        }

        private long GetPercentile(int percentile)
        {
            if (_values.Count == 0)
                return 0;

            ArrayList ordered = new ArrayList(_values);
            ordered.Sort();

            long numerator =
                (long)percentile * (long)ordered.Count;
            int index = (int)((numerator + 99L) / 100L) - 1;
            index = Math.Max(0, Math.Min(ordered.Count - 1, index));
            return Convert.ToInt64(ordered[index]);
        }
    }

    internal sealed class ResourceSnapshot
    {
        private const int GdiResourceKind = 0;
        private const int UserResourceKind = 1;

        public long WorkingSetBytes;
        public long PrivateBytes;
        public int Gen0Collections;
        public int Gen1Collections;
        public int Gen2Collections;
        public bool HasGuiResourceCounts;
        public int GdiObjects;
        public int UserObjects;

        public static ResourceSnapshot Capture()
        {
            ResourceSnapshot snapshot = new ResourceSnapshot();
            snapshot.WorkingSetBytes = -1;
            snapshot.PrivateBytes = -1;
            snapshot.Gen0Collections = GC.CollectionCount(0);
            snapshot.Gen1Collections = GC.CollectionCount(1);
            snapshot.Gen2Collections = GC.CollectionCount(2);

            Process process = null;

            try
            {
                process = Process.GetCurrentProcess();
                process.Refresh();
                snapshot.WorkingSetBytes = process.WorkingSet64;
                snapshot.PrivateBytes = process.PrivateMemorySize64;

                if (SupportsGuiResourceCounts())
                {
                    try
                    {
                        snapshot.GdiObjects = GetGuiResources(
                            process.Handle,
                            GdiResourceKind);
                        snapshot.UserObjects = GetGuiResources(
                            process.Handle,
                            UserResourceKind);
                        snapshot.HasGuiResourceCounts = true;
                    }
                    catch
                    {
                        snapshot.HasGuiResourceCounts = false;
                    }
                }
            }
            catch
            {
                // Process counters are optional evidence on legacy systems.
                // Managed collection counts above remain available.
            }
            finally
            {
                if (process != null)
                    process.Dispose();
            }

            return snapshot;
        }

        public static string FormatDelta(
            ResourceSnapshot before,
            ResourceSnapshot after)
        {
            if (before == null || after == null)
                return "resource counters unavailable";

            string text =
                "GC collections delta=" +
                (after.Gen0Collections - before.Gen0Collections).ToString() +
                "/" +
                (after.Gen1Collections - before.Gen1Collections).ToString() +
                "/" +
                (after.Gen2Collections - before.Gen2Collections).ToString();

            if (before.WorkingSetBytes >= 0 &&
                after.WorkingSetBytes >= 0)
            {
                text +=
                    ", working-set delta=" +
                    FormatBytes(
                        after.WorkingSetBytes - before.WorkingSetBytes);
            }

            if (before.PrivateBytes >= 0 && after.PrivateBytes >= 0)
            {
                text +=
                    ", private-bytes delta=" +
                    FormatBytes(after.PrivateBytes - before.PrivateBytes);
            }

            if (before.HasGuiResourceCounts &&
                after.HasGuiResourceCounts)
            {
                text +=
                    ", GDI/USER delta=" +
                    (after.GdiObjects - before.GdiObjects).ToString() +
                    "/" +
                    (after.UserObjects - before.UserObjects).ToString();
            }
            else
            {
                text += ", GDI/USER=unavailable (requires Windows XP+)";
            }

            return text;
        }

        private static bool SupportsGuiResourceCounts()
        {
            OperatingSystem operatingSystem = Environment.OSVersion;

            return
                operatingSystem.Platform == PlatformID.Win32NT &&
                (operatingSystem.Version.Major > 5 ||
                 (operatingSystem.Version.Major == 5 &&
                  operatingSystem.Version.Minor >= 1));
        }

        private static string FormatBytes(long value)
        {
            return String.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.0} MiB",
                (double)value / (1024.0 * 1024.0));
        }

        [DllImport("user32.dll")]
        private static extern int GetGuiResources(
            IntPtr process,
            int resourceKind);
    }
}
