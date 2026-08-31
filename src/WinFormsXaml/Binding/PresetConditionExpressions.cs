using System;
using System.Collections;
using System.Globalization;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private sealed class PresetConditionExpressionPlan
        {
            public readonly string Expression;
            public readonly BindingConditionExpressionPlan Condition;

            public PresetConditionExpressionPlan(
                string expression,
                BindingConditionExpressionPlan condition)
            {
                Expression = expression;
                Condition = condition;
            }
        }

        private sealed class PresetConditionValue
        {
            public readonly object Value;
            public readonly bool FromPreset;

            public PresetConditionValue(object value, bool fromPreset)
            {
                Value = value;
                FromPreset = fromPreset;
            }
        }

        private static bool TryParsePresetConditionExpression(
            string value,
            out PresetConditionExpressionPlan plan)
        {
            plan = null;

            if (String.IsNullOrEmpty(value))
                return false;

            string text = value.Trim();
            const string prefix = "{Preset ";

            if (!IsSingleMarkupExpression(text) ||
                !text.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string body = text.Substring(
                prefix.Length,
                text.Length - prefix.Length - 1).Trim();

            if (!LooksLikePresetConditionExpression(body))
                return false;

            BindingConditionExpressionPlan condition;

            try
            {
                condition = new BindingConditionParser(body).Parse();
                ValidatePresetConditionOperators(condition.Root, body);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Invalid Preset Boolean expression '" + body + "'.",
                    ex);
            }

            plan = new PresetConditionExpressionPlan(body, condition);
            return true;
        }

        private static bool LooksLikePresetConditionExpression(string body)
        {
            if (String.IsNullOrEmpty(body))
                return false;

            char quote = '\0';
            bool escaped = false;
            int i;

            for (i = 0; i < body.Length; i++)
            {
                char current = body[i];

                if (quote != '\0')
                {
                    if (escaped)
                        escaped = false;
                    else if (current == '\\')
                        escaped = true;
                    else if (current == quote)
                        quote = '\0';

                    continue;
                }

                if (current == '\'' || current == '"')
                {
                    quote = current;
                    continue;
                }

                if (current == '(' || current == ')' ||
                    current == '!' || current == '&' ||
                    current == '|' || current == '=')
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidatePresetConditionOperators(
            BindingConditionNode node,
            string expression)
        {
            if (node == null)
                return;

            if (node.Kind == BindingConditionNodeKind.LessThan ||
                node.Kind == BindingConditionNodeKind.LessThanOrEqual ||
                node.Kind == BindingConditionNodeKind.GreaterThan ||
                node.Kind == BindingConditionNodeKind.GreaterThanOrEqual)
            {
                throw new InvalidOperationException(
                    "Preset Boolean expression '" + expression +
                    "' supports ==, !=, !, &&, ||, and parentheses.");
            }

            ValidatePresetConditionOperators(node.Left, expression);
            ValidatePresetConditionOperators(node.Right, expression);
        }

        private object EvaluatePresetConditionExpression(
            PresetConditionExpressionPlan plan)
        {
            if (plan == null || plan.Condition == null)
            {
                throw new InvalidOperationException(
                    "Preset Boolean expression metadata is incomplete.");
            }

            PresetConditionValue result = EvaluatePresetConditionNode(
                plan,
                plan.Condition.Root,
                false);

            if (!result.FromPreset)
            {
                throw new InvalidOperationException(
                    "Preset Boolean expression '" + plan.Expression +
                    "' must reference at least one known preset collection.");
            }

            if (IsUnsetPresetValue(result.Value))
                return UnsetPresetValue;

            return ConvertPresetConditionBoolean(
                plan,
                plan.Condition.Root,
                result.Value);
        }

        private PresetConditionValue EvaluatePresetConditionNode(
            PresetConditionExpressionPlan plan,
            BindingConditionNode node,
            bool allowBareName)
        {
            if (node == null)
            {
                throw new InvalidOperationException(
                    "Preset Boolean expression metadata is incomplete.");
            }

            if (node.Kind == BindingConditionNodeKind.Literal)
                return new PresetConditionValue(node.Literal, false);

            if (node.Kind == BindingConditionNodeKind.Path)
            {
                return ResolvePresetConditionPath(
                    plan,
                    node,
                    plan.Condition.Paths[node.PathIndex],
                    allowBareName);
            }

            if (node.Kind == BindingConditionNodeKind.Not)
            {
                PresetConditionValue operand =
                    EvaluatePresetConditionNode(plan, node.Left, false);

                if (IsUnsetPresetValue(operand.Value))
                    return operand;

                return new PresetConditionValue(
                    !ConvertPresetConditionBoolean(
                        plan,
                        node,
                        operand.Value),
                    operand.FromPreset);
            }

            if (node.Kind == BindingConditionNodeKind.And ||
                node.Kind == BindingConditionNodeKind.Or)
            {
                PresetConditionValue leftLogical =
                    EvaluatePresetConditionNode(plan, node.Left, false);
                PresetConditionValue rightLogical =
                    EvaluatePresetConditionNode(plan, node.Right, false);

                if (IsUnsetPresetValue(leftLogical.Value) ||
                    IsUnsetPresetValue(rightLogical.Value))
                {
                    return new PresetConditionValue(
                        UnsetPresetValue,
                        leftLogical.FromPreset || rightLogical.FromPreset);
                }

                bool left = ConvertPresetConditionBoolean(
                    plan,
                    node,
                    leftLogical.Value);
                bool right = ConvertPresetConditionBoolean(
                    plan,
                    node,
                    rightLogical.Value);

                return new PresetConditionValue(
                    node.Kind == BindingConditionNodeKind.And
                        ? left && right
                        : left || right,
                    leftLogical.FromPreset || rightLogical.FromPreset);
            }

            if (node.Kind == BindingConditionNodeKind.Equal ||
                node.Kind == BindingConditionNodeKind.NotEqual)
            {
                PresetConditionValue leftValue =
                    EvaluatePresetConditionNode(plan, node.Left, false);
                PresetConditionValue rightValue =
                    EvaluatePresetConditionNode(plan, node.Right, true);
                bool fromPreset =
                    leftValue.FromPreset || rightValue.FromPreset;

                if (IsUnsetPresetValue(leftValue.Value) ||
                    IsUnsetPresetValue(rightValue.Value))
                {
                    return new PresetConditionValue(
                        UnsetPresetValue,
                        fromPreset);
                }

                if (!fromPreset)
                {
                    string unknown = FindFirstPresetConditionPath(
                        plan.Condition,
                        node);

                    throw new InvalidOperationException(
                        "Preset set '" + unknown + "' was not found while " +
                        "evaluating Preset Boolean expression '" +
                        plan.Expression + "'.");
                }

                bool equal = EvaluatePresetConditionEquality(
                    plan,
                    node,
                    leftValue.Value,
                    rightValue.Value);

                return new PresetConditionValue(
                    node.Kind == BindingConditionNodeKind.Equal
                        ? equal
                        : !equal,
                    true);
            }

            throw new InvalidOperationException(
                "Preset Boolean expression '" + plan.Expression +
                "' contains an unsupported operator.");
        }

        private PresetConditionValue ResolvePresetConditionPath(
            PresetConditionExpressionPlan plan,
            BindingConditionNode node,
            string path,
            bool allowBareName)
        {
            int separator = path.IndexOf('.');

            if (separator >= 0)
            {
                string setName = path.Substring(0, separator);
                string key = path.Substring(separator + 1);

                EnsurePresetConditionSet(setName, plan);

                object value = ResolvePresetValue(setName, key);

                if (IsUnsetPresetValue(value))
                {
                    return new PresetConditionValue(
                        UnsetPresetValue,
                        true);
                }

                return new PresetConditionValue(value, true);
            }

            if (_presetManager.Contains(path))
            {
                return new PresetConditionValue(
                    _presetManager.GetSet(path).SelectedName,
                    true);
            }

            if (allowBareName)
                return new PresetConditionValue(path, false);

            throw new InvalidOperationException(
                "Preset set '" + path + "' was not found while evaluating " +
                "Preset Boolean expression '" + plan.Expression + "'.");
        }

        private void EnsurePresetConditionSet(
            string setName,
            PresetConditionExpressionPlan plan)
        {
            if (_presetManager.Contains(setName))
                return;

            throw new InvalidOperationException(
                "Preset set '" + setName + "' was not found while evaluating " +
                "Preset Boolean expression '" + plan.Expression + "'.");
        }

        private static string FindFirstPresetConditionPath(
            BindingConditionExpressionPlan condition,
            BindingConditionNode node)
        {
            if (node == null)
                return String.Empty;

            if (node.Kind == BindingConditionNodeKind.Path)
                return condition.Paths[node.PathIndex];

            string left = FindFirstPresetConditionPath(
                condition,
                node.Left);

            if (!String.IsNullOrEmpty(left))
                return left;

            return FindFirstPresetConditionPath(
                condition,
                node.Right);
        }

        private static bool ConvertPresetConditionBoolean(
            PresetConditionExpressionPlan plan,
            BindingConditionNode node,
            object value)
        {
            object converted;

            if (TryConvertObjectValue(value, typeof(bool), out converted))
                return (bool)converted;

            throw PresetConditionTypeError(
                plan,
                node,
                "logical operators require boolean-compatible operands, but " +
                "the operand resolved to " +
                GetBindingConditionTypeName(value));
        }

        private static bool EvaluatePresetConditionEquality(
            PresetConditionExpressionPlan plan,
            BindingConditionNode node,
            object left,
            object right)
        {
            if (left == null || right == null)
                return left == null && right == null;

            if (IsBindingConditionNumeric(left) &&
                IsBindingConditionNumeric(right))
            {
                return EvaluateBindingConditionNumeric(
                    BindingConditionNodeKind.Equal,
                    left,
                    right);
            }

            string leftString = left as string;
            string rightString = right as string;

            if (leftString != null && rightString != null)
            {
                return String.Equals(
                    leftString,
                    rightString,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (left is bool && right is bool)
                return (bool)left == (bool)right;

            if (left.GetType() == right.GetType())
                return Object.Equals(left, right);

            throw PresetConditionTypeError(
                plan,
                node,
                "equality cannot compare " +
                GetBindingConditionTypeName(left) + " with " +
                GetBindingConditionTypeName(right));
        }

        private static InvalidOperationException PresetConditionTypeError(
            PresetConditionExpressionPlan plan,
            BindingConditionNode node,
            string message)
        {
            return new InvalidOperationException(
                "Preset Boolean expression '" + plan.Expression +
                "' at character " +
                (node.Position + 1).ToString(CultureInfo.InvariantCulture) +
                ": " + message + ".");
        }

        private bool PresetConditionDependsOnChange(
            PresetConditionExpressionPlan plan,
            PresetChangedEventArgs change)
        {
            if (plan == null || plan.Condition == null)
                return false;

            if (change == null || String.IsNullOrEmpty(change.SetName))
                return true;

            int i;

            for (i = 0; i < plan.Condition.Paths.Length; i++)
            {
                string path = plan.Condition.Paths[i];
                int separator = path.IndexOf('.');

                if (separator >= 0)
                {
                    string setName = path.Substring(0, separator);
                    string key = path.Substring(separator + 1);

                    if (EqualsIgnoreCase(setName, change.SetName) &&
                        (String.IsNullOrEmpty(change.Key) ||
                         EqualsIgnoreCase(key, change.Key)))
                    {
                        return true;
                    }
                }
                else if (_presetManager.Contains(path) &&
                    EqualsIgnoreCase(path, change.SetName) &&
                    String.IsNullOrEmpty(change.Key))
                {
                    return true;
                }
            }

            return false;
        }

        private void MergePresetConditionObservableDependencies(
            PresetConditionExpressionPlan plan,
            BindingPathResult aggregate,
            BindingDependencySourceIndex aggregateSources)
        {
            if (plan == null || plan.Condition == null)
                return;

            int i;

            for (i = 0; i < plan.Condition.Paths.Length; i++)
            {
                string path = plan.Condition.Paths[i];
                int separator = path.IndexOf('.');

                if (separator <= 0 || separator == path.Length - 1)
                    continue;

                BindingPathResult dependencies =
                    ResolveReactivePresetDependencies(
                        path.Substring(0, separator),
                        path.Substring(separator + 1));

                if (dependencies == null ||
                    dependencies.Dependencies.Count == 0)
                {
                    continue;
                }

                MergeBindingPathDependencies(
                    aggregate,
                    dependencies,
                    aggregateSources);
            }
        }
    }
}
