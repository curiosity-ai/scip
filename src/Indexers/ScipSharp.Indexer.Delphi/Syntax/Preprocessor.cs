using System.Globalization;

namespace ScipSharp.Indexer.Delphi.Syntax;

/// <summary>Compiler settings that drive conditional compilation.</summary>
/// <param name="Defines">Conditional symbols defined at the start of every compilation unit.</param>
/// <param name="CompilerVersion">Value of <c>CompilerVersion</c> (36 = Delphi 12 Athens, 15 = Delphi 7).</param>
/// <param name="PointerSize">Result of <c>SizeOf(Pointer)</c> in <c>{$IF}</c> expressions.</param>
internal sealed record CompilerSettings(IReadOnlySet<string> Defines, double CompilerVersion, int PointerSize)
{
    public static CompilerSettings Create(string platform, double compilerVersion, IEnumerable<string> extraDefines, IEnumerable<string> undefines)
    {
        var defines = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DCC", "CONDITIONALEXPRESSIONS", "NATIVECODE", "ASSEMBLER", "UNICODE", "MSWINDOWS",
            $"VER{(int)Math.Round(compilerVersion * 10)}",
        };
        if (compilerVersion < 20)
        {
            defines.Remove("UNICODE");
        }

        var pointerSize = 4;
        switch (platform.ToUpperInvariant())
        {
            case "WIN64":
                defines.UnionWith(["WIN64", "CPUX64", "CPU64BITS", "CPU64"]);
                pointerSize = 8;
                break;
            case "LINUX64":
                defines.Remove("MSWINDOWS");
                defines.UnionWith(["LINUX", "LINUX64", "POSIX", "POSIX64", "CPUX64", "CPU64BITS"]);
                pointerSize = 8;
                break;
            case "OSX64" or "OSXARM64":
                defines.Remove("MSWINDOWS");
                defines.UnionWith(["MACOS", "MACOS64", "OSX", "OSX64", "POSIX", "POSIX64", "CPU64BITS"]);
                pointerSize = 8;
                break;
            case "ANDROID" or "ANDROID64":
                defines.Remove("MSWINDOWS");
                defines.UnionWith(["ANDROID", "LINUX", "POSIX", "CPUARM"]);
                break;
            case "IOSDEVICE64":
                defines.Remove("MSWINDOWS");
                defines.UnionWith(["IOS", "MACOS", "POSIX", "CPUARM64", "CPU64BITS"]);
                pointerSize = 8;
                break;
            default:
                defines.UnionWith(["WIN32", "CPU386", "CPUX86", "CPU32BITS", "UNDERSCOREIMPORTNAME"]);
                break;
        }

        defines.UnionWith(extraDefines);
        defines.ExceptWith(undefines);
        return new CompilerSettings(defines, compilerVersion, pointerSize);
    }

    public static CompilerSettings Default { get; } = Create("Win32", 36, [], []);
}

/// <summary>
/// Applies compiler directives to a raw token stream: <c>{$DEFINE}</c>/<c>{$UNDEF}</c>, <c>{$IFDEF}</c>/<c>{$IFNDEF}</c>/
/// <c>{$IFOPT}</c>/<c>{$IF expr}</c>/<c>{$ELSEIF}</c>/<c>{$ELSE}</c>/<c>{$ENDIF}</c>/<c>{$IFEND}</c>, <c>{$I file}</c>/
/// <c>{$INCLUDE file}</c> (spliced in with the include file's own file id) and <c>{$SCOPEDENUMS}</c>. Unknown directives
/// are dropped. Inactive branches are removed.
/// </summary>
internal sealed class Preprocessor
{
    private readonly CompilerSettings _settings;
    private readonly Func<string, string, (int FileId, string Text)?> _resolveInclude;
    private readonly HashSet<string> _defines;
    private readonly Dictionary<char, bool> _switches = new()
    {
        ['B'] = false, ['H'] = true, ['I'] = true, ['J'] = false, ['M'] = false, ['O'] = true, ['Q'] = false,
        ['R'] = false, ['T'] = false, ['W'] = false, ['X'] = true, ['Z'] = false,
    };

    private readonly Stack<Frame> _stack = new();
    private readonly List<Token> _output = [];
    private readonly HashSet<int> _includeChain = [];
    private bool _scopedEnums;

    private sealed class Frame(bool parentActive, bool active)
    {
        public bool ParentActive { get; } = parentActive;

        public bool Active { get; set; } = active;

        public bool Taken { get; set; } = active;
    }

    /// <param name="resolveInclude">Maps (including file path, include name) to a file id and text, or null if not found.</param>
    public Preprocessor(CompilerSettings settings, Func<string, string, (int FileId, string Text)?> resolveInclude)
    {
        _settings = settings;
        _resolveInclude = resolveInclude;
        _defines = new HashSet<string>(settings.Defines, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Files spliced in through <c>{$I}</c>.</summary>
    public HashSet<int> IncludedFiles { get; } = [];

    private bool Active => _stack.Count == 0 || _stack.Peek().Active;

    public List<Token> Process(List<Token> raw, string path, int fileId)
    {
        _includeChain.Add(fileId);
        Run(raw, path, 0);
        var eof = raw[^1];
        _output.Add(eof);
        return _output;
    }

    private void Run(List<Token> raw, string path, int depth)
    {
        foreach (var t in raw)
        {
            if (t.IsEof)
            {
                return;
            }

            if (t.Kind == TokenKind.Directive)
            {
                Directive(t, path, depth);
                continue;
            }

            if (Active)
            {
                _output.Add(_scopedEnums ? t with { Flags = t.Flags | TokenFlags.ScopedEnums } : t);
            }
        }
    }

    private static (string Name, string Arg) Split(string directive)
    {
        var d = directive.Trim();
        var i = 0;
        while (i < d.Length && (char.IsLetterOrDigit(d[i]) || d[i] == '_'))
        {
            i++;
        }

        return (d[..i].ToUpperInvariant(), d[i..].Trim());
    }

    private static string FirstWord(string arg)
    {
        var i = 0;
        while (i < arg.Length && (char.IsLetterOrDigit(arg[i]) || arg[i] == '_'))
        {
            i++;
        }

        return arg[..i];
    }

    private void Directive(Token t, string path, int depth)
    {
        var (name, arg) = Split(t.Text);
        switch (name)
        {
            case "IFDEF":
                Push(_defines.Contains(FirstWord(arg)));
                return;
            case "IFNDEF":
                Push(!_defines.Contains(FirstWord(arg)));
                return;
            case "IFOPT":
                Push(EvalIfOpt(arg));
                return;
            case "IF":
                Push(Active && Evaluate(arg));
                return;
            case "ELSEIF":
                if (_stack.Count > 0)
                {
                    var f = _stack.Peek();
                    if (f.Taken)
                    {
                        f.Active = false;
                    }
                    else
                    {
                        f.Active = f.ParentActive && Evaluate(arg);
                        f.Taken = f.Active;
                    }
                }

                return;
            case "ELSE":
                if (_stack.Count > 0)
                {
                    var f = _stack.Peek();
                    f.Active = f.ParentActive && !f.Taken;
                    f.Taken = true;
                }

                return;
            case "ENDIF" or "IFEND":
                if (_stack.Count > 0)
                {
                    _stack.Pop();
                }

                return;
        }

        if (!Active)
        {
            return;
        }

        switch (name)
        {
            case "DEFINE":
                _defines.Add(FirstWord(arg));
                break;
            case "UNDEF":
                _defines.Remove(FirstWord(arg));
                break;
            case "SCOPEDENUMS":
                _scopedEnums = arg.StartsWith("ON", StringComparison.OrdinalIgnoreCase);
                break;
            case "I" or "INCLUDE":
                if (arg.Length > 0 && arg[0] is '+' or '-')
                {
                    _switches['I'] = arg[0] == '+';
                    break;
                }

                Include(arg, path, depth);
                break;
            default:
                // Single-letter switches such as {$R+} or {$Q-,R-}.
                foreach (var part in t.Text.Trim().Split(','))
                {
                    var p = part.Trim();
                    if (p.Length == 2 && char.IsLetter(p[0]) && p[1] is '+' or '-')
                    {
                        _switches[char.ToUpperInvariant(p[0])] = p[1] == '+';
                    }
                }

                break;
        }
    }

    private void Push(bool condition)
    {
        var parent = Active;
        _stack.Push(new Frame(parent, parent && condition));
    }

    private bool EvalIfOpt(string arg)
    {
        arg = arg.Trim();
        if (arg.Length >= 2 && char.IsLetter(arg[0]) && arg[1] is '+' or '-')
        {
            var on = _switches.TryGetValue(char.ToUpperInvariant(arg[0]), out var v) && v;
            return arg[1] == '+' ? on : !on;
        }

        return false;
    }

    private void Include(string arg, string path, int depth)
    {
        var name = arg.Trim();
        if (name.StartsWith('\''))
        {
            var end = name.IndexOf('\'', 1);
            name = end > 0 ? name[1..end] : name[1..];
        }
        else
        {
            var space = name.IndexOfAny([' ', '\t']);
            if (space > 0)
            {
                name = name[..space];
            }
        }

        if (name.Length == 0 || name.Contains('%', StringComparison.Ordinal) || depth > 16)
        {
            return;
        }

        var resolved = _resolveInclude(path, name);
        if (resolved is not { } inc || !_includeChain.Add(inc.FileId))
        {
            return;
        }

        IncludedFiles.Add(inc.FileId);
        var tokens = Lexer.Tokenize(inc.Text, inc.FileId);
        var incPath = Path.Combine(Path.GetDirectoryName(path) ?? "", name);
        Run(tokens, incPath, depth + 1);
        _includeChain.Remove(inc.FileId);
    }

    private bool Evaluate(string expression)
    {
        try
        {
            var v = new ConditionEvaluator(expression, this).Parse();
            return v.AsBool();
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal bool IsDefined(string name) => _defines.Contains(name);

    internal CompilerSettings Settings => _settings;
}

/// <summary>A value in a <c>{$IF}</c> expression.</summary>
internal readonly record struct CondValue(double Number, string? Text, bool IsBool)
{
    public static CondValue Of(bool b) => new(b ? 1 : 0, null, true);

    public static CondValue Of(double d) => new(d, null, false);

    public bool AsBool() => Text is null ? Number != 0 : Text.Length > 0;
}

/// <summary>Evaluates <c>{$IF}</c> / <c>{$ELSEIF}</c> expressions. Unknown identifiers evaluate to 0 (false).</summary>
internal sealed class ConditionEvaluator(string expression, Preprocessor pp)
{
    private readonly List<Token> _tokens = Lexer.Tokenize(expression, -1);
    private int _p;

    private Token Cur => _tokens[Math.Min(_p, _tokens.Count - 1)];

    public CondValue Parse()
    {
        var v = Or();
        return v;
    }

    private CondValue Or()
    {
        var l = And();
        while (Cur.IsKeyword("or") || Cur.IsKeyword("xor"))
        {
            var xor = Cur.IsKeyword("xor");
            _p++;
            var r = And();
            l = CondValue.Of(xor ? l.AsBool() ^ r.AsBool() : l.AsBool() || r.AsBool());
        }

        return l;
    }

    private CondValue And()
    {
        var l = Not();
        while (Cur.IsKeyword("and"))
        {
            _p++;
            var r = Not();
            l = CondValue.Of(l.AsBool() && r.AsBool());
        }

        return l;
    }

    private CondValue Not()
    {
        if (Cur.IsKeyword("not"))
        {
            _p++;
            return CondValue.Of(!Not().AsBool());
        }

        return Comparison();
    }

    private CondValue Comparison()
    {
        var l = Additive();
        if (Cur.Kind == TokenKind.Symbol && Cur.Text is "=" or "<>" or "<" or ">" or "<=" or ">=")
        {
            var op = Cur.Text;
            _p++;
            var r = Additive();
            if (l.Text is not null || r.Text is not null)
            {
                var c = string.Compare(l.Text ?? "", r.Text ?? "", StringComparison.OrdinalIgnoreCase);
                return CondValue.Of(op switch { "=" => c == 0, "<>" => c != 0, "<" => c < 0, ">" => c > 0, "<=" => c <= 0, _ => c >= 0 });
            }

            return CondValue.Of(op switch
            {
                "=" => l.Number == r.Number,
                "<>" => l.Number != r.Number,
                "<" => l.Number < r.Number,
                ">" => l.Number > r.Number,
                "<=" => l.Number <= r.Number,
                _ => l.Number >= r.Number,
            });
        }

        return l;
    }

    private CondValue Additive()
    {
        var l = Term();
        while (Cur.IsSymbol("+") || Cur.IsSymbol("-"))
        {
            var minus = Cur.Text == "-";
            _p++;
            var r = Term();
            l = CondValue.Of(minus ? l.Number - r.Number : l.Number + r.Number);
        }

        return l;
    }

    private CondValue Term()
    {
        var l = Factor();
        while (Cur.IsSymbol("*") || Cur.IsSymbol("/") || Cur.IsKeyword("div") || Cur.IsKeyword("mod"))
        {
            var op = Cur.Text.ToLowerInvariant();
            _p++;
            var r = Factor();
            l = CondValue.Of(op switch
            {
                "*" => l.Number * r.Number,
                "/" => r.Number == 0 ? 0 : l.Number / r.Number,
                "div" => r.Number == 0 ? 0 : Math.Truncate(l.Number / r.Number),
                _ => r.Number == 0 ? 0 : l.Number % r.Number,
            });
        }

        return l;
    }

    private CondValue Factor()
    {
        var t = Cur;
        if (t.IsSymbol("("))
        {
            _p++;
            var v = Or();
            if (Cur.IsSymbol(")"))
            {
                _p++;
            }

            return v;
        }

        if (t.IsSymbol("-"))
        {
            _p++;
            return CondValue.Of(-Factor().Number);
        }

        if (t.Kind == TokenKind.Number)
        {
            _p++;
            var s = t.Text.Replace("_", "", StringComparison.Ordinal);
            if (s.StartsWith('$'))
            {
                return CondValue.Of(long.TryParse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? h : 0);
            }

            return CondValue.Of(double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0);
        }

        if (t.Kind == TokenKind.String)
        {
            _p++;
            return new CondValue(0, t.Text, false);
        }

        if (t.Kind is TokenKind.Identifier or TokenKind.Keyword)
        {
            _p++;
            var name = t.Text;
            if (Cur.IsSymbol("("))
            {
                _p++;
                var argStart = _p;
                var depth = 1;
                while (!Cur.IsEof && depth > 0)
                {
                    if (Cur.IsSymbol("("))
                    {
                        depth++;
                    }
                    else if (Cur.IsSymbol(")"))
                    {
                        depth--;
                        if (depth == 0)
                        {
                            break;
                        }
                    }

                    _p++;
                }

                var argText = string.Concat(_tokens.Skip(argStart).Take(_p - argStart).Select(x => x.Text));
                _p++;
                return name.ToUpperInvariant() switch
                {
                    "DEFINED" => CondValue.Of(pp.IsDefined(argText)),
                    "DECLARED" => CondValue.Of(IsDeclared(argText)),
                    "SIZEOF" => CondValue.Of(SizeOf(argText)),
                    _ => CondValue.Of(0),
                };
            }

            return name.ToUpperInvariant() switch
            {
                "TRUE" => CondValue.Of(true),
                "FALSE" => CondValue.Of(false),
                "COMPILERVERSION" or "RTLVERSION" => CondValue.Of(pp.Settings.CompilerVersion),
                _ when name.StartsWith("RTLVersion", StringComparison.OrdinalIgnoreCase) => CondValue.Of(1),
                _ => CondValue.Of(0),
            };
        }

        _p++;
        return CondValue.Of(0);
    }

    private bool IsDeclared(string name) => name.ToUpperInvariant() switch
    {
        "RTLVERSION" or "COMPILERVERSION" => true,
        "RTLVERSION111" or "RTLVERSION112" or "RTLVERSION113" => pp.Settings.CompilerVersion >= 35,
        "RTLVERSION121" or "RTLVERSION122" => pp.Settings.CompilerVersion >= 36,
        _ => false,
    };

    private int SizeOf(string type) => type.ToUpperInvariant() switch
    {
        "POINTER" or "NATIVEINT" or "NATIVEUINT" or "PCHAR" or "TOBJECT" => pp.Settings.PointerSize,
        "CHAR" => pp.Settings.CompilerVersion >= 20 ? 2 : 1,
        "INTEGER" or "CARDINAL" or "LONGINT" or "LONGWORD" or "SINGLE" => 4,
        "INT64" or "UINT64" or "DOUBLE" => 8,
        "EXTENDED" => pp.Settings.PointerSize == 8 ? 8 : 10,
        "WORD" or "SMALLINT" or "WIDECHAR" => 2,
        "BYTE" or "SHORTINT" or "ANSICHAR" or "BOOLEAN" => 1,
        _ => 0,
    };
}
