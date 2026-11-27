namespace ScipSharp.Indexer.Delphi.Syntax;

internal enum TokenKind : byte
{
    Identifier,
    Keyword,
    Number,
    String,
    Symbol,
    Directive,
    EndOfFile,
}

[Flags]
internal enum TokenFlags : byte
{
    None = 0,

    /// <summary>Written as <c>&amp;name</c>: never a keyword or directive.</summary>
    Escaped = 1,

    /// <summary>Lexed while <c>{$SCOPEDENUMS ON}</c> was active.</summary>
    ScopedEnums = 2,
}

/// <summary>
/// A lexical token. Positions are zero-based lines and UTF-16 code unit columns of the file <see cref="File"/>
/// (an index into the project's file table; tokens of <c>{$I file}</c> includes keep the include file's id).
/// </summary>
internal readonly record struct Token(
    TokenKind Kind,
    string Text,
    int File,
    int Line,
    int Column,
    int EndLine,
    int EndColumn,
    int Start,
    int End,
    TokenFlags Flags = TokenFlags.None,
    string? Doc = null)
{
    public bool IsKeyword(string keyword) => Kind == TokenKind.Keyword && string.Equals(Text, keyword, StringComparison.OrdinalIgnoreCase);

    public bool IsSymbol(string symbol) => Kind == TokenKind.Symbol && Text == symbol;

    /// <summary>An identifier (not escaped) spelled <paramref name="word"/>, e.g. a context-sensitive directive like <c>read</c>.</summary>
    public bool IsWord(string word) =>
        Kind == TokenKind.Identifier && (Flags & TokenFlags.Escaped) == 0 && string.Equals(Text, word, StringComparison.OrdinalIgnoreCase);

    public bool IsIdentifier => Kind == TokenKind.Identifier;

    public bool IsEof => Kind == TokenKind.EndOfFile;

    public Range Range => new(Line, Column, EndLine, EndColumn);

    public override string ToString() => $"{Kind}:{Text}@{Line}:{Column}";
}

internal static class Keywords
{
    /// <summary>Delphi reserved words (directives such as <c>read</c>, <c>override</c> or <c>helper</c> are ordinary identifiers).</summary>
    public static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "array", "as", "asm", "begin", "case", "class", "const", "constructor", "destructor", "dispinterface",
        "div", "do", "downto", "else", "end", "except", "exports", "file", "finalization", "finally", "for", "function",
        "goto", "if", "implementation", "in", "inherited", "initialization", "inline", "interface", "is", "label",
        "library", "mod", "nil", "not", "object", "of", "or", "packed", "procedure", "program", "property", "raise",
        "record", "repeat", "resourcestring", "set", "shl", "shr", "string", "then", "threadvar", "to", "try", "type",
        "unit", "until", "uses", "var", "while", "with", "xor",
    };
}
