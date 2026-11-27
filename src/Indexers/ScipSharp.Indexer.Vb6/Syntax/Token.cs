namespace ScipSharp.Indexer.Vb6.Syntax;

internal enum TokenKind
{
    Identifier,
    String,
    Number,
    Date,
    Punct,
    NewLine,
}

/// <summary>
/// A VB6 token. Positions are zero-based (line, UTF-16 column) into the decoded source; for identifiers they cover the name
/// only (no surrounding brackets, no type-suffix character).
/// </summary>
internal sealed record Token(TokenKind Kind, string Text, int Line, int Col, int EndCol, char Suffix = '\0', bool Bracketed = false)
{
    /// <summary>A reserved word (never a bracketed identifier).</summary>
    public bool IsKeyword => Kind == TokenKind.Identifier && !Bracketed && Keywords.IsReserved(Text);

    /// <summary>A plain (non-reserved) identifier.</summary>
    public bool IsName => Kind == TokenKind.Identifier && (Bracketed || !Keywords.IsReserved(Text));

    /// <summary>Case-insensitive comparison with a keyword or punctuation text.</summary>
    public bool Is(string text) => Kind != TokenKind.String && !Bracketed && string.Equals(Text, text, StringComparison.OrdinalIgnoreCase);

    public bool IsPunct(string text) => Kind == TokenKind.Punct && Text == text;

    public override string ToString() => $"{Kind}:{Text}@{Line}:{Col}";
}

internal static class Keywords
{
    // Reserved words: they can never name a variable or procedure (they may still follow a '.' as a member name, which
    // the resolver handles). Statement words such as Open/Close/Print/Name/Line/Get/Put are deliberately absent because
    // they are legal member names (rs.Close, Printer.Print, x.Name).
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "AddressOf", "Alias", "And", "Any", "As", "Boolean", "ByRef", "Byte", "ByVal", "Call", "Case", "CBool", "CByte",
        "CCur", "CDate", "CDbl", "CDec", "CInt", "CLng", "CLngLng", "CLngPtr", "Const", "CSng", "CStr", "Currency", "CVar",
        "Date", "Decimal", "Declare", "DefBool", "DefByte", "DefCur", "DefDate", "DefDbl", "DefDec", "DefInt", "DefLng",
        "DefLngLng", "DefLngPtr", "DefObj", "DefSng", "DefStr", "DefVar", "Dim", "Do", "Double", "Each", "Else", "ElseIf",
        "Empty", "End", "Enum", "Eqv", "Erase", "Event", "Exit", "False", "For", "Friend", "Function", "GoSub", "GoTo",
        "If", "Imp", "Implements", "In", "Integer", "Is", "Let", "Lib", "Like", "Long", "LongLong", "LongPtr", "Loop",
        "Me", "Mod", "New", "Next", "Not", "Nothing", "Null", "Object", "On", "Option", "Optional", "Or", "ParamArray",
        "Preserve", "Private", "Property", "PtrSafe", "Public", "RaiseEvent", "ReDim", "Rem", "Resume", "Select", "Set",
        "Single", "Static", "Step", "Stop", "String", "Sub", "Then", "To", "True", "Type", "TypeOf", "Until", "Variant",
        "Wend", "While", "With", "WithEvents", "Xor", "Global",
    };

    /// <summary>Built-in type names usable after <c>As</c>.</summary>
    public static readonly HashSet<string> BuiltinTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Boolean", "Byte", "Currency", "Date", "Decimal", "Double", "Integer", "Long", "LongLong", "LongPtr", "Object",
        "Single", "String", "Variant", "Any",
    };

    public static bool IsReserved(string text) => Reserved.Contains(text);
}
