using System;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private const int BindingFunctionInvocationPlanLimit = 128;
        private const int BindingFunctionInvocationPlansPerMethodSet = 8;

        private sealed class BindingFunctionInvocationPlan
        {
            public Type[] ArgumentTypes;
            public bool[] NullArguments;
            public MethodInfo Method;
            public ParameterInfo[] Parameters;
        }

        // FUNCTION BINDINGS
        // ============================================================

        private bool TryResolveFunctionExpression(
            string value,
            object dataContext,
            out object result)
        {
            result = null;

            TemplateExpressionPlan plan =
                GetTemplateExpressionPlan(value);

            if (plan.Kind != TemplateExpressionKind.Function)
            {
                return false;
            }

            if (_activeFunctionResultCache != null &&
                _activeFunctionResultCache.ContainsKey(value))
            {
                result =
                    _activeFunctionResultCache[value];

                return true;
            }

            result =
                InvokeBindingFunction(
                    plan.MethodName,
                    plan.ArgumentText,
                    dataContext,
                    plan.AutomaticDataContext);

            if (_activeFunctionResultCache != null)
            {
                _activeFunctionResultCache[value] =
                    result;
            }

            return true;
        }

        /// <summary>
        /// Supported examples:
        /// {Function GetImage}
        /// {Function GetImage(.)}
        /// {Function DecodeImage(ImageBytes)}
        /// </summary>
        private static bool TryParseFunctionExpression(
            string value,
            out string methodName,
            out string argumentText,
            out bool automaticDataContext)
        {
            methodName = null;
            argumentText = null;
            automaticDataContext = false;

            if (String.IsNullOrEmpty(value))
                return false;

            string trimmed = value.Trim();

            if (!IsSingleMarkupExpression(trimmed))
            {
                return false;
            }

            string inner =
                trimmed.Substring(
                    1,
                    trimmed.Length - 2).Trim();

            string body = null;

            if (inner.StartsWith(
                "Function ",
                StringComparison.OrdinalIgnoreCase))
            {
                body =
                    inner.Substring(9).Trim();
            }
            else
            {
                return false;
            }

            if (String.IsNullOrEmpty(body))
                return false;

            int open = body.IndexOf('(');

            if (open < 0)
            {
                methodName = body.Trim();
                automaticDataContext = true;
                return methodName.Length > 0;
            }

            if (!body.EndsWith(")"))
                return false;

            methodName =
                body.Substring(0, open).Trim();

            argumentText =
                body.Substring(
                    open + 1,
                    body.Length - open - 2).Trim();

            automaticDataContext = false;

            return methodName.Length > 0;
        }

        private BindingPathResult ResolveFunctionExpressionDependencies(
            string expression,
            object dataContext)
        {
            TemplateExpressionPlan plan =
                GetTemplateExpressionPlan(expression);

            if (plan.Kind != TemplateExpressionKind.Function ||
                String.IsNullOrEmpty(plan.ArgumentText))
            {
                return null;
            }

            string[] arguments =
                GetCachedFunctionArgumentParts(plan.ArgumentText);
            object source = ResolveBindingSource(dataContext);
            BindingPathResult aggregate = null;
            int i;

            for (i = 0; i < arguments.Length; i++)
            {
                string path = arguments[i] == null
                    ? null
                    : arguments[i].Trim();

                if (!IsObservableFunctionArgument(path))
                    continue;

                BindingPathResult candidate =
                    ResolveBindingPathResult(source, path);

                if (candidate.Dependencies.Count == 0)
                    continue;

                if (aggregate == null)
                    aggregate = new BindingPathResult();

                MergeBindingPathDependencies(
                    aggregate,
                    candidate,
                    aggregate.DependencySourceIndex);
            }

            return aggregate;
        }

        private static bool IsObservableFunctionArgument(string value)
        {
            if (String.IsNullOrEmpty(value) ||
                value == "." ||
                EqualsIgnoreCase(value, "DataContext") ||
                EqualsIgnoreCase(value, "this") ||
                EqualsIgnoreCase(value, "CodeBehind") ||
                EqualsIgnoreCase(value, "null") ||
                EqualsIgnoreCase(value, "true") ||
                EqualsIgnoreCase(value, "false"))
            {
                return false;
            }

            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
            {
                return false;
            }

            int integer;
            long longInteger;
            double number;

            return
                !Int32.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out integer) &&
                !Int64.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out longInteger) &&
                !Double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number);
        }

        private MethodInfo[] GetCachedBindingFunctionMethods(
            string methodName,
            object eventTarget)
        {
            if (eventTarget == null)
                return _emptyMethodInfoArray;

            string cacheKey = Object.ReferenceEquals(
                    eventTarget,
                    _eventTarget)
                ? methodName
                : "@" +
                  eventTarget.GetType().AssemblyQualifiedName +
                  "\n" +
                  methodName;
            MethodInfo[] cached =
                _bindingFunctionMethodsCache == null
                    ? null
                    : _bindingFunctionMethodsCache[cacheKey]
                        as MethodInfo[];

            if (cached != null)
                return cached;

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            MethodInfo[] all = Object.ReferenceEquals(
                    eventTarget,
                    _eventTarget)
                ? GetCachedEventTargetMethods()
                : eventTarget.GetType().GetMethods(flags);

            ArrayList matches = new ArrayList();
            int i;

            for (i = 0; i < all.Length; i++)
            {
                if (String.Equals(
                    all[i].Name,
                    methodName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(all[i]);
                }
            }

            MethodInfo[] result =
                (MethodInfo[])matches.ToArray(
                    typeof(MethodInfo));

            if (_bindingFunctionMethodsCache != null &&
                _bindingFunctionMethodsCache.Count <
                    BindingFunctionMethodCacheLimit &&
                cacheKey.Length <= RuntimeMetadataCacheKeyLengthLimit)
            {
                _bindingFunctionMethodsCache[cacheKey] = result;
            }

            return result;
        }

        private object InvokeBindingFunction(
            string methodName,
            string argumentText,
            object dataContext,
            bool automaticDataContext)
        {
            object eventTarget =
                GetComponentEventTarget(dataContext);

            if (eventTarget == null)
            {
                throw new InvalidOperationException(
                    "Function binding '" +
                    methodName +
                    "' requires a code-behind/event target object.");
            }

            EnsureBindingFunctionCaches();

            bool explicitNullDataContext =
                Object.ReferenceEquals(
                    dataContext,
                    _nullItemDataContext);
            object actualDataContext =
                UnwrapDataContext(dataContext);
            object[] rawArguments = _emptyObjectArray;

            if (!String.IsNullOrEmpty(argumentText))
            {
                string[] parts =
                    GetCachedFunctionArgumentParts(
                        argumentText);

                rawArguments = new object[parts.Length];
                int n;

                for (n = 0;
                     n < parts.Length;
                     n++)
                {
                    rawArguments[n] =
                        ResolveFunctionArgument(
                            parts[n],
                            dataContext);
                }
            }

            MethodInfo[] methods =
                GetCachedBindingFunctionMethods(
                    methodName,
                    eventTarget);

            object[] automaticArguments = null;

            if (automaticDataContext &&
                (actualDataContext != null || explicitNullDataContext))
            {
                automaticArguments =
                    new object[]
                    {
                        actualDataContext
                    };
            }

            // A bare {Function Name} first prefers a zero-argument overload.
            // If none matches, it tries passing the current item automatically.
            MethodInfo selected = null;
            object[] convertedArguments = null;

            if (automaticDataContext)
            {
                if (!TryFindBindingFunction(
                    methods,
                    methodName,
                    _emptyObjectArray,
                    out selected,
                    out convertedArguments) &&
                    automaticArguments != null)
                {
                    TryFindBindingFunction(
                        methods,
                        methodName,
                        automaticArguments,
                        out selected,
                        out convertedArguments);
                }
            }
            else
            {
                TryFindBindingFunction(
                    methods,
                    methodName,
                    rawArguments,
                    out selected,
                    out convertedArguments);
            }

            if (selected == null)
            {
                throw new InvalidOperationException(
                    "Could not find a compatible code-behind function named '" +
                    methodName +
                    "' for the supplied binding arguments.");
            }

            if (selected.ReturnType == typeof(void))
            {
                throw new InvalidOperationException(
                    "Function binding '" +
                    methodName +
                    "' returns void. A binding function must return a value.");
            }

            try
            {
                return selected.Invoke(
                    selected.IsStatic
                        ? null
                        : eventTarget,
                    convertedArguments);
            }
            catch (TargetInvocationException ex)
            {
                Exception inner =
                    ex.InnerException != null
                        ? ex.InnerException
                        : ex;

                throw new InvalidOperationException(
                    "Function binding '" +
                    methodName +
                    "' threw: " +
                    inner.Message,
                    inner);
            }
        }

        private bool TryFindBindingFunction(
            MethodInfo[] methods,
            string methodName,
            object[] arguments,
            out MethodInfo selected,
            out object[] convertedArguments)
        {
            selected = null;
            convertedArguments = null;

            BindingFunctionInvocationPlan cachedPlan =
                FindBindingFunctionInvocationPlan(
                    methods,
                    arguments);

            if (cachedPlan != null &&
                TryConvertBindingFunctionArguments(
                    cachedPlan.Parameters,
                    arguments,
                    out convertedArguments))
            {
                selected = cachedPlan.Method;
                _bindingFunctionInvocationPlanHitCount++;
                return true;
            }

            int i;

            for (i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method = methods[i];

                if (!String.Equals(
                    method.Name,
                    methodName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    GetCachedBindingFunctionParameters(method);

                if (parameters.Length != arguments.Length)
                    continue;

                object[] converted;

                if (!TryConvertBindingFunctionArguments(
                        parameters,
                        arguments,
                        out converted))
                {
                    continue;
                }

                selected = method;
                convertedArguments = converted;

                TryCacheBindingFunctionInvocationPlan(
                    methods,
                    i,
                    arguments,
                    method,
                    parameters);

                return true;
            }

            return false;
        }

        private static bool TryConvertBindingFunctionArguments(
            ParameterInfo[] parameters,
            object[] arguments,
            out object[] convertedArguments)
        {
            convertedArguments = null;

            if (parameters == null ||
                arguments == null ||
                parameters.Length != arguments.Length)
            {
                return false;
            }

            object[] converted = null;
            int i;

            for (i = 0; i < arguments.Length; i++)
            {
                object convertedValue;

                if (!TryConvertObjectValue(
                        arguments[i],
                        parameters[i].ParameterType,
                        out convertedValue))
                {
                    return false;
                }

                if (!Object.ReferenceEquals(
                        convertedValue,
                        arguments[i]))
                {
                    if (converted == null)
                        converted = (object[])arguments.Clone();

                    converted[i] = convertedValue;
                }
                else if (converted != null)
                {
                    converted[i] = convertedValue;
                }
            }

            convertedArguments = converted == null
                ? arguments
                : converted;

            return true;
        }

        private BindingFunctionInvocationPlan
            FindBindingFunctionInvocationPlan(
                MethodInfo[] methods,
                object[] arguments)
        {
            if (_bindingFunctionInvocationPlans == null ||
                methods == null ||
                arguments == null)
            {
                return null;
            }

            ArrayList plans =
                _bindingFunctionInvocationPlans[methods]
                    as ArrayList;

            if (plans == null)
                return null;

            int i;

            for (i = 0; i < plans.Count; i++)
            {
                BindingFunctionInvocationPlan plan =
                    plans[i] as BindingFunctionInvocationPlan;

                if (plan == null ||
                    plan.ArgumentTypes.Length != arguments.Length)
                {
                    continue;
                }

                bool matches = true;
                int n;

                for (n = 0; n < arguments.Length; n++)
                {
                    object argument = arguments[n];

                    if (argument == null)
                    {
                        if (!plan.NullArguments[n])
                        {
                            matches = false;
                            break;
                        }
                    }
                    else if (plan.NullArguments[n] ||
                             plan.ArgumentTypes[n] != argument.GetType())
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return plan;
            }

            return null;
        }

        private void TryCacheBindingFunctionInvocationPlan(
            MethodInfo[] methods,
            int selectedIndex,
            object[] arguments,
            MethodInfo selected,
            ParameterInfo[] parameters)
        {
            if (_bindingFunctionInvocationPlans == null ||
                _bindingFunctionInvocationPlanCount >=
                    BindingFunctionInvocationPlanLimit ||
                methods == null ||
                arguments == null ||
                selected == null ||
                parameters == null ||
                !IsStableBindingFunctionSelection(
                    methods,
                    selectedIndex,
                    arguments,
                    parameters))
            {
                return;
            }

            ArrayList plans =
                _bindingFunctionInvocationPlans[methods]
                    as ArrayList;

            if (plans == null)
            {
                plans = new ArrayList();
                _bindingFunctionInvocationPlans[methods] = plans;
            }

            if (plans.Count >=
                BindingFunctionInvocationPlansPerMethodSet)
            {
                return;
            }

            BindingFunctionInvocationPlan plan =
                new BindingFunctionInvocationPlan();
            plan.ArgumentTypes = new Type[arguments.Length];
            plan.NullArguments = new bool[arguments.Length];
            plan.Method = selected;
            plan.Parameters = parameters;

            int i;

            for (i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] == null)
                    plan.NullArguments[i] = true;
                else
                    plan.ArgumentTypes[i] = arguments[i].GetType();
            }

            plans.Add(plan);
            _bindingFunctionInvocationPlanCount++;
        }

        private bool IsStableBindingFunctionSelection(
            MethodInfo[] methods,
            int selectedIndex,
            object[] arguments,
            ParameterInfo[] selectedParameters)
        {
            int i;

            for (i = 0; i < arguments.Length; i++)
            {
                if (!IsDirectBindingFunctionArgument(
                        arguments[i],
                        selectedParameters[i].ParameterType))
                {
                    return false;
                }
            }

            // Reflection order is part of the existing first-compatible
            // behavior. A prior same-arity overload can become compatible when
            // a string or numeric value changes even if its CLR type does not.
            // Cache only when every earlier candidate is rejected for the
            // value-independent null/non-nullable rule.
            for (i = 0; i < selectedIndex; i++)
            {
                ParameterInfo[] earlier =
                    GetCachedBindingFunctionParameters(methods[i]);

                if (earlier.Length != arguments.Length)
                    continue;

                bool stableMismatch = false;
                int n;

                for (n = 0; n < arguments.Length; n++)
                {
                    if (arguments[n] == null &&
                        IsNonNullableValueType(
                            earlier[n].ParameterType))
                    {
                        stableMismatch = true;
                        break;
                    }
                }

                if (!stableMismatch)
                    return false;
            }

            return true;
        }

        private static bool IsDirectBindingFunctionArgument(
            object argument,
            Type parameterType)
        {
            if (argument == null)
                return !IsNonNullableValueType(parameterType);

            Type nullableType = Nullable.GetUnderlyingType(parameterType);
            Type effectiveType = nullableType == null
                ? parameterType
                : nullableType;

            return parameterType.IsInstanceOfType(argument) ||
                   effectiveType.IsInstanceOfType(argument);
        }

        private static bool IsNonNullableValueType(Type type)
        {
            return type != null &&
                   type.IsValueType &&
                   Nullable.GetUnderlyingType(type) == null;
        }

        private ParameterInfo[] GetCachedBindingFunctionParameters(
            MethodInfo method)
        {
            ParameterInfo[] cached =
                _bindingFunctionParametersCache == null
                    ? null
                    : _bindingFunctionParametersCache[method]
                        as ParameterInfo[];

            if (cached != null)
                return cached;

            cached = method.GetParameters();

            if (_bindingFunctionParametersCache != null &&
                _bindingFunctionParametersCache.Count <
                    BindingFunctionParameterCacheLimit)
            {
                _bindingFunctionParametersCache[method] = cached;
            }

            return cached;
        }

        private object ResolveFunctionArgument(
            string text,
            object dataContext)
        {
            if (text == null)
                return null;

            text = text.Trim();

            if (text.Length == 0)
                return null;

            if (text == "." ||
                EqualsIgnoreCase(
                    text,
                    "DataContext"))
            {
                return UnwrapDataContext(dataContext);
            }

            if (EqualsIgnoreCase(
                    text,
                    "this") ||
                EqualsIgnoreCase(
                    text,
                    "CodeBehind"))
            {
                return GetComponentEventTarget(dataContext);
            }

            if (EqualsIgnoreCase(
                text,
                "null"))
            {
                return null;
            }

            if (EqualsIgnoreCase(
                text,
                "true"))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                text,
                "false"))
            {
                return false;
            }

            if ((text.StartsWith("\"") &&
                 text.EndsWith("\"")) ||
                (text.StartsWith("'") &&
                 text.EndsWith("'")))
            {
                return UnquoteFunctionString(text);
            }

            int intValue;

            if (Int32.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out intValue))
            {
                return intValue;
            }

            long longValue;

            if (Int64.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out longValue))
            {
                return longValue;
            }

            double doubleValue;

            if (Double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out doubleValue))
            {
                return doubleValue;
            }

            object source = ResolveBindingSource(dataContext);

            return ResolveBindingPath(
                source,
                text);
        }

        private static string UnquoteFunctionString(
            string value)
        {
            if (value == null ||
                value.Length < 2)
            {
                return value;
            }

            string result =
                value.Substring(
                    1,
                    value.Length - 2);

            result =
                result.Replace(
                    "\\\"",
                    "\"");

            result =
                result.Replace(
                    "\\'",
                    "'");

            result =
                result.Replace(
                    "\\\\",
                    "\\");

            return result;
        }

        private string[] GetCachedFunctionArgumentParts(
            string value)
        {
            if (String.IsNullOrEmpty(value))
                return _emptyStringArray;

            if (_functionArgumentPartsCache == null)
            {
                _functionArgumentPartsCache =
                    new Hashtable(StringComparer.Ordinal);
            }

            string[] cached =
                _functionArgumentPartsCache[value] as string[];

            if (cached != null)
                return cached;

            cached = SplitFunctionArguments(value);

            if (_functionArgumentPartsCache != null &&
                _functionArgumentPartsCache.Count <
                    FunctionArgumentPartsCacheLimit &&
                value.Length <= RuntimeMetadataCacheKeyLengthLimit)
            {
                _functionArgumentPartsCache[value] = cached;
            }

            return cached;
        }

        private static string[] SplitFunctionArguments(
            string value)
        {
            ArrayList parts =
                new ArrayList();

            int start = 0;
            int depth = 0;
            char quote = '\0';
            bool escaped = false;
            int i;

            for (i = 0;
                 i < value.Length;
                 i++)
            {
                char c = value[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' &&
                    quote != '\0')
                {
                    escaped = true;
                    continue;
                }

                if (quote != '\0')
                {
                    if (c == quote)
                        quote = '\0';

                    continue;
                }

                if (c == '\"' ||
                    c == '\'')
                {
                    quote = c;
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                    continue;
                }

                if (c == ')')
                {
                    if (depth > 0)
                        depth--;

                    continue;
                }

                if (c == ',' &&
                    depth == 0)
                {
                    parts.Add(
                        value.Substring(
                            start,
                            i - start).Trim());

                    start = i + 1;
                }
            }

            parts.Add(
                value.Substring(start).Trim());

            string[] result =
                new string[parts.Count];

            for (i = 0;
                 i < parts.Count;
                 i++)
            {
                result[i] =
                    (string)parts[i];
            }

            return result;
        }

        private void EnsureBindingFunctionCaches()
        {
            if (_bindingFunctionMethodsCache == null)
            {
                _bindingFunctionMethodsCache =
                    new Hashtable(StringComparer.OrdinalIgnoreCase);
            }

            if (_bindingFunctionParametersCache == null)
                _bindingFunctionParametersCache = new Hashtable();

            if (_bindingFunctionInvocationPlans == null)
                _bindingFunctionInvocationPlans = new Hashtable();

            if (_functionArgumentPartsCache == null)
            {
                _functionArgumentPartsCache =
                    new Hashtable(StringComparer.Ordinal);
            }
        }

    }
}
