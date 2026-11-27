namespace ScipSharp.Indexer.Vb6.Syntax;

internal enum Visibility
{
    Default,
    Public,
    Private,
    Friend,
    Global,
}

internal enum ProcedureKind
{
    Sub,
    Function,
    PropertyGet,
    PropertyLet,
    PropertySet,
}

/// <summary>One statement: the tokens between line ends / colons (a label keeps its trailing colon).</summary>
internal sealed record Statement(Token[] Tokens)
{
    public Token First => Tokens[0];

    public Token Last => Tokens[^1];

    public bool IsLabel => Tokens.Length == 2 && Tokens[1].IsPunct(":");

    public bool StartsWith(params string[] words)
    {
        if (Tokens.Length < words.Length)
        {
            return false;
        }

        for (var i = 0; i < words.Length; i++)
        {
            if (!Tokens[i].Is(words[i]))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary><c>As [New] Lib.Type [* len]</c>. <see cref="Parts"/> is the (possibly qualified) type name.</summary>
internal sealed record TypeRefSyntax(List<Token> Parts, bool IsNew, List<Token> FixedLength)
{
    public string Text => string.Join('.', Parts.Select(p => p.Text));

    public bool IsBuiltin => Parts.Count == 1 && !Parts[0].Bracketed && Keywords.BuiltinTypes.Contains(Parts[0].Text);
}

internal sealed record ParameterSyntax(
    Token Name,
    TypeRefSyntax? Type,
    List<Token> Default,
    bool IsOptional,
    bool IsByVal,
    bool IsParamArray,
    bool IsArray);

/// <summary>A module-level declaration (or a member of a Type/Enum).</summary>
internal abstract class MemberSyntax(Token name, Visibility visibility)
{
    public Token Name { get; } = name;

    public Visibility Visibility { get; } = visibility;

    /// <summary>Start of the declaration (first modifier); used for documentation comments and enclosing ranges.</summary>
    public Token Start { get; set; } = name;

    /// <summary>Last token of the declaration (the <c>End Sub</c> of a procedure).</summary>
    public Token End { get; set; } = name;

    /// <summary>The declaration header as written (whitespace collapsed).</summary>
    public string Signature { get; set; } = "";

    /// <summary>Text of a <c>VB_Description</c> attribute, if any.</summary>
    public string? Description { get; set; }

    public bool IsPublicOr(bool defaultPublic) => Visibility switch
    {
        Visibility.Public or Visibility.Global or Visibility.Friend => true,
        Visibility.Private => false,
        _ => defaultPublic,
    };
}

internal sealed class ProcedureSyntax(Token name, Visibility visibility, ProcedureKind kind) : MemberSyntax(name, visibility)
{
    public ProcedureKind Kind { get; } = kind;

    public bool IsStatic { get; set; }

    public List<ParameterSyntax> Parameters { get; } = [];

    public TypeRefSyntax? ReturnType { get; set; }

    public List<Statement> Body { get; } = [];

    /// <summary>True when the closing <c>End Sub/Function/Property</c> was found.</summary>
    public bool Closed { get; set; }
}

internal sealed class DeclareSyntax(Token name, Visibility visibility, bool isFunction) : MemberSyntax(name, visibility)
{
    public bool IsFunction { get; } = isFunction;

    public string Library { get; set; } = "";

    public string? Alias { get; set; }

    public List<ParameterSyntax> Parameters { get; } = [];

    public TypeRefSyntax? ReturnType { get; set; }
}

internal sealed class VariableSyntax(Token name, Visibility visibility) : MemberSyntax(name, visibility)
{
    public bool IsConst { get; set; }

    public bool IsWithEvents { get; set; }

    public bool IsArray { get; set; }

    public bool IsStatic { get; set; }

    public TypeRefSyntax? Type { get; set; }

    /// <summary>Expressions in the declaration: array bounds, constant value.</summary>
    public List<List<Token>> Expressions { get; } = [];
}

internal sealed class EventSyntax(Token name, Visibility visibility) : MemberSyntax(name, visibility)
{
    public List<ParameterSyntax> Parameters { get; } = [];
}

internal sealed class UdtSyntax(Token name, Visibility visibility) : MemberSyntax(name, visibility)
{
    public List<VariableSyntax> Fields { get; } = [];
}

internal sealed class EnumSyntax(Token name, Visibility visibility) : MemberSyntax(name, visibility)
{
    public List<EnumMemberSyntax> Members { get; } = [];
}

internal sealed class EnumMemberSyntax(Token name) : MemberSyntax(name, Visibility.Public)
{
    public List<Token> Value { get; } = [];
}

/// <summary>The code part of a module.</summary>
internal sealed class ModuleSyntax
{
    /// <summary>The string token of <c>Attribute VB_Name = "..."</c>.</summary>
    public Token? NameAttribute { get; set; }

    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool OptionExplicit { get; set; }

    public List<MemberSyntax> Members { get; } = [];

    public List<TypeRefSyntax> Implements { get; } = [];

    /// <summary>Statements at module level that are not declarations (kept so their names can still be resolved).</summary>
    public List<Statement> LooseStatements { get; } = [];

    public IEnumerable<ProcedureSyntax> Procedures => Members.OfType<ProcedureSyntax>();
}
