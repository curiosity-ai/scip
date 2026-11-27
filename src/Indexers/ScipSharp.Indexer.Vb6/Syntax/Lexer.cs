namespace ScipSharp.Indexer.Vb6.Syntax;

/// <summary>Result of lexing: the token stream plus the text of comment-only lines (used for documentation).</summary>
internal sealed record LexResult(List<Token> Tokens, Dictionary<int, string> CommentLines, Dictionary<int, string> TrailingComments);

/// <summary>
/// The VB6/VBA lexer. Handles line continuations (<c> _</c>), <c>'</c> and <c>Rem</c> comments (which continue onto the next
/// line when they end in <c> _</c>), bracketed identifiers, type-suffix characters (<c>$ % &amp; ! # @</c>), string literals
/// with doubled quotes, numbers (<c>&amp;H</c>/<c>&amp;O</c>, exponents, suffixes) and <c>#date#</c> literals. A
/// <see cref="TokenKind.NewLine"/> token ends every logical line; colons are emitted as punctuation.
/// </summary>
internal static class Lexer
{
    public static LexResult Lex(IReadOnlyList<string> lines, int startLine = 0, bool[]? active = null)
    {
        var tokens = new List<Token>();
        var comments = new Dictionary<int, string>();
        var trailing = new Dictionary<int, string>();
        var commentContinues = false;
        for (var lineNo = startLine; lineNo < lines.Count; lineNo++)
        {
            var line = lines[lineNo];
            if (active is not null && !active[lineNo])
            {
                EndLine(tokens, lineNo, line.Length);
                commentContinues = false;
                continue;
            }

            if (commentContinues)
            {
                commentContinues = EndsWithContinuation(line);
                comments[lineNo] = line.Trim();
                if (!commentContinues)
                {
                    EndLine(tokens, lineNo, line.Length);
                }

                continue;
            }

            var lineHadToken = false;
            var continued = false;
            var i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                if (c is ' ' or '\t' or '\f' or ' ' or '\u001A')
                {
                    i++;
                    continue;
                }

                // Line continuation: '_' preceded by whitespace (or at line start) and followed by nothing but whitespace.
                if (c == '_' && (i == 0 || char.IsWhiteSpace(line[i - 1])) && line.AsSpan(i + 1).IsWhiteSpace())
                {
                    continued = true;
                    break;
                }

                if (c == '\'' || (IsRem(line, i) && AtStatementStart(tokens)))
                {
                    var text = line[(c == '\'' ? i + 1 : i + 3)..];
                    if (!lineHadToken && !EndsWithContinuationAfterTokens(tokens, lineNo))
                    {
                        comments[lineNo] = text;
                    }
                    else if (lineHadToken)
                    {
                        trailing[lineNo] = text;
                    }

                    commentContinues = EndsWithContinuation(line);
                    break;
                }

                lineHadToken = true;
                if (char.IsLetter(c) || c == '[')
                {
                    i = LexIdentifier(line, lineNo, i, tokens);
                    continue;
                }

                if (c == '"')
                {
                    var start = i++;
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                i += 2;
                                continue;
                            }

                            i++;
                            break;
                        }

                        i++;
                    }

                    var raw = line[start..i];
                    var value = raw.Length >= 2 && raw.EndsWith('"') ? raw[1..^1] : raw[1..];
                    tokens.Add(new Token(TokenKind.String, value.Replace("\"\"", "\"", StringComparison.Ordinal), lineNo, start, i));
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1]) && !PreviousIsOperand(tokens)))
                {
                    i = LexNumber(line, lineNo, i, tokens);
                    continue;
                }

                if (c == '&' && i + 1 < line.Length && char.ToUpperInvariant(line[i + 1]) is 'H' or 'O'
                    && i + 2 < line.Length && Uri.IsHexDigit(line[i + 2]))
                {
                    var start = i;
                    i += 2;
                    while (i < line.Length && Uri.IsHexDigit(line[i]))
                    {
                        i++;
                    }

                    if (i < line.Length && line[i] is '&' or '%' or '^')
                    {
                        i++;
                    }

                    tokens.Add(new Token(TokenKind.Number, line[start..i], lineNo, start, i));
                    continue;
                }

                if (c == '#' && TryDateLiteral(line, i, out var end))
                {
                    tokens.Add(new Token(TokenKind.Date, line[i..end], lineNo, i, end));
                    i = end;
                    continue;
                }

                var op = Operator(line, i);
                tokens.Add(new Token(TokenKind.Punct, op, lineNo, i, i + op.Length));
                i += op.Length;
            }

            if (!continued && !commentContinues)
            {
                EndLine(tokens, lineNo, line.Length);
            }
            else if (continued && lineNo == lines.Count - 1)
            {
                EndLine(tokens, lineNo, line.Length);
            }
        }

        if (tokens.Count > 0 && tokens[^1].Kind != TokenKind.NewLine)
        {
            var last = lines.Count - 1;
            EndLine(tokens, last, last >= 0 ? lines[last].Length : 0);
        }

        return new LexResult(tokens, comments, trailing);
    }

    private static bool EndsWithContinuation(string line)
    {
        var t = line.TrimEnd();
        return t.EndsWith('_') && (t.Length == 1 || char.IsWhiteSpace(t[^2]));
    }

    // A comment on a continuation line (rare, and invalid VB) is not documentation for what follows.
    private static bool EndsWithContinuationAfterTokens(List<Token> tokens, int lineNo) =>
        tokens.Count > 0 && tokens[^1].Kind != TokenKind.NewLine && tokens[^1].Line < lineNo;

    private static void EndLine(List<Token> tokens, int line, int col)
    {
        if (tokens.Count == 0 || tokens[^1].Kind != TokenKind.NewLine)
        {
            tokens.Add(new Token(TokenKind.NewLine, "\n", line, col, col));
        }
    }

    private static bool IsRem(string line, int i) =>
        i + 3 <= line.Length
        && string.Compare(line, i, "rem", 0, 3, StringComparison.OrdinalIgnoreCase) == 0
        && (i + 3 == line.Length || !IsIdentifierPart(line[i + 3]));

    private static bool AtStatementStart(List<Token> tokens)
    {
        if (tokens.Count == 0)
        {
            return true;
        }

        // "Rem" starts a comment only where a statement may start: after a line end or a colon.
        var last = tokens[^1];
        return last.Kind == TokenKind.NewLine || last.IsPunct(":");
    }

    private static bool PreviousIsOperand(List<Token> tokens) =>
        tokens.Count > 0 && tokens[^1] is var t
        && (t.Kind is TokenKind.Identifier or TokenKind.Number or TokenKind.String || t.IsPunct(")"));

    public static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int LexIdentifier(string line, int lineNo, int i, List<Token> tokens)
    {
        if (line[i] == '[')
        {
            var close = line.IndexOf(']', i + 1);
            if (close < 0)
            {
                tokens.Add(new Token(TokenKind.Punct, "[", lineNo, i, i + 1));
                return i + 1;
            }

            tokens.Add(new Token(TokenKind.Identifier, line[(i + 1)..close], lineNo, i + 1, close, Bracketed: true));
            return close + 1;
        }

        var start = i;
        while (i < line.Length && IsIdentifierPart(line[i]))
        {
            i++;
        }

        var end = i;
        var suffix = '\0';
        if (i < line.Length)
        {
            var next = i + 1 < line.Length ? line[i + 1] : '\0';
            switch (line[i])
            {
                case '$' or '%' or '@':
                    suffix = line[i];
                    break;
                case '!' when !(char.IsLetter(next) || next == '[' || next == '_'):
                    suffix = '!';
                    break;
                case '#' when !char.IsLetterOrDigit(next):
                    suffix = '#';
                    break;
                case '&' when next is '\0' or ' ' or '\t' or ')' or ',' or '(' or '=' or ':':
                    suffix = '&';
                    break;
            }

            if (suffix != '\0')
            {
                i++;
            }
        }

        tokens.Add(new Token(TokenKind.Identifier, line[start..end], lineNo, start, end, suffix));
        return i;
    }

    private static int LexNumber(string line, int lineNo, int i, List<Token> tokens)
    {
        var start = i;
        while (i < line.Length && char.IsDigit(line[i]))
        {
            i++;
        }

        if (i < line.Length && line[i] == '.' && (i + 1 >= line.Length || !char.IsLetter(line[i + 1]) || IsExponent(line, i + 1)))
        {
            i++;
            while (i < line.Length && char.IsDigit(line[i]))
            {
                i++;
            }
        }

        if (IsExponent(line, i))
        {
            i++;
            if (i < line.Length && line[i] is '+' or '-')
            {
                i++;
            }

            while (i < line.Length && char.IsDigit(line[i]))
            {
                i++;
            }
        }

        if (i < line.Length && line[i] is '%' or '&' or '!' or '#' or '@' or '^'
            && (i + 1 >= line.Length || !IsIdentifierPart(line[i + 1])))
        {
            i++;
        }

        tokens.Add(new Token(TokenKind.Number, line[start..i], lineNo, start, i));
        return i;
    }

    private static bool IsExponent(string line, int i) =>
        i + 1 < line.Length && line[i] is 'e' or 'E' or 'd' or 'D'
        && (char.IsDigit(line[i + 1]) || (line[i + 1] is '+' or '-' && i + 2 < line.Length && char.IsDigit(line[i + 2])));

    /// <summary>
    /// <c>#1/2/2000#</c>, <c>#12:00:00 AM#</c>. A '#' is also the file-number prefix (<c>Print #1, x</c>), so the content
    /// must look like a date: digits, letters, spaces and <c>/ - : .</c>, with at least one digit and one separator.
    /// </summary>
    private static bool TryDateLiteral(string line, int i, out int end)
    {
        end = i;
        var close = line.IndexOf('#', i + 1);
        if (close < 0)
        {
            return false;
        }

        var content = line.AsSpan(i + 1, close - i - 1);
        var digit = false;
        var separator = false;
        foreach (var ch in content)
        {
            if (char.IsDigit(ch))
            {
                digit = true;
            }
            else if (ch is '/' or '-' or ':')
            {
                separator = true;
            }
            else if (!(char.IsLetter(ch) || ch is ' ' or '.'))
            {
                return false;
            }
        }

        if (!digit || !separator)
        {
            return false;
        }

        end = close + 1;
        return true;
    }

    private static string Operator(string line, int i)
    {
        if (i + 1 < line.Length)
        {
            var two = line.Substring(i, 2);
            if (two is ":=" or "<>" or "<=" or ">=" or "=<" or "=>")
            {
                return two;
            }
        }

        return line[i].ToString();
    }
}
