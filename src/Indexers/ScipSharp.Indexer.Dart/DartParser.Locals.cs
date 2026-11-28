namespace ScipSharp.Indexer.Dart;

/// <summary>
/// Local variable, closure parameter and catch parameter detection. This is a token-level heuristic (there is no
/// full expression grammar): declarations are recognised by shape (<c>var x =</c>, <c>Type x;</c>, <c>(a, b) =></c>,
/// <c>catch (e)</c>, <c>for (final x in</c>) and scoped to the enclosing block.
/// </summary>
internal sealed partial class DartParser
{
    private static readonly HashSet<string> NotAType =
    [
        "return", "case", "else", "await", "throw", "in", "is", "as", "new", "yield", "assert", "do", "default", "if",
        "while", "for", "var", "final", "const", "late", "static", "required", "covariant", "break", "continue", "try",
        "catch", "finally", "switch", "this", "super", "null", "true", "false", "when", "async", "sync", "rethrow",
        "with", "extends", "implements", "on", "get", "set", "import", "export", "part", "of", "show", "hide", "typedef",
        "class", "enum", "mixin", "extension", "operator", "factory", "external", "abstract", "interface", "base",
    ];

    private int[] _match = [];

    /// <summary>Per token: the greatest index of a matched opener before it that is still open there, or -1.</summary>
    private int[] _innermostOpen = [];
    private Dictionary<int, int> _indexByOffset = [];

    private void ScanLocals()
    {
        BuildMatches();
        _indexByOffset = new Dictionary<int, int>(_t.Count);
        for (var i = 0; i < _t.Count; i++)
        {
            _indexByOffset.TryAdd(_t[i].Offset, i);
        }

        foreach (var d in _m.AllDecls)
        {
            d.RangeStart = d.EnclosingStart;
            if (d.Doc is not null && _indexByOffset.TryGetValue(d.EnclosingStart, out var ti) && _t[ti].DocIndex is >= 0 and var di && di < _m.DocStarts.Count)
            {
                d.RangeStart = Math.Min(d.EnclosingStart, _m.DocStarts[di]);
            }
        }

        // Parameters of declarations are visible in the whole declaration.
        foreach (var d in _m.AllDecls.Concat(_m.SyntheticParams).ToList())
        {
            if (d is { Kind: DeclKind.Parameter, Parent: { } owner })
            {
                _m.AddScope(d, owner.EnclosingStart, owner.EnclosingEnd);
            }
        }

        var callables = _m.AllDecls.Where(c => c.Kind is DeclKind.Method or DeclKind.Function or DeclKind.Constructor or DeclKind.Property).ToList();
        foreach (var lf in _m.AllDecls.Where(c => c.Kind == DeclKind.Function && c.Parent is null && !_m.IsTopLevel(c)).ToList())
        {
            var outer = callables
                .Where(c => c != lf && c.EnclosingStart <= lf.EnclosingStart && lf.EnclosingEnd <= c.EnclosingEnd)
                .OrderBy(c => c.EnclosingEnd - c.EnclosingStart)
                .FirstOrDefault();
            if (outer is not null)
            {
                _m.AddScope(lf, lf.EnclosingStart, outer.EnclosingEnd);
            }
        }

        foreach (var d in _m.AllDecls.ToList())
        {
            if (d.Kind is not (DeclKind.Method or DeclKind.Function or DeclKind.Constructor or DeclKind.Property or DeclKind.Variable or DeclKind.Field))
            {
                continue;
            }

            if (!_indexByOffset.TryGetValue(d.NameToken.Offset, out var nameIdx))
            {
                continue;
            }

            var start = nameIdx + 1;
            if (d.Kind is DeclKind.Method or DeclKind.Function or DeclKind.Constructor)
            {
                if (d.Name.Length > 0 && !char.IsLetter(d.Name[0]) && d.Name[0] != '_' && d.Name[0] != '$')
                {
                    while (start < _t.Count && !_t[start].Is("("))
                    {
                        start++;
                    }
                }

                if (At(start).Is("<"))
                {
                    start = SkipAngle(start);
                }

                if (At(start).Is("(") && _match[start] > 0)
                {
                    start = _match[start] + 1;
                }
            }

            var end = LowerBound(d.EnclosingEnd);
            if (start < end)
            {
                ScanBody(start, end, d.EnclosingEnd);
            }
        }
    }

    private int LowerBound(int offset)
    {
        int lo = 0, hi = _t.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_t[mid].Offset < offset)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private void BuildMatches()
    {
        _match = new int[_t.Count];
        Array.Fill(_match, -1);
        var stacks = new Dictionary<char, Stack<int>> { ['('] = [], ['['] = [], ['{'] = [] };
        for (var i = 0; i < _t.Count; i++)
        {
            var tk = _t[i];
            if (tk.Kind != TokenKind.Punctuation || tk.Length != 1)
            {
                continue;
            }

            var c = tk.Text[0];
            if (c is '(' or '[' or '{')
            {
                stacks[c].Push(i);
            }
            else if (c is ')' or ']' or '}')
            {
                var open = c == ')' ? '(' : c == ']' ? '[' : '{';
                if (stacks[open].Count > 0)
                {
                    var o = stacks[open].Pop();
                    _match[o] = i;
                    _match[i] = o;
                }
            }
        }

        // Openers are pushed in index order, so the top (after dropping closed ones) is the greatest still-open index.
        _innermostOpen = new int[_t.Count];
        var openers = new Stack<int>();
        for (var i = 0; i < _t.Count; i++)
        {
            while (openers.Count > 0 && _match[openers.Peek()] <= i)
            {
                openers.Pop();
            }

            _innermostOpen[i] = openers.Count > 0 ? openers.Peek() : -1;
            if (_match[i] > i && _t[i].Kind == TokenKind.Punctuation && _t[i].Text is "(" or "[" or "{")
            {
                openers.Push(i);
            }
        }
    }

    private static bool IsPlainName(Token tk) => tk.Kind == TokenKind.Identifier && !NotAType.Contains(tk.Text) && tk.Text != "void" && tk.Text != "Function";

    private void ScanBody(int from, int to, int fallbackEnd)
    {
        var blocks = new Stack<int>();
        for (var k = from; k < to && k < _t.Count; k++)
        {
            var tk = _t[k];
            if (tk.Is("{"))
            {
                blocks.Push(k);
                continue;
            }

            if (tk.Is("}"))
            {
                if (blocks.Count > 0)
                {
                    blocks.Pop();
                }

                continue;
            }

            int ScopeEnd() => blocks.Count > 0 && _match[blocks.Peek()] > 0 ? _t[_match[blocks.Peek()]].End : fallbackEnd;

            if (tk.Is("catch") && At(k + 1).Is("(") && _match[k + 1] > 0 && At(_match[k + 1] + 1).Is("{") && _match[_match[k + 1] + 1] > 0)
            {
                var blockEnd = _t[_match[_match[k + 1] + 1]].End;
                for (var q = k + 2; q < _match[k + 1]; q++)
                {
                    if (IsPlainName(_t[q]) && (At(q + 1).Is(",") || q + 1 == _match[k + 1]) && !_m.DeclTokenOffsets.Contains(_t[q].Offset)
                        && !At(q - 1).Is("."))
                    {
                        AddLocal(DeclKind.LocalVariable, q, _t[q].Offset, _t[q].End, blockEnd, null, synthetic: true);
                    }
                }

                continue;
            }

            if (tk.Is("(") && _match[k] > 0 && IsClosureStart(k) && !IsSwitchArm(blocks, k))
            {
                ClosureParams(k);
                continue;
            }

            if (tk.Kind != TokenKind.Identifier || !IsPlainName(tk) || _m.DeclTokenOffsets.Contains(tk.Offset))
            {
                continue;
            }

            var prev = At(k - 1);
            var next = At(k + 1);
            if (prev.Is(".") || prev.Is("?.") || prev.Is(".."))
            {
                continue;
            }

            string? typeName = null;
            var declStart = tk.Offset;
            var isDecl = false;
            var forIn = false;
            var typeTokIdx = -1;
            if ((prev.Is("var") || prev.Is("final") || prev.Is("const")) && (next.Is("=") || next.Is(";") || next.Is(",") || next.Is("in") || next.Is(")")))
            {
                isDecl = true;
                var q = k - 1;
                // `final Type name` is handled below; `final name` / `var name` start at the modifier
                declStart = prev.Offset;
                forIn = next.Is("in");
                if (next.Is("=") && !At(k + 2).Is("="))
                {
                    typeName = InferType(k + 2);
                }

                _ = q;
            }
            else if ((next.Is("=") && !At(k + 2).Is("=")) || next.Is(";") || next.Is("in"))
            {
                if (IsTypeEnd(k - 1))
                {
                    isDecl = true;
                    forIn = next.Is("in");
                    typeTokIdx = TypeStart(k - 1);
                    declStart = typeTokIdx >= 0 ? _t[typeTokIdx].Offset : tk.Offset;
                    // include a leading `final`/`const`/`late` for for-in declared identifiers
                    typeName = TypeNameBefore(_t, k);
                }
            }

            if (!isDecl)
            {
                continue;
            }

            var scopeEnd = ScopeEnd();
            var declEnd = tk.End;
            if (!forIn && next.Is("="))
            {
                declEnd = At(ExprEnd(k + 2) - 1).End;
            }

            var d = AddLocal(DeclKind.LocalVariable, k, forIn ? declStart : tk.Offset, declEnd, scopeEnd, typeName);
            if (d is not null && typeName is null && !forIn && next.Is("="))
            {
                d.InitIndex = k + 2;
            }
            if (forIn && d is not null)
            {
                d.EnclosingStart = declStart;
            }

            // additional declarators: `var a = 1, b = 2;`
            if (!forIn && (next.Is("=") || next.Is(",")))
            {
                var p = next.Is("=") ? ExprEnd(k + 2) : k + 1;
                while (At(p).Is(",") && IsPlainName(At(p + 1)) && (At(p + 2).Is("=") || At(p + 2).Is(";") || At(p + 2).Is(",")) && !At(p + 2).Is("==") )
                {
                    var nk = p + 1;
                    var end2 = At(nk + 1).Is("=") ? At(ExprEnd(nk + 2) - 1).End : _t[nk].End;
                    string? tn = At(nk + 1).Is("=") ? InferType(nk + 2) : typeName;
                    var d2 = AddLocal(DeclKind.LocalVariable, nk, _t[nk].Offset, end2, scopeEnd, tn);
                    if (d2 is not null && tn is null && At(nk + 1).Is("="))
                    {
                        d2.InitIndex = nk + 2;
                    }

                    p = At(nk + 1).Is("=") ? ExprEnd(nk + 2) : nk + 1;
                }
            }
        }
    }

    private Decl? AddLocal(DeclKind kind, int tokIdx, int declStart, int declEnd, int scopeEnd, string? typeName, bool synthetic = false)
    {
        var tk = _t[tokIdx];
        if (!_m.DeclTokenOffsets.Add(tk.Offset))
        {
            return null;
        }

        var d = new Decl(kind, tk.Text, null, tk, _m.RelativePath)
        {
            Path = [(tk.Text, SymbolDescriptor.Types.Suffix.Term)],
            IsLocal = true,
            EnclosingStart = declStart,
            EnclosingEnd = declEnd,
            TypeName = typeName,
            IsSynthetic = synthetic,
        };
        d.Signature = typeName is null ? tk.Text : typeName + " " + tk.Text;
        if (!synthetic)
        {
            _m.AllDecls.Add(d);
        }

        _m.AddScope(d, tk.End, scopeEnd);
        return d;
    }

    /// <summary>True when the token at <paramref name="i"/> can end a type (<c>Foo</c>, <c>Foo&lt;T&gt;</c>, <c>Foo?</c>).</summary>
    private bool IsTypeEnd(int i)
    {
        var tk = At(i);
        if (tk.Kind == TokenKind.Identifier)
        {
            return IsPlainName(tk) || tk.Text == "void" || tk.Text == "dynamic";
        }

        if (tk.Is(">"))
        {
            return true;
        }

        // `Foo? x` but not the `??` operator
        return tk.Is("?") && !At(i - 1).Is("?") && (IsPlainName(At(i - 1)) || At(i - 1).Is(">")) && At(i - 1).End == tk.Offset;
    }

    /// <summary>Index of the first token of the type that ends at <paramref name="i"/>, or -1.</summary>
    private int TypeStart(int i)
    {
        if (At(i).Is("?"))
        {
            i--;
        }

        if (At(i).Is(">"))
        {
            var depth = 0;
            for (; i >= 0; i--)
            {
                if (At(i).Is(">"))
                {
                    depth++;
                }
                else if (At(i).Is("<") && --depth == 0)
                {
                    i--;
                    break;
                }
                else if (At(i).Is(";") || At(i).Is("{"))
                {
                    return -1;
                }
            }
        }

        while (At(i - 1).Is(".") && IsPlainName(At(i - 2)))
        {
            i -= 2;
        }

        return i >= 0 && IsTypeEndOrIdent(i) ? i : -1;
    }

    private bool IsTypeEndOrIdent(int i) => At(i).Kind == TokenKind.Identifier;

    private static string? TypeNameBefore(List<Token> toks, int nameIdx)
    {
        var i = nameIdx - 1;
        if (i >= 0 && toks[i].Is("?"))
        {
            i--;
        }

        if (i >= 0 && toks[i].Is(">"))
        {
            var depth = 0;
            for (; i >= 0; i--)
            {
                if (toks[i].Is(">"))
                {
                    depth++;
                }
                else if (toks[i].Is("<") && --depth == 0)
                {
                    i--;
                    break;
                }
            }
        }

        return i >= 0 && toks[i].Kind == TokenKind.Identifier && !NotAType.Contains(toks[i].Text) && toks[i].Text is not ("void" or "dynamic") ? toks[i].Text : null;
    }

    /// <summary>Type of an initializer that is obviously a constructor call (<c>Foo(...)</c>, <c>Foo.named(...)</c>, <c>new Foo()</c>).</summary>
    private string? InferType(int i)
    {
        if (At(i).Is("new") || At(i).Is("const"))
        {
            i++;
        }

        var tk = At(i);
        if (tk.Kind == TokenKind.Identifier && _m.ImportPrefixes.Contains(tk.Text) && At(i + 1).Is(".") && At(i + 2).Kind == TokenKind.Identifier)
        {
            i += 2;
            tk = At(i);
        }

        if (tk.Kind != TokenKind.Identifier || tk.Text.Length == 0 || !char.IsUpper(tk.Text[0]))
        {
            return null;
        }

        var n = At(i + 1);
        if (n.Is("("))
        {
            return tk.Text;
        }

        if (n.Is("<"))
        {
            var after = SkipAngle(i + 1);
            return At(after).Is("(") ? tk.Text : null;
        }

        if (n.Is(".") && At(i + 2).Kind == TokenKind.Identifier && At(i + 3).Is("("))
        {
            return tk.Text;
        }

        return null;
    }

    /// <summary>Index just past the expression starting at <paramref name="i"/> (stops at a depth-0 <c>,</c>, <c>;</c> or closer).</summary>
    private int ExprEnd(int i)
    {
        var depth = 0;
        for (; i < _t.Count; i++)
        {
            var tk = _t[i];
            if (tk.Kind != TokenKind.Punctuation)
            {
                continue;
            }

            if (tk.Text is "(" or "[" or "{")
            {
                if (_match[i] > 0)
                {
                    i = _match[i];
                    continue;
                }

                depth++;
            }
            else if (tk.Text is ")" or "]" or "}")
            {
                return i;
            }
            else if (depth == 0 && tk.Text is "," or ";")
            {
                return i;
            }
        }

        return i;
    }

    private bool IsClosureStart(int k)
    {
        var close = _match[k];
        var after = At(close + 1);
        if (!(after.Is("=>") || after.Is("{") || after.Is("async") || after.Is("sync")))
        {
            return false;
        }

        var prev = At(k - 1);
        if (prev.Kind == TokenKind.Punctuation)
        {
            return prev.Text is "(" or "," or "=" or ":" or "=>" or "[" or "?" or "{" or ";" or "}" or "&" or "|";
        }

        return prev.Kind == TokenKind.Identifier && prev.Text is "return" or "await" or "yield" or "throw" or "else";
    }

    /// <summary>True when the parenthesis at <paramref name="k"/> starts a switch-expression arm (a pattern), not a closure.</summary>
    private bool IsSwitchArm(Stack<int> blocks, int k)
    {
        if (blocks.Count == 0 || !(At(k - 1).Is("{") || At(k - 1).Is(",")))
        {
            return false;
        }

        var b = blocks.Peek();
        if (b >= k || (_match[b] > 0 && _match[b] < k))
        {
            return false;
        }

        // the enclosing brace must be the direct container: `switch (x) {` ...
        var close = At(b - 1);
        if (!close.Is(")") || _match[b - 1] <= 0)
        {
            return false;
        }

        var open = _match[b - 1];
        // no bracket opened between the brace and the candidate may still be pending (precomputed: a scan here was
        // quadratic in long lists of closures)
        if (_innermostOpen[k] > b)
        {
            return false;
        }

        return At(open - 1).Is("switch");
    }

    private void ClosureParams(int open)
    {
        var close = _match[open];
        var p = close + 1;
        if (At(p).Is("async") || At(p).Is("sync"))
        {
            p++;
            if (At(p).Is("*"))
            {
                p++;
            }
        }

        int scopeEnd;
        if (At(p).Is("{") && _match[p] > 0)
        {
            scopeEnd = _t[_match[p]].End;
        }
        else if (At(p).Is("=>"))
        {
            scopeEnd = At(ExprEnd(p + 1) - 1).End;
        }
        else
        {
            return;
        }

        SplitParams(open + 1, close, scopeEnd);
    }

    private void SplitParams(int from, int to, int scopeEnd)
    {
        var s = from;
        for (var q = from; q <= to; q++)
        {
            if (q == to || _t[q].Is(","))
            {
                ParamSlice(s, q, scopeEnd);
                s = q + 1;
                continue;
            }

            var tk = _t[q];
            if (tk.Kind == TokenKind.Punctuation && tk.Text is "(" or "[" or "{" && _match[q] > q)
            {
                if (q == s && tk.Text is "[" or "{")
                {
                    SplitParams(q + 1, _match[q], scopeEnd);
                    q = _match[q];
                    s = q + 1;
                    continue;
                }

                q = _match[q];
            }
        }
    }

    private void ParamSlice(int from, int to, int scopeEnd)
    {
        if (from >= to)
        {
            return;
        }

        // cut at default value
        var end = to;
        for (var q = from; q < to; q++)
        {
            if (_t[q].Kind == TokenKind.Punctuation && _t[q].Text is "(" or "[" or "{" && _match[q] > 0)
            {
                q = _match[q];
            }
            else if (_t[q].Is("=") || _t[q].Is(":"))
            {
                end = q;
                break;
            }
        }

        var nameIdx = -1;
        for (var q = end - 1; q >= from; q--)
        {
            if (_t[q].Kind == TokenKind.Identifier && IsPlainName(_t[q]) && !At(q - 1).Is("."))
            {
                nameIdx = q;
                break;
            }

            if (_t[q].Is(")"))
            {
                return; // function-typed parameter: not handled
            }
        }

        if (nameIdx < 0 || At(nameIdx + 1).Is("(") || At(nameIdx + 1).Is("<") || At(nameIdx - 1).Is("this"))
        {
            return;
        }

        var d = AddLocal(DeclKind.Parameter, nameIdx, _t[from].Offset, _t[end - 1].End, scopeEnd, TypeNameBefore(_t, nameIdx));
        if (d is not null)
        {
            d.Signature = Collapse(_t[from].Offset, _t[end - 1].End);
        }
    }
}
