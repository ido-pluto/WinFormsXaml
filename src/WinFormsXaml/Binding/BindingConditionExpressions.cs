using System;
using System.Collections;
using System.Globalization;
using System.Text;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private const int BindingConditionExpressionLengthLimit = 1024;
        private const int BindingConditionExpressionTokenLimit = 256;
        private const int BindingConditionExpressionDepthLimit = 32;

        private enum BindingConditionNodeKind
        {
            Path,
            Literal,
            Not,
            And,
            Or,
            Equal,
            NotEqual,
            LessThan,
            LessThanOrEqual,
            GreaterThan,
            GreaterThanOrEqual
        }

        private sealed class BindingConditionNode
        {
            public readonly BindingConditionNodeKind Kind;
            public readonly BindingConditionNode Left;
            public readonly BindingConditionNode Right;
            public readonly int PathIndex;
            public readonly object Literal;
            public readonly int Position;

            public BindingConditionNode(
                BindingConditionNodeKind kind,
                BindingConditionNode left,
                BindingConditionNode right,
                int position)
            {
                Kind = kind;
                Left = left;
                Right = right;
                PathIndex = -1;
                Literal = null;
                Position = position;
            }

            public BindingConditionNode(
                int pathIndex,
                int position)
            {
                Kind = BindingConditionNodeKind.Path;
                Left = null;
                Right = null;
                PathIndex = pathIndex;
                Literal = null;
                Position = position;
            }

            public BindingConditionNode(
                object literal,
                int position)
            {
                Kind = BindingConditionNodeKind.Literal;
                Left = null;
                Right = null;
                PathIndex = -1;
                Literal = literal;
                Position = position;
            }
        }

        private sealed class BindingConditionExpressionPlan
        {
            public readonly string Expression;
            public readonly BindingConditionNode Root;
            public readonly string[] Paths;
            public readonly bool HasNegation;

            public BindingConditionExpressionPlan(
                string expression,
                BindingConditionNode root,
                string[] paths,
                bool hasNegation)
            {
                Expression = expression;
                Root = root;
                Paths = paths;
                HasNegation = hasNegation;
            }
        }

        private sealed class BindingConditionParser
        {
            private readonly string _expression;
            private readonly ArrayList _paths;
            private readonly Hashtable _pathIndices;
            private int _position;
            private int _tokenCount;
            private int _depth;
            private bool _hasNegation;

            public BindingConditionParser(string expression)
            {
                _expression = expression;
                _paths = new ArrayList();
                _pathIndices = new Hashtable(StringComparer.Ordinal);
            }

            public BindingConditionExpressionPlan Parse()
            {
                BindingConditionNode root = ParseOr();
                SkipWhiteSpace();

                if (_position != _expression.Length)
                {
                    char unexpected = _expression[_position];

                    if (unexpected == '=')
                    {
                        throw SyntaxError(
                            "single '=' is not supported; use '=='");
                    }

                    if (unexpected == '&')
                    {
                        throw SyntaxError(
                            "single '&' is not supported; use '&&'");
                    }

                    if (unexpected == '|')
                    {
                        throw SyntaxError(
                            "single '|' is not supported; use '||'");
                    }

                    throw SyntaxError(
                        "unexpected token '" + unexpected + "'");
                }

                string[] paths = new string[_paths.Count];
                int i;

                for (i = 0; i < _paths.Count; i++)
                    paths[i] = (string)_paths[i];

                return new BindingConditionExpressionPlan(
                    _expression,
                    root,
                    paths,
                    _hasNegation);
            }

            private BindingConditionNode ParseOr()
            {
                BindingConditionNode left = ParseAnd();

                while (TryReadOperator("||"))
                {
                    int position = _position - 2;
                    BindingConditionNode right = ParseAnd();
                    left = new BindingConditionNode(
                        BindingConditionNodeKind.Or,
                        left,
                        right,
                        position);
                }

                return left;
            }

            private BindingConditionNode ParseAnd()
            {
                BindingConditionNode left = ParseEquality();

                while (TryReadOperator("&&"))
                {
                    int position = _position - 2;
                    BindingConditionNode right = ParseEquality();
                    left = new BindingConditionNode(
                        BindingConditionNodeKind.And,
                        left,
                        right,
                        position);
                }

                return left;
            }

            private BindingConditionNode ParseEquality()
            {
                BindingConditionNode left = ParseRelational();

                while (true)
                {
                    BindingConditionNodeKind kind;
                    int position;

                    if (TryReadOperator("!=="))
                    {
                        kind = BindingConditionNodeKind.NotEqual;
                        position = _position - 3;
                    }
                    else if (TryReadOperator("==="))
                    {
                        kind = BindingConditionNodeKind.Equal;
                        position = _position - 3;
                    }
                    else if (TryReadOperator("!="))
                    {
                        kind = BindingConditionNodeKind.NotEqual;
                        position = _position - 2;
                    }
                    else if (TryReadOperator("=="))
                    {
                        kind = BindingConditionNodeKind.Equal;
                        position = _position - 2;
                    }
                    else
                    {
                        break;
                    }

                    BindingConditionNode right = ParseRelational();
                    left = new BindingConditionNode(
                        kind,
                        left,
                        right,
                        position);
                }

                return left;
            }

            private BindingConditionNode ParseRelational()
            {
                BindingConditionNode left = ParseUnary();

                while (true)
                {
                    BindingConditionNodeKind kind;
                    int position;

                    if (TryReadOperator("<="))
                    {
                        kind = BindingConditionNodeKind.LessThanOrEqual;
                        position = _position - 2;
                    }
                    else if (TryReadOperator(">="))
                    {
                        kind = BindingConditionNodeKind.GreaterThanOrEqual;
                        position = _position - 2;
                    }
                    else if (TryReadOperator("<"))
                    {
                        kind = BindingConditionNodeKind.LessThan;
                        position = _position - 1;
                    }
                    else if (TryReadOperator(">"))
                    {
                        kind = BindingConditionNodeKind.GreaterThan;
                        position = _position - 1;
                    }
                    else
                    {
                        break;
                    }

                    BindingConditionNode right = ParseUnary();
                    left = new BindingConditionNode(
                        kind,
                        left,
                        right,
                        position);
                }

                return left;
            }

            private BindingConditionNode ParseUnary()
            {
                SkipWhiteSpace();

                if (_position < _expression.Length &&
                    _expression[_position] == '!' &&
                    (_position + 1 >= _expression.Length ||
                     _expression[_position + 1] != '='))
                {
                    int position = _position;
                    _position++;
                    CountToken();
                    _hasNegation = true;
                    return new BindingConditionNode(
                        BindingConditionNodeKind.Not,
                        ParseUnary(),
                        null,
                        position);
                }

                return ParsePrimary();
            }

            private BindingConditionNode ParsePrimary()
            {
                SkipWhiteSpace();

                if (_position >= _expression.Length)
                    throw SyntaxError("expected an operand");

                int start = _position;
                char current = _expression[_position];

                if (current == '(')
                {
                    _position++;
                    CountToken();
                    _depth++;

                    if (_depth > BindingConditionExpressionDepthLimit)
                    {
                        throw SyntaxError(
                            "parenthesis nesting exceeds " +
                            BindingConditionExpressionDepthLimit.ToString(
                                CultureInfo.InvariantCulture));
                    }

                    BindingConditionNode nested = ParseOr();
                    SkipWhiteSpace();

                    if (_position >= _expression.Length ||
                        _expression[_position] != ')')
                    {
                        throw SyntaxError("expected ')'");
                    }

                    _position++;
                    CountToken();
                    _depth--;
                    return nested;
                }

                if (current == '\'' || current == '"')
                {
                    CountToken();
                    return new BindingConditionNode(
                        ParseStringLiteral(),
                        start);
                }

                if (StartsNumber())
                {
                    CountToken();
                    return new BindingConditionNode(
                        ParseNumberLiteral(),
                        start);
                }

                if (current == '.' || IsIdentifierStart(current))
                {
                    string path = ParsePath();
                    CountToken();

                    if (path.IndexOf('.') < 0)
                    {
                        if (String.Equals(
                                path,
                                "true",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return new BindingConditionNode(true, start);
                        }

                        if (String.Equals(
                                path,
                                "false",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return new BindingConditionNode(false, start);
                        }

                        if (String.Equals(
                                path,
                                "null",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return new BindingConditionNode(null, start);
                        }
                    }

                    return new BindingConditionNode(
                        GetPathIndex(path),
                        start);
                }

                throw SyntaxError("expected a path, literal, or '('");
            }

            private string ParsePath()
            {
                int start = _position;

                if (_expression[_position] == '.')
                {
                    _position++;
                    return ".";
                }

                ReadIdentifier();

                while (_position < _expression.Length &&
                       _expression[_position] == '.')
                {
                    _position++;

                    if (_position >= _expression.Length ||
                        !IsIdentifierStart(_expression[_position]))
                    {
                        throw SyntaxError(
                            "expected a member name after '.'");
                    }

                    ReadIdentifier();
                }

                return _expression.Substring(
                    start,
                    _position - start);
            }

            private void ReadIdentifier()
            {
                if (_position >= _expression.Length ||
                    !IsIdentifierStart(_expression[_position]))
                {
                    throw SyntaxError("expected an identifier");
                }

                _position++;

                while (_position < _expression.Length &&
                       IsIdentifierPart(_expression[_position]))
                {
                    _position++;
                }
            }

            private object ParseStringLiteral()
            {
                int start = _position;
                char quote = _expression[_position];
                _position++;
                StringBuilder value = new StringBuilder();

                while (_position < _expression.Length)
                {
                    char current = _expression[_position++];

                    if (current == quote)
                        return value.ToString();

                    if (current != '\\')
                    {
                        value.Append(current);
                        continue;
                    }

                    if (_position >= _expression.Length)
                    {
                        throw SyntaxErrorAt(
                            start,
                            "unterminated string escape");
                    }

                    char escaped = _expression[_position++];

                    if (escaped == '\\')
                        value.Append('\\');
                    else if (escaped == '\'' || escaped == '"')
                        value.Append(escaped);
                    else if (escaped == 'n')
                        value.Append('\n');
                    else if (escaped == 'r')
                        value.Append('\r');
                    else if (escaped == 't')
                        value.Append('\t');
                    else
                    {
                        throw SyntaxErrorAt(
                            _position - 2,
                            "unsupported string escape '\\" +
                            escaped + "'");
                    }
                }

                throw SyntaxErrorAt(start, "unterminated string literal");
            }

            private object ParseNumberLiteral()
            {
                int start = _position;

                if (_expression[_position] == '+' ||
                    _expression[_position] == '-')
                {
                    _position++;
                }

                bool digits = ReadDigits();

                if (_position < _expression.Length &&
                    _expression[_position] == '.')
                {
                    _position++;
                    digits = ReadDigits() || digits;
                }

                if (!digits)
                    throw SyntaxErrorAt(start, "invalid numeric literal");

                if (_position < _expression.Length &&
                    (_expression[_position] == 'e' ||
                     _expression[_position] == 'E'))
                {
                    _position++;

                    if (_position < _expression.Length &&
                        (_expression[_position] == '+' ||
                         _expression[_position] == '-'))
                    {
                        _position++;
                    }

                    if (!ReadDigits())
                    {
                        throw SyntaxErrorAt(
                            start,
                            "invalid numeric exponent");
                    }
                }

                string token = _expression.Substring(
                    start,
                    _position - start);
                decimal decimalValue;

                if (Decimal.TryParse(
                        token,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out decimalValue))
                {
                    return decimalValue;
                }

                double doubleValue;

                if (Double.TryParse(
                        token,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out doubleValue) &&
                    !Double.IsNaN(doubleValue) &&
                    !Double.IsInfinity(doubleValue))
                {
                    return doubleValue;
                }

                throw SyntaxErrorAt(start, "numeric literal is out of range");
            }

            private bool StartsNumber()
            {
                if (_position >= _expression.Length)
                    return false;

                char current = _expression[_position];

                if (Char.IsDigit(current))
                    return true;

                if (current == '.')
                {
                    return _position + 1 < _expression.Length &&
                        Char.IsDigit(_expression[_position + 1]);
                }

                if (current != '+' && current != '-')
                    return false;

                if (_position + 1 >= _expression.Length)
                    return false;

                char next = _expression[_position + 1];

                if (Char.IsDigit(next))
                    return true;

                return next == '.' &&
                    _position + 2 < _expression.Length &&
                    Char.IsDigit(_expression[_position + 2]);
            }

            private bool ReadDigits()
            {
                int start = _position;

                while (_position < _expression.Length &&
                       Char.IsDigit(_expression[_position]))
                {
                    _position++;
                }

                return _position != start;
            }

            private int GetPathIndex(string path)
            {
                object retained = _pathIndices[path];

                if (retained != null)
                    return (int)retained;

                int index = _paths.Count;
                _paths.Add(path);
                _pathIndices.Add(path, index);
                return index;
            }

            private bool TryReadOperator(string value)
            {
                SkipWhiteSpace();

                if (_position + value.Length > _expression.Length ||
                    String.CompareOrdinal(
                        _expression,
                        _position,
                        value,
                        0,
                        value.Length) != 0)
                {
                    return false;
                }

                _position += value.Length;
                CountToken();
                return true;
            }

            private void CountToken()
            {
                _tokenCount++;

                if (_tokenCount > BindingConditionExpressionTokenLimit)
                {
                    throw SyntaxError(
                        "token count exceeds " +
                        BindingConditionExpressionTokenLimit.ToString(
                            CultureInfo.InvariantCulture));
                }
            }

            private void SkipWhiteSpace()
            {
                while (_position < _expression.Length &&
                       Char.IsWhiteSpace(_expression[_position]))
                {
                    _position++;
                }
            }

            private InvalidOperationException SyntaxError(string message)
            {
                return SyntaxErrorAt(_position, message);
            }

            private InvalidOperationException SyntaxErrorAt(
                int position,
                string message)
            {
                return new InvalidOperationException(
                    "Invalid Binding condition expression at character " +
                    (position + 1).ToString(CultureInfo.InvariantCulture) +
                    ": " + message + ". Expression: '" +
                    _expression + "'.");
            }

            private static bool IsIdentifierStart(char value)
            {
                return value == '_' || Char.IsLetter(value);
            }

            private static bool IsIdentifierPart(char value)
            {
                return value == '_' || Char.IsLetterOrDigit(value);
            }
        }

        private static bool TryCompileBindingConditionExpression(
            string expression,
            out BindingConditionExpressionPlan plan)
        {
            plan = null;

            if (!LooksLikeBindingConditionExpression(expression))
                return false;

            if (expression.Length > BindingConditionExpressionLengthLimit)
            {
                throw new InvalidOperationException(
                    "Binding condition expression length exceeds " +
                    BindingConditionExpressionLengthLimit.ToString(
                        CultureInfo.InvariantCulture) +
                    " characters.");
            }

            plan = new BindingConditionParser(expression).Parse();
            return true;
        }

        private static bool LooksLikeBindingConditionExpression(
            string expression)
        {
            if (String.IsNullOrEmpty(expression))
                return false;

            char quote = '\0';
            bool escaped = false;
            bool sawOperandCharacter = false;
            int i;

            for (i = 0; i < expression.Length; i++)
            {
                char current = expression[i];

                if (quote != '\0')
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (current == '\'' || current == '"')
                {
                    quote = current;
                    sawOperandCharacter = true;
                    continue;
                }

                if (Char.IsWhiteSpace(current))
                    continue;

                if (current == '(' || current == ')' ||
                    current == '&' || current == '|' ||
                    current == '<' || current == '>' ||
                    current == '=')
                {
                    return true;
                }

                if (current == '!')
                {
                    if (i + 1 < expression.Length &&
                        expression[i + 1] == '=')
                    {
                        return true;
                    }

                    if (sawOperandCharacter)
                        return true;

                    continue;
                }

                sawOperandCharacter = true;
            }

            return false;
        }

        private static BindingPathResult ResolveBindingExpressionResult(
            object source,
            BindingExpressionPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");

            if (!plan.HasComputedExpression)
            {
                return ResolveBindingPathResult(
                    source,
                    plan.Path);
            }

            BindingConditionExpressionPlan condition =
                plan.ConditionExpression;
            BindingPathResult aggregate = new BindingPathResult();
            object[] pathValues = new object[condition.Paths.Length];
            int i;

            for (i = 0; i < condition.Paths.Length; i++)
            {
                string path = condition.Paths[i];
                BindingPathResult pathResult;

                try
                {
                    pathResult = ResolveBindingPathResult(source, path);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Binding condition expression '" +
                        condition.Expression +
                        "' could not resolve operand '" + path + "'.",
                        ex);
                }

                pathValues[i] = pathResult.Value;
                MergeBindingPathDependencies(
                    aggregate,
                    pathResult,
                    aggregate.DependencySourceIndex);
            }

            aggregate.Value = EvaluateBindingConditionNode(
                condition,
                condition.Root,
                pathValues);
            aggregate.ValueType = typeof(bool);
            aggregate.TerminalDependency = null;
            aggregate.HasNegation = condition.HasNegation;
            aggregate.HasComputedExpression = true;
            return aggregate;
        }

        private static object ResolveBindingExpressionValue(
            object source,
            BindingExpressionPlan plan)
        {
            return ResolveBindingExpressionResult(source, plan).Value;
        }

        private static object EvaluateBindingConditionNode(
            BindingConditionExpressionPlan plan,
            BindingConditionNode node,
            object[] pathValues)
        {
            if (node == null)
            {
                throw new InvalidOperationException(
                    "Binding condition expression metadata is incomplete.");
            }

            if (node.Kind == BindingConditionNodeKind.Path)
                return pathValues[node.PathIndex];

            if (node.Kind == BindingConditionNodeKind.Literal)
                return node.Literal;

            if (node.Kind == BindingConditionNodeKind.Not)
            {
                return !ConvertBindingConditionBoolean(
                    plan,
                    node,
                    EvaluateBindingConditionNode(
                        plan,
                        node.Left,
                        pathValues));
            }

            object left = EvaluateBindingConditionNode(
                plan,
                node.Left,
                pathValues);
            object right = EvaluateBindingConditionNode(
                plan,
                node.Right,
                pathValues);

            if (node.Kind == BindingConditionNodeKind.And)
            {
                bool leftBoolean = ConvertBindingConditionBoolean(
                    plan,
                    node,
                    left);
                bool rightBoolean = ConvertBindingConditionBoolean(
                    plan,
                    node,
                    right);
                return leftBoolean && rightBoolean;
            }

            if (node.Kind == BindingConditionNodeKind.Or)
            {
                bool leftBoolean = ConvertBindingConditionBoolean(
                    plan,
                    node,
                    left);
                bool rightBoolean = ConvertBindingConditionBoolean(
                    plan,
                    node,
                    right);
                return leftBoolean || rightBoolean;
            }

            if (node.Kind == BindingConditionNodeKind.Equal ||
                node.Kind == BindingConditionNodeKind.NotEqual)
            {
                bool equal = EvaluateBindingConditionEquality(
                    plan,
                    node,
                    left,
                    right);

                return node.Kind == BindingConditionNodeKind.Equal
                    ? equal
                    : !equal;
            }

            return EvaluateBindingConditionRelational(
                plan,
                node,
                left,
                right);
        }

        private static bool ConvertBindingConditionBoolean(
            BindingConditionExpressionPlan plan,
            BindingConditionNode node,
            object value)
        {
            object converted;

            if (TryConvertObjectValue(
                    value,
                    typeof(bool),
                    out converted))
            {
                return (bool)converted;
            }

            throw BindingConditionTypeError(
                plan,
                node,
                "logical operators require boolean-compatible operands, but " +
                "the operand resolved to " +
                GetBindingConditionTypeName(value));
        }

        private static bool EvaluateBindingConditionEquality(
            BindingConditionExpressionPlan plan,
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
                    StringComparison.Ordinal);
            }

            if (left is bool && right is bool)
                return (bool)left == (bool)right;

            if (left.GetType() == right.GetType())
                return Object.Equals(left, right);

            throw BindingConditionTypeError(
                plan,
                node,
                "equality cannot compare " +
                GetBindingConditionTypeName(left) +
                " with " + GetBindingConditionTypeName(right));
        }

        private static bool EvaluateBindingConditionRelational(
            BindingConditionExpressionPlan plan,
            BindingConditionNode node,
            object left,
            object right)
        {
            if (!IsBindingConditionNumeric(left) ||
                !IsBindingConditionNumeric(right))
            {
                throw BindingConditionTypeError(
                    plan,
                    node,
                    "relational operators require numeric operands, but got " +
                    GetBindingConditionTypeName(left) +
                    " and " + GetBindingConditionTypeName(right));
            }

            return EvaluateBindingConditionNumeric(
                node.Kind,
                left,
                right);
        }

        private static bool EvaluateBindingConditionNumeric(
            BindingConditionNodeKind kind,
            object left,
            object right)
        {
            if (left is float || left is double ||
                right is float || right is double)
            {
                double leftValue = Convert.ToDouble(
                    left,
                    CultureInfo.InvariantCulture);
                double rightValue = Convert.ToDouble(
                    right,
                    CultureInfo.InvariantCulture);

                if (kind == BindingConditionNodeKind.Equal)
                    return leftValue == rightValue;
                if (kind == BindingConditionNodeKind.LessThan)
                    return leftValue < rightValue;
                if (kind == BindingConditionNodeKind.LessThanOrEqual)
                    return leftValue <= rightValue;
                if (kind == BindingConditionNodeKind.GreaterThan)
                    return leftValue > rightValue;

                return leftValue >= rightValue;
            }

            decimal leftDecimal = Convert.ToDecimal(
                left,
                CultureInfo.InvariantCulture);
            decimal rightDecimal = Convert.ToDecimal(
                right,
                CultureInfo.InvariantCulture);

            if (kind == BindingConditionNodeKind.Equal)
                return leftDecimal == rightDecimal;
            if (kind == BindingConditionNodeKind.LessThan)
                return leftDecimal < rightDecimal;
            if (kind == BindingConditionNodeKind.LessThanOrEqual)
                return leftDecimal <= rightDecimal;
            if (kind == BindingConditionNodeKind.GreaterThan)
                return leftDecimal > rightDecimal;

            return leftDecimal >= rightDecimal;
        }

        private static bool IsBindingConditionNumeric(object value)
        {
            return value is byte || value is sbyte ||
                value is short || value is ushort ||
                value is int || value is uint ||
                value is long || value is ulong ||
                value is float || value is double ||
                value is decimal;
        }

        private static string GetBindingConditionTypeName(object value)
        {
            return value == null
                ? "null"
                : value.GetType().FullName;
        }

        private static InvalidOperationException BindingConditionTypeError(
            BindingConditionExpressionPlan plan,
            BindingConditionNode node,
            string message)
        {
            return new InvalidOperationException(
                "Binding condition expression '" + plan.Expression +
                "' at character " +
                (node.Position + 1).ToString(
                    CultureInfo.InvariantCulture) +
                ": " + message + ".");
        }

        private static string[] SplitBindingExpressionParts(
            string body,
            string completeExpression)
        {
            ArrayList parts = new ArrayList();
            int partStart = 0;
            int parenthesisDepth = 0;
            char quote = '\0';
            bool escaped = false;
            int i;

            for (i = 0; i < body.Length; i++)
            {
                char current = body[i];

                if (quote != '\0')
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (current == '\'' || current == '"')
                {
                    quote = current;
                    continue;
                }

                if (current == '(')
                {
                    parenthesisDepth++;
                    continue;
                }

                if (current == ')')
                {
                    parenthesisDepth--;

                    if (parenthesisDepth < 0)
                    {
                        throw new InvalidOperationException(
                            "Binding expression contains an unmatched ')' at " +
                            "character " +
                            (i + 1).ToString(
                                CultureInfo.InvariantCulture) +
                            ": '" + completeExpression + "'.");
                    }

                    continue;
                }

                if (current == ',' && parenthesisDepth == 0)
                {
                    parts.Add(body.Substring(
                        partStart,
                        i - partStart));
                    partStart = i + 1;
                }
            }

            if (quote != '\0')
            {
                throw new InvalidOperationException(
                    "Binding expression contains an unterminated quoted value: '" +
                    completeExpression + "'.");
            }

            if (parenthesisDepth != 0)
            {
                throw new InvalidOperationException(
                    "Binding expression contains an unmatched '(': '" +
                    completeExpression + "'.");
            }

            parts.Add(body.Substring(partStart));

            string[] result = new string[parts.Count];

            for (i = 0; i < parts.Count; i++)
                result[i] = (string)parts[i];

            return result;
        }

        private static bool TrySplitKnownBindingOption(
            string part,
            out string optionName,
            out string optionValue)
        {
            optionName = null;
            optionValue = null;

            int equals = part.IndexOf('=');

            if (equals < 0 ||
                (equals + 1 < part.Length && part[equals + 1] == '=') ||
                (equals > 0 &&
                 (part[equals - 1] == '=' ||
                  part[equals - 1] == '!' ||
                  part[equals - 1] == '<' ||
                  part[equals - 1] == '>')))
            {
                return false;
            }

            string candidate = part.Substring(0, equals).Trim();

            if (!String.Equals(
                    candidate,
                    "Path",
                    StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(
                    candidate,
                    "Mode",
                    StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(
                    candidate,
                    "Source",
                    StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(
                    candidate,
                    "UpdateSourceTrigger",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            optionName = candidate;
            optionValue = part.Substring(equals + 1).Trim();
            return true;
        }
    }
}
