namespace ScipSharp.Indexer.Fortran.Syntax;

/// <summary>
/// Tokenizes one logical line (the code parts of an initial line and its continuation lines, already concatenated).
/// Every character carries its original (line, column) so tokens point back into the document.
/// </summary>
internal static class Lexer
{
    private static readonly string[] TwoCharOps = ["**", "//", "==", "/=", "<=", ">=", "=>", "::"];

    public static List<Token> Tokenize(string text, int[] lines, int[] cols, SourceDoc file)
    {
        var tokens = new List<Token>();
        var i = 0;
        var n = text.Length;
        while (i < n)
        {
            var c = text[i];
            if (c is ' ' or '\t' or '\r' or '\f')
            {
                i++;
                continue;
            }

            var start = i;
            if (IsLetter(c))
            {
                i++;
                while (i < n && IsNameChar(text[i]))
                {
                    i++;
                }

                // BOZ literal: B'0101', O"17", Z'FF'
                if (i - start == 1 && i < n && text[i] is '\'' or '"' && char.ToLowerInvariant(c) is 'b' or 'o' or 'z' or 'x')
                {
                    i = SkipString(text, i);
                    tokens.Add(Make(TokenKind.Boz, text, start, i, lines, cols, file));
                    continue;
                }

                // A kind prefix on a character literal (ck_"abc"): keep the name, the string follows.
                tokens.Add(Make(TokenKind.Name, text, start, i, lines, cols, file));
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(text[i + 1])))
            {
                i = LexNumber(text, i, tokens, lines, cols, file);
                continue;
            }

            if (c is '\'' or '"')
            {
                i = SkipString(text, i);
                tokens.Add(Make(TokenKind.String, text, start, i, lines, cols, file));
                // trailing BOZ suffix ('FF'X)
                if (i < n && text[i] is 'x' or 'X' or 'o' or 'O' or 'b' or 'B' && (i + 1 >= n || !IsNameChar(text[i + 1])))
                {
                    i++;
                }

                continue;
            }

            if (c == '.')
            {
                var j = i + 1;
                while (j < n && char.IsAsciiLetter(text[j]))
                {
                    j++;
                }

                if (j > i + 1 && j < n && text[j] == '.')
                {
                    var word = text.AsSpan(i + 1, j - i - 1);
                    var isLogical = word.Equals("true", StringComparison.OrdinalIgnoreCase) || word.Equals("false", StringComparison.OrdinalIgnoreCase);
                    tokens.Add(Make(isLogical ? TokenKind.Logical : TokenKind.DotOp, text, i, j + 1, lines, cols, file));
                    i = j + 1;
                    if (isLogical)
                    {
                        i = LexKindSuffix(text, i, tokens, lines, cols, file);
                    }

                    continue;
                }

                tokens.Add(Make(TokenKind.Op, text, i, i + 1, lines, cols, file));
                i++;
                continue;
            }

            if (i + 1 < n)
            {
                var two = text.AsSpan(i, 2);
                var matched = false;
                foreach (var op in TwoCharOps)
                {
                    if (two.SequenceEqual(op))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    tokens.Add(Make(TokenKind.Op, text, i, i + 2, lines, cols, file));
                    i += 2;
                    continue;
                }
            }

            tokens.Add(Make(TokenKind.Op, text, i, i + 1, lines, cols, file));
            i++;
        }

        return tokens;
    }

    public static bool IsLetter(char c) => char.IsAsciiLetter(c) || (c > 127 && char.IsLetter(c));

    public static bool IsNameChar(char c) => IsLetter(c) || char.IsDigit(c) || c is '_' or '$';

    private static int SkipString(string text, int i)
    {
        var quote = text[i];
        i++;
        while (i < text.Length)
        {
            if (text[i] == quote)
            {
                if (i + 1 < text.Length && text[i + 1] == quote)
                {
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        return i;
    }

    private static int LexNumber(string text, int i, List<Token> tokens, int[] lines, int[] cols, SourceDoc file)
    {
        var n = text.Length;
        var start = i;
        var isReal = false;
        while (i < n && char.IsDigit(text[i]))
        {
            i++;
        }

        // Hollerith constant nHxxxx (FORTRAN 66/77): only after ( , / * or at the start of an item list.
        if (i < n && text[i] is 'h' or 'H' && i > start && IsHollerithContext(tokens))
        {
            if (int.TryParse(text.AsSpan(start, i - start), out var count) && count > 0)
            {
                var end = Math.Min(n, i + 1 + count);
                tokens.Add(Make(TokenKind.String, text, start, end, lines, cols, file));
                return end;
            }
        }

        if (i < n && text[i] == '.' && !IsDotOperatorAt(text, i))
        {
            isReal = true;
            i++;
            while (i < n && char.IsDigit(text[i]))
            {
                i++;
            }
        }

        if (i < n && text[i] is 'e' or 'E' or 'd' or 'D' or 'q' or 'Q')
        {
            var j = i + 1;
            if (j < n && text[j] is '+' or '-')
            {
                j++;
            }

            if (j < n && char.IsDigit(text[j]))
            {
                isReal = true;
                i = j;
                while (i < n && char.IsDigit(text[i]))
                {
                    i++;
                }
            }
        }

        tokens.Add(Make(isReal ? TokenKind.Real : TokenKind.Int, text, start, i, lines, cols, file));
        return LexKindSuffix(text, i, tokens, lines, cols, file);
    }

    private static int LexKindSuffix(string text, int i, List<Token> tokens, int[] lines, int[] cols, SourceDoc file)
    {
        var n = text.Length;
        if (i + 1 < n && text[i] == '_' && (IsLetter(text[i + 1]) || char.IsDigit(text[i + 1])))
        {
            var j = i + 1;
            if (char.IsDigit(text[j]))
            {
                while (j < n && char.IsDigit(text[j]))
                {
                    j++;
                }

                return j;
            }

            while (j < n && IsNameChar(text[j]))
            {
                j++;
            }

            var t = Make(TokenKind.Name, text, i + 1, j, lines, cols, file);
            tokens.Add(new Token(t.Kind, t.Text, t.File, t.Line, t.Col, t.EndCol) { IsKindParam = true });
            return j;
        }

        return i;
    }

    /// <summary>True when the '.' at <paramref name="i"/> starts an operator such as <c>.eq.</c> (as in <c>1.eq.2</c>).</summary>
    private static bool IsDotOperatorAt(string text, int i)
    {
        var j = i + 1;
        while (j < text.Length && char.IsAsciiLetter(text[j]))
        {
            j++;
        }

        if (j == i + 1 || j >= text.Length || text[j] != '.')
        {
            return false;
        }

        // "1.e5" / "1.d0" are reals: a single exponent letter followed by a digit never reaches here (no closing dot),
        // but "1.e.2" is ambiguous; treat runs of 2+ letters as operators.
        return j - i - 1 >= 2;
    }

    private static bool IsHollerithContext(List<Token> tokens)
    {
        if (tokens.Count == 0)
        {
            return false;
        }

        var prev = tokens[^1];
        if (prev.Kind != TokenKind.Op)
        {
            return false;
        }

        // "2*4HABCD" (DATA repeat) is Hollerith, "REAL*8H" (blank-insensitive REAL*8 H) is not.
        return prev.Text is "(" or "," or "/" || (prev.Text == "*" && tokens.Count >= 2 && tokens[^2].Kind == TokenKind.Int);
    }

    private static Token Make(TokenKind kind, string text, int start, int end, int[] lines, int[] cols, SourceDoc file)
    {
        var line = lines[start];
        var col = cols[start];
        var last = end - 1;
        var endCol = lines[last] == line ? cols[last] + 1 : file.Lines[line].Length;
        if (endCol <= col)
        {
            endCol = col + 1;
        }

        return new Token(kind, text[start..end], file, line, col, endCol);
    }
}
