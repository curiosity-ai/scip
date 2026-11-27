namespace ScipSharp.Indexer.Pascal.Syntax;

internal enum TokenKind : byte
{
    Identifier,
    Keyword,
    Number,
    String,
    Symbol,
    Directive,
    Eof,
}

/// <summary>A lexical token. <see cref="Start"/>/<see cref="End"/> are UTF-16 offsets into <see cref="Source"/>.</summary>
internal sealed class Token(TokenKind kind, string text, SourceText source, int start, int end)
{
    private string? _lower;

    public TokenKind Kind { get; set; } = kind;

    /// <summary>Token text; for escaped identifiers (<c>&amp;begin</c>) the name without the ampersand.</summary>
    public string Text { get; } = text;

    public SourceText Source { get; } = source;

    public int Start { get; } = start;

    public int End { get; } = end;

    /// <summary>An identifier written with a leading <c>&amp;</c>: never a keyword.</summary>
    public bool Escaped { get; init; }

    public string Lower => _lower ??= Text.ToLowerInvariant();

    public bool IsIdentifier => Kind == TokenKind.Identifier;

    /// <summary>Identifier or keyword with the given lower-case spelling.</summary>
    public bool IsWord(string lower) => Kind is TokenKind.Identifier or TokenKind.Keyword && !Escaped && Lower == lower;

    public bool IsKeyword(string lower) => Kind == TokenKind.Keyword && Lower == lower;

    public bool IsSymbol(string symbol) => Kind == TokenKind.Symbol && Text == symbol;

    public override string ToString() => $"{Kind} '{Text}' @{Start}";
}
