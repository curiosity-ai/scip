namespace ScipSharp.Indexer.Rust;

public enum RustTokenKind : byte
{
    Ident,
    Lifetime,
    Number,
    Str,
    Chr,
    Punct,
    /// <summary>Outer doc comment (<c>///</c>, <c>/** */</c>).</summary>
    DocOuter,
    /// <summary>Inner doc comment (<c>//!</c>, <c>/*! */</c>).</summary>
    DocInner,
}

/// <summary>A token; offsets are UTF-8 byte offsets into the source.</summary>
public readonly record struct RustToken(RustTokenKind Kind, int Start, int End);

/// <summary>
/// Minimal Rust lexer working on UTF-8 bytes (SCIP positions are UTF-8 code unit offsets). Whitespace and plain
/// comments are dropped; doc comments are kept because they are part of an item's enclosing range and documentation.
/// </summary>
public static class RustLexer
{
    public static List<RustToken> Lex(ReadOnlySpan<byte> s)
    {
        List<RustToken> tokens = [];
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or 0x0B or 0x0C)
            {
                i++;
            }
            else if (c == '/' && At(s, i + 1) == '/')
            {
                var end = i;
                while (end < s.Length && s[end] != '\n')
                {
                    end++;
                }

                var third = At(s, i + 2);
                if (third == '/' && At(s, i + 3) != '/')
                {
                    tokens.Add(new(RustTokenKind.DocOuter, i, end));
                }
                else if (third == '!')
                {
                    tokens.Add(new(RustTokenKind.DocInner, i, end));
                }

                i = end;
            }
            else if (c == '/' && At(s, i + 1) == '*')
            {
                var start = i;
                var depth = 0;
                while (i < s.Length)
                {
                    if (s[i] == '/' && At(s, i + 1) == '*')
                    {
                        depth++;
                        i += 2;
                    }
                    else if (s[i] == '*' && At(s, i + 1) == '/')
                    {
                        depth--;
                        i += 2;
                        if (depth == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        i++;
                    }
                }

                var third = At(s, start + 2);
                var fourth = At(s, start + 3);
                if (third == '*' && fourth != '*' && fourth != '/')
                {
                    tokens.Add(new(RustTokenKind.DocOuter, start, i));
                }
                else if (third == '!')
                {
                    tokens.Add(new(RustTokenKind.DocInner, start, i));
                }
            }
            else if (c == '"')
            {
                var start = i;
                i = SkipString(s, i + 1);
                tokens.Add(new(RustTokenKind.Str, start, i));
            }
            else if (IsRawStringStart(s, i, out var prefixLen, out var hashes))
            {
                var start = i;
                i = SkipRawString(s, i + prefixLen + hashes + 1, hashes);
                tokens.Add(new(RustTokenKind.Str, start, i));
            }
            else if (c == 'b' && At(s, i + 1) == '"')
            {
                var start = i;
                i = SkipString(s, i + 2);
                tokens.Add(new(RustTokenKind.Str, start, i));
            }
            else if (c == 'b' && At(s, i + 1) == '\'' && TryChar(s, i + 1, out var bend))
            {
                tokens.Add(new(RustTokenKind.Chr, i, bend));
                i = bend;
            }
            else if (c == '\'')
            {
                if (TryChar(s, i, out var end))
                {
                    tokens.Add(new(RustTokenKind.Chr, i, end));
                    i = end;
                }
                else
                {
                    var start = i++;
                    while (i < s.Length && IsIdentContinue(s[i]))
                    {
                        i++;
                    }

                    tokens.Add(new(RustTokenKind.Lifetime, start, i));
                }
            }
            else if (c == 'r' && At(s, i + 1) == '#' && IsIdentStart(At(s, i + 2)))
            {
                var start = i;
                i += 2;
                while (i < s.Length && IsIdentContinue(s[i]))
                {
                    i++;
                }

                tokens.Add(new(RustTokenKind.Ident, start, i));
            }
            else if (IsIdentStart(c))
            {
                var start = i;
                while (i < s.Length && IsIdentContinue(s[i]))
                {
                    i++;
                }

                tokens.Add(new(RustTokenKind.Ident, start, i));
            }
            else if (c is >= (byte)'0' and <= (byte)'9')
            {
                var start = i;
                while (i < s.Length && (IsIdentContinue(s[i]) || (s[i] == '.' && At(s, i + 1) is >= (byte)'0' and <= (byte)'9')))
                {
                    i++;
                }

                tokens.Add(new(RustTokenKind.Number, start, i));
            }
            else
            {
                // `::`, `->` and `=>` are kept together; everything else is one token per byte (angle-bracket
                // counting relies on `>` never being merged).
                var len = (c == ':' && At(s, i + 1) == ':') || (c == '-' && At(s, i + 1) == '>')
                    || (c == '=' && At(s, i + 1) == '>') ? 2 : 1;
                tokens.Add(new(RustTokenKind.Punct, i, i + len));
                i += len;
            }
        }

        return tokens;
    }

    private static byte At(ReadOnlySpan<byte> s, int i) => i < s.Length ? s[i] : (byte)0;

    public static bool IsIdentStart(byte c) => c is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (byte)'_' or >= 0x80;

    public static bool IsIdentContinue(byte c) => IsIdentStart(c) || c is >= (byte)'0' and <= (byte)'9';

    private static int SkipString(ReadOnlySpan<byte> s, int i)
    {
        while (i < s.Length)
        {
            if (s[i] == '\\')
            {
                i += 2;
            }
            else if (s[i] == '"')
            {
                return i + 1;
            }
            else
            {
                i++;
            }
        }

        return s.Length;
    }

    private static bool IsRawStringStart(ReadOnlySpan<byte> s, int i, out int prefixLen, out int hashes)
    {
        prefixLen = 0;
        hashes = 0;
        var p = i;
        if (At(s, p) == 'b')
        {
            p++;
        }

        if (At(s, p) != 'r')
        {
            return false;
        }

        p++;
        var h = 0;
        while (At(s, p + h) == '#')
        {
            h++;
        }

        if (At(s, p + h) != '"')
        {
            return false;
        }

        prefixLen = p - i;
        hashes = h;
        return true;
    }

    private static int SkipRawString(ReadOnlySpan<byte> s, int i, int hashes)
    {
        while (i < s.Length)
        {
            if (s[i] == '"')
            {
                var h = 0;
                while (h < hashes && At(s, i + 1 + h) == '#')
                {
                    h++;
                }

                if (h == hashes)
                {
                    return i + 1 + hashes;
                }
            }

            i++;
        }

        return s.Length;
    }

    /// <summary>Tries to read a char literal starting at the quote at <paramref name="i"/>.</summary>
    private static bool TryChar(ReadOnlySpan<byte> s, int i, out int end)
    {
        end = 0;
        var p = i + 1;
        if (At(s, p) == '\\')
        {
            p += 2;
            while (p < s.Length && s[p] != '\'' && s[p] != '\n')
            {
                p++;
            }

            if (At(s, p) == '\'')
            {
                end = p + 1;
                return true;
            }

            return false;
        }

        // One (possibly multi-byte) scalar followed by a quote.
        if (p >= s.Length || s[p] == '\'' || s[p] == '\n')
        {
            return false;
        }

        p++;
        while (p < s.Length && (s[p] & 0xC0) == 0x80)
        {
            p++;
        }

        if (At(s, p) == '\'')
        {
            end = p + 1;
            return true;
        }

        return false;
    }
}
