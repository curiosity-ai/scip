using System.Globalization;

namespace ScipSharp.Indexer.Fortran.Syntax;

/// <summary>
/// Best-effort C preprocessor state for <c>.F</c>/<c>.F90</c> files. In the default mode every branch of
/// <c>#if</c>/<c>#ifdef</c> is indexed (so both alternatives of a declaration are navigable); with
/// <see cref="Evaluate"/> the conditions are evaluated against the configured macros and inactive lines are skipped.
/// Macro expansion is not performed; <c>#define</c>d names are recorded so they are never resolved as Fortran names.
/// </summary>
internal sealed class Preprocessor(bool evaluate, IReadOnlyDictionary<string, string> defines)
{
    private readonly Dictionary<string, string> _defines = new(defines, StringComparer.Ordinal);
    private readonly Stack<(bool ParentActive, bool Taken)> _stack = new();

    public bool Evaluate { get; } = evaluate;

    /// <summary>False while inside a branch that is excluded by an evaluated condition.</summary>
    public bool Active { get; private set; } = true;

    /// <summary>True when <paramref name="line"/> is a preprocessor directive line.</summary>
    public static bool IsDirective(string line)
    {
        foreach (var c in line)
        {
            if (c is ' ' or '\t')
            {
                continue;
            }

            return c == '#';
        }

        return false;
    }

    /// <summary>Processes a directive (text after '#'). Returns the path of an active <c>#include</c>, otherwise null.</summary>
    public string? Directive(string body, SourceDoc doc)
    {
        var text = body.Trim();
        var nameEnd = 0;
        while (nameEnd < text.Length && char.IsAsciiLetter(text[nameEnd]))
        {
            nameEnd++;
        }

        var name = text[..nameEnd];
        var rest = text[nameEnd..].Trim();
        switch (name)
        {
            case "ifdef":
                Push(_defines.ContainsKey(FirstWord(rest)));
                break;
            case "ifndef":
                Push(!_defines.ContainsKey(FirstWord(rest)));
                break;
            case "if":
                Push(Eval(rest) != 0);
                break;
            case "elif":
                if (_stack.Count > 0)
                {
                    var (parent, taken) = _stack.Pop();
                    var cond = !Evaluate || (!taken && Eval(rest) != 0);
                    _stack.Push((parent, taken || cond));
                    Active = parent && cond;
                }

                break;
            case "else":
                if (_stack.Count > 0)
                {
                    var (parent, taken) = _stack.Pop();
                    var cond = !Evaluate || !taken;
                    _stack.Push((parent, true));
                    Active = parent && cond;
                }

                break;
            case "endif":
                if (_stack.Count > 0)
                {
                    Active = _stack.Pop().ParentActive;
                }

                break;
            case "define":
                if (Active)
                {
                    var macro = FirstWord(rest);
                    if (macro.Length > 0)
                    {
                        _defines[macro] = rest[macro.Length..].TrimStart('(').Trim();
                        doc.Macros.Add(macro.ToLowerInvariant());
                    }
                }

                break;
            case "undef":
                if (Active)
                {
                    _defines.Remove(FirstWord(rest));
                }

                break;
            case "include":
                if (Active && rest.Length > 2 && rest[0] is '"' or '<')
                {
                    var close = rest[0] == '"' ? '"' : '>';
                    var end = rest.IndexOf(close, 1);
                    if (end > 1)
                    {
                        return rest[1..end];
                    }
                }

                break;
        }

        return null;
    }

    private void Push(bool condition)
    {
        var cond = !Evaluate || condition;
        _stack.Push((Active, cond));
        Active = Active && cond;
    }

    private static string FirstWord(string s)
    {
        var i = 0;
        while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] == '_'))
        {
            i++;
        }

        return s[..i];
    }

    // ---- #if expression evaluation (integers, defined(), ! && || comparisons, + - * /) ----

    private long Eval(string expression)
    {
        try
        {
            var p = new ExprParser(expression, _defines);
            return p.ParseOr();
        }
        catch (FormatException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
        catch (DivideByZeroException)
        {
            return 0;
        }
    }

    private sealed class ExprParser(string text, Dictionary<string, string> defines)
    {
        private int _pos;

        public long ParseOr()
        {
            var v = ParseAnd();
            while (Accept("||"))
            {
                var r = ParseAnd();
                v = v != 0 || r != 0 ? 1 : 0;
            }

            return v;
        }

        private long ParseAnd()
        {
            var v = ParseCompare();
            while (Accept("&&"))
            {
                var r = ParseCompare();
                v = v != 0 && r != 0 ? 1 : 0;
            }

            return v;
        }

        private long ParseCompare()
        {
            var v = ParseAdd();
            while (true)
            {
                if (Accept("=="))
                {
                    v = v == ParseAdd() ? 1 : 0;
                }
                else if (Accept("!="))
                {
                    v = v != ParseAdd() ? 1 : 0;
                }
                else if (Accept("<="))
                {
                    v = v <= ParseAdd() ? 1 : 0;
                }
                else if (Accept(">="))
                {
                    v = v >= ParseAdd() ? 1 : 0;
                }
                else if (Accept("<"))
                {
                    v = v < ParseAdd() ? 1 : 0;
                }
                else if (Accept(">"))
                {
                    v = v > ParseAdd() ? 1 : 0;
                }
                else
                {
                    return v;
                }
            }
        }

        private long ParseAdd()
        {
            var v = ParseMul();
            while (true)
            {
                if (Accept("+"))
                {
                    v += ParseMul();
                }
                else if (Accept("-"))
                {
                    v -= ParseMul();
                }
                else
                {
                    return v;
                }
            }
        }

        private long ParseMul()
        {
            var v = ParseUnary();
            while (true)
            {
                if (Accept("*"))
                {
                    v *= ParseUnary();
                }
                else if (Accept("/"))
                {
                    v /= ParseUnary();
                }
                else
                {
                    return v;
                }
            }
        }

        private long ParseUnary()
        {
            if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                throw new FormatException("#if expression nested too deeply"); // evaluates to 0
            }

            if (Accept("!"))
            {
                return ParseUnary() == 0 ? 1 : 0;
            }

            if (Accept("-"))
            {
                return -ParseUnary();
            }

            if (Accept("("))
            {
                var v = ParseOr();
                Accept(")");
                return v;
            }

            SkipWs();
            if (_pos < text.Length && char.IsDigit(text[_pos]))
            {
                var start = _pos;
                while (_pos < text.Length && char.IsAsciiLetterOrDigit(text[_pos]))
                {
                    _pos++;
                }

                var lit = text[start.._pos].TrimEnd('L', 'l', 'U', 'u');
                return long.Parse(lit, CultureInfo.InvariantCulture);
            }

            var word = Word();
            if (word == "defined")
            {
                var paren = Accept("(");
                var name = Word();
                if (paren)
                {
                    Accept(")");
                }

                return defines.ContainsKey(name) ? 1 : 0;
            }

            if (word.Length == 0)
            {
                _pos = text.Length;
                return 0;
            }

            return defines.TryGetValue(word, out var value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n
                : defines.ContainsKey(word) ? 1 : 0;
        }

        private string Word()
        {
            SkipWs();
            var start = _pos;
            while (_pos < text.Length && (char.IsAsciiLetterOrDigit(text[_pos]) || text[_pos] == '_'))
            {
                _pos++;
            }

            return text[start.._pos];
        }

        private bool Accept(string op)
        {
            SkipWs();
            if (string.CompareOrdinal(text, _pos, op, 0, op.Length) == 0)
            {
                _pos += op.Length;
                return true;
            }

            return false;
        }

        private void SkipWs()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }
    }
}
