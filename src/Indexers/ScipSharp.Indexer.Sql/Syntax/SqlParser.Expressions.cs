using ScipSharp.Indexer.Sql.Lexing;

namespace ScipSharp.Indexer.Sql.Syntax;

public sealed partial class SqlParser
{
    private static readonly HashSet<string> DatePartFunctions =
        ["EXTRACT", "DATEADD", "DATEDIFF", "DATEDIFF_BIG", "DATEPART", "DATENAME", "DATETRUNC", "DATE_BUCKET", "TIMESTAMPADD", "TIMESTAMPDIFF", "DATE_PART"];

    /// <summary>
    /// Parses an expression. In clause position (<paramref name="paren"/> false) it ends at a clause keyword, a comma,
    /// a closing parenthesis or an implicit alias; inside parentheses only <c>,</c> and <c>)</c> end it.
    /// </summary>
    private Expr ParseExpr(bool paren = false)
    {
        EnsureStack();
        var items = new List<ExprItem>();
        var caseDepth = 0;
        var operand = false;
        while (!AtEnd)
        {
            var t = Cur;
            if (t.IsTerminator || (!paren && caseDepth == 0 && _stop is not null && _stop(t)))
            {
                break;
            }

            switch (t.Kind)
            {
                case TokenKind.Op:
                    switch (t.Text)
                    {
                        case "," or ")" or "]":
                            return new Expr(items);
                        case "(":
                            ParseParenGroup(items);
                            operand = true;
                            continue;
                        case "[":
                            _p++;
                            while (!AtEnd && !IsOp("]") && !Cur.IsTerminator && !IsOp(")"))
                            {
                                var before = _p;
                                items.AddRange(ParseExpr(paren: true).Items);
                                if (!AcceptOp(",") && !AcceptOp(":") && _p == before)
                                {
                                    _p++;
                                }
                            }

                            AcceptOp("]");
                            operand = true;
                            continue;
                        case "::":
                            _p++;
                            ParseTypeInto(items);
                            operand = true;
                            continue;
                        case "*" when !operand:
                            items.Add(new StarRef(null, t));
                            _p++;
                            operand = true;
                            continue;
                        case "%TYPE" or "%ROWTYPE":
                            _p++;
                            operand = true;
                            continue;
                        case "=>":
                            // Named argument: the preceding name is a parameter, not a column.
                            if (items.Count > 0 && items[^1] is NameRef { Name.Parts.Count: 1 })
                            {
                                items.RemoveAt(items.Count - 1);
                            }

                            _p++;
                            operand = false;
                            continue;
                        case "." when operand && At(1).IsName:
                            _p += 2; // (expr).field
                            continue;
                        case "." when operand && At(1).IsOp("*"):
                            _p += 2;
                            continue;
                    }

                    _p++;
                    operand = false;
                    continue;
                case TokenKind.String or TokenKind.Number:
                    if (operand && !paren && t.Kind == TokenKind.String && _d == SqlDialect.TSql)
                    {
                        return new Expr(items); // SELECT x 'alias'
                    }

                    _p++;
                    operand = true;
                    continue;
                case TokenKind.Variable:
                    if (operand && !paren)
                    {
                        return new Expr(items);
                    }

                    items.Add(ParseVarRef());
                    operand = true;
                    continue;
                case TokenKind.Template:
                    _p++;
                    operand = true;
                    continue;
            }

            if (t.Kind == TokenKind.Word)
            {
                var u = t.Upper;
                if (caseDepth > 0 && u is "WHEN" or "THEN" or "ELSE")
                {
                    _p++;
                    operand = false;
                    continue;
                }

                if (u == "CASE")
                {
                    caseDepth++;
                    _p++;
                    operand = false;
                    continue;
                }

                if ((u == "ARRAY" && At(1).IsOp("[")) || (u is "FIRST" or "LAST" && Prev.Is("DENSE_RANK")))
                {
                    _p++;
                    operand = false;
                    continue;
                }

                if (u is "IGNORE" or "RESPECT" && IsAt(1, "NULLS"))
                {
                    _p += 2;
                    continue;
                }

                if (u == "NEXT" && IsAt(1, "VALUE") && IsAt(2, "FOR"))
                {
                    _p += 3;
                    if (ParseQName() is { } seq)
                    {
                        items.Add(new ObjectRef(seq, ObjectKind.Sequence));
                    }

                    operand = true;
                    continue;
                }

                if (u == "END" && caseDepth > 0)
                {
                    caseDepth--;
                    _p++;
                    operand = true;
                    continue;
                }

                var isCall = At(1).IsOp("(") && (Keywords.FunctionLike.Contains(u) || !Keywords.IsReserved(u));
                if (!isCall)
                {
                    if (!paren && Keywords.ClauseStops.Contains(u))
                    {
                        return new Expr(items);
                    }

                    if (u == "AS" && paren)
                    {
                        _p++;
                        ParseTypeInto(items);
                        operand = true;
                        continue;
                    }

                    if (u == "AT" && IsAt(1, "TIME") && IsAt(2, "ZONE"))
                    {
                        _p += 3;
                        operand = false;
                        continue;
                    }

                    if (u == "IS")
                    {
                        _p++;
                        Accept("NOT");
                        if (Accept("DISTINCT"))
                        {
                            Accept("FROM");
                        }

                        operand = false;
                        continue;
                    }

                    if (u == "INTERVAL" && At(1).Kind is TokenKind.String or TokenKind.Number)
                    {
                        _p += 2;
                        while (Cur.IsWord && Cur.Upper is "YEAR" or "YEARS" or "MONTH" or "MONTHS" or "DAY" or "DAYS" or "HOUR" or "HOURS" or "MINUTE" or "MINUTES" or "SECOND" or "SECONDS" or "TO" or "WEEK" or "WEEKS")
                        {
                            _p++;
                        }

                        operand = true;
                        continue;
                    }

                    if (Keywords.IsReserved(u))
                    {
                        _p++;
                        operand = Keywords.Operands.Contains(u);
                        if (u is "BETWEEN" or "AND" or "OR" or "NOT" or "LIKE" or "IN")
                        {
                            operand = false;
                        }

                        continue;
                    }
                }

                // Typed literal: DATE '2020-01-01', TIMESTAMP '...', n'..' is lexed as a string already.
                if (!isCall && At(1).Kind == TokenKind.String && !operand && At(1).Text.Length > 0 && At(1).Text[0] == '\'')
                {
                    _p += 2;
                    operand = true;
                    continue;
                }
            }

            // A name.
            if (operand && !paren)
            {
                return new Expr(items); // implicit alias
            }

            var name = ParseNameChain(out var star);
            if (star is not null)
            {
                items.Add(new StarRef(name, star));
            }
            else if (IsOp("("))
            {
                items.Add(ParseFunctionCall(name));
            }
            else
            {
                items.Add(new NameRef(name));
            }

            operand = true;
        }

        return new Expr(items);
    }

    private VarRef ParseVarRef()
    {
        var v = Cur;
        _p++;
        Ident? field = null;
        if (IsOp(".") && At(1).IsName)
        {
            field = MakeIdent(At(1));
            _p += 2;
        }

        return new VarRef(v, field);
    }

    private QName ParseNameChain(out Token? star)
    {
        star = null;
        var parts = new List<Ident> { MakeIdent(Cur) };
        _p++;
        while (IsOp("."))
        {
            var next = At(1);
            if (next.IsName)
            {
                parts.Add(MakeIdent(next));
                _p += 2;
            }
            else if (next.IsOp("*"))
            {
                star = next;
                _p += 2;
                break;
            }
            else if (next.IsOp(".") && _d == SqlDialect.TSql)
            {
                _p++;
            }
            else
            {
                break;
            }
        }

        return new QName(parts);
    }

    private void ParseParenGroup(List<ExprItem> items)
    {
        _p++; // (
        if (IsQueryStart())
        {
            items.Add(new SubqueryExpr(ParseQuery()));
            SkipToClose();
            return;
        }

        while (!AtEnd && !IsOp(")") && !Cur.IsTerminator)
        {
            var before = _p;
            items.AddRange(ParseExpr(paren: true).Items);
            if (!AcceptOp(",") && _p == before)
            {
                _p++;
            }
        }

        AcceptOp(")");
    }

    /// <summary>Skips forward to (and over) the next <c>)</c> at the current level.</summary>
    private void SkipToClose()
    {
        while (!AtEnd && !IsOp(")") && !Cur.IsTerminator)
        {
            SkipBalanced();
        }

        AcceptOp(")");
    }

    private FuncCall ParseFunctionCall(QName name)
    {
        var call = new FuncCall(name, []);
        _p++; // (
        var upper = name.Parts.Count == 1 && name.Last.Token.IsWord ? name.Last.Token.Upper : "";
        Accept("DISTINCT");
        Accept("ALL");
        if (IsOp("*") && At(1).IsOp(")"))
        {
            _p++;
        }

        if (DatePartFunctions.Contains(upper) && Cur.IsWord && (At(1).IsOp(",") || At(1).Is("FROM")))
        {
            _p++;
            AcceptOp(",");
            Accept("FROM");
        }

        if (upper is "CONVERT" or "TRY_CONVERT" && _d == SqlDialect.TSql && Cur.IsName && (At(1).IsOp(",") || At(1).IsOp("(")))
        {
            ParseType(null);
            AcceptOp(",");
        }

        if (Cur.Kind == TokenKind.String && (At(1).IsOp(")") || At(1).IsOp(",") || At(1).IsOp("::")))
        {
            call.FirstStringArg = Cur;
        }

        if (IsQueryStart())
        {
            call.Args.Add(new Expr([new SubqueryExpr(ParseQuery())]));
            call.ArgCount = 1;
        }

        if (!IsOp(")"))
        {
            call.ArgCount++;
        }

        while (!AtEnd && !IsOp(")") && !Cur.IsTerminator)
        {
            var before = _p;
            var e = ParseExpr(paren: true);
            if (e.Items.Count > 0)
            {
                call.Args.Add(e);
            }

            if (AcceptOp(","))
            {
                call.ArgCount++;
            }
            else if (_p == before)
            {
                _p++;
            }
        }

        AcceptOp(")");

        // Trailing clauses: WITHIN GROUP (...), FILTER (...), OVER (...) | OVER w, IGNORE/RESPECT NULLS, KEEP (...)
        while (true)
        {
            if (AcceptSeq("WITHIN", "GROUP") || ((Is("FILTER") || Is("KEEP")) && At(1).IsOp("(") && Accept(Cur.Upper)))
            {
                var items = new List<ExprItem>();
                if (IsOp("("))
                {
                    ParseParenGroup(items);
                }

                call.Extra.Add(new Expr(items));
                continue;
            }

            if (AcceptSeq("IGNORE", "NULLS") || AcceptSeq("RESPECT", "NULLS"))
            {
                continue;
            }

            if (Is("OVER"))
            {
                _p++;
                if (IsOp("("))
                {
                    var items = new List<ExprItem>();
                    ParseParenGroup(items);
                    call.Extra.Add(new Expr(items));
                }
                else if (ParseIdent() is { } w)
                {
                    call.WindowName = w;
                }

                continue;
            }

            break;
        }

        return call;
    }

    /// <summary>Parses a data type (<c>int</c>, <c>numeric(10,2)</c>, <c>public.mood[]</c>, <c>timestamp with time zone</c>).</summary>
    private void ParseTypeInto(List<ExprItem> items) => ParseType(items);

    private string ParseType(List<ExprItem>? items, Func<Token, bool>? stop = null)
    {
        var start = _p;
        if (!Cur.IsName)
        {
            return "";
        }

        var q = ParseQName(allowReserved: true);
        if (q is not null && items is not null && !Keywords.BuiltinTypes.Contains(q.Last.Token.Upper))
        {
            items.Add(new TypeRef(q));
        }

        while (!AtEnd)
        {
            if (stop is not null && stop(Cur))
            {
                break;
            }

            if (Cur.IsWord && Cur.Upper is "YEAR" or "MONTH" or "DAY" or "HOUR" or "MINUTE" or "SECOND" or "TO"
                && (Prev.IsWord && Prev.Upper is "INTERVAL" or "YEAR" or "MONTH" or "DAY" or "HOUR" or "MINUTE" or "SECOND" or "TO" || (Prev.IsOp(")") && _t[start].Is("INTERVAL"))))
            {
                _p++;
                continue;
            }

            if (Cur.IsWord && Keywords.TypeContinuations.Contains(Cur.Upper))
            {
                _p++;
                continue;
            }

            if ((Is("WITH") || Is("WITHOUT")) && IsAt(1, "TIME") && IsAt(2, "ZONE"))
            {
                _p += 3;
                continue;
            }

            if (Is("WITH") && IsAt(1, "LOCAL") && IsAt(2, "TIME"))
            {
                _p += 4;
                continue;
            }

            if (Is("CHARACTER") && IsAt(1, "SET") || Is("CHARSET"))
            {
                _p += Is("CHARSET") ? 1 : 2;
                _p++;
                continue;
            }

            if (IsOp("("))
            {
                SkipBalanced();
                continue;
            }

            if (IsOp("["))
            {
                while (!AtEnd && !IsOp("]"))
                {
                    _p++;
                }

                _p++;
                continue;
            }

            if (Is("ARRAY") || IsOp("%TYPE") || IsOp("%ROWTYPE"))
            {
                _p++;
                continue;
            }

            break;
        }

        return TextOf(start, _p);
    }

    /// <summary>Source text of tokens <c>[from, to)</c> with whitespace collapsed.</summary>
    private string TextOf(int from, int to)
    {
        if (to <= from)
        {
            return "";
        }

        var raw = _text[_t[from].Start.._t[to - 1].End];
        return string.Join(' ', raw.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }
}
