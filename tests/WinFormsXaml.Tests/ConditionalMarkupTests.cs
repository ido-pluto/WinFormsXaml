using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    internal static class ConditionalMarkupTests
    {
        private sealed class State
        {
            public readonly PropertyBinding<bool> Light =
                new PropertyBinding<bool>(true);
            public readonly PropertyBinding<bool> Dark =
                new PropertyBinding<bool>(false);
            public readonly PropertyBinding<bool> Accent =
                new PropertyBinding<bool>(true);
            public readonly PropertyBinding<bool> CustomScroll =
                new PropertyBinding<bool>(false);
        }

        public static void Run()
        {
            TestConditionalStylesRestoreStaleValues();
            TestConditionalObjectPropertyRestoresBaseline();
        }

        private static void TestConditionalStylesRestoreStaleValues()
        {
            State state = new State();
            string markup =
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Light'>" +
                "    <Preset Name='Light'>" +
                "      <Set Key='Defined' Value='true' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Panel.Resources>" +
                "    <Style TargetType='Button'>" +
                "      <Setter Property='ForeColor' Value='Red' " +
                "              Condition='{Binding Accent}' />" +
                "    </Style>" +
                "    <Style TargetType='Button' Condition='{Binding Light}'>" +
                "      <Setter Property='BackColor' Value='Yellow' />" +
                "    </Style>" +
                "    <Style TargetType='Button' Condition='{Binding Dark}'>" +
                "      <Setter Property='BackColor' Value='Black' />" +
                "      <Setter Property='ForeColor' Value='White' />" +
                "    </Style>" +
                "    <Style TargetType='Button' " +
                "           Condition='{Preset Theme.Missing}'>" +
                "      <Setter Property='BackColor' Value='Purple' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Button Name='Styled' />" +
                "  <Button Name='Local' BackColor='Green' />" +
                "</Panel>";
            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Button styled = runtime.Get<Button>("Styled");
                Button local = runtime.Get<Button>("Local");
                CreateHandleAndDrain(runtime.RootControl);

                AssertEqual(Color.Yellow, styled.BackColor,
                    "initial light style applies");
                AssertEqual(Color.Red, styled.ForeColor,
                    "initial conditional setter applies");
                AssertEqual(Color.Green, local.BackColor,
                    "local value wins over conditional implicit styles");

                state.Accent.Value = false;
                DrainCallbacks();
                AssertEqual(SystemColors.ControlText, styled.ForeColor,
                    "inactive setter restores the WinForms baseline");

                state.Light.Value = false;
                DrainCallbacks();
                AssertEqual(SystemColors.Control, styled.BackColor,
                    "inactive implicit style removes its stale color");

                state.Dark.Value = true;
                DrainCallbacks();
                AssertEqual(Color.Black, styled.BackColor,
                    "dark conditional implicit style activates");
                AssertEqual(Color.White, styled.ForeColor,
                    "later active implicit style wins");

                state.Light.Value = true;
                state.Dark.Value = false;
                state.Accent.Value = true;
                DrainCallbacks();
                AssertEqual(Color.Yellow, styled.BackColor,
                    "repeated transition restores light style");
                AssertEqual(Color.Red, styled.ForeColor,
                    "repeated transition restores conditional setter");
                AssertEqual(Color.Green, local.BackColor,
                    "local value stays unchanged across transitions");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestConditionalObjectPropertyRestoresBaseline()
        {
            State state = new State();
            string markup =
                "<Panel>" +
                "  <ItemsControl Name='Rows' Width='240' Height='120' " +
                "      ScrollBarGap='4'>" +
                "    <ItemsControl.VerticalScrollStyle " +
                "        Condition='{Binding CustomScroll}'>" +
                "      <ScrollBarStyle Thickness='19' />" +
                "    </ItemsControl.VerticalScrollStyle>" +
                "  </ItemsControl>" +
                "</Panel>";
            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                ItemsControl rows = runtime.Get<ItemsControl>("Rows");
                CreateHandleAndDrain(runtime.RootControl);

                AssertTrue(rows.VerticalScrollStyle == null,
                    "false property condition preserves native scrollbar mode");

                state.CustomScroll.Value = true;
                DrainCallbacks();
                AssertTrue(rows.VerticalScrollStyle != null,
                    "true property condition assigns the retained style object");
                AssertEqual(19, rows.VerticalScrollStyle.Thickness,
                    "conditional property preserves object configuration");
                AssertEqual(4, rows.ScrollBarGap,
                    "conditional host preserves scrollbar gap");

                state.CustomScroll.Value = false;
                DrainCallbacks();
                AssertTrue(rows.VerticalScrollStyle == null,
                    "false transition restores the original null property");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void CreateHandleAndDrain(Control root)
        {
            if (root != null)
                root.CreateControl();

            DrainCallbacks();
        }

        private static void DrainCallbacks()
        {
            int i;

            for (i = 0; i < 8; i++)
                Application.DoEvents();
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(object expected, object actual, string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ": expected " + expected + ", actual " + actual + ".");
            }
        }
    }
}
