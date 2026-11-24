using System.Globalization;
using System.Text;

namespace Scip.Indexer.Clang;

/// <summary>Maps absolute paths to small ids and knows which files belong to the project.</summary>
public sealed class FileTable(string root)
{
    private readonly List<string> _paths = [];
    private readonly Dictionary<string, int> _ids = [];

    public int Count => _paths.Count;

    public int Id(string fullPath)
    {
        if (!_ids.TryGetValue(fullPath, out var id))
        {
            _ids[fullPath] = id = _paths.Count;
            _paths.Add(fullPath);
        }

        return id;
    }

    public string Path(int id) => _paths[id];

    /// <summary>Project-relative '/'-separated path, or null for files outside the project root.</summary>
    public string? Relative(int id)
    {
        var rel = System.IO.Path.GetRelativePath(root, _paths[id]);
        return rel.StartsWith("..", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(rel) ? null : rel.Replace('\\', '/');
    }
}

public sealed record Macro(string Name, List<string>? Params, bool Variadic, List<Tok> Body, int File, int Line, int Col);

public sealed record IncludeEvent(int File, int Line, int Col, int Len, int TargetFile);

/// <summary>A macro definition (<see cref="Def"/>) or use at the given token.</summary>
public sealed record MacroEvent(Tok At, Macro Macro, bool Def);

/// <summary>
/// Native C/C++ preprocessor: conditional compilation with <c>#if</c> expression evaluation, object- and function-like
/// macros (including <c>#</c>, <c>##</c> and variadics), <c>#include</c> resolution via the quote directory and
/// <c>-I</c> directories, <c>#pragma once</c>. Headers that cannot be found (system headers) are skipped.
/// </summary>
public sealed class Preprocessor(FileTable files, IReadOnlyList<string> includeDirs, IEnumerable<KeyValuePair<string, string?>> defines)
{
    private readonly Dictionary<string, Macro> _macros = [];
    private readonly HashSet<int> _once = [];
    private readonly Dictionary<string, string?> _defines = new(defines);

    public List<Tok> Output { get; } = [];

    public List<IncludeEvent> Includes { get; } = [];

    public List<MacroEvent> MacroEvents { get; } = [];

    public IReadOnlyDictionary<string, Macro> Macros => _macros;

    public void Run(string mainFile, IEnumerable<string> forcedIncludes)
    {
        foreach (var (name, value) in _defines)
        {
            var body = value is null ? [] : Lexer.Lex(Encoding.UTF8.GetBytes(value), -1);
            _macros[name] = new Macro(name, null, false, body, -1, 0, 0);
        }

        foreach (var f in forcedIncludes)
        {
            if (File.Exists(f))
            {
                Process(Path.GetFullPath(f), 0);
            }
        }

        Process(Path.GetFullPath(mainFile), 0);
    }

    private void Process(string path, int depth)
    {
        var id = files.Id(path);
        var virtualHeader = BuiltinHeaders.IsVirtual(path);
        if (depth > 100 || _once.Contains(id) || (!virtualHeader && !File.Exists(path)))
        {
            return;
        }

        var toks = Lexer.Lex(virtualHeader ? BuiltinHeaders.Read(path) : File.ReadAllBytes(path), id);
        var conds = new List<(bool ParentActive, bool Taken, bool Active)>();
        bool Active() => conds.Count == 0 || conds[^1].Active;

        var i = 0;
        while (i < toks.Count)
        {
            var t = toks[i];
            if (t is { Bol: true, Kind: TokKind.Punct, Text: "#" })
            {
                var j = i + 1;
                while (j < toks.Count && !toks[j].Bol)
                {
                    j++;
                }

                Directive(toks.GetRange(i + 1, j - i - 1), t, conds, depth, id, path);
                i = j;
                continue;
            }

            if (!Active())
            {
                i++;
                continue;
            }

            if (t.Kind == TokKind.Ident && _macros.TryGetValue(t.Text, out var m))
            {
                var consumed = TryExpand(toks, i, m, [], out var expansion);
                if (consumed > 0)
                {
                    MacroEvents.Add(new MacroEvent(t, m, false));
                    Output.AddRange(expansion);
                    i += consumed;
                    continue;
                }
            }

            Output.Add(t);
            i++;
        }
    }

    // ---- directives ---------------------------------------------------------------------------------------

    private void Directive(List<Tok> d, Tok hash, List<(bool ParentActive, bool Taken, bool Active)> conds, int depth, int fileId, string path)
    {
        if (d.Count == 0 || d[0].Kind != TokKind.Ident)
        {
            return;
        }

        var active = conds.Count == 0 || conds[^1].Active;
        switch (d[0].Text)
        {
            case "if":
            {
                var v = active && Eval(d.GetRange(1, d.Count - 1), fileId, path) != 0;
                conds.Add((active, v, active && v));
                break;
            }

            case "ifdef" or "ifndef":
            {
                var defined = d.Count > 1 && _macros.TryGetValue(d[1].Text, out var mm) && NoteUse(d[1], mm);
                var v = active && (d[0].Text == "ifdef" ? defined : !defined);
                conds.Add((active, v, v));
                break;
            }

            case "elif":
                if (conds.Count > 0)
                {
                    var (pa, taken, _) = conds[^1];
                    var v = pa && !taken && Eval(d.GetRange(1, d.Count - 1), fileId, path) != 0;
                    conds[^1] = (pa, taken || v, v);
                }

                break;
            case "else":
                if (conds.Count > 0)
                {
                    var (pa, taken, _) = conds[^1];
                    conds[^1] = (pa, true, pa && !taken);
                }

                break;
            case "endif":
                if (conds.Count > 0)
                {
                    conds.RemoveAt(conds.Count - 1);
                }

                break;
            case "define" when active && d.Count > 1:
                Define(d);
                break;
            case "undef" when active && d.Count > 1:
                _macros.Remove(d[1].Text);
                break;
            case "include" or "import" when active:
                Include(d, fileId, path, depth);
                break;
            case "pragma" when active && d.Count > 1 && d[1].Text == "once":
                _once.Add(fileId);
                break;
        }
    }

    private bool NoteUse(Tok at, Macro m)
    {
        MacroEvents.Add(new MacroEvent(at, m, false));
        return true;
    }

    private void Define(List<Tok> d)
    {
        var name = d[1];
        List<string>? ps = null;
        var variadic = false;
        var bodyStart = 2;
        if (d.Count > 2 && d[2].Text == "(" && !d[2].Space)
        {
            ps = [];
            var k = 3;
            for (; k < d.Count && d[k].Text != ")"; k++)
            {
                if (d[k].Text == "...")
                {
                    variadic = true;
                    ps.Add("__VA_ARGS__");
                }
                else if (d[k].Kind == TokKind.Ident)
                {
                    ps.Add(d[k].Text);
                    if (k + 1 < d.Count && d[k + 1].Text == "...")
                    {
                        variadic = true;
                        k++;
                    }
                }
            }

            bodyStart = k + 1;
        }

        var m = new Macro(name.Text, ps, variadic, bodyStart < d.Count ? d.GetRange(bodyStart, d.Count - bodyStart) : [], name.File, name.Line, name.Col);
        _macros[name.Text] = m;
        MacroEvents.Add(new MacroEvent(name, m, true));
    }

    private void Include(List<Tok> d, int fileId, string path, int depth)
    {
        if (d.Count < 2)
        {
            return;
        }

        string name;
        bool quoted;
        int col, len;
        var first = d[1];
        if (first.Kind == TokKind.StringLiteral && first.Text.StartsWith('"'))
        {
            name = first.Text[1..^1];
            quoted = true;
            col = first.Col + 1;
            len = Encoding.UTF8.GetByteCount(name);
        }
        else if (first.Text == "<")
        {
            var sb = new StringBuilder();
            var k = 2;
            for (; k < d.Count && d[k].Text != ">"; k++)
            {
                sb.Append(d[k].Text);
            }

            if (k >= d.Count || k == 2)
            {
                return;
            }

            name = sb.ToString();
            quoted = false;
            col = d[2].Col;
            len = d[k - 1].Col + d[k - 1].Len - d[2].Col;
        }
        else
        {
            return;
        }

        var target = ResolveInclude(name, Path.GetDirectoryName(path)!, quoted);
        if (target is null)
        {
            return;
        }

        Includes.Add(new IncludeEvent(fileId, first.Line, col, len, files.Id(target)));
        Process(target, depth + 1);
    }

    public string? ResolveInclude(string name, string includingDir, bool quoted)
    {
        IEnumerable<string> dirs = quoted ? includeDirs.Prepend(includingDir) : includeDirs;
        foreach (var dir in dirs)
        {
            var full = Path.GetFullPath(name, dir);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return BuiltinHeaders.Resolve(name);
    }

    // ---- macro expansion ----------------------------------------------------------------------------------

    /// <summary>Tries to expand the macro invoked at <paramref name="at"/>; returns the number of source tokens consumed (0 if none).</summary>
    private int TryExpand(List<Tok> src, int at, Macro m, HashSet<string> hide, out List<Tok> result)
    {
        result = [];
        if (hide.Contains(m.Name))
        {
            return 0;
        }

        var site = src[at];
        var consumed = 1;
        List<List<Tok>> args = [];
        if (m.Params is not null)
        {
            if (at + 1 >= src.Count || src[at + 1].Text != "(" || src[at + 1].Kind != TokKind.Punct)
            {
                return 0;
            }

            var depth = 0;
            var cur = new List<Tok>();
            var k = at + 1;
            for (; k < src.Count; k++)
            {
                var t = src[k];
                if (t.Kind == TokKind.Punct)
                {
                    if (t.Text is "(" or "[" or "{")
                    {
                        depth++;
                        if (depth == 1)
                        {
                            continue;
                        }
                    }
                    else if (t.Text is ")" or "]" or "}")
                    {
                        depth--;
                        if (depth == 0)
                        {
                            break;
                        }
                    }
                    else if (t.Text == "," && depth == 1 && !(m.Variadic && args.Count >= m.Params.Count - 1))
                    {
                        args.Add(cur);
                        cur = [];
                        continue;
                    }
                }

                cur.Add(t);
            }

            if (k >= src.Count)
            {
                return 0;
            }

            if (cur.Count > 0 || args.Count > 0 || m.Params.Count > 0)
            {
                args.Add(cur);
            }

            consumed = k - at + 1;
        }

        var body = Substitute(m, args, site, hide);
        var hide2 = new HashSet<string>(hide) { m.Name };
        result = ExpandList(body, hide2);
        return consumed;
    }

    private List<Tok> ExpandList(List<Tok> list, HashSet<string> hide)
    {
        var res = new List<Tok>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (t.Kind == TokKind.Ident && _macros.TryGetValue(t.Text, out var m) && !hide.Contains(t.Text))
            {
                var n = TryExpand(list, i, m, hide, out var exp);
                if (n > 0)
                {
                    res.AddRange(exp);
                    i += n - 1;
                    continue;
                }
            }

            res.Add(t);
        }

        return res;
    }

    private List<Tok> Substitute(Macro m, List<List<Tok>> args, Tok site, HashSet<string> hide)
    {
        List<Tok> Arg(string p, bool expand)
        {
            var ix = m.Params!.IndexOf(p);
            var a = ix >= 0 && ix < args.Count ? args[ix] : [];
            if (ix >= 0 && ix == m.Params.Count - 1 && m.Variadic)
            {
                a = args.Skip(ix).SelectMany(static (x, n) => n == 0 ? x : [new Tok(TokKind.Punct, ",", 0, 0, 0, 1, false, false), .. x]).ToList();
            }

            return expand ? ExpandList(a, hide) : a;
        }

        Tok Mark(Tok t) => t with { File = site.File, Line = site.Line, Col = site.Col, Len = site.Len, Macro = true, Bol = false };

        var res = new List<Tok>();
        var body = m.Body;
        for (var i = 0; i < body.Count; i++)
        {
            var t = body[i];
            var nextPaste = i + 1 < body.Count && body[i + 1].Text == "##";
            var prevPaste = res.Count > 0 && i > 0 && body[i - 1].Text == "##";
            if (t.Text == "#" && m.Params is not null && i + 1 < body.Count && m.Params.Contains(body[i + 1].Text))
            {
                var text = string.Join(' ', Arg(body[i + 1].Text, false).Select(static x => x.Text));
                res.Add(Mark(new Tok(TokKind.StringLiteral, "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"", 0, 0, 0, 0, false, true)));
                i++;
                continue;
            }

            if (t.Text == "##")
            {
                continue;
            }

            IEnumerable<Tok> piece;
            if (t.Kind == TokKind.Ident && m.Params is not null && m.Params.Contains(t.Text))
            {
                piece = Arg(t.Text, !nextPaste && !prevPaste);
            }
            else
            {
                piece = [Mark(t)];
            }

            if (prevPaste)
            {
                var pieces = piece.ToList();
                if (pieces.Count > 0 && res.Count > 0)
                {
                    var l = res[^1];
                    var joined = l.Text + pieces[0].Text;
                    var kind = char.IsDigit(joined[0]) ? TokKind.Number : char.IsLetter(joined[0]) || joined[0] == '_' ? TokKind.Ident : TokKind.Punct;
                    res[^1] = l with { Text = joined, Kind = kind };
                    res.AddRange(pieces.Skip(1));
                }
                else
                {
                    res.AddRange(pieces);
                }
            }
            else
            {
                res.AddRange(piece);
            }
        }

        return res;
    }

    // ---- #if evaluation -----------------------------------------------------------------------------------

    private long Eval(List<Tok> expr, int fileId, string path)
    {
        var pre = new List<Tok>();
        for (var i = 0; i < expr.Count; i++)
        {
            var t = expr[i];
            if (t.Kind == TokKind.Ident && t.Text == "defined")
            {
                var paren = i + 1 < expr.Count && expr[i + 1].Text == "(";
                var nameIdx = i + (paren ? 2 : 1);
                if (nameIdx < expr.Count)
                {
                    var defined = _macros.TryGetValue(expr[nameIdx].Text, out var mm);
                    if (defined)
                    {
                        NoteUse(expr[nameIdx], mm!);
                    }

                    pre.Add(t with { Kind = TokKind.Number, Text = defined ? "1" : "0" });
                    i = nameIdx + (paren ? 1 : 0);
                }

                continue;
            }

            if (t.Kind == TokKind.Ident && t.Text is "__has_include" or "__has_include_next" && i + 1 < expr.Count)
            {
                var close = expr.FindIndex(i, static x => x.Text == ")");
                var inner = close > i + 2 ? expr.GetRange(i + 2, close - i - 2) : [];
                var found = false;
                if (inner.Count > 0)
                {
                    var quoted = inner[0].Kind == TokKind.StringLiteral;
                    var name = quoted ? inner[0].Text[1..^1] : string.Concat(inner.Skip(1).TakeWhile(static x => x.Text != ">").Select(static x => x.Text));
                    found = ResolveInclude(name, Path.GetDirectoryName(path)!, quoted) is not null;
                }

                pre.Add(t with { Kind = TokKind.Number, Text = found ? "1" : "0" });
                i = close < 0 ? expr.Count : close;
                continue;
            }

            pre.Add(t);
        }

        var toks = ExpandList(pre, []);
        var p = 0;
        try
        {
            return Ternary(toks, ref p);
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException or FormatException or DivideByZeroException or OverflowException)
        {
            return 0;
        }
    }

    private static string At(List<Tok> t, int p) => p < t.Count ? t[p].Text : "";

    private static long Ternary(List<Tok> t, ref int p)
    {
        var c = Binary(t, ref p, 0);
        if (At(t, p) == "?")
        {
            p++;
            var a = Ternary(t, ref p);
            if (At(t, p) == ":")
            {
                p++;
            }

            var b = Ternary(t, ref p);
            return c != 0 ? a : b;
        }

        return c;
    }

    // Operators by precedence level; '>' is lexed singly so ">=", ">>" are detected from adjacent tokens.
    private static readonly string[][] Levels =
    [
        ["||"], ["&&"], ["|"], ["^"], ["&"], ["==", "!="], ["<", ">", "<=", ">="], ["<<", ">>"], ["+", "-"], ["*", "/", "%"],
    ];

    private static string? Op(List<Tok> t, int p, out int width)
    {
        width = 1;
        var s = At(t, p);
        if (s == ">" && p + 1 < t.Count && t[p + 1].Col == t[p].Col + 1 && t[p + 1].Line == t[p].Line)
        {
            if (t[p + 1].Text == "=")
            {
                width = 2;
                return ">=";
            }

            if (t[p + 1].Text == ">")
            {
                width = 2;
                return ">>";
            }
        }

        if (s == "<" && p + 1 < t.Count && t[p + 1].Text == "=" && t[p + 1].Col == t[p].Col + 1)
        {
            width = 2;
            return "<=";
        }

        return s;
    }

    private static long Binary(List<Tok> t, ref int p, int level)
    {
        if (level >= Levels.Length)
        {
            return Unary(t, ref p);
        }

        var l = Binary(t, ref p, level + 1);
        while (true)
        {
            var op = Op(t, p, out var w);
            if (op is null || !Levels[level].Contains(op))
            {
                return l;
            }

            // A '>' that is really the first half of '>>' / '>=' must be matched at the right level.
            p += w;
            var r = Binary(t, ref p, level + 1);
            l = op switch
            {
                "||" => l != 0 || r != 0 ? 1 : 0,
                "&&" => l != 0 && r != 0 ? 1 : 0,
                "|" => l | r,
                "^" => l ^ r,
                "&" => l & r,
                "==" => l == r ? 1 : 0,
                "!=" => l != r ? 1 : 0,
                "<" => l < r ? 1 : 0,
                ">" => l > r ? 1 : 0,
                "<=" => l <= r ? 1 : 0,
                ">=" => l >= r ? 1 : 0,
                "<<" => l << (int)r,
                ">>" => l >> (int)r,
                "+" => l + r,
                "-" => l - r,
                "*" => l * r,
                "/" => l / r,
                _ => l % r,
            };
        }
    }

    private static long Unary(List<Tok> t, ref int p)
    {
        var s = At(t, p);
        switch (s)
        {
            case "!": p++; return Unary(t, ref p) == 0 ? 1 : 0;
            case "~": p++; return ~Unary(t, ref p);
            case "-": p++; return -Unary(t, ref p);
            case "+": p++; return Unary(t, ref p);
            case "(":
            {
                p++;
                var v = Ternary(t, ref p);
                if (At(t, p) == ")")
                {
                    p++;
                }

                return v;
            }
        }

        var tok = t[p++];
        switch (tok.Kind)
        {
            case TokKind.Number:
                return ParseNumber(tok.Text);
            case TokKind.CharLiteral:
                return tok.Text.Length > 2 ? tok.Text[tok.Text.IndexOf('\'') + 1] is '\\' ? EscapeValue(tok.Text) : tok.Text[tok.Text.IndexOf('\'') + 1] : 0;
            default:
                if (tok.Text == "true")
                {
                    return 1;
                }

                // Unknown identifiers (and function-call-like uses such as __has_feature(x)) evaluate to 0.
                if (At(t, p) == "(")
                {
                    var depth = 0;
                    do
                    {
                        depth += At(t, p) == "(" ? 1 : At(t, p) == ")" ? -1 : 0;
                        p++;
                    }
                    while (depth > 0 && p < t.Count);
                }

                return 0;
        }
    }

    private static long EscapeValue(string s) => s[s.IndexOf('\\') + 1] switch { 'n' => 10, 't' => 9, 'r' => 13, '0' => 0, var c => c };

    private static long ParseNumber(string text)
    {
        var s = text.Replace("'", "").TrimEnd('u', 'U', 'l', 'L');
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.Parse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt64(s[2..], 2);
        }

        return s.Length > 1 && s[0] == '0' ? Convert.ToInt64(s, 8) : long.Parse(s, CultureInfo.InvariantCulture);
    }
}
