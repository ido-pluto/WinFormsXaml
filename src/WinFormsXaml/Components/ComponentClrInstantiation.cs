using System;
using System.Collections;
using System.Reflection;
using System.Xml;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private object CreateRegisteredTypeComponent(
            RegisteredComponent component,
            XmlElement element,
            out Hashtable constructorAttributes)
        {
            constructorAttributes = null;

            ComponentConstructorCandidate[] constructors =
                component.ComponentConstructors;
            ConstructorInfo selected = null;
            object[] selectedArguments = null;
            ArrayList selectedAttributes = null;
            int selectedScore = -1;
            int selectedParameterCount = Int32.MaxValue;
            bool ambiguous = false;
            int i;

            for (i = 0; i < constructors.Length; i++)
            {
                ComponentConstructorCandidate candidate = constructors[i];
                ConstructorInfo constructor = candidate.Constructor;
                ParameterInfo[] parameters = candidate.Parameters;
                object[] arguments = parameters.Length == 0
                    ? _emptyObjectArray
                    : new object[parameters.Length];
                ArrayList suppliedAttributes = null;
                int score = 0;
                bool matches = true;
                int n;

                for (n = 0; n < parameters.Length; n++)
                {
                    ParameterInfo parameter = parameters[n];
                    XmlAttribute attribute =
                        FindComponentConstructorAttribute(
                            element,
                            parameter.Name);

                    if (attribute == null)
                    {
                        if (parameter.IsOptional)
                        {
                            arguments[n] = parameter.DefaultValue;
                            continue;
                        }

                        matches = false;
                        break;
                    }

                    object rawValue;

                    if (!TryPeekBoundObject(
                        attribute.Value,
                        out rawValue))
                    {
                        rawValue = attribute.Value;
                    }

                    object converted;

                    if (!TryConvertObjectValue(
                        rawValue,
                        parameter.ParameterType,
                        out converted))
                    {
                        matches = false;
                        break;
                    }

                    arguments[n] = converted;
                    if (suppliedAttributes == null)
                        suppliedAttributes = new ArrayList(parameters.Length);

                    suppliedAttributes.Add(attribute);
                    score++;
                }

                if (!matches)
                    continue;

                if (score > selectedScore ||
                    (score == selectedScore &&
                     parameters.Length < selectedParameterCount))
                {
                    selected = constructor;
                    selectedArguments = arguments;
                    selectedAttributes = suppliedAttributes;
                    selectedScore = score;
                    selectedParameterCount = parameters.Length;
                    ambiguous = false;
                }
                else if (score == selectedScore &&
                         parameters.Length == selectedParameterCount)
                {
                    ambiguous = true;
                }
            }

            if (selected == null)
            {
                throw new InvalidOperationException(
                    "No public constructor on registered component type '" +
                    component.ComponentType.FullName +
                    "' can be satisfied by the attributes on <" +
                    component.Name +
                    ">.");
            }

            if (ambiguous)
            {
                throw new InvalidOperationException(
                    "Attributes on registered component <" +
                    component.Name +
                    "> match more than one public constructor on '" +
                    component.ComponentType.FullName +
                    "'.");
            }

            object instance;

            try
            {
                instance = selected.Invoke(selectedArguments);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Constructor for registered component <" +
                    component.Name +
                    "> failed: " +
                    ex.Message,
                    ex);
            }

            if (selectedAttributes != null && selectedAttributes.Count != 0)
            {
                constructorAttributes =
                    new Hashtable(StringComparer.OrdinalIgnoreCase);

                for (i = 0; i < selectedAttributes.Count; i++)
                {
                    XmlAttribute attribute =
                        selectedAttributes[i] as XmlAttribute;
                    string attributeName = attribute.LocalName;
                    constructorAttributes[attributeName] = true;

                    // The constructor already consumed the prepared object
                    // token. Initial property/event application is skipped,
                    // but a retained dynamic binding may apply a later value.
                    object ignored;

                    TryTakeBoundObject(attribute.Value, out ignored);
                }
            }

            return instance;
        }

        private XmlAttribute FindComponentConstructorAttribute(
            XmlElement element,
            string name)
        {
            XmlAttribute attribute =
                FindAttributeIgnoreNamespace(element, name);

            if (attribute == null ||
                ShouldIgnoreAttribute(attribute) ||
                EqualsIgnoreCase(attribute.LocalName, "Name") ||
                EqualsIgnoreCase(attribute.LocalName, "Style") ||
                EqualsIgnoreCase(attribute.LocalName, "ResourceStyle") ||
                EqualsIgnoreCase(attribute.LocalName, "Condition") ||
                attribute.LocalName.IndexOf('.') >= 0)
            {
                return null;
            }

            return attribute;
        }

        private bool TryPeekBoundObject(
            string token,
            out object value)
        {
            value = null;

            return !String.IsNullOrEmpty(token) &&
                _boundObjectValues.TryGetValue(token, out value);
        }

        private static void ValidateConstructorOnlyBindings(
            ArrayList bindings,
            Hashtable constructorAttributes,
            object instance,
            string componentName)
        {
            if (bindings == null || constructorAttributes == null)
                return;

            int i;

            for (i = 0; i < bindings.Count; i++)
            {
                DynamicPropertyBinding binding =
                    bindings[i] as DynamicPropertyBinding;

                if (binding != null &&
                    constructorAttributes.ContainsKey(
                        binding.PropertyName))
                {
                    PropertyInfo property = FindProperty(
                        instance.GetType(),
                        binding.PropertyName);
                    EventInfo eventInfo = FindEvent(
                        instance.GetType(),
                        binding.PropertyName);

                    if ((property != null &&
                         property.CanWrite &&
                         property.GetIndexParameters().Length == 0) ||
                        eventInfo != null)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        "Binding for constructor-only attribute '" +
                        binding.PropertyName +
                        "' on registered component <" +
                        componentName +
                        "> cannot be reloaded. Expose a writable property to make it reactive.");
                }
            }
        }
    }
}
