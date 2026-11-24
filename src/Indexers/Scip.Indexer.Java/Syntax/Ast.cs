namespace Scip.Indexer.Java.Syntax;

public abstract class Node
{
    public int Start { get; set; }

    public int End { get; set; }
}

[Flags]
public enum Mod
{
    None = 0, Public = 1, Private = 2, Protected = 4, Static = 8, Final = 16, Abstract = 32, Default = 64,
    Sealed = 128, Native = 256, Synchronized = 512, Transient = 1024, Volatile = 2048, Strictfp = 4096, NonSealed = 8192,
}

public sealed class Modifiers : Node
{
    public Mod Flags { get; set; }

    public List<Annotation> Annotations { get; } = [];

    /// <summary>Offset of the first modifier or annotation, or -1 when there is none.</summary>
    public int First { get; set; } = -1;

    public List<string> Keywords { get; } = [];
}

public sealed class Annotation : Node
{
    public TypeRef Name { get; set; } = null!;

    public List<(string? Name, int NameStart, Expr Value)> Args { get; } = [];
}

public sealed class TypeSeg
{
    public string Name { get; set; } = "";
    public int Start { get; set; }
    public int End { get; set; }
    public List<TypeRef> Args { get; } = [];
    public bool Diamond { get; set; }
}

public enum WildKind { None, Unbounded, Extends, Super }

/// <summary>A (possibly qualified, parameterised, array) type as written in source.</summary>
public sealed class TypeRef : Node
{
    public List<TypeSeg> Segs { get; } = [];
    public int Dims { get; set; }
    public WildKind Wild { get; set; }
    public TypeRef? Bound { get; set; }
    public List<TypeRef> Intersection { get; } = [];
    public bool IsPrimitive { get; set; }
    public List<Annotation> Annotations { get; } = [];
    public string Text { get; set; } = "";
    public string SimpleName => Segs.Count > 0 ? Segs[^1].Name : "";
}

public sealed class TypeParam : Node
{
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
    public List<Annotation> Annotations { get; } = [];
    public List<TypeRef> Bounds { get; } = [];
}

public enum TypeKind { Class, Interface, Enum, Annotation, Record }

public abstract class Member : Node
{
    public Modifiers Mods { get; set; } = new();
    public string? Doc { get; set; }
}

public sealed class TypeDecl : Member
{
    public TypeKind Kind { get; set; }
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
    public List<TypeParam> TypeParams { get; } = [];
    public List<TypeRef> Extends { get; } = [];
    public List<TypeRef> Implements { get; } = [];
    public List<Param> RecordComponents { get; } = [];
    public List<EnumConstant> EnumConstants { get; } = [];
    public List<Member> Members { get; } = [];
    public bool IsLocalOrAnon { get; set; }
}

public sealed class EnumConstant : Member
{
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
    public List<Expr> Args { get; } = [];
    public List<Member>? Body { get; set; }
}

public sealed class FieldDecl : Member
{
    public TypeRef Type { get; set; } = null!;
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
    public int ExtraDims { get; set; }
    public Expr? Init { get; set; }
}

public sealed class Param : Node
{
    public Modifiers Mods { get; set; } = new();
    public TypeRef? Type { get; set; }
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
    public bool Varargs { get; set; }
}

public sealed class MethodDecl : Member
{
    public List<TypeParam> TypeParams { get; } = [];
    public TypeRef? Ret { get; set; }
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
    public List<Param> Params { get; } = [];
    public List<TypeRef> Throws { get; } = [];
    public Block? Body { get; set; }
    public bool IsCtor { get; set; }
    public Expr? DefaultValue { get; set; }
    public bool CompactCtor { get; set; }
}

public sealed class InitBlock : Member
{
    public bool IsStatic { get; set; }
    public Block Body { get; set; } = null!;
}

public sealed class Import : Node
{
    public bool IsStatic { get; set; }
    public bool Wildcard { get; set; }
    public List<(string Name, int Start, int End)> Parts { get; } = [];
}

public sealed class CompilationUnit : Node
{
    public List<(string Name, int Start, int End)> Package { get; } = [];
    public List<Import> Imports { get; } = [];
    public List<TypeDecl> Types { get; } = [];
    public string Source { get; set; } = "";
}

// ---------------------------------------------------------------- statements

public abstract class Stmt : Node;

public sealed class Block : Stmt
{
    public List<Stmt> Stmts { get; } = [];
}

public sealed class VarDeclStmt : Stmt
{
    public Modifiers Mods { get; set; } = new();
    public TypeRef? Type { get; set; }
    public List<Declarator> Vars { get; } = [];
}

public sealed class Declarator : Node
{
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
    public int ExtraDims { get; set; }
    public Expr? Init { get; set; }
}

public sealed class ExprStmt : Stmt { public Expr E { get; set; } = null!; }

public sealed class IfStmt : Stmt
{
    public Expr Cond { get; set; } = null!;
    public Stmt Then { get; set; } = null!;
    public Stmt? Else { get; set; }
}

public sealed class WhileStmt : Stmt
{
    public Expr Cond { get; set; } = null!;
    public Stmt Body { get; set; } = null!;
    public bool IsDo { get; set; }
}

public sealed class ForStmt : Stmt
{
    public List<Stmt> Init { get; } = [];
    public Expr? Cond { get; set; }
    public List<Expr> Update { get; } = [];
    public Stmt Body { get; set; } = null!;
}

public sealed class ForEachStmt : Stmt
{
    public VarDeclStmt Var { get; set; } = null!;
    public Expr Iterable { get; set; } = null!;
    public Stmt Body { get; set; } = null!;
}

public sealed class ReturnStmt : Stmt { public Expr? E { get; set; } }

public sealed class ThrowStmt : Stmt { public Expr E { get; set; } = null!; }

public sealed class TryStmt : Stmt
{
    public List<Stmt> Resources { get; } = [];
    public Block Body { get; set; } = null!;
    public List<CatchClause> Catches { get; } = [];
    public Block? Finally { get; set; }
}

public sealed class CatchClause : Node
{
    public Modifiers Mods { get; set; } = new();
    public List<TypeRef> Types { get; } = [];
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
    public Block Body { get; set; } = null!;
}

public sealed class SwitchStmt : Stmt
{
    public Expr Selector { get; set; } = null!;
    public List<SwitchCase> Cases { get; } = [];
}

public sealed class SwitchCase : Node
{
    public List<Expr> Labels { get; } = [];
    public List<Pattern> Patterns { get; } = [];
    public bool IsDefault { get; set; }
    public Expr? Guard { get; set; }
    public List<Stmt> Body { get; } = [];
}

public sealed class SyncStmt : Stmt
{
    public Expr Lock { get; set; } = null!;
    public Block Body { get; set; } = null!;
}

public sealed class LabeledStmt : Stmt { public Stmt Body { get; set; } = null!; }

public sealed class LocalClassStmt : Stmt { public TypeDecl Decl { get; set; } = null!; }

public sealed class SimpleStmt : Stmt { public Expr? E { get; set; } }

// ---------------------------------------------------------------- expressions

public abstract class Expr : Node;

public sealed class Literal : Expr { public string Text { get; set; } = ""; public Tk Kind { get; set; } }

public sealed class NameExpr : Expr { public string Name { get; set; } = ""; }

public sealed class FieldAccess : Expr
{
    public Expr Target { get; set; } = null!;
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
}

public sealed class Call : Expr
{
    public Expr? Target { get; set; }
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
    public List<Expr> Args { get; } = [];
}

public sealed class NewExpr : Expr
{
    public Expr? Outer { get; set; }
    public TypeRef Type { get; set; } = null!;
    public List<Expr> Args { get; } = [];
    public TypeDecl? Body { get; set; }
}

public sealed class NewArray : Expr
{
    public TypeRef Elem { get; set; } = null!;
    public List<Expr> Dims { get; } = [];
    public int ExtraDims { get; set; }
    public ArrayInit? Init { get; set; }
}

public sealed class ArrayInit : Expr { public List<Expr> Items { get; } = []; }

public sealed class Binary : Expr
{
    public string Op { get; set; } = "";
    public Expr L { get; set; } = null!;
    public Expr R { get; set; } = null!;
}

public sealed class Unary : Expr
{
    public string Op { get; set; } = "";
    public Expr E { get; set; } = null!;
    public bool Postfix { get; set; }
}

public sealed class Assign : Expr
{
    public string Op { get; set; } = "";
    public Expr L { get; set; } = null!;
    public Expr R { get; set; } = null!;
}

public sealed class Conditional : Expr
{
    public Expr Cond { get; set; } = null!;
    public Expr A { get; set; } = null!;
    public Expr B { get; set; } = null!;
}

public sealed class Cast : Expr
{
    public TypeRef Type { get; set; } = null!;
    public Expr E { get; set; } = null!;
}

public sealed class Paren : Expr { public Expr E { get; set; } = null!; }

public sealed class IndexExpr : Expr
{
    public Expr Target { get; set; } = null!;
    public Expr Index { get; set; } = null!;
}

/// <summary>A type pattern (<c>String s</c>) or record pattern (<c>Point(int x, int y)</c>).</summary>
public sealed class Pattern : Node
{
    public Modifiers Mods { get; set; } = new();
    public TypeRef Type { get; set; } = null!;
    public string? Name { get; set; }
    public int NameStart { get; set; }
    public List<Pattern>? Sub { get; set; }
}

public sealed class InstanceOf : Expr
{
    public Expr E { get; set; } = null!;
    public TypeRef? Type { get; set; }
    public Pattern? Pattern { get; set; }
}

public sealed class Lambda : Expr
{
    public List<Param> Params { get; } = [];
    public Expr? BodyExpr { get; set; }
    public Block? BodyBlock { get; set; }
}

public sealed class MethodRef : Expr
{
    public Expr? Target { get; set; }
    public TypeRef? TargetType { get; set; }
    public string Name { get; set; } = "";
    public int NameStart { get; set; }
}

public sealed class ClassLit : Expr { public TypeRef Type { get; set; } = null!; }

public sealed class SwitchExpr : Expr
{
    public SwitchStmt Switch { get; set; } = null!;
}
