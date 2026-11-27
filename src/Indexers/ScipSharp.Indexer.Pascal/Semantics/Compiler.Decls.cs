using ScipSharp.Indexer.Pascal.Syntax;
using ScipKind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Pascal.Semantics;

internal sealed partial class Compiler
{
    /// <summary>Pointer types (<c>^TNode</c>) whose target is resolved at the end of the enclosing <c>type</c> section.</summary>
    private List<(Token Name, PType Pointer)>? _pendingPointers;

    /// <summary>Last token of a structured type's header (for signatures).</summary>
    private Token? _typeHeaderEnd;

    private void Declarations(Ctx ctx)
    {
        while (!AtEof)
        {
            var before = _i;
            if (IsKw("const") || IsKw("resourcestring"))
            {
                Next();
                ConstSection(ctx);
            }
            else if (IsKw("type"))
            {
                Next();
                TypeSection(ctx);
            }
            else if (IsKw("var") || IsKw("threadvar"))
            {
                Next();
                VarSection(ctx);
            }
            else if (IsKw("label"))
            {
                Next();
                LabelSection(ctx);
            }
            else if (IsRoutineStart())
            {
                RoutineDecl(ctx);
            }
            else if (IsKw("exports"))
            {
                Next();
                Exports();
            }
            else if (IsKw("uses"))
            {
                UsesClause(_inImplementation ? _implUses : _intfUses);
            }
            else if (IsWord("export") && ctx.Owner.Kind == SymKind.Unit)
            {
                SkipToSemicolon();
            }
            else if (IsSym("[") || IsSym(";"))
            {
                if (IsSym("["))
                {
                    SkipBalanced();
                }
                else
                {
                    Next();
                }
            }
            else
            {
                break;
            }

            if (_i == before)
            {
                break;
            }
        }
    }

    private bool IsRoutineStart(int at = 0)
    {
        var t = Peek(at);
        if (t.Kind == TokenKind.Keyword && t.Lower is "procedure" or "function" or "constructor" or "destructor" or "operator")
        {
            return true;
        }

        if (t.IsWord("operator") && Peek(at + 1).Kind == TokenKind.Symbol)
        {
            return true;
        }

        if (t.IsWord("class") || t.IsWord("generic"))
        {
            var n = Peek(at + 1);
            return n.Lower is "procedure" or "function" or "constructor" or "destructor" or "operator"
                || (t.IsWord("class") && n.IsWord("generic"));
        }

        return false;
    }

    private void Exports()
    {
        while (!AtEof && !IsSym(";"))
        {
            if (IsIdent)
            {
                var tok = Next();
                if (Lookup(tok.Text) is { } found)
                {
                    Emit.Reference(tok, found[0]);
                }
            }
            else
            {
                Next();
            }

            while (IsWord("name") || IsWord("index") || IsWord("resident"))
            {
                Next();
                if (!IsSym(",") && !IsSym(";"))
                {
                    Expr();
                }
            }

            Accept(",");
        }

        Accept(";");
    }

    // ---------------------------------------------------------------- const / var / label

    private void ConstSection(Ctx ctx)
    {
        while ((IsIdent || DemoteKeywordName()) && (Peek().IsSymbol("=") || Peek().IsSymbol(":")))
        {
            var name = Next();
            PType? type = null;
            if (Accept(":"))
            {
                type = TypeSpec(null, ctx).Type;
            }

            var sym = Declare(ctx, name, SymKind.Const, Names.Term(name.Text), ScipKind.Constant);
            if (Accept("="))
            {
                var value = ConstValue(type);
                type ??= value.Type;
            }

            sym.Type = type;
            SkipHints();
            var last = Prev;
            sym.Signature = "const " + Slice(name, last);
            if (Accept(";"))
            {
                sym.Documentation ??= TrailingDoc(Prev);
            }

            Emit.Definition(name, sym, name, last);
        }
    }

    /// <summary>A constant value; structured (record/array) constants use the expected type to resolve field names.</summary>
    private ExprInfo ConstValue(PType? expected)
    {
        if (!IsSym("("))
        {
            return Expr();
        }

        var t = expected is null ? null : TypeSystem.Structural(expected);
        var looksLikeRecord = Peek().IsIdentifier && Peek(2).IsSymbol(":");
        if (t is { Kind: TypeKind.Record or TypeKind.Object or TypeKind.Class } || (t is null && looksLikeRecord))
        {
            Next();
            while (!AtEof && !IsSym(")"))
            {
                var before = _i;
                if (IsIdent && Peek().IsSymbol(":"))
                {
                    var field = Next();
                    Next();
                    PType? ft = null;
                    if (t is not null && Types.FindMember(t, field.Text) is { } members)
                    {
                        Emit.Reference(field, members[0]);
                        ft = TypeSystem.ValueType(members[0], expected);
                    }

                    ConstValue(ft);
                }
                else
                {
                    ConstValue(null);
                }

                if (!Accept(";") && !Accept(",") && _i == before)
                {
                    Next();
                }
            }

            Accept(")");
            return new ExprInfo(null, expected, false);
        }

        if (t is { Kind: TypeKind.Array } || (t is null && !looksLikeRecord && LooksLikeAggregate()))
        {
            Next();
            while (!AtEof && !IsSym(")"))
            {
                var before = _i;
                ConstValue(t?.Elem);
                if (!Accept(",") && _i == before)
                {
                    Next();
                }
            }

            Accept(")");
            return new ExprInfo(null, expected, false);
        }

        return Expr();
    }

    /// <summary>True when the parenthesised group at the cursor contains a top-level comma (an array constant).</summary>
    private bool LooksLikeAggregate()
    {
        var depth = 0;
        for (var k = 0; k < 4096; k++)
        {
            var t = Peek(k);
            if (t.Kind == TokenKind.Eof)
            {
                return false;
            }

            if (t.IsSymbol("(") || t.IsSymbol("["))
            {
                depth++;
            }
            else if (t.IsSymbol(")") || t.IsSymbol("]"))
            {
                depth--;
                if (depth == 0)
                {
                    return false;
                }
            }
            else if (depth == 1 && (t.IsSymbol(",") || t.IsSymbol(";")))
            {
                return true;
            }
        }

        return false;
    }

    private void VarSection(Ctx ctx)
    {
        while ((IsIdent || DemoteKeywordName()) && (Peek().IsSymbol(":") || Peek().IsSymbol(",")))
        {
            var names = new List<Token> { Next() };
            while (Accept(","))
            {
                if (IsIdent || DemoteKeywordName())
                {
                    names.Add(Next());
                }
            }

            if (!Accept(":"))
            {
                SkipToSemicolon();
                continue;
            }

            var typeStart = Cur;
            var type = TypeSpec(null, ctx).Type;
            var typeEnd = Prev;
            if (AcceptWord("absolute"))
            {
                Expr();
                if (Accept(":"))
                {
                    Expr(); // TP segment:offset
                }
            }

            if (Accept("=") || AcceptWord("value"))
            {
                ConstValue(type);
            }

            SkipHints();
            while (IsSym(";") && Peek().Lower is "cvar" or "external" or "public" or "export" or "weakexternal")
            {
                Next();
                Next();
                while (Cur.Kind == TokenKind.String || IsWord("name"))
                {
                    Next();
                }
            }

            var typeText = Slice(typeStart, typeEnd, 200);
            string? trailing = null;
            if (Accept(";"))
            {
                trailing = TrailingDoc(Prev);
            }

            foreach (var n in names)
            {
                var sym = Declare(ctx, n, SymKind.Var, Names.Term(n.Text), ctx.MemberOf is null ? ScipKind.Variable : ScipKind.StaticField, names[0]);
                sym.Type = type;
                sym.Signature = $"var {n.Text}: {typeText}";
                sym.Documentation ??= trailing;
                Emit.Definition(n, sym);
            }
        }
    }

    private void LabelSection(Ctx ctx)
    {
        while (!AtEof && !IsSym(";"))
        {
            if (IsIdent || Cur.Kind == TokenKind.Number)
            {
                var tok = Next();
                var sym = Declare(ctx, tok, SymKind.Label, Names.Term(tok.Text), ScipKind.UnspecifiedKind);
                sym.Signature = "label " + tok.Text;
                Emit.Definition(tok, sym);
            }
            else if (!Accept(","))
            {
                break;
            }
        }

        Accept(";");
    }

    // ---------------------------------------------------------------- type sections

    private void TypeSection(Ctx ctx)
    {
        var saved = _pendingPointers;
        _pendingPointers = [];
        while (!AtEof)
        {
            DemoteKeywordName();
            var isGeneric = IsWord("generic") && Peek().IsIdentifier;
            if (!isGeneric && !(IsIdent && (Peek().IsSymbol("=") || Peek().IsSymbol("<"))))
            {
                break;
            }

            var before = _i;
            TypeDecl(ctx);
            if (_i == before)
            {
                Next();
            }
        }

        foreach (var (name, ptr) in _pendingPointers)
        {
            if (LookupType(name) is { } target)
            {
                Emit.Reference(name, target);
                ptr.Elem = target.Type;
            }
        }

        _pendingPointers = saved;
    }

    /// <summary>Resolves a type name (inferring an external type when needed).</summary>
    private Sym? LookupType(Token name)
    {
        if (Lookup(name.Text) is { } found)
        {
            return found.FirstOrDefault(static s => s.Kind is SymKind.Type or SymKind.TypeParam) ?? found[0];
        }

        return ResolveUnknown(name, InferHint.Type);
    }

    private void TypeDecl(Ctx ctx)
    {
        var declStart = Cur;
        AcceptWord("generic");
        var name = Next();
        List<Token>? typeParams = null;
        if (IsSym("<"))
        {
            typeParams = TypeParamNames();
        }

        if (!Accept("="))
        {
            SkipToSemicolon();
            return;
        }

        if (IsKw("type") && !Peek().IsWord("helper"))
        {
            Next();
        }

        var existing = ctx.Scope.Find(name.Text)?.FirstOrDefault(static s => s.Kind == SymKind.Type && s.IsForwardType);
        var sym = existing ?? Declare(ctx, name, SymKind.Type, Names.Type(name.Text), ScipKind.Type, declStart);
        if (existing is not null)
        {
            existing.Documentation ??= DocFor(declStart);
        }

        var mark = Mark();
        if (typeParams is not null)
        {
            var tpScope = new Scope(sym);
            sym.TypeParams = [];
            for (var i = 0; i < typeParams.Count; i++)
            {
                var tp = DeclareTypeParam(sym, typeParams[i], i);
                sym.TypeParams.Add(tp);
                tpScope.Add(tp);
            }

            Push(tpScope);
        }

        _typeHeaderEnd = null;
        var startOfSpec = Cur;
        var (type, forward) = TypeSpec(sym, ctx, existing?.Type);
        var headerEnd = _typeHeaderEnd ?? Prev;
        Restore(mark);
        if (type.Decl is null || ReferenceEquals(type.Decl, sym))
        {
            type.Decl = sym;
            sym.ScipKind = KindOf(type);
        }
        else
        {
            sym.ScipKind = ScipKind.TypeAlias;
        }

        sym.Type = type;
        sym.IsForwardType = forward;
        SkipHints();
        var structured = type.IsStructured || type.Kind == TypeKind.Enum;
        var sigEnd = structured && type.Kind != TypeKind.Enum ? headerEnd : Prev;
        sym.Signature = "type " + Slice(name, ReferenceEquals(sigEnd.Source, name.Source) && sigEnd.End >= startOfSpec.Start ? sigEnd : Prev, 300);
        var end = Cur.IsSymbol(";") ? Cur : Prev;
        Accept(";");
        Emit.Definition(name, sym, name, end, forward);
    }

    private Sym DeclareTypeParam(Sym owner, Token tok, int index)
    {
        var tp = new Sym(tok.Text, SymKind.TypeParam, owner)
        {
            Symbol = ChildSymbol(owner, Names.TypeParameter(tok.Text)),
            ScipKind = ScipKind.TypeParameter,
            Index = index,
            HomeSource = tok.Source,
            Signature = tok.Text,
        };
        tp.Type = new PType(TypeKind.TypeParam) { Decl = tp };
        Emit.Definition(tok, tp);
        return tp;
    }

    private static ScipKind KindOf(PType t) => t.Kind switch
    {
        TypeKind.Record => ScipKind.Struct,
        TypeKind.Class => ScipKind.Class,
        TypeKind.Object => ScipKind.Object,
        TypeKind.Interface => ScipKind.Interface,
        TypeKind.Helper => ScipKind.Extension,
        TypeKind.Enum => ScipKind.Enum,
        TypeKind.Proc => ScipKind.Delegate,
        TypeKind.Specialization => t.Generic is { } g ? KindOf(TypeSystem.Structural(g)) : ScipKind.Type,
        _ => ScipKind.Type,
    };

    /// <summary>Parses <c>&lt;T, U: constraint; V&gt;</c>, resolving constraint types.</summary>
    private List<Token> TypeParamNames()
    {
        var names = new List<Token>();
        Next(); // <
        while (!AtEof && !IsSym(">"))
        {
            var before = _i;
            if (IsIdent)
            {
                names.Add(Next());
            }

            if (Accept(":"))
            {
                while (!AtEof && !IsSym(";") && !IsSym(">"))
                {
                    if (IsIdent && !IsWord("constructor"))
                    {
                        var c = Next();
                        if (LookupType(c) is { } cs)
                        {
                            Emit.Reference(c, cs);
                        }
                    }
                    else
                    {
                        Next();
                    }
                }
            }

            if (!Accept(",") && !Accept(";") && _i == before)
            {
                Next();
            }
        }

        Accept(">");
        return names;
    }

    /// <summary>Parses a type. <paramref name="decl"/> is the type symbol being declared (null for anonymous types).</summary>
    private (PType Type, bool Forward) TypeSpec(Sym? decl, Ctx ctx, PType? reuse = null)
    {
        if (IsSym("("))
        {
            return (EnumType(decl, ctx), false);
        }

        if (IsSym("^"))
        {
            Next();
            var ptr = new PType(TypeKind.Pointer);
            if (IsIdent && !Peek().IsSymbol(".") && !Peek().IsSymbol("<") && _pendingPointers is not null)
            {
                _pendingPointers.Add((Next(), ptr));
            }
            else
            {
                ptr.Elem = TypeSpec(null, ctx).Type;
            }

            return (ptr, false);
        }

        if (IsKw("packed") || IsWord("bitpacked"))
        {
            Next();
            return TypeSpec(decl, ctx, reuse);
        }

        if (IsKw("array"))
        {
            return (ArrayType(ctx), false);
        }

        if (IsKw("set"))
        {
            Next();
            Accept("of");
            if (IsKw("of"))
            {
                Next();
            }

            return (new PType(TypeKind.Set) { Elem = TypeSpec(null, ctx).Type }, false);
        }

        if (IsKw("file"))
        {
            Next();
            var f = new PType(TypeKind.File);
            if (IsKw("of"))
            {
                Next();
                f.Elem = TypeSpec(null, ctx).Type;
            }

            return (f, false);
        }

        if (IsKw("string") || (IsWord("string") && _mode is PascalMode.Iso or PascalMode.ExtendedPascal && Lookup("string") is null))
        {
            Next();
            if (IsSym("[") || IsSym("("))
            {
                Next();
                Expr();
                Accept("]");
                Accept(")");
            }

            return (Types.StringType, false);
        }

        if (IsKw("record"))
        {
            return (RecordType(decl, ctx, reuse), false);
        }

        if (IsKw("object"))
        {
            return ClassLike(TypeKind.Object, decl, ctx, reuse);
        }

        if (IsKw("class"))
        {
            if (Peek().IsKeyword("of"))
            {
                Next();
                Next();
                return (new PType(TypeKind.ClassRef) { Elem = TypeSpec(null, ctx).Type }, false);
            }

            return ClassLike(TypeKind.Class, decl, ctx, reuse);
        }

        if (IsKw("interface") || IsKw("dispinterface") || IsWord("objcprotocol"))
        {
            return ClassLike(TypeKind.Interface, decl, ctx, reuse);
        }

        if (IsWord("objcclass") || IsWord("cppclass") || IsWord("objccategory"))
        {
            return ClassLike(TypeKind.Class, decl, ctx, reuse);
        }

        if (IsKw("procedure") || IsKw("function"))
        {
            return (ProcType(ctx), false);
        }

        if (IsWord("reference") && Peek().IsWord("to"))
        {
            Next();
            Next();
            return (ProcType(ctx), false);
        }

        if (IsWord("type") && Peek().IsWord("helper"))
        {
            Next();
            Next();
            return (HelperType(decl, ctx, reuse), false);
        }

        if (IsKw("type"))
        {
            Next();
            return TypeSpec(decl, ctx, reuse);
        }

        AcceptWord("specialize");
        return (TypeRefOrSubrange(), false);
    }

    /// <summary>A named type (possibly unit-qualified or generic) or a subrange <c>lo..hi</c>.</summary>
    private PType TypeRefOrSubrange()
    {
        var saved = _typeContext;
        _typeContext = true;
        ExprInfo e;
        try
        {
            e = SimpleExpr();
        }
        finally
        {
            _typeContext = saved;
        }

        if (IsSym(".."))
        {
            Next();
            SimpleExpr();
            return new PType(TypeKind.Subrange) { Elem = e.IsType ? null : e.Type };
        }

        return e.IsType && e.Type is not null ? e.Type : new PType(TypeKind.Unknown) { Decl = e.Sym is { Kind: SymKind.Type } s ? s : null };
    }

    private PType EnumType(Sym? decl, Ctx ctx)
    {
        Next(); // (
        var t = new PType(TypeKind.Enum) { Members = new Scope(decl), Decl = decl };
        while (!AtEof && !IsSym(")"))
        {
            var before = _i;
            if (IsIdent || DemoteKeywordName())
            {
                var name = Next();
                if (Accept("=") || Accept(":="))
                {
                    Expr();
                }

                var member = new Sym(name.Text, SymKind.EnumMember, decl ?? ctx.Owner)
                {
                    Symbol = decl is not null ? ChildSymbol(decl, Names.Term(name.Text)) : ctx.LocalData ? null : ChildSymbol(ctx.Owner, Names.Term(name.Text)),
                    ScipKind = ScipKind.EnumMember,
                    Type = t,
                    HomeSource = name.Source,
                    Signature = decl is null ? name.Text : $"{decl.Name}.{name.Text}",
                };
                member.Documentation = DocFor(name);
                ctx.Scope.Add(member);
                t.Members.Add(member);
                Emit.Definition(name, member);
            }

            if (!Accept(",") && _i == before)
            {
                Next();
            }
        }

        Accept(")");
        return t;
    }

    private PType ArrayType(Ctx ctx)
    {
        Next(); // array
        if (IsSym("["))
        {
            Next();
            while (!AtEof && !IsSym("]"))
            {
                var before = _i;
                TypeSpec(null, ctx);
                if (!Accept(",") && _i == before)
                {
                    Next();
                }
            }

            Accept("]");
        }

        if (IsKw("of"))
        {
            Next();
        }

        if (IsKw("const"))
        {
            Next();
            return new PType(TypeKind.Array);
        }

        return new PType(TypeKind.Array) { Elem = TypeSpec(null, ctx).Type };
    }

    private PType ProcType(Ctx ctx)
    {
        var isFunction = IsKw("function");
        Next();
        var t = new PType(TypeKind.Proc);
        if (IsSym("("))
        {
            foreach (var p in ParseParams(ctx))
            {
                var ps = new Sym(p.Name.Text, SymKind.Param, ctx.Owner) { ScipKind = ScipKind.Parameter, Type = p.Type, HomeSource = p.Name.Source, Signature = p.Signature };
                Emit.Definition(p.Name, ps);
            }
        }

        if (isFunction && Accept(":"))
        {
            t.Result = TypeSpec(null, ctx).Type;
        }

        if (IsKw("of") && Peek().IsWord("object"))
        {
            Next();
            Next();
        }

        if (IsKw("is") && Peek().IsWord("nested"))
        {
            Next();
            Next();
        }

        while (IsSym(";") && IsCallingConvention(Peek()))
        {
            Next();
            Next();
        }

        while (IsCallingConvention(Cur))
        {
            Next();
        }

        return t;
    }

    private static bool IsCallingConvention(Token t) => t.Kind == TokenKind.Identifier && t.Lower is "cdecl" or "stdcall" or "pascal"
        or "register" or "safecall" or "cppdecl" or "far" or "near" or "winapi" or "mwpascal" or "syscall" or "vectorcall"
        or "varargs" or "ms_abi_default" or "sysv_abi_default" or "hardfloat" or "softfloat" or "oldfpccall" or "far16";

    private PType RecordType(Sym? decl, Ctx ctx, PType? reuse)
    {
        Next(); // record
        if (IsWord("helper"))
        {
            Next();
            return HelperType(decl, ctx, reuse);
        }

        var t = reuse ?? new PType(TypeKind.Record);
        t.Kind = TypeKind.Record;
        t.Members ??= new Scope(decl);
        t.Decl ??= decl;
        if (decl is not null)
        {
            decl.Type ??= t;
        }
        var mctx = decl is null
            ? new Ctx { Owner = ctx.Owner, Scope = t.Members, LocalData = true, MemberOf = t }
            : new Ctx { Owner = decl, Scope = t.Members, MemberOf = t };
        var mark = Mark();
        Push(new MemberLayer(t, Types));
        var header = Prev;
        MemberList(mctx, record: true, stopAtParen: false);
        Restore(mark);
        if (IsKw("end"))
        {
            Next();
        }

        _typeHeaderEnd = header;
        return t;
    }

    private PType HelperType(Sym? decl, Ctx ctx, PType? reuse)
    {
        var t = reuse ?? new PType(TypeKind.Helper);
        t.Kind = TypeKind.Helper;
        t.Members ??= new Scope(decl);
        t.Decl ??= decl;
        if (decl is not null)
        {
            decl.Type ??= t;
        }
        if (IsSym("("))
        {
            Next();
            while (!AtEof && !IsSym(")"))
            {
                var before = _i;
                var a = TypeSpec(null, ctx).Type;
                t.Ancestors.Add(a);
                if (!Accept(",") && _i == before)
                {
                    Next();
                }
            }

            Accept(")");
        }

        if (AcceptWord("for"))
        {
            t.Elem = TypeSpec(null, ctx).Type;
            Types.RegisterHelper(t, t.Elem);
            if (decl is not null && t.Elem.Decl?.Symbol is not null)
            {
                AddRelationship(decl, t.Elem.Decl, implementation: false);
            }
        }

        var header = Prev;
        var mctx = new Ctx { Owner = decl ?? ctx.Owner, Scope = t.Members, LocalData = decl is null, MemberOf = t };
        var mark = Mark();
        if (t.Elem is not null)
        {
            Push(new MemberLayer(t.Elem, Types));
        }

        Push(new MemberLayer(t, Types));
        MemberList(mctx, record: false, stopAtParen: false);
        Restore(mark);
        if (IsKw("end"))
        {
            Next();
        }

        _typeHeaderEnd = header;
        return t;
    }

    private void AddRelationship(Sym from, Sym to, bool implementation)
    {
        if (to.Symbol is null || from.Symbol is null || ReferenceEquals(from, to) || from.Relationships.Any(r => r.Symbol == to.Symbol))
        {
            return;
        }

        from.Relationships.Add(new Relationship { Symbol = to.Symbol, IsImplementation = implementation, IsReference = !implementation });
        Emit.Require(to);
    }

    private (PType Type, bool Forward) ClassLike(TypeKind kind, Sym? decl, Ctx ctx, PType? reuse)
    {
        Next(); // class / object / interface
        while (IsWord("abstract") || IsWord("sealed") || IsWord("external"))
        {
            Next();
            if (Cur.Kind == TokenKind.String)
            {
                Next();
            }
        }

        if (IsWord("helper"))
        {
            Next();
            return (HelperType(decl, ctx, reuse), false);
        }

        var t = reuse ?? new PType(kind);
        t.Kind = kind;
        t.Members ??= new Scope(decl);
        t.Decl ??= decl;
        if (decl is not null)
        {
            decl.Type ??= t;
        }
        if (IsSym(";") && kind is TypeKind.Class or TypeKind.Interface)
        {
            return (t, true);
        }

        if (IsSym("("))
        {
            Next();
            while (!AtEof && !IsSym(")"))
            {
                var before = _i;
                var a = TypeSpec(null, ctx).Type;
                if (!ReferenceEquals(a, t) && (a.Kind != TypeKind.Unknown || a.Decl is not null))
                {
                    t.Ancestors.Add(a);
                    var target = a.Kind == TypeKind.Specialization ? TypeSystem.Structural(a).Decl : a.Decl;
                    if (decl is not null && target is not null)
                    {
                        AddRelationship(decl, target, implementation: true);
                    }
                }

                if (!Accept(",") && _i == before)
                {
                    Next();
                }
            }

            Accept(")");
        }

        var header = Prev;
        _typeHeaderEnd = header;
        if (kind == TypeKind.Interface && IsSym("["))
        {
            SkipBalanced();
        }

        if (IsSym(";"))
        {
            return (t, false);
        }

        var mctx = decl is null
            ? new Ctx { Owner = ctx.Owner, Scope = t.Members, LocalData = true, MemberOf = t }
            : new Ctx { Owner = decl, Scope = t.Members, MemberOf = t };
        var mark = Mark();
        Push(new MemberLayer(t, Types));
        MemberList(mctx, record: false, stopAtParen: false);
        Restore(mark);
        if (IsKw("end"))
        {
            Next();
        }

        if (decl is not null)
        {
            LinkInterfaceImplementations(t);
        }

        _typeHeaderEnd = header;
        return (t, false);
    }

    /// <summary>Methods of a class implementing methods of its interfaces get an implementation relationship.</summary>
    private void LinkInterfaceImplementations(PType t)
    {
        var interfaces = Types.AncestorsOf(t).Where(static a => a.Kind == TypeKind.Interface).ToList();
        if (interfaces.Count == 0 || t.Members is null)
        {
            return;
        }

        foreach (var member in t.Members.All.Where(static m => m.Kind == SymKind.Routine).ToList())
        {
            foreach (var intf in interfaces)
            {
                if (intf.Members?.Find(member.Name) is { } targets && targets.FirstOrDefault(static x => x.Kind == SymKind.Routine) is { } target)
                {
                    AddRelationship(member, target, implementation: true);
                }
            }
        }
    }

    /// <summary>Fields, methods, properties and nested sections of a record/class/object/interface/helper.</summary>
    private void MemberList(Ctx mctx, bool record, bool stopAtParen)
    {
        var isStatic = false;
        while (!AtEof && !IsKw("end") && !(stopAtParen && IsSym(")")))
        {
            var before = _i;
            if (IsWord("private") || IsWord("protected") || IsWord("public") || IsWord("published") || IsWord("automated")
                || IsWord("strict") || IsWord("required") || IsWord("optional"))
            {
                Next();
                isStatic = false;
            }
            else if (IsKw("case") && record)
            {
                VariantPart(mctx);
            }
            else if (IsKw("const"))
            {
                Next();
                ConstSection(mctx);
            }
            else if (IsKw("type"))
            {
                Next();
                TypeSection(mctx);
            }
            else if (IsKw("var") || IsKw("threadvar"))
            {
                Next();
                isStatic = false;
            }
            else if (IsWord("class") && Peek().IsKeyword("var"))
            {
                Next();
                Next();
                isStatic = true;
            }
            else if (IsSym("["))
            {
                SkipBalanced();
            }
            else if (IsRoutineStart())
            {
                RoutineDecl(mctx);
            }
            else if (IsKw("property") || (IsWord("class") && Peek().IsKeyword("property")))
            {
                PropertyDecl(mctx);
            }
            else if ((IsIdent || DemoteKeywordName()) && (Peek().IsSymbol(":") || Peek().IsSymbol(",")))
            {
                FieldDecl(mctx, isStatic);
            }
            else if (IsSym(";"))
            {
                Next();
            }

            if (_i == before)
            {
                Next();
            }
        }
    }

    private void FieldDecl(Ctx mctx, bool isStatic)
    {
        var names = new List<Token> { Next() };
        while (Accept(","))
        {
            if (IsIdent || DemoteKeywordName())
            {
                names.Add(Next());
            }
        }

        if (!Accept(":"))
        {
            return;
        }

        var typeStart = Cur;
        var type = TypeSpec(null, mctx).Type;
        var typeEnd = Prev;
        if (Accept("="))
        {
            ConstValue(type);
        }

        SkipHints();
        var typeText = Slice(typeStart, typeEnd, 200);
        string? trailing = null;
        if (Accept(";"))
        {
            trailing = TrailingDoc(Prev);
        }
        else if (IsKw("end") || IsSym(")"))
        {
            trailing = TrailingDoc(Prev);
        }

        foreach (var n in names)
        {
            var sym = Declare(mctx, n, SymKind.Field, Names.Term(n.Text), isStatic ? ScipKind.StaticField : ScipKind.Field, names[0]);
            sym.Type = type;
            sym.IsStatic = isStatic;
            sym.Signature = $"{n.Text}: {typeText}";
            sym.Documentation ??= trailing;
            Emit.Definition(n, sym);
        }
    }

    private void VariantPart(Ctx mctx)
    {
        Next(); // case
        if (IsIdent && Peek().IsSymbol(":"))
        {
            var tag = Next();
            Next();
            var typeStart = Cur;
            var type = TypeSpec(null, mctx).Type;
            var sym = Declare(mctx, tag, SymKind.Field, Names.Term(tag.Text), ScipKind.Field);
            sym.Type = type;
            sym.Signature = $"case {tag.Text}: {Slice(typeStart, Prev, 200)}";
            Emit.Definition(tag, sym);
        }
        else
        {
            TypeSpec(null, mctx);
        }

        if (IsKw("of"))
        {
            Next();
        }

        while (!AtEof && !IsKw("end") && !IsSym(")"))
        {
            var before = _i;
            while (!AtEof && !IsSym(":") && !IsSym("(") && !IsKw("end") && !IsSym(";"))
            {
                var b2 = _i;
                Expr();
                if (Accept(".."))
                {
                    Expr();
                }

                if (!Accept(",") && _i == b2)
                {
                    Next();
                }
            }

            Accept(":");
            if (Accept("("))
            {
                MemberList(mctx, record: true, stopAtParen: true);
                Accept(")");
            }

            Accept(";");
            if (_i == before)
            {
                Next();
            }
        }
    }

    private void PropertyDecl(Ctx mctx)
    {
        var start = Cur;
        AcceptWord("class");
        Next(); // property
        if (!IsNameLike)
        {
            SkipToSemicolon();
            return;
        }

        var name = Next();
        var mark = Mark();
        var indexScope = new Scope(null);
        List<ParamDecl>? indexParams = null;
        if (IsSym("["))
        {
            indexParams = ParseParams(mctx, "]");
        }

        PType? type = null;
        if (Accept(":"))
        {
            type = TypeSpec(null, mctx).Type;
        }

        var headerEnd = Prev;
        var isDefaultHere = false;
        while (!AtEof)
        {
            if (IsWord("index") || IsWord("stored") || IsWord("dispid"))
            {
                Next();
                Expr();
            }
            else if (IsWord("read") || IsWord("write") || IsWord("add") || IsWord("remove"))
            {
                Next();
                Expr();
            }
            else if (IsWord("default"))
            {
                Next();
                if (IsSym(";"))
                {
                    isDefaultHere = true;
                }
                else
                {
                    Expr();
                }
            }
            else if (IsWord("nodefault") || IsWord("readonly") || IsWord("writeonly"))
            {
                Next();
            }
            else if (IsWord("implements"))
            {
                Next();
                TypeSpec(null, mctx);
                while (Accept(","))
                {
                    TypeSpec(null, mctx);
                }
            }
            else
            {
                break;
            }
        }

        var end = Prev;
        Accept(";");
        if (IsWord("default") && Peek().IsSymbol(";"))
        {
            Next();
            Next();
            isDefaultHere = true;
        }

        SkipHints();
        Restore(mark);
        if (type is null && indexParams is null && mctx.MemberOf is { } owner)
        {
            // Redeclaration (visibility change) of an inherited property.
            foreach (var a in Types.AncestorsOf(owner))
            {
                if (a.Members?.Find(name.Text) is { } inherited && inherited.FirstOrDefault(static s => s.Kind == SymKind.Property) is { } p)
                {
                    Emit.Reference(name, p);
                    break;
                }
            }

            return;
        }

        var sym = Declare(mctx, name, SymKind.Property, Names.Term(name.Text), mctx.IsInterfaceType ? ScipKind.Property : ScipKind.Property, start);
        sym.Type = type;
        sym.Signature = Slice(start, headerEnd, 300);
        sym.Documentation ??= TrailingDoc(Prev);
        Emit.Definition(name, sym);
        if (indexParams is not null)
        {
            foreach (var p in indexParams)
            {
                var ps = new Sym(p.Name.Text, SymKind.Param, sym) { ScipKind = ScipKind.Parameter, Type = p.Type, HomeSource = p.Name.Source, Signature = p.Signature };
                Emit.Definition(p.Name, ps);
                indexScope.Add(ps);
            }
        }

        if (isDefaultHere && mctx.MemberOf is { } mt)
        {
            mt.DefaultProperty = sym;
        }
    }
}
