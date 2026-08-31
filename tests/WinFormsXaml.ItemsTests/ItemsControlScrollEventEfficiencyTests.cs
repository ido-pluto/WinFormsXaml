using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    /// <summary>
    /// Guards the bounded native/message cost of one-dimensional styled
    /// scrolling. These assertions measure transactions rather than elapsed
    /// time so they remain deterministic on Windows 98 and Wine.
    /// </summary>
    internal static class ItemsControlScrollEventEfficiencyTests
    {
        internal static void RunAll()
        {
            TestStyledInputPublishesOncePerMovedFrame();
        }

        private static void TestStyledInputPublishesOncePerMovedFrame()
        {
            Form form = new Form();
            XamlRuntime.ItemsControl host =
                new XamlRuntime.ItemsControl();

            try
            {
                form.ClientSize = new Size(320, 240);
                host.Location = new Point(20, 20);
                host.Size = new Size(240, 160);
                host.AutoScroll = true;
                host.Orientation = Orientation.Vertical;
                host.VerticalScrollStyle = new ScrollBarStyle();
                host.AutoScrollMinSize = new Size(1, 2400);
                form.Controls.Add(host);
                form.Show();
                Application.DoEvents();
                host.PerformLayout();
                Application.DoEvents();

                ScrollBarControl bar = host.ThemedScrollBarForTest;
                AssertTrue(bar != null, "styled bar exists");
                AssertTrue(bar.Visible, "styled bar is visible");

                host.SmoothScroll = false;
                host.SetLogicalScrollOffset(80);

                long synchronizations =
                    host.ThemedScrollBarSynchronizationCountForTest;
                long frames =
                    host.ScrollVisualFramePublicationCountForTest;
                long probes =
                    host.ThemedNativeChromeProbeCountForTest;
                int i;

                for (i = 0; i < 12; i++)
                {
                    AssertTrue(
                        bar.ExecuteScrollCommand(
                            ScrollEventType.SmallIncrement),
                        "immediate command moves " + i);
                }

                AssertEqual(
                    synchronizations + 12L,
                    host.ThemedScrollBarSynchronizationCountForTest,
                    "one bar synchronization per moved frame");
                AssertEqual(
                    frames + 12L,
                    host.ScrollVisualFramePublicationCountForTest,
                    "one visual publication per moved frame");
                AssertEqual(
                    probes,
                    host.ThemedNativeChromeProbeCountForTest,
                    "stable styled frames perform no user32 chrome probe");

                host.SetLogicalScrollOffset(
                    host.GetMaximumLogicalScrollOffsetForTest);
                synchronizations =
                    host.ThemedScrollBarSynchronizationCountForTest;
                frames = host.ScrollVisualFramePublicationCountForTest;
                probes = host.ThemedNativeChromeProbeCountForTest;

                for (i = 0; i < 32; i++)
                {
                    AssertTrue(
                        !bar.ExecuteScrollCommand(
                            ScrollEventType.SmallIncrement),
                        "range-bound input is a no-op " + i);
                }

                AssertEqual(
                    synchronizations,
                    host.ThemedScrollBarSynchronizationCountForTest,
                    "range-bound input performs no synchronization");
                AssertEqual(
                    frames,
                    host.ScrollVisualFramePublicationCountForTest,
                    "range-bound input performs no visual publication");
                AssertEqual(
                    probes,
                    host.ThemedNativeChromeProbeCountForTest,
                    "range-bound input performs no native probe");

                host.SmoothScroll = false;
                host.SetLogicalScrollOffset(80);
                host.SmoothScroll = true;
                host.SmoothScrollDuration = 120;
                synchronizations =
                    host.ThemedScrollBarSynchronizationCountForTest;
                frames = host.ScrollVisualFramePublicationCountForTest;
                probes = host.ThemedNativeChromeProbeCountForTest;

                for (i = 0; i < 16; i++)
                {
                    bar.ExecuteScrollCommand(
                        ScrollEventType.SmallIncrement);
                }

                AssertTrue(
                    host.SmoothScrollAnimationActiveForTest,
                    "smooth burst starts one animation");
                AssertEqual(
                    synchronizations,
                    host.ThemedScrollBarSynchronizationCountForTest,
                    "retarget-only input publishes no duplicate thumb frame");
                AssertEqual(
                    frames,
                    host.ScrollVisualFramePublicationCountForTest,
                    "retarget-only input moves no retained children");

                host.ApplySmoothScrollFrameForTest(30);
                host.ApplySmoothScrollFrameForTest(60);
                host.ApplySmoothScrollFrameForTest(90);
                host.ApplySmoothScrollFrameForTest(120);

                long publishedFrames =
                    host.ScrollVisualFramePublicationCountForTest - frames;
                long publishedSynchronizations =
                    host.ThemedScrollBarSynchronizationCountForTest -
                        synchronizations;

                AssertTrue(
                    publishedFrames > 0L && publishedFrames <= 4L,
                    "timer publishes at most one visual frame per tick");
                AssertEqual(
                    publishedFrames,
                    publishedSynchronizations,
                    "timer keeps content and thumb in one transaction");
                AssertEqual(
                    probes,
                    host.ThemedNativeChromeProbeCountForTest,
                    "smooth frames perform no repeated native probe");
            }
            finally
            {
                if (!host.IsDisposed)
                    host.Dispose();

                form.Dispose();
            }
        }

        private static void AssertEqual(
            long expected,
            long actual,
            string message)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    message + ": expected " + expected +
                    ", actual " + actual + ".");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message + ".");
        }
    }
}
