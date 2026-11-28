using System.Globalization;
using System.Text;
using ScipSharp.Indexer.Pli.Syntax;

namespace ScipSharp.Indexer.Pli.Semantics;

/// <summary>
/// First pass over a compilation unit: splits the token stream (with %INCLUDE members spliced in) into statements,
/// builds the block tree with every declaration, and records the token ranges whose names are resolved in the second
/// pass (<see cref="Resolver"/>). PL/I declarations apply to their whole block regardless of position, hence two passes.
/// Error tolerant: unknown statements are treated as expressions, unbalanced ENDs are ignored.
/// </summary>
internal sealed class UnitParser
{
    private const int MaxIncludeDepth = 32;
    private const int MaxStatementNesting = 200;

    /// <summary>
    /// How often one member may be spliced into the same block of a unit. Re-including it there yields the very same
    /// tokens resolved in the same scope, so the cap loses nothing for normal code but stops a diamond of includes
    /// (each member including two others) from expanding 2^depth times.
    /// </summary>
    private const int MaxIncludesPerBlock = 8;

    private readonly bool _caseSensitive;
    private readonly Func<string, int, SourceFileEntry?> _include;
    private readonly Stack<(List<Token> Tokens, int Pos, int File)> _sources = new();
    private readonly List<Open> _open = [];
    private readonly List<PpOpen> _ppOpen = [];
    private readonly Dictionary<(int File, Block Block), int> _includeCounts = [];
    private Unit _unit = null!;
    private Block _current = null!;
    private int _anonymous;
    private int _depth;

    /// <param name="caseSensitive">Whether names are case-sensitive (Multics) or folded to upper case.</param>
    /// <param name="include">Resolves an include member name from a file id to a loaded file (null when not found).</param>
    public UnitParser(bool caseSensitive, Func<string, int, SourceFileEntry?> include)
    {
        _caseSensitive = caseSensitive;
        _include = include;
    }

    private enum OpenKind
    {
        Group,
        Block,
    }

    private sealed record Open(OpenKind Kind, Decl? Label, Block? Block, Token Start);

    private sealed record PpOpen(Decl? Procedure, Block? Block, Token Start, Block? Previous = null);

    public string Normalize(string name) => _caseSensitive ? name : name.ToUpperInvariant();

    public Unit Parse(SourceFileEntry main)
    {
        var root = new Block(BlockKind.File, null, null) { AnonymousName = main.RelativePath };
        _unit = new Unit(main, root);
        _unit.Files.Add(main.Id);
        _current = root;
        _open.Clear();
        _ppOpen.Clear();
        _sources.Clear();
        _includeCounts.Clear();
        _anonymous = 0;
        _sources.Push((main.Tokens, 0, main.Id));
        while (NextStatement() is { } statement)
        {
            try
            {
                ProcessStatement(statement, 0);
            }
            catch (Exception e) when (e is ArgumentOutOfRangeException or InsufficientExecutionStackException)
            {
                // Malformed statement: keep going with the next one.
                _unit.Warnings.Add($"{main.RelativePath}: could not parse statement at {statement[0]}");
            }
        }

        return _unit;
    }

    // ---- statement splitting ------------------------------------------------------------------------------------

    private List<Token>? NextStatement()
    {
        while (_sources.Count > 0)
        {
            var (tokens, pos, file) = _sources.Pop();
            if (pos >= tokens.Count)
            {
                continue;
            }

            var statement = new List<Token>();
            var i = pos;
            while (i < tokens.Count)
            {
                var t = tokens[i++];
                statement.Add(t);
                if (t.Is(";"))
                {
                    break;
                }
            }

            _sources.Push((tokens, i, file));
            return statement;
        }

        return null;
    }

    // ---- statements -----------------------------------------------------------------------------------------------

    private bool InPreprocessor => _current.Kind == BlockKind.PreprocessorProcedure;

    private void ProcessStatement(List<Token> t, int i)
    {
        var end = t.Count > 0 && t[^1].Is(";") ? t.Count - 1 : t.Count;
        if (i >= end)
        {
            return;
        }

        // Nested IF/ELSE/WHEN/ON prefixes recurse once per prefix; give up on pathological statements.
        if (++_depth > MaxStatementNesting)
        {
            _depth--;
            _unit.Warnings.Add($"{_unit.File.RelativePath}: statement nested too deeply at {t[i]}");
            AddRefs(t, i, end, RefMode.Expression);
            return;
        }

        try
        {
            ProcessStatementCore(t, i, end);
        }
        finally
        {
            _depth--;
        }
    }

    private void ProcessStatementCore(List<Token> t, int i, int end)
    {

        if (t[i].Is("%"))
        {
            ProcessPreprocessor(t, i + 1, end);
            return;
        }

        // Condition prefixes: (SIZE, NOSUBRG): stmt
        while (i < end && t[i].Is("("))
        {
            var close = MatchParen(t, i, end);
            if (close + 1 < end && t[close + 1].Is(":"))
            {
                i = close + 2;
            }
            else
            {
                break;
            }
        }

        var labels = new List<Token>();
        while (i + 1 < end && t[i].IsIdent)
        {
            if (t[i + 1].Is(":"))
            {
                labels.Add(t[i]);
                i += 2;
            }
            else if (t[i + 1].Is("(") && MatchParen(t, i + 1, end) is var c && c + 1 < end && t[c + 1].Is(":") && IsConstantList(t, i + 2, c))
            {
                labels.Add(t[i]); // label array element L(3):
                i = c + 2;
            }
            else
            {
                break;
            }
        }

        if (i >= end)
        {
            DeclareLabels(labels);
            return;
        }

        if (t[i].Is("%"))
        {
            DeclareLabels(labels);
            ProcessPreprocessor(t, i + 1, end);
            return;
        }

        var first = t[i];
        if (!first.IsIdent)
        {
            DeclareLabels(labels);
            AddRefs(t, i, end, RefMode.Expression);
            return;
        }

        var keyword = first.Text.ToUpperInvariant();
        var isIf = keyword == "IF" && FindWord(t, i + 1, end, "THEN") >= 0;
        if (!isIf && IsAssignment(t, i, end, out var eq))
        {
            DeclareLabels(labels);
            AddRefs(t, i, eq, RefMode.Expression, write: true);
            var rhs = eq + 1;
            AddRefs(t, rhs, end, RefMode.Expression);
            return;
        }

        switch (keyword)
        {
            case "PROC" or "PROCEDURE":
                OpenProcedure(labels, t, i, end);
                return;
            case "PACKAGE":
                OpenPackage(labels, t, i, end);
                return;
            case "BEGIN":
                OpenBegin(labels, t, i, end);
                return;
            case "ENTRY" when labels.Count > 0:
                EntryStatement(labels, t, i, end);
                return;
            case "END":
                DeclareLabels(labels);
                EndStatement(t, i, end);
                return;
        }

        DeclareLabels(labels);
        switch (keyword)
        {
            case "DCL" or "DECLARE":
                Declare(t, i + 1, end, define: false);
                break;
            case "DEFINE":
                Define(t, i + 1, end);
                break;
            case "DEFAULT" or "DFT":
                break;
            case "DO":
                {
                    var label = labels.Count > 0 ? LastLabel() : null;
                    _open.Add(new Open(OpenKind.Group, label, null, labels.Count > 0 ? labels[0] : first));
                    var j = i + 1;
                    if (j < end && t[j].IsIdent && !Keywords.StatementOptions["DO"].Contains(t[j].Text) && IsAssignment(t, j, end, out var doEq))
                    {
                        AddRefs(t, j, doEq, RefMode.Expression, write: true);
                        j = doEq + 1;
                    }

                    AddRefs(t, j, end, RefMode.Expression, Keywords.StatementOptions["DO"]);
                    break;
                }

            case "SELECT":
                _open.Add(new Open(OpenKind.Group, labels.Count > 0 ? LastLabel() : null, null, labels.Count > 0 ? labels[0] : first));
                AddRefs(t, i + 1, end, RefMode.Expression);
                break;
            case "IF":
                {
                    var then = FindWord(t, i + 1, end, "THEN");
                    AddRefs(t, i + 1, then, RefMode.Expression);
                    ProcessStatement(t, then + 1);
                    break;
                }

            case "ELSE":
                ProcessStatement(t, i + 1);
                break;
            case "WHEN":
                {
                    var j = i + 1;
                    if (j < end && t[j].Is("("))
                    {
                        var close = MatchParen(t, j, end);
                        AddRefs(t, j + 1, close, RefMode.Expression);
                        j = close + 1;
                    }

                    ProcessStatement(t, j);
                    break;
                }

            case "OTHERWISE" or "OTHER":
                ProcessStatement(t, i + 1);
                break;
            case "ON":
                OnStatement(t, i + 1, end);
                break;
            case "CALL":
                AddRefs(t, i + 1, end, RefMode.Call);
                break;
            case "GO" or "GOTO":
                AddRefs(t, i + 1, end, RefMode.Expression, Set("TO"));
                break;
            case "EXEC":
                {
                    var sql = i + 1 < end && t[i + 1].IsWord("SQL");
                    AddRefs(t, i + 2, end, sql ? RefMode.Sql : RefMode.DeclaredOnly);
                    break;
                }

            default:
                if (Keywords.StatementOptions.TryGetValue(keyword, out var options))
                {
                    AddRefs(t, i + 1, end, RefMode.Expression, options, formatItems: keyword is "PUT" or "GET" or "FORMAT");
                }
                else
                {
                    // Unknown statement (or a keyword we do not model): treat it all as an expression.
                    AddRefs(t, Keywords.Statements.Contains(keyword) ? i + 1 : i, end, RefMode.Expression);
                }

                break;
        }

        Decl? LastLabel() => _current.ByName.TryGetValue(Normalize(labels[^1].Text), out var l) ? l[^1] : null;
    }

    private static HashSet<string> Set(params string[] words) => new(words, StringComparer.OrdinalIgnoreCase);

    private void OnStatement(List<Token> t, int i, int end)
    {
        // ON cond [(args)] {, cond [(args)]} [SNAP] (SYSTEM; | on-unit)
        while (i < end && t[i].IsIdent)
        {
            i++;
            if (i < end && t[i].Is("("))
            {
                var close = MatchParen(t, i, end);
                AddRefs(t, i + 1, close, RefMode.Expression);
                i = close + 1;
            }

            if (i < end && t[i].Is(","))
            {
                i++;
                continue;
            }

            break;
        }

        if (i < end && t[i].IsWord("SNAP"))
        {
            i++;
        }

        if (i < end && t[i].IsWord("SYSTEM"))
        {
            return;
        }

        ProcessStatement(t, i);
    }

    private void DeclareLabels(List<Token> labels)
    {
        foreach (var l in labels)
        {
            var d = NewDecl(l, DeclKind.Label, _current);
            d.Signature = $"{l.Text}: (label)";
        }
    }

    private Decl NewDecl(Token name, DeclKind kind, Block block)
    {
        var d = new Decl(Normalize(name.Text), name.Text, kind, block);
        d.Definitions.Add(name);
        block.Add(d);
        _unit.Decls.Add(d);
        return d;
    }

    private void OpenProcedure(List<Token> labels, List<Token> t, int i, int end)
    {
        var start = labels.Count > 0 ? labels[0] : t[i];
        var nameToken = labels.Count > 0 ? labels[0] : t[i];
        var parent = _current;
        var decl = new Decl(Normalize(nameToken.Text), nameToken.Text, DeclKind.Procedure, parent);
        if (labels.Count > 0)
        {
            decl.Definitions.Add(nameToken);
        }

        parent.Add(decl);
        _unit.Decls.Add(decl);
        var block = new Block(BlockKind.Procedure, parent, decl);
        if (labels.Count == 0)
        {
            block.AnonymousName = $"$proc{++_anonymous}";
        }

        parent.Children.Add(block);
        decl.OwnBlock = block;
        decl.IsExternal = block.IsExternalProcedure;
        decl.Signature = Render(t, labels.Count > 0 ? IndexOf(t, labels[0]) : i, end);

        // Further labels are additional entry points of the same procedure.
        for (var k = 1; k < labels.Count; k++)
        {
            var entry = NewDecl(labels[k], DeclKind.Entry, parent);
            entry.OwnBlock = block;
            entry.Signature = decl.Signature;
        }

        var j = Parameters(t, i + 1, end, block);
        _current = block;
        AddRefs(t, j, end, RefMode.Attributes);
        _open.Add(new Open(OpenKind.Block, decl, block, start));
    }

    private void OpenPackage(List<Token> labels, List<Token> t, int i, int end)
    {
        var nameToken = labels.Count > 0 ? labels[0] : t[i];
        var parent = _current;
        var decl = new Decl(Normalize(nameToken.Text), nameToken.Text, DeclKind.Package, parent);
        if (labels.Count > 0)
        {
            decl.Definitions.Add(nameToken);
        }

        parent.Add(decl);
        _unit.Decls.Add(decl);
        var block = new Block(BlockKind.Package, parent, decl);
        parent.Children.Add(block);
        decl.OwnBlock = block;
        decl.Signature = Render(t, labels.Count > 0 ? IndexOf(t, labels[0]) : i, end);
        _current = block;
        for (var j = i + 1; j < end; j++)
        {
            if ((t[j].IsWord("EXPORTS") || t[j].IsWord("RESERVES")) && j + 1 < end && t[j + 1].Is("("))
            {
                var close = MatchParen(t, j + 1, end);
                AddRefs(t, j + 2, close, RefMode.Exports);
                j = close;
            }
        }

        _open.Add(new Open(OpenKind.Block, decl, block, labels.Count > 0 ? labels[0] : t[i]));
    }

    private void OpenBegin(List<Token> labels, List<Token> t, int i, int end)
    {
        var parent = _current;
        Decl? decl = null;
        if (labels.Count > 0)
        {
            decl = NewDecl(labels[0], DeclKind.BeginBlock, parent);
            decl.Signature = $"{labels[0].Text}: BEGIN";
            for (var k = 1; k < labels.Count; k++)
            {
                NewDecl(labels[k], DeclKind.Label, parent).Signature = $"{labels[k].Text}: (label)";
            }
        }

        var block = new Block(BlockKind.Begin, parent, decl);
        if (decl is null)
        {
            block.AnonymousName = $"$begin{++_anonymous}";
        }
        else
        {
            decl.OwnBlock = block;
        }

        parent.Children.Add(block);
        _current = block;
        AddRefs(t, i + 1, end, RefMode.Attributes);
        _open.Add(new Open(OpenKind.Block, decl, block, labels.Count > 0 ? labels[0] : t[i]));
    }

    private void EntryStatement(List<Token> labels, List<Token> t, int i, int end)
    {
        var proc = _current;
        while (proc.Kind != BlockKind.Procedure && proc.Parent is not null)
        {
            proc = proc.Parent;
        }

        var holder = proc.Parent ?? proc;
        var signature = Render(t, IndexOf(t, labels[0]), end);
        foreach (var l in labels)
        {
            var e = NewDecl(l, DeclKind.Entry, holder);
            e.OwnBlock = proc;
            e.Signature = signature;
        }

        var j = Parameters(t, i + 1, end, proc);
        AddRefs(t, j, end, RefMode.Attributes);
    }

    /// <summary>Parses a parameter list at <paramref name="i"/> (if any) and returns the index after it.</summary>
    private int Parameters(List<Token> t, int i, int end, Block block)
    {
        if (i >= end || !t[i].Is("("))
        {
            return i;
        }

        var close = MatchParen(t, i, end);
        for (var k = i + 1; k < close; k++)
        {
            if (!t[k].IsIdent)
            {
                continue;
            }

            var name = Normalize(t[k].Text);
            if (block.ByName.TryGetValue(name, out var existing)
                && existing.Find(static d => d.Parent is null && d.Kind is DeclKind.Parameter or DeclKind.Variable or DeclKind.EntryDeclaration
                    or DeclKind.File or DeclKind.Condition) is { } p)
            {
                // Declared before this (secondary) entry point names it as a parameter.
                if (p.Kind != DeclKind.Parameter && !p.IsExternal)
                {
                    p.Kind = DeclKind.Parameter;
                }

                p.DeclarationReferences.Add(t[k]);
            }
            else
            {
                var d = NewDecl(t[k], DeclKind.Parameter, block);
                d.Signature = $"{t[k].Text} (parameter)";
            }
        }

        return close + 1;
    }

    private void EndStatement(List<Token> t, int i, int end)
    {
        var endToken = t[^1];
        Token? label = i + 1 < end && t[i + 1].IsIdent ? t[i + 1] : null;
        var target = _open.Count - 1;
        if (label is not null)
        {
            var name = Normalize(label.Text);
            for (var k = _open.Count - 1; k >= 0; k--)
            {
                if (_open[k].Label?.Name == name)
                {
                    target = k;
                    break;
                }
            }

            if (target >= 0 && _open[target].Label is { } l && l.Name == name)
            {
                l.DeclarationReferences.Add(label);
            }
            else
            {
                AddRefs(t, i + 1, end, RefMode.DeclaredOnly);
            }
        }

        if (target < 0)
        {
            return;
        }

        for (var k = _open.Count - 1; k >= target; k--)
        {
            var o = _open[k];
            _open.RemoveAt(k);
            if (o.Kind == OpenKind.Block && o.Block is { } b)
            {
                if (b.Decl is { } d)
                {
                    d.Extent = (o.Start, endToken);
                }

                _current = b.Parent ?? _current;
            }
        }
    }

    // ---- declarations ---------------------------------------------------------------------------------------------

    private sealed class Leaf(Token name)
    {
        public Token Name { get; } = name;

        public int? Level { get; set; }

        public (int Start, int End)? Dimensions { get; set; }

        public List<(int Start, int End)> Attributes { get; } = [];
    }

    private void Declare(List<Token> t, int i, int end, bool define, DeclKind? defineKind = null)
    {
        var leaves = new List<Leaf>();
        var j = i;
        while (j < end)
        {
            var before = j;
            leaves.AddRange(ParseItem(t, ref j, end));
            if (j < end && t[j].Is(","))
            {
                j++;
            }
            else if (j == before)
            {
                j++;
            }
        }

        var stack = new List<Decl>();
        foreach (var leaf in leaves)
        {
            var level = leaf.Level ?? 1;
            while (stack.Count > 0 && stack[^1].Level >= level)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            var parent = level > 1 && stack.Count > 0 ? stack[^1] : null;
            var decl = DeclareLeaf(t, leaf, level, parent, define, defineKind);
            stack.Add(decl);
        }
    }

    private static List<Leaf> ParseItem(List<Token> t, ref int j, int end)
    {
        int? level = null;
        if (j < end && t[j].Kind == TokenKind.Number && int.TryParse(t[j].Text, NumberStyles.None, CultureInfo.InvariantCulture, out var lv))
        {
            level = lv;
            j++;
        }

        var result = new List<Leaf>();
        if (j < end && t[j].Is("("))
        {
            // Factored declarations nest `((a, b) FIXED, c)`; pathological nesting fails this statement only.
            System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();
            var close = MatchParen(t, j, end);
            var k = j + 1;
            while (k < close)
            {
                var before = k;
                result.AddRange(ParseItem(t, ref k, close));
                if (k < close && t[k].Is(","))
                {
                    k++;
                }
                else if (k == before)
                {
                    k++;
                }
            }

            j = close + 1;
        }
        else if (j < end && t[j].IsIdent)
        {
            var leaf = new Leaf(t[j]);
            j++;
            if (j < end && t[j].Is("("))
            {
                var close = MatchParen(t, j, end);
                leaf.Dimensions = (j + 1, close);
                j = close + 1;
            }

            result.Add(leaf);
        }
        else
        {
            return result;
        }

        var attrStart = j;
        while (j < end && !t[j].Is(",") && !t[j].Is(")"))
        {
            j = t[j].Is("(") ? MatchParen(t, j, end) + 1 : j + 1;
        }

        foreach (var leaf in result)
        {
            if (level is not null)
            {
                leaf.Level ??= level;
            }

            if (j > attrStart)
            {
                leaf.Attributes.Add((attrStart, j));
            }
        }

        return result;
    }

    private Decl DeclareLeaf(List<Token> t, Leaf leaf, int level, Decl? parent, bool define, DeclKind? defineKind)
    {
        var name = Normalize(leaf.Name.Text);
        var kind = parent is not null ? DeclKind.Member : define ? defineKind ?? DeclKind.Type : DeclKind.Variable;
        bool external = false, isInternal = false, builtin = false, entry = false, variable = false;
        List<Token>? like = null, type = null;
        foreach (var (s, e) in leaf.Attributes)
        {
            for (var k = s; k < e; k++)
            {
                var tok = t[k];
                if (tok.Is("("))
                {
                    k = MatchParen(t, k, e);
                    continue;
                }

                if (!tok.IsIdent)
                {
                    continue;
                }

                switch (tok.Text.ToUpperInvariant())
                {
                    case "EXTERNAL" or "EXT":
                        external = true;
                        break;
                    case "INTERNAL" or "INT":
                        isInternal = true;
                        break;
                    case "BUILTIN":
                        builtin = true;
                        break;
                    case "ENTRY":
                        entry = true;
                        break;
                    case "VARIABLE":
                        variable = true;
                        break;
                    case "CONDITION" or "COND" when kind == DeclKind.Variable:
                        kind = DeclKind.Condition;
                        break;
                    case "FILE" when kind == DeclKind.Variable:
                        kind = DeclKind.File;
                        break;
                    case "PARAMETER" or "PARM" when kind == DeclKind.Variable:
                        kind = DeclKind.Parameter;
                        break;
                    case "GENERIC" when kind == DeclKind.Variable:
                        kind = DeclKind.Generic;
                        break;
                    case "LIKE":
                        like = Chain(t, k + 1, e);
                        break;
                    case "TYPE" or "HANDLE" when k + 1 < e && t[k + 1].Is("("):
                        type = Chain(t, k + 2, MatchParen(t, k + 1, e));
                        break;
                }
            }
        }

        if (kind == DeclKind.Variable && builtin)
        {
            kind = DeclKind.Builtin;
        }
        else if (kind == DeclKind.Variable && entry && !variable)
        {
            kind = DeclKind.EntryDeclaration;
        }

        var signature = DeclarationText(t, leaf, level, define);
        Decl decl;
        if (parent is null
            && _current.ByName.TryGetValue(name, out var existing)
            && existing.Find(static d => d.Kind == DeclKind.Parameter && !d.Declared) is { } param)
        {
            // DCL of a parameter: the DECLARE becomes the definition, the parameter list entries references.
            decl = param;
            decl.DeclarationReferences.InsertRange(0, decl.Definitions);
            decl.Definitions.Clear();
            decl.Definitions.Add(leaf.Name);
        }
        else
        {
            decl = new Decl(name, leaf.Name.Text, kind, _current);
            if (kind == DeclKind.Builtin)
            {
                decl.DeclarationReferences.Add(leaf.Name);
            }
            else
            {
                decl.Definitions.Add(leaf.Name);
            }

            _current.Add(decl);
            _unit.Decls.Add(decl);
        }

        decl.Declared = true;
        decl.Level = level;
        decl.Signature = signature;
        decl.IsExternal = parent?.IsExternal ?? (external && !isInternal);
        decl.LikeTarget = like;
        decl.TypeTarget = type;
        if (parent is not null)
        {
            decl.Parent = parent;
            parent.Children.Add(decl);
        }

        if (leaf.Dimensions is { } dims)
        {
            AddRefs(t, dims.Start, dims.End, RefMode.Expression);
        }

        foreach (var (s, e) in leaf.Attributes)
        {
            AddRefs(t, s, e, RefMode.Attributes);
        }

        return decl;
    }

    private static string DeclarationText(List<Token> t, Leaf leaf, int level, bool define)
    {
        var b = new StringBuilder(define ? "DEFINE " : "DCL ");
        if (level > 1 || leaf.Level is not null)
        {
            b.Append(level.ToString(CultureInfo.InvariantCulture)).Append(' ');
        }

        b.Append(leaf.Name.Text);
        if (leaf.Dimensions is { } d)
        {
            b.Append('(').Append(Render(t, d.Start, d.End)).Append(')');
        }

        foreach (var (s, e) in leaf.Attributes)
        {
            b.Append(' ').Append(Render(t, s, e));
        }

        return b.ToString();
    }

    private void Define(List<Token> t, int i, int end)
    {
        if (i >= end || !t[i].IsIdent)
        {
            return;
        }

        var what = t[i].Text.ToUpperInvariant();
        switch (what)
        {
            case "STRUCTURE" or "STRUCT" or "ALIAS":
                Declare(t, i + 1, end, define: true);
                break;
            case "ORDINAL" when i + 1 < end && t[i + 1].IsIdent:
                {
                    var type = NewDecl(t[i + 1], DeclKind.Type, _current);
                    type.Signature = "DEFINE " + Render(t, i, end);
                    var j = i + 2;
                    if (j < end && t[j].Is("("))
                    {
                        var close = MatchParen(t, j, end);
                        var expectName = true;
                        for (var k = j + 1; k < close; k++)
                        {
                            if (t[k].Is("("))
                            {
                                k = MatchParen(t, k, close);
                                continue;
                            }

                            if (t[k].Is(","))
                            {
                                expectName = true;
                                continue;
                            }

                            if (expectName && t[k].IsIdent)
                            {
                                var v = NewDecl(t[k], DeclKind.OrdinalValue, _current);
                                v.Parent = type;
                                v.Signature = $"{t[k].Text} (ordinal value of {type.DisplayName})";
                                expectName = false;
                            }
                        }

                        j = close + 1;
                    }

                    AddRefs(t, j, end, RefMode.Attributes);
                    break;
                }

            default:
                AddRefs(t, i, end, RefMode.Attributes);
                break;
        }
    }

    // ---- preprocessor ---------------------------------------------------------------------------------------------

    private void ProcessPreprocessor(List<Token> t, int i, int end)
    {
        var labels = new List<Token>();
        while (i + 1 < end && t[i].IsIdent && t[i + 1].Is(":"))
        {
            labels.Add(t[i]);
            i += 2;
        }

        if (i >= end || !t[i].IsIdent)
        {
            return;
        }

        var keyword = t[i].Text.ToUpperInvariant();
        if (keyword is not ("IF" or "DO") && IsAssignment(t, i, end, out var eq))
        {
            AddRefs(t, i, eq, RefMode.Expression, preprocessor: true, write: true);
            AddRefs(t, eq + 1, end, RefMode.Expression, preprocessor: true);
            return;
        }

        switch (keyword)
        {
            case "INCLUDE" or "XINCLUDE" or "INSCAN" or "XINSCAN":
                Include(t, i + 1, end);
                break;
            case "DCL" or "DECLARE":
                PreprocessorDeclare(t, i + 1, end);
                break;
            case "REPLACE" when i + 1 < end && t[i + 1].IsIdent:
                {
                    var d = PreprocessorDecl(t[i + 1], DeclKind.PreprocessorVariable);
                    d.Signature = "%" + Render(t, i, end);
                    AddRefs(t, i + 2, end, RefMode.Expression, Set("BY"), preprocessor: true);
                    break;
                }

            case "PROC" or "PROCEDURE":
                {
                    // Preprocessor procedures live in the compilation's preprocessor scope, wherever they appear.
                    Decl? decl = labels.Count > 0 ? PreprocessorDecl(labels[0], DeclKind.PreprocessorProcedure) : null;
                    var block = new Block(BlockKind.PreprocessorProcedure, _unit.Root, decl);
                    if (decl is not null)
                    {
                        decl.Signature = "%" + Render(t, IndexOf(t, labels[0]), end);
                        decl.OwnBlock = block;
                    }
                    else
                    {
                        block.AnonymousName = $"$pp{++_anonymous}";
                    }

                    _unit.Root.Children.Add(block);
                    Parameters(t, i + 1, end, block);
                    _ppOpen.Add(new PpOpen(decl, block, labels.Count > 0 ? labels[0] : t[i], _current));
                    _current = block;
                    break;
                }

            case "END":
                if (_ppOpen.Count > 0)
                {
                    var o = _ppOpen[^1];
                    _ppOpen.RemoveAt(_ppOpen.Count - 1);
                    if (o.Block is { } b)
                    {
                        if (o.Procedure is { } p)
                        {
                            p.Extent = (o.Start, t[^1]);
                        }

                        _current = o.Previous ?? b.Parent ?? _current;
                    }
                }

                break;
            case "DO":
                _ppOpen.Add(new PpOpen(null, null, t[i]));
                AddRefs(t, i + 1, end, RefMode.Expression, Keywords.StatementOptions["DO"], preprocessor: true);
                break;
            case "IF":
                {
                    var then = FindWord(t, i + 1, end, "THEN");
                    var exprEnd = then < 0 ? end : then > 0 && t[then - 1].Is("%") ? then - 1 : then;
                    AddRefs(t, i + 1, exprEnd, RefMode.Expression, preprocessor: true);
                    if (then >= 0)
                    {
                        ProcessStatement(t, then + 1);
                    }

                    break;
                }

            case "ELSE":
                ProcessStatement(t, i + 1);
                break;
            case "ACTIVATE" or "ACT" or "DEACTIVATE" or "DEACT":
                AddRefs(t, i + 1, end, RefMode.DeclaredOnly, Set("RESCAN", "NORESCAN"), preprocessor: true);
                break;
            case "PAGE" or "SKIP" or "PRINT" or "NOPRINT" or "PUSH" or "POP" or "NOTE" or "PROCESS" or "LINE" or "OPTION" or "OPTIONS":
                break;
            default:
                AddRefs(t, i + 1, end, RefMode.Expression, preprocessor: true);
                break;
        }
    }

    private Decl PreprocessorDecl(Token name, DeclKind kind)
    {
        var n = Normalize(name.Text);
        if (_unit.Preprocessor.TryGetValue(n, out var existing))
        {
            if (kind == DeclKind.PreprocessorProcedure && existing.Kind == DeclKind.PreprocessorProcedure && existing.OwnBlock is null)
            {
                // %DCL P ENTRY; ... %P: PROC; — the procedure statement is the definition.
                existing.DeclarationReferences.AddRange(existing.Definitions);
                existing.Definitions.Clear();
                existing.Definitions.Add(name);
            }
            else
            {
                existing.DeclarationReferences.Add(name);
            }

            return existing;
        }

        var d = new Decl(n, name.Text, kind, _unit.Root);
        d.Definitions.Add(name);
        _unit.Preprocessor[n] = d;
        _unit.Decls.Add(d);
        return d;
    }

    private void PreprocessorDeclare(List<Token> t, int i, int end)
    {
        var j = i;
        while (j < end)
        {
            var before = j;
            var leaves = ParseItem(t, ref j, end);
            foreach (var leaf in leaves)
            {
                var isEntry = leaf.Attributes.Exists(a => FindWord(t, a.Start, a.End, "ENTRY") >= 0);
                var d = PreprocessorDecl(leaf.Name, isEntry ? DeclKind.PreprocessorProcedure : DeclKind.PreprocessorVariable);
                d.Signature ??= DeclarationText(t, leaf, 1, false).Insert(0, "%");
            }

            if (j < end && t[j].Is(","))
            {
                j++;
            }
            else if (j == before)
            {
                j++;
            }
        }
    }

    private void Include(List<Token> t, int i, int end)
    {
        var members = new List<string>();
        for (var j = i; j < end; j++)
        {
            var tok = t[j];
            if (tok.Kind == TokenKind.String)
            {
                var q = tok.Text[0];
                var close = tok.Text.LastIndexOf(q);
                members.Add(close > 0 ? tok.Text[1..close] : tok.Text[1..]);
            }
            else if (tok.IsIdent)
            {
                var name = tok.Text;

                // ddname(member)
                if (j + 3 < end + 1 && j + 1 < end && t[j + 1].Is("(") && j + 2 < end && (t[j + 2].IsIdent || t[j + 2].Kind == TokenKind.String))
                {
                    var m = t[j + 2];
                    name = m.Kind == TokenKind.String ? m.Text.Trim('\'', '"') : m.Text;
                    j = MatchParen(t, j + 1, end);
                }

                // Allow dotted names: %include foo.incl;
                while (j + 2 < end && t[j + 1].Is(".") && t[j + 2].IsIdent)
                {
                    name += "." + t[j + 2].Text;
                    j += 2;
                }

                members.Add(name);
            }
        }

        var from = t[i - 1].File;
        var pending = new List<SourceFileEntry>();
        foreach (var m in members)
        {
            if (_sources.Count >= MaxIncludeDepth)
            {
                _unit.Warnings.Add($"include nesting too deep at {m}");
                break;
            }

            var file = _include(m, from);
            if (file is null)
            {
                _unit.Warnings.Add($"{_unit.File.RelativePath}: %INCLUDE {m} not found");
                continue;
            }

            if (_sources.Any(s => s.File == file.Id))
            {
                _unit.Warnings.Add($"{_unit.File.RelativePath}: recursive %INCLUDE {m} ignored");
                continue;
            }

            var count = _includeCounts[(file.Id, _current)] = _includeCounts.GetValueOrDefault((file.Id, _current)) + 1;
            if (count > MaxIncludesPerBlock)
            {
                if (count == MaxIncludesPerBlock + 1)
                {
                    _unit.Warnings.Add($"{_unit.File.RelativePath}: %INCLUDE {m} repeated more than {MaxIncludesPerBlock} times in one block; further copies ignored");
                }

                continue;
            }

            pending.Add(file);
        }

        // Push in reverse so that the first member is read first.
        for (var k = pending.Count - 1; k >= 0; k--)
        {
            var file = pending[k];
            if (!_unit.Files.Contains(file.Id))
            {
                _unit.Files.Add(file.Id);
            }

            _sources.Push((file.Tokens, 0, file.Id));
        }
    }

    // ---- helpers --------------------------------------------------------------------------------------------------

    private void AddRefs(
        List<Token> t,
        int start,
        int end,
        RefMode mode,
        HashSet<string>? options = null,
        bool preprocessor = false,
        bool write = false,
        bool formatItems = false)
    {
        if (start < end && start >= 0)
        {
            _unit.References.Add(new RefSite(_current, t, start, end, mode, options, preprocessor || InPreprocessor, write, formatItems));
        }
    }

    /// <summary>
    /// Assignment detection (keywords are not reserved): a reference, optional further targets separated by commas, then
    /// <c>=</c> or a compound assignment operator at parenthesis depth 0.
    /// </summary>
    internal static bool IsAssignment(List<Token> t, int i, int end, out int eq)
    {
        eq = -1;
        var j = i;
        while (true)
        {
            if (j >= end || !t[j].IsIdent)
            {
                return false;
            }

            j++;
            while (true)
            {
                if (j < end && t[j].Is("("))
                {
                    j = MatchParen(t, j, end) + 1;
                }

                if (j + 1 < end && (t[j].Is(".") || t[j].Is("->") || t[j].Is("=>")) && t[j + 1].IsIdent)
                {
                    j += 2;
                    continue;
                }

                break;
            }

            if (j < end && t[j].Is(","))
            {
                j++;
                continue;
            }

            break;
        }

        if (j < end && t[j].Kind == TokenKind.Punct && t[j].Text is "=" or "+=" or "-=" or "*=" or "/=" or "|=" or "&=" or "||=" or "**=")
        {
            eq = j;
            return true;
        }

        return false;
    }

    /// <summary>Index of the parenthesis closing the one at <paramref name="open"/> (or <paramref name="end"/> - 1 if unbalanced).</summary>
    internal static int MatchParen(IReadOnlyList<Token> t, int open, int end)
    {
        var depth = 0;
        for (var k = open; k < end; k++)
        {
            if (t[k].Is("("))
            {
                depth++;
            }
            else if (t[k].Is(")") && --depth == 0)
            {
                return k;
            }
        }

        return Math.Max(open, end - 1);
    }

    /// <summary>Index of the identifier <paramref name="word"/> at depth 0 in [start, end), or -1.</summary>
    private static int FindWord(List<Token> t, int start, int end, string word)
    {
        for (var k = start; k < end; k++)
        {
            if (t[k].Is("("))
            {
                k = MatchParen(t, k, end);
            }
            else if (t[k].IsWord(word))
            {
                return k;
            }
        }

        return -1;
    }

    private static bool IsConstantList(List<Token> t, int start, int end)
    {
        for (var k = start; k < end; k++)
        {
            if (!(t[k].Kind == TokenKind.Number || t[k].Is(",") || t[k].Is("-") || t[k].Is("+")))
            {
                return false;
            }
        }

        return true;
    }

    private static List<Token> Chain(List<Token> t, int start, int end)
    {
        var chain = new List<Token>();
        var k = start;
        while (k < end && t[k].IsIdent)
        {
            chain.Add(t[k]);
            k++;
            if (k < end && t[k].Is("("))
            {
                k = MatchParen(t, k, end) + 1;
            }

            if (k + 1 < end && t[k].Is(".") && t[k + 1].IsIdent)
            {
                k++;
                continue;
            }

            break;
        }

        return chain;
    }

    private static int IndexOf(List<Token> t, Token token)
    {
        var k = t.IndexOf(token);
        return k < 0 ? 0 : k;
    }

    /// <summary>Renders tokens as normalized source text (single spaces, no space around parentheses and commas).</summary>
    internal static string Render(IReadOnlyList<Token> t, int start, int end)
    {
        var b = new StringBuilder();
        Token? prev = null;
        for (var k = start; k < end && k < t.Count; k++)
        {
            var tok = t[k];
            if (tok.Is(";"))
            {
                break;
            }

            if (prev is not null && NeedsSpace(prev, tok))
            {
                b.Append(' ');
            }

            b.Append(tok.Text.ReplaceLineEndings(" "));
            prev = tok;
        }

        return b.ToString();
    }

    private static bool NeedsSpace(Token prev, Token cur)
    {
        if (cur.Is(")") || cur.Is(",") || cur.Is(".") || prev.Is("(") || prev.Is(".") || prev.Is("%") || cur.Is(":"))
        {
            return false;
        }

        if (cur.Is("("))
        {
            return !(prev.IsIdent || prev.Is(")"));
        }

        return true;
    }
}
