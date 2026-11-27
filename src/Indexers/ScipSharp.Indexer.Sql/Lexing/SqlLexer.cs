using System.Text;

namespace ScipSharp.Indexer.Sql.Lexing;

/// <summary>Result of lexing a range of a source text.</summary>
public sealed record LexResult(List<Token> Tokens, List<Comment> Comments);

/// <summary>
/// Dialect-aware SQL lexer. Handles quoted identifiers (<c>"x"</c>, <c>[x]</c>, <c>`x`</c>), string flavours
/// (<c>''</c>-escaped, <c>E''</c>/<c>N''</c>/<c>X''</c>/<c>B''</c>/<c>U&amp;''</c>, Oracle <c>q'[..]'</c>, PostgreSQL
/// <c>$tag$..$tag$</c>, MySQL backslash escapes and double-quoted strings), nested block comments (PostgreSQL, T-SQL),
/// MySQL <c>#</c> comments and <c>DELIMITER</c> directives, T-SQL <c>GO</c> and SQL*Plus <c>/</c> separators, psql and
/// SQL*Plus client commands (skipped as comments) and Jinja/dbt templates.
/// </summary>
public sealed class SqlLexer
{
    private readonly string _s;
    private readonly int _end;
    private readonly SqlDialect _d;
    private readonly List<Token> _tokens = [];
    private readonly List<Comment> _comments = [];
    private int _i;
    private bool _atLineStart = true;
    private string? _delimiter;

    private SqlLexer(string text, int start, int end, SqlDialect dialect)
    {
        _s = text;
        _i = start;
        _end = end;
        _d = dialect;
        _atLineStart = start == 0 || text[start - 1] == '\n';
    }

    public static LexResult Lex(string text, SqlDialect dialect) => Lex(text, 0, text.Length, dialect);

    /// <summary>Lexes <c>text[start..end)</c>; token offsets stay absolute (used for dollar-quoted function bodies).</summary>
    public static LexResult Lex(string text, int start, int end, SqlDialect dialect)
    {
        var lexer = new SqlLexer(text, start, end, dialect);
        lexer.Run();
        return new LexResult(lexer._tokens, lexer._comments);
    }

    private char Peek(int k = 0) => _i + k < _end ? _s[_i + k] : '\0';

    private void Run()
    {
        while (_i < _end)
        {
            var c = _s[_i];
            if (c == '\n')
            {
                _i++;
                _atLineStart = true;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                _i++;
                continue;
            }

            if (_atLineStart && LineDirective())
            {
                continue;
            }

            _atLineStart = false;
            LexOne(c);
        }

        _tokens.Add(new Token(TokenKind.EndOfFile, _end, _end, "", ""));
    }

    /// <summary>Handles constructs only recognised as the first thing on a line. Returns true when it consumed input.</summary>
    private bool LineDirective()
    {
        var lineEnd = _s.IndexOf('\n', _i, _end - _i);
        if (lineEnd < 0)
        {
            lineEnd = _end;
        }

        var line = _s.AsSpan(_i, lineEnd - _i).TrimEnd();
        if (_d == SqlDialect.TSql && IsGoLine(line))
        {
            Add(TokenKind.BatchSeparator, _i, _i + line.Length);
            _i = lineEnd;
            return true;
        }

        if (_d == SqlDialect.PlSql && line is "/")
        {
            Add(TokenKind.BatchSeparator, _i, _i + 1);
            _i = lineEnd;
            return true;
        }

        if (_d is SqlDialect.MySql or SqlDialect.Ansi or SqlDialect.Sqlite && StartsWithWord(line, "DELIMITER"))
        {
            var rest = line["DELIMITER".Length..].Trim();
            if (rest.Length > 0)
            {
                _delimiter = rest.ToString() == ";" ? null : rest.ToString();
                _comments.Add(new Comment(_i, lineEnd, line.ToString(), true));
                _i = lineEnd;
                return true;
            }
        }

        if ((_d == SqlDialect.Postgres && line.Length > 1 && line[0] == '\\' && char.IsLetter(line[1]))
            || (_d == SqlDialect.PlSql && IsSqlPlusCommand(line)))
        {
            _comments.Add(new Comment(_i, lineEnd, line.ToString(), true));
            _i = lineEnd;
            return true;
        }

        return false;
    }

    private static bool IsGoLine(ReadOnlySpan<char> line)
    {
        if (line.Length < 2 || !line[..2].Equals("GO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = line[2..].Trim();
        if (rest.StartsWith("--"))
        {
            return true;
        }

        foreach (var ch in rest)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool StartsWithWord(ReadOnlySpan<char> line, string word) =>
        line.Length > word.Length && line.StartsWith(word, StringComparison.OrdinalIgnoreCase) && char.IsWhiteSpace(line[word.Length]);

    private static readonly string[] SqlPlusCommands =
        ["PROMPT", "REM", "REMARK", "SPOOL", "WHENEVER", "SHOW", "CONNECT", "CONN", "DEFINE", "UNDEFINE", "COLUMN", "TTITLE", "BTITLE", "BREAK", "COMPUTE", "PAUSE", "ACCEPT", "VARIABLE", "PRINT", "HOST", "EXIT", "QUIT", "DESC", "DESCRIBE"];

    private static readonly string[] SqlPlusSetOptions =
        ["SERVEROUTPUT", "DEFINE", "ECHO", "FEEDBACK", "VERIFY", "TERMOUT", "LINESIZE", "PAGESIZE", "HEADING", "TIMING", "SQLBLANKLINES", "TRIMSPOOL", "LONG", "SCAN", "SQLPROMPT", "TAB", "WRAP", "NEWPAGE", "AUTOCOMMIT", "AUTOTRACE", "ARRAYSIZE", "COLSEP", "NULL", "NUMWIDTH", "TRIMOUT", "CONCAT", "ESCAPE"];

    private static bool IsSqlPlusCommand(ReadOnlySpan<char> line)
    {
        if (line.Length > 0 && line[0] == '@')
        {
            return true;
        }

        var wordEnd = 0;
        while (wordEnd < line.Length && char.IsLetter(line[wordEnd]))
        {
            wordEnd++;
        }

        var word = line[..wordEnd].ToString().ToUpperInvariant();
        if (wordEnd < line.Length && !char.IsWhiteSpace(line[wordEnd]) && line[wordEnd] != ';')
        {
            return false;
        }

        if (word == "SET")
        {
            var rest = line[wordEnd..].TrimStart();
            var optEnd = 0;
            while (optEnd < rest.Length && char.IsLetter(rest[optEnd]))
            {
                optEnd++;
            }

            return SqlPlusSetOptions.Contains(rest[..optEnd].ToString().ToUpperInvariant());
        }

        return word.Length > 0 && SqlPlusCommands.Contains(word) && (word is not ("DESC" or "PRINT" or "SHOW" or "COLUMN" or "BREAK") || line.Length > word.Length);
    }

    private void Add(TokenKind kind, int start, int end, string? value = null)
    {
        var text = _s[start..end];
        _tokens.Add(new Token(kind, start, end, text, value ?? text));
    }

    private void LexOne(char c)
    {
        var start = _i;

        // Custom MySQL client delimiter.
        if (_delimiter is not null && string.CompareOrdinal(_s, _i, _delimiter, 0, _delimiter.Length) == 0 && _i + _delimiter.Length <= _end)
        {
            _i += _delimiter.Length;
            Add(TokenKind.Delimiter, start, _i);
            return;
        }

        // Comments.
        if (c == '-' && Peek(1) == '-')
        {
            LineComment();
            return;
        }

        if (c == '#' && _d.HashComments())
        {
            LineComment();
            return;
        }

        if (c == '/' && Peek(1) == '*')
        {
            BlockComment();
            return;
        }

        // Templates (dbt / Jinja).
        if (c == '{' && Peek(1) is '{' or '%' or '#')
        {
            Template();
            return;
        }

        // Strings with prefixes.
        if ((c is 'N' or 'n' or 'X' or 'x' or 'B' or 'b') && Peek(1) == '\'')
        {
            _i++;
            QuotedString(start, '\'', c is 'X' or 'x' or 'B' or 'b' ? false : _d.BackslashEscapes());
            return;
        }

        if ((c is 'E' or 'e') && Peek(1) == '\'' && _d is SqlDialect.Postgres or SqlDialect.Ansi)
        {
            _i++;
            QuotedString(start, '\'', backslash: true);
            return;
        }

        if ((c is 'U' or 'u') && Peek(1) == '&' && Peek(2) is '\'' or '"')
        {
            _i += 2;
            if (_s[_i] == '"')
            {
                QuotedIdentifier(start, '"', '"');
            }
            else
            {
                QuotedString(start, '\'', backslash: false);
            }

            return;
        }

        if ((c is 'Q' or 'q') && Peek(1) == '\'' && _d == SqlDialect.PlSql && Peek(2) != '\0')
        {
            OracleQString(start);
            return;
        }

        if ((c is 'N' or 'n') && (Peek(1) is 'Q' or 'q') && Peek(2) == '\'' && _d == SqlDialect.PlSql)
        {
            _i++;
            OracleQString(start);
            return;
        }

        if (c == '\'')
        {
            QuotedString(start, '\'', _d.BackslashEscapes());
            return;
        }

        if (c == '"')
        {
            if (_d.DoubleQuotedStrings())
            {
                QuotedString(start, '"', backslash: true);
            }
            else
            {
                QuotedIdentifier(start, '"', '"');
            }

            return;
        }

        if (c == '`')
        {
            QuotedIdentifier(start, '`', '`');
            return;
        }

        if (c == '[' && _d.BracketIdentifiers())
        {
            QuotedIdentifier(start, '[', ']');
            return;
        }

        if (c == '$' && _d.DollarQuotes() && TryDollarQuote())
        {
            return;
        }

        if (c == '$' && char.IsDigit(Peek(1)))
        {
            _i++;
            while (_i < _end && char.IsDigit(_s[_i]))
            {
                _i++;
            }

            Add(TokenKind.Variable, start, _i);
            return;
        }

        // Variables.
        if (c == '@' && _d != SqlDialect.Postgres)
        {
            _i++;
            if (Peek() == '@')
            {
                _i++;
            }

            if (Peek() is '\'' or '"' or '`')
            {
                // MySQL @'quoted var'
                var q = Peek();
                _i++;
                while (_i < _end && _s[_i] != q)
                {
                    _i++;
                }

                _i = Math.Min(_i + 1, _end);
            }

            while (_i < _end && IsIdentPart(_s[_i]))
            {
                _i++;
            }

            if (_i == start + 1)
            {
                Add(TokenKind.Op, start, _i);
                return;
            }

            Add(TokenKind.Variable, start, _i);
            return;
        }

        if (c == ':' && IsIdentStart(Peek(1)) && (_d is SqlDialect.PlSql or SqlDialect.Ansi or SqlDialect.Postgres or SqlDialect.Sqlite)
            && !(_tokens.Count > 0 && _tokens[^1].End == start && _tokens[^1].Kind is TokenKind.Op && _tokens[^1].Text == ":"))
        {
            // Bind variable (:new, :1 not handled). Not after an identifier/']' when it is a slice/label colon.
            var prev = _tokens.Count > 0 ? _tokens[^1] : null;
            if (prev is null || prev.End != start || prev.Kind is TokenKind.Op or TokenKind.Semicolon)
            {
                _i++;
                while (_i < _end && IsIdentPart(_s[_i]))
                {
                    _i++;
                }

                Add(TokenKind.Variable, start, _i);
                return;
            }
        }

        if (c == '?' && !(Peek(1) is '|' or '&' or '-' or '#'))
        {
            _i++;
            Add(TokenKind.Variable, start, _i);
            return;
        }

        if (c == '#' && _d == SqlDialect.TSql && (IsIdentStart(Peek(1)) || Peek(1) == '#'))
        {
            // Temporary table name.
            _i++;
            while (_i < _end && (IsIdentPart(_s[_i]) || _s[_i] == '#'))
            {
                _i++;
            }

            Add(TokenKind.Word, start, _i);
            return;
        }

        if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek(1)) && !PrevIsNameLike()))
        {
            Number();
            return;
        }

        if (IsIdentStart(c))
        {
            _i++;
            while (_i < _end && (IsIdentPart(_s[_i]) || (_s[_i] == '#' && _d is SqlDialect.PlSql or SqlDialect.TSql)))
            {
                if (_delimiter is not null && string.CompareOrdinal(_s, _i, _delimiter, 0, _delimiter.Length) == 0)
                {
                    break; // END$$ with DELIMITER $$
                }

                _i++;
            }

            Add(TokenKind.Word, start, _i);
            return;
        }

        if (c == ';')
        {
            _i++;
            Add(_delimiter is null ? TokenKind.Semicolon : TokenKind.Op, start, _i);
            return;
        }

        Operator();
    }

    private bool PrevIsNameLike() =>
        _tokens.Count > 0 && _tokens[^1].End == _i && _tokens[^1].Kind is TokenKind.Word or TokenKind.QuotedIdent;

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c > 0x7f && !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c);

    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$' || c > 0x7f && !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c);

    private static readonly string[] MultiCharOps =
        ["->>", "#>>", "<=>", "::", ":=", "=>", "<>", "!=", "<=", ">=", "||", "->", "#>", "@>", "<@", "&&", "**", "..", "!<", "!>", "+=", "-=", "*=", "/=", "%=", "<<", ">>", "~~", "!~", "@@", "%TYPE", "%ROWTYPE"];

    private void Operator()
    {
        var start = _i;
        foreach (var op in MultiCharOps)
        {
            if (string.Compare(_s, _i, op, 0, op.Length, StringComparison.OrdinalIgnoreCase) == 0 && _i + op.Length <= _end)
            {
                if (op[0] == '%' && _i + op.Length < _end && IsIdentPart(_s[_i + op.Length]))
                {
                    continue;
                }

                _i += op.Length;
                Add(TokenKind.Op, start, _i);
                return;
            }
        }

        _i++;
        if (char.IsHighSurrogate(_s[start]) && _i < _end)
        {
            _i++;
        }

        Add(TokenKind.Op, start, _i);
    }

    private void Number()
    {
        var start = _i;
        if (Peek() == '0' && Peek(1) is 'x' or 'X')
        {
            _i += 2;
            while (_i < _end && char.IsAsciiHexDigit(_s[_i]))
            {
                _i++;
            }

            Add(TokenKind.Number, start, _i);
            return;
        }

        while (_i < _end && (char.IsDigit(_s[_i]) || _s[_i] == '_'))
        {
            _i++;
        }

        if (Peek() == '.' && Peek(1) != '.')
        {
            _i++;
            while (_i < _end && char.IsDigit(_s[_i]))
            {
                _i++;
            }
        }

        if (Peek() is 'e' or 'E' && (char.IsDigit(Peek(1)) || (Peek(1) is '+' or '-' && char.IsDigit(Peek(2)))))
        {
            _i += 2;
            while (_i < _end && char.IsDigit(_s[_i]))
            {
                _i++;
            }
        }

        Add(TokenKind.Number, start, _i);
    }

    private void LineComment()
    {
        var start = _i;
        var e = _s.IndexOf('\n', _i, _end - _i);
        _i = e < 0 ? _end : e;
        var text = _s[start.._i].TrimEnd('\r');
        _comments.Add(new Comment(start, start + text.Length, text, true));
    }

    private void BlockComment()
    {
        var start = _i;
        _i += 2;
        var depth = 1;
        var nested = _d.NestedBlockComments();
        while (_i < _end && depth > 0)
        {
            if (_s[_i] == '*' && Peek(1) == '/')
            {
                depth--;
                _i += 2;
            }
            else if (nested && _s[_i] == '/' && Peek(1) == '*')
            {
                depth++;
                _i += 2;
            }
            else
            {
                _i++;
            }
        }

        _comments.Add(new Comment(start, _i, _s[start.._i], false));
    }

    private void Template()
    {
        var start = _i;
        var close = Peek(1) switch { '{' => "}}", '%' => "%}", _ => "#}" };
        var e = _s.IndexOf(close, _i + 2, _end - _i - 2, StringComparison.Ordinal);
        _i = e < 0 ? _end : e + 2;
        var text = _s[start.._i];
        if (close is "#}" or "%}")
        {
            // Jinja comments and control blocks ({% if %} ... {% endif %}): both branches are indexed as plain SQL.
            _comments.Add(new Comment(start, _i, text, false));
            return;
        }

        Add(TokenKind.Template, start, _i, TemplateTarget(text));
    }

    /// <summary>Extracts the model from <c>{{ ref('m') }}</c> / <c>{{ ref('pkg', 'm') }}</c> or <c>source.table</c> from <c>{{ source('s', 't') }}</c>.</summary>
    internal static string TemplateTarget(string text)
    {
        var inner = text.AsSpan(2, Math.Max(0, text.Length - 4)).Trim();
        var isRef = inner.StartsWith("ref(", StringComparison.Ordinal) || inner.StartsWith("ref (", StringComparison.Ordinal);
        var isSource = inner.StartsWith("source(", StringComparison.Ordinal) || inner.StartsWith("source (", StringComparison.Ordinal);
        if (!isRef && !isSource)
        {
            return "";
        }

        var args = new List<string>();
        var i = 0;
        while (i < inner.Length)
        {
            if (inner[i] is '\'' or '"')
            {
                var q = inner[i];
                var j = inner[(i + 1)..].IndexOf(q);
                if (j < 0)
                {
                    break;
                }

                args.Add(inner.Slice(i + 1, j).ToString());
                i += j + 2;
            }
            else
            {
                i++;
            }
        }

        if (args.Count == 0)
        {
            return "";
        }

        return isRef ? "ref:" + args[^1] : "source:" + string.Join('.', args.TakeLast(2));
    }

    private void QuotedString(int start, char quote, bool backslash)
    {
        _i++; // opening quote
        var b = new StringBuilder();
        while (_i < _end)
        {
            var ch = _s[_i];
            if (backslash && ch == '\\' && _i + 1 < _end)
            {
                b.Append(_s[_i + 1]);
                _i += 2;
                continue;
            }

            if (ch == quote)
            {
                if (Peek(1) == quote)
                {
                    b.Append(quote);
                    _i += 2;
                    continue;
                }

                _i++;
                Add(TokenKind.String, start, _i, b.ToString());
                return;
            }

            b.Append(ch);
            _i++;
        }

        Add(TokenKind.String, start, _i, b.ToString());
    }

    private void OracleQString(int start)
    {
        _i += 2; // q'
        var open = _s[_i];
        var close = open switch { '[' => ']', '{' => '}', '(' => ')', '<' => '>', _ => open };
        _i++;
        var contentStart = _i;
        while (_i < _end && !(_s[_i] == close && Peek(1) == '\''))
        {
            _i++;
        }

        var content = _s[contentStart..Math.Min(_i, _end)];
        _i = Math.Min(_i + 2, _end);
        Add(TokenKind.String, start, _i, content);
    }

    private void QuotedIdentifier(int start, char open, char close)
    {
        _i++;
        var b = new StringBuilder();
        while (_i < _end)
        {
            var ch = _s[_i];
            if (ch == close)
            {
                if (Peek(1) == close)
                {
                    b.Append(close);
                    _i += 2;
                    continue;
                }

                _i++;
                Add(TokenKind.QuotedIdent, start, _i, b.ToString());
                return;
            }

            if (ch == '\n' && open == '[')
            {
                break; // unterminated bracket: treat as an operator to stay robust
            }

            b.Append(ch);
            _i++;
        }

        if (open == '[')
        {
            _i = start + 1;
            Add(TokenKind.Op, start, _i);
            return;
        }

        Add(TokenKind.QuotedIdent, start, _i, b.ToString());
    }

    private bool TryDollarQuote()
    {
        var start = _i;
        var j = _i + 1;
        while (j < _end && (char.IsLetterOrDigit(_s[j]) || _s[j] == '_'))
        {
            j++;
        }

        if (j >= _end || _s[j] != '$' || (j > _i + 1 && char.IsDigit(_s[_i + 1])))
        {
            return false;
        }

        // Do not treat identifiers such as a$b$ as dollar quotes.
        if (_tokens.Count > 0 && _tokens[^1].End == start && _tokens[^1].Kind == TokenKind.Word)
        {
            return false;
        }

        var tag = _s[_i..(j + 1)];
        var contentStart = j + 1;
        var e = _s.IndexOf(tag, contentStart, _end - contentStart, StringComparison.Ordinal);
        var contentEnd = e < 0 ? _end : e;
        _i = e < 0 ? _end : e + tag.Length;
        Add(TokenKind.String, start, _i, _s[contentStart..contentEnd]);
        return true;
    }

    /// <summary>Offset of the content of a dollar-quoted string token (after the opening tag), or -1.</summary>
    public static int DollarContentStart(Token t)
    {
        if (t.Kind != TokenKind.String || t.Text.Length < 2 || t.Text[0] != '$')
        {
            return -1;
        }

        var close = t.Text.IndexOf('$', 1);
        return close < 0 ? -1 : t.Start + close + 1;
    }

    /// <summary>Offset of the content of a plain <c>'..'</c> string token, or -1.</summary>
    public static int QuotedContentStart(Token t) =>
        t.Kind == TokenKind.String && t.Text.Length >= 2 && t.Text[0] == '\'' ? t.Start + 1 : -1;
}
