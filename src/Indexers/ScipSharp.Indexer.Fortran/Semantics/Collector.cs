using System.Globalization;
using ScipSharp.Indexer.Fortran.Syntax;
using static ScipSharp.Indexer.Fortran.Syntax.Toks;

namespace ScipSharp.Indexer.Fortran.Semantics;

/// <summary>
/// Pass 1: classifies every statement of a file (keywords are not reserved, so assignments are recognised first),
/// builds the scope tree and declares entities, and records the pass-2 work (definitions, references, expressions)
/// on each statement. INCLUDE / #include files are spliced in place and parsed in the includer's scope.
/// </summary>
internal sealed partial class Collector(Workspace ws, Func<string, SourceDoc, SourceDoc?> resolveInclude, Func<SourceDoc, List<Stmt>> read)
{
    private enum FrameKind
    {
        Unit,
        Interface,
        Type,
        Enum,
        Block,
        Associate,
        SelectType,
        SelectCase,
        SelectRank,
        Do,
        If,
        Where,
        Forall,
        Critical,
        Team,
    }

    private sealed class Frame(FrameKind kind, Scope? scope, Entity? construct)
    {
        public FrameKind Kind { get; } = kind;

        public Scope? Scope { get; } = scope;

        public Entity? Construct { get; } = construct;

        public Entity? Assoc { get; init; }
    }

    private static readonly HashSet<string> ProcPrefixes = new(StringComparer.Ordinal)
    {
        "recursive", "pure", "elemental", "impure", "module", "non_recursive", "simple",
    };

    private static readonly HashSet<string> TypeKeywords = new(StringComparer.Ordinal)
    {
        "integer", "real", "complex", "logical", "character", "byte", "doubleprecision", "doublecomplex", "double",
    };

    private static readonly HashSet<string> IntrinsicTypeNames = new(StringComparer.Ordinal)
    {
        "integer", "real", "complex", "logical", "character", "doubleprecision", "double",
    };

    private static readonly HashSet<string> AttrStatements = new(StringComparer.Ordinal)
    {
        "allocatable", "pointer", "target", "optional", "save", "value", "volatile", "asynchronous", "contiguous",
        "protected", "intent", "bind", "codimension", "dimension", "external", "intrinsic", "automatic", "static",
    };

    private static readonly HashSet<string> IoStatements = new(StringComparer.Ordinal)
    {
        "read", "write", "print", "open", "close", "inquire", "rewind", "backspace", "endfile", "flush", "wait",
        "accept", "encode", "decode",
    };

    private static readonly string[] FixedGlueKeywords =
    [
        "doubleprecision", "doublecomplex", "subroutine", "equivalence", "character", "dimension", "intrinsic",
        "parameter", "external", "function", "implicit", "integer", "logical", "complex", "program", "common", "goto",
        "call", "data", "real", "save",
    ];

    private readonly List<Frame> _frames = [];
    private Scope _file = null!;
    private SourceDoc _doc = null!;
    private List<Stmt> _out = null!;
    private int _includeDepth;
    private readonly HashSet<string> _activeIncludes = new(StringComparer.Ordinal);

    private Scope Current
    {
        get
        {
            for (var i = _frames.Count - 1; i >= 0; i--)
            {
                if (_frames[i].Scope is { } s)
                {
                    return s;
                }
            }

            return _file;
        }
    }

    private Frame? Top => _frames.Count > 0 ? _frames[^1] : null;

    /// <summary>Collects one compilation unit file; returns its statements (with included statements spliced in).</summary>
    public (Scope File, List<Stmt> Statements) Collect(SourceDoc doc, List<Stmt> statements)
    {
        _doc = doc;
        _file = new Scope(ScopeKind.File, null, doc);
        _frames.Clear();
        _out = [];
        foreach (var st in statements)
        {
            Process(st);
        }

        _frames.Clear();
        return (_file, _out);
    }

    private void Process(Stmt st)
    {
        if (st.CppInclude is not null)
        {
            Include(st.CppInclude, st.File);
            return;
        }

        var t = st.Tokens;
        if (t.Count == 2 && t[0].IsKw("include") && t[1].Kind == TokenKind.String)
        {
            Include(Unquote(t[1].Text), st.File);
            return;
        }

        _out.Add(st);
        if (t.Count == 0 && st.Label is null)
        {
            return;
        }

        try
        {
            if (Current.Kind == ScopeKind.File && t.Count > 0 && !StartsUnit(t))
            {
                if (IsEndKeyword(t))
                {
                    return;
                }

                OpenImplicitMain(t[0]);
            }

            if (st.Label is { } label)
            {
                DefineLabel(st, label);
            }

            Handle(st, 0);
        }
        catch (ArgumentOutOfRangeException)
        {
            // malformed statement: keep whatever was recorded
        }
        catch (IndexOutOfRangeException)
        {
        }
    }

    private void Include(string path, SourceDoc from)
    {
        var target = resolveInclude(path, from);
        if (target is null || _includeDepth > 16 || !_activeIncludes.Add(target.AbsolutePath))
        {
            return;
        }

        _includeDepth++;
        try
        {
            foreach (var inner in read(target))
            {
                Process(inner);
            }

            from.Macros.UnionWith(target.Macros);
        }
        finally
        {
            _includeDepth--;
            _activeIncludes.Remove(target.AbsolutePath);
        }
    }

    private static string Unquote(string s) => s.Length >= 2 && s[0] is '\'' or '"' ? s[1..^1].Replace(new string(s[0], 2), s[0].ToString(), StringComparison.Ordinal) : s;

    private static bool IsEndKeyword(List<Token> t) => t[0].IsName && t[0].Lower.StartsWith("end", StringComparison.Ordinal) && t[0].Lower != "endfile";

    private static bool StartsUnit(List<Token> t)
    {
        var first = Lower(t, 0);
        if (first is "program" or "module" or "submodule" or "blockdata" or "subroutine" or "function")
        {
            return true;
        }

        if (first == "block" && IsKw(t, 1, "data"))
        {
            return true;
        }

        if (IsAssignment(t, 0))
        {
            return false;
        }

        for (var i = 0; i < t.Count && i < 12; i++)
        {
            if (t[i].Is("("))
            {
                i = Match(t, i);
                continue;
            }

            if (t[i].IsKw("subroutine") || t[i].IsKw("function"))
            {
                return i > 0 && (ProcPrefixes.Contains(Lower(t, 0)) || TypeKeywords.Contains(Lower(t, 0)) || t[0].IsKw("type") || t[0].IsKw("class"));
            }

            if (t[i].Is("::") || t[i].Is("="))
            {
                return false;
            }
        }

        return false;
    }

    private void OpenImplicitMain(Token at)
    {
        var e = ws.Track(new Entity("", "main", EntityKind.Program, _file));
        var body = new Scope(ScopeKind.Program, _file, _doc) { Owner = e, Start = at };
        e.Body = body;
        e.Header = "program";
        Push(FrameKind.Unit, body);
    }

    // ---------------- recording helpers ----------------

    private static void Def(Stmt st, Token tok, Entity e) => st.Actions.Add(r => r.Define(tok, e));

    private static void RefE(Stmt st, Token tok, Entity e) => st.Actions.Add(r => r.RefEntity(tok, e));

    private static void RefName(Stmt st, Token tok, Scope scope, LookupCtx ctx, bool allowImplicit = false) =>
        st.Actions.Add(r => r.RefKey(tok, scope, tok.Lower, ctx, allowImplicit));

    private static void RefKey(Stmt st, Token tok, Scope scope, string key, LookupCtx ctx) =>
        st.Actions.Add(r => r.RefKey(tok, scope, key, ctx, false));

    private static void RefProc(Stmt st, Token tok, Scope scope) => st.Actions.Add(r => r.RefProc(tok, scope));

    private static void RefLabel(Stmt st, Token tok, Scope scope)
    {
        var unit = scope.Unit;
        st.Actions.Add(r => r.RefLabel(tok, unit));
    }

    private static void Walk(Stmt st, int a, int b, Scope scope)
    {
        if (a < b)
        {
            st.Actions.Add(r => r.Walk(st, a, b, scope));
        }
    }

    private void Push(FrameKind kind, Scope? scope, Entity? construct = null, Entity? assoc = null) =>
        _frames.Add(new Frame(kind, scope, construct) { Assoc = assoc });

    /// <summary>Pops up to and including the innermost frame of one of <paramref name="kinds"/>; constructs never cross a unit.</summary>
    private Frame? PopTo(Stmt st, params FrameKind[] kinds)
    {
        var isUnitLevel = kinds.Any(static k => k is FrameKind.Unit or FrameKind.Interface or FrameKind.Type);
        for (var i = _frames.Count - 1; i >= 0; i--)
        {
            var f = _frames[i];
            if (kinds.Contains(f.Kind))
            {
                _frames.RemoveRange(i, _frames.Count - i);
                if (f.Scope is { } s && st.Tokens.Count > 0)
                {
                    s.End = st.Tokens[^1];
                }

                return f;
            }

            if (!isUnitLevel && f.Kind is FrameKind.Unit or FrameKind.Interface or FrameKind.Type)
            {
                return null;
            }
        }

        return null;
    }

    private Entity Declare(Scope scope, Token tok, EntityKind kind)
    {
        if (scope.Entities.TryGetValue(tok.Lower, out var e) && (e.Scope == scope || e.Body == scope || e.Kind == EntityKind.Function))
        {
            return e;
        }

        e = ws.Track(new Entity(tok.Lower, tok.Text, kind, scope) { DefToken = tok });
        scope.Entities[tok.Lower] = e;
        return e;
    }

    private static void AddDocs(Entity e, Stmt st)
    {
        foreach (var d in st.Docs)
        {
            e.Docs.Add(d);
        }
    }

    private void DefineLabel(Stmt st, Token label)
    {
        var unit = Current.Unit;
        var key = NormalizeLabel(label.Text);
        if (!unit.Labels.TryGetValue(key, out var e))
        {
            e = ws.Track(new Entity(key, key, EntityKind.Label, unit) { DefToken = label, Signature = (label.Text + " " + Text(st.Tokens, 0, st.Tokens.Count)).Trim() });
            unit.Labels[key] = e;
        }

        Def(st, label, e);
    }

    public static string NormalizeLabel(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n.ToString(CultureInfo.InvariantCulture) : text;

    // ---------------- statement dispatch ----------------

    private void Handle(Stmt st, int i)
    {
        var t = st.Tokens;
        if (i >= t.Count)
        {
            return;
        }

        Entity? construct = null;
        if (t[i].IsName && IsOp(t, i + 1, ":") && IsName(t, i + 2))
        {
            var unit = Current.Unit;
            construct = ws.Track(new Entity(t[i].Lower, t[i].Text, EntityKind.ConstructName, unit) { DefToken = t[i] });
            Def(st, t[i], construct);
            i += 2;
        }

        if (st.File.Form == SourceForm.Fixed)
        {
            FixFixedForm(t, i);
        }

        if (IsAssignment(t, i))
        {
            Assignment(st, i);
            return;
        }

        if (!t[i].IsName)
        {
            Walk(st, i, t.Count, Current);
            return;
        }

        var (kw, next) = Keyword(t, i);
        switch (kw)
        {
            case "program":
                ProgramUnit(st, i, next, EntityKind.Program, ScopeKind.Program);
                return;
            case "module":
                if (IsKw(t, next, "procedure"))
                {
                    ProcedureStatement(st, next, moduleKeyword: true);
                }
                else if (!TryProcedureHeader(st, i))
                {
                    ProgramUnit(st, i, next, EntityKind.Module, ScopeKind.Module);
                }

                return;
            case "submodule":
                Submodule(st, next);
                return;
            case "blockdata":
                ProgramUnit(st, i, next, EntityKind.BlockData, ScopeKind.BlockData);
                return;
            case "entry":
                Entry(st, next);
                return;
            case "contains":
                Current.AfterContains = true;
                return;
            case "interface":
                Interface(st, next, isAbstract: false);
                return;
            case "abstractinterface":
                Interface(st, next, isAbstract: true);
                return;
            case "procedure":
                ProcedureStatement(st, next - 1, moduleKeyword: false);
                return;
            case "generic":
                GenericBinding(st, next);
                return;
            case "final":
                Final(st, next);
                return;
            case "type" when !IsOp(t, next, "("):
                DerivedType(st, next);
                return;
            case "typeis":
            case "classis":
                Guard(st, next);
                return;
            case "classdefault":
                Guard(st, -1);
                return;
            case "enum":
                Push(FrameKind.Enum, null);
                return;
            case "enumerator":
                Enumerator(st, next);
                return;
            case "use":
                Use(st, next);
                return;
            case "import":
                Import(st, next);
                return;
            case "implicit":
                Implicit(st, next);
                return;
            case "public":
            case "private":
                Access(st, next, kw == "public");
                return;
            case "parameter":
                Parameter(st, next);
                return;
            case "common":
                Common(st, next);
                return;
            case "namelist":
                Namelist(st, next);
                return;
            case "equivalence":
            case "data":
                Walk(st, next, t.Count, Current);
                return;
            case "format":
            case "sequence":
            case "continue":
                return;
            case "call":
                Current.Unit.SeenExecutable = true;
                Call(st, next);
                return;
            case "if":
                Current.Unit.SeenExecutable = true;
                If(st, next, construct);
                return;
            case "elseif":
                ElseIf(st, next);
                return;
            case "else":
            case "elsewhere":
                ElseLike(st, next);
                return;
            case "do":
                Current.Unit.SeenExecutable = true;
                Do(st, next, construct);
                return;
            case "cycle":
            case "exit":
                ConstructRef(st, next);
                return;
            case "goto":
                Current.Unit.SeenExecutable = true;
                GoTo(st, next);
                return;
            case "assign":
                Assign(st, next);
                return;
            case "selectcase":
                Current.Unit.SeenExecutable = true;
                WalkParen(st, next);
                Push(FrameKind.SelectCase, null, construct);
                return;
            case "selectrank":
                SelectType(st, next, construct, FrameKind.SelectRank);
                return;
            case "selecttype":
                Current.Unit.SeenExecutable = true;
                SelectType(st, next, construct, FrameKind.SelectType);
                return;
            case "case":
            case "rank":
                CaseLike(st, next);
                return;
            case "where":
            case "forall":
                Current.Unit.SeenExecutable = true;
                WhereLike(st, next, construct, kw == "where" ? FrameKind.Where : FrameKind.Forall);
                return;
            case "associate":
                Current.Unit.SeenExecutable = true;
                Associate(st, next, construct);
                return;
            case "block":
                Push(FrameKind.Block, new Scope(ScopeKind.Block, Current, _doc), construct);
                return;
            case "critical":
                Push(FrameKind.Critical, null, construct);
                return;
            case "changeteam":
                WalkParen(st, next);
                Push(FrameKind.Team, null, construct);
                return;
            case "allocate":
            case "deallocate":
            case "nullify":
                Current.Unit.SeenExecutable = true;
                Allocate(st, next);
                return;
            case "stop":
            case "errorstop":
                Current.Unit.SeenExecutable = true;
                StopStatement(st, next);
                return;
            case "return":
            case "pause":
            case "sync":
            case "lock":
            case "unlock":
            case "event":
            case "fail":
            case "form":
                Current.Unit.SeenExecutable = true;
                Walk(st, next, t.Count, Current);
                return;
            case "include":
                return;
        }

        if (kw.StartsWith("end", StringComparison.Ordinal) && kw != "endfile" && End(st, kw, next))
        {
            return;
        }

        if (IoStatements.Contains(kw))
        {
            Current.Unit.SeenExecutable = true;
            Io(st, kw, next);
            return;
        }

        if (AttrStatements.Contains(kw))
        {
            AttributeStatement(st, kw, next);
            return;
        }

        if (ProcPrefixes.Contains(kw) || TypeKeywords.Contains(kw) || kw is "subroutine" or "function" or "type" or "class")
        {
            if (TryProcedureHeader(st, i))
            {
                return;
            }

            if (ParseTypeSpec(t, i) is { } spec)
            {
                TypeDecl(st, spec);
                return;
            }
        }

        // Unknown statement keyword (vendor extension or a construct we do not model): index the operands only.
        Walk(st, next, t.Count, Current);
    }

    /// <summary>Normalises multi-word keywords ("end do", "go to", "double precision", "select case", ...).</summary>
    private static (string Keyword, int Next) Keyword(List<Token> t, int i)
    {
        var kw = t[i].Lower;
        var k2 = Lower(t, i + 1);
        switch (kw)
        {
            case "end" when k2 is "if" or "do" or "select" or "subroutine" or "function" or "program" or "module" or "submodule"
                or "type" or "interface" or "associate" or "where" or "forall" or "enum" or "critical" or "team" or "procedure"
                or "blockdata" or "file":
                return ("end" + k2, i + 2);
            case "end" when k2 == "block":
                return IsKw(t, i + 2, "data") ? ("endblockdata", i + 3) : ("endblock", i + 2);
            case "go" when k2 == "to":
                return ("goto", i + 2);
            case "double" when k2 is "precision" or "complex":
                return ("double" + k2, i + 2);
            case "else" when k2 is "if" or "where":
                return ("else" + k2, i + 2);
            case "block" when k2 == "data":
                return ("blockdata", i + 2);
            case "select" when k2 is "case" or "type" or "rank":
                return ("select" + k2, i + 2);
            case "error" when k2 == "stop":
                return ("errorstop", i + 2);
            case "change" when k2 == "team":
                return ("changeteam", i + 2);
            case "type" when k2 == "is" && IsOp(t, i + 2, "("):
                return ("typeis", i + 2);
            case "class" when k2 == "is" && IsOp(t, i + 2, "("):
                return ("classis", i + 2);
            case "class" when k2 == "default":
                return ("classdefault", i + 2);
            case "abstract" when k2 == "interface":
                return ("abstractinterface", i + 2);
            default:
                return (kw, i + 1);
        }
    }

    /// <summary>
    /// Fixed form ignores blanks, so old code glues keywords to names (<c>CALLFOO(X)</c>, <c>GOTO10</c>, <c>REALX</c>,
    /// <c>DO10I=1,10</c>). Splits the first token where that is unambiguous.
    /// </summary>
    private static void FixFixedForm(List<Token> t, int i)
    {
        if (!IsName(t, i))
        {
            return;
        }

        var lower = t[i].Lower;
        if (IsAssignment(t, i))
        {
            // DO10I=1,10 is a DO statement, not an assignment to DO10I.
            if (lower.Length > 3 && lower.StartsWith("do", StringComparison.Ordinal) && char.IsDigit(lower[2]) && IsOp(t, i + 1, "=")
                && FindTop(t, i + 2, t.Count, ",") > 0)
            {
                var d = 2;
                while (d < lower.Length && char.IsDigit(lower[d]))
                {
                    d++;
                }

                if (d < lower.Length && Split(t[i], 2, TokenKind.Int) is { } first && Split(first.Second, d - 2, TokenKind.Name) is { } second)
                {
                    var label = second.First;
                    t[i] = first.First;
                    t.Insert(i + 1, new Token(TokenKind.Int, label.Text, label.File, label.Line, label.Col, label.EndCol));
                    t.Insert(i + 2, second.Second);
                }
            }

            return;
        }

        foreach (var kw in FixedGlueKeywords)
        {
            if (lower.Length > kw.Length && lower.StartsWith(kw, StringComparison.Ordinal))
            {
                var restKind = kw == "goto" && char.IsDigit(lower[kw.Length]) ? TokenKind.Int : TokenKind.Name;
                if (restKind == TokenKind.Name && !Lexer.IsLetter(lower[kw.Length]))
                {
                    return;
                }

                if (Split(t[i], kw.Length, restKind) is { } parts)
                {
                    t[i] = parts.First;
                    t.Insert(i + 1, parts.Second);
                }

                return;
            }
        }
    }

    // ---------------- program units ----------------

    private void ProgramUnit(Stmt st, int start, int nameIdx, EntityKind kind, ScopeKind scopeKind)
    {
        var t = st.Tokens;
        var hasName = IsName(t, nameIdx);
        var name = hasName ? t[nameIdx].Lower : "";
        var display = hasName ? t[nameIdx].Text : kind == EntityKind.BlockData ? "_BLOCK_DATA_" : "main";
        if (!hasName && kind == EntityKind.BlockData)
        {
            name = "_block_data_";
        }

        var e = ws.Track(new Entity(name, display, kind, _file) { DefToken = hasName ? t[nameIdx] : null });
        var body = new Scope(scopeKind, _file, _doc) { Owner = e, Start = t[0] };
        e.Body = body;
        e.Header = Text(t, start, t.Count);
        AddDocs(e, st);
        if (kind == EntityKind.Module)
        {
            ws.Modules.TryAdd(name, e);
        }

        if (hasName)
        {
            Def(st, t[nameIdx], e);
        }

        Push(FrameKind.Unit, body);
    }

    private void Submodule(Stmt st, int i)
    {
        var t = st.Tokens;
        if (!IsOp(t, i, "("))
        {
            return;
        }

        var close = Match(t, i);
        string? ancestor = null;
        string? parent = null;
        Token? ancestorTok = null;
        Token? parentTok = null;
        if (IsName(t, i + 1))
        {
            ancestorTok = t[i + 1];
            ancestor = ancestorTok.Lower;
            if (IsOp(t, i + 2, ":") && IsName(t, i + 3))
            {
                parentTok = t[i + 3];
                parent = parentTok.Lower;
            }
        }

        var nameIdx = close + 1;
        if (!IsName(t, nameIdx) || ancestor is null)
        {
            return;
        }

        var e = ws.Track(new Entity(t[nameIdx].Lower, t[nameIdx].Text, EntityKind.Submodule, _file) { DefToken = t[nameIdx] });
        var body = new Scope(ScopeKind.Submodule, _file, _doc)
        {
            Owner = e, Start = t[0], AncestorName = ancestor, ParentSubmoduleName = parent,
        };
        e.Body = body;
        e.Header = Text(t, 0, t.Count);
        AddDocs(e, st);
        ws.Submodules.TryAdd(ancestor + ":" + e.Name, e);
        ws.SubmoduleScopes.Add(body);
        st.Actions.Add(r => r.RefModule(ancestorTok!, new UseInfo(ancestor, ancestorTok!)));
        if (parentTok is not null)
        {
            st.Actions.Add(r => r.RefSubmodule(parentTok, ancestor, parent!));
        }

        Def(st, t[nameIdx], e);
        Push(FrameKind.Unit, body);
    }

    private bool TryProcedureHeader(Stmt st, int i)
    {
        var t = st.Tokens;
        var j = i;
        var prefixes = new List<string>();
        TypeSpec? spec = null;
        for (var guard = 0; guard < 10 && j < t.Count; guard++)
        {
            var kw = Lower(t, j);
            if (ProcPrefixes.Contains(kw))
            {
                prefixes.Add(kw);
                j++;
                continue;
            }

            if (spec is null && ParseTypeSpec(t, j) is { } s)
            {
                spec = s;
                j = s.End;
                continue;
            }

            break;
        }

        var word = Lower(t, j);
        if (st.File.Form == SourceForm.Fixed && word.Length > 8 && (spec is not null || prefixes.Count > 0 || j == i))
        {
            foreach (var kw in (string[])["subroutine", "function"])
            {
                if (word.StartsWith(kw, StringComparison.Ordinal) && word.Length > kw.Length && Lexer.IsLetter(word[kw.Length])
                    && Split(t[j], kw.Length, TokenKind.Name) is { } parts)
                {
                    t[j] = parts.First;
                    t.Insert(j + 1, parts.Second);
                    word = kw;
                    break;
                }
            }
        }

        if (word is not ("subroutine" or "function") || !IsName(t, j + 1))
        {
            return false;
        }

        ProcedureHeader(st, i, j, prefixes, spec);
        return true;
    }

    private void ProcedureHeader(Stmt st, int start, int kwIdx, List<string> prefixes, TypeSpec? spec)
    {
        var t = st.Tokens;
        var isFunction = t[kwIdx].IsKw("function");
        var nameTok = t[kwIdx + 1];
        var parent = Current;
        var argIdx = new List<int>();
        var after = kwIdx + 2;
        if (IsOp(t, after, "("))
        {
            var close = Match(t, after);
            foreach (var (s, _) in SplitTop(t, after + 1, close))
            {
                if (IsName(t, s))
                {
                    argIdx.Add(s);
                }
            }

            after = close + 1;
        }

        var resultIdx = -1;
        for (var k = after; k < t.Count; k++)
        {
            if (t[k].IsKw("result") && IsOp(t, k + 1, "(") && IsName(t, k + 2))
            {
                resultIdx = k + 2;
            }
        }

        var kind = isFunction ? EntityKind.Function : EntityKind.Subroutine;
        var host = parent;
        bool external = false, forward = false, sepInterface = false, sepImpl = false;
        if (parent.Kind == ScopeKind.Interface)
        {
            host = parent.Parent ?? _file;
            if (parent.AbstractInterface)
            {
                kind = EntityKind.AbstractInterface;
            }
            else if (prefixes.Contains("module"))
            {
                sepInterface = true;
                forward = true;
            }
            else
            {
                external = true;
                forward = true;
            }
        }
        else if (parent.Kind == ScopeKind.File)
        {
            external = true;
        }
        else if (parent.Kind is ScopeKind.Submodule or ScopeKind.Module && prefixes.Contains("module"))
        {
            sepImpl = true;
        }

        var e = ws.Track(new Entity(nameTok.Lower, nameTok.Text, kind, host)
        {
            DefToken = nameTok, IsExternalProc = external, IsForward = forward, IsSeparateInterface = sepInterface, IsSeparateImpl = sepImpl,
        });
        if (!forward || !host.Entities.TryGetValue(e.Name, out var existing) || !existing.IsProcedureLike)
        {
            host.Entities[e.Name] = e;
        }

        if (external && !forward)
        {
            ws.Procs.TryAdd(e.Name, e);
        }

        if (parent.Kind == ScopeKind.Interface && parent.Generic is { } generic)
        {
            generic.Specifics.Add((e.Name, host));
        }

        // Separate module procedure interfaces access their host; other interface bodies only see IMPORTed names.
        var body = new Scope(ScopeKind.Procedure, parent, _doc) { Owner = e, IsInterfaceBody = parent.Kind == ScopeKind.Interface && !sepInterface, Start = t[0] };
        e.Body = body;
        e.Header = Text(t, start, t.Count);
        AddDocs(e, st);
        Push(FrameKind.Unit, body);
        Def(st, nameTok, e);
        if (spec is not null)
        {
            ApplyType(e, spec, t, body);
            RecordTypeSpec(st, spec, body);
        }

        foreach (var a in argIdx)
        {
            var d = ws.Track(new Entity(t[a].Lower, t[a].Text, EntityKind.Dummy, body) { DefToken = t[a] });
            body.Entities[d.Name] = d;
            e.Args.Add(d);
            Def(st, t[a], d);
        }

        if (resultIdx > 0)
        {
            var r = Declare(body, t[resultIdx], EntityKind.Variable);
            e.Result = r;
            Def(st, t[resultIdx], r);
        }
        else if (isFunction)
        {
            body.Entities[e.Name] = e;
        }
    }

    private void Entry(Stmt st, int i)
    {
        var t = st.Tokens;
        if (!IsName(t, i))
        {
            return;
        }

        var unit = Current.Unit;
        var owner = unit.Owner;
        var host = owner?.Scope ?? _file;
        var external = owner is null || owner.IsExternalProc || host.Kind == ScopeKind.File;
        var kind = owner?.Kind == EntityKind.Function ? EntityKind.Function : EntityKind.Subroutine;
        var e = ws.Track(new Entity(t[i].Lower, t[i].Text, kind, host) { DefToken = t[i], IsExternalProc = external });
        host.Entities.TryAdd(e.Name, e);
        if (external)
        {
            ws.Procs.TryAdd(e.Name, e);
        }

        e.Header = Text(t, 0, t.Count);
        AddDocs(e, st);
        Def(st, t[i], e);
        if (IsOp(t, i + 1, "("))
        {
            var close = Match(t, i + 1);
            foreach (var (s, _) in SplitTop(t, i + 2, close))
            {
                if (IsName(t, s))
                {
                    var d = Declare(unit, t[s], EntityKind.Dummy);
                    e.Args.Add(d);
                    Def(st, t[s], d);
                }
            }
        }
    }

    private bool End(Stmt st, string kw, int next)
    {
        var t = st.Tokens;
        Frame? f;
        switch (kw)
        {
            case "end":
                f = PopTo(st, FrameKind.Unit);
                break;
            case "endsubroutine":
            case "endfunction":
            case "endprogram":
            case "endmodule":
            case "endsubmodule":
            case "endblockdata":
            case "endprocedure":
                f = PopTo(st, FrameKind.Unit);
                if (f?.Scope?.Owner is { } owner && IsName(t, next) && t[next].Lower == owner.Name)
                {
                    RefE(st, t[next], owner);
                }

                return true;
            case "endinterface":
                f = PopTo(st, FrameKind.Interface);
                if (f?.Scope?.Generic is { } g && next < t.Count && ParseGenericSpec(t, next) is { } spec && spec.Key == g.Name)
                {
                    RefE(st, t[spec.TokenIndex], g);
                }

                return true;
            case "endtype":
                f = PopTo(st, FrameKind.Type);
                if (f?.Scope?.Owner is { } type && IsName(t, next))
                {
                    RefE(st, t[next], type);
                }

                return true;
            case "endenum":
                PopTo(st, FrameKind.Enum);
                return true;
            case "enddo":
                f = PopTo(st, FrameKind.Do);
                break;
            case "endif":
                f = PopTo(st, FrameKind.If);
                break;
            case "endselect":
                f = PopTo(st, FrameKind.SelectCase, FrameKind.SelectType, FrameKind.SelectRank);
                break;
            case "endblock":
                f = PopTo(st, FrameKind.Block);
                break;
            case "endassociate":
                f = PopTo(st, FrameKind.Associate);
                break;
            case "endwhere":
                f = PopTo(st, FrameKind.Where);
                break;
            case "endforall":
                f = PopTo(st, FrameKind.Forall);
                break;
            case "endcritical":
                f = PopTo(st, FrameKind.Critical);
                break;
            case "endteam":
                f = PopTo(st, FrameKind.Team);
                WalkParen(st, next);
                break;
            default:
                return false;
        }

        if (f?.Construct is { } c && IsName(t, next) && t[next].Lower == c.Name)
        {
            RefE(st, t[next], c);
        }

        return true;
    }

    // ---------------- interfaces, types, bindings ----------------

    private sealed record GenericSpec(string Key, string Display, int TokenIndex, int End);

    /// <summary>Parses a generic-spec: a name, <c>operator(op)</c>, <c>assignment(=)</c> or <c>read|write(formatted|unformatted)</c>.</summary>
    private static GenericSpec? ParseGenericSpec(List<Token> t, int j)
    {
        if (!IsName(t, j))
        {
            return null;
        }

        var word = t[j].Lower;
        if (word is "operator" or "assignment" or "read" or "write" && IsOp(t, j + 1, "("))
        {
            var close = Match(t, j + 1);
            if (close > j + 2 && close < t.Count)
            {
                var op = string.Concat(t.Skip(j + 2).Take(close - j - 2).Select(static x => x.Lower));
                op = op switch
                {
                    ".eq." => "==",
                    ".ne." => "/=",
                    ".lt." => "<",
                    ".le." => "<=",
                    ".gt." => ">",
                    ".ge." => ">=",
                    _ => op,
                };
                var display = word + "(" + string.Concat(t.Skip(j + 2).Take(close - j - 2).Select(static x => x.Text)) + ")";
                return new GenericSpec(word + "(" + op + ")", display, j + 2, close + 1);
            }
        }

        return new GenericSpec(word, t[j].Text, j, j + 1);
    }

    public static string OperatorKey(Token dotOp)
    {
        var op = dotOp.Lower switch
        {
            ".eq." => "==",
            ".ne." => "/=",
            ".lt." => "<",
            ".le." => "<=",
            ".gt." => ">",
            ".ge." => ">=",
            var x => x,
        };
        return "operator(" + op + ")";
    }

    private Entity DeclareGeneric(Scope scope, GenericSpec spec, Stmt st, EntityKind kind = EntityKind.Generic)
    {
        var t = st.Tokens;
        var dict = kind == EntityKind.Generic ? scope.Generics : scope.Entities;
        if (!dict.TryGetValue(spec.Key, out var g) || g.Kind != kind)
        {
            g = ws.Track(new Entity(spec.Key, spec.Display, kind, scope) { DefToken = t[spec.TokenIndex], Signature = Text(t, 0, t.Count) });
            dict[spec.Key] = g;
        }

        AddDocs(g, st);
        Def(st, t[spec.TokenIndex], g);
        return g;
    }

    private void Interface(Stmt st, int i, bool isAbstract)
    {
        var t = st.Tokens;
        var parent = Current;
        var container = new Scope(ScopeKind.Interface, parent, _doc) { AbstractInterface = isAbstract, Start = t[0] };
        if (!isAbstract && ParseGenericSpec(t, i) is { } spec)
        {
            container.Generic = DeclareGeneric(parent, spec, st);
        }

        Push(FrameKind.Interface, container);
    }

    private void ProcedureStatement(Stmt st, int i, bool moduleKeyword)
    {
        var t = st.Tokens;
        var scope = Current;
        var j = i + 1;
        if (scope.Kind == ScopeKind.Interface)
        {
            if (IsOp(t, j, "::"))
            {
                j++;
            }

            var enclosing = scope.Parent ?? _file;
            foreach (var (s, _) in SplitTop(t, j, t.Count))
            {
                if (IsName(t, s))
                {
                    scope.Generic?.Specifics.Add((t[s].Lower, enclosing));
                    st.Actions.Add(r => r.RefKey(t[s], enclosing, t[s].Lower, LookupCtx.Specific, false));
                }
            }

            return;
        }

        if (scope.Kind == ScopeKind.DerivedType)
        {
            if (scope.AfterContains)
            {
                Bindings(st, j, scope);
            }
            else
            {
                ProcedureDecl(st, j, scope, EntityKind.Component);
            }

            return;
        }

        if (moduleKeyword && scope.Kind is ScopeKind.Submodule or ScopeKind.Module && IsName(t, j))
        {
            // Separate module procedure implementation: `module procedure f` (interface declared in the ancestor).
            var nameTok = t[j];
            var e = ws.Track(new Entity(nameTok.Lower, nameTok.Text, EntityKind.Subroutine, scope) { DefToken = nameTok, IsSeparateImpl = true });
            scope.Entities[e.Name] = e;
            var body = new Scope(ScopeKind.Procedure, scope, _doc) { Owner = e, Start = t[0] };
            e.Body = body;
            e.Header = Text(t, 0, t.Count);
            AddDocs(e, st);
            Push(FrameKind.Unit, body);
            Def(st, nameTok, e);
            return;
        }

        ProcedureDecl(st, j, scope, EntityKind.ProcPointer);
    }

    /// <summary><c>procedure(iface) [, attrs] :: p [=&gt; init]</c>: procedure pointers, dummy procedures and procedure components.</summary>
    private void ProcedureDecl(Stmt st, int j, Scope scope, EntityKind kind)
    {
        var t = st.Tokens;
        string? iface = null;
        var typeText = "procedure";
        if (IsOp(t, j, "("))
        {
            var close = Match(t, j);
            if (IsName(t, j + 1))
            {
                iface = t[j + 1].Lower;
                RefName(st, t[j + 1], scope, LookupCtx.Proc);
            }

            typeText = Text(t, j - 1, close + 1);
            j = close + 1;
        }

        var attrs = new List<string>();
        if (IsOp(t, j, ","))
        {
            var dc = FindTop(t, j, t.Count, "::");
            foreach (var (s, e) in SplitTop(t, j + 1, dc < 0 ? t.Count : dc))
            {
                attrs.Add(Text(t, s, e));
            }

            j = dc < 0 ? t.Count : dc + 1;
        }
        else if (IsOp(t, j, "::"))
        {
            j++;
        }

        foreach (var item in ParseDecls(t, j, t.Count))
        {
            var e = Declare(scope, t[item.Name], kind);
            e.TypeText = typeText;
            e.Attrs.AddRange(attrs);
            foreach (var a in attrs)
            {
                if (a.Equals("public", StringComparison.OrdinalIgnoreCase) || a.Equals("private", StringComparison.OrdinalIgnoreCase))
                {
                    scope.Access[e.Name] = a.Equals("public", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (iface is not null)
            {
                e.ImplName ??= iface;
            }

            AddDocs(e, st);
            Def(st, t[item.Name], e);
            WalkItem(st, item, scope);
        }
    }

    private void Bindings(Stmt st, int j, Scope typeScope)
    {
        var t = st.Tokens;
        var host = typeScope.Parent ?? _file;
        if (IsOp(t, j, "("))
        {
            var close = Match(t, j);
            if (IsName(t, j + 1))
            {
                RefName(st, t[j + 1], host, LookupCtx.Proc);
            }

            j = close + 1;
        }

        var attrs = new List<string>();
        if (IsOp(t, j, ","))
        {
            var dc = FindTop(t, j, t.Count, "::");
            foreach (var (s, e) in SplitTop(t, j + 1, dc < 0 ? t.Count : dc))
            {
                attrs.Add(Text(t, s, e).ToLowerInvariant());
            }

            j = dc < 0 ? t.Count : dc + 1;
        }
        else if (IsOp(t, j, "::"))
        {
            j++;
        }

        var deferred = attrs.Contains("deferred");
        foreach (var (s, end) in SplitTop(t, j, t.Count))
        {
            if (!IsName(t, s))
            {
                continue;
            }

            var b = ws.Track(new Entity(t[s].Lower, t[s].Text, EntityKind.Binding, typeScope) { DefToken = t[s], Signature = Text(t, 0, t.Count) });
            typeScope.Entities[b.Name] = b;
            b.Attrs.AddRange(attrs);
            AddDocs(b, st);
            Def(st, t[s], b);
            if (IsOp(t, s + 1, "=>") && IsName(t, s + 2) && s + 2 < end)
            {
                b.ImplName = t[s + 2].Lower;
                RefName(st, t[s + 2], host, LookupCtx.Specific);
            }
            else if (!deferred)
            {
                b.ImplName = b.Name;
            }
        }
    }

    private void GenericBinding(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        if (scope.Kind != ScopeKind.DerivedType || scope.Owner is null)
        {
            Walk(st, j, t.Count, scope);
            return;
        }

        var dc = FindTop(t, j, t.Count, "::");
        if (dc < 0 || ParseGenericSpec(t, dc + 1) is not { } spec)
        {
            return;
        }

        var g = DeclareGeneric(scope, spec, st, EntityKind.GenericBinding);
        var arrow = FindTop(t, spec.End, t.Count, "=>");
        if (arrow < 0)
        {
            return;
        }

        var type = scope.Owner;
        foreach (var (s, _) in SplitTop(t, arrow + 1, t.Count))
        {
            if (IsName(t, s))
            {
                g.Specifics.Add((t[s].Lower, scope));
                var tok = t[s];
                st.Actions.Add(r => r.RefMember(tok, type, tok.Lower));
            }
        }
    }

    private void Final(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        if (IsOp(t, j, "::"))
        {
            j++;
        }

        var host = scope.Kind == ScopeKind.DerivedType ? scope.Parent ?? _file : scope;
        foreach (var (s, _) in SplitTop(t, j, t.Count))
        {
            if (IsName(t, s))
            {
                RefName(st, t[s], host, LookupCtx.Specific);
            }
        }
    }

    private void DerivedType(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        string? parentName = null;
        var attrs = new List<string>();
        if (IsOp(t, j, ","))
        {
            var dc = FindTop(t, j, t.Count, "::");
            foreach (var (s, end) in SplitTop(t, j + 1, dc < 0 ? t.Count : dc))
            {
                attrs.Add(Text(t, s, end));
                if (t[s].IsKw("extends") && IsOp(t, s + 1, "(") && IsName(t, s + 2))
                {
                    parentName = t[s + 2].Lower;
                    RefName(st, t[s + 2], scope, LookupCtx.Type);
                }
                else if (t[s].IsKw("public") || t[s].IsKw("private"))
                {
                    // accessibility recorded below, once the name is known
                }
            }

            j = dc < 0 ? t.Count : dc + 1;
        }
        else if (IsOp(t, j, "::"))
        {
            j++;
        }

        if (!IsName(t, j))
        {
            return;
        }

        var nameTok = t[j];
        var e = ws.Track(new Entity(nameTok.Lower, nameTok.Text, EntityKind.DerivedType, scope) { DefToken = nameTok, ParentTypeName = parentName });
        scope.Entities[e.Name] = e;
        foreach (var a in attrs)
        {
            if (a.Equals("public", StringComparison.OrdinalIgnoreCase) || a.Equals("private", StringComparison.OrdinalIgnoreCase))
            {
                scope.Access[e.Name] = a.Equals("public", StringComparison.OrdinalIgnoreCase);
            }
        }

        var body = new Scope(ScopeKind.DerivedType, scope, _doc) { Owner = e, Start = t[0] };
        e.Body = body;
        e.Header = Text(t, 0, t.Count);
        e.Attrs.AddRange(attrs);
        AddDocs(e, st);
        Def(st, nameTok, e);
        if (IsOp(t, j + 1, "("))
        {
            var close = Match(t, j + 1);
            foreach (var (s, _) in SplitTop(t, j + 2, close))
            {
                if (IsName(t, s))
                {
                    var p = Declare(body, t[s], EntityKind.TypeParam);
                    Def(st, t[s], p);
                }
            }
        }

        Push(FrameKind.Type, body);
    }

    private void Guard(Stmt st, int i)
    {
        var t = st.Tokens;
        var frame = _frames.LastOrDefault(static f => f.Kind == FrameKind.SelectType);
        var assoc = frame?.Assoc;
        Token? typeTok = null;
        if (i >= 0 && IsOp(t, i, "("))
        {
            var close = Match(t, i);
            if (IsName(t, i + 1) && !IntrinsicTypeNames.Contains(t[i + 1].Lower))
            {
                typeTok = t[i + 1];
                RefName(st, typeTok, Current, LookupCtx.Type);
                Walk(st, i + 2, close, Current);
            }
            else
            {
                Walk(st, i + 1, close, Current);
            }

            ConstructRef(st, close + 1);
        }
        else
        {
            ConstructRef(st, Keyword(t, 0).Next);
        }

        if (assoc is not null)
        {
            var scope = Current;
            st.Actions.Add(r => r.SetGuard(assoc, typeTok, scope));
        }
    }

    private void Enumerator(Stmt st, int j)
    {
        var t = st.Tokens;
        if (IsOp(t, j, "::"))
        {
            j++;
        }

        var scope = Current;
        foreach (var item in ParseDecls(t, j, t.Count))
        {
            var e = Declare(scope, t[item.Name], EntityKind.Enumerator);
            e.TypeText = "enumerator";
            if (item.Init is { } init)
            {
                e.Init = Text(t, init.Start, init.End);
            }

            AddDocs(e, st);
            Def(st, t[item.Name], e);
            WalkItem(st, item, scope);
        }
    }

    // ---------------- use / import / implicit / access ----------------

    private void Use(Stmt st, int j)
    {
        var t = st.Tokens;
        var intrinsic = false;
        if (IsOp(t, j, ","))
        {
            intrinsic = IsKw(t, j + 1, "intrinsic");
            j += 2;
        }

        if (IsOp(t, j, "::"))
        {
            j++;
        }

        if (!IsName(t, j))
        {
            return;
        }

        var modTok = t[j];
        var u = new UseInfo(modTok.Lower, modTok) { Intrinsic = intrinsic };
        Current.Uses.Add(u);
        st.Actions.Add(r => r.RefModule(modTok, u));
        j++;
        if (!IsOp(t, j, ","))
        {
            return;
        }

        List<(int Start, int End)> items;
        if (IsKw(t, j + 1, "only") && IsOp(t, j + 2, ":"))
        {
            u.Only = new Dictionary<string, string>(StringComparer.Ordinal);
            items = SplitTop(t, j + 3, t.Count);
        }
        else
        {
            items = SplitTop(t, j + 1, t.Count);
        }

        foreach (var (s, _) in items)
        {
            if (ParseGenericSpec(t, s) is not { } local)
            {
                continue;
            }

            var remote = local;
            if (IsOp(t, local.End, "=>") && ParseGenericSpec(t, local.End + 1) is { } r)
            {
                remote = r;
            }

            if (u.Only is not null)
            {
                u.Only[local.Key] = remote.Key;
            }
            else if (remote != local)
            {
                u.Renames[local.Key] = remote.Key;
            }

            var remoteTok = t[remote.TokenIndex];
            var remoteKey = remote.Key;
            st.Actions.Add(rs => rs.RefUseItem(remoteTok, u, remoteKey));
            if (remote != local)
            {
                var localTok = t[local.TokenIndex];
                st.Actions.Add(rs => rs.RefUseItem(localTok, u, remoteKey));
            }
        }
    }

    private void Import(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        if (IsOp(t, j, ","))
        {
            var mode = Lower(t, j + 1);
            if (mode is "all")
            {
                scope.ImportAll = true;
                return;
            }

            if (mode is "none")
            {
                return;
            }

            j += 2;
            if (IsOp(t, j, ":"))
            {
                j++;
            }
        }

        if (IsOp(t, j, "::"))
        {
            j++;
        }

        if (j >= t.Count)
        {
            scope.ImportAll = true;
            return;
        }

        var enclosing = scope.Parent?.Kind == ScopeKind.Interface ? scope.Parent.Parent ?? _file : scope.Parent ?? _file;
        foreach (var (s, _) in SplitTop(t, j, t.Count))
        {
            if (IsName(t, s))
            {
                scope.Imports.Add(t[s].Lower);
                RefName(st, t[s], enclosing, LookupCtx.Expr);
            }
        }
    }

    private void Implicit(Stmt st, int j)
    {
        var t = st.Tokens;
        var unit = Current.Unit;
        if (IsKw(t, j, "none"))
        {
            unit.ImplicitNone = true;
            return;
        }

        foreach (var (s, e) in SplitTop(t, j, t.Count))
        {
            // type-spec (letter-spec-list); the last parenthesised group is the letter list.
            var lastOpen = -1;
            for (var k = e - 1; k >= s; k--)
            {
                if (t[k].Is(")"))
                {
                    var depth = 0;
                    for (var m = k; m >= s; m--)
                    {
                        if (t[m].Is(")"))
                        {
                            depth++;
                        }
                        else if (t[m].Is("("))
                        {
                            depth--;
                            if (depth == 0)
                            {
                                lastOpen = m;
                                break;
                            }
                        }
                    }

                    break;
                }
            }

            if (lastOpen <= s)
            {
                continue;
            }

            var typeText = Text(t, s, lastOpen).ToLowerInvariant();
            if (ParseTypeSpec(t, s) is { } spec && spec.End <= lastOpen)
            {
                RecordTypeSpec(st, spec, Current);
            }

            unit.ImplicitTypes ??= new string?[26];
            var close = Match(t, lastOpen);
            foreach (var (ls, le) in SplitTop(t, lastOpen + 1, close))
            {
                var from = Lower(t, ls);
                var to = le - ls >= 3 && IsOp(t, ls + 1, "-") ? Lower(t, ls + 2) : from;
                if (from.Length == 1 && to.Length == 1 && from[0] is >= 'a' and <= 'z' && to[0] is >= 'a' and <= 'z')
                {
                    for (var c = from[0]; c <= to[0]; c++)
                    {
                        unit.ImplicitTypes[c - 'a'] = typeText;
                    }
                }
            }
        }
    }

    private void Access(Stmt st, int j, bool isPublic)
    {
        var t = st.Tokens;
        var scope = Current;
        if (IsOp(t, j, "::"))
        {
            j++;
        }

        if (j >= t.Count)
        {
            if (scope.Kind is ScopeKind.Module or ScopeKind.DerivedType && !scope.AfterContains)
            {
                scope.DefaultPrivate = !isPublic;
            }

            return;
        }

        foreach (var (s, _) in SplitTop(t, j, t.Count))
        {
            if (ParseGenericSpec(t, s) is { } spec)
            {
                scope.Access[spec.Key] = isPublic;
                RefKey(st, t[spec.TokenIndex], scope, spec.Key, LookupCtx.Expr);
            }
        }
    }

    // ---------------- specification statements ----------------

    private void AttributeStatement(Stmt st, string kw, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        var attrText = kw;
        if (kw is "intent" or "bind" && IsOp(t, j, "("))
        {
            var close = Match(t, j);
            attrText = Text(t, j - 1, close + 1).ToLowerInvariant();
            j = close + 1;
        }

        if (IsOp(t, j, "::"))
        {
            j++;
        }

        foreach (var (s, e) in SplitTop(t, j, t.Count))
        {
            if (IsOp(t, s, "/") && IsName(t, s + 1))
            {
                var blockTok = t[s + 1];
                st.Actions.Add(r => r.RefCommon(blockTok));
                continue;
            }

            if (!IsName(t, s))
            {
                continue;
            }

            var tok = t[s];
            var existed = scope.Entities.ContainsKey(tok.Lower);
            var ent = Declare(scope, tok, kw == "external" ? EntityKind.Function : EntityKind.Variable);
            switch (kw)
            {
                case "external":
                    ent.IsExternalProc = true;
                    ent.IsExternalDecl = ent.Body is null;
                    if (ent.Kind is EntityKind.Variable)
                    {
                        ent.Kind = EntityKind.Function;
                    }

                    break;
                case "intrinsic":
                    ent.Kind = EntityKind.Intrinsic;
                    continue;
                default:
                    if (!ent.Attrs.Contains(attrText))
                    {
                        ent.Attrs.Add(attrText);
                    }

                    break;
            }

            if (IsOp(t, s + 1, "(") || IsOp(t, s + 1, "["))
            {
                var close = Match(t, s + 1);
                ent.IsArray |= t[s + 1].Is("(");
                ent.Dims = Text(t, s + 1, close + 1);
                Walk(st, s + 1, Math.Min(e, close + 1), scope);
            }

            if (existed && kw != "external")
            {
                RefE(st, tok, ent);
            }
            else
            {
                Def(st, tok, ent);
            }
        }
    }

    private void Parameter(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        var (a, b) = IsOp(t, j, "(") ? (j + 1, Match(t, j)) : (j, t.Count);
        foreach (var (s, e) in SplitTop(t, a, b))
        {
            if (!IsName(t, s) || !IsOp(t, s + 1, "="))
            {
                continue;
            }

            var ent = Declare(scope, t[s], EntityKind.NamedConstant);
            if (ent.Kind is EntityKind.Variable)
            {
                ent.Kind = EntityKind.NamedConstant;
            }

            if (!ent.Attrs.Contains("parameter"))
            {
                ent.Attrs.Add("parameter");
            }

            ent.Init = Text(t, s + 2, e);
            AddDocs(ent, st);
            Def(st, t[s], ent);
            Walk(st, s + 2, e, scope);
        }
    }

    private void Common(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        var n = t.Count;
        var blockName = "";
        Token? blockTok = null;
        while (j < n)
        {
            if (IsOp(t, j, ","))
            {
                j++;
                continue;
            }

            if (IsOp(t, j, "//"))
            {
                blockName = "";
                blockTok = null;
                j++;
            }
            else if (IsOp(t, j, "/"))
            {
                if (IsName(t, j + 1) && IsOp(t, j + 2, "/"))
                {
                    blockTok = t[j + 1];
                    blockName = blockTok.Lower;
                    j += 3;
                }
                else if (IsOp(t, j + 1, "/"))
                {
                    blockName = "";
                    blockTok = null;
                    j += 2;
                }
                else
                {
                    j++;
                }
            }

            if (!ws.Commons.TryGetValue(blockName, out var block))
            {
                block = ws.Track(new Entity(blockName, blockTok?.Text ?? "", EntityKind.CommonBlock, _file)
                {
                    DefToken = blockTok, Signature = "common /" + (blockTok?.Text ?? "") + "/",
                });
                ws.Commons[blockName] = block;
            }

            if (blockTok is not null)
            {
                AddDocs(block, st);
                Def(st, blockTok, block);
            }

            // members up to the next block name
            var end = j;
            var depth = 0;
            while (end < n)
            {
                if (t[end].Is("(") || t[end].Is("["))
                {
                    depth++;
                }
                else if (t[end].Is(")") || t[end].Is("]"))
                {
                    depth--;
                }
                else if (depth == 0 && (t[end].Is("/") || t[end].Is("//")))
                {
                    break;
                }

                end++;
            }

            foreach (var item in ParseDecls(t, j, end))
            {
                var m = Declare(scope, t[item.Name], EntityKind.CommonMember);
                if (m.Kind is EntityKind.Variable or EntityKind.CommonMember)
                {
                    m.Kind = EntityKind.CommonMember;
                    m.Common = block;
                }

                if (item.Dims is { } dims)
                {
                    m.IsArray = true;
                    m.Dims = Text(t, dims.Start, dims.End);
                }

                Def(st, t[item.Name], m);
                WalkItem(st, item, scope);
            }

            if (end == j)
            {
                end++;
            }

            j = end;
        }
    }

    private void Namelist(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        while (j < t.Count)
        {
            if (IsOp(t, j, "/") && IsName(t, j + 1) && IsOp(t, j + 2, "/"))
            {
                var e = Declare(scope, t[j + 1], EntityKind.Namelist);
                e.Signature = Text(t, 0, t.Count);
                AddDocs(e, st);
                Def(st, t[j + 1], e);
                j += 3;
                continue;
            }

            if (IsName(t, j))
            {
                RefName(st, t[j], scope, LookupCtx.Expr, allowImplicit: true);
            }

            j++;
        }
    }

    private sealed record TypeSpec(int Start, int End, string Text, int DerivedIndex, List<(int Start, int End)> Walks);

    private static TypeSpec? ParseTypeSpec(List<Token> t, int i)
    {
        var kw = Lower(t, i);
        var j = i + 1;
        switch (kw)
        {
            case "integer" or "real" or "complex" or "logical" or "character" or "byte" or "doubleprecision" or "doublecomplex":
                break;
            case "double" when IsKw(t, i + 1, "precision") || IsKw(t, i + 1, "complex"):
                j = i + 2;
                break;
            case "type" or "class" when IsOp(t, i + 1, "("):
            {
                var close = Match(t, i + 1);
                var walks = new List<(int, int)>();
                var derived = -1;
                var inner = i + 2;
                if (IsName(t, inner) && !IntrinsicTypeNames.Contains(t[inner].Lower) && !t[inner].IsKw("double"))
                {
                    derived = inner;
                    if (IsOp(t, inner + 1, "("))
                    {
                        walks.Add((inner + 1, Match(t, inner + 1) + 1));
                    }
                }
                else if (inner < close)
                {
                    walks.Add((inner, close));
                }

                var end = Math.Min(close + 1, t.Count);
                return new TypeSpec(i, end, Text(t, i, end), derived, walks);
            }

            default:
                return null;
        }

        var w = new List<(int, int)>();
        if (IsOp(t, j, "*"))
        {
            if (IsOp(t, j + 1, "("))
            {
                var c = Match(t, j + 1);
                w.Add((j + 1, c + 1));
                j = c + 1;
            }
            else if (j + 1 < t.Count && t[j + 1].Kind == TokenKind.Int)
            {
                j += 2;
            }
        }

        if (IsOp(t, j, "("))
        {
            var c = Match(t, j);
            w.Add((j, c + 1));
            j = c + 1;
        }

        j = Math.Min(j, t.Count);
        return new TypeSpec(i, j, Text(t, i, j), -1, w);
    }

    private static void RecordTypeSpec(Stmt st, TypeSpec spec, Scope scope)
    {
        if (spec.DerivedIndex >= 0)
        {
            RefName(st, st.Tokens[spec.DerivedIndex], scope, LookupCtx.Type);
        }

        foreach (var (a, b) in spec.Walks)
        {
            Walk(st, a, b, scope);
        }
    }

    private static void ApplyType(Entity e, TypeSpec spec, List<Token> t, Scope scope)
    {
        e.TypeText = spec.Text;
        e.TypeName = spec.DerivedIndex >= 0 ? t[spec.DerivedIndex].Lower : null;
        e.TypeScope = scope;
        e.TypeResolved = false;
    }

    private void TypeDecl(Stmt st, TypeSpec spec)
    {
        var t = st.Tokens;
        var scope = Current;
        var j = spec.End;
        var attrs = new List<(string Name, string Text, int Start, int End)>();
        if (IsOp(t, j, ","))
        {
            var dc = FindTop(t, j, t.Count, "::");
            foreach (var (s, e) in SplitTop(t, j + 1, dc < 0 ? t.Count : dc))
            {
                attrs.Add((Lower(t, s), Text(t, s, e), s, e));
            }

            j = dc < 0 ? t.Count : dc + 1;
        }
        else if (IsOp(t, j, "::"))
        {
            j++;
        }

        RecordTypeSpec(st, spec, scope);
        string? attrDims = null;
        foreach (var (name, _, s, e) in attrs)
        {
            if (name is "dimension" or "codimension" && s + 1 < e)
            {
                if (name == "dimension")
                {
                    attrDims = Text(t, s + 1, e);
                }

                Walk(st, s + 1, e, scope);
            }
        }

        var inType = scope.Kind == ScopeKind.DerivedType;
        var isParam = attrs.Any(static a => a.Name == "parameter");
        var isKindLen = inType && attrs.Any(static a => a.Name is "kind" or "len");
        var isExternal = attrs.Any(static a => a.Name == "external");
        var isIntrinsic = attrs.Any(static a => a.Name == "intrinsic");
        foreach (var item in ParseDecls(t, j, t.Count))
        {
            var tok = t[item.Name];
            var kind = inType ? isKindLen ? EntityKind.TypeParam : EntityKind.Component : isParam ? EntityKind.NamedConstant : EntityKind.Variable;
            var e = Declare(scope, tok, kind);
            if (e.Kind == EntityKind.Variable && isParam)
            {
                e.Kind = EntityKind.NamedConstant;
            }

            if (isIntrinsic)
            {
                e.Kind = EntityKind.Intrinsic;
            }

            if (isExternal)
            {
                e.IsExternalProc = true;
                e.IsExternalDecl = e.Body is null;
                if (e.Kind is EntityKind.Variable)
                {
                    e.Kind = EntityKind.Function;
                }
            }

            if (e.Kind is EntityKind.Function or EntityKind.Subroutine && e.Body is not null && e.TypeText is null)
            {
                // `real :: f` inside function f declares the result type.
                ApplyType(e, spec, t, scope);
            }
            else if (e.Kind is not (EntityKind.Function or EntityKind.Subroutine) || e.IsExternalProc)
            {
                ApplyType(e, spec, t, scope);
            }

            if (item.CharLen is { } len)
            {
                e.TypeText = spec.Text + "*" + Text(t, len.Start, len.End);
            }

            foreach (var a in attrs)
            {
                if (a.Name != "dimension" && !e.Attrs.Contains(a.Text))
                {
                    e.Attrs.Add(a.Text);
                }

                if (a.Name is "public" or "private")
                {
                    scope.Access[e.Name] = a.Name == "public";
                }
            }

            if (item.Dims is { } dims)
            {
                e.Dims = Text(t, dims.Start, dims.End);
                e.IsArray = true;
            }
            else if (attrDims is not null)
            {
                e.Dims = attrDims;
                e.IsArray = true;
            }

            if (item.Init is { } init)
            {
                e.Init = (item.PointerInit ? "=> " : "= ") + Text(t, init.Start, init.End);
            }

            AddDocs(e, st);
            Def(st, tok, e);
            WalkItem(st, item, scope);
        }
    }

    private sealed record DeclItem(int Name, (int Start, int End)? Dims, (int Start, int End)? CoDims, (int Start, int End)? CharLen,
        (int Start, int End)? Init, bool PointerInit, (int Start, int End)? OldInit);

    private static List<DeclItem> ParseDecls(List<Token> t, int a, int b)
    {
        var items = new List<DeclItem>();
        foreach (var (s, e) in SplitTop(t, a, b))
        {
            if (!IsName(t, s))
            {
                continue;
            }

            var k = s + 1;
            (int, int)? dims = null, codims = null, charLen = null, init = null, oldInit = null;
            var pointer = false;
            if (IsOp(t, k, "(") && k < e)
            {
                var c = Match(t, k);
                dims = (k, Math.Min(c + 1, e));
                k = c + 1;
            }

            if (IsOp(t, k, "[") && k < e)
            {
                var c = Match(t, k);
                codims = (k, Math.Min(c + 1, e));
                k = c + 1;
            }

            if (IsOp(t, k, "*") && k + 1 < e)
            {
                if (IsOp(t, k + 1, "("))
                {
                    var c = Match(t, k + 1);
                    charLen = (k + 1, Math.Min(c + 1, e));
                    k = c + 1;
                }
                else
                {
                    charLen = (k + 1, k + 2);
                    k += 2;
                }
            }

            if (dims is null && IsOp(t, k, "(") && k < e)
            {
                var c = Match(t, k);
                dims = (k, Math.Min(c + 1, e));
                k = c + 1;
            }

            if ((IsOp(t, k, "=") || IsOp(t, k, "=>")) && k < e)
            {
                pointer = t[k].Is("=>");
                init = (k + 1, e);
            }
            else if (IsOp(t, k, "/") && k < e)
            {
                oldInit = (k, e);
            }

            items.Add(new DeclItem(s, dims, codims, charLen, init, pointer, oldInit));
        }

        return items;
    }

    private static void WalkItem(Stmt st, DeclItem item, Scope scope)
    {
        foreach (var range in (ReadOnlySpan<(int Start, int End)?>)[item.Dims, item.CoDims, item.CharLen, item.Init, item.OldInit])
        {
            if (range is { } r)
            {
                Walk(st, r.Start, r.End, scope);
            }
        }
    }

    // ---------------- executable statements ----------------

    private void Assignment(Stmt st, int i)
    {
        var t = st.Tokens;
        var scope = Current;
        var unit = scope.Unit;
        if (IsOp(t, i + 1, "(") && !unit.SeenExecutable && unit.Kind is ScopeKind.Procedure or ScopeKind.Program
            && (!scope.Entities.TryGetValue(t[i].Lower, out var existing) || existing.Kind is EntityKind.Variable && !existing.IsArray
                && existing.TypeText?.StartsWith("character", StringComparison.OrdinalIgnoreCase) != true)
            && IsStatementFunctionArgs(t, i + 1))
        {
            var close = Match(t, i + 1);
            if (IsOp(t, close + 1, "="))
            {
                var e = Declare(scope, t[i], EntityKind.StatementFunction);
                e.Kind = EntityKind.StatementFunction;
                e.Header = Text(t, i, t.Count);
                AddDocs(e, st);
                Def(st, t[i], e);
                Walk(st, i + 1, t.Count, scope);
                return;
            }
        }

        unit.SeenExecutable = true;
        Walk(st, i, t.Count, scope);
    }

    private static bool IsStatementFunctionArgs(List<Token> t, int open)
    {
        var close = Match(t, open);
        for (var k = open + 1; k < close; k++)
        {
            if (!(t[k].IsName || t[k].Is(",")))
            {
                return false;
            }
        }

        return true;
    }

    private void Call(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        if (!IsName(t, j))
        {
            Walk(st, j, t.Count, scope);
            return;
        }

        var k = j + 1;
        while (k < t.Count && (t[k].Is("(") || t[k].Is("[")))
        {
            k = Match(t, k) + 1;
        }

        if (IsOp(t, k, "%"))
        {
            Walk(st, j, t.Count, scope);
            return;
        }

        RefProc(st, t[j], scope);
        Walk(st, j + 1, t.Count, scope);
        AltReturnLabels(st, j + 1, scope);
    }

    private static void AltReturnLabels(Stmt st, int from, Scope scope)
    {
        var t = st.Tokens;
        for (var k = from; k + 1 < t.Count; k++)
        {
            if ((t[k].Is("*") || t[k].Is("&")) && t[k + 1].Kind == TokenKind.Int && (t[k - 1].Is("(") || t[k - 1].Is(",")))
            {
                RefLabel(st, t[k + 1], scope);
            }
        }
    }

    private void If(Stmt st, int j, Entity? construct)
    {
        var t = st.Tokens;
        var scope = Current;
        if (!IsOp(t, j, "("))
        {
            Walk(st, j, t.Count, scope);
            return;
        }

        var close = Match(t, j);
        Walk(st, j, close + 1, scope);
        var after = close + 1;
        if (after >= t.Count)
        {
            return;
        }

        if (t[after].IsKw("then") && after + 1 >= t.Count)
        {
            Push(FrameKind.If, null, construct);
            return;
        }

        if (t[after].Kind == TokenKind.Int)
        {
            // arithmetic IF: IF (e) l1, l2, l3
            for (var k = after; k < t.Count; k++)
            {
                if (t[k].Kind == TokenKind.Int)
                {
                    RefLabel(st, t[k], scope);
                }
            }

            return;
        }

        Handle(st, after);
    }

    private void ElseIf(Stmt st, int j)
    {
        var t = st.Tokens;
        if (IsOp(t, j, "("))
        {
            var close = Match(t, j);
            Walk(st, j, close + 1, Current);
            var k = close + 1;
            if (IsKw(t, k, "then"))
            {
                k++;
            }

            ConstructRef(st, k);
        }
    }

    private void ElseLike(Stmt st, int j)
    {
        var t = st.Tokens;
        if (IsOp(t, j, "("))
        {
            var close = Match(t, j);
            Walk(st, j, close + 1, Current);
            j = close + 1;
        }

        ConstructRef(st, j);
    }

    private void CaseLike(Stmt st, int j)
    {
        var t = st.Tokens;
        if (IsOp(t, j, "("))
        {
            var close = Match(t, j);
            Walk(st, j, close + 1, Current);
            ConstructRef(st, close + 1);
        }
        else if (IsKw(t, j, "default"))
        {
            ConstructRef(st, j + 1);
        }
    }

    /// <summary>A construct name operand (EXIT name, END DO name, ELSE name, ...): resolved against the open constructs.</summary>
    private void ConstructRef(Stmt st, int j)
    {
        var t = st.Tokens;
        if (!IsName(t, j))
        {
            return;
        }

        for (var k = _frames.Count - 1; k >= 0; k--)
        {
            if (_frames[k].Construct is { } c && c.Name == t[j].Lower)
            {
                RefE(st, t[j], c);
                return;
            }

            if (_frames[k].Kind == FrameKind.Unit)
            {
                break;
            }
        }
    }

    private void Do(Stmt st, int j, Entity? construct)
    {
        var t = st.Tokens;
        var scope = Current;
        var labelled = false;
        if (j < t.Count && t[j].Kind == TokenKind.Int)
        {
            RefLabel(st, t[j], scope);
            labelled = true;
            j++;
        }

        if (IsOp(t, j, ","))
        {
            j++;
        }

        if (IsKw(t, j, "while") || IsKw(t, j, "concurrent"))
        {
            j++;
        }

        Walk(st, j, t.Count, scope);
        if (!labelled)
        {
            Push(FrameKind.Do, null, construct);
        }
    }

    private void GoTo(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        if (j < t.Count && t[j].Kind == TokenKind.Int)
        {
            RefLabel(st, t[j], scope);
            return;
        }

        if (IsOp(t, j, "("))
        {
            // computed GO TO (l1, l2, ...) [,] expr
            var close = Match(t, j);
            for (var k = j + 1; k < close; k++)
            {
                if (t[k].Kind == TokenKind.Int)
                {
                    RefLabel(st, t[k], scope);
                }
            }

            Walk(st, close + 1, t.Count, scope);
            return;
        }

        // assigned GO TO var [, (l1, l2)]
        var open = -1;
        for (var k = j; k < t.Count; k++)
        {
            if (t[k].Is("("))
            {
                open = k;
                break;
            }
        }

        Walk(st, j, open < 0 ? t.Count : open, scope);
        if (open >= 0)
        {
            for (var k = open + 1; k < t.Count; k++)
            {
                if (t[k].Kind == TokenKind.Int)
                {
                    RefLabel(st, t[k], scope);
                }
            }
        }
    }

    private void Assign(Stmt st, int j)
    {
        var t = st.Tokens;
        if (j < t.Count && t[j].Kind == TokenKind.Int)
        {
            RefLabel(st, t[j], Current);
        }

        if (IsKw(t, j + 1, "to"))
        {
            Walk(st, j + 2, t.Count, Current);
        }
    }

    /// <summary><c>STOP [code] [, QUIET = expr]</c>: the QUIET= specifier name is not a variable.</summary>
    private void StopStatement(Stmt st, int j)
    {
        var t = st.Tokens;
        var comma = FindTop(t, j, t.Count, ",");
        Walk(st, j, comma < 0 ? t.Count : comma, Current);
        if (comma > 0 && IsName(t, comma + 1) && IsOp(t, comma + 2, "="))
        {
            Walk(st, comma + 3, t.Count, Current);
        }
    }

    private void WalkParen(Stmt st, int j)
    {
        var t = st.Tokens;
        if (IsOp(t, j, "("))
        {
            Walk(st, j, Match(t, j) + 1, Current);
        }
    }

    private void WhereLike(Stmt st, int j, Entity? construct, FrameKind kind)
    {
        var t = st.Tokens;
        if (!IsOp(t, j, "("))
        {
            return;
        }

        var close = Match(t, j);
        Walk(st, j, close + 1, Current);
        if (close + 1 < t.Count)
        {
            Handle(st, close + 1);
        }
        else
        {
            Push(kind, null, construct);
        }
    }

    private void SelectType(Stmt st, int j, Entity? construct, FrameKind kind)
    {
        var t = st.Tokens;
        var outer = Current;
        var scope = new Scope(ScopeKind.SelectType, outer, _doc);
        Entity? assoc = null;
        if (IsOp(t, j, "("))
        {
            var close = Match(t, j);
            var inner = j + 1;
            var arrow = FindTop(t, inner, close, "=>");
            if (arrow > 0 && IsName(t, inner))
            {
                assoc = ws.Track(new Entity(t[inner].Lower, t[inner].Text, EntityKind.Variable, scope) { DefToken = t[inner] });
                scope.Entities[assoc.Name] = assoc;
                Def(st, t[inner], assoc);
                Walk(st, arrow + 1, close, outer);
                var a = assoc;
                st.Actions.Add(r => r.InferAssociate(a, st, arrow + 1, close, outer));
            }
            else
            {
                if (IsName(t, inner) && inner + 1 == close)
                {
                    assoc = ws.Track(new Entity(t[inner].Lower, t[inner].Text, EntityKind.Variable, scope) { AliasOf = t[inner].Lower });
                    scope.Entities[assoc.Name] = assoc;
                }

                Walk(st, inner, close, outer);
            }
        }

        Push(kind, scope, construct, assoc);
    }

    private void Associate(Stmt st, int j, Entity? construct)
    {
        var t = st.Tokens;
        var outer = Current;
        var scope = new Scope(ScopeKind.Associate, outer, _doc);
        if (IsOp(t, j, "("))
        {
            var close = Match(t, j);
            foreach (var (s, e) in SplitTop(t, j + 1, close))
            {
                if (IsName(t, s) && IsOp(t, s + 1, "=>"))
                {
                    var a = ws.Track(new Entity(t[s].Lower, t[s].Text, EntityKind.Variable, scope) { DefToken = t[s], Signature = Text(t, s, e) });
                    scope.Entities[a.Name] = a;
                    Def(st, t[s], a);
                    Walk(st, s + 2, e, outer);
                    var from = s + 2;
                    st.Actions.Add(r => r.InferAssociate(a, st, from, e, outer));
                }
                else
                {
                    Walk(st, s, e, outer);
                }
            }
        }

        Push(FrameKind.Associate, scope, construct);
    }

    private void Allocate(Stmt st, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        if (!IsOp(t, j, "("))
        {
            return;
        }

        var close = Match(t, j);
        var dc = FindTop(t, j + 1, close, "::");
        var from = j;
        if (dc > 0)
        {
            if (ParseTypeSpec(t, j + 1) is { } spec)
            {
                RecordTypeSpec(st, spec, scope);
            }
            else if (IsName(t, j + 1))
            {
                RefName(st, t[j + 1], scope, LookupCtx.Type);
                if (IsOp(t, j + 2, "("))
                {
                    Walk(st, j + 2, Match(t, j + 2) + 1, scope);
                }
            }

            from = dc + 1;
            Walk(st, from, close, scope);
            return;
        }

        Walk(st, from, close + 1, scope);
    }

    private void Io(Stmt st, string kw, int j)
    {
        var t = st.Tokens;
        var scope = Current;
        if (IsOp(t, j, "("))
        {
            var close = Match(t, j);
            var position = 0;
            foreach (var (s, e) in SplitTop(t, j + 1, close))
            {
                if (IsName(t, s) && IsOp(t, s + 1, "="))
                {
                    var key = t[s].Lower;
                    var value = s + 2;
                    if (key is "err" or "end" or "eor" or "fmt" && value < e && t[value].Kind == TokenKind.Int)
                    {
                        RefLabel(st, t[value], scope);
                    }
                    else if (key == "nml" && IsName(t, value))
                    {
                        RefName(st, t[value], scope, LookupCtx.Expr);
                    }
                    else
                    {
                        Walk(st, value, e, scope);
                    }
                }
                else if (position == 1 && kw is "read" or "write" && s < e && t[s].Kind == TokenKind.Int && e - s == 1)
                {
                    RefLabel(st, t[s], scope);
                }
                else
                {
                    Walk(st, s, e, scope);
                }

                position++;
            }

            Walk(st, close + 1, t.Count, scope);
            return;
        }

        // PRINT fmt, list / READ fmt, list
        if (j < t.Count && t[j].Kind == TokenKind.Int)
        {
            RefLabel(st, t[j], scope);
        }

        Walk(st, j, t.Count, scope);
    }
}
