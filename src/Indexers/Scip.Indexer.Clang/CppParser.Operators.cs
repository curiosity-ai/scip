namespace Scip.Indexer.Clang;

public sealed partial class CppParser
{
    private static readonly HashSet<string> BinaryOperators =
    [
        "<<", ">>", ",", "+", "-", "*", "/", "%", "==", "!=", "<", ">", "<=", ">=", "&&", "||", "&", "|", "^", "+=", "-=", "*=", "/=", "%=", "<<=", ">>=", "&=", "|=", "^=", "=", "<=>",
    ];

    private readonly record struct ExprType(Entity? Cls, bool IsPointer);

    private bool EndsOperand(int a, int j) =>
        j >= a && j < _t.Count && (_t[j].Kind is TokKind.Number or TokKind.StringLiteral or TokKind.CharLiteral
            || (_t[j].Kind == TokKind.Ident && (!Keywords.Contains(_t[j].Text) || _t[j].Text is "this"))
            || (_t[j].Kind == TokKind.Punct && _t[j].Text is ")" or "]" or "}"));

    private static Entity? ClassOf(TypeRef? tr) => SymbolTable.ResolveType(tr) is { Kind: EKind.Class } c ? c : null;

    /// <summary>Best-effort static type of the expression whose last token is <paramref name="j"/> (declared types only).</summary>
    private ExprType TypeOfExprEnding(Scope scope, int a, int j, int depth = 0)
    {
        if (depth > 6 || j < a)
        {
            return default;
        }

        if (Id(j) && _t[j].Text == "this")
        {
            return new ExprType(scope.NearestClass()?.Owner, true);
        }

        if (Id(j) && !Keywords.Contains(_t[j].Text))
        {
            Entity? ent;
            if (j - 1 >= a && (P(j - 1, ".") || P(j - 1, "->")))
            {
                var recv = TypeOfExprEnding(scope, a, j - 2, depth + 1);
                ent = recv.Cls?.Own?.LookupHere(_t[j].Text)?.FirstOrDefault(static e => e.Kind is EKind.Field or EKind.Var);
            }
            else if (j - 1 >= a && P(j - 1, "::"))
            {
                return default;
            }
            else
            {
                ent = scope.Lookup(_t[j].Text)?.FirstOrDefault(static e => e.Kind is EKind.Var or EKind.Local or EKind.Param or EKind.Field);
            }

            return ent?.Type is { } tr ? new ExprType(ClassOf(tr), tr.Indirect) : default;
        }

        if (P(j, ")") || P(j, "}"))
        {
            var m = M(j);
            var c = m - 1;
            if (m > a && c >= a && Id(c) && !Keywords.Contains(_t[c].Text))
            {
                var found = c - 1 >= a && (P(c - 1, ".") || P(c - 1, "->"))
                    ? TypeOfExprEnding(scope, a, c - 2, depth + 1).Cls?.Own?.LookupHere(_t[c].Text)
                    : scope.Lookup(_t[c].Text);
                var ent = found?.FirstOrDefault(static e => e.Kind is EKind.Class or EKind.Func);
                if (ent?.Kind == EKind.Class)
                {
                    return new ExprType(ent, false);
                }

                if (ent?.Type is { } tr)
                {
                    return new ExprType(ClassOf(tr), tr.Indirect);
                }
            }
        }

        return default;
    }

    private string? OperandKind(Scope scope, int r, int b)
    {
        if (r >= b || r >= _t.Count)
        {
            return null;
        }

        var t = _t[r];
        switch (t.Kind)
        {
            case TokKind.Number:
                return t.Text.Contains('.') && !t.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? "double" : "int";
            case TokKind.StringLiteral:
                return "const char *";
            case TokKind.CharLiteral:
                return "char";
        }

        if (Id(r) && !Keywords.Contains(t.Text))
        {
            var e = scope.Lookup(t.Text)?.FirstOrDefault(static x => x.Kind is EKind.Var or EKind.Local or EKind.Param or EKind.Field);
            if (ClassOf(e?.Type) is { } c)
            {
                return TypeCanon.QualName(c);
            }
        }

        return null;
    }

    private static int MatchScore(string param, string? arg)
    {
        if (arg is null)
        {
            return 0;
        }

        var bare = param.Replace("const ", "", StringComparison.Ordinal).Replace(" &", "", StringComparison.Ordinal).Replace("&", "", StringComparison.Ordinal).Trim();
        if (arg == "int")
        {
            return bare == "int" ? 3 : bare is "long" or "unsigned int" or "unsigned long" or "short" or "long long" or "_Bool" or "char" or "double" or "float" ? 1 : -1;
        }

        if (arg == "double")
        {
            return bare is "double" ? 3 : bare is "float" or "long double" ? 2 : -1;
        }

        if (arg == "const char *")
        {
            return param == "const char *" ? 3 : param is "char *" ? 1 : -1;
        }

        if (arg == "char")
        {
            return bare == "char" ? 3 : bare == "int" ? 1 : -1;
        }

        return bare.EndsWith(arg, StringComparison.Ordinal) ? 3 : -1;
    }

    /// <summary>Chooses among operator overloads: first by operand count, then by how well the right operand matches.</summary>
    private static Entity? PickOperator(IEnumerable<Entity> cands, int operands, string? rightKind, bool rightIsFirstParamForMembers = true)
    {
        Entity? best = null;
        var bestScore = int.MinValue;
        foreach (var c in cands.Where(static c => c.Kind == EKind.Func))
        {
            var isMember = c.DeclScope.Kind == SKind.Class;
            var paramCount = isMember ? operands - 1 : operands;
            if (paramCount < c.MinArgs || paramCount > c.MaxArgs)
            {
                continue;
            }

            var score = 0;
            if (rightKind is not null && operands >= 2)
            {
                var idx = isMember ? 0 : 1;
                score = idx < c.ParamTypes.Count ? MatchScore(c.ParamTypes[idx], rightKind) : 0;
            }

            if (score > bestScore)
            {
                best = c;
                bestScore = score;
            }
        }

        return bestScore >= 0 || best is not null && bestScore > -1 ? best : null;
    }

    private static List<Entity> OperatorCandidates(Scope scope, Entity? cls, string opName)
    {
        var res = new List<Entity>();
        if (cls?.Own?.LookupHere(opName) is { } members)
        {
            res.AddRange(members);
        }

        if (scope.Lookup(opName) is { } frees)
        {
            res.AddRange(frees.Where(f => !res.Contains(f)));
        }

        if (cls?.DeclScope.Lookup(opName) is { } adl)
        {
            res.AddRange(adl.Where(f => !res.Contains(f)));
        }

        return res;
    }

    private bool InsideParenExpression(int a, int i)
    {
        var depth = 0;
        for (var k = i - 1; k >= a; k--)
        {
            if (_t[k].Kind != TokKind.Punct)
            {
                continue;
            }

            if (_t[k].Text is ")" or "]" or "}")
            {
                depth++;
            }
            else if (_t[k].Text is "(" or "[" or "{")
            {
                if (depth == 0)
                {
                    return _t[k].Text == "(" && !(k - 1 >= a && (Id(k - 1) && !Keywords.Contains(_t[k - 1].Text) || P(k - 1, ">") || P(k - 1, ")") || P(k - 1, "]")));
                }

                depth--;
            }
        }

        return false;
    }

    /// <summary>
    /// Emits a reference to an overloaded operator used at <c>_t[i]</c> when the (declared) type of the left operand is a
    /// class with a matching operator function. Returns true when something was emitted.
    /// </summary>
    private bool OperatorUse(Scope scope, int a, int b, int i, ref Entity? last, ref int lastIdx, ref Entity? opFn, ref int opEnd)
    {
        var t = _t[i];
        var text = t.Text;
        if (!EndsOperand(a, i - 1))
        {
            return false;
        }

        ExprType left;
        if (opFn is not null && opEnd == i - 1)
        {
            left = new ExprType(ClassOf(opFn.Type), opFn.Type?.Indirect ?? false);
        }
        else
        {
            left = TypeOfExprEnding(scope, a, i - 1);
        }

        if (left.Cls is null || left.IsPointer)
        {
            return false;
        }

        Entity? picked = null;
        switch (text)
        {
            case "++" or "--":
                picked = PickOperator(OperatorCandidates(scope, left.Cls, "operator" + text), 2, "int");
                break;
            case "(":
                if (!(Id(i - 1) || P(i - 1, ")") || P(i - 1, "}")) || (Id(i - 1) && scope.Lookup(_t[i - 1].Text)?.Any(static e => e.Kind is EKind.Class or EKind.Func) == true))
                {
                    return false;
                }

                picked = PickOperator(OperatorCandidates(scope, left.Cls, "operator()").Where(static c => c.DeclScope.Kind == SKind.Class), CountArgs(i) + 1, null);
                break;
            case "[":
                if (IsLambdaStart(i, a, b))
                {
                    return false;
                }

                picked = PickOperator(OperatorCandidates(scope, left.Cls, "operator[]"), CountArgs(i) + 1, null);
                break;
            case "->":
                picked = PickOperator(OperatorCandidates(scope, left.Cls, "operator->"), 1, null);
                if (picked is not null)
                {
                    Emit(t, picked, false);
                    last = picked;
                    lastIdx = i - 1;
                    return true;
                }

                return false;
            default:
                if (!BinaryOperators.Contains(text) || (text == "," && !InsideParenExpression(a, i)))
                {
                    return false;
                }

                var kind = OperandKind(scope, i + 1, b);
                picked = PickOperator(OperatorCandidates(scope, left.Cls, "operator" + text), 2, kind);
                if (picked is not null)
                {
                    Emit(t, picked, false);
                    opFn = picked;
                    opEnd = i + 1;
                    return true;
                }

                return false;
        }

        if (picked is null)
        {
            return false;
        }

        Emit(t, picked, false);
        opFn = picked;
        opEnd = text is "(" or "[" ? M(i) : i;
        return true;
    }

    /// <summary>User-defined literal suffixes: <c>123_km</c> (number token) and <c>"x"_s</c> (string followed by an identifier).</summary>
    private bool LiteralOperatorUse(Scope scope, int i)
    {
        var t = _t[i];
        string? suffix = null;
        var col = t.Col;
        if (t.Kind == TokKind.Number && t.Text.IndexOf('_') is var u and > 0)
        {
            suffix = t.Text[u..];
            col += u;
        }
        else if (t.Kind == TokKind.Ident && t.Text.StartsWith('_') && i > 0 && !t.Space && _t[i - 1].Kind is TokKind.StringLiteral or TokKind.CharLiteral)
        {
            suffix = t.Text;
        }

        if (suffix is null)
        {
            return false;
        }

        var ent = scope.Lookup("operator\"\"" + suffix)?.FirstOrDefault(static e => e.Kind == EKind.Func);
        if (ent is null)
        {
            return false;
        }

        Emit(t with { Col = col, Len = suffix.Length }, ent, false);
        return true;
    }

    /// <summary><c>operator new</c> / <c>::operator delete[]</c> spelled out in an expression.</summary>
    private int OperatorKeywordUse(Scope scope, int i, int b, bool leading)
    {
        var after = SkipOperatorName(i, b);
        if (after <= i + 1)
        {
            return i + 1;
        }

        var name = OperatorName(scope, i, after - 1);
        var cands = leading ? Global.LookupHere(name) : scope.Lookup(name);
        var argc = P(after, "(") ? CountArgs(after) : -1;
        if (Pick(cands, argc) is { } ent)
        {
            Emit(_t[i], ent, false);
        }

        return after;
    }

    /// <summary>Reference to the constructor selected by a variable declaration's initialiser (user-declared constructors only).</summary>
    private void ConstructorRef(Scope scope, Entity variable, Tok nameTok, int q, int stop)
    {
        var cls = ClassOf(variable.Type);
        if (cls?.Own is null || variable.Type is { Indirect: true } || !cls.Own.Names.TryGetValue("<ctor>", out var ctors))
        {
            return;
        }

        var cands = ctors.Where(static c => c.Kind == EKind.Func).ToList();
        if (cands.Count == 0)
        {
            return;
        }

        Tok at = nameTok;
        int argA, argB;
        if (P(q, "{") && M(q) > q)
        {
            (argA, argB) = (q + 1, M(q));
        }
        else if (P(q, "(") && M(q) > q)
        {
            (argA, argB) = (q + 1, M(q));
        }
        else if (P(q, "=") && q + 1 < stop)
        {
            var r = q + 1;
            if (P(r, "{") && M(r) > r)
            {
                at = _t[r];
                (argA, argB) = (r + 1, M(r));
            }
            else if (Id(r) && (P(r + 1, "(") || P(r + 1, "{")) && M(r + 1) > r + 1)
            {
                var callee = scope.Lookup(_t[r].Text)?.FirstOrDefault(static e => e.Kind is EKind.Class or EKind.Func or EKind.Typedef);
                if (callee is { Kind: EKind.Func } fn && fn.ReturnsRvalueRef)
                {
                    at = _t[r];
                    (argA, argB) = (r, r + 1); // single rvalue argument
                    EmitCtor(scope, cands, cls, at, argA, argB, forceRvalue: true);
                    return;
                }

                if (callee is not null && (callee == cls || ClassOf(new TypeRef(scope, [_t[r].Text], false)) == cls))
                {
                    at = _t[r];
                    (argA, argB) = (r + 2, M(r + 1));
                }
                else
                {
                    return;
                }
            }
            else if (Id(r) && r + 1 == stop)
            {
                var v = scope.Lookup(_t[r].Text)?.FirstOrDefault(static e => e.Kind is EKind.Var or EKind.Local or EKind.Param or EKind.Field);
                if (v is not null && ClassOf(v.Type) == cls && v.Type is { Indirect: false })
                {
                    at = _t[r];
                    EmitCtor(scope, cands, cls, at, r, r + 1, forceRvalue: false);
                }

                return;
            }
            else
            {
                return;
            }
        }
        else
        {
            (argA, argB) = (q, q);
        }

        EmitCtor(scope, cands, cls, at, argA, argB, forceRvalue: false);
    }

    private void EmitCtor(Scope scope, List<Entity> cands, Entity cls, Tok at, int argA, int argB, bool forceRvalue)
    {
        var argc = forceRvalue ? 1 : CountArgsIn(argA, argB);
        string? kind = null;
        if (forceRvalue)
        {
            kind = "rvalue";
        }
        else if (argc >= 1)
        {
            if (Id(argA) && (P(argA + 1, "(") || P(argA + 1, "{")) && M(argA + 1) > argA + 1)
            {
                var callee = scope.Lookup(_t[argA].Text)?.FirstOrDefault(static e => e.Kind is EKind.Class or EKind.Func);
                if (callee == cls || (callee is { Kind: EKind.Func } fn && fn.ReturnsRvalueRef))
                {
                    kind = "rvalue";
                }
            }
            else if (Id(argA) && argA + 1 == argB && scope.Lookup(_t[argA].Text)?.FirstOrDefault(static e => e.Kind is EKind.Var or EKind.Local or EKind.Param or EKind.Field) is { } v
                && ClassOf(v.Type) == cls && v.Type is { Indirect: false })
            {
                kind = "lvalue";
            }
        }

        var viable = cands.Where(c => argc >= c.MinArgs && argc <= c.MaxArgs).ToList();
        if (viable.Count == 0)
        {
            return;
        }

        Entity pick = viable[0];
        if (argc == 1 && kind is not null)
        {
            var match = viable.FirstOrDefault(c => c.ParamTypes.Count == 1 && (kind == "rvalue" ? c.ParamTypes[0].EndsWith("&&", StringComparison.Ordinal) : c.ParamTypes[0].StartsWith("const ", StringComparison.Ordinal) && c.ParamTypes[0].EndsWith('&')));
            if (match is not null)
            {
                pick = match;
            }
        }

        Emit(at, pick, false);
    }

    private int CountArgsIn(int a, int b)
    {
        if (b <= a)
        {
            return 0;
        }

        var n = 1;
        for (var k = a; k < b; k++)
        {
            if (_t[k].Kind == TokKind.Punct && _t[k].Text is "(" or "[" or "{" && M(k) > k)
            {
                k = M(k);
            }
            else if (P(k, ",") && _t[k].Kind == TokKind.Punct)
            {
                n++;
            }
        }

        return n;
    }
}
