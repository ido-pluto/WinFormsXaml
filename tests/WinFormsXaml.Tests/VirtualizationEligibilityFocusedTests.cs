using System;
using System.Collections;
using System.Reflection;
using System.Xml;

namespace WinFormsXaml
{
    // Metadata-only regressions for direct viewport eligibility.
    internal static class VirtualizationEligibilityFocusedTests
    {
        private delegate void TestAction();
        private static readonly ArrayList _addedComponentNames =
            new ArrayList();

        internal static void Run()
        {
            XamlRuntime runtime = CreateRuntimeWithoutControls();

            try
            {
                TestStableRoots(runtime);
                TestRootVisibility(runtime);
                TestRegisteredComponentChains(runtime);
                TestCyclesAndMissingRegistrations(runtime);
                TestNullTemplateCallerSemantics(runtime);
            }
            finally
            {
                runtime.Dispose();
                RemoveTestComponents();
            }
        }

        private static void TestStableRoots(XamlRuntime runtime)
        {
            AssertEqual(
                true,
                Eligible(runtime, "<Panel />", 1),
                "a stable visual root is eligible");
            AssertEqual(
                true,
                Eligible(
                    runtime,
                    "<Panel><Label Condition='{Binding Show}' /></Panel>",
                    1),
                "a descendant Condition does not change item membership");
            AssertEqual(
                false,
                Eligible(
                    runtime,
                    "<Panel Condition='true'><Label /></Panel>",
                    1),
                "an ItemTemplate-root Condition uses keyed fallback");
            AssertEqual(
                false,
                Eligible(runtime, "<EligibilityMissingRoot />", 1),
                "an unresolved root safely uses keyed fallback");
        }

        private static void TestRootVisibility(
            XamlRuntime runtime)
        {
            AssertEqual(
                true,
                Eligible(runtime, "<Panel Visibility='Visible' />", 1),
                "a visible root retains one stable layout slot");
            AssertEqual(
                true,
                Eligible(runtime, "<Panel Visibility='Hidden' />", 1),
                "a hidden root retains one stable layout slot");
            AssertEqual(
                true,
                Eligible(runtime, "<Panel Visible='false' />", 1),
                "the native boolean visibility alias retains layout space");
            AssertEqual(
                false,
                Eligible(runtime, "<Panel Visibility='Collapsed' />", 1),
                "a collapsed root uses keyed fallback");
            AssertEqual(
                false,
                Eligible(
                    runtime,
                    "<Panel Visibility='{Binding RowVisibility}' />",
                    1),
                "a dynamic root Visibility can collapse and uses fallback");
        }

        private static void TestRegisteredComponentChains(
            XamlRuntime runtime)
        {
            AddXmlComponent(
                "EligibilityStableComponent",
                "<Panel>" +
                " <Label Condition='{Binding Show}' />" +
                "</Panel>");
            AddXmlComponent(
                "EligibilityConditionalComponent",
                "<Panel Condition='{Binding Show}' />");
            AddXmlComponent(
                "EligibilityOuterComponent",
                "<EligibilityConditionalComponent />");
            AddXmlComponent(
                "EligibilityCollapsibleComponent",
                "<Panel Visibility='{Binding RowVisibility}' />");

            AssertEqual(
                true,
                Eligible(runtime, "<EligibilityStableComponent />", 3),
                "a component with a stable template root is eligible");
            AssertEqual(
                false,
                Eligible(
                    runtime,
                    "<EligibilityStableComponent Condition='true' />",
                    3),
                "a root component invocation Condition uses fallback");
            AssertEqual(
                false,
                Eligible(
                    runtime,
                    "<EligibilityConditionalComponent />",
                    3),
                "a component template-root Condition uses fallback");
            AssertEqual(
                false,
                Eligible(runtime, "<EligibilityOuterComponent />", 3),
                "a nested component template-root Condition uses fallback");
            AssertEqual(
                false,
                Eligible(
                    runtime,
                    "<EligibilityCollapsibleComponent />",
                    3),
                "a component template-root Visibility uses fallback");
        }

        private static void TestCyclesAndMissingRegistrations(
            XamlRuntime runtime)
        {
            AddXmlComponent(
                "EligibilityCycleA",
                "<EligibilityCycleB />");
            AddXmlComponent(
                "EligibilityCycleB",
                "<EligibilityCycleA />");
            AddXmlComponent(
                "EligibilityMissingExpansion",
                "<EligibilityNotRegistered />");

            AssertEqual(
                false,
                Eligible(runtime, "<EligibilityCycleA />", 1),
                "a component-root cycle safely uses fallback");
            AssertEqual(
                false,
                Eligible(runtime, "<EligibilityMissingExpansion />", 1),
                "a missing component expansion safely uses fallback");
        }

        private static void TestNullTemplateCallerSemantics(
            XamlRuntime runtime)
        {
            AssertEqual(
                true,
                runtime.CanUseDirectViewportVirtualization(null, 0),
                "an empty source needs no ItemTemplate");
            AssertEqual(
                false,
                runtime.CanUseDirectViewportVirtualization(null, 1),
                "a nonempty source falls back to existing template validation");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate
                {
                    runtime.CanUseDirectViewportVirtualization(null, -1);
                },
                "negative logical count validation");
        }

        private static bool Eligible(
            XamlRuntime runtime,
            string xml,
            int count)
        {
            XmlDocument document = new XmlDocument();
            document.PreserveWhitespace = false;
            document.XmlResolver = null;
            document.LoadXml(xml);
            return runtime.CanUseDirectViewportVirtualization(
                document.DocumentElement,
                count);
        }

        private static XamlRuntime CreateRuntimeWithoutControls()
        {
            ConstructorInfo constructor = typeof(XamlRuntime).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[]
                {
                    typeof(object),
                    typeof(string),
                    typeof(PresetManager),
                    typeof(Assembly)
                },
                null);

            if (constructor == null)
            {
                throw new InvalidOperationException(
                    "The metadata-only runtime constructor was not found.");
            }

            return (XamlRuntime)constructor.Invoke(
                new object[]
                {
                    null,
                    null,
                    null,
                    typeof(XamlRuntime).Assembly
                });
        }

        private static void AddXmlComponent(
            string name,
            string templateXml)
        {
            Type componentType = typeof(XamlRuntime).GetNestedType(
                "RegisteredComponent",
                BindingFlags.NonPublic);
            object component = Activator.CreateInstance(componentType);
            componentType.GetField("Name").SetValue(component, name);
            componentType.GetField("TemplateXml").SetValue(
                component,
                templateXml);

            lock (GetComponentRegistrySync())
            {
                Hashtable registry = GetComponentRegistry();

                if (registry.ContainsKey(name))
                {
                    throw new InvalidOperationException(
                        "The focused component name is already registered: " +
                        name + ".");
                }

                registry.Add(name, component);
                _addedComponentNames.Add(name);
            }
        }

        private static void RemoveTestComponents()
        {
            lock (GetComponentRegistrySync())
            {
                Hashtable registry = GetComponentRegistry();
                int i;

                for (i = 0; i < _addedComponentNames.Count; i++)
                    registry.Remove(_addedComponentNames[i]);

                _addedComponentNames.Clear();
            }
        }

        private static Hashtable GetComponentRegistry()
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                "_registeredComponents",
                BindingFlags.Static | BindingFlags.NonPublic);
            return (Hashtable)field.GetValue(null);
        }

        private static object GetComponentRegistrySync()
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                "_componentRegistrySync",
                BindingFlags.Static | BindingFlags.NonPublic);
            return field.GetValue(null);
        }

        private static void ExpectException(
            Type expectedType,
            TestAction action,
            string message)
        {
            Exception failure = null;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            if (failure == null || !expectedType.IsInstanceOfType(failure))
            {
                throw new InvalidOperationException(
                    message + ": expected " + expectedType.FullName +
                    ", actual " +
                    (failure == null
                        ? "no exception"
                        : failure.GetType().FullName) + ".");
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
                    message + ": expected '" + expected +
                    "', actual '" + actual + "'.");
            }
        }
    }
}
