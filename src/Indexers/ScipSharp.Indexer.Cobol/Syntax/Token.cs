namespace ScipSharp.Indexer.Cobol.Syntax;

/// <summary>Reference format of a COBOL source file.</summary>
public enum SourceFormat
{
    /// <summary>Detect per file (fixed unless the layout or a directive says otherwise).</summary>
    Auto,

    /// <summary>Columns 1-6 sequence area, 7 indicator, 8-72 program text, 73-80 identification area.</summary>
    Fixed,

    /// <summary>Free format (<c>&gt;&gt;SOURCE FORMAT FREE</c>): the whole line is program text, <c>*&gt;</c> starts a comment.</summary>
    Free,

    /// <summary>Micro Focus "variable" format: like fixed, but program text runs to the end of the line.</summary>
    Variable,
}

internal enum TokenKind
{
    Word,
    Literal,
    Picture,
    Period,
    Comma,
    LParen,
    RParen,
    Colon,
    PseudoDelimiter,
    Operator,
    Other,
}

/// <summary>Where a token is in its source file: zero-based line and UTF-16 code unit columns [Start, End).</summary>
internal readonly record struct SourceLocation(int FileId, int Line, int Start, int End);

/// <summary>A lexical token. Tokens produced by COPY ... REPLACING may have no location (and then never become occurrences).</summary>
internal sealed record Token(TokenKind Kind, string Text, SourceLocation? Location)
{
    /// <summary>Upper-cased text: COBOL words are case-insensitive.</summary>
    public string Upper { get; init; } = Text.ToUpperInvariant();

    /// <summary>Whitespace (or a line break) precedes the token.</summary>
    public bool SpaceBefore { get; init; } = true;

    /// <summary>The token is the first one on its source line.</summary>
    public bool FirstOnLine { get; init; }

    /// <summary>Fixed format only: the token starts in Area A (columns 8-11).</summary>
    public bool AreaA { get; init; }

    /// <summary>File id of the copybook this token was copied from, or -1 when it belongs to the program text itself.</summary>
    public int Origin { get; init; } = -1;

    public bool IsWord => Kind == TokenKind.Word;

    public bool Is(string upper) => Kind == TokenKind.Word && Upper == upper;

    /// <summary>For a literal: its value without prefix and quotes (doubled quotes collapsed). Otherwise the text.</summary>
    public string Value => Kind == TokenKind.Literal ? LiteralValue(Text) : Text;

    /// <summary>True when the word consists of digits only (level numbers, integer literals).</summary>
    public bool IsInteger => Kind == TokenKind.Word && Text.Length > 0 && Text.All(char.IsAsciiDigit);

    /// <summary>True for a word that can be a user-defined name (contains at least one letter).</summary>
    public bool IsUserWordShape => Kind == TokenKind.Word && Text.Any(char.IsLetter);

    private static string LiteralValue(string text)
    {
        var q = text.IndexOfAny(['\'', '"']);
        if (q < 0)
        {
            return text;
        }

        var quote = text[q];
        var body = text.AsSpan(q + 1);
        if (body.Length > 0 && body[^1] == quote)
        {
            body = body[..^1];
        }

        return body.ToString().Replace(new string(quote, 2), new string(quote, 1), StringComparison.Ordinal);
    }

    public override string ToString() => Text;
}
