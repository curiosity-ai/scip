using ScipSharp.Indexer.Pascal.Syntax;
using ScipKind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Pascal.Semantics;

/// <summary>
/// One-pass parser and binder for a Pascal compile unit, modelled on how a Pascal compiler works: declarations are
/// entered into scopes as they are parsed and every identifier is resolved at the point of use (static nested
/// scoping, <c>with</c> scopes, class member lookup through ancestors, unit scoping in <c>uses</c> order). A unit is
/// compiled in two phases (interface, implementation) so that circular implementation-section <c>uses</c> work.
/// The parser is error tolerant: it never throws on malformed input and resynchronises on <c>;</c> and keywords.
/// </summary>
internal sealed partial class Compiler
{
    private readonly Workspace _ws;
    private readonly CompileUnit _unit;
    private readonly List<Token> _toks;
    private readonly PascalMode _mode;
    private readonly List<Layer> _layers = [];
    private readonly List<CompileUnit> _intfUses = [];
    private readonly List<CompileUnit> _implUses = [];
    private int _i;
    private bool _inImplementation;
    private bool _typeContext;
    private Occurrence? _lastOcc;

    /// <summary>The method whose body is being compiled (for <c>inherited</c>).</summary>
    private Sym? _currentMethod;

    public Compiler(Workspace ws, CompileUnit unit, List<Token> tokens, PascalMode mode)
    {
        _ws = ws;
        _unit = unit;
        _toks = tokens;
        _mode = mode;
    }

    private TypeSystem Types => _ws.Types;

    private Emitter Emit => _ws.Emit;

    public IReadOnlyList<CompileUnit> UsedUnits => [.. _intfUses, .. _implUses];

    // ---------------------------------------------------------------- tokens

    private Token Cur => _toks[_i];

    private Token Peek(int n = 1) => _toks[Math.Min(_i + n, _toks.Count - 1)];

    private Token Prev => _toks[Math.Max(0, _i - 1)];

    private bool AtEof => Cur.Kind == TokenKind.Eof;

    private Token Next()
    {
        var t = _toks[_i];
        if (_i < _toks.Count - 1)
        {
            _i++;
        }

        return t;
    }

    private bool IsSym(string s) => Cur.IsSymbol(s);

    private bool IsWord(string w) => Cur.IsWord(w);

    private bool IsKw(string w) => Cur.IsKeyword(w);

    private bool Accept(string symbol)
    {
        if (Cur.IsSymbol(symbol))
        {
            Next();
            return true;
        }

        return false;
    }

    private bool AcceptWord(string word)
    {
        if (Cur.IsWord(word))
        {
            Next();
            return true;
        }

        return false;
    }

    private bool IsIdent => Cur.Kind == TokenKind.Identifier;

    /// <summary>Identifiers, plus keywords that may name members after a dot (<c>x.&amp;end</c> is rare; <c>x.Create</c> common).</summary>
    private bool IsNameLike => Cur.Kind is TokenKind.Identifier or TokenKind.Keyword;

    private Token? AcceptIdent() => IsIdent ? Next() : null;

    /// <summary>
    /// A non-ISO reserved word (<c>string</c>, <c>object</c>, <c>operator</c>, ...) in a declaration's name position
    /// means the source is classic Pascal using it as an identifier: demote it to an identifier for the rest of the file.
    /// </summary>
    private bool DemoteKeywordName()
    {
        if (Cur.Kind != TokenKind.Keyword || Preprocessor.IsReserved(Cur.Lower, PascalMode.Iso))
        {
            return false;
        }

        var n = Peek();
        if (!(n.IsSymbol(":") || n.IsSymbol(",") || n.IsSymbol("=") || n.IsSymbol(")") || n.IsSymbol(";")))
        {
            return false;
        }

        var lower = Cur.Lower;
        for (var k = _i; k < _toks.Count; k++)
        {
            if (_toks[k].Kind == TokenKind.Keyword && _toks[k].Lower == lower)
            {
                _toks[k].Kind = TokenKind.Identifier;
            }
        }

        return true;
    }

    /// <summary>Skips a balanced bracket group starting at the current <c>(</c> or <c>[</c>.</summary>
    private void SkipBalanced()
    {
        var depth = 0;
        do
        {
            if (IsSym("(") || IsSym("["))
            {
                depth++;
            }
            else if (IsSym(")") || IsSym("]"))
            {
                depth--;
            }

            Next();
        }
        while (depth > 0 && !AtEof);
    }

    /// <summary>Skips to the next <c>;</c> (consumed) at bracket depth 0, stopping before block keywords.</summary>
    private void SkipToSemicolon()
    {
        var depth = 0;
        while (!AtEof)
        {
            if (IsSym("(") || IsSym("["))
            {
                depth++;
            }
            else if ((IsSym(")") || IsSym("]")) && depth > 0)
            {
                depth--;
            }
            else if (depth == 0 && IsSym(";"))
            {
                Next();
                return;
            }
            else if (depth == 0 && Cur.Kind == TokenKind.Keyword && Cur.Lower is "begin" or "end" or "implementation" or "var" or "const" or "type" or "procedure" or "function")
            {
                return;
            }

            Next();
        }
    }

    // ---------------------------------------------------------------- scopes

    private int Mark() => _layers.Count;

    private void Restore(int mark) => _layers.RemoveRange(mark, _layers.Count - mark);

    private void Push(Layer layer) => _layers.Add(layer);

    /// <summary>Looks a name up through nested scopes, then the unit and its <c>uses</c> (last unit first), then System.</summary>
    private List<Sym>? Lookup(string name)
    {
        for (var i = _layers.Count - 1; i >= 0; i--)
        {
            if (_layers[i].Find(name) is { } found)
            {
                return found;
            }
        }

        if (_inImplementation && _unit.Implementation.Find(name) is { } impl)
        {
            return impl;
        }

        if (_unit.Interface.Find(name) is { } intf)
        {
            return intf;
        }

        for (var i = _implUses.Count - 1; i >= 0; i--)
        {
            if (_implUses[i].Interface.Find(name) is { } u)
            {
                return u;
            }
        }

        for (var i = _intfUses.Count - 1; i >= 0; i--)
        {
            if (_intfUses[i].Interface.Find(name) is { } u)
            {
                return u;
            }
        }

        return _ws.System.Interface.Find(name) ?? _programParams?.Find(name);
    }

    /// <summary>ISO program parameters not declared as variables (implementation-defined external files such as P4's prr).</summary>
    private Scope? _programParams;

    /// <summary>Scope receiving unit-level declarations.</summary>
    private Scope UnitScope => _inImplementation ? _unit.Implementation : _unit.Interface;

    /// <summary>Declaration context: where names go and how their symbols are formed.</summary>
    private sealed class Ctx
    {
        public required Sym Owner { get; init; }

        public required Scope Scope { get; init; }

        /// <summary>Constants, types, variables and labels declared here are document-local symbols.</summary>
        public bool LocalData { get; init; }

        /// <summary>Routine headings here are followed by bodies (implementation section, program, routine bodies).</summary>
        public bool AllowBodies { get; init; }

        /// <summary>The structured type whose members are being declared.</summary>
        public PType? MemberOf { get; init; }

        public bool IsInterfaceType => MemberOf?.Kind == TypeKind.Interface;
    }

    private static string? ChildSymbol(Sym owner, string descriptor) => owner.Symbol is null ? null : owner.Symbol + descriptor;

    private Sym Declare(Ctx ctx, Token name, SymKind kind, string descriptor, ScipKind scipKind, Token? docStart = null, bool addToScope = true)
    {
        var local = (ctx.LocalData && kind != SymKind.Routine) || ctx.Owner.Symbol is null || kind is SymKind.Param or SymKind.Label or SymKind.Result;
        var sym = new Sym(name.Text, kind, ctx.Owner)
        {
            Symbol = local ? null : ctx.Owner.Symbol + descriptor,
            ScipKind = scipKind,
            HomeSource = name.Source,
        };
        sym.Documentation = DocFor(docStart ?? name);
        if (addToScope)
        {
            ctx.Scope.Add(sym);
        }

        return sym;
    }

    /// <summary>Doc comment above the token at <paramref name="start"/>.</summary>
    private string? DocFor(Token start)
    {
        // A bounded window: a start token further back than 64 tokens was never found, but searching for it scanned to
        // the end of the file (quadratic for declarations whose name precedes a large type or nested records).
        var lo = Math.Max(0, _i - 64);
        var idx = _toks.IndexOf(start, lo, Math.Min(_toks.Count, _i + 65) - lo);
        var prevEnd = 0;
        if (idx > 0 && ReferenceEquals(_toks[idx - 1].Source, start.Source))
        {
            prevEnd = _toks[idx - 1].End;
        }

        return DocComments.Above(start.Source, prevEnd, start.Start);
    }

    /// <summary>A trailing <c>// comment</c> after <paramref name="end"/> on the same line.</summary>
    private string? TrailingDoc(Token end)
    {
        var next = Cur;
        return DocComments.Trailing(end.Source, end.End, ReferenceEquals(next.Source, end.Source) && next.Start > end.End ? next.Start : int.MaxValue);
    }

    /// <summary>Source text between two tokens (same file), comment-free and whitespace-collapsed.</summary>
    private static string Slice(Token first, Token last, int max = 400)
    {
        if (!ReferenceEquals(first.Source, last.Source) || last.End < first.Start)
        {
            return first.Text;
        }

        return Names.CleanSignature(first.Source.Text[first.Start..last.End], max);
    }

    // ---------------------------------------------------------------- unit structure

    /// <summary>Compiles the header and interface part (for programs/libraries: everything).</summary>
    public void CompileInterface()
    {
        _unit.State = UnitState.InInterface;
        var unitSym = _unit.Sym!;
        var ctx = new Ctx { Owner = unitSym, Scope = _unit.Interface, AllowBodies = _unit.Kind != UnitFileKind.Unit };
        if (IsKw("unit") || IsWord("module"))
        {
            var isModule = IsWord("module");
            var start = Next();
            var (first, last) = DottedName();
            if (first is not null)
            {
                unitSym.Documentation ??= DocComments.Above(start.Source, 0, start.Start);
                Emit.Definition(first, last!, unitSym);
            }

            SkipHints();
            if (isModule)
            {
                ModuleBody(ModulePart());
                _unit.State = UnitState.Done;
                return;
            }

            Accept(";");
            if (IsKw("interface"))
            {
                Next();
            }

            UsesClause(_intfUses);
            Declarations(ctx);
            _unit.State = UnitState.InterfaceDone;
            return;
        }

        // program / library / headerless program
        if (IsKw("program") || IsKw("library") || IsWord("package"))
        {
            var start = Next();
            var (first, last) = DottedName();
            if (first is not null)
            {
                unitSym.Documentation ??= DocComments.Above(start.Source, 0, start.Start);
                Emit.Definition(first, last!, unitSym);
            }

            List<Token>? programParams = null;
            if (IsSym("("))
            {
                Next();
                programParams = [];
                while (!AtEof && !IsSym(")"))
                {
                    if (IsIdent)
                    {
                        programParams.Add(Next());
                    }
                    else
                    {
                        Next();
                    }
                }

                Accept(")");
            }

            Accept(";");
            if (programParams is not null)
            {
                _programParams = new Scope(unitSym);
                foreach (var p in programParams)
                {
                    _programParams.Add(new Sym(p.Text, SymKind.Var, unitSym)
                    {
                        Symbol = unitSym.Symbol + Names.Term(p.Text),
                        ScipKind = ScipKind.Variable,
                        Type = _ws.System.Interface.Find("Text")?[0].Type,
                        Signature = $"{p.Text}: text (program parameter)",
                        HomeSource = p.Source,
                    });
                }
            }

            ProgramBlock(ctx, programParams);
        }
        else
        {
            // Headerless program (TP allows it) or a header preceded by unparsable text: skip junk up to a header or a section.
            while (!AtEof && !IsKw("program") && !IsKw("uses") && !IsKw("begin") && !IsKw("const") && !IsKw("type") && !IsKw("var")
                && !IsKw("label") && !IsRoutineStart())
            {
                Next();
            }

            if (IsKw("program"))
            {
                CompileInterface();
                return;
            }

            ProgramBlock(ctx, null);
        }

        _unit.State = UnitState.Done;
    }

    private void ProgramBlock(Ctx ctx, List<Token>? programParams)
    {
        if (IsWord("import"))
        {
            ImportClause();
        }

        UsesClause(_intfUses);
        Declarations(ctx);
        ResolveProgramParams(programParams);
        if (IsKw("begin"))
        {
            Next();
            StatementList("end");
            if (IsKw("end"))
            {
                Next();
            }
        }
        else if (IsKw("end"))
        {
            Next();
        }
    }

    private void ResolveProgramParams(List<Token>? programParams)
    {
        if (programParams is null)
        {
            return;
        }

        foreach (var p in programParams)
        {
            if (Lookup(p.Text) is { } found)
            {
                if (_programParams?.Find(p.Text) is { } implicitParam && ReferenceEquals(found[0], implicitParam[0]))
                {
                    Emit.Definition(p, found[0]);
                }
                else
                {
                    Emit.Reference(p, found[0]);
                }
            }
        }
    }

    /// <summary>Compiles the implementation part of a unit.</summary>
    public void CompileImplementation()
    {
        if (_unit.State == UnitState.Done)
        {
            return;
        }

        _unit.State = UnitState.InImplementation;
        _inImplementation = true;
        var ctx = new Ctx { Owner = _unit.Sym!, Scope = _unit.Implementation, AllowBodies = true };
        if (IsKw("implementation"))
        {
            Next();
            UsesClause(_implUses);
        }

        Declarations(ctx);
        if (IsKw("initialization"))
        {
            Next();
            StatementList("end", "finalization");
        }

        if (IsKw("finalization"))
        {
            Next();
            StatementList("end");
        }

        if (IsKw("begin"))
        {
            Next();
            StatementList("end");
        }

        _unit.State = UnitState.Done;
    }

    /// <summary>After <c>module Name</c>: reads <c>interface</c>/<c>implementation</c> and returns the declaration context.</summary>
    private Ctx ModulePart()
    {
        var isInterface = AcceptWord("interface");
        var isImplementation = AcceptWord("implementation");
        if (IsSym("("))
        {
            SkipBalanced(); // module parameters
        }

        Accept(";");
        _inImplementation = isImplementation;
        return new Ctx { Owner = _unit.Sym!, Scope = isImplementation ? _unit.Implementation : _unit.Interface, AllowBodies = !isInterface };
    }

    /// <summary>
    /// Extended Pascal (ISO 10206) module, best effort: <c>export</c> lists are skipped, <c>import</c> works like
    /// <c>uses</c>, an interface module and its implementation module may follow each other in one file.
    /// </summary>
    private void ModuleBody(Ctx ctx)
    {
        while (!AtEof)
        {
            var before = _i;
            if (IsWord("export"))
            {
                SkipToSemicolon();
                continue;
            }

            if (IsWord("import"))
            {
                ImportClause();
                continue;
            }

            if (IsWord("module"))
            {
                Next();
                var (first, last) = DottedName();
                if (first is not null && string.Equals(DottedText(first, last!), _unit.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Emit.Reference(first, last!, _unit.Sym!);
                }

                ctx = ModulePart();
                continue;
            }

            Declarations(ctx);
            if (IsKw("begin") || (IsWord("to") && (Peek().IsWord("begin") || Peek().IsWord("end"))))
            {
                if (IsWord("to"))
                {
                    Next(); // to begin do / to end do
                    Next();
                    AcceptWord("do");
                }
                else
                {
                    Next();
                }

                Statement();
                if (IsKw("end") && !Peek().IsSymbol("."))
                {
                    Next();
                }

                Accept(";");
                continue;
            }

            if (IsKw("end"))
            {
                Next();
                Accept(";");
                Accept(".");
            }

            if (_i == before)
            {
                Next();
            }
        }
    }

    /// <summary>Extended Pascal <c>import A; B qualified only (x, y);</c>.</summary>
    private void ImportClause()
    {
        Next(); // import
        while (IsIdent && !AtEof)
        {
            var (first, last) = DottedName();
            if (first is not null)
            {
                AddUsedUnit(_intfUses, first, last!, null);
            }

            AcceptWord("qualified");
            if (AcceptWord("only") && IsSym("("))
            {
                SkipBalanced();
            }

            if (!Accept(";"))
            {
                break;
            }
        }
    }

    private (Token? First, Token? Last) DottedName()
    {
        if (!IsNameLike)
        {
            return (null, null);
        }

        var first = Next();
        var last = first;
        while (IsSym(".") && Peek().Kind is TokenKind.Identifier or TokenKind.Keyword)
        {
            Next();
            last = Next();
        }

        return (first, last);
    }

    private static string DottedText(Token first, Token last) =>
        ReferenceEquals(first, last) ? first.Text : Names.CleanSignature(first.Source.Text[first.Start..last.End]).Replace(" ", "", StringComparison.Ordinal);

    private void UsesClause(List<CompileUnit> target)
    {
        while (IsKw("uses") || IsWord("uses"))
        {
            Next();
            while (!AtEof)
            {
                var (first, last) = DottedName();
                if (first is null)
                {
                    break;
                }

                string? inPath = null;
                if (IsKw("in") && Peek().Kind == TokenKind.String)
                {
                    Next();
                    var s = Next().Text;
                    inPath = s.Length >= 2 ? s[1..^1].Replace("''", "'", StringComparison.Ordinal) : s;
                }

                AddUsedUnit(target, first, last!, inPath);
                if (!Accept(","))
                {
                    break;
                }
            }

            if (!Accept(";"))
            {
                SkipToSemicolon();
            }
        }
    }

    private void AddUsedUnit(List<CompileUnit> target, Token first, Token last, string? inPath)
    {
        var name = DottedText(first, last);
        var used = _ws.UseUnit(name, inPath, _unit);
        if (used.Sym is { } usym)
        {
            Emit.Reference(first, last, usym, (int)SymbolRole.Import);
        }

        if (!ReferenceEquals(used, _unit) && !target.Contains(used))
        {
            target.Add(used);
        }
    }

    /// <summary>Finds a unit (own or used) named by the dotted identifiers at the cursor; returns the number of name tokens matched.</summary>
    private (CompileUnit? Unit, int Parts) MatchUnitName()
    {
        var parts = new List<string> { Cur.Text };
        var k = 1;

        // No unit name has more parts than the longest candidate: bounding the chain keeps a.b.c... (thousands of
        // members) linear instead of joining every prefix.
        var maxParts = MaxUnitNameParts();
        while (k < maxParts && Peek(2 * k - 1).IsSymbol(".") && Peek(2 * k).Kind is TokenKind.Identifier or TokenKind.Keyword)
        {
            parts.Add(Peek(2 * k).Text);
            k++;
        }

        // Longest dotted prefix naming a unit, but leave at least one member after it when the prefix is the whole chain.
        for (var n = parts.Count; n >= 1; n--)
        {
            var name = string.Join('.', parts.Take(n));
            var unit = FindUnitByName(name);
            if (unit is not null)
            {
                return (unit, n);
            }
        }

        return (null, 0);
    }

    private int MaxUnitNameParts()
    {
        static int Parts(string name) => name.AsSpan().Count('.') + 1;
        var max = Math.Max(Parts(_unit.Name), 1);
        foreach (var u in _implUses)
        {
            max = Math.Max(max, Parts(u.Name));
        }

        foreach (var u in _intfUses)
        {
            max = Math.Max(max, Parts(u.Name));
        }

        return max;
    }

    private CompileUnit? FindUnitByName(string name)
    {
        if (string.Equals(_unit.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            return _unit;
        }

        foreach (var u in _implUses)
        {
            if (string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return u;
            }
        }

        foreach (var u in _intfUses)
        {
            if (string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return u;
            }
        }

        return string.Equals(name, "System", StringComparison.OrdinalIgnoreCase) ? _ws.System : null;
    }

    private void SkipHints()
    {
        while (IsWord("deprecated") || IsWord("platform") || IsWord("experimental") || IsWord("library") || IsWord("unimplemented"))
        {
            Next();
            if (Cur.Kind == TokenKind.String)
            {
                Next();
            }
        }
    }
}
