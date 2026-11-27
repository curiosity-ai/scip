using ScipSharp.Indexer.Pascal.Syntax;
using ScipKind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Pascal.Semantics;

/// <summary>What an expression denotes: a symbol (if a designator), its value type, and whether it names a type.</summary>
internal readonly record struct ExprInfo(Sym? Sym, PType? Type, bool IsType);

internal enum InferHint
{
    Value,
    Type,
    Call,
}

internal sealed partial class Compiler
{
    private ExprInfo Expr()
    {
        var l = SimpleExpr();
        while (IsSym("=") || IsSym("<>") || IsSym("<") || IsSym(">") || IsSym("<=") || IsSym(">=") || IsKw("in") || IsKw("is") || IsKw("as"))
        {
            var op = Next();
            var r = SimpleExpr();
            l = op.IsKeyword("as")
                ? new ExprInfo(null, r.IsType ? r.Type : null, false)
                : OverloadedOperator(op, l, r) ?? new ExprInfo(null, Types.BooleanType, false);
        }

        return l;
    }

    private ExprInfo SimpleExpr()
    {
        if (IsSym("+") || IsSym("-"))
        {
            Next();
        }

        var l = Term();
        while (IsSym("+") || IsSym("-") || IsKw("or") || IsKw("xor"))
        {
            var op = Next();
            var r = Term();
            var t = l.Type?.Kind == TypeKind.String || r.Type?.Kind == TypeKind.String ? Types.StringType : l.Type;
            l = OverloadedOperator(op, l, r) ?? new ExprInfo(null, t, false);
        }

        return l;
    }

    private ExprInfo Term()
    {
        var l = Factor();
        while (IsSym("*") || IsSym("/") || IsKw("div") || IsKw("mod") || IsKw("and") || IsKw("shl") || IsKw("shr") || IsSym("**") || IsSym("><"))
        {
            var op = Next();
            var r = Factor();
            l = OverloadedOperator(op, l, r) ?? new ExprInfo(null, op.IsSymbol("/") ? Types.RealType : l.Type, false);
        }

        return l;
    }

    /// <summary>A use of an FPC global operator overload (<c>operator + (a, b: TVector)</c>) on structured operands.</summary>
    private ExprInfo? OverloadedOperator(Token op, ExprInfo l, ExprInfo r)
    {
        if (l.Type is not { } lt || !(TypeSystem.Structural(lt).IsStructured || (r.Type is { } rt && TypeSystem.Structural(rt).IsStructured)))
        {
            return null;
        }

        var cands = Lookup(op.Lower)?.Where(static s => s.Routine?.Kind == RoutineKind.Operator).ToList();
        if (cands is not { Count: > 0 })
        {
            return null;
        }

        var chosen = cands.Count == 1 ? cands[0] : Choose(cands, [l.Type, r.Type]);
        Emit.Reference(op, chosen);
        return new ExprInfo(null, chosen.Routine?.Result, false);
    }

    private ExprInfo Factor()
    {
        var t = Cur;
        switch (t.Kind)
        {
            case TokenKind.Number:
                Next();
                return new ExprInfo(null, t.Text.Contains('.') || (t.Text.Contains('e', StringComparison.OrdinalIgnoreCase) && !t.Text.StartsWith('$')) ? Types.RealType : Types.IntegerType, false);
            case TokenKind.String:
                Next();
                SkipControlChars();
                return new ExprInfo(null, Types.StringType, false);
            case TokenKind.Eof:
                return default;
        }

        if (t.Kind == TokenKind.Keyword)
        {
            switch (t.Lower)
            {
                case "not":
                    Next();
                    return Factor() is var f && f.Type == Types.BooleanType ? f : new ExprInfo(null, Types.BooleanType, false);
                case "nil":
                    Next();
                    return default;
                case "inherited":
                    return Inherited();
                case "string":
                    Next();
                    return Postfix(new ExprInfo(null, Types.StringType, true));
                case "procedure" or "function":
                    return AnonymousRoutine();
                case "file":
                    Next();
                    return Postfix(new ExprInfo(null, new PType(TypeKind.File), true));
                default:
                    Next();
                    return default;
            }
        }

        if (t.Kind == TokenKind.Symbol)
        {
            switch (t.Text)
            {
                case "@" or "@@":
                    Next();
                    var target = Factor();
                    return new ExprInfo(target.Sym, new PType(TypeKind.Pointer) { Elem = target.Type }, false);
                case "^":
                    // Control character literal (^M); pointer types are handled by TypeSpec.
                    Next();
                    if (Cur.Text.Length == 1 || Cur.Kind == TokenKind.Identifier)
                    {
                        Next();
                    }

                    SkipControlChars();
                    return new ExprInfo(null, Types.CharType, false);
                case "[":
                    SetConstructor();
                    return default;
                case "(":
                {
                    Next();
                    var e = Expr();
                    while (Accept(","))
                    {
                        Expr();
                    }

                    Accept(")");
                    return Postfix(new ExprInfo(null, e.Type, e.IsType));
                }

                default:
                    Next();
                    return default;
            }
        }

        if (t.IsWord("specialize"))
        {
            Next();
            var saved = _typeContext;
            _typeContext = true;
            try
            {
                return Designator();
            }
            finally
            {
                _typeContext = saved;
            }
        }

        return Designator();
    }

    /// <summary>Skips <c>^M^J</c> control characters following a string literal (<c>'abc'^M</c>).</summary>
    private void SkipControlChars()
    {
        while (IsSym("^") && Peek().Text.Length == 1 && !Peek(2).IsSymbol("."))
        {
            Next();
            Next();
            if (Cur.Kind == TokenKind.String)
            {
                Next();
            }
        }
    }

    private void SetConstructor()
    {
        Next(); // [
        var saved = _lastOcc;
        while (!AtEof && !IsSym("]"))
        {
            var before = _i;
            Expr();
            if (Accept(".."))
            {
                Expr();
            }

            if (!Accept(",") && _i == before)
            {
                Next();
            }

            if (!IsSym("]") && !IsSym(",") && _i != before && (IsSym(";") || IsKw("end") || IsKw("then") || IsKw("do") || IsKw("of")))
            {
                break;
            }
        }

        Accept("]");
        _lastOcc = saved;
    }

    private static bool LooksLikeTypeName(string name) =>
        name.Length >= 2 && name[0] is 'T' or 'E' or 'I' or 'P' && char.IsAsciiLetterUpper(name[1]);

    private ExprInfo Designator()
    {
        if (!IsNameLike)
        {
            Next();
            return default;
        }

        if (Peek().IsSymbol("."))
        {
            var (unit, parts) = MatchUnitName();
            if (unit?.Sym is { } usym && !(parts == 1 && Lookup(Cur.Text) is not null))
            {
                var first = Next();
                var last = first;
                for (var n = 1; n < parts; n++)
                {
                    Next();
                    last = Next();
                }

                Emit.Reference(first, last, usym);
                if (!IsSym(".") || !Peek().IsIdentifier && Peek().Kind != TokenKind.Keyword)
                {
                    return new ExprInfo(usym, null, false);
                }

                Next();
                var memberTok = Next();
                var cands = UnitMember(unit, memberTok);
                return Postfix(cands is null ? default : Apply(memberTok, cands, null, false));
            }
        }

        var nameTok = Next();
        var found = Lookup(nameTok.Text);
        if (found is null)
        {
            var hint = _typeContext || (LooksLikeTypeName(nameTok.Text) && (IsSym(".") || IsSym("(") || IsSym("<"))) ? InferHint.Type
                : IsSym("(") ? InferHint.Call
                : InferHint.Value;
            if (ResolveUnknown(nameTok, hint) is { } inferred)
            {
                found = [inferred];
            }
            else
            {
                if (_typeContext && IsSym("<") && TypeArgsFollow())
                {
                    TypeArgs();
                }

                return Postfix(default);
            }
        }

        return Postfix(Apply(nameTok, found, null, false));
    }

    private List<Sym>? UnitMember(CompileUnit unit, Token memberTok)
    {
        if (ReferenceEquals(unit, _unit))
        {
            return (_inImplementation ? _unit.Implementation.Find(memberTok.Text) : null) ?? _unit.Interface.Find(memberTok.Text);
        }

        if (unit.Interface.Find(memberTok.Text) is { } found)
        {
            return found;
        }

        if (unit.IsBuiltin)
        {
            var hint = _typeContext ? InferHint.Type : IsSym("(") ? InferHint.Call : InferHint.Value;
            return [Workspace.Infer(unit, memberTok.Text, hint)];
        }

        return null;
    }

    /// <summary>Binds <paramref name="nameTok"/> to one of <paramref name="cands"/> (overloads: by argument count) and emits the reference.</summary>
    private ExprInfo Apply(Token nameTok, List<Sym> cands, PType? receiver, bool receiverIsType)
    {
        var first = cands[0];
        if (first.Kind == SymKind.Routine)
        {
            List<PType?> argTypes = [];
            if (IsSym("("))
            {
                argTypes = first.External && first.Owner?.Name == "System" && first.Name.ToLowerInvariant() is "new" or "dispose"
                    ? NewDisposeArgs()
                    : Args(")");
            }

            var chosen = cands.Count == 1 ? first : Choose(cands, argTypes);
            _lastOcc = Emit.Reference(nameTok, chosen);
            var t = chosen.Routine?.Kind == RoutineKind.Constructor && receiverIsType && receiver is not null
                ? receiver
                : TypeSystem.Substitute(chosen.Routine?.Result, receiver);
            return new ExprInfo(chosen, t, false);
        }

        _lastOcc = Emit.Reference(nameTok, first);
        if (first.Kind is SymKind.Type or SymKind.TypeParam)
        {
            var type = first.Type;
            if (IsSym("<") && (first.TypeParams is { Count: > 0 } || _typeContext) && TypeArgsFollow())
            {
                type = new PType(TypeKind.Specialization) { Generic = first.Type, Args = TypeArgs() };
            }

            return new ExprInfo(first, type, true);
        }

        if (first.Kind is SymKind.Unit or SymKind.Program)
        {
            return new ExprInfo(first, null, false);
        }

        return new ExprInfo(first, TypeSystem.Substitute(first.Type, receiver), false);
    }

    /// <summary>Picks an overload: the argument count must fit; then the best match of argument and parameter types.</summary>
    private static Sym Choose(List<Sym> cands, List<PType?> args)
    {
        Sym? best = null;
        var bestScore = -1;
        foreach (var c in cands)
        {
            if (c.Routine is not { } r || args.Count < r.MinArgs || args.Count > r.MaxArgs)
            {
                continue;
            }

            var score = 0;
            var ps = r.Params.Where(static p => p.Kind == SymKind.Param).ToList();
            for (var i = 0; i < args.Count && i < ps.Count; i++)
            {
                score += Compatibility(args[i], ps[i].Type);
            }

            if (score > bestScore)
            {
                best = c;
                bestScore = score;
            }
        }

        return best ?? cands[0];
    }

    private static int Compatibility(PType? arg, PType? param)
    {
        if (arg is null || param is null)
        {
            return 0;
        }

        if (ReferenceEquals(arg, param))
        {
            return 3;
        }

        if (arg.Kind == TypeKind.String && param.Kind == TypeKind.String)
        {
            return 2;
        }

        var fa = Family(arg);
        return fa != 0 && fa == Family(param) ? 2 : 0;
    }

    private static int Family(PType t) => t.Kind != TypeKind.Simple || t.Decl is null ? 0 : t.Decl.Name.ToLowerInvariant() switch
    {
        "real" or "single" or "double" or "extended" or "currency" or "comp" or "real48" or "valreal" => 1,
        "integer" or "longint" or "smallint" or "shortint" or "int64" or "byte" or "word" or "cardinal" or "longword"
            or "dword" or "qword" or "uint64" or "nativeint" or "nativeuint" or "ptrint" or "ptruint" or "sizeint" => 2,
        "char" or "ansichar" or "widechar" => 3,
        "boolean" or "bytebool" or "wordbool" or "longbool" => 4,
        _ => 0,
    };

    /// <summary>TP extended syntax <c>New(P, Init(...))</c> / <c>Dispose(P, Done)</c>: the constructor names a method of P's target.</summary>
    private List<PType?> NewDisposeArgs()
    {
        Next(); // (
        var saved = _lastOcc;
        var result = new List<PType?>();
        var first = Expr();
        result.Add(first.Type);
        if (Accept(","))
        {
            var target = first.IsType ? first.Type : TypeSystem.Deref(first.Type);
            var mark = Mark();
            if (target is not null)
            {
                Push(new MemberLayer(target, Types));
            }

            result.Add(Expr().Type);
            Restore(mark);
        }

        while (!AtEof && !IsSym(")") && !IsSym(";"))
        {
            var before = _i;
            Accept(",");
            Expr();
            if (_i == before)
            {
                Next();
            }
        }

        Accept(")");
        _lastOcc = saved;
        return result;
    }

    private bool TypeArgsFollow()
    {
        var depth = 0;
        for (var k = 0; k < 64; k++)
        {
            var t = Peek(k);
            if (t.IsSymbol("<"))
            {
                depth++;
            }
            else if (t.IsSymbol(">"))
            {
                depth--;
                if (depth == 0)
                {
                    return true;
                }
            }
            else if (!(t.IsIdentifier || t.IsSymbol(",") || t.IsSymbol(".") || t.IsKeyword("string") || t.IsSymbol("^") || t.IsKeyword("array") || t.IsKeyword("of") || t.IsWord("specialize")))
            {
                return false;
            }
        }

        return false;
    }

    private List<PType> TypeArgs()
    {
        var args = new List<PType>();
        Next(); // <
        var ctx = ScratchCtx();
        var saved = _typeContext;
        _typeContext = true;
        try
        {
            while (!AtEof && !IsSym(">"))
            {
                var before = _i;
                args.Add(TypeSpec(null, ctx).Type);
                if (!Accept(",") && _i == before)
                {
                    Next();
                }
            }
        }
        finally
        {
            _typeContext = saved;
        }

        Accept(">");
        return args;
    }

    private Ctx ScratchCtx() => new() { Owner = _unit.Sym!, Scope = new Scope(null), LocalData = true };

    /// <summary>Parses an argument list; returns the argument types. Handles <c>Write(x:10:2)</c> format specifiers.</summary>
    private List<PType?> Args(string close)
    {
        Next(); // ( or [
        var saved = _lastOcc;
        var savedType = _typeContext;
        _typeContext = false;
        var types = new List<PType?>();
        while (!AtEof && !IsSym(close))
        {
            var before = _i;
            var e = Expr();
            types.Add(e.IsType ? null : e.Type);
            while (Accept(":"))
            {
                Expr();
            }

            if (!Accept(",") && !IsSym(close))
            {
                if (_i == before)
                {
                    Next();
                }

                if (IsSym(";") || IsKw("end") || IsKw("begin") || IsKw("then") || IsKw("do"))
                {
                    break;
                }
            }
        }

        Accept(close);
        _typeContext = savedType;
        _lastOcc = saved;
        return types;
    }

    private ExprInfo Postfix(ExprInfo cur)
    {
        while (true)
        {
            if (IsSym(".") && Peek().Kind is TokenKind.Identifier or TokenKind.Keyword)
            {
                Next();
                var nameTok = Next();
                var (cands, receiver, receiverIsType) = Member(cur, nameTok);
                cur = cands is null ? default : Apply(nameTok, cands, receiver, receiverIsType);
                if (cands is null && IsSym("("))
                {
                    Args(")");
                }

                continue;
            }

            if (IsSym("["))
            {
                var type = cur.Type;
                var isType = cur.IsType;
                Args("]");
                cur = isType ? cur : new ExprInfo(null, Types.IndexResult(type), false);
                continue;
            }

            if (IsSym("^") && !(Peek().Text.Length == 1 && Peek().Kind == TokenKind.Identifier && cur.Sym is null && cur.Type?.Kind == TypeKind.String))
            {
                Next();
                cur = new ExprInfo(null, TypeSystem.Deref(cur.Type), false);
                continue;
            }

            if (IsSym("("))
            {
                var castType = cur.IsType ? cur.Type : null;
                var procResult = cur.Type is { Kind: TypeKind.Proc } pt ? pt.Result : null;
                Args(")");
                cur = new ExprInfo(null, castType ?? procResult, false);
                continue;
            }

            return cur;
        }
    }

    /// <summary>Members named <paramref name="nameTok"/> of what <paramref name="cur"/> denotes.</summary>
    private (List<Sym>? Cands, PType? Receiver, bool ReceiverIsType) Member(ExprInfo cur, Token nameTok)
    {
        if (cur.Sym is { Kind: SymKind.Unit or SymKind.Program, Unit: { } u })
        {
            return (UnitMember(u, nameTok), null, false);
        }

        if (cur.Type is not { } t)
        {
            return (null, null, false);
        }

        if (Types.FindMember(t, nameTok.Text) is { } found)
        {
            return (found, t, cur.IsType);
        }

        var target = TypeSystem.Structural(t);
        if (target.Kind is TypeKind.Pointer or TypeKind.ClassRef && target.Elem is { } e)
        {
            target = TypeSystem.Structural(e);
        }

        var owner = target.Decl;
        if (owner is { External: true } && target.Members is not null && owner.Symbol is not null)
        {
            return ([Workspace.InferMember(target, nameTok.Text, IsSym("("))], t, cur.IsType);
        }

        // Members inherited from an external ancestor (TForm, TComponent, ...).
        foreach (var a in Types.AncestorsOf(target))
        {
            if (a.Decl is { External: true, Symbol: not null } && a.Members is not null && (a.Ancestors.Count == 0 || a.Decl.Inferred))
            {
                return ([Workspace.InferMember(a, nameTok.Text, IsSym("("))], t, cur.IsType);
            }
        }

        return (null, null, false);
    }

    private ExprInfo Inherited()
    {
        var kw = Next();
        var method = _currentMethod;
        var cls = method?.Owner?.Type;
        if (IsIdent)
        {
            var nameTok = Next();
            List<Sym>? cands = null;
            if (cls is not null)
            {
                foreach (var a in Types.AncestorsOf(cls))
                {
                    if (a.Members?.Find(nameTok.Text) is { } found)
                    {
                        cands = found;
                        break;
                    }
                }

                if (cands is null && Types.AncestorsOf(cls).FirstOrDefault(static a => a.Decl is { External: true, Symbol: not null } && a.Members is not null) is { } ext)
                {
                    cands = [Workspace.InferMember(ext, nameTok.Text, true)];
                }
            }

            if (cands is null)
            {
                if (IsSym("("))
                {
                    Args(")");
                }

                return Postfix(default);
            }

            return Postfix(Apply(nameTok, cands, cls, false));
        }

        if (method is not null && cls is not null)
        {
            foreach (var a in Types.AncestorsOf(cls))
            {
                if (a.Members?.Find(method.Name)?.FirstOrDefault(static s => s.Kind == SymKind.Routine) is { } target)
                {
                    Emit.Reference(kw, target);
                    break;
                }
            }
        }

        return default;
    }

    private ExprInfo AnonymousRoutine()
    {
        var start = Cur;
        var isFunction = IsKw("function");
        Next();
        var mark = Mark();
        var ctx = ScratchCtx();
        var sym = new Sym("anonymous", SymKind.Routine, _currentMethod ?? _unit.Sym) { ScipKind = ScipKind.Function, HomeSource = start.Source };
        var h = new Heading { Start = start, Name = start, Kind = isFunction ? RoutineKind.Function : RoutineKind.Procedure };
        if (IsSym("("))
        {
            h.HasParamList = true;
            h.Params = ParseParams(ctx);
        }

        if (isFunction && Accept(":"))
        {
            h.Result = TypeSpec(null, ctx).Type;
        }

        sym.Routine = new RoutineInfo(h.Kind) { Result = h.Result, HasBody = true };
        var parameters = new List<Sym>();
        foreach (var p in h.Params)
        {
            var ps = new Sym(p.Name.Text, SymKind.Param, sym) { ScipKind = ScipKind.Parameter, Type = p.Type, HomeSource = p.Name.Source, Signature = p.Signature };
            Emit.Definition(p.Name, ps);
            parameters.Add(ps);
        }

        Body(sym, parameters, h, null);
        Restore(mark);
        return new ExprInfo(null, new PType(TypeKind.Proc) { Result = h.Result }, false);
    }

    /// <summary>
    /// A name not declared anywhere visible: attribute it to the only unresolved unit in <c>uses</c> (preferring units
    /// unknown to the built-in table), creating an inferred external symbol. Returns null when ambiguous.
    /// </summary>
    private Sym? ResolveUnknown(Token name, InferHint hint)
    {
        CompileUnit? untabled = null, tabled = null;
        int untabledCount = 0, tabledCount = 0;
        foreach (var u in _intfUses.Concat(_implUses))
        {
            if (!u.IsBuiltin || Builtins.DeclaresNothing(u.Name) || string.Equals(u.Name, "System", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (u.HasTable)
            {
                tabled = u;
                tabledCount++;
            }
            else
            {
                untabled = u;
                untabledCount++;
            }
        }

        // A unit described by the built-in table only gets names when nothing else (project or parsed units) could declare them.
        var onlyBuiltins = _intfUses.Concat(_implUses).All(static u => u.IsBuiltin);
        var target = untabledCount == 1 ? untabled : untabledCount == 0 && tabledCount == 1 && onlyBuiltins ? tabled : null;
        return target is null ? null : Workspace.Infer(target, name.Text, hint);
    }
}
