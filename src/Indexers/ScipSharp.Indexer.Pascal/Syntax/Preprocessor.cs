using System.Globalization;
using System.Runtime.CompilerServices;

namespace ScipSharp.Indexer.Pascal.Syntax;

/// <summary>FPC compiler modes (<c>{$mode ...}</c> / <c>-M</c>).</summary>
internal enum PascalMode
{
    Fpc,
    ObjFpc,
    Delphi,
    Tp,
    Iso,
    ExtendedPascal,
    MacPas,
}

/// <summary>Resolves an <c>{$I name}</c> include relative to the including file.</summary>
internal delegate SourceText? IncludeResolver(string name, SourceText includer);

/// <summary>
/// Runs the lexer over a file and its includes and applies conditional compilation, producing the flat list of
/// significant tokens the parser sees. Handles <c>{$mode}</c>, <c>{$IFDEF}/{$IFNDEF}/{$IFOPT}/{$IF}/{$ELSEIF}/{$ELSE}/{$ENDIF}/{$IFEND}</c>,
/// <c>{$DEFINE}/{$UNDEF}</c> and <c>{$I}/{$INCLUDE}</c>; classifies reserved words for the active mode.
/// </summary>
internal sealed class Preprocessor
{
    private const int MaxIncludeDepth = 16;

    /// <summary>Bound on include expansions per unit: a file including another twice, 16 levels deep, is 2^16 expansions.</summary>
    private const int MaxIncludeExpansions = 10_000;

    private static readonly HashSet<string> IsoWords = new(StringComparer.Ordinal)
    {
        "and", "array", "begin", "case", "const", "div", "do", "downto", "else", "end", "file", "for", "function",
        "goto", "if", "in", "label", "mod", "nil", "not", "of", "or", "packed", "procedure", "program", "record",
        "repeat", "set", "then", "to", "type", "until", "var", "while", "with",
    };

    private static readonly HashSet<string> TpWords = new(StringComparer.Ordinal)
    {
        "asm", "constructor", "destructor", "implementation", "inherited", "interface", "object", "shl", "shr",
        "string", "unit", "uses", "xor",
    };

    private static readonly HashSet<string> ObjFpcWords = new(StringComparer.Ordinal)
    {
        "as", "class", "dispinterface", "except", "exports", "finalization", "finally", "initialization", "is",
        "library", "operator", "property", "raise", "resourcestring", "threadvar", "try",
    };

    private readonly IncludeResolver _include;
    private readonly HashSet<string> _defines;
    private readonly Dictionary<string, string> _macroValues;
    private readonly List<Token> _tokens = [];
    private readonly Stack<CondState> _conds = new();
    private int _includeDepth;
    private int _includeExpansions;

    /// <summary>Files being processed (the include stack): re-including one of them is a cycle (a compile error in FPC).</summary>
    private readonly HashSet<SourceText> _active = new(ReferenceEqualityComparer.Instance);

    private bool _nested;

    private Preprocessor(PascalMode mode, IEnumerable<string> defines, IncludeResolver include, bool modeIsExplicit)
    {
        Mode = mode;
        _nested = modeIsExplicit && mode is PascalMode.Fpc or PascalMode.ObjFpc;
        _include = include;
        _defines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _macroValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FPC_FULLVERSION"] = "30202",
            ["FPC_VERSION"] = "3",
            ["FPC_RELEASE"] = "2",
            ["FPC_PATCH"] = "2",
        };
        foreach (var d in defines)
        {
            var eq = d.IndexOf(":=", StringComparison.Ordinal);
            if (eq < 0)
            {
                eq = d.IndexOf('=', StringComparison.Ordinal);
            }

            if (eq > 0)
            {
                var name = d[..eq].Trim();
                _defines.Add(name);
                _macroValues[name] = d[(eq + (d[eq] == ':' ? 2 : 1))..].Trim();
            }
            else
            {
                _defines.Add(d.Trim());
            }
        }

        SetModeDefines();
    }

    public PascalMode Mode { get; private set; }

    /// <summary>Include files that contributed tokens.</summary>
    public List<SourceText> Includes { get; } = [];

    /// <summary>Default defines of a 64-bit Linux FPC 3.2.2.</summary>
    public static IReadOnlyList<string> DefaultDefines { get; } =
    [
        "FPC", "VER3", "VER3_2", "VER3_2_2", "FPC_FULLVERSION", "CPU64", "CPUX86_64", "CPUAMD64", "LINUX", "UNIX",
        "ENDIAN_LITTLE", "FPC_HAS_TYPE_EXTENDED", "FPC_LITTLE_ENDIAN", "HASAMIGA_NO",
    ];

    /// <summary>
    /// Preprocesses <paramref name="source"/>. Nested <c>{ }</c> comments (an FPC/OBJFPC mode feature) are honoured only
    /// when the mode was set explicitly (project/option or a <c>{$mode}</c> directive), since classic sources often
    /// contain stray braces inside comments.
    /// </summary>
    public static (List<Token> Tokens, Preprocessor State) Run(SourceText source, PascalMode mode, IEnumerable<string> defines, IncludeResolver include, bool modeIsExplicit = false)
    {
        var pp = new Preprocessor(mode, defines, include, modeIsExplicit);
        pp._active.Add(source);
        pp.Process(source);
        var eofAt = source.Text.Length;
        pp._tokens.Add(new Token(TokenKind.Eof, "", source, eofAt, eofAt));
        return (pp._tokens, pp);
    }

    public static PascalMode? ParseMode(string name) => name.Trim().ToLowerInvariant() switch
    {
        "fpc" or "default" => PascalMode.Fpc,
        "objfpc" => PascalMode.ObjFpc,
        "delphi" or "delphiunicode" => PascalMode.Delphi,
        "tp" or "turbo" => PascalMode.Tp,
        "iso" or "iso7185" => PascalMode.Iso,
        "extendedpascal" or "iso10206" => PascalMode.ExtendedPascal,
        "macpas" => PascalMode.MacPas,
        _ => null,
    };

    /// <summary>True when <paramref name="lower"/> is a reserved word in <paramref name="mode"/>.</summary>
    public static bool IsReserved(string lower, PascalMode mode) => mode switch
    {
        PascalMode.Iso or PascalMode.ExtendedPascal => IsoWords.Contains(lower),
        PascalMode.Tp or PascalMode.MacPas => IsoWords.Contains(lower) || TpWords.Contains(lower),
        _ => IsoWords.Contains(lower) || TpWords.Contains(lower) || ObjFpcWords.Contains(lower),
    };

    private bool Active => _conds.Count == 0 || _conds.Peek().Active;

    private void Process(SourceText source)
    {
        var lexer = new Lexer(source) { NestedComments = _nested };
        while (true)
        {
            var tok = lexer.Next();
            if (tok.Kind == TokenKind.Eof)
            {
                return;
            }

            if (tok.Kind == TokenKind.Directive)
            {
                Directive(tok, source);
                lexer.NestedComments = _nested;
                continue;
            }

            if (!Active)
            {
                continue;
            }

            if (tok.Kind == TokenKind.Identifier && !tok.Escaped && IsReserved(tok.Lower, Mode))
            {
                tok.Kind = TokenKind.Keyword;
            }

            _tokens.Add(tok);
        }
    }

    private void Directive(Token tok, SourceText source)
    {
        var text = tok.Text;
        var nameEnd = 0;
        while (nameEnd < text.Length && (char.IsAsciiLetterOrDigit(text[nameEnd]) || text[nameEnd] == '_'))
        {
            nameEnd++;
        }

        var name = text[..nameEnd].ToUpperInvariant();
        var arg = text[nameEnd..].Trim();
        switch (name)
        {
            case "IFDEF":
                PushCond(_defines.Contains(FirstWord(arg)));
                return;
            case "IFNDEF":
                PushCond(!_defines.Contains(FirstWord(arg)));
                return;
            case "IFOPT":
                PushCond(false);
                return;
            case "IF":
                PushCond(Truthy(new CondEval(arg, this).Evaluate()));
                return;
            case "ELSEIF":
                if (_conds.Count > 0)
                {
                    var c = _conds.Pop();
                    var take = c.ParentActive && !c.Taken && Truthy(new CondEval(arg, this).Evaluate());
                    _conds.Push(c with { Active = take, Taken = c.Taken || take });
                }

                return;
            case "ELSE":
                if (_conds.Count > 0)
                {
                    var c = _conds.Pop();
                    _conds.Push(c with { Active = c.ParentActive && !c.Taken, Taken = true });
                }

                return;
            case "ENDIF" or "IFEND" or "ENDC":
                if (_conds.Count > 0)
                {
                    _conds.Pop();
                }

                return;
        }

        if (!Active)
        {
            return;
        }

        switch (name)
        {
            case "DEFINE" or "DEFINEC" or "SETC":
            {
                var w = FirstWord(arg);
                if (w.Length > 0)
                {
                    _defines.Add(w);
                    var assign = arg.IndexOf(":=", StringComparison.Ordinal);
                    if (assign > 0)
                    {
                        _macroValues[w] = arg[(assign + 2)..].Trim();
                    }
                }

                return;
            }

            case "UNDEF":
                _defines.Remove(FirstWord(arg));
                return;
            case "MODE":
                if (ParseMode(FirstWord(arg)) is { } m)
                {
                    Mode = m;
                    _nested = m is PascalMode.Fpc or PascalMode.ObjFpc;
                    SetModeDefines();
                }

                return;
            case "I" or "INCLUDE":
                if (arg.Length == 0 || arg[0] is '+' or '-' or '%' || (name == "I" && text.Length > 1 && text[1] is '+' or '-'))
                {
                    return;
                }

                Include(arg, source);
                return;
        }
    }

    private void Include(string arg, SourceText source)
    {
        var fileName = arg.Trim();
        if (fileName.StartsWith('\''))
        {
            var close = fileName.IndexOf('\'', 1);
            fileName = close > 0 ? fileName[1..close] : fileName[1..];
        }
        else
        {
            var space = fileName.IndexOfAny([' ', '\t']);
            if (space > 0)
            {
                fileName = fileName[..space];
            }
        }

        if (fileName.Length == 0 || _includeDepth >= MaxIncludeDepth)
        {
            return;
        }

        var included = _include(fileName, source);
        if (included is null)
        {
            return;
        }

        if (_active.Contains(included) || ++_includeExpansions > MaxIncludeExpansions)
        {
            return;
        }

        if (!Includes.Contains(included))
        {
            Includes.Add(included);
        }

        _includeDepth++;
        _active.Add(included);
        try
        {
            Process(included);
        }
        finally
        {
            _active.Remove(included);
            _includeDepth--;
        }
    }

    private void SetModeDefines()
    {
        foreach (var d in (string[])["FPC_OBJFPC", "FPC_DELPHI", "FPC_TP", "FPC_ISO", "FPC_MACPAS"])
        {
            _defines.Remove(d);
        }

        _defines.Add(Mode switch
        {
            PascalMode.ObjFpc => "FPC_OBJFPC",
            PascalMode.Delphi => "FPC_DELPHI",
            PascalMode.Tp => "FPC_TP",
            PascalMode.Iso or PascalMode.ExtendedPascal => "FPC_ISO",
            PascalMode.MacPas => "FPC_MACPAS",
            _ => "FPC_FPC",
        });
    }

    private void PushCond(bool value)
    {
        var parent = Active;
        _conds.Push(new CondState(parent && value, parent && value, parent));
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

    private static bool Truthy(object? v) => v switch
    {
        bool b => b,
        long l => l != 0,
        string s => s.Length > 0 && !s.Equals("false", StringComparison.OrdinalIgnoreCase) && s != "0",
        _ => false,
    };

    private readonly record struct CondState(bool Active, bool Taken, bool ParentActive);

    /// <summary>Evaluator for <c>{$IF}</c> expressions: defined(), declared() (always false), numbers, comparisons, and/or/not.</summary>
    private sealed class CondEval(string text, Preprocessor pp)
    {
        private int _pos;

        public object? Evaluate()
        {
            try
            {
                return Or();
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private object? Or()
        {
            var left = And();
            while (true)
            {
                if (Word("or"))
                {
                    var r = And();
                    left = Truthy(left) || Truthy(r);
                }
                else if (Word("xor"))
                {
                    var r = And();
                    left = Truthy(left) ^ Truthy(r);
                }
                else
                {
                    return left;
                }
            }
        }

        private object? And()
        {
            var left = Rel();
            while (Word("and"))
            {
                var r = Rel();
                left = Truthy(left) && Truthy(r);
            }

            return left;
        }

        private object? Rel()
        {
            var left = Add();
            Skip();
            foreach (var op in (string[])["<>", "<=", ">=", "=", "<", ">"])
            {
                if (string.CompareOrdinal(text, _pos, op, 0, op.Length) == 0)
                {
                    _pos += op.Length;
                    var right = Add();
                    if (left is long a && right is long b)
                    {
                        return op switch
                        {
                            "<>" => a != b,
                            "<=" => a <= b,
                            ">=" => a >= b,
                            "=" => a == b,
                            "<" => a < b,
                            _ => a > b,
                        };
                    }

                    var eq = string.Equals(left?.ToString(), right?.ToString(), StringComparison.OrdinalIgnoreCase);
                    return op switch
                    {
                        "=" => eq,
                        "<>" => !eq,
                        _ => false,
                    };
                }
            }

            return left;
        }

        private object? Add()
        {
            var left = Unary();
            while (true)
            {
                Skip();
                if (_pos < text.Length && text[_pos] is '+' or '-')
                {
                    var plus = text[_pos] == '+';
                    _pos++;
                    var r = Unary();
                    left = left is long a && r is long b ? (plus ? a + b : a - b) : 0L;
                }
                else
                {
                    return left;
                }
            }
        }

        private object? Unary()
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                throw new FormatException("expression nested too deeply"); // evaluates to false
            }

            if (Word("not"))
            {
                return !Truthy(Unary());
            }

            Skip();
            if (_pos >= text.Length)
            {
                return false;
            }

            var c = text[_pos];
            if (c == '(')
            {
                _pos++;
                var v = Or();
                Skip();
                if (_pos < text.Length && text[_pos] == ')')
                {
                    _pos++;
                }

                return v;
            }

            if (c == '-')
            {
                _pos++;
                return Unary() is long l ? -l : 0L;
            }

            if (char.IsAsciiDigit(c))
            {
                var start = _pos;
                while (_pos < text.Length && char.IsAsciiDigit(text[_pos]))
                {
                    _pos++;
                }

                return long.Parse(text.AsSpan(start, _pos - start), CultureInfo.InvariantCulture);
            }

            if (c == '\'')
            {
                var end = text.IndexOf('\'', _pos + 1);
                end = end < 0 ? text.Length : end;
                var s = text[(_pos + 1)..end];
                _pos = Math.Min(text.Length, end + 1);
                return s;
            }

            if (char.IsAsciiLetter(c) || c == '_')
            {
                var start = _pos;
                while (_pos < text.Length && (char.IsAsciiLetterOrDigit(text[_pos]) || text[_pos] is '_' or '.'))
                {
                    _pos++;
                }

                var w = text[start.._pos];
                var lower = w.ToLowerInvariant();
                if (lower is "defined" or "declared" or "undefined" or "option" or "sizeof" or "high" or "low")
                {
                    var inner = ParenArgument();
                    return lower switch
                    {
                        "defined" => pp._defines.Contains(inner),
                        "undefined" => !pp._defines.Contains(inner),
                        "sizeof" => 8L,
                        _ => false,
                    };
                }

                if (lower == "true")
                {
                    return true;
                }

                if (lower == "false")
                {
                    return false;
                }

                if (pp._macroValues.TryGetValue(w, out var val))
                {
                    return long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : val;
                }

                return pp._defines.Contains(w) ? (object)1L : 0L;
            }

            _pos++;
            return false;
        }

        private string ParenArgument()
        {
            Skip();
            if (_pos >= text.Length || text[_pos] != '(')
            {
                return "";
            }

            var close = text.IndexOf(')', _pos);
            close = close < 0 ? text.Length : close;
            var inner = text[(_pos + 1)..close].Trim();
            _pos = Math.Min(text.Length, close + 1);
            return inner;
        }

        private bool Word(string w)
        {
            Skip();
            if (_pos + w.Length <= text.Length
                && string.Compare(text, _pos, w, 0, w.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (_pos + w.Length == text.Length || !char.IsAsciiLetterOrDigit(text[_pos + w.Length])))
            {
                _pos += w.Length;
                return true;
            }

            return false;
        }

        private void Skip()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }
    }
}
