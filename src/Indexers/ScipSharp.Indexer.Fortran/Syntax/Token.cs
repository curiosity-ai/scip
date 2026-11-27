namespace ScipSharp.Indexer.Fortran.Syntax;

/// <summary>Lexical category of a <see cref="Token"/>.</summary>
internal enum TokenKind
{
    /// <summary>An identifier or keyword (Fortran keywords are not reserved).</summary>
    Name,

    /// <summary>An integer literal (also statement labels).</summary>
    Int,

    /// <summary>A real literal.</summary>
    Real,

    /// <summary>A character literal or Hollerith constant.</summary>
    String,

    /// <summary>A BOZ literal.</summary>
    Boz,

    /// <summary>A dot operator such as <c>.and.</c> or a user-defined <c>.cross.</c>.</summary>
    DotOp,

    /// <summary><c>.true.</c> / <c>.false.</c>.</summary>
    Logical,

    /// <summary>Punctuation and symbolic operators.</summary>
    Op,
}

/// <summary>A token with its position in the original document (UTF-16 offsets, zero based).</summary>
internal sealed class Token(TokenKind kind, string text, SourceDoc file, int line, int col, int endCol)
{
    private string? _lower;

    public TokenKind Kind { get; } = kind;

    public string Text { get; } = text;

    /// <summary>Lower-cased text: Fortran names are case-insensitive.</summary>
    public string Lower => _lower ??= Text.ToLowerInvariant();

    public SourceDoc File { get; } = file;

    public int Line { get; } = line;

    public int Col { get; } = col;

    public int EndCol { get; } = endCol;

    /// <summary>True for the kind parameter of a literal (<c>dp</c> in <c>1.0_dp</c>).</summary>
    public bool IsKindParam { get; init; }

    public bool IsName => Kind == TokenKind.Name;

    public bool Is(string op) => Kind == TokenKind.Op && Text == op;

    public bool IsKw(string keyword) => Kind == TokenKind.Name && Lower == keyword;

    public override string ToString() => $"{Kind}:{Text}@{Line}:{Col}";
}

/// <summary>A source file taking part in the index (a compilation unit or an included file).</summary>
internal sealed class SourceDoc(string absolutePath, string relativePath, string[] lines, SourceForm form)
{
    public string AbsolutePath { get; } = absolutePath;

    /// <summary>Path relative to the project root, with '/' separators.</summary>
    public string RelativePath { get; } = relativePath;

    public string[] Lines { get; } = lines;

    public SourceForm Form { get; set; } = form;

    /// <summary>True when the file is only indexed through INCLUDE / #include.</summary>
    public bool IsInclude { get; set; }

    /// <summary>Names introduced with <c>#define</c>; never resolved as Fortran entities.</summary>
    public HashSet<string> Macros { get; } = new(StringComparer.Ordinal);

    public override string ToString() => RelativePath;
}
