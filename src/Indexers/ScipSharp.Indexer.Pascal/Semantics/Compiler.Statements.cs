using ScipSharp.Indexer.Pascal.Syntax;
using ScipKind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Pascal.Semantics;

internal sealed partial class Compiler
{
    private bool AtStatementEnd(string[] terminators)
    {
        if (AtEof || IsSym("."))
        {
            return true;
        }

        if (Cur.Kind != TokenKind.Keyword)
        {
            return false;
        }

        foreach (var t in terminators)
        {
            if (Cur.Lower == t)
            {
                return true;
            }
        }

        // Always stop at section keywords so a missing 'end' does not swallow the rest of the unit.
        return Cur.Lower is "implementation" or "initialization" or "finalization" || (Cur.Lower is "procedure" or "function" or "constructor" or "destructor" && Prev.IsSymbol(";"));
    }

    private void StatementList(params string[] terminators)
    {
        while (!AtStatementEnd(terminators))
        {
            var before = _i;
            Statement();
            if (!Accept(";") && !AtStatementEnd(terminators) && _i == before)
            {
                Next();
            }
        }
    }

    private void Statement()
    {
        // Labels: 10: stmt / done: stmt
        if ((IsIdent || Cur.Kind == TokenKind.Number) && Peek().IsSymbol(":") && !Peek().IsSymbol(":="))
        {
            var label = Next();
            Next();
            if (Lookup(label.Text)?.FirstOrDefault(static s => s.Kind == SymKind.Label) is { } ls)
            {
                Emit.Reference(label, ls);
            }
        }

        if (Cur.Kind == TokenKind.Keyword)
        {
            switch (Cur.Lower)
            {
                case "begin":
                    Next();
                    StatementList("end");
                    if (IsKw("end"))
                    {
                        Next();
                    }

                    return;
                case "if":
                    Next();
                    Expr();
                    if (IsKw("then"))
                    {
                        Next();
                    }

                    if (!IsKw("else") && !IsSym(";"))
                    {
                        Statement();
                    }

                    if (IsKw("else"))
                    {
                        Next();
                        Statement();
                    }

                    return;
                case "case":
                    CaseStatement();
                    return;
                case "while":
                    Next();
                    Expr();
                    if (IsKw("do"))
                    {
                        Next();
                    }

                    Statement();
                    return;
                case "repeat":
                    Next();
                    StatementList("until");
                    if (IsKw("until"))
                    {
                        Next();
                        Expr();
                    }

                    return;
                case "for":
                    ForStatement();
                    return;
                case "with":
                    WithStatement();
                    return;
                case "goto":
                    Next();
                    if (IsIdent || Cur.Kind == TokenKind.Number)
                    {
                        var label = Next();
                        if (Lookup(label.Text)?.FirstOrDefault(static s => s.Kind == SymKind.Label) is { } ls)
                        {
                            Emit.Reference(label, ls);
                        }
                    }

                    return;
                case "try":
                    TryStatement();
                    return;
                case "raise":
                    Next();
                    if (!IsSym(";") && !IsKw("end") && !IsKw("else") && !IsKw("except") && !IsKw("finally"))
                    {
                        Expr();
                        if (AcceptWord("at"))
                        {
                            Expr();
                            if (Accept(","))
                            {
                                Expr();
                            }
                        }
                    }

                    return;
                case "asm":
                    SkipAsm();
                    return;
                case "inherited":
                    SimpleStatement();
                    return;
                case "end" or "until" or "else" or "except" or "finally":
                    return;
            }
        }

        if (IsSym(";"))
        {
            return;
        }

        SimpleStatement();
    }

    private void SimpleStatement()
    {
        _lastOcc = null;
        var lhs = Expr();
        var target = _lastOcc;
        if (IsSym(":=") || IsSym("+=") || IsSym("-=") || IsSym("*=") || IsSym("/="))
        {
            if (target is not null && lhs.Sym is not null && target.Symbol.Length > 0)
            {
                target.SymbolRoles |= (int)SymbolRole.WriteAccess;
            }

            Next();
            Expr();
        }
    }

    private void CaseStatement()
    {
        Next(); // case
        Expr();
        if (IsKw("of"))
        {
            Next();
        }

        while (!AtEof && !IsKw("end"))
        {
            var before = _i;
            if (IsKw("else") || IsWord("otherwise"))
            {
                Next();
                Accept(":");
                StatementList("end");
                break;
            }

            // labels
            while (!AtEof && !IsSym(":") && !IsKw("end"))
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

                if (IsSym(";"))
                {
                    break;
                }
            }

            if (Accept(":"))
            {
                Statement();
            }

            Accept(";");
            if (_i == before)
            {
                Next();
            }
        }

        if (IsKw("end"))
        {
            Next();
        }
    }

    private void ForStatement()
    {
        Next(); // for
        AcceptWord("var");
        var savedLast = _lastOcc;
        _lastOcc = null;
        if (IsNameLike)
        {
            Designator();
        }

        if (_lastOcc is { } control)
        {
            control.SymbolRoles |= (int)SymbolRole.WriteAccess;
        }

        _lastOcc = savedLast;
        if (Accept(":="))
        {
            Expr();
            if (IsKw("to") || IsKw("downto"))
            {
                Next();
                Expr();
            }
        }
        else if (IsKw("in"))
        {
            Next();
            Expr();
        }

        if (IsKw("do"))
        {
            Next();
        }

        Statement();
    }

    private void WithStatement()
    {
        Next(); // with
        var mark = Mark();
        while (!AtEof)
        {
            var e = Expr();
            var t = e.Type;
            if (t is not null)
            {
                var s = TypeSystem.Structural(t);
                if (s.Kind is TypeKind.Pointer && s.Elem is { } pe)
                {
                    t = pe;
                }

                Push(new MemberLayer(t, Types));
            }
            else if (e.Sym is { Kind: SymKind.Unit or SymKind.Program, Unit: { } u })
            {
                Push(u.Interface);
            }

            if (!Accept(","))
            {
                break;
            }
        }

        if (IsKw("do"))
        {
            Next();
        }

        Statement();
        Restore(mark);
    }

    private void TryStatement()
    {
        Next(); // try
        StatementList("except", "finally", "end");
        if (IsKw("except"))
        {
            Next();
            if (IsWord("on"))
            {
                while (IsWord("on"))
                {
                    Handler();
                    Accept(";");
                }

                if (IsKw("else"))
                {
                    Next();
                    StatementList("end");
                }
            }
            else
            {
                StatementList("end");
            }
        }

        if (IsKw("finally"))
        {
            Next();
            StatementList("end");
        }

        if (IsKw("end"))
        {
            Next();
        }
    }

    private void Handler()
    {
        Next(); // on
        var mark = Mark();
        Token? varTok = null;
        if (IsIdent && Peek().IsSymbol(":"))
        {
            varTok = Next();
            Next();
        }

        var typeStart = Cur;
        var type = TypeRefOrSubrange();
        if (varTok is not null)
        {
            var scope = new Scope(null);
            var v = new Sym(varTok.Text, SymKind.Var, _currentMethod ?? _unit.Sym)
            {
                ScipKind = ScipKind.Variable,
                Type = type,
                HomeSource = varTok.Source,
                Signature = $"{varTok.Text}: {Slice(typeStart, Prev, 100)}",
            };
            Emit.Definition(varTok, v);
            scope.Add(v);
            Push(scope);
        }

        if (IsKw("do"))
        {
            Next();
        }

        Statement();
        Restore(mark);
    }
}
