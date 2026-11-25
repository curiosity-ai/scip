namespace ScipSharp.Indexer.Php.Syntax;

[Flags]
internal enum Mod
{
    None = 0,
    Public = 1,
    Protected = 2,
    Private = 4,
    Static = 8,
    Abstract = 16,
    Final = 32,
    ReadOnly = 64,
    Visibility = Public | Protected | Private,
}

/// <summary>Base of the PHP syntax tree. <see cref="End"/> is exclusive; offsets are UTF-8 byte offsets.</summary>
internal abstract class Node
{
    public int Start;
    public int End;
    public Node? Parent;
    public DocComment? Doc;

    /// <summary>Index of the first token in PHP's token stream (used for anonymous class / function names).</summary>
    public int TokPos;

    public virtual void Children(List<Node> into)
    {
    }

    protected static void Add(List<Node> into, Node? n)
    {
        if (n != null)
        {
            into.Add(n);
        }
    }

    protected static void Add<T>(List<Node> into, List<T> ns)
        where T : Node
    {
        foreach (var n in ns)
        {
            into.Add(n);
        }
    }
}

internal interface IFunctionLike
{
    Node? ReturnType { get; }

    List<Param> Params { get; }
}

// ------------------------------------------------------------------ names

internal sealed class Name : Node
{
    /// <summary>Resolved (when possible) or raw name, without a leading backslash. This is PHP-Parser's <c>toString()</c>.</summary>
    public string Text = "";

    public NameKind Kind;

    public bool IsSpecialClassName =>
        Kind == NameKind.Unqualified
        && (Text.Equals("self", StringComparison.OrdinalIgnoreCase)
            || Text.Equals("parent", StringComparison.OrdinalIgnoreCase)
            || Text.Equals("static", StringComparison.OrdinalIgnoreCase));

    public override string ToString() => Text;
}

/// <summary>An identifier (method / property / constant name, builtin type). Variable-like identifiers span the <c>$</c> too.</summary>
internal sealed class Ident : Node
{
    public string Name = "";

    public override string ToString() => Name;
}

// ------------------------------------------------------------------ expressions

internal abstract class Expr : Node;

internal sealed class Variable : Expr
{
    public string? Name;
    public Node? NameExpr;

    public override void Children(List<Node> into) => Add(into, NameExpr);
}

internal sealed class ArrayDimFetch : Expr
{
    public Expr Var = null!;
    public Expr? Dim;

    public override void Children(List<Node> into)
    {
        Add(into, Var);
        Add(into, Dim);
    }
}

internal sealed class Assign : Expr
{
    public Expr Var = null!;
    public Expr Value = null!;

    public override void Children(List<Node> into)
    {
        Add(into, Var);
        Add(into, Value);
    }
}

internal sealed class BinaryOp : Expr
{
    public string Op = "";
    public Expr Left = null!;
    public Expr Right = null!;

    public override void Children(List<Node> into)
    {
        Add(into, Left);
        Add(into, Right);
    }
}

internal sealed class CloneExpr : Expr
{
    public Expr Value = null!;

    public override void Children(List<Node> into) => Add(into, Value);
}

internal sealed class Arg : Node
{
    public Expr Value = null!;
    public Ident? Label;
    public bool Unpack;
    public bool ByRef;

    public override void Children(List<Node> into)
    {
        Add(into, Label);
        Add(into, Value);
    }
}

internal sealed class FuncCall : Expr
{
    public Node Callee = null!;
    public List<Arg> Args = [];

    public override void Children(List<Node> into)
    {
        Add(into, Callee);
        Add(into, Args);
    }
}

internal sealed class MethodCall : Expr
{
    public Expr Var = null!;
    public Node NameNode = null!;
    public List<Arg> Args = [];
    public bool Nullsafe;

    public override void Children(List<Node> into)
    {
        Add(into, Var);
        Add(into, NameNode);
        Add(into, Args);
    }
}

internal sealed class StaticCall : Expr
{
    public Node Class = null!;
    public Node NameNode = null!;
    public List<Arg> Args = [];

    public override void Children(List<Node> into)
    {
        Add(into, Class);
        Add(into, NameNode);
        Add(into, Args);
    }
}

internal sealed class NewExpr : Expr
{
    public Node Class = null!;
    public List<Arg> Args = [];

    public override void Children(List<Node> into)
    {
        Add(into, Class);
        Add(into, Args);
    }
}

internal sealed class PropertyFetch : Expr
{
    public Expr Var = null!;
    public Node NameNode = null!;
    public bool Nullsafe;

    public override void Children(List<Node> into)
    {
        Add(into, Var);
        Add(into, NameNode);
    }
}

internal sealed class StaticPropertyFetch : Expr
{
    public Node Class = null!;
    public Node NameNode = null!;

    public override void Children(List<Node> into)
    {
        Add(into, Class);
        Add(into, NameNode);
    }
}

internal sealed class ClassConstFetch : Expr
{
    public Node Class = null!;
    public Node NameNode = null!;

    public override void Children(List<Node> into)
    {
        Add(into, Class);
        Add(into, NameNode);
    }
}

internal sealed class ConstFetch : Expr
{
    public Name Name = null!;

    public override void Children(List<Node> into) => Add(into, Name);
}

internal sealed class Ternary : Expr
{
    public Expr Cond = null!;
    public Expr? If;
    public Expr Else = null!;

    public override void Children(List<Node> into)
    {
        Add(into, Cond);
        Add(into, If);
        Add(into, Else);
    }
}

internal sealed class MatchArm : Node
{
    public List<Expr>? Conds;
    public Expr Body = null!;

    public override void Children(List<Node> into)
    {
        if (Conds != null)
        {
            Add(into, Conds);
        }

        Add(into, Body);
    }
}

internal sealed class MatchExpr : Expr
{
    public Expr Cond = null!;
    public List<MatchArm> Arms = [];

    public override void Children(List<Node> into)
    {
        Add(into, Cond);
        Add(into, Arms);
    }
}

internal sealed class NumberLit : Expr
{
    public string Text = "";
}

internal enum StringKind { Single, Double, Heredoc, Nowdoc, Backtick }

internal sealed class StringLit : Expr
{
    public string Raw = "";
    public StringKind Kind;

    /// <summary>Value for simple single/double-quoted strings.</summary>
    public string Value = "";
}

internal sealed class TemplateExpr : Expr
{
    public List<Node> Parts = [];
    public string Raw = "";

    public override void Children(List<Node> into) => Add(into, Parts);
}

internal sealed class MagicConst : Expr
{
    public string Text = "";
}

internal sealed class ArrayItem : Node
{
    public Expr? Key;
    public Expr? Value;
    public bool ByRef;
    public bool Unpack;

    public override void Children(List<Node> into)
    {
        Add(into, Key);
        Add(into, Value);
    }
}

internal sealed class ArrayLit : Expr
{
    public List<ArrayItem?> Items = [];
    public bool IsList;

    public override void Children(List<Node> into)
    {
        foreach (var i in Items)
        {
            Add(into, i);
        }
    }
}

internal sealed class ClosureUse : Node
{
    public Variable Var = null!;
    public bool ByRef;

    public override void Children(List<Node> into) => Add(into, Var);
}

internal sealed class Closure : Expr, IFunctionLike
{
    public bool Static;
    public bool ByRef;
    public List<AttrGroup> AttrGroups = [];
    public List<Param> Params { get; set; } = [];
    public List<ClosureUse> Uses = [];
    public Node? ReturnType { get; set; }
    public List<Stmt> Stmts = [];

    public override void Children(List<Node> into)
    {
        Add(into, AttrGroups);
        Add(into, Params);
        Add(into, Uses);
        Add(into, ReturnType);
        Add(into, Stmts);
    }
}

internal sealed class ArrowFunction : Expr, IFunctionLike
{
    public bool Static;
    public bool ByRef;
    public List<AttrGroup> AttrGroups = [];
    public List<Param> Params { get; set; } = [];
    public Node? ReturnType { get; set; }
    public Expr Body = null!;

    public override void Children(List<Node> into)
    {
        Add(into, AttrGroups);
        Add(into, Params);
        Add(into, ReturnType);
        Add(into, Body);
    }
}

/// <summary>Any other expression: unary / cast / isset / instanceof / include / yield / ... Only the children matter.</summary>
internal sealed class GenericExpr : Expr
{
    public string Kind = "";
    public List<Node?> Kids = [];

    public override void Children(List<Node> into)
    {
        foreach (var k in Kids)
        {
            Add(into, k);
        }
    }
}

// ------------------------------------------------------------------ types

internal sealed class NullableType : Node
{
    public Node Type = null!;

    public override void Children(List<Node> into) => Add(into, Type);
}

internal sealed class UnionType : Node
{
    public List<Node> Types = [];

    public override void Children(List<Node> into) => Add(into, Types);
}

internal sealed class IntersectionType : Node
{
    public List<Node> Types = [];

    public override void Children(List<Node> into) => Add(into, Types);
}

// ------------------------------------------------------------------ statements

internal abstract class Stmt : Node;

internal sealed class InlineHtmlStmt : Stmt;

internal sealed class ExprStmt : Stmt
{
    public Expr Expr = null!;

    public override void Children(List<Node> into) => Add(into, Expr);
}

internal sealed class GenericStmt : Stmt
{
    public string Kind = "";
    public List<Node?> Kids = [];

    public override void Children(List<Node> into)
    {
        foreach (var k in Kids)
        {
            Add(into, k);
        }
    }
}

internal sealed class NamespaceStmt : Stmt
{
    public Name? Name;
    public List<Stmt> Stmts = [];

    public override void Children(List<Node> into)
    {
        Add(into, Name);
        Add(into, Stmts);
    }
}

internal sealed class UseItem : Node
{
    public Name Name = null!;
    public Ident? Alias;

    /// <summary>1 = class, 2 = function, 3 = const, 0 = inherited from the statement.</summary>
    public int Type;

    public string AliasName => Alias?.Name ?? Name.Text[(Name.Text.LastIndexOf('\\') + 1)..];

    public override void Children(List<Node> into)
    {
        Add(into, Name);
        Add(into, Alias);
    }
}

internal sealed class UseStmt : Stmt
{
    public int Type;
    public Name? Prefix;
    public List<UseItem> Uses = [];

    public override void Children(List<Node> into)
    {
        Add(into, Prefix);
        Add(into, Uses);
    }
}

internal sealed class AttrItem : Node
{
    public Name Name = null!;
    public List<Arg> Args = [];

    public override void Children(List<Node> into)
    {
        Add(into, Name);
        Add(into, Args);
    }
}

internal sealed class AttrGroup : Node
{
    public List<AttrItem> Attrs = [];

    public override void Children(List<Node> into) => Add(into, Attrs);
}

internal abstract class ClassLike : Stmt
{
    public Ident? Name;
    public Mod Flags;
    public List<AttrGroup> AttrGroups = [];
    public List<Stmt> Stmts = [];

    public IEnumerable<TraitUse> TraitUses => Stmts.OfType<TraitUse>();

    protected void BaseChildren(List<Node> into)
    {
        Add(into, AttrGroups);
        Add(into, Name);
    }
}

internal sealed class ClassStmt : ClassLike
{
    public Name? Extends;
    public List<Name> Implements = [];

    public bool IsAbstract => (Flags & Mod.Abstract) != 0;

    public bool IsFinal => (Flags & Mod.Final) != 0;

    public bool IsReadonly => (Flags & Mod.ReadOnly) != 0;

    /// <summary>Constructor arguments of an anonymous class.</summary>
    public List<Arg> CtorArgs = [];

    public override void Children(List<Node> into)
    {
        BaseChildren(into);
        Add(into, CtorArgs);
        Add(into, Extends);
        Add(into, Implements);
        Add(into, Stmts);
    }
}

internal sealed class InterfaceStmt : ClassLike
{
    public List<Name> Extends = [];

    public override void Children(List<Node> into)
    {
        BaseChildren(into);
        Add(into, Extends);
        Add(into, Stmts);
    }
}

internal sealed class TraitStmt : ClassLike
{
    public override void Children(List<Node> into)
    {
        BaseChildren(into);
        Add(into, Stmts);
    }
}

internal sealed class EnumStmt : ClassLike
{
    public Node? ScalarType;
    public List<Name> Implements = [];

    public override void Children(List<Node> into)
    {
        BaseChildren(into);
        Add(into, ScalarType);
        Add(into, Implements);
        Add(into, Stmts);
    }
}

internal sealed class TraitUse : Stmt
{
    public List<Name> Traits = [];
    public List<Node> Adaptations = [];

    public override void Children(List<Node> into)
    {
        Add(into, Traits);
        Add(into, Adaptations);
    }
}

internal sealed class ConstItem : Node
{
    public Ident Name = null!;
    public Expr Value = null!;

    /// <summary>Namespaced name for top-level <c>const X = ...</c> declarations.</summary>
    public string? NamespacedName;

    public override void Children(List<Node> into)
    {
        Add(into, Name);
        Add(into, Value);
    }
}

internal sealed class ClassConst : Stmt
{
    public Mod Flags;
    public Node? Type;
    public List<AttrGroup> AttrGroups = [];
    public List<ConstItem> Consts = [];

    public override void Children(List<Node> into)
    {
        Add(into, AttrGroups);
        Add(into, Type);
        Add(into, Consts);
    }
}

internal sealed class ConstStmt : Stmt
{
    public List<ConstItem> Consts = [];

    public override void Children(List<Node> into) => Add(into, Consts);
}

internal sealed class PropertyItem : Node
{
    public Ident Name = null!;
    public Expr? Default;

    public override void Children(List<Node> into)
    {
        Add(into, Name);
        Add(into, Default);
    }
}

internal sealed class PropertyStmt : Stmt
{
    public Mod Flags;
    public Node? Type;
    public List<AttrGroup> AttrGroups = [];
    public List<PropertyItem> Props = [];

    /// <summary>Statements of property hooks (PHP 8.4); only kept so that nested names are visited.</summary>
    public List<Node> Hooks = [];

    public override void Children(List<Node> into)
    {
        Add(into, AttrGroups);
        Add(into, Type);
        Add(into, Props);
        Add(into, Hooks);
    }
}

internal sealed class Param : Node
{
    public Mod Flags;
    public Node? Type;
    public bool ByRef;
    public bool Variadic;
    public List<AttrGroup> AttrGroups = [];
    public Variable Var = null!;
    public Expr? Default;
    public List<Node> Hooks = [];

    public override void Children(List<Node> into)
    {
        Add(into, AttrGroups);
        Add(into, Type);
        Add(into, Var);
        Add(into, Default);
        Add(into, Hooks);
    }
}

internal sealed class ClassMethod : Stmt, IFunctionLike
{
    public Mod Flags;
    public bool ByRef;
    public Ident Name = null!;
    public List<AttrGroup> AttrGroups = [];
    public List<Param> Params { get; set; } = [];
    public Node? ReturnType { get; set; }
    public List<Stmt>? Stmts;

    public override void Children(List<Node> into)
    {
        Add(into, AttrGroups);
        Add(into, Name);
        Add(into, Params);
        Add(into, ReturnType);
        if (Stmts != null)
        {
            Add(into, Stmts);
        }
    }
}

internal sealed class FunctionStmt : Stmt, IFunctionLike
{
    public bool ByRef;
    public Ident Name = null!;
    public string NamespacedName = "";
    public List<AttrGroup> AttrGroups = [];
    public List<Param> Params { get; set; } = [];
    public Node? ReturnType { get; set; }
    public List<Stmt> Stmts = [];

    public override void Children(List<Node> into)
    {
        Add(into, AttrGroups);
        Add(into, Name);
        Add(into, Params);
        Add(into, ReturnType);
        Add(into, Stmts);
    }
}

internal sealed class EnumCase : Stmt
{
    public Ident Name = null!;
    public Expr? Value;
    public List<AttrGroup> AttrGroups = [];

    public override void Children(List<Node> into)
    {
        Add(into, AttrGroups);
        Add(into, Name);
        Add(into, Value);
    }
}
