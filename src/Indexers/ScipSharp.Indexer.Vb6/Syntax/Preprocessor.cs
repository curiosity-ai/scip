using System.Globalization;

namespace ScipSharp.Indexer.Vb6.Syntax;

/// <summary>
/// Conditional compilation (<c>#If</c>/<c>#ElseIf</c>/<c>#Else</c>/<c>#End If</c>, <c>#Const</c>). Evaluates the
/// directives like the VB6 compiler does: compiler constants (Win32, Win64, VBA6, VBA7, ...), project constants (.vbp
/// <c>CondComp</c>, <c>--define</c>) and module <c>#Const</c>s; unknown names are Empty (0). Lines of inactive branches and the
/// directive lines themselves are masked out, so the lexer never sees them.
/// </summary>
internal static class Preprocessor
{
    /// <summary>Compiler constants of the VB6 IDE on 32-bit Windows.</summary>
    public static IReadOnlyDictionary<string, object> DefaultConstants { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["Win16"] = 0L,
        ["Win32"] = -1L,
        ["Win64"] = 0L,
        ["VBA6"] = -1L,
        ["VBA7"] = 0L,
        ["Mac"] = 0L,
    };

    /// <summary>Returns, per line from <paramref name="startLine"/> on, whether it is active code.</summary>
    public static bool[] Evaluate(IReadOnlyList<string> lines, int startLine, IReadOnlyDictionary<string, object> constants)
    {
        var active = new bool[lines.Count];
        var consts = new Dictionary<string, object>(constants, StringComparer.OrdinalIgnoreCase);

        // Stack of (parentActive, anyBranchTaken, currentActive)
        var stack = new Stack<(bool Parent, bool Taken, bool Current)>();
        var current = true;
        for (var i = startLine; i < lines.Count; i++)
        {
            var directive = DirectiveOf(lines[i]);
            if (directive is null)
            {
                active[i] = current;
                continue;
            }

            var (keyword, rest) = directive.Value;
            switch (keyword)
            {
                case "if":
                {
                    var cond = current && Truthy(Eval(StripThen(rest), consts));
                    stack.Push((current, cond, cond));
                    current = cond;
                    break;
                }

                case "elseif":
                    if (stack.Count > 0)
                    {
                        var (parent, taken, _) = stack.Pop();
                        var cond = parent && !taken && Truthy(Eval(StripThen(rest), consts));
                        stack.Push((parent, taken || cond, cond));
                        current = cond;
                    }

                    break;
                case "else":
                    if (stack.Count > 0)
                    {
                        var (parent, taken, _) = stack.Pop();
                        var cond = parent && !taken;
                        stack.Push((parent, true, cond));
                        current = cond;
                    }

                    break;
                case "endif":
                    if (stack.Count > 0)
                    {
                        current = stack.Pop().Parent;
                    }

                    break;
                case "const":
                    if (current)
                    {
                        var eq = rest.IndexOf('=', StringComparison.Ordinal);
                        if (eq > 0)
                        {
                            consts[rest[..eq].Trim()] = Eval(rest[(eq + 1)..], consts);
                        }
                    }

                    break;
            }
        }

        return active;
    }

    /// <summary>Parses <c>A = 1 : B = 0</c> (the .vbp <c>CondComp</c> format) or <c>A=1</c> (a single <c>--define</c>).</summary>
    public static void ParseDefinitions(string text, IDictionary<string, object> into)
    {
        foreach (var part in text.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0)
            {
                into[part[..eq].Trim()] = Eval(part[(eq + 1)..], new Dictionary<string, object>());
            }
            else if (part.Length > 0)
            {
                into[part] = -1L;
            }
        }
    }

    /// <summary>Recognises a directive line; returns its normalised keyword (if/elseif/else/endif/const) and the rest.</summary>
    public static (string Keyword, string Tail)? DirectiveOf(string line)
    {
        var s = line.AsSpan().TrimStart();
        if (s.Length < 2 || s[0] != '#' || !char.IsLetter(s[1]))
        {
            return null;
        }

        s = s[1..];
        var n = 0;
        while (n < s.Length && char.IsLetter(s[n]))
        {
            n++;
        }

        var word = s[..n].ToString().ToLowerInvariant();
        var rest = StripComment(s[n..].ToString());
        switch (word)
        {
            case "if" or "elseif" or "else" or "const":
                return (word, rest);
            case "end":
                return ("endif", "");
            case "endif":
                return ("endif", "");
            default:
                return null;
        }
    }

    private static string StripComment(string s)
    {
        var inString = false;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '"')
            {
                inString = !inString;
            }
            else if (!inString && s[i] == '\'')
            {
                return s[..i];
            }
        }

        return s;
    }

    private static string StripThen(string s)
    {
        var t = s.TrimEnd();
        return t.EndsWith("then", StringComparison.OrdinalIgnoreCase) && (t.Length == 4 || !char.IsLetterOrDigit(t[^5]))
            ? t[..^4]
            : t;
    }

    public static bool Truthy(object? v) => v switch
    {
        long l => l != 0,
        double d => d != 0,
        string s => s.Length > 0 && !(double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == 0),
        _ => false,
    };

    public static object Eval(string expression, IReadOnlyDictionary<string, object> consts)
    {
        try
        {
            var p = new ExprParser(expression, consts);
            return p.Parse();
        }
        catch (FormatException)
        {
            return 0L;
        }
    }

    private sealed class ExprParser(string text, IReadOnlyDictionary<string, object> consts)
    {
        private int _pos;

        public object Parse() => Imp();

        private object Imp() => Binary(Eqv, "imp", (a, b) => ToLong(!Truthy(a) || Truthy(b)));

        private object Eqv() => Binary(Xor, "eqv", (a, b) => ToLong(Truthy(a) == Truthy(b)));

        private object Xor() => Binary(Or, "xor", (a, b) => AsLong(a) ^ AsLong(b));

        private object Or() => Binary(And, "or", (a, b) => AsLong(a) | AsLong(b));

        private object And() => Binary(Not, "and", (a, b) => AsLong(a) & AsLong(b));

        private object Binary(Func<object> next, string op, Func<object, object, object> apply)
        {
            var left = next();
            while (TryWord(op))
            {
                left = apply(left, next());
            }

            return left;
        }

        private object Not() => TryWord("not") ? ~AsLong(Not()) : Comparison();

        private object Comparison()
        {
            var left = Concat();
            while (true)
            {
                SkipWs();
                string? op = null;
                foreach (var candidate in (string[])["<>", "<=", ">=", "=<", "=>", "=", "<", ">"])
                {
                    if (string.CompareOrdinal(text, _pos, candidate, 0, candidate.Length) == 0)
                    {
                        op = candidate;
                        break;
                    }
                }

                if (op is null)
                {
                    return left;
                }

                _pos += op.Length;
                var right = Concat();
                var c = Compare(left, right);
                left = ToLong(op switch
                {
                    "<>" => c != 0,
                    "<=" or "=<" => c <= 0,
                    ">=" or "=>" => c >= 0,
                    "=" => c == 0,
                    "<" => c < 0,
                    _ => c > 0,
                });
            }
        }

        private object Concat()
        {
            var left = Additive();
            while (TryChar('&'))
            {
                left = AsString(left) + AsString(Additive());
            }

            return left;
        }

        private object Additive()
        {
            var left = Multiplicative();
            while (true)
            {
                if (TryChar('+'))
                {
                    var r = Multiplicative();
                    left = left is string ls && r is string rs ? ls + rs : AsLong(left) + AsLong(r);
                }
                else if (TryChar('-'))
                {
                    left = AsLong(left) - AsLong(Multiplicative());
                }
                else
                {
                    return left;
                }
            }
        }

        private object Multiplicative()
        {
            var left = Unary();
            while (true)
            {
                if (TryChar('*'))
                {
                    left = AsLong(left) * AsLong(Unary());
                }
                else if (TryChar('/') || TryChar('\\'))
                {
                    var r = AsLong(Unary());
                    left = r == 0 ? 0L : AsLong(left) / r;
                }
                else if (TryWord("mod"))
                {
                    var r = AsLong(Unary());
                    left = r == 0 ? 0L : AsLong(left) % r;
                }
                else
                {
                    return left;
                }
            }
        }

        private object Unary()
        {
            if (TryChar('-'))
            {
                return -AsLong(Unary());
            }

            return TryChar('+') ? Unary() : Primary();
        }

        private object Primary()
        {
            SkipWs();
            if (_pos >= text.Length)
            {
                throw new FormatException("unexpected end");
            }

            var c = text[_pos];
            if (c == '(')
            {
                _pos++;
                var v = Imp();
                TryChar(')');
                return v;
            }

            if (c == '"')
            {
                var start = ++_pos;
                while (_pos < text.Length && text[_pos] != '"')
                {
                    _pos++;
                }

                var s = text[start.._pos];
                _pos = Math.Min(_pos + 1, text.Length);
                return s;
            }

            if (c == '&' && _pos + 1 < text.Length && char.ToUpperInvariant(text[_pos + 1]) is 'H' or 'O')
            {
                var hex = char.ToUpperInvariant(text[_pos + 1]) == 'H';
                _pos += 2;
                var start = _pos;
                while (_pos < text.Length && Uri.IsHexDigit(text[_pos]))
                {
                    _pos++;
                }

                SkipSuffix();
                return Convert.ToInt64(text[start.._pos].TrimEnd('&', '%'), hex ? 16 : 8);
            }

            if (char.IsDigit(c))
            {
                var start = _pos;
                while (_pos < text.Length && (char.IsDigit(text[_pos]) || text[_pos] == '.'))
                {
                    _pos++;
                }

                var num = text[start.._pos];
                SkipSuffix();
                return num.Contains('.', StringComparison.Ordinal)
                    ? (long)double.Parse(num, CultureInfo.InvariantCulture)
                    : long.Parse(num, CultureInfo.InvariantCulture);
            }

            if (char.IsLetter(c) || c == '[')
            {
                var bracket = c == '[';
                var start = bracket ? ++_pos : _pos;
                while (_pos < text.Length && (char.IsLetterOrDigit(text[_pos]) || text[_pos] == '_'))
                {
                    _pos++;
                }

                var name = text[start.._pos];
                if (bracket)
                {
                    TryChar(']');
                }

                SkipSuffix();
                if (name.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return -1L;
                }

                if (name.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    return 0L;
                }

                return consts.TryGetValue(name, out var v) ? v : 0L;
            }

            throw new FormatException($"unexpected '{c}'");
        }

        private void SkipSuffix()
        {
            if (_pos < text.Length && text[_pos] is '%' or '&' or '!' or '#' or '@' or '$')
            {
                _pos++;
            }
        }

        private void SkipWs()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }

        private bool TryChar(char c)
        {
            SkipWs();
            if (_pos < text.Length && text[_pos] == c)
            {
                // Do not take '&' of a hex literal as concatenation.
                if (c == '&' && _pos + 1 < text.Length && char.ToUpperInvariant(text[_pos + 1]) is 'H' or 'O')
                {
                    return false;
                }

                _pos++;
                return true;
            }

            return false;
        }

        private bool TryWord(string word)
        {
            SkipWs();
            if (_pos + word.Length <= text.Length
                && string.Compare(text, _pos, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (_pos + word.Length == text.Length || !char.IsLetterOrDigit(text[_pos + word.Length])))
            {
                _pos += word.Length;
                return true;
            }

            return false;
        }

        private static long ToLong(bool b) => b ? -1L : 0L;

        private static long AsLong(object v) => v switch
        {
            long l => l,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => (long)d,
            _ => 0L,
        };

        private static string AsString(object v) => v is string s ? s : AsLong(v).ToString(CultureInfo.InvariantCulture);

        private static int Compare(object a, object b) =>
            a is string || b is string ? string.CompareOrdinal(AsString(a), AsString(b)) : AsLong(a).CompareTo(AsLong(b));
    }
}
