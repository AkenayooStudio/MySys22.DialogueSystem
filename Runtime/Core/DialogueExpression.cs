using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MySys22.DialogueEngine.Core
{

    public static class DialogueExpression
    {
        private static readonly string[] Keywords = { "true", "false", "and", "or", "not" };

        public static bool Evaluate(string expression, IVariableSource variables,
            out List<string> unknownVariables, out string error)
        {
            unknownVariables = new List<string>();
            error = null;

            if (string.IsNullOrWhiteSpace(expression)) return true;

            try
            {
                var parser = new Parser(expression, variables, unknownVariables);
                bool result = parser.ParseExpression();
                parser.ExpectEnd();
                return result;
            }
            catch (ExpressionException ex)
            {
                error = ex.Message;
                DialogueLogger.LogError("324", "Condition evaluation failed", $"{expression}: {ex.Message}");
                return false;
            }
        }

        public static bool Evaluate(string expression, IVariableSource variables)
            => Evaluate(expression, variables, out _, out _);

        public static bool TryValidate(string expression, out string error, out List<string> variables)
        {
            variables = new List<string>();
            error = null;

            if (string.IsNullOrWhiteSpace(expression)) return true;

            try
            {
                var parser = new Parser(expression, null, variables, syntaxOnly: true);
                parser.ParseExpression();
                parser.ExpectEnd();
                return true;
            }
            catch (ExpressionException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static List<string> CollectVariables(string expression)
        {
            var found = new List<string>();
            if (string.IsNullOrWhiteSpace(expression)) return found;

            try
            {
                var parser = new Parser(expression, null, found, syntaxOnly: true);
                parser.ParseExpression();
            }
            catch (ExpressionException)
            {

            }
            return found;
        }

        public static bool IsKeyword(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return false;
            foreach (string keyword in Keywords)
                if (string.Equals(keyword, identifier, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private enum TokenKind { Value, Identifier, Operator, OpenParen, CloseParen, End }

        private readonly struct Token
        {
            public readonly TokenKind Kind;
            public readonly string Text;
            public readonly DialogueValue Value;

            public Token(TokenKind kind, string text, DialogueValue value)
            {
                Kind = kind; Text = text; Value = value;
            }
        }

        private sealed class ExpressionException : Exception
        {
            public ExpressionException(string message) : base(message) { }
        }

        private sealed class Parser
        {
            private readonly List<Token> _tokens;
            private readonly IVariableSource _variables;
            private readonly List<string> _unknown;
            private readonly bool _syntaxOnly;
            private int _index;

            public Parser(string expression, IVariableSource variables, List<string> unknown,
                bool syntaxOnly = false)
            {
                _variables = variables;
                _unknown = unknown ?? new List<string>();
                _syntaxOnly = syntaxOnly;
                _tokens = Tokenize(expression);
            }

            public void ExpectEnd()
            {
                if (Peek().Kind != TokenKind.End)
                    throw new ExpressionException($"unexpected '{Peek().Text}'");
            }

            public bool ParseExpression() => ParseOr();

            private bool ParseOr()
            {
                bool left = ParseAnd();
                while (IsOperator("||") || IsOperator("or"))
                {
                    Next();
                    bool right = ParseAnd();
                    left = left || right;
                }
                return left;
            }

            private bool ParseAnd()
            {
                bool left = ParseEquality();
                while (IsOperator("&&") || IsOperator("and"))
                {
                    Next();
                    bool right = ParseEquality();
                    left = left && right;
                }
                return left;
            }

            private bool ParseEquality()
            {
                DialogueValue left = ParseComparison();
                while (IsOperator("==") || IsOperator("!="))
                {
                    string op = Next().Text;
                    DialogueValue right = ParseComparison();
                    bool equal = left.CompareTo(right) == 0;
                    left = DialogueValue.FromBool(op == "==" ? equal : !equal);
                }
                return left.AsBool;
            }

            private DialogueValue ParseComparison()
            {
                DialogueValue left = ParseUnary();
                while (IsOperator(">=") || IsOperator("<=") || IsOperator(">") || IsOperator("<"))
                {
                    string op = Next().Text;
                    DialogueValue right = ParseUnary();
                    int cmp = left.CompareTo(right);
                    bool result = op switch
                    {
                        ">=" => cmp >= 0,
                        "<=" => cmp <= 0,
                        ">" => cmp > 0,
                        _ => cmp < 0
                    };
                    left = DialogueValue.FromBool(result);
                }
                return left;
            }

            private DialogueValue ParseUnary()
            {
                if (IsOperator("!"))
                {
                    Next();
                    return DialogueValue.FromBool(!ParseUnary().AsBool);
                }
                if (IsOperator("-"))
                {
                    Next();
                    DialogueValue value = ParseUnary();
                    return value.Type == DialogueValueType.Float
                        ? DialogueValue.FromFloat(-value.AsFloat)
                        : DialogueValue.FromInt(-value.AsInt);
                }
                return ParsePrimary();
            }

            private DialogueValue ParsePrimary()
            {
                Token token = Next();

                switch (token.Kind)
                {
                    case TokenKind.Value:
                        return token.Value;

                    case TokenKind.OpenParen:
                    {
                        bool inner = ParseExpression();
                        if (Next().Kind != TokenKind.CloseParen)
                            throw new ExpressionException("missing ')'");
                        return DialogueValue.FromBool(inner);
                    }

                    case TokenKind.Identifier:
                        if (_unknown != null && !DialogueExpression.IsKeyword(token.Text) &&
                            !_unknown.Contains(token.Text))
                        {
                            _unknown.Add(token.Text);
                        }
                        if (_syntaxOnly) return DialogueValue.FromBool(false);
                        if (_variables != null && _variables.TryGetValue(token.Text, out DialogueValue found))
                            return found;
                        return DialogueValue.FromBool(false);

                    default:
                        throw new ExpressionException(
                            token.Kind == TokenKind.End ? "unexpected end of expression" : $"unexpected '{token.Text}'");
                }
            }

            private Token Peek() => _tokens[_index];

            private Token Next() => _tokens[_index++];

            private bool IsOperator(string text)
            {
                Token token = Peek();
                if (token.Kind != TokenKind.Operator)
                {

                    return (text == "and" || text == "or") &&
                           token.Kind == TokenKind.Identifier &&
                           string.Equals(token.Text, text, StringComparison.OrdinalIgnoreCase);
                }
                return token.Text == text;
            }
        }

        private static List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            int i = 0;

            while (i < expression.Length)
            {
                char c = expression[i];

                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '(') { tokens.Add(new Token(TokenKind.OpenParen, "(", default)); i++; continue; }
                if (c == ')') { tokens.Add(new Token(TokenKind.CloseParen, ")", default)); i++; continue; }

                if (c == '&' || c == '|')
                {
                    if (i + 1 < expression.Length && expression[i + 1] == c)
                    {
                        tokens.Add(new Token(TokenKind.Operator, new string(c, 2), default));
                        i += 2;
                        continue;
                    }
                    throw new ExpressionException($"single '{c}' is not valid, use '{c}{c}'");
                }

                if (c == '=' || c == '!' || c == '<' || c == '>' || c == '-')
                {
                    if (c != '-' && i + 1 < expression.Length && expression[i + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.Operator, expression.Substring(i, 2), default));
                        i += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Operator, c.ToString(), default));
                        i++;
                    }
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    int start = ++i;
                    while (i < expression.Length && expression[i] != quote) i++;
                    if (i >= expression.Length) throw new ExpressionException("unterminated string literal");
                    string text = expression.Substring(start, i - start);
                    tokens.Add(new Token(TokenKind.Value, text, DialogueValue.FromString(text)));
                    i++;
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < expression.Length && char.IsDigit(expression[i + 1])))
                {
                    int start = i;
                    bool isFloat = false;
                    while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
                    {
                        if (expression[i] == '.') isFloat = true;
                        i++;
                    }
                    string number = expression.Substring(start, i - start);
                    DialogueValue value = isFloat
                        ? DialogueValue.FromFloat(float.Parse(number, CultureInfo.InvariantCulture))
                        : DialogueValue.FromInt(int.Parse(number, CultureInfo.InvariantCulture));
                    tokens.Add(new Token(TokenKind.Value, number, value));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < expression.Length &&
                           (char.IsLetterOrDigit(expression[i]) || expression[i] == '_' || expression[i] == '.'))
                    {
                        i++;
                    }

                    string identifier = expression.Substring(start, i - start);
                    if (string.Equals(identifier, "true", StringComparison.OrdinalIgnoreCase))
                        tokens.Add(new Token(TokenKind.Value, identifier, DialogueValue.FromBool(true)));
                    else if (string.Equals(identifier, "false", StringComparison.OrdinalIgnoreCase))
                        tokens.Add(new Token(TokenKind.Value, identifier, DialogueValue.FromBool(false)));
                    else
                        tokens.Add(new Token(TokenKind.Identifier, identifier, default));
                    continue;
                }

                throw new ExpressionException($"unexpected character '{c}'");
            }

            tokens.Add(new Token(TokenKind.End, "", default));
            return tokens;
        }

        public static string Describe(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return "(always)";
            var names = CollectVariables(expression);
            if (names.Count == 0) return expression.Trim();
            var sb = new StringBuilder(expression.Trim());
            sb.Append("  [").Append(string.Join(", ", names)).Append(']');
            return sb.ToString();
        }
    }
}
