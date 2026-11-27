namespace ScipSharp.Indexer.Pli.Syntax;

internal enum TokenKind
{
    Identifier,
    Number,
    String,
    Punct,
}

/// <summary>A PL/I token. Positions are 0-based lines and UTF-16 columns of the original file.</summary>
/// <param name="Kind">Token class.</param>
/// <param name="Text">Source text of the token (for strings including quotes and suffix).</param>
/// <param name="File">Id of the file the token was read from (see <see cref="Semantics.FileTable"/>).</param>
/// <param name="Line">Start line.</param>
/// <param name="Column">Start column.</param>
/// <param name="EndLine">End line (strings may continue across lines).</param>
/// <param name="EndColumn">End column (exclusive).</param>
internal sealed record Token(TokenKind Kind, string Text, int File, int Line, int Column, int EndLine, int EndColumn)
{
    public bool IsIdent => Kind == TokenKind.Identifier;

    public bool Is(string punct) => Kind == TokenKind.Punct && Text == punct;

    /// <summary>True for an identifier equal (case-insensitively) to <paramref name="word"/>.</summary>
    public bool IsWord(string word) => Kind == TokenKind.Identifier && Text.Equals(word, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => $"{Text}@{Line}:{Column}";
}

/// <summary>
/// Tokenizer for (masked) PL/I source. Keywords are not reserved in PL/I, so every word is an <see cref="TokenKind.Identifier"/>;
/// the parser decides from context. Comments are dropped. Identifiers may contain the national characters <c>#@$</c>.
/// </summary>
internal static class Lexer
{
    private static readonly string[] MultiCharOperators =
    [
        "||=", "**=", "%;", "->", "=>", "**", "||", "!!", "<=", ">=", "<>", "¬=", "^=", "~=", "¬<", "^<", "¬>", "^>", "+=", "-=", "*=", "/=", "|=", "&=",
    ];

    public static List<Token> Tokenize(string masked, int file)
    {
        var tokens = new List<Token>();
        var i = 0;
        var line = 0;
        var lineStart = 0;
        var n = masked.Length;
        while (i < n)
        {
            var c = masked[i];
            if (c == '\n')
            {
                i++;
                line++;
                lineStart = i;
                continue;
            }

            if (char.IsWhiteSpace(c) || c == '\0')
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < n && masked[i + 1] == '*')
            {
                i += 2;
                while (i < n && !(masked[i] == '*' && i + 1 < n && masked[i + 1] == '/'))
                {
                    if (masked[i] == '\n')
                    {
                        line++;
                        lineStart = i + 1;
                    }

                    i++;
                }

                i = Math.Min(n, i + 2);
                continue;
            }

            var start = i;
            var startLine = line;
            var startCol = i - lineStart;
            if (IsIdentStart(c))
            {
                while (i < n && IsIdentPart(masked[i]))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Identifier, masked[start..i], file, line, startCol, line, i - lineStart));
                continue;
            }

            if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < n && char.IsAsciiDigit(masked[i + 1])))
            {
                i = ScanNumber(masked, i);
                tokens.Add(new Token(TokenKind.Number, masked[start..i], file, line, startCol, line, i - lineStart));
                continue;
            }

            if (c is '\'' or '"')
            {
                i++;
                while (i < n)
                {
                    if (masked[i] == c)
                    {
                        if (i + 1 < n && masked[i + 1] == c)
                        {
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    if (masked[i] == '\n')
                    {
                        line++;
                        lineStart = i + 1;
                    }

                    i++;
                }

                // String suffixes: 'FF'X, '1'B, 'abc'G, 'xx'BX, '..'B4 ...
                while (i < n && char.IsAsciiLetterOrDigit(masked[i]))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.String, masked[start..i], file, startLine, startCol, line, i - lineStart));
                continue;
            }

            var op = MatchOperator(masked, i);
            i += op.Length;
            tokens.Add(new Token(TokenKind.Punct, op, file, line, startCol, line, i - lineStart));
        }

        return tokens;
    }

    private static int ScanNumber(string s, int i)
    {
        var n = s.Length;
        while (i < n && (char.IsAsciiDigit(s[i]) || s[i] == '_'))
        {
            i++;
        }

        if (i < n && s[i] == '.' && !(i + 1 < n && char.IsAsciiLetter(s[i + 1]) && !IsExponent(s, i + 1)))
        {
            i++;
            while (i < n && char.IsAsciiDigit(s[i]))
            {
                i++;
            }
        }

        if (i < n && IsExponent(s, i))
        {
            i++;
            if (i < n && s[i] is '+' or '-')
            {
                i++;
            }

            while (i < n && char.IsAsciiDigit(s[i]))
            {
                i++;
            }
        }

        // Suffixes: 1011B, 3I, 12XN, 1.5Q ...
        while (i < n && char.IsAsciiLetterOrDigit(s[i]))
        {
            i++;
        }

        return i;
    }

    private static bool IsExponent(string s, int i) =>
        i < s.Length && s[i] is 'e' or 'E' or 'd' or 'D' or 'q' or 'Q'
        && i + 1 < s.Length && (char.IsAsciiDigit(s[i + 1]) || (s[i + 1] is '+' or '-' && i + 2 < s.Length && char.IsAsciiDigit(s[i + 2])));

    private static string MatchOperator(string s, int i)
    {
        foreach (var op in MultiCharOperators)
        {
            if (string.CompareOrdinal(s, i, op, 0, op.Length) == 0)
            {
                return op;
            }
        }

        return s[i].ToString();
    }

    public static bool IsIdentStart(char c) => char.IsLetter(c) || c is '_' or '#' or '@' or '$';

    public static bool IsIdentPart(char c) => IsIdentStart(c) || char.IsDigit(c);
}
