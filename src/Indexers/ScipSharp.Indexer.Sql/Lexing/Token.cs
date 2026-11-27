namespace ScipSharp.Indexer.Sql.Lexing;

#pragma warning disable CA1720 // token kinds are named after the lexical class
public enum TokenKind
{
    /// <summary>An unquoted word: keyword or identifier.</summary>
    Word,

    /// <summary><c>"x"</c>, <c>[x]</c> or <c>`x`</c>; <see cref="Token.Value"/> holds the unescaped name.</summary>
    QuotedIdent,

    /// <summary>Any string literal (including dollar-quoted, E'', N'', q'[..]'); <see cref="Token.Value"/> holds the content.</summary>
    String,
    Number,

    /// <summary><c>@x</c>, <c>@@x</c>, <c>:x</c>, <c>$1</c>, <c>?</c>.</summary>
    Variable,

    /// <summary>Operators and punctuation (including a <c>;</c> inside a custom MySQL <c>DELIMITER</c> region).</summary>
    Op,

    /// <summary>A top-level <c>;</c>.</summary>
    Semicolon,

    /// <summary>A Jinja/dbt <c>{{ }}</c> or <c>{% %}</c> block; <see cref="Token.Value"/> holds a <c>ref()</c>/<c>source()</c> target if any.</summary>
    Template,

    /// <summary>T-SQL <c>GO</c> or SQL*Plus <c>/</c> on a line of its own.</summary>
    BatchSeparator,

    /// <summary>A custom MySQL client delimiter (after <c>DELIMITER $$</c>).</summary>
    Delimiter,
    EndOfFile,
}
#pragma warning restore CA1720

/// <summary>A lexical token. Offsets are UTF-16 indices into <see cref="SourceText.Text"/>.</summary>
public sealed class Token(TokenKind kind, int start, int end, string text, string value)
{
    public TokenKind Kind { get; } = kind;

    public int Start { get; } = start;

    public int End { get; } = end;

    /// <summary>Raw source text.</summary>
    public string Text { get; } = text;

    /// <summary>Unquoted identifier, string content, or the raw text for other kinds.</summary>
    public string Value { get; } = value;

    /// <summary>Upper-cased word (for keyword comparisons); empty for non-words.</summary>
    public string Upper { get; } = kind == TokenKind.Word ? text.ToUpperInvariant() : "";

    public bool IsWord => Kind == TokenKind.Word;

    public bool IsName => Kind is TokenKind.Word or TokenKind.QuotedIdent;

    public bool Is(string keyword) => Kind == TokenKind.Word && Upper == keyword;

    public bool IsOp(string op) => Kind == TokenKind.Op && Text == op;

    public bool IsSemi => Kind is TokenKind.Semicolon or TokenKind.Delimiter || (Kind == TokenKind.Op && Text == ";");

    public bool IsTerminator => IsSemi || Kind is TokenKind.BatchSeparator or TokenKind.EndOfFile;

    public override string ToString() => $"{Kind}:{Text}@{Start}";
}

/// <summary>A comment (kept out of the token stream; used for documentation).</summary>
public sealed record Comment(int Start, int End, string Text, bool IsLineComment);

/// <summary>Source text with a line table (positions are UTF-16 code units from the line start).</summary>
public sealed class SourceText
{
    private readonly int[] _lineStarts;

    public SourceText(string text)
    {
        Text = text;
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        _lineStarts = [.. starts];
    }

    public string Text { get; }

    public int LineCount => _lineStarts.Length;

    public int LineOf(int offset)
    {
        var i = Array.BinarySearch(_lineStarts, offset);
        return i >= 0 ? i : ~i - 1;
    }

    public (int Line, int Column) Position(int offset)
    {
        var line = LineOf(offset);
        return (line, offset - _lineStarts[line]);
    }

    public int LineStart(int line) => _lineStarts[line];
}
