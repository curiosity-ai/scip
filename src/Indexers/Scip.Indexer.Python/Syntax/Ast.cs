namespace Scip.Indexer.Python.Syntax;

public enum NodeKind
{
    Module, Suite, If, While, For, Try, Except, With, WithItem, Function, Parameter, Class, Decorator,
    Return, Raise, Assert, Del, Global, Nonlocal, Import, ImportAs, ImportFrom, ImportFromAs, ModuleName,
    Assignment, AugAssignment, TypeAnnotation, Name, Number, StringList, String, Constant, MemberAccess,
    Call, Argument, Index, Slice, Unary, Binary, Ternary, Lambda, Tuple, List, Set, Dictionary, DictKeyEntry,
    DictExpandEntry, Comprehension, ComprehensionFor, ComprehensionIf, Await, Yield, YieldFrom, Unpack,
    AssignmentExpression, Match, Case, Error,
}

/// <summary>
/// Syntax tree node. Shapes deliberately mirror pyright's parse nodes (<c>parseNodes.ts</c>) because the SCIP
/// symbol-naming logic of scip-python is defined in terms of parent chains of those nodes.
/// </summary>
public abstract class Node
{
    private static int s_nextId;

    public int Id { get; } = Interlocked.Increment(ref s_nextId);

    public int Start { get; set; }

    public int End { get; set; }

    public Node? Parent { get; set; }

    public abstract NodeKind Kind { get; }

    /// <summary>Children in pyright's <c>ParseTreeWalker</c> order.</summary>
    public abstract IEnumerable<Node?> Children();

    public int Length => End - Start;

    protected static IEnumerable<Node?> Seq(params Node?[] nodes) => nodes;
}

public sealed class ModuleNode : Node
{
    /// <summary>Number of statements the parser had to skip because of syntax errors.</summary>
    public int SyntaxErrors { get; set; }

    /// <summary>Offsets at which the parser reported an error (diagnostics).</summary>
    public List<(int Offset, string Message)> ErrorList { get; } = [];

    public List<Node> Statements { get; } = [];

    public override NodeKind Kind => NodeKind.Module;

    public override IEnumerable<Node?> Children() => Statements;
}

public sealed class SuiteNode : Node
{
    public List<Node> Statements { get; } = [];

    public override NodeKind Kind => NodeKind.Suite;

    public override IEnumerable<Node?> Children() => Statements;
}

public sealed class IfNode : Node
{
    public Node Test { get; set; } = null!;

    public SuiteNode IfSuite { get; set; } = null!;

    /// <summary>Either a <see cref="SuiteNode"/> (else) or an <see cref="IfNode"/> (elif).</summary>
    public Node? ElseSuite { get; set; }

    public override NodeKind Kind => NodeKind.If;

    public override IEnumerable<Node?> Children() => Seq(Test, IfSuite, ElseSuite);
}

public sealed class WhileNode : Node
{
    public Node Test { get; set; } = null!;

    public SuiteNode Suite { get; set; } = null!;

    public SuiteNode? ElseSuite { get; set; }

    public override NodeKind Kind => NodeKind.While;

    public override IEnumerable<Node?> Children() => Seq(Test, Suite, ElseSuite);
}

public sealed class ForNode : Node
{
    public Node Target { get; set; } = null!;

    public Node Iterable { get; set; } = null!;

    public SuiteNode Suite { get; set; } = null!;

    public SuiteNode? ElseSuite { get; set; }

    public override NodeKind Kind => NodeKind.For;

    public override IEnumerable<Node?> Children() => Seq(Target, Iterable, Suite, ElseSuite);
}

public sealed class TryNode : Node
{
    public SuiteNode TrySuite { get; set; } = null!;

    public List<ExceptNode> Excepts { get; } = [];

    public SuiteNode? ElseSuite { get; set; }

    public SuiteNode? FinallySuite { get; set; }

    public override NodeKind Kind => NodeKind.Try;

    public override IEnumerable<Node?> Children() => Seq(TrySuite).Concat(Excepts).Append(ElseSuite).Append(FinallySuite);
}

public sealed class ExceptNode : Node
{
    public Node? TypeExpression { get; set; }

    public NameNode? Name { get; set; }

    public SuiteNode Suite { get; set; } = null!;

    public override NodeKind Kind => NodeKind.Except;

    public override IEnumerable<Node?> Children() => Seq(TypeExpression, Name, Suite);
}

public sealed class WithNode : Node
{
    public List<WithItemNode> Items { get; } = [];

    public SuiteNode Suite { get; set; } = null!;

    public override NodeKind Kind => NodeKind.With;

    public override IEnumerable<Node?> Children() => Items.Cast<Node?>().Append(Suite);
}

public sealed class WithItemNode : Node
{
    public Node Expression { get; set; } = null!;

    public Node? Target { get; set; }

    public override NodeKind Kind => NodeKind.WithItem;

    public override IEnumerable<Node?> Children() => Seq(Expression, Target);
}

public enum ParamCategory { Simple, ArgsList, KwargsDict }

public sealed class ParameterNode : Node
{
    public ParamCategory Category { get; set; }

    public NameNode? Name { get; set; }

    public Node? Annotation { get; set; }

    public Node? Default { get; set; }

    public override NodeKind Kind => NodeKind.Parameter;

    public override IEnumerable<Node?> Children() => Seq(Name, Annotation, Default);
}

public sealed class FunctionNode : Node
{
    public List<DecoratorNode> Decorators { get; } = [];

    public NameNode Name { get; set; } = null!;

    public List<ParameterNode> Parameters { get; } = [];

    public Node? ReturnAnnotation { get; set; }

    public SuiteNode Suite { get; set; } = null!;

    public bool IsAsync { get; set; }

    public override NodeKind Kind => NodeKind.Function;

    public override IEnumerable<Node?> Children() =>
        Decorators.Cast<Node?>().Append(Name).Concat(Parameters).Append(ReturnAnnotation).Append(Suite);
}

public sealed class ClassNode : Node
{
    public List<DecoratorNode> Decorators { get; } = [];

    public NameNode Name { get; set; } = null!;

    public List<ArgumentNode> Arguments { get; } = [];

    public SuiteNode Suite { get; set; } = null!;

    public override NodeKind Kind => NodeKind.Class;

    public override IEnumerable<Node?> Children() => Decorators.Cast<Node?>().Append(Name).Concat(Arguments).Append(Suite);
}

public sealed class DecoratorNode : Node
{
    public Node Expression { get; set; } = null!;

    public override NodeKind Kind => NodeKind.Decorator;

    public override IEnumerable<Node?> Children() => Seq(Expression);
}

public sealed class ReturnNode : Node
{
    public Node? Value { get; set; }

    public override NodeKind Kind => NodeKind.Return;

    public override IEnumerable<Node?> Children() => Seq(Value);
}

public sealed class RaiseNode : Node
{
    public Node? Exception { get; set; }

    public Node? From { get; set; }

    public override NodeKind Kind => NodeKind.Raise;

    public override IEnumerable<Node?> Children() => Seq(Exception, From);
}

public sealed class AssertNode : Node
{
    public Node Test { get; set; } = null!;

    public Node? Message { get; set; }

    public override NodeKind Kind => NodeKind.Assert;

    public override IEnumerable<Node?> Children() => Seq(Test, Message);
}

public sealed class DelNode : Node
{
    public List<Node> Targets { get; } = [];

    public override NodeKind Kind => NodeKind.Del;

    public override IEnumerable<Node?> Children() => Targets;
}

public sealed class GlobalNode : Node
{
    public List<NameNode> Names { get; } = [];

    public bool IsNonlocal { get; set; }

    public override NodeKind Kind => IsNonlocal ? NodeKind.Nonlocal : NodeKind.Global;

    public override IEnumerable<Node?> Children() => Names;
}

public sealed class ImportNode : Node
{
    public List<ImportAsNode> List { get; } = [];

    public override NodeKind Kind => NodeKind.Import;

    public override IEnumerable<Node?> Children() => List;
}

public sealed class ModuleNameNode : Node
{
    public int LeadingDots { get; set; }

    public List<NameNode> NameParts { get; } = [];

    public override NodeKind Kind => NodeKind.ModuleName;

    public override IEnumerable<Node?> Children() => NameParts;

    /// <summary>The dotted name as written, including leading dots.</summary>
    public string Formatted => new string('.', LeadingDots) + string.Join('.', NameParts.Select(p => p.Value));
}

public sealed class ImportAsNode : Node
{
    public ModuleNameNode Module { get; set; } = null!;

    public NameNode? Alias { get; set; }

    public override NodeKind Kind => NodeKind.ImportAs;

    public override IEnumerable<Node?> Children() => Seq(Module, Alias);
}

public sealed class ImportFromNode : Node
{
    public ModuleNameNode Module { get; set; } = null!;

    public List<ImportFromAsNode> Imports { get; } = [];

    public bool IsWildcard { get; set; }

    public override NodeKind Kind => NodeKind.ImportFrom;

    public override IEnumerable<Node?> Children() => Seq(Module).Concat(Imports);
}

public sealed class ImportFromAsNode : Node
{
    public NameNode Name { get; set; } = null!;

    public NameNode? Alias { get; set; }

    public override NodeKind Kind => NodeKind.ImportFromAs;

    public override IEnumerable<Node?> Children() => Seq(Name, Alias);
}

/// <summary>Plain assignment. Chained assignments (<c>a = b = c</c>) carry several targets.</summary>
public sealed class AssignmentNode : Node
{
    public List<Node> Targets { get; } = [];

    public Node Value { get; set; } = null!;

    public override NodeKind Kind => NodeKind.Assignment;

    public override IEnumerable<Node?> Children() => Targets.Cast<Node?>().Append(Value);
}

public sealed class AugAssignmentNode : Node
{
    public Node Target { get; set; } = null!;

    public Node Value { get; set; } = null!;

    public override NodeKind Kind => NodeKind.AugAssignment;

    public override IEnumerable<Node?> Children() => Seq(Target, Value);
}

public sealed class TypeAnnotationNode : Node
{
    public Node ValueExpression { get; set; } = null!;

    public Node Annotation { get; set; } = null!;

    public override NodeKind Kind => NodeKind.TypeAnnotation;

    public override IEnumerable<Node?> Children() => Seq(ValueExpression, Annotation);
}

public sealed class NameNode : Node
{
    public string Value { get; set; } = "";

    public override NodeKind Kind => NodeKind.Name;

    public override IEnumerable<Node?> Children() => [];
}

public sealed class NumberNode : Node
{
    public override NodeKind Kind => NodeKind.Number;

    public override IEnumerable<Node?> Children() => [];
}

public sealed class ConstantNode : Node
{
    public string Text { get; set; } = "";

    public override NodeKind Kind => NodeKind.Constant;

    public override IEnumerable<Node?> Children() => [];
}

public sealed class StringListNode : Node
{
    public List<StringNode> Strings { get; } = [];

    public override NodeKind Kind => NodeKind.StringList;

    public override IEnumerable<Node?> Children() => Strings;
}

public sealed class StringNode : Node
{
    public string Value { get; set; } = "";

    public bool IsFormat { get; set; }

    /// <summary>Expressions inside f-string replacement fields (including format-spec fields).</summary>
    public List<Node> Expressions { get; } = [];

    public override NodeKind Kind => NodeKind.String;

    public override IEnumerable<Node?> Children() => Expressions;
}

public sealed class MemberAccessNode : Node
{
    public Node Left { get; set; } = null!;

    public NameNode Member { get; set; } = null!;

    public override NodeKind Kind => NodeKind.MemberAccess;

    public override IEnumerable<Node?> Children() => Seq(Left, Member);
}

public sealed class CallNode : Node
{
    public Node Left { get; set; } = null!;

    public List<ArgumentNode> Arguments { get; } = [];

    public override NodeKind Kind => NodeKind.Call;

    public override IEnumerable<Node?> Children() => Seq(Left).Concat(Arguments);
}

public sealed class ArgumentNode : Node
{
    /// <summary>0 = positional, 1 = <c>*args</c>, 2 = <c>**kwargs</c>.</summary>
    public int Category { get; set; }

    public NameNode? Name { get; set; }

    public Node Value { get; set; } = null!;

    public override NodeKind Kind => NodeKind.Argument;

    public override IEnumerable<Node?> Children() => Seq(Name, Value);
}

public sealed class IndexNode : Node
{
    public Node Left { get; set; } = null!;

    public List<ArgumentNode> Items { get; } = [];

    public override NodeKind Kind => NodeKind.Index;

    public override IEnumerable<Node?> Children() => Seq(Left).Concat(Items);
}

public sealed class SliceNode : Node
{
    public Node? StartValue { get; set; }

    public Node? EndValue { get; set; }

    public Node? Step { get; set; }

    public override NodeKind Kind => NodeKind.Slice;

    public override IEnumerable<Node?> Children() => Seq(StartValue, EndValue, Step);
}

public sealed class UnaryNode : Node
{
    public Node Operand { get; set; } = null!;

    public override NodeKind Kind => NodeKind.Unary;

    public override IEnumerable<Node?> Children() => Seq(Operand);
}

public sealed class BinaryNode : Node
{
    public Node Left { get; set; } = null!;

    public Node Right { get; set; } = null!;

    public string Operator { get; set; } = "";

    public override NodeKind Kind => NodeKind.Binary;

    public override IEnumerable<Node?> Children() => Seq(Left, Right);
}

public sealed class TernaryNode : Node
{
    public Node IfExpression { get; set; } = null!;

    public Node Test { get; set; } = null!;

    public Node ElseExpression { get; set; } = null!;

    public override NodeKind Kind => NodeKind.Ternary;

    public override IEnumerable<Node?> Children() => Seq(IfExpression, Test, ElseExpression);
}

public sealed class LambdaNode : Node
{
    public List<ParameterNode> Parameters { get; } = [];

    public Node Body { get; set; } = null!;

    public override NodeKind Kind => NodeKind.Lambda;

    public override IEnumerable<Node?> Children() => Parameters.Cast<Node?>().Append(Body);
}

public sealed class SequenceNode(NodeKind kind) : Node
{
    public List<Node> Items { get; } = [];

    public bool Parenthesized { get; set; }

    public override NodeKind Kind => kind;

    public override IEnumerable<Node?> Children() => Items;
}

public sealed class DictionaryNode : Node
{
    public List<Node> Entries { get; } = [];

    public override NodeKind Kind => NodeKind.Dictionary;

    public override IEnumerable<Node?> Children() => Entries;
}

public sealed class DictKeyEntryNode : Node
{
    public Node Key { get; set; } = null!;

    public Node Value { get; set; } = null!;

    public override NodeKind Kind => NodeKind.DictKeyEntry;

    public override IEnumerable<Node?> Children() => Seq(Key, Value);
}

public sealed class DictExpandEntryNode : Node
{
    public Node Expression { get; set; } = null!;

    public override NodeKind Kind => NodeKind.DictExpandEntry;

    public override IEnumerable<Node?> Children() => Seq(Expression);
}

/// <summary>A list/set/dict comprehension or generator expression.</summary>
public sealed class ComprehensionNode : Node
{
    public Node Expression { get; set; } = null!;

    /// <summary><see cref="ComprehensionForNode"/> and <see cref="ComprehensionIfNode"/> clauses, in source order.</summary>
    public List<Node> Clauses { get; } = [];

    public override NodeKind Kind => NodeKind.Comprehension;

    public override IEnumerable<Node?> Children() => Seq(Expression).Concat(Clauses);
}

public sealed class ComprehensionForNode : Node
{
    public Node Target { get; set; } = null!;

    public Node Iterable { get; set; } = null!;

    public override NodeKind Kind => NodeKind.ComprehensionFor;

    public override IEnumerable<Node?> Children() => Seq(Target, Iterable);
}

public sealed class ComprehensionIfNode : Node
{
    public Node Test { get; set; } = null!;

    public override NodeKind Kind => NodeKind.ComprehensionIf;

    public override IEnumerable<Node?> Children() => Seq(Test);
}

/// <summary>Await, yield, yield from and <c>*x</c> unpack, all of which just wrap one optional expression.</summary>
public sealed class WrapperNode(NodeKind kind) : Node
{
    public Node? Expression { get; set; }

    public override NodeKind Kind => kind;

    public override IEnumerable<Node?> Children() => Seq(Expression);
}

public sealed class AssignmentExpressionNode : Node
{
    public NameNode Name { get; set; } = null!;

    public Node Right { get; set; } = null!;

    public override NodeKind Kind => NodeKind.AssignmentExpression;

    public override IEnumerable<Node?> Children() => Seq(Name, Right);
}

public sealed class MatchNode : Node
{
    public Node Subject { get; set; } = null!;

    public List<CaseNode> Cases { get; } = [];

    public override NodeKind Kind => NodeKind.Match;

    public override IEnumerable<Node?> Children() => Seq(Subject).Concat(Cases);
}

public sealed class CaseNode : Node
{
    /// <summary>The pattern, parsed with the expression grammar (bare names are captures).</summary>
    public Node Pattern { get; set; } = null!;

    public NameNode? AsName { get; set; }

    public Node? Guard { get; set; }

    public SuiteNode Suite { get; set; } = null!;

    public override NodeKind Kind => NodeKind.Case;

    public override IEnumerable<Node?> Children() => Seq(Pattern, AsName, Guard, Suite);
}

public sealed class ErrorNode : Node
{
    public override NodeKind Kind => NodeKind.Error;

    public override IEnumerable<Node?> Children() => [];
}
