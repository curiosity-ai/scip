namespace Scip.Indexer.TypeScript.Syntax;

/// <summary>Node kinds. Names follow the TypeScript compiler's <c>SyntaxKind</c> so upstream logic ports 1:1.</summary>
public enum SK
{
    TsSourceFile, Identifier, PrivateIdentifier, StringLiteral, NumericLiteral, RegexLiteral, KeywordLiteral,
    TemplateExpression, TaggedTemplate, This, Super,
    VariableStatement, VariableDeclarationList, VariableDeclaration,
    FunctionDeclaration, FunctionExpression, ArrowFunction, ClassDeclaration, ClassExpression,
    InterfaceDeclaration, TypeAliasDeclaration, EnumDeclaration, EnumMember, ModuleDeclaration, ModuleBlock,
    ImportDeclaration, ImportClause, NamespaceImport, NamedImports, ImportSpecifier, ImportEqualsDeclaration,
    ExternalModuleReference, ExportDeclaration, NamedExports, NamespaceExport, ExportSpecifier, ExportAssignment,
    Parameter, TypeParameter, PropertyDeclaration, PropertySignature, MethodDeclaration, MethodSignature,
    Constructor, GetAccessor, SetAccessor, IndexSignature, CallSignature, ConstructSignature, ClassStaticBlock,
    Block, ExpressionStatement, IfStatement, ForStatement, ForInStatement, ForOfStatement, WhileStatement,
    DoStatement, ReturnStatement, ThrowStatement, TryStatement, CatchClause, SwitchStatement, CaseBlock,
    CaseClause, DefaultClause, LabeledStatement, BreakStatement, ContinueStatement, EmptyStatement, DebuggerStatement,
    CallExpression, NewExpression, PropertyAccessExpression, ElementAccessExpression, BinaryExpression,
    PrefixUnaryExpression, PostfixUnaryExpression, ConditionalExpression, SpreadElement, ObjectLiteralExpression,
    ArrayLiteralExpression, PropertyAssignment, ShorthandPropertyAssignment, SpreadAssignment, ComputedPropertyName,
    ObjectBindingPattern, ArrayBindingPattern, BindingElement, OmittedExpression, ParenthesizedExpression,
    AsExpression, SatisfiesExpression, NonNullExpression, TypeAssertion, AwaitExpression, YieldExpression,
    TypeOfExpression, VoidExpression, DeleteExpression, MetaProperty, ImportCall, CommaList,
    TypeReference, QualifiedName, TypeLiteral, UnionType, IntersectionType, ArrayType, TupleType, NamedTupleMember,
    FunctionType, ConstructorType, TypeQuery, TypeOperator, IndexedAccessType, ConditionalType, InferType,
    MappedType, LiteralType, ParenthesizedType, KeywordType, TypePredicate, ImportType, ThisType, TemplateLiteralType,
    OptionalType, RestType,
    Decorator, HeritageClause, ExpressionWithTypeArguments,
    JsxElement, JsxFragment, JsxAttribute, JsxSpreadAttribute, JsxExpression,
}

[Flags]
public enum Mods
{
    None = 0, Export = 1, Default = 2, Declare = 4, Public = 8, Private = 16, Protected = 32, Static = 64,
    Readonly = 128, Abstract = 256, Override = 512, Async = 1024, Const = 2048, Accessor = 4096,
}

/// <summary>A syntax tree node. Only the slots a given <see cref="Kind"/> uses are populated.</summary>
public sealed class Node(SK kind, int pos)
{
    public SK Kind { get; } = kind;

    /// <summary>Start of the node's first token (like <c>node.getStart()</c>).</summary>
    public int Start { get; set; } = pos;

    public int End { get; set; }

    public Node? Parent { get; set; }

    public TsSourceFile? File { get; set; }

    /// <summary>Identifier name, literal value, operator text, ...</summary>
    public string Text { get; set; } = "";

    public Mods Modifiers { get; set; }

    /// <summary>Declaration list flags: const/let/using.</summary>
    public bool IsConst { get; set; }

    public bool IsLet { get; set; }

    public bool IsTypeOnly { get; set; }

    public bool IsExtends { get; set; }

    /// <summary>Raw JSDoc comment text preceding the node, if any.</summary>
    public string? JsDoc { get; set; }

    public Node? Name { get; set; }
    public Node? PropertyName { get; set; }
    public Node? Type { get; set; }
    public Node? Expression { get; set; }
    public Node? Left { get; set; }
    public Node? Right { get; set; }
    public Node? Initializer { get; set; }
    public Node? Body { get; set; }
    public Node? Clause { get; set; }
    public Node? Extra { get; set; }
    public Node? ModuleSpecifier { get; set; }
    public Node? Condition { get; set; }
    public Node? Then { get; set; }
    public Node? Else { get; set; }

    public List<Node> Decorators { get; } = [];
    public List<Node> TypeParameters { get; } = [];
    public List<Node> Parameters { get; } = [];
    public List<Node> TypeArguments { get; } = [];
    public List<Node> Arguments { get; } = [];
    public List<Node> Members { get; } = [];
    public List<Node> HeritageClauses { get; } = [];
    public List<Node> Elements { get; } = [];

    private List<Node>? _children;

    /// <summary>Children in source order (port of <c>ts.forEachChild</c> order for our purposes).</summary>
    public IReadOnlyList<Node> Children
    {
        get
        {
            if (_children is not null)
            {
                return _children;
            }

            var list = new List<Node>();
            void Add(Node? n) { if (n is not null) { list.Add(n); } }
            void AddAll(List<Node> ns) { foreach (var n in ns) { list.Add(n); } }
            AddAll(Decorators);
            Add(PropertyName);
            Add(Name);
            AddAll(TypeParameters);
            AddAll(Parameters);
            Add(Expression);
            AddAll(TypeArguments);
            Add(Left);
            Add(Right);
            Add(Clause);
            Add(Extra);
            AddAll(Arguments);
            Add(Condition);
            Add(Then);
            Add(Else);
            AddAll(HeritageClauses);
            AddAll(Members);
            AddAll(Elements);
            Add(Type);
            Add(Initializer);
            Add(Body);
            Add(ModuleSpecifier);
            // Stable sort by position gives source order regardless of slot order above.
            _children = list.Select((n, i) => (n, i)).OrderBy(x => x.n.Start).ThenBy(x => x.i).Select(x => x.n).ToList();
            return _children;
        }
    }

    public bool Has(Mods m) => (Modifiers & m) != 0;

    public string GetText() => File!.Text[Start..End];

    public override string ToString() => $"{Kind}({Start}..{End}) {Text}";
}

public sealed class TsSourceFile
{
    public required string FileName { get; init; }
    public required string Text { get; init; }
    public Node Root { get; set; } = null!;
    public bool IsExternalModule { get; set; }
    public bool IsJavaScript { get; init; }
    /// <summary>Syntax errors the parser recovered from (position, message).</summary>
    public List<(int Position, string Message)> Diagnostics { get; } = [];
    private int[]? _lineStarts;

    public int[] LineStarts => _lineStarts ??= ComputeLineStarts();

    private int[] ComputeLineStarts()
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < Text.Length; i++)
        {
            var c = Text[i];
            if (c == '\r')
            {
                if (i + 1 < Text.Length && Text[i + 1] == '\n') { i++; }
                starts.Add(i + 1);
            }
            else if (c == '\n' || c == '\u2028' || c == '\u2029')
            {
                starts.Add(i + 1);
            }
        }

        return [.. starts];
    }

    public (int Line, int Character) LineAndCharacter(int position)
    {
        var idx = Array.BinarySearch(LineStarts, position);
        if (idx < 0) { idx = ~idx - 1; }
        return (idx, position - LineStarts[idx]);
    }
}
