using System.Runtime.CompilerServices;
using ScipSharp.Indexer.Sql.Lexing;

namespace ScipSharp.Indexer.Sql.Syntax;

/// <summary>A lexed, split and parsed SQL file.</summary>
public sealed record ParsedScript(SourceText Source, LexResult Lex, List<StatementSpan> Spans, List<Stmt> Statements);

/// <summary>
/// Tolerant recursive-descent SQL parser. It never fails: constructs it does not understand are skipped, and the
/// expressions they contain are still collected (as <see cref="GenericStmt"/>) so that names inside them resolve.
/// </summary>
public sealed partial class SqlParser
{
    private readonly List<Token> _t;
    private readonly SqlDialect _d;
    private readonly string _text;
    private readonly bool _procedural;
    private readonly Token _eof;
    private int _p;
    private int _end;
    private int _depth;

    /// <summary>Extra clause-level stop condition for <see cref="ParseExpr"/> (column constraints after DEFAULT).</summary>
    private Func<Token, bool>? _stop;

    private SqlParser(string text, List<Token> tokens, SqlDialect dialect, bool procedural)
    {
        _text = text;
        _t = tokens;
        _d = dialect;
        _procedural = procedural;
        _eof = tokens[^1];
    }

    public SqlDialect Dialect => _d;

    /// <summary>Lexes, splits and parses a whole file.</summary>
    public static ParsedScript ParseScript(string text, SqlDialect dialect)
    {
        var lex = SqlLexer.Lex(text, dialect);
        var spans = StatementSplitter.Split(lex.Tokens, dialect);
        var parser = new SqlParser(text, lex.Tokens, dialect, procedural: false);
        var statements = new List<Stmt>();
        var batch = 0;
        var next = 0;
        for (var spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            var span = spans[spanIndex];
            for (; next < span.Start; next++)
            {
                if (lex.Tokens[next].Kind == TokenKind.BatchSeparator)
                {
                    batch++;
                }
            }

            foreach (var s in parser.ParseRange(span.Start, span.End))
            {
                s.Batch = batch;
                s.Span = spanIndex;
                statements.Add(s);
            }
        }

        return new ParsedScript(new SourceText(text), lex, spans, statements);
    }

    /// <summary>Parses the content of a string token (PostgreSQL function body, <c>DO</c> block) as procedural SQL.</summary>
    private List<Stmt> ParseEmbedded(Token body)
    {
        int start, end;
        var dollar = SqlLexer.DollarContentStart(body);
        if (dollar >= 0)
        {
            start = dollar;
            end = body.End - (dollar - body.Start);
        }
        else if (SqlLexer.QuotedContentStart(body) is var q and >= 0)
        {
            start = q;
            end = body.End - 1;
        }
        else
        {
            return [];
        }

        if (end <= start || _depth > 8)
        {
            return [];
        }

        var lex = SqlLexer.Lex(_text, start, end, _d);
        var spans = StatementSplitter.Split(lex.Tokens, _d, procedural: true);
        var parser = new SqlParser(_text, lex.Tokens, _d, procedural: true) { _depth = _depth + 1 };
        var result = new List<Stmt>();
        foreach (var span in spans)
        {
            result.AddRange(parser.ParseRange(span.Start, span.End));
        }

        return result;
    }

    internal List<Stmt> ParseRange(int start, int end)
    {
        _p = start;
        _end = end;
        return ParseStatements();
    }

    // ------------------------------------------------------------ cursor helpers

    private Token Cur => _p < _end ? _t[_p] : _eof;

    private Token At(int k) => _p + k < _end ? _t[_p + k] : _eof;

    private bool AtEnd => _p >= _end || _t[_p].Kind == TokenKind.EndOfFile;

    private bool Is(string keyword) => Cur.Is(keyword);

    private bool IsAt(int k, string keyword) => At(k).Is(keyword);

    private bool IsOp(string op) => Cur.IsOp(op);

    private bool Accept(string keyword)
    {
        if (Cur.Is(keyword))
        {
            _p++;
            return true;
        }

        return false;
    }

    private bool AcceptSeq(params string[] keywords)
    {
        for (var k = 0; k < keywords.Length; k++)
        {
            if (!At(k).Is(keywords[k]))
            {
                return false;
            }
        }

        _p += keywords.Length;
        return true;
    }

    private bool AcceptOp(string op)
    {
        if (Cur.IsOp(op))
        {
            _p++;
            return true;
        }

        return false;
    }

    private Token Prev => _p > 0 ? _t[_p - 1] : _eof;

    /// <summary>
    /// Guards the genuinely recursive constructs (nested parentheses, subqueries, blocks): pathological nesting must not
    /// overflow the stack (which kills the process). The exception is caught per statement in <see cref="ParseStatements"/>.
    /// </summary>
    private static void EnsureStack()
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new InsufficientExecutionStackException("SQL nesting too deep");
        }
    }

    private Ident MakeIdent(Token t) => t.Kind switch
    {
        TokenKind.QuotedIdent => new Ident(t, t.Value, _d.Normalize(t.Value, quoted: true), true),
        TokenKind.Template => new Ident(t, TemplateName(t.Value), _d.Normalize(TemplateName(t.Value), quoted: false), false),
        _ => new Ident(t, t.Text, _d.Normalize(t.Text, quoted: false), false),
    };

    private static string TemplateName(string value)
    {
        var colon = value.IndexOf(':', StringComparison.Ordinal);
        var name = colon >= 0 ? value[(colon + 1)..] : value;
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
    }

    /// <summary>True for a word that can be an identifier here (quoted, or not a reserved word).</summary>
    private static bool IsNameToken(Token t) =>
        t.Kind == TokenKind.QuotedIdent || (t.Kind == TokenKind.Word && !Keywords.IsReserved(t.Upper));

    private Ident? ParseIdent(bool allowReserved = false)
    {
        var t = Cur;
        if (t.Kind == TokenKind.QuotedIdent || (t.Kind == TokenKind.Word && (allowReserved || !Keywords.IsReserved(t.Upper))))
        {
            _p++;
            return MakeIdent(t);
        }

        return null;
    }

    /// <summary>Parses <c>a.b.c</c> (also dbt templates and T-SQL <c>db..t</c>).</summary>
    private QName? ParseQName(bool allowReserved = false)
    {
        if (Cur.Kind == TokenKind.Template && Cur.Value.Length > 0)
        {
            var tt = Cur;
            _p++;
            if (tt.Value.StartsWith("source:", StringComparison.Ordinal) && tt.Value.IndexOf('.') is var dot and > 0)
            {
                var schema = tt.Value["source:".Length..dot];
                return new QName([new Ident(tt, schema, _d.Normalize(schema, quoted: false), false), MakeIdent(tt)]);
            }

            return new QName([MakeIdent(tt)]);
        }

        var first = ParseIdent(allowReserved);
        if (first is null)
        {
            return null;
        }

        var parts = new List<Ident> { first };
        while (IsOp("."))
        {
            var next = At(1);
            if (next.IsName)
            {
                _p += 2;
                parts.Add(MakeIdent(next));
            }
            else if (next.IsOp("."))
            {
                _p++; // db..table
            }
            else
            {
                break;
            }
        }

        return new QName(parts);
    }

    private List<Ident> ParseIdentList()
    {
        // At '(' : (a, b(10) ASC, c)
        var list = new List<Ident>();
        if (!AcceptOp("("))
        {
            return list;
        }

        while (!AtEnd && !IsOp(")"))
        {
            if (Cur.IsWord && Cur.Upper is "PERIOD" && At(1).IsName)
            {
                _p++; // temporal keys: (id, PERIOD valid_at)
            }

            if (ParseIdent(allowReserved: true) is { } id)
            {
                list.Add(id);
            }

            // skip anything up to the next comma at this level (lengths, ASC, opclasses, ...)
            while (!AtEnd && !IsOp(",") && !IsOp(")") && !Cur.IsTerminator)
            {
                SkipBalanced();
            }

            if (!AcceptOp(","))
            {
                break;
            }
        }

        AcceptOp(")");
        return list;
    }

    /// <summary>Skips one token, or a whole balanced parenthesised group.</summary>
    private void SkipBalanced()
    {
        if (!IsOp("("))
        {
            _p++;
            return;
        }

        var depth = 0;
        while (!AtEnd)
        {
            if (IsOp("("))
            {
                depth++;
            }
            else if (IsOp(")"))
            {
                depth--;
                if (depth == 0)
                {
                    _p++;
                    return;
                }
            }
            else if (Cur.IsTerminator)
            {
                return;
            }

            _p++;
        }
    }

    /// <summary>Skips to the end of the statement (or, in T-SQL, to the next statement-starting keyword).</summary>
    private void SkipToStatementEnd()
    {
        while (!AtEnd && !Cur.IsSemi)
        {
            if (_d == SqlDialect.TSql && Cur.IsWord && Keywords.StatementStarts.Contains(Cur.Upper) && !(Cur.Is("WITH") && At(1).IsOp("(")))
            {
                return;
            }

            SkipBalanced();
        }
    }

    private bool IsQueryStart(int k = 0)
    {
        var t = At(k);
        if (t.Is("SELECT") || t.Is("VALUES"))
        {
            return true;
        }

        if (t.Is("WITH"))
        {
            return At(k + 1).IsName && !At(k + 1).IsOp("(");
        }

        return t.IsOp("(") && k < 8 && IsQueryStart(k + 1);
    }

    // ------------------------------------------------------------ statements

    private List<Stmt> ParseStatements()
    {
        var list = new List<Stmt>();
        while (!AtEnd)
        {
            if (Cur.IsTerminator)
            {
                _p++;
                continue;
            }

            var start = _p;
            Stmt? s;
            try
            {
                s = ParseStatement();
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NullReferenceException or InsufficientExecutionStackException)
            {
                _p = start;
                s = null;
                while (!AtEnd && !Cur.IsSemi)
                {
                    _p++;
                }
            }

            if (_p <= start)
            {
                _p = start + 1;
                continue;
            }

            if (s is not null)
            {
                Finish(s, start);
                list.Add(s);
            }
        }

        return list;
    }

    private T Finish<T>(T s, int start)
        where T : Stmt
    {
        s.First = _t[Math.Min(start, _t.Count - 1)];
        s.Last = _t[Math.Clamp(_p - 1, start, _t.Count - 1)];
        return s;
    }

    private Stmt? ParseStatement()
    {
        EnsureStack();
        var t = Cur;
        if (t.Kind == TokenKind.Op)
        {
            if (t.Text == "(" && IsQueryStart(1))
            {
                return ParseQueryStatement();
            }

            if (t.Text == "<<")
            {
                // PL/SQL & PL/pgSQL block label <<name>>
                while (!AtEnd && !IsOp(">>"))
                {
                    _p++;
                }

                _p++;
                return null;
            }

            return ParseGeneric();
        }

        if (t.Kind != TokenKind.Word)
        {
            return ParseGeneric();
        }

        if (At(1).IsOp(":") && !Keywords.IsReserved(t.Upper) && !At(2).IsOp("="))
        {
            _p += 2; // label:
            return null;
        }

        switch (t.Upper)
        {
            case "CREATE":
                return ParseCreate();
            case "ALTER":
                return ParseAlter();
            case "DROP":
                return ParseDrop();
            case "COMMENT" when IsAt(1, "ON"):
                return ParseCommentOn();
            case "SELECT" or "VALUES":
                return ParseQueryStatement();
            case "WITH":
                return ParseWithStatement();
            case "INSERT" or "UPSERT":
                return ParseInsert();
            case "REPLACE" when IsAt(1, "INTO") || _d == SqlDialect.MySql:
                return ParseInsert();
            case "UPDATE" when !IsAt(1, "STATISTICS"):
                return ParseUpdate();
            case "DELETE":
                return ParseDelete();
            case "MERGE":
                return ParseMerge();
            case "TRUNCATE" or "LOCK" or "ANALYZE" or "ANALYSE" or "VACUUM" or "REINDEX" or "CLUSTER" or "OPTIMIZE" or "REFRESH"
                or "DESCRIBE" or "DESC" or "CHECKSUM" or "REPAIR" or "UPDATE" or "COPY" or "UNLOCK":
                return ParseTableRefs();
            case "GRANT" or "REVOKE" or "DENY":
                return ParseGrant();
            case "USE":
                {
                    _p++;
                    var db = ParseIdent(allowReserved: true);
                    return db is null ? null : new SetSchemaStmt([db], isUse: true);
                }

            case "SET":
                return ParseSet();
            case "DECLARE":
                return ParseDeclare();
            case "EXEC" or "EXECUTE" when !IsAt(1, "IMMEDIATE"):
                return ParseExec();
            case "CALL":
                return ParseCall();
            case "PERFORM":
                return ParseQueryStatement(perform: true);
            case "FOR" or "FOREACH" when _d != SqlDialect.TSql:
                return ParseFor();
            case "RENAME" when IsAt(1, "TABLE"):
                return ParseRenameTable();
            case "RETURN":
                {
                    _p++;
                    if (Accept("QUERY") && !Is("EXECUTE"))
                    {
                        return ParseQueryStatement();
                    }

                    return ParseGeneric();
                }

            case "DO" when At(1).Kind == TokenKind.String || (IsAt(1, "LANGUAGE") && At(3).Kind == TokenKind.String):
                {
                    _p++;
                    if (Accept("LANGUAGE"))
                    {
                        _p++;
                    }

                    var body = Cur;
                    _p++;
                    return new AnonymousBlockStmt(ParseEmbedded(body));
                }

            case "EXPLAIN":
                _p++;
                while (Is("ANALYZE") || Is("VERBOSE") || Is("PLAN") || Is("QUERY") || Is("FOR") || IsOp("("))
                {
                    SkipBalanced();
                }

                return ParseStatement();
            case "PREPARE" when _d == SqlDialect.Postgres:
                while (!AtEnd && !Is("AS") && !Cur.IsSemi)
                {
                    SkipBalanced();
                }

                Accept("AS");
                return ParseStatement();
            case "BEGIN":
                _p++;
                if (!Accept("TRANSACTION") && !Accept("TRAN") && !Accept("WORK") && !Accept("ATOMIC") && !Accept("TRY") && !Accept("CATCH"))
                {
                    AcceptSeq("DISTRIBUTED", "TRANSACTION");
                }

                return null;
            case "END":
                _p++;
                if (Cur.IsWord && Cur.Upper is "IF" or "LOOP" or "CASE" or "WHILE" or "REPEAT" or "FOR" or "TRY" or "CATCH" or "TRANSACTION")
                {
                    _p++;
                }

                if (Cur.IsName && (At(1).IsTerminator || _p + 1 >= _end))
                {
                    _p++; // END label;
                }

                return null;
            case "ELSE" or "LOOP" or "THEN" or "EXCEPTION" or "REPEAT" or "TRY" or "CATCH" or "DO":
                _p++;
                return null;
            case "IF" or "ELSIF" or "ELSEIF" or "WHILE" or "UNTIL" or "WHEN" or "CASE" or "EXIT" or "CONTINUE" or "RAISE" or "PRINT"
                or "THROW" or "RAISERROR" or "OPEN" or "CLOSE" or "FETCH" or "DEALLOCATE" or "GOTO" or "LEAVE" or "ITERATE"
                or "SIGNAL" or "RESIGNAL" or "ASSERT" or "GET" or "BREAK" or "WAITFOR" or "COMMIT" or "ROLLBACK" or "SAVEPOINT"
                or "RELEASE" or "START" or "PIPE" or "NULL" or "FORALL":
                return ParseGeneric();
        }

        // PL/SQL procedure call statement: name(args); or pkg.proc;
        if (_procedural || _d == SqlDialect.PlSql)
        {
            var save = _p;
            if (IsNameToken(t) && ParseQName() is { } callee)
            {
                if (IsOp("(") && _d == SqlDialect.PlSql)
                {
                    var call = ParseFunctionCall(callee);
                    if (Cur.IsTerminator || AtEnd)
                    {
                        return new CallStmt(callee, call.Args);
                    }
                }
                else if ((Cur.IsTerminator || AtEnd) && _d == SqlDialect.PlSql && callee.Parts.Count > 1)
                {
                    return new CallStmt(callee, []);
                }
            }

            _p = save;
        }

        return ParseGeneric();
    }

    /// <summary>Anything else: collect expressions (and nested queries) until the statement ends.</summary>
    private GenericStmt ParseGeneric()
    {
        var g = new GenericStmt();
        var start = _p;
        while (!AtEnd && !Cur.IsTerminator)
        {
            var before = _p;
            if (IsQueryStart() && _p > start)
            {
                if (_d == SqlDialect.TSql || _procedural || Prev.IsWord && Keywords.StatementStarts.Contains(Prev.Upper))
                {
                    break;
                }

                g.Exprs.Add(new Expr([new SubqueryExpr(ParseQuery())]));
                continue;
            }

            var e = ParseExpr();
            if (e.Items.Count > 0)
            {
                g.Exprs.Add(e);
            }

            if (_p == before)
            {
                if (_p > start && Cur.IsWord && Keywords.StatementStarts.Contains(Cur.Upper))
                {
                    break;
                }

                if (_p > start && Cur.IsWord && Cur.Upper is "THEN" or "LOOP" or "DO" or "ELSE" or "WHEN" or "BEGIN")
                {
                    break;
                }

                _p++;
            }
        }

        return g;
    }
}
