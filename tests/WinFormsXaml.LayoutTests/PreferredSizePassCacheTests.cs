using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.LayoutTests
{
    /// <summary>
    /// A deterministic preferred-size source used to observe layout measurement.
    /// It is public so the runtime can resolve it from focused test markup.
    /// </summary>
    public class PreferredSizeProbe : Panel
    {
        private int _preferredWidth;
        private int _preferredHeight;
        private int _preferredSizeCallCount;
        private bool _reenterParentOnNextMeasurement;
        private bool _throwOnNextMeasurement;
        private bool _hasFirstProposal;
        private Size _firstProposal;
        private bool _sawDifferentProposal;

        public PreferredSizeProbe()
        {
            _preferredWidth = 30;
            _preferredHeight = 20;
        }

        public int PreferredWidth
        {
            get { return _preferredWidth; }
            set { _preferredWidth = value; }
        }

        public int PreferredHeight
        {
            get { return _preferredHeight; }
            set { _preferredHeight = value; }
        }

        public int PreferredSizeCallCount
        {
            get { return _preferredSizeCallCount; }
        }

        public bool ReenterParentOnNextMeasurement
        {
            get { return _reenterParentOnNextMeasurement; }
            set { _reenterParentOnNextMeasurement = value; }
        }

        public bool ThrowOnNextMeasurement
        {
            get { return _throwOnNextMeasurement; }
            set { _throwOnNextMeasurement = value; }
        }

        public bool SawDifferentProposal
        {
            get { return _sawDifferentProposal; }
        }

        public void ResetMeasurementCount()
        {
            _preferredSizeCallCount = 0;
            _hasFirstProposal = false;
            _firstProposal = Size.Empty;
            _sawDifferentProposal = false;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            _preferredSizeCallCount++;

            if (!_hasFirstProposal)
            {
                _hasFirstProposal = true;
                _firstProposal = proposedSize;
            }
            else if (_firstProposal != proposedSize)
            {
                _sawDifferentProposal = true;
            }

            if (_throwOnNextMeasurement)
            {
                _throwOnNextMeasurement = false;
                throw new InvalidOperationException(
                    "Intentional preferred-size failure.");
            }

            if (_reenterParentOnNextMeasurement)
            {
                _reenterParentOnNextMeasurement = false;

                Control parent = Parent;

                if (parent != null)
                    parent.GetPreferredSize(proposedSize);
            }

            return new Size(
                _preferredWidth,
                _preferredHeight);
        }
    }

    /// <summary>Forces an arranging pass to leave through an exception.</summary>
    public sealed class BoundsExceptionProbe : PreferredSizeProbe
    {
        private bool _throwOnNextBoundsChange;

        public bool ThrowOnNextBoundsChange
        {
            get { return _throwOnNextBoundsChange; }
            set { _throwOnNextBoundsChange = value; }
        }

        protected override void SetBoundsCore(
            int x,
            int y,
            int width,
            int height,
            BoundsSpecified specified)
        {
            if (_throwOnNextBoundsChange &&
                (x != Left ||
                 y != Top ||
                 width != Width ||
                 height != Height))
            {
                _throwOnNextBoundsChange = false;
                throw new InvalidOperationException(
                    "Intentional bounds failure.");
            }

            base.SetBoundsCore(
                x,
                y,
                width,
                height,
                specified);
        }
    }

    internal static class PreferredSizePassCacheTests
    {
        public static void PreservesNestedCustomLayoutSemantics()
        {
            const string markup =
                "<Grid Name='Root' Width='300' Height='180' Padding='5'>" +
                "  <Grid.RowDefinitions>" +
                "    <RowDefinition Height='30' />" +
                "    <RowDefinition Height='*' />" +
                "  </Grid.RowDefinitions>" +
                "  <StackPanel Name='Header' Grid.Row='0' " +
                "Orientation='Horizontal'>" +
                "    <Panel Name='HeaderItem' Width='40' Height='20' " +
                "Margin='2' />" +
                "  </StackPanel>" +
                "  <Border Name='Body' Grid.Row='1' Padding='3'>" +
                "    <DockPanel Name='Dock' LastChildFill='true'>" +
                "      <Canvas Name='Rail' Width='50' DockPanel.Dock='Left'>" +
                "        <Panel Name='Pin' Width='10' Height='8' " +
                "Canvas.Left='4' Canvas.Top='6' />" +
                "      </Canvas>" +
                "      <FlexPanel Name='Flex' Direction='Row' Gap='5' " +
                "AlignItems='Start'>" +
                "        <Panel Name='FirstFlex' Width='20' Height='10' />" +
                "        <Viewbox Name='Single' Width='30' Height='12'>" +
                "          <Panel Name='Leaf' />" +
                "        </Viewbox>" +
                "      </FlexPanel>" +
                "    </DockPanel>" +
                "  </Border>" +
                "</Grid>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                Panel root = runtime.Get<Panel>("Root");
                root.PerformLayout();

                AssertBounds(
                    runtime.Get<Panel>("Header"),
                    5,
                    5,
                    290,
                    30,
                    "Grid must preserve the nested header slot");

                AssertBounds(
                    runtime.Get<Panel>("HeaderItem"),
                    2,
                    2,
                    40,
                    20,
                    "Stack must preserve margin and explicit size");

                AssertBounds(
                    runtime.Get<Panel>("Dock"),
                    3,
                    3,
                    284,
                    134,
                    "Border/Single must preserve its padded content slot");

                AssertBounds(
                    runtime.Get<Panel>("Rail"),
                    0,
                    0,
                    50,
                    134,
                    "Dock must preserve the leading rail slot");

                AssertBounds(
                    runtime.Get<Panel>("Pin"),
                    4,
                    6,
                    10,
                    8,
                    "Canvas must preserve absolute child coordinates");

                AssertBounds(
                    runtime.Get<XamlRuntime.FlexPanel>("Flex"),
                    50,
                    0,
                    234,
                    134,
                    "Dock last-child fill must preserve the Flex slot");

                AssertBounds(
                    runtime.Get<Panel>("FirstFlex"),
                    0,
                    0,
                    20,
                    10,
                    "Flex must preserve the first item bounds");

                AssertBounds(
                    runtime.Get<Panel>("Single"),
                    25,
                    0,
                    30,
                    12,
                    "Flex gap and the Viewbox slot must remain stable");

                AssertBounds(
                    runtime.Get<Panel>("Leaf"),
                    0,
                    0,
                    30,
                    12,
                    "Viewbox/Single must continue stretching its child");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        public static void ReusesMeasurementWithinOneFlexPass()
        {
            XamlRuntime runtime = LoadProbeRuntime(
                "PreferredSizeProbe");

            try
            {
                XamlRuntime.FlexPanel root =
                    runtime.Get<XamlRuntime.FlexPanel>("Root");

                PreferredSizeProbe probe =
                    runtime.Get<PreferredSizeProbe>("Probe");

                probe.ResetMeasurementCount();
                root.PerformLayout();

                AssertEqual(
                    1,
                    probe.PreferredSizeCallCount,
                    "a non-wrapping Flex pass must reuse its identical " +
                    "measure/arrange preferred-size query");

                AssertEqual(
                    30,
                    probe.Width,
                    "the cached result must preserve the arranged width");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        public static void KeepsDifferentProposedSizesSeparate()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<Grid Name='Root' Width='240' Height='80'>" +
                "  <PreferredSizeProbe Name='Probe' />" +
                "</Grid>");

            try
            {
                Panel root = runtime.Get<Panel>("Root");
                PreferredSizeProbe probe =
                    runtime.Get<PreferredSizeProbe>("Probe");

                probe.ResetMeasurementCount();
                root.PerformLayout();

                if (!probe.SawDifferentProposal)
                {
                    throw new InvalidOperationException(
                        "Grid column, row, and slot constraints must remain " +
                        "distinct preferred-size cache keys.");
                }
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        public static void ClearsMeasurementsBetweenOuterPasses()
        {
            XamlRuntime runtime = LoadProbeRuntime(
                "PreferredSizeProbe");

            try
            {
                XamlRuntime.FlexPanel root =
                    runtime.Get<XamlRuntime.FlexPanel>("Root");

                PreferredSizeProbe probe =
                    runtime.Get<PreferredSizeProbe>("Probe");

                probe.ResetMeasurementCount();
                root.PerformLayout();

                probe.PreferredWidth = 74;
                root.PerformLayout();

                AssertEqual(
                    2,
                    probe.PreferredSizeCallCount,
                    "a new outer pass must perform a fresh measurement");

                AssertEqual(
                    74,
                    probe.Width,
                    "a preferred-size change between passes must not use stale data");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        public static void PreservesReentrantMeasurement()
        {
            XamlRuntime runtime = LoadProbeRuntime(
                "PreferredSizeProbe");

            try
            {
                XamlRuntime.FlexPanel root =
                    runtime.Get<XamlRuntime.FlexPanel>("Root");

                PreferredSizeProbe probe =
                    runtime.Get<PreferredSizeProbe>("Probe");

                probe.ResetMeasurementCount();
                probe.ReenterParentOnNextMeasurement = true;
                root.PerformLayout();

                AssertEqual(
                    2,
                    probe.PreferredSizeCallCount,
                    "a nested preferred-size request must complete without " +
                    "resetting or corrupting the outer pass");

                root.PerformLayout();

                AssertEqual(
                    3,
                    probe.PreferredSizeCallCount,
                    "the reentrant pass must still release its cache at the " +
                    "outer boundary");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        public static void DoesNotCacheFailedMeasurements()
        {
            XamlRuntime runtime = LoadProbeRuntime(
                "PreferredSizeProbe");

            try
            {
                XamlRuntime.FlexPanel root =
                    runtime.Get<XamlRuntime.FlexPanel>("Root");

                PreferredSizeProbe probe =
                    runtime.Get<PreferredSizeProbe>("Probe");

                probe.ResetMeasurementCount();
                probe.ThrowOnNextMeasurement = true;
                root.PerformLayout();

                AssertEqual(
                    2,
                    probe.PreferredSizeCallCount,
                    "a transient failed measurement must be retried instead of cached");

                root.PerformLayout();

                AssertEqual(
                    3,
                    probe.PreferredSizeCallCount,
                    "the successful retry must remain scoped to its outer pass");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        public static void ClearsCacheWhenArrangementThrows()
        {
            XamlRuntime runtime = LoadProbeRuntime(
                "BoundsExceptionProbe");

            try
            {
                XamlRuntime.FlexPanel root =
                    runtime.Get<XamlRuntime.FlexPanel>("Root");

                BoundsExceptionProbe probe =
                    runtime.Get<BoundsExceptionProbe>("Probe");

                probe.ResetMeasurementCount();
                probe.PreferredWidth = 83;
                probe.ThrowOnNextBoundsChange = true;

                Exception failure = null;

                try
                {
                    root.PerformLayout();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                if (failure == null)
                {
                    throw new InvalidOperationException(
                        "The focused control did not raise its intentional " +
                        "arrangement exception.");
                }

                AssertEqual(
                    1,
                    probe.PreferredSizeCallCount,
                    "the failed arranging pass should still share successful " +
                    "preferred-size queries before the exception");

                root.PerformLayout();

                AssertEqual(
                    2,
                    probe.PreferredSizeCallCount,
                    "the outer finally must clear the cache after an exception");

                AssertEqual(
                    83,
                    probe.Width,
                    "layout must recover normally after the failed pass");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static XamlRuntime LoadProbeRuntime(
            string elementName)
        {
            return XamlRuntime.Load(
                "<FlexPanel Name='Root' Width='240' Height='80'>" +
                "  <" + elementName + " Name='Probe' " +
                "PreferredWidth='30' PreferredHeight='20' />" +
                "</FlexPanel>");
        }

        private static void AssertEqual(
            int expected,
            int actual,
            string message)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    "Assertion failed: " +
                    message +
                    ". Expected <" +
                    expected +
                    ">, actual <" +
                    actual +
                    ">.");
            }
        }

        private static void AssertBounds(
            Control control,
            int x,
            int y,
            int width,
            int height,
            string message)
        {
            Rectangle expected =
                new Rectangle(
                    x,
                    y,
                    width,
                    height);

            if (control.Bounds != expected)
            {
                throw new InvalidOperationException(
                    "Assertion failed: " +
                    message +
                    ". Expected <" +
                    expected +
                    ">, actual <" +
                    control.Bounds +
                    ">.");
            }
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
    }
}
