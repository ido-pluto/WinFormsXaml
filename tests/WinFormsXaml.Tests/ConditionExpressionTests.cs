using System;
using System.Collections;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    internal static class ConditionExpressionTests
    {
        private sealed class ExpressionState
        {
            public readonly PropertyBinding<int> NumCount;
            public readonly PropertyBinding<string> TextContent;
            public readonly PropertyBinding<double> DoubleNum;
            public readonly ArrayList Rows;

            public ExpressionState(
                int numCount,
                string textContent,
                double doubleNum)
            {
                NumCount = new PropertyBinding<int>(numCount);
                TextContent =
                    new PropertyBinding<string>(textContent);
                DoubleNum = new PropertyBinding<double>(doubleNum);
                Rows = new ArrayList();
            }
        }

        private sealed class ExpressionRow
        {
            public readonly PropertyBinding<int> NumCount;
            public readonly PropertyBinding<string> TextContent;

            public ExpressionRow(int numCount, string textContent)
            {
                NumCount = new PropertyBinding<int>(numCount);
                TextContent =
                    new PropertyBinding<string>(textContent);
            }
        }

        public static void Run()
        {
            TestRequestedExpressionsAndReactivity();
            TestBindingOptionsAndOneWayTargets();
            TestItemTemplateExpressionReactivity();
            TestExpressionDiagnostics();
            TestInvariantFloatingPointLiteral();
        }

#if CONDITION_EXPRESSION_STANDALONE
        public static void Main()
        {
            Run();
            Console.WriteLine("PASS computed binding condition expressions");
        }
#endif

        private static void TestRequestedExpressionsAndReactivity()
        {
            ExpressionState state =
                new ExpressionState(11, "Other", 2.6);
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Name='Greater' " +
                "      Condition='{Binding NumCount > 10}' />" +
                "  <Label Name='AtMostTwo' " +
                "      Condition='{Binding NumCount &lt;= 2}' />" +
                "  <Label Name='Range' " +
                "      Condition='{Binding NumCount &lt; 2 &amp;&amp; NumCount > 0}' />" +
                "  <Label Name='TextMatch' " +
                "      Condition='{Binding TextContent === &quot;Text&quot; || TextContent == &quot;&quot;}' />" +
                "  <Label Name='DoubleMatch' " +
                "      Condition='{Binding DoubleNum == 2.6}' />" +
                "  <Label Name='Grouped' " +
                "      Condition='{Binding !(NumCount &lt;= 2 || DoubleNum != 2.6)}' />" +
                "</Panel>",
                state);

            try
            {
                CreateHandleAndDrain(runtime.RootControl);

                AssertVisible(runtime, "Greater", true);
                AssertVisible(runtime, "AtMostTwo", false);
                AssertVisible(runtime, "Range", false);
                AssertVisible(runtime, "TextMatch", false);
                AssertVisible(runtime, "DoubleMatch", true);
                AssertVisible(runtime, "Grouped", true);

                state.NumCount.Value = 1;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertVisible(runtime, "Greater", false);
                AssertVisible(runtime, "AtMostTwo", true);
                AssertVisible(runtime, "Range", true);
                AssertVisible(runtime, "Grouped", false);

                state.TextContent.Value = String.Empty;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertVisible(runtime, "TextMatch", true);

                state.TextContent.Value = "Text";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertVisible(runtime, "TextMatch", true);

                state.DoubleNum.Value = 2.7;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertVisible(runtime, "DoubleMatch", false);
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestBindingOptionsAndOneWayTargets()
        {
            ExpressionState state =
                new ExpressionState(1, "A,B", 0.0);
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Name='OptionCondition' " +
                "      Condition='{Binding TextContent == &quot;A,B&quot;, Source=CodeBehind}' />" +
                "  <Button Name='ComputedEnabled' " +
                "      Enabled='{Binding NumCount > 0}' />" +
                "</Panel>",
                state);

            try
            {
                CreateHandleAndDrain(runtime.RootControl);
                AssertVisible(runtime, "OptionCondition", true);
                AssertEqual(
                    true,
                    runtime.Get<Button>("ComputedEnabled").Enabled,
                    "computed one-way target value");

                state.TextContent.Value = "Other";
                state.NumCount.Value = 0;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertVisible(runtime, "OptionCondition", false);
                AssertEqual(
                    false,
                    runtime.Get<Button>("ComputedEnabled").Enabled,
                    "computed one-way target refresh");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestItemTemplateExpressionReactivity()
        {
            ExpressionState state =
                new ExpressionState(0, String.Empty, 0.0);
            ExpressionRow row = new ExpressionRow(2, "Ready");
            state.Rows.Add(row);

            XamlRuntime runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' " +
                "    ItemsSource='{Binding Rows}' " +
                "    Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel>" +
                "      <Label Text='{Binding TextContent}' " +
                "          Condition='{Binding NumCount > 1 &amp;&amp; TextContent != &quot;&quot;}' />" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>",
                state);

            try
            {
                CreateHandleAndDrain(runtime.RootControl);
                XamlRuntime.ItemsControl host =
                    runtime.Get<XamlRuntime.ItemsControl>("Rows");
                Label label = FindFirstLabel(host);

                AssertTrue(label != null, "item expression label exists");
                AssertEqual(true, label.Visible, "item expression initially true");

                row.NumCount.Value = 1;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(false, label.Visible, "item expression reacts to number");

                row.NumCount.Value = 3;
                row.TextContent.Value = String.Empty;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(false, label.Visible, "item expression reacts to text");

                row.TextContent.Value = "Visible again";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(true, label.Visible, "item expression becomes true again");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestExpressionDiagnostics()
        {
            ExpressionState state =
                new ExpressionState(1, "Text", 2.6);

            ExpectConditionFailure(
                "<Panel><Label Condition='{Binding NumCount > 0 || MissingValue == 1}' /></Panel>",
                state,
                "MissingValue");
            ExpectConditionFailure(
                "<Panel><Label Condition='{Binding NumCount = 1}' /></Panel>",
                state,
                "use '=='");
            ExpectConditionFailure(
                "<Panel><Label Condition='{Binding TextContent &lt; 2}' /></Panel>",
                state,
                "numeric operands");
            ExpectConditionFailure(
                "<Panel><Label Condition='{Binding NumCount > 0, Mode=TwoWay}' /></Panel>",
                state,
                "OneWay");

            ExpressionRow row = new ExpressionRow(2, "Ready");
            state.Rows.Add(row);
            ExpectConditionFailure(
                "<ItemsControl ItemsSource='{Binding Rows}' " +
                "    Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel><Label Condition='{Binding NumCount > 0 &amp;&amp; MissingValue == 1}' /></Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>",
                state,
                "MissingValue");
        }

        private static void TestInvariantFloatingPointLiteral()
        {
            CultureInfo previousCulture =
                Thread.CurrentThread.CurrentCulture;
            CultureInfo previousUiCulture =
                Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentCulture =
                    new CultureInfo("pl-PL");
                Thread.CurrentThread.CurrentUICulture =
                    new CultureInfo("pl-PL");

                ExpressionState state =
                    new ExpressionState(0, String.Empty, 2.6);
                XamlRuntime runtime = XamlRuntime.Load(
                    "<Label Name='Target' " +
                    "    Condition='{Binding DoubleNum == 2.6}' />",
                    state);

                try
                {
                    AssertVisible(runtime, "Target", true);
                }
                finally
                {
                    DisposeRuntime(runtime);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previousCulture;
                Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            }
        }

        private static void ExpectConditionFailure(
            string markup,
            object dataContext,
            string expectedText)
        {
            XamlRuntime runtime = null;
            Exception failure = null;

            try
            {
                runtime = XamlRuntime.Load(markup, dataContext);

                if (runtime.RootControl != null)
                    CreateHandleAndDrain(runtime.RootControl);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                DisposeRuntime(runtime);
            }

            AssertTrue(failure != null, "invalid condition expression rejected");
            AssertTrue(
                ExceptionContains(failure, expectedText),
                "condition diagnostic contains " + expectedText);

            WinFormsXamlLoadException located =
                FindLoadException(failure);

            AssertTrue(located != null, "condition failure is source-located");
            AssertEqual(
                "Condition",
                located.PropertyName,
                "condition failure property name");
        }

        private static WinFormsXamlLoadException FindLoadException(
            Exception failure)
        {
            while (failure != null)
            {
                WinFormsXamlLoadException located =
                    failure as WinFormsXamlLoadException;

                if (located != null)
                    return located;

                failure = failure.InnerException;
            }

            return null;
        }

        private static bool ExceptionContains(
            Exception failure,
            string text)
        {
            while (failure != null)
            {
                if (failure.Message != null &&
                    failure.Message.IndexOf(
                        text,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                failure = failure.InnerException;
            }

            return false;
        }

        private static Label FindFirstLabel(Control root)
        {
            if (root == null)
                return null;

            Label label = root as Label;

            if (label != null)
                return label;

            int i;

            for (i = 0; i < root.Controls.Count; i++)
            {
                label = FindFirstLabel(root.Controls[i]);

                if (label != null)
                    return label;
            }

            return null;
        }

        private static void AssertVisible(
            XamlRuntime runtime,
            string name,
            bool expected)
        {
            AssertEqual(
                expected,
                runtime.Get<Control>(name).Visible,
                name + " condition visibility");
        }

        private static void CreateHandleAndDrain(Control root)
        {
            AssertTrue(root != null, "condition expression root exists");

            if (!root.IsHandleCreated)
                root.CreateControl();

            if (!root.IsHandleCreated)
            {
                IntPtr handle = root.Handle;
                AssertTrue(handle != IntPtr.Zero, "condition expression root handle");
            }

            DrainReactiveCallbacks(root);
        }

        private static void DrainReactiveCallbacks(Control root)
        {
            int round;

            for (round = 0; round < 6; round++)
            {
                bool reached = false;

                root.BeginInvoke(
                    new MethodInvoker(
                        delegate { reached = true; }));

                int iterations = 0;

                while (!reached && iterations < 1024)
                {
                    Application.DoEvents();
                    iterations++;
                }

                AssertTrue(reached, "condition reactive dispatch sentinel");
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

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message + ".");
            }
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message +
                    ". Expected <" + expected +
                    ">, actual <" + actual + ">.");
            }
        }
    }
}
