using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Reflection;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private enum BindingMode
        {
            OneWay,
            TwoWay
        }

        private enum BindingUpdateSourceTrigger
        {
            PropertyChanged,
            LostFocus,
            Explicit
        }

        private enum BindingSourceKind
        {
            Current,
            CodeBehind
        }

        private sealed class BindingExpressionPlan
        {
            public readonly string Path;
            public readonly BindingMode Mode;
            public readonly BindingSourceKind Source;
            public readonly BindingUpdateSourceTrigger UpdateSourceTrigger;
            public readonly bool HasNegation;
            public readonly BindingConditionExpressionPlan ConditionExpression;

            public bool HasComputedExpression
            {
                get { return ConditionExpression != null; }
            }

            public BindingExpressionPlan(
                string path,
                BindingMode mode,
                BindingSourceKind source,
                BindingUpdateSourceTrigger updateSourceTrigger)
            {
                Path = path;
                Mode = mode;
                Source = source;
                UpdateSourceTrigger = updateSourceTrigger;

                BindingConditionExpressionPlan conditionExpression;

                ConditionExpression =
                    TryCompileBindingConditionExpression(
                        path,
                        out conditionExpression)
                            ? conditionExpression
                            : null;
                HasNegation = ConditionExpression == null
                    ? HasBindingNegation(path)
                    : ConditionExpression.HasNegation;
            }
        }

        private sealed class BindingPathDependency
        {
            public readonly object Source;
            public readonly IPropertyBindingRuntime RuntimeBinding;
            public readonly PropertyInfo NotifyProperty;
            public readonly FieldInfo NotifyField;
            public readonly string NotifyMemberName;
            public readonly object SnapshotValue;
            public readonly bool MayRebind;
            public readonly long Version;

            public BindingPathDependency(
                object source,
                IPropertyBindingRuntime runtimeBinding,
                long version)
            {
                Source = source;
                RuntimeBinding = runtimeBinding;
                NotifyProperty = null;
                NotifyField = null;
                NotifyMemberName = null;
                SnapshotValue = null;
                MayRebind = false;
                Version = version;
            }

            public BindingPathDependency(
                object source,
                PropertyInfo notifyProperty,
                FieldInfo notifyField,
                string notifyMemberName,
                object snapshotValue,
                bool mayRebind)
            {
                Source = source;
                RuntimeBinding = null;
                NotifyProperty = notifyProperty;
                NotifyField = notifyField;
                NotifyMemberName = notifyMemberName;
                SnapshotValue = snapshotValue;
                MayRebind = mayRebind;
                Version = 0;
            }
        }

        private sealed class BindingDependencySourceIndex
        {
            private readonly Hashtable _dependenciesBySource;

            public BindingDependencySourceIndex()
            {
                _dependenciesBySource =
                    new Hashtable(_observableReferenceComparer);
            }

            public bool Add(BindingPathDependency dependency)
            {
                if (dependency == null)
                    return false;

                object retained =
                    _dependenciesBySource[dependency.Source];
                BindingPathDependency single =
                    retained as BindingPathDependency;

                if (single != null)
                {
                    if (ObservableDependenciesMatch(single, dependency))
                        return false;

                    ArrayList promoted = new ArrayList(2);
                    promoted.Add(single);
                    promoted.Add(dependency);
                    _dependenciesBySource[dependency.Source] = promoted;
                    return true;
                }

                ArrayList multiple = retained as ArrayList;

                if (multiple != null)
                {
                    int i;

                    for (i = 0; i < multiple.Count; i++)
                    {
                        if (ObservableDependenciesMatch(
                                multiple[i] as BindingPathDependency,
                                dependency))
                        {
                            return false;
                        }
                    }

                    multiple.Add(dependency);
                    return true;
                }

                _dependenciesBySource.Add(
                    dependency.Source,
                    dependency);
                return true;
            }

            public object GetBucket(object source)
            {
                return _dependenciesBySource[source];
            }

            public bool ContainsSource(object source)
            {
                return source != null &&
                    _dependenciesBySource.ContainsKey(source);
            }

            public bool IsFirstDependencyForSource(
                BindingPathDependency dependency)
            {
                if (dependency == null || dependency.Source == null)
                    return false;

                object retained =
                    _dependenciesBySource[dependency.Source];
                BindingPathDependency single =
                    retained as BindingPathDependency;

                if (single != null)
                    return Object.ReferenceEquals(single, dependency);

                ArrayList multiple = retained as ArrayList;

                return multiple != null &&
                    multiple.Count != 0 &&
                    Object.ReferenceEquals(multiple[0], dependency);
            }
        }

        private sealed class BindingPathResult
        {
            public object Value;
            public Type ValueType;
            public readonly ArrayList Dependencies;
            public BindingDependencySourceIndex DependencySourceIndex;
            public BindingPathDependency TerminalDependency;
            public bool HasNegation;
            public bool HasComputedExpression;

            public BindingPathResult()
            {
                Dependencies = new ArrayList();
            }
        }

        private static bool TryConvertObjectValue(
            object value,
            Type targetType,
            out object converted)
        {
            converted = null;

            Type nullableType =
                Nullable.GetUnderlyingType(
                    targetType);

            Type effectiveType =
                nullableType != null
                    ? nullableType
                    : targetType;

            if (value == null)
            {
                if (!targetType.IsValueType ||
                    nullableType != null)
                {
                    converted = null;
                    return true;
                }

                return false;
            }

            if (targetType.IsInstanceOfType(value) ||
                effectiveType.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            try
            {
                if (effectiveType == typeof(string))
                {
                    converted = value.ToString();
                    return true;
                }

                if (effectiveType.IsEnum)
                {
                    if (value is string)
                    {
                        converted =
                            Enum.Parse(
                                effectiveType,
                                (string)value,
                                true);

                        return true;
                    }

                    converted =
                        Enum.ToObject(
                            effectiveType,
                            value);

                    return true;
                }

                TypeConverter converter =
                    TypeDescriptor.GetConverter(
                        effectiveType);

                if (converter != null &&
                    converter.CanConvertFrom(
                        value.GetType()))
                {
                    converted =
                        converter.ConvertFrom(
                            null,
                            CultureInfo.InvariantCulture,
                            value);

                    return true;
                }

                converted =
                    Convert.ChangeType(
                        value,
                        effectiveType,
                        CultureInfo.InvariantCulture);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseBindingExpression(
            string value,
            out string path)
        {
            path = null;

            BindingExpressionPlan plan;

            if (!TryParseBindingExpression(value, out plan))
                return false;

            path = plan.Path;
            return true;
        }

        private static bool TryParseBindingExpression(
            string value,
            out BindingExpressionPlan plan)
        {
            plan = null;

            if (String.IsNullOrEmpty(value))
                return false;

            string trimmed =
                value.Trim();

            if (!IsSingleMarkupExpression(trimmed))
            {
                return false;
            }

            string inner =
                trimmed.Substring(
                    1,
                    trimmed.Length - 2).Trim();

            const string bindingWord =
                "Binding";

            if (!inner.StartsWith(
                    bindingWord,
                    StringComparison.OrdinalIgnoreCase) ||
                (inner.Length > bindingWord.Length &&
                 !Char.IsWhiteSpace(inner[bindingWord.Length])))
            {
                return false;
            }

            string body =
                inner.Substring(
                    bindingWord.Length).Trim();

            if (body.Length == 0)
            {
                plan = new BindingExpressionPlan(
                    String.Empty,
                    BindingMode.OneWay,
                    BindingSourceKind.Current,
                    BindingUpdateSourceTrigger.PropertyChanged);

                return true;
            }

            string[] parts = SplitBindingExpressionParts(
                body,
                value);
            string bindingPath = null;
            BindingMode mode = BindingMode.OneWay;
            BindingSourceKind source = BindingSourceKind.Current;
            BindingUpdateSourceTrigger updateSourceTrigger =
                BindingUpdateSourceTrigger.PropertyChanged;
            bool pathSpecified = false;
            bool modeSpecified = false;
            bool sourceSpecified = false;
            bool updateSourceTriggerSpecified = false;
            int i;

            for (i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();

                if (part.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Binding expression contains an empty option: '" +
                        value + "'.");
                }

                string optionName;
                string optionValue;
                bool recognizedOption =
                    TrySplitKnownBindingOption(
                        part,
                        out optionName,
                        out optionValue);

                if (!recognizedOption)
                {
                    if (i != 0 || pathSpecified)
                    {
                        int unknownEquals = part.IndexOf('=');

                        if (unknownEquals > 0)
                        {
                            string unknownOption =
                                part.Substring(0, unknownEquals).Trim();

                            if (unknownOption.Length != 0)
                            {
                                throw new InvalidOperationException(
                                    "Unknown Binding option '" +
                                    unknownOption + "' in '" + value + "'.");
                            }
                        }

                        throw new InvalidOperationException(
                            "Binding path must be the first positional value: '" +
                            value + "'.");
                    }

                    bindingPath = part;
                    pathSpecified = true;
                    continue;
                }

                if (optionName.Length == 0 || optionValue.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Binding option names and values cannot be empty: '" +
                        value + "'.");
                }

                if (String.Equals(
                    optionName,
                    "Path",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (pathSpecified)
                    {
                        throw new InvalidOperationException(
                            "Binding expression specifies Path more than once: '" +
                            value + "'.");
                    }

                    bindingPath = optionValue;
                    pathSpecified = true;
                    continue;
                }

                if (String.Equals(
                    optionName,
                    "Mode",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (modeSpecified)
                    {
                        throw new InvalidOperationException(
                            "Binding expression specifies Mode more than once: '" +
                            value + "'.");
                    }

                    if (String.Equals(
                        optionValue,
                        "OneWay",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        mode = BindingMode.OneWay;
                    }
                    else if (String.Equals(
                        optionValue,
                        "TwoWay",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        mode = BindingMode.TwoWay;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Binding Mode must be OneWay or TwoWay: '" +
                            value + "'.");
                    }

                    modeSpecified = true;
                    continue;
                }

                if (String.Equals(
                    optionName,
                    "Source",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (sourceSpecified)
                    {
                        throw new InvalidOperationException(
                            "Binding expression specifies Source more than once: '" +
                            value + "'.");
                    }

                    if (String.Equals(
                        optionValue,
                        "Current",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        source = BindingSourceKind.Current;
                    }
                    else if (String.Equals(
                        optionValue,
                        "CodeBehind",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        source = BindingSourceKind.CodeBehind;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Binding Source must be Current or CodeBehind: '" +
                            value + "'.");
                    }

                    sourceSpecified = true;
                    continue;
                }

                if (String.Equals(
                    optionName,
                    "UpdateSourceTrigger",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (updateSourceTriggerSpecified)
                    {
                        throw new InvalidOperationException(
                            "Binding expression specifies UpdateSourceTrigger " +
                            "more than once: '" + value + "'.");
                    }

                    if (String.Equals(
                        optionValue,
                        "PropertyChanged",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        updateSourceTrigger =
                            BindingUpdateSourceTrigger.PropertyChanged;
                    }
                    else if (String.Equals(
                        optionValue,
                        "LostFocus",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        updateSourceTrigger =
                            BindingUpdateSourceTrigger.LostFocus;
                    }
                    else if (String.Equals(
                        optionValue,
                        "Explicit",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        updateSourceTrigger =
                            BindingUpdateSourceTrigger.Explicit;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Binding UpdateSourceTrigger must be " +
                            "PropertyChanged, LostFocus, or Explicit: '" +
                            value + "'.");
                    }

                    updateSourceTriggerSpecified = true;
                    continue;
                }

                throw new InvalidOperationException(
                    "Unknown Binding option '" + optionName +
                    "' in '" + value + "'.");
            }

            if (!pathSpecified)
                bindingPath = String.Empty;

            if (updateSourceTriggerSpecified &&
                mode != BindingMode.TwoWay)
            {
                throw new InvalidOperationException(
                    "Binding UpdateSourceTrigger is valid only with " +
                    "Mode=TwoWay: '" + value + "'.");
            }

            plan = new BindingExpressionPlan(
                bindingPath,
                mode,
                source,
                updateSourceTrigger);

            return true;
        }

        private static bool HasBindingNegation(string path)
        {
            if (String.IsNullOrEmpty(path))
                return false;

            path = path.Trim();
            return path.Length > 0 && path[0] == '!';
        }

        private static bool IsSingleMarkupExpression(
            string value)
        {
            if (String.IsNullOrEmpty(value) ||
                value.Length < 2 ||
                value[0] != '{' ||
                value[value.Length - 1] != '}')
            {
                return false;
            }

            int depth = 0;
            int i;

            for (i = 0; i < value.Length; i++)
            {
                if (value[i] == '{')
                {
                    depth++;
                }
                else if (value[i] == '}')
                {
                    depth--;

                    if (depth < 0 ||
                        (depth == 0 && i != value.Length - 1))
                    {
                        return false;
                    }
                }
            }

            return depth == 0;
        }

        /// <summary>
        /// Resolves a Binding path. A C#-style unary ! prefix is supported and
        /// negates the final boolean-compatible value. This works anywhere a
        /// Binding expression is accepted, including Condition. Examples:
        ///     {Binding AlreadyReaded}
        ///     {Binding !AlreadyReaded}
        ///     {Binding !State.AlreadyReaded}
        /// Multiple ! operators are allowed, so !!Value returns Value as bool.
        /// </summary>
        private static object ResolveBindingPath(
            object dataContext,
            string path)
        {
            return ResolveBindingPathResult(
                dataContext,
                path).Value;
        }

        private static BindingPathResult ResolveBindingPathResult(
            object dataContext,
            string path)
        {
            BindingPathResult result =
                new BindingPathResult();
            string originalPath = path;
            bool negate = false;

            if (!String.IsNullOrEmpty(path))
            {
                path = path.Trim();

                while (path.Length > 0 &&
                       path[0] == '!')
                {
                    result.HasNegation = true;
                    negate = !negate;
                    path = path.Substring(1).TrimStart();
                }
            }

            object current;
            Type valueType = null;

            if (dataContext == null)
            {
                current = null;
            }
            else if (String.IsNullOrEmpty(path) ||
                     path == ".")
            {
                current = dataContext;
                valueType = dataContext.GetType();
            }
            else
            {
                current = dataContext;
                valueType = dataContext.GetType();

                string[] parts =
                    GetCachedBindingPathParts(path);

                int i;

                for (i = 0;
                     i < parts.Length;
                     i++)
                {
                    string part =
                        parts[i];
                    IPropertyBindingRuntime explicitValueBinding =
                        current as IPropertyBindingRuntime;

                    if (explicitValueBinding != null &&
                        EqualsIgnoreCase(part, "Value"))
                    {
                        long version;
                        current = GetPropertyBindingSnapshot(
                            explicitValueBinding,
                            out version);
                        BindingPathDependency dependency =
                            GetOrAddBindingPathDependency(
                                result,
                                explicitValueBinding,
                                explicitValueBinding,
                                version);

                        valueType = explicitValueBinding.ValueType;

                        if (i == parts.Length - 1)
                            result.TerminalDependency = dependency;

                        continue;
                    }

                    current = UnwrapPropertyBindingValues(
                        current,
                        result,
                        false,
                        ref valueType);

                    if (current == null)
                        break;

                    IDictionary dictionary =
                        current as IDictionary;

                    if (dictionary != null &&
                        dictionary.Contains(part))
                    {
                        current =
                            dictionary[part];
                        valueType =
                            current == null
                                ? null
                                : current.GetType();

                        continue;
                    }

                    if (current is ComponentValueContext)
                    {
                        throw new InvalidOperationException(
                            "Binding path '" +
                            originalPath +
                            "' references undeclared component property '" +
                            part +
                            "'.");
                    }

                    Type type =
                        current.GetType();

                    BindingMemberLookup member =
                        GetCachedBindingMember(
                            type,
                            part);

                    if (member != null &&
                        member.Property != null)
                    {
                        object source = current;

                        valueType =
                            member.Property.PropertyType;
                        current =
                            member.Property.GetValue(
                                source,
                                null);

                        BindingPathDependency notifyDependency =
                            GetOrAddNotifyBindingPathDependency(
                                result,
                                source,
                                member.Property,
                                null,
                                current,
                                i < parts.Length - 1 ||
                                current is IPropertyBindingRuntime);

                        if (i == parts.Length - 1 &&
                            notifyDependency != null)
                        {
                            result.TerminalDependency =
                                notifyDependency;
                        }

                        continue;
                    }

                    if (member != null &&
                        member.Field != null)
                    {
                        object source = current;

                        valueType =
                            member.Field.FieldType;
                        current =
                            member.Field.GetValue(source);

                        BindingPathDependency notifyDependency =
                            GetOrAddNotifyBindingPathDependency(
                                result,
                                source,
                                null,
                                member.Field,
                                current,
                                i < parts.Length - 1 ||
                                current is IPropertyBindingRuntime);

                        if (i == parts.Length - 1 &&
                            notifyDependency != null)
                        {
                            result.TerminalDependency =
                                notifyDependency;
                        }

                        continue;
                    }

                    throw new InvalidOperationException(
                        "Binding path '" +
                        originalPath +
                        "' could not find member '" +
                        part +
                        "' on " +
                        type.FullName +
                        ".");
                }
            }

            current = UnwrapPropertyBindingValues(
                current,
                result,
                true,
                ref valueType);

            if (result.TerminalDependency != null)
            {
                result.ValueType =
                    GetBindingDependencyValueType(
                        result.TerminalDependency);
            }
            else if (current != null)
            {
                result.ValueType = current.GetType();
            }
            else
            {
                result.ValueType = valueType;
            }

            if (!negate)
            {
                result.Value = current;
                return result;
            }

            object converted;

            if (!TryConvertObjectValue(
                    current,
                    typeof(bool),
                    out converted))
            {
                throw new InvalidOperationException(
                    "Binding expression '" +
                    originalPath +
                    "' uses ! but its value is not boolean-compatible.");
            }

            result.Value = !(bool)converted;
            result.ValueType = typeof(bool);
            return result;
        }

        private static object UnwrapPropertyBindingValues(
            object value,
            BindingPathResult result,
            bool terminal,
            ref Type valueType)
        {
            ArrayList unwrapChain = null;

            while (value != null)
            {
                IPropertyBindingRuntime runtimeBinding =
                    value as IPropertyBindingRuntime;

                if (runtimeBinding == null)
                    break;

                if (unwrapChain == null)
                    unwrapChain = new ArrayList();

                if (ContainsReference(unwrapChain, value))
                {
                    throw new InvalidOperationException(
                        "PropertyBinding values contain a reference cycle.");
                }

                unwrapChain.Add(value);

                long version;
                object snapshotValue = GetPropertyBindingSnapshot(
                    runtimeBinding,
                    out version);
                BindingPathDependency dependency =
                    GetOrAddBindingPathDependency(
                        result,
                        value,
                        runtimeBinding,
                        version);

                if (terminal)
                    result.TerminalDependency = dependency;

                valueType = runtimeBinding.ValueType;
                value = snapshotValue;
            }

            return value;
        }

        private static BindingPathDependency GetOrAddBindingPathDependency(
            BindingPathResult result,
            object source,
            IPropertyBindingRuntime runtimeBinding,
            long version)
        {
            int i;

            for (i = 0; i < result.Dependencies.Count; i++)
            {
                BindingPathDependency existing =
                    result.Dependencies[i] as BindingPathDependency;

                if (existing != null &&
                    Object.ReferenceEquals(existing.Source, source) &&
                    Object.ReferenceEquals(
                        existing.RuntimeBinding,
                        runtimeBinding))
                {
                    return existing;
                }
            }

            BindingPathDependency dependency =
                new BindingPathDependency(
                    source,
                    runtimeBinding,
                    version);

            result.Dependencies.Add(dependency);
            return dependency;
        }

        private static BindingPathDependency
            GetOrAddNotifyBindingPathDependency(
                BindingPathResult result,
                object source,
                PropertyInfo property,
                FieldInfo field,
                object snapshotValue,
                bool mayRebind)
        {
            INotifyPropertyChanged notifier =
                source as INotifyPropertyChanged;

            if (notifier == null)
                return null;

            string memberName = property != null
                ? property.Name
                : field.Name;
            int i;

            for (i = 0; i < result.Dependencies.Count; i++)
            {
                BindingPathDependency existing =
                    result.Dependencies[i] as BindingPathDependency;

                if (existing != null &&
                    Object.ReferenceEquals(existing.Source, source) &&
                    Object.ReferenceEquals(existing.NotifyProperty, property) &&
                    Object.ReferenceEquals(existing.NotifyField, field) &&
                    existing.MayRebind == mayRebind)
                {
                    return existing;
                }
            }

            BindingPathDependency dependency =
                new BindingPathDependency(
                    source,
                    property,
                    field,
                    memberName,
                    snapshotValue,
                    mayRebind);

            result.Dependencies.Add(dependency);
            return dependency;
        }

        private static Type GetBindingDependencyValueType(
            BindingPathDependency dependency)
        {
            if (dependency == null)
                return null;

            if (dependency.RuntimeBinding != null)
                return dependency.RuntimeBinding.ValueType;

            if (dependency.NotifyProperty != null)
                return dependency.NotifyProperty.PropertyType;

            return dependency.NotifyField == null
                ? null
                : dependency.NotifyField.FieldType;
        }

        private static object GetPropertyBindingSnapshot(
            IPropertyBindingRuntime source,
            out long version)
        {
            if (source == null)
            {
                throw new InvalidOperationException(
                    "PropertyBinding runtime metadata is incomplete.");
            }

            return source.GetSnapshot(out version);
        }

        private static bool ContainsReference(
            ArrayList values,
            object candidate)
        {
            int i;

            for (i = 0; i < values.Count; i++)
            {
                if (Object.ReferenceEquals(values[i], candidate))
                    return true;
            }

            return false;
        }

        private static string BindingValueToString(
            object value)
        {
            if (value == null)
                return String.Empty;

            Color color;

            if (value is Color)
            {
                color = (Color)value;

                if (color.A == 255)
                {
                    return String.Format(
                        CultureInfo.InvariantCulture,
                        "#{0:X2}{1:X2}{2:X2}",
                        color.R,
                        color.G,
                        color.B);
                }

                return String.Format(
                    CultureInfo.InvariantCulture,
                    "#{0:X2}{1:X2}{2:X2}{3:X2}",
                    color.A,
                    color.R,
                    color.G,
                    color.B);
            }

            IFormattable formattable =
                value as IFormattable;

            if (formattable != null)
            {
                return formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }
    }
}
