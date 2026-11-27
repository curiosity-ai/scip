namespace ScipSharp.Indexer.Delphi.Syntax;

/// <summary>A half-open range of token indices <c>[Start, End)</c>.</summary>
internal readonly record struct TokRange(int Start, int End)
{
    public bool IsEmpty => End <= Start;
}

internal enum FileKind
{
    Unit,
    Program,
    Library,
    Package,
}

internal enum Visibility
{
    Default,
    StrictPrivate,
    Private,
    StrictProtected,
    Protected,
    Public,
    Published,
    Automated,
}

/// <summary>One segment of a (possibly generic, possibly dotted) name: <c>TList&lt;Integer&gt;</c>.</summary>
internal sealed record NamePart(int Tok, List<TypeNode>? Args);

/// <summary>An attribute <c>[Name(args)]</c>.</summary>
internal sealed record AttributeNode(NamedTypeNode Name, TokRange? Args);

internal abstract class TypeNode
{
    public int Start { get; set; }

    public int End { get; set; }
}

/// <summary>A reference to a named type: <c>Integer</c>, <c>string</c>, <c>System.Classes.TStrings</c>, <c>TList&lt;T&gt;</c>.</summary>
internal sealed class NamedTypeNode : TypeNode
{
    public List<NamePart> Parts { get; } = [];

    /// <summary>For <c>string[10]</c> and similar: the bracketed length expression.</summary>
    public TokRange? Length { get; set; }
}

internal enum StructKind
{
    Class,
    Record,
    Interface,
    DispInterface,
    Object,
    ClassHelper,
    RecordHelper,
}

internal sealed class StructTypeNode : TypeNode
{
    public StructKind Kind { get; set; }

    public List<NamedTypeNode> Ancestors { get; } = [];

    public NamedTypeNode? HelperFor { get; set; }

    public bool IsForward { get; set; }

    public bool IsAbstract { get; set; }

    public bool IsSealed { get; set; }

    public int? GuidTok { get; set; }

    public List<Decl> Members { get; } = [];

    /// <summary>Token index of the closing <c>end</c> (or -1).</summary>
    public int EndTok { get; set; } = -1;
}

internal sealed class ClassOfTypeNode(NamedTypeNode target) : TypeNode
{
    public NamedTypeNode Target { get; } = target;
}

internal sealed record EnumItem(int NameTok, TokRange? Value);

internal sealed class EnumTypeNode : TypeNode
{
    public List<EnumItem> Items { get; } = [];

    public bool Scoped { get; set; }
}

internal sealed class SetTypeNode(TypeNode? element) : TypeNode
{
    public TypeNode? Element { get; } = element;
}

internal sealed class ArrayTypeNode : TypeNode
{
    public List<TypeNode> Indexes { get; } = [];

    /// <summary>Element type, or null for <c>array of const</c>.</summary>
    public TypeNode? Element { get; set; }
}

internal sealed class PointerTypeNode(TypeNode? target) : TypeNode
{
    public TypeNode? Target { get; } = target;
}

internal sealed class FileTypeNode(TypeNode? element) : TypeNode
{
    public TypeNode? Element { get; } = element;
}

internal sealed class ProcTypeNode : TypeNode
{
    public RoutineKind Kind { get; set; }

    public List<ParamGroup> Params { get; } = [];

    public TypeNode? Return { get; set; }

    public bool OfObject { get; set; }

    public bool IsReference { get; set; }
}

/// <summary>A subrange (<c>1..10</c>, <c>Low(X)..High(X)</c>) or any other type written as an expression.</summary>
internal sealed class ExprTypeNode(TokRange expr) : TypeNode
{
    public TokRange Expr { get; } = expr;
}

internal enum RoutineKind
{
    Procedure,
    Function,
    Constructor,
    Destructor,
    Operator,
}

internal sealed class ParamGroup
{
    public List<int> NameToks { get; } = [];

    public string? Modifier { get; set; }

    public TypeNode? Type { get; set; }

    public TokRange? Default { get; set; }

    public List<AttributeNode> Attributes { get; } = [];
}

internal sealed record TypeParamNode(int NameTok, List<TypeNode> Constraints);

internal abstract class Decl
{
    /// <summary>First token of the declaration (including attributes and leading <c>class</c>).</summary>
    public int Start { get; set; }

    /// <summary>Token index just past the declaration.</summary>
    public int End { get; set; }

    public string? Doc { get; set; }

    public List<AttributeNode> Attributes { get; } = [];

    public Visibility Visibility { get; set; }
}

internal sealed class TypeDecl : Decl
{
    public int NameTok { get; set; }

    public List<TypeParamNode> TypeParams { get; } = [];

    public TypeNode? Type { get; set; }

    public bool IsStrongAlias { get; set; }
}

internal sealed class ConstDecl : Decl
{
    public int NameTok { get; set; }

    public TypeNode? Type { get; set; }

    public TokRange? Value { get; set; }

    public bool IsResourceString { get; set; }

    public bool IsClassMember { get; set; }
}

internal enum VarKind
{
    Var,
    ThreadVar,
    Field,
    ClassVar,
}

internal sealed class VarDecl : Decl
{
    public List<int> NameToks { get; } = [];

    public TypeNode? Type { get; set; }

    public TokRange? Init { get; set; }

    public TokRange? Absolute { get; set; }

    public VarKind Kind { get; set; }
}

internal sealed class RoutineBody
{
    public List<Decl> Locals { get; } = [];

    /// <summary>The <c>begin … end</c> / <c>asm … end</c> block, including both keywords.</summary>
    public TokRange Block { get; set; }

    public bool IsAsm { get; set; }
}

internal sealed class RoutineDecl : Decl
{
    public RoutineKind Kind { get; set; }

    public bool IsClassMethod { get; set; }

    /// <summary>The name, possibly qualified (<c>TFoo.Bar</c>, <c>TOuter.TInner&lt;T&gt;.Bar</c>, <c>IFoo.Bar</c>).</summary>
    public List<NamePart> NameParts { get; } = [];

    /// <summary>Type parameters of the routine itself (the last name part's args, as declared).</summary>
    public List<TypeParamNode> TypeParams { get; } = [];

    /// <summary>For a method resolution clause <c>procedure IFoo.Bar = Baz;</c>: the token of <c>Baz</c>.</summary>
    public int? ResolutionTarget { get; set; }

    public List<ParamGroup> Params { get; } = [];

    public TypeNode? Return { get; set; }

    public HashSet<string> Directives { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Token ranges of directive arguments (e.g. <c>message WM_PAINT</c>, <c>external 'x.dll'</c>).</summary>
    public List<TokRange> DirectiveArgs { get; } = [];

    /// <summary>Token index just past the heading (before directives).</summary>
    public int HeadingEnd { get; set; }

    public RoutineBody? Body { get; set; }

    public int NameTok => NameParts[^1].Tok;

    public bool IsQualified => NameParts.Count > 1;
}

internal sealed class PropertySpec(string keyword, int keywordTok, TokRange value)
{
    public string Keyword { get; } = keyword;

    public int KeywordTok { get; } = keywordTok;

    public TokRange Value { get; } = value;
}

internal sealed class PropertyDecl : Decl
{
    public int NameTok { get; set; }

    public bool IsClassProperty { get; set; }

    public List<ParamGroup> IndexParams { get; } = [];

    public TypeNode? Type { get; set; }

    public List<PropertySpec> Specs { get; } = [];

    /// <summary>Declared with a trailing <c>default;</c> (the default array property).</summary>
    public bool IsDefault { get; set; }
}

/// <summary>A label section (<c>label 10, Done;</c>).</summary>
internal sealed class LabelDecl : Decl
{
    public List<int> NameToks { get; } = [];
}

internal sealed record UsesItem(List<int> NameToks, string Name, int? InTok, string? InPath);

internal sealed class FileSyntax(IReadOnlyList<Token> tokens)
{
    public IReadOnlyList<Token> Tokens { get; } = tokens;

    public FileKind Kind { get; set; }

    public List<int> NameToks { get; } = [];

    public string Name { get; set; } = "";

    public bool HasHeader { get; set; }

    public List<UsesItem> InterfaceUses { get; } = [];

    public List<UsesItem> ImplementationUses { get; } = [];

    public List<UsesItem> Requires { get; } = [];

    public List<Decl> InterfaceDecls { get; } = [];

    public List<Decl> ImplementationDecls { get; } = [];

    /// <summary>Statement blocks: <c>initialization</c>, <c>finalization</c> or the main <c>begin … end.</c>.</summary>
    public List<TokRange> Blocks { get; } = [];

    public int ImplementationTok { get; set; } = -1;

    public string? Doc { get; set; }
}
