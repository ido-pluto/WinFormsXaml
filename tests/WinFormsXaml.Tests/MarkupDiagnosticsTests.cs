using System;
using System.ComponentModel;
using System.Reflection;
using System.Xml;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    internal static class MarkupDiagnosticsTests
    {
        private sealed class PlainBindingState
        {
            public string Text
            {
                get { return "Plain"; }
                set { }
            }
        }

        private sealed class ItemsState
        {
            private readonly object[] _items;

            public ItemsState()
            {
                _items = new object[] { "one" };
            }

            public object[] Items
            {
                get { return _items; }
            }
        }

        private sealed class CountingNotifyString : INotifyPropertyChanged
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

            public string Value
            {
                get { return "Safe"; }
            }
        }

        private sealed class ThrowingNotifyInt : INotifyPropertyChanged
        {
            private int _addAttempts;

            public event PropertyChangedEventHandler PropertyChanged
            {
                add
                {
                    _addAttempts++;
                    throw new InvalidOperationException(
                        "The diagnostic event accessor rejected attachment.");
                }
                remove { }
            }

            public int AddAttempts
            {
                get { return _addAttempts; }
            }

            public int Value
            {
                get { return 9; }
            }
        }

        private sealed class ComponentActivationState
        {
            private readonly CountingNotifyString _safe;
            private readonly ThrowingNotifyInt _broken;

            public ComponentActivationState()
            {
                _safe = new CountingNotifyString();
                _broken = new ThrowingNotifyInt();
            }

            public CountingNotifyString Safe
            {
                get { return _safe; }
            }

            public ThrowingNotifyInt Broken
            {
                get { return _broken; }
            }
        }

        private sealed class DeferredReloadState
        {
            private bool _throwOnRead;

            public bool ThrowOnRead
            {
                get { return _throwOnRead; }
                set { _throwOnRead = value; }
            }

            public string GetCaption()
            {
                if (_throwOnRead)
                {
                    throw new InvalidOperationException(
                        "The deferred diagnostic value could not be read.");
                }

                return "Ready";
            }
        }

        public static void Run()
        {
            TestParseLocation();
            TestNestedElementAndProperty();
            TestReservedLocationCannotSpoof();
            TestPropertyElementLocation();
            TestMarkupClassLocation();
            TestBindingPropertyContext();
            TestBindingSourceOptionContext();
            TestLateTwoWayValidationContext();
            TestDeferredReloadBindingLocation();
            TestDeferredReloadBindingsPropertyElementLocation();
            TestAttachedPropertyContext();
            TestComponentInvocationPropertyContext();
            TestComponentActivationPropertyContextAndCleanup();
            TestEmbeddedResourceSource();
            TestRegisteredComponentSourceAndPath();
            TestProjectedComponentContentSourceAndPath();
            TestNestedComponentInvocationLocation();
            TestItemTemplateCloneLocation();
            TestCompiledItemBindingEvaluationLocations();
            TestRootConditionFallbackEvaluationLocation();
            TestComponentConditionFallbackEvaluationLocation();
            TestCircularComponentFallbackLocation();
        }

        private static void TestDeferredReloadBindingLocation()
        {
            DeferredReloadState state =
                new DeferredReloadState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel Name='Root'>\n" +
                "  <Label Name='Deferred'\n" +
                "    Text='{Function GetCaption}' />\n" +
                "</Panel>",
                state);

            try
            {
                state.ThrowOnRead = true;
                WinFormsXamlLoadException failure = null;

                try
                {
                    runtime.ReloadBinding(
                        "Deferred",
                        "Text");
                }
                catch (WinFormsXamlLoadException ex)
                {
                    failure = ex;
                }

                AssertTrue(
                    failure != null,
                    "deferred ReloadBinding failure is structured");
                AssertEqual(
                    "inline XML",
                    failure.MarkupSource,
                    "deferred ReloadBinding retains its source");
                AssertEqual(
                    "/Panel#Root/Label#Deferred",
                    failure.ElementPath,
                    "deferred ReloadBinding retains its target path");
                AssertEqual(
                    "Text",
                    failure.PropertyName,
                    "deferred ReloadBinding retains its property");
                AssertLocation(
                    3,
                    5,
                    failure,
                    "deferred ReloadBinding retains attribute coordinates");
                AssertTrue(
                    !(failure.InnerException is WinFormsXamlLoadException),
                    "deferred ReloadBinding failure is wrapped once");
                AssertTrue(
                    failure.Message.IndexOf(
                        "deferred diagnostic value",
                        StringComparison.Ordinal) >= 0,
                    "deferred ReloadBinding retains the underlying message");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestDeferredReloadBindingsPropertyElementLocation()
        {
            DeferredReloadState state =
                new DeferredReloadState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Label Name='Deferred'>\n" +
                "  <Label.Text>{Function GetCaption}</Label.Text>\n" +
                "</Label>",
                state);

            try
            {
                state.ThrowOnRead = true;
                WinFormsXamlLoadException failure = null;

                try
                {
                    runtime.ReloadBindings();
                }
                catch (WinFormsXamlLoadException ex)
                {
                    failure = ex;
                }

                AssertTrue(
                    failure != null,
                    "deferred ReloadBindings failure is structured");
                AssertEqual(
                    "inline XML",
                    failure.MarkupSource,
                    "deferred ReloadBindings retains its source");
                AssertEqual(
                    "/Label#Deferred/Label.Text",
                    failure.ElementPath,
                    "deferred ReloadBindings retains its property-element path");
                AssertEqual(
                    "Text",
                    failure.PropertyName,
                    "deferred ReloadBindings retains its property");
                AssertLocation(
                    2,
                    3,
                    failure,
                    "deferred ReloadBindings retains property-element coordinates");
                AssertTrue(
                    !(failure.InnerException is WinFormsXamlLoadException),
                    "deferred ReloadBindings failure is wrapped once");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestParseLocation()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Panel>\n  <Button>\n</Panel>",
                null);

            AssertEqual(
                "inline XML",
                failure.MarkupSource,
                "inline parse source");
            AssertTrue(failure.LineNumber > 0, "parse line is retained");
            AssertTrue(failure.LinePosition > 0, "parse position is retained");
            AssertEqual(null, failure.ElementPath, "parse element is unknown");
            AssertEqual(null, failure.PropertyName, "parse property is unknown");
            AssertTrue(
                failure.InnerException is XmlException,
                "parse failure retains XmlException");
        }

        private static void TestNestedElementAndProperty()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Panel Name='Root'>\n" +
                "  <FlowLayoutPanel>\n" +
                "    <Button Name='Save'\n" +
                "      DefinitelyMissing='value' />\n" +
                "  </FlowLayoutPanel>\n" +
                "</Panel>",
                null);

            AssertEqual(
                "inline XML",
                failure.MarkupSource,
                "semantic failure source");
            AssertEqual(
                "/Panel#Root/FlowLayoutPanel/Button#Save",
                failure.ElementPath,
                "deepest semantic element path");
            AssertEqual(
                "DefinitelyMissing",
                failure.PropertyName,
                "semantic property name");
            AssertLocation(
                4,
                7,
                failure,
                "inline semantic failure uses the failing attribute");
            AssertTrue(
                !(failure.InnerException is WinFormsXamlLoadException),
                "semantic failure is wrapped once");
        }

        private static void TestReservedLocationCannotSpoof()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Button __WfxLocation='E,999,999'\n" +
                "  " +
                "MissingProperty='value' />",
                null);

            AssertLocation(
                2,
                3,
                failure,
                "source markup cannot replace parser-retained coordinates");
        }

        private static void TestPropertyElementLocation()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Button Name='Target'>\n" +
                "  <Button.TabIndex>not-a-number</Button.TabIndex>\n" +
                "</Button>",
                null);

            AssertEqual(
                "/Button#Target/Button.TabIndex",
                failure.ElementPath,
                "property-element failure uses the property element path");
            AssertEqual(
                "TabIndex",
                failure.PropertyName,
                "property-element failure retains its property name");
            AssertEqual(
                2,
                failure.LineNumber,
                "property-element failure uses the opening-element line");
            AssertEqual(
                3,
                failure.LinePosition,
                "property-element failure uses the opening-element position");
        }

        private static void TestMarkupClassLocation()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Form Name='Target'\n" +
                "      Class='WinFormsXaml.Tests.MissingMarkupClass' />",
                null);

            AssertEqual(
                "/Form#Target",
                failure.ElementPath,
                "markup-class failure retains the root path");
            AssertEqual(
                "Class",
                failure.PropertyName,
                "markup-class failure retains the Class property");
            AssertEqual(
                2,
                failure.LineNumber,
                "markup-class failure uses the Class attribute line");
            AssertEqual(
                7,
                failure.LinePosition,
                "markup-class failure uses the Class attribute position");
        }

        private static void TestEmbeddedResourceSource()
        {
            WinFormsXamlLoadException failure = null;

            try
            {
                XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                    Assembly.GetExecutingAssembly(),
                    "WinFormsXaml.Tests.Fixtures.InvalidDiagnosticForm.xml",
                    null);
                runtime.Dispose();
            }
            catch (WinFormsXamlLoadException ex)
            {
                failure = ex;
            }

            AssertTrue(failure != null, "embedded diagnostic fixture fails");
            AssertEqual(
                "WinFormsXaml.Tests.Fixtures.InvalidDiagnosticForm.xml",
                failure.MarkupSource,
                "embedded resource name is retained");
            AssertEqual(
                "/Form#DiagnosticForm/Button#Broken",
                failure.ElementPath,
                "embedded resource element path");
            AssertEqual(
                "MissingProperty",
                failure.PropertyName,
                "embedded resource property name");
            AssertEqual(
                4,
                failure.LineNumber,
                "embedded semantic failure retains its resource line");
            AssertEqual(
                5,
                failure.LinePosition,
                "embedded semantic failure retains its resource position");
        }

        private static void TestAttachedPropertyContext()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Panel Name='Host'>" +
                "  <Button Name='Child' Grid.Row='not-a-number' />" +
                "</Panel>",
                null);

            AssertEqual(
                "/Panel#Host/Button#Child",
                failure.ElementPath,
                "attached-property failure uses the child element path");
            AssertEqual(
                "Grid.Row",
                failure.PropertyName,
                "attached-property failure retains its full name");
            AssertSemanticLocation(
                failure,
                "attached-property failure retains its source location");
        }

        private static void TestBindingPropertyContext()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Label Name='Bound' Text='{Binding Missing.Member}' />",
                new object());

            AssertEqual(
                "/Label#Bound",
                failure.ElementPath,
                "binding failure retains its target element path");
            AssertEqual(
                "Text",
                failure.PropertyName,
                "binding failure retains its target property");
            AssertSemanticLocation(
                failure,
                "binding failure retains its source location");
        }

        private static void TestLateTwoWayValidationContext()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<TextBox Name='Editor' " +
                "Text='{Binding Text, Mode=TwoWay}' />",
                new PlainBindingState());

            AssertEqual(
                "/TextBox#Editor",
                failure.ElementPath,
                "late two-way validation retains its target path");
            AssertEqual(
                "Text",
                failure.PropertyName,
                "late two-way validation retains its target property");
            AssertSemanticLocation(
                failure,
                "late two-way validation retains its source location");
        }

        private static void TestBindingSourceOptionContext()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Panel Name='Root'>\n" +
                "  <Label Name='InvalidSource' " +
                "Text='{Binding Text, Source=Ancestor}' />\n" +
                "</Panel>",
                new PlainBindingState());

            AssertEqual(
                "/Panel#Root/Label#InvalidSource",
                failure.ElementPath,
                "invalid Binding Source retains its target path");
            AssertEqual(
                "Text",
                failure.PropertyName,
                "invalid Binding Source retains its target property");
            AssertTrue(
                failure.Message.IndexOf(
                    "Binding Source must be Current or CodeBehind",
                    StringComparison.Ordinal) >= 0,
                "invalid Binding Source lists the supported values");
            AssertSemanticLocation(
                failure,
                "invalid Binding Source retains its source location");

            failure = ExpectFailure(
                "<Label Name='DuplicateSource' " +
                "Text='{Binding Text, Source=Current, Source=CodeBehind}' />",
                new PlainBindingState());
            AssertEqual(
                "Text",
                failure.PropertyName,
                "duplicate Binding Source retains its target property");
            AssertTrue(
                failure.Message.IndexOf(
                    "specifies Source more than once",
                    StringComparison.Ordinal) >= 0,
                "duplicate Binding Source is diagnosed clearly");

            failure = ExpectFailure(
                "<Label Name='MissingCodeBehind' " +
                "Text='{Binding Text, Source=CodeBehind}' />",
                null);
            AssertEqual(
                "/Label#MissingCodeBehind",
                failure.ElementPath,
                "missing code-behind source retains its target path");
            AssertEqual(
                "Text",
                failure.PropertyName,
                "missing code-behind source retains its target property");
            AssertTrue(
                failure.Message.IndexOf(
                    "requires a code-behind/event target",
                    StringComparison.Ordinal) >= 0,
                "missing code-behind source explains the required target");
        }

        private static void TestComponentInvocationPropertyContext()
        {
            const string resourceName =
                "WinFormsXaml.Tests.Fixtures.EditableCard.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                resourceName);
            WinFormsXamlLoadException failure = ExpectFailure(
                "<EditableCard Name='Card'\n" +
                "  Value='valid'\n" +
                "  Count='not-a-number' />",
                null);

            AssertEqual(
                "inline XML",
                failure.MarkupSource,
                "component invocation failure uses the invoking source");
            AssertEqual(
                "/EditableCard#Card",
                failure.ElementPath,
                "component invocation failure retains its invocation path");
            AssertEqual(
                "Count",
                failure.PropertyName,
                "component conversion failure retains its declared property");
            AssertLocation(
                3,
                3,
                failure,
                "component conversion failure retains invocation location");

            failure = ExpectFailure(
                "<EditableCard Name='Card'\n" +
                "  Unexpected='value' />",
                null);
            AssertEqual(
                "Unexpected",
                failure.PropertyName,
                "unknown component invocation property is retained");
            AssertLocation(
                2,
                3,
                failure,
                "unknown component property retains invocation location");
        }

        private static void TestComponentActivationPropertyContextAndCleanup()
        {
            const string resourceName =
                "WinFormsXaml.Tests.Fixtures.EditableCard.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                resourceName);
            ComponentActivationState state =
                new ComponentActivationState();
            WinFormsXamlLoadException failure = ExpectFailure(
                "<EditableCard Name='Card'\n" +
                "  Value='{Binding Safe.Value}'\n" +
                "  Count='{Binding Broken.Value}' />",
                state);

            AssertEqual(
                "/EditableCard#Card",
                failure.ElementPath,
                "component activation failure retains its invocation path");
            AssertEqual(
                "Count",
                failure.PropertyName,
                "component activation failure retains its declared property");
            AssertLocation(
                3,
                3,
                failure,
                "component activation failure retains invocation location");
            AssertEqual(
                1,
                state.Broken.AddAttempts,
                "the failing component source was attached once");
            AssertEqual(
                0,
                state.Safe.SubscriberCount,
                "earlier component source subscriptions are rolled back");
        }

        private static void TestRegisteredComponentSourceAndPath()
        {
            const string resourceName =
                "WinFormsXaml.Tests.Fixtures.DiagnosticCard.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                resourceName);
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Panel Name='Host'>" +
                "  <DiagnosticCard Name='Card' />" +
                "</Panel>",
                null);

            AssertEqual(
                resourceName,
                failure.MarkupSource,
                "component resource name is retained");
            AssertTrue(
                failure.ElementPath.IndexOf(
                    "/Panel#Host/DiagnosticCard#Card",
                    StringComparison.Ordinal) >= 0,
                "component path includes its invocation");
            AssertTrue(
                failure.ElementPath.IndexOf(
                    "/Button#DiagnosticCardRoot",
                    StringComparison.Ordinal) >= 0,
                "component path includes its template element");
            AssertEqual(
                "MissingProperty",
                failure.PropertyName,
                "component property name is retained");
            AssertEqual(
                4,
                failure.LineNumber,
                "component template retains its original resource line");
            AssertEqual(
                5,
                failure.LinePosition,
                "component template retains its original resource position");
        }

        private static void TestProjectedComponentContentSourceAndPath()
        {
            const string resourceName =
                "WinFormsXaml.Tests.Fixtures.ContentCard.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                resourceName);
            WinFormsXamlLoadException failure = ExpectFailure(
                "<Panel Name='Host'>\n" +
                "  <ContentCard Name='Card' Title='Card'>\n" +
                "    <Button Name='ProjectedBroken'\n" +
                "      MissingProperty='value' />\n" +
                "  </ContentCard>\n" +
                "</Panel>",
                null);

            AssertEqual(
                "inline XML",
                failure.MarkupSource,
                "projected content uses the consuming markup source");
            AssertEqual(
                "/Panel#Host/ContentCard#Card -> /Button#ProjectedBroken",
                failure.ElementPath,
                "projected content uses the consuming markup path");
            AssertEqual(
                "MissingProperty",
                failure.PropertyName,
                "projected content retains the failing property");
            AssertLocation(
                4,
                7,
                failure,
                "projected content retains consuming markup coordinates");
        }

        private static void TestNestedComponentInvocationLocation()
        {
            const string editableResource =
                "WinFormsXaml.Tests.Fixtures.EditableCard.xml";
            const string nestedResource =
                "WinFormsXaml.Tests.Fixtures.NestedDiagnosticFailure.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                editableResource);
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                nestedResource);

            WinFormsXamlLoadException failure = ExpectFailure(
                "<Panel Name='Host'>\n" +
                "  <NestedDiagnosticFailure Name='Outer' />\n" +
                "</Panel>",
                null);

            AssertEqual(
                nestedResource,
                failure.MarkupSource,
                "nested invocation failure uses its component resource");
            AssertTrue(
                failure.ElementPath.IndexOf(
                    "/NestedDiagnosticFailure#Outer",
                    StringComparison.Ordinal) >= 0,
                "nested invocation path retains the outer component");
            AssertTrue(
                failure.ElementPath.IndexOf(
                    "/EditableCard#Nested",
                    StringComparison.Ordinal) >= 0,
                "nested invocation path retains the failing component");
            AssertEqual(
                "Unexpected",
                failure.PropertyName,
                "nested invocation retains the failing property");
            AssertLocation(
                5,
                7,
                failure,
                "nested component uses its original attribute coordinates");
        }

        private static void TestItemTemplateCloneLocation()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                "Virtualizing='false' ProgressiveRendering='false'>\n" +
                "  <ItemsControl.ItemTemplate>\n" +
                "    <Button Name='BrokenItem'\n" +
                "      MissingProperty='value' />\n" +
                "  </ItemsControl.ItemTemplate>\n" +
                "</ItemsControl>",
                new ItemsState());

            AssertEqual(
                "MissingProperty",
                failure.PropertyName,
                "item-template failure retains its property name");
            AssertEqual(
                4,
                failure.LineNumber,
                "item-template clone retains its original source line");
            AssertEqual(
                7,
                failure.LinePosition,
                "item-template clone retains its original source position");
        }

        private static void TestCompiledItemBindingEvaluationLocations()
        {
            string[] expressions = new string[]
            {
                "{Binding Missing, Source=CodeBehind}",
                "prefix {Binding Missing, Source=CodeBehind}"
            };
            int i;

            for (i = 0; i < expressions.Length; i++)
            {
                WinFormsXamlLoadException failure = ExpectFailure(
                    "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                    "Virtualizing='false' ProgressiveRendering='false'>\n" +
                    "  <ItemsControl.ItemTemplate>\n" +
                    "    <Label Name='BrokenItem'\n" +
                    "      Text='" + expressions[i] + "' />\n" +
                    "  </ItemsControl.ItemTemplate>\n" +
                    "</ItemsControl>",
                    new ItemsState());

                AssertEqual(
                    "/Label#BrokenItem",
                    failure.ElementPath,
                    "compiled item binding uses its cloned target path");
                AssertEqual(
                    "Text",
                    failure.PropertyName,
                    "compiled item binding retains its target property");
                AssertLocation(
                    4,
                    7,
                    failure,
                    "compiled item binding retains its attribute coordinates");
                AssertTrue(
                    !(failure.InnerException is WinFormsXamlLoadException),
                    "compiled item binding failure is wrapped once");
            }
        }

        private static void TestRootConditionFallbackEvaluationLocation()
        {
            WinFormsXamlLoadException failure = ExpectFailure(
                "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                "Width='100' Height='40' AutoScroll='true' Virtualizing='true' " +
                "VirtualizationThreshold='1' ProgressiveRendering='false'>\n" +
                "  <ItemsControl.ItemTemplate>\n" +
                "    <Label Name='VirtualBroken'\n" +
                "      Condition='{Binding Missing, Source=CodeBehind}' />\n" +
                "  </ItemsControl.ItemTemplate>\n" +
                "</ItemsControl>",
                new ItemsState());

            AssertEqual(
                "/Label#VirtualBroken",
                failure.ElementPath,
                "keyed fallback uses the item-template root path");
            AssertEqual(
                "Condition",
                failure.PropertyName,
                "keyed fallback retains Condition");
            AssertLocation(
                4,
                7,
                failure,
                "keyed fallback retains Condition coordinates");
            AssertTrue(
                !(failure.InnerException is WinFormsXamlLoadException),
                "keyed fallback failure is wrapped once");
        }

        private static void TestComponentConditionFallbackEvaluationLocation()
        {
            const string resourceName =
                "WinFormsXaml.Tests.Fixtures.VirtualDiagnosticCondition.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                resourceName);
            WinFormsXamlLoadException failure = ExpectFailure(
                "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                "Width='100' Height='40' AutoScroll='true' Virtualizing='true' " +
                "VirtualizationThreshold='1' ProgressiveRendering='false'>\n" +
                "  <ItemsControl.ItemTemplate>\n" +
                "    <VirtualDiagnosticCondition Name='Card' />\n" +
                "  </ItemsControl.ItemTemplate>\n" +
                "</ItemsControl>",
                new ItemsState());

            AssertEqual(
                resourceName,
                failure.MarkupSource,
                "component keyed fallback uses its resource source");
            AssertEqual(
                "/VirtualDiagnosticCondition#Card" +
                " -> component VirtualDiagnosticCondition" +
                " -> /Panel#VirtualDiagnosticRoot",
                failure.ElementPath,
                "component keyed fallback retains invocation and template paths");
            AssertEqual(
                "Condition",
                failure.PropertyName,
                "component keyed fallback retains Condition");
            AssertLocation(
                3,
                5,
                failure,
                "component keyed fallback retains resource coordinates");
            AssertTrue(
                !(failure.InnerException is WinFormsXamlLoadException),
                "component keyed fallback failure is wrapped once");
        }

        private static void TestCircularComponentFallbackLocation()
        {
            const string firstResource =
                "WinFormsXaml.Tests.Fixtures.CircularDiagnosticA.xml";
            const string secondResource =
                "WinFormsXaml.Tests.Fixtures.CircularDiagnosticB.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                firstResource);
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                secondResource);

            WinFormsXamlLoadException failure = ExpectFailure(
                "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                "Width='100' Height='40' AutoScroll='true' Virtualizing='true' " +
                "VirtualizationThreshold='1' ProgressiveRendering='false'>\n" +
                "  <ItemsControl.ItemTemplate>\n" +
                "    <CircularDiagnosticA Name='Cycle' />\n" +
                "  </ItemsControl.ItemTemplate>\n" +
                "</ItemsControl>",
                new ItemsState());

            AssertEqual(
                secondResource,
                failure.MarkupSource,
                "circular fallback failure uses the repeated invocation source");
            AssertEqual(
                "/CircularDiagnosticA#Cycle" +
                " -> component CircularDiagnosticA" +
                " -> /CircularDiagnosticB" +
                " -> component CircularDiagnosticB" +
                " -> /CircularDiagnosticA",
                failure.ElementPath,
                "circular fallback failure retains the complete component chain");
            AssertEqual(
                null,
                failure.PropertyName,
                "a circular element chain is not misreported as a property failure");
            AssertLocation(
                2,
                3,
                failure,
                "circular fallback failure retains resource coordinates");
            AssertTrue(
                failure.InnerException is InvalidOperationException,
                "circular fallback failure retains its original exception");
            AssertTrue(
                failure.InnerException.Message.IndexOf(
                    "circular visual-root component chain",
                    StringComparison.Ordinal) >= 0,
                "circular fallback failure explains the invalid chain");
        }

        private static WinFormsXamlLoadException ExpectFailure(
            string markup,
            object eventTarget)
        {
            try
            {
                XamlRuntime runtime = XamlRuntime.Load(markup, eventTarget);
                runtime.Dispose();
            }
            catch (WinFormsXamlLoadException ex)
            {
                return ex;
            }

            throw new InvalidOperationException(
                "Expected WinFormsXamlLoadException was not raised.");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertSemanticLocation(
            WinFormsXamlLoadException failure,
            string message)
        {
            AssertTrue(failure.LineNumber > 0, message + " line");
            AssertTrue(failure.LinePosition > 0, message + " position");
        }

        private static void AssertLocation(
            int expectedLine,
            int expectedPosition,
            WinFormsXamlLoadException failure,
            string message)
        {
            AssertEqual(expectedLine, failure.LineNumber, message + " line");
            AssertEqual(
                expectedPosition,
                failure.LinePosition,
                message + " position");
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message +
                    ": expected '" +
                    Convert.ToString(expected) +
                    "', actual '" +
                    Convert.ToString(actual) +
                    "'.");
            }
        }
    }
}
