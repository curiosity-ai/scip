using System.Text;
using ScipSharp.Indexer.Python.Syntax;

namespace ScipSharp.Indexer.Python.Emit;

/// <summary>
/// Port of scip-python's <c>TypeStubExtendedWriter</c>: the signature block shown in hover documentation of functions and
/// classes (decorators, parameters one per line, inferred return type as a trailing comment).
/// </summary>
public static class HoverStubs
{
    public static string Function(FunctionNode node, string text)
    {
        var sb = new StringBuilder();
        sb.Append(Decorators(node.Decorators, text));
        sb.Append(node.IsAsync ? "async " : "").Append("def ").Append(node.Name.Value);
        var parms = node.Parameters.Select(p => Parameter(p, text)).ToList();
        if (parms.Count == 0)
        {
            sb.Append("()");
        }
        else
        {
            sb.Append("(\n  ").Append(string.Join(",\n  ", parms)).Append("\n)");
        }

        string? returnAnnotation = null;
        if (node.ReturnAnnotation is not null)
        {
            returnAnnotation = ExpressionPrinter.Print(node.ReturnAnnotation, text, forwardDeclarations: true);
        }
        else
        {
            returnAnnotation = node.Name.Value switch
            {
                "__init__" => "None",
                "__str__" => "str",
                "__int__" or "__hash__" => "int",
                "__eq__" or "__ne__" or "__gt__" or "__lt__" or "__ge__" or "__le__" => "bool",
                _ => null,
            };
        }

        if (returnAnnotation is not null)
        {
            sb.Append(" -> ").Append(returnAnnotation);
        }

        sb.Append(':');
        if (returnAnnotation is null && InferReturn(node, text) is { } inferred)
        {
            sb.Append(" # -> ").Append(inferred).Append(':');
        }

        return sb.ToString();
    }

    public static string Class(ClassNode cls, string text)
    {
        var sb = new StringBuilder();
        sb.Append(Decorators(cls.Decorators, text));
        sb.Append("class ").Append(cls.Name.Value);
        var args = cls.Arguments.Where(a =>
            a.Name is not null || a.Category != 0 || a.Value is not NameNode { Value: "object" }).ToList();
        if (args.Count > 0)
        {
            sb.Append('(').Append(string.Join(", ", args.Select(a =>
                (a.Name is not null ? a.Name.Value + "=" : "") + ExpressionPrinter.Print(a.Value, text)))).Append(')');
        }

        return sb.Append(':').ToString();
    }

    private static string Decorators(List<DecoratorNode> decorators, string text) =>
        string.Concat(decorators.Select(d => "@" + ExpressionPrinter.Print(d.Expression, text) + "\n"));

    private static string Parameter(ParameterNode p, string text)
    {
        var line = p.Category switch { ParamCategory.ArgsList => "*", ParamCategory.KwargsDict => "**", _ => "" };
        if (p.Name is not null)
        {
            line += p.Name.Value;
        }

        var type = p.Annotation is null ? "" : ExpressionPrinter.Print(p.Annotation, text, forwardDeclarations: true);
        if (type.Length > 0)
        {
            line += ": " + type;
        }

        if (p.Default is not null)
        {
            line += (type.Length > 0 ? " = " : "=") + ExpressionPrinter.Print(p.Default, text);
        }

        return line;
    }

    // ------------------------------------------------------------------ return type inference (the cheap, certain cases)

    private static string? InferReturn(FunctionNode node, string text)
    {
        var returns = new List<ReturnNode>();
        var hasYield = false;
        Scan(node.Suite, returns, ref hasYield);
        if (hasYield)
        {
            return null;
        }

        var endReachable = !AlwaysTerminates(node.Suite.Statements);
        var types = new List<string>();
        foreach (var r in returns)
        {
            if (r.Value is null or ConstantNode { Text: "None" })
            {
                types.Add("None");
            }
            else if (LiteralType(r.Value, text) is { } lit)
            {
                types.Add(lit);
            }
            else
            {
                return null;
            }
        }

        if (endReachable)
        {
            types.Add("None");
        }

        if (types.Count == 0)
        {
            return null; // NoReturn: pyright does not print Never
        }

        var distinct = types.Distinct().ToList();
        string result;
        if (distinct.Count == 1)
        {
            result = distinct[0];
        }
        else if (distinct.All(t => t.StartsWith("Literal[", StringComparison.Ordinal)))
        {
            result = "Literal[" + string.Join(", ", distinct.Select(t => t["Literal[".Length..^1])) + "]";
        }
        else
        {
            return null;
        }

        return node.IsAsync ? $"Coroutine[Any, Any, {result}]" : result;
    }

    private static string? LiteralType(Node value, string text)
    {
        switch (value)
        {
            case StringListNode { Strings: [{ IsFormat: false } s] } when !IsBytes(s, text):
                return "Literal['" + s.Value.Replace("\\", "\\\\").Replace("'", "\\'") + "']";
            case NumberNode n:
            {
                var src = text[Math.Clamp(n.Start, 0, text.Length)..Math.Clamp(n.End, 0, text.Length)];
                return long.TryParse(src, out var l) ? $"Literal[{l}]" : null;
            }

            case SequenceNode { Kind: NodeKind.Tuple } t when t.Items.Count > 0:
            {
                var parts = new List<string>();
                foreach (var item in t.Items)
                {
                    var p = LiteralType(item, text);
                    if (p is null)
                    {
                        return null;
                    }

                    parts.Add(p);
                }

                return "tuple[" + string.Join(", ", parts) + "]";
            }
        }

        return null;
    }

    private static bool IsBytes(StringNode s, string text)
    {
        var src = text[Math.Clamp(s.Start, 0, text.Length)..Math.Clamp(s.End, 0, text.Length)];
        return src.Length > 0 && char.IsLetter(src[0]) && src.TakeWhile(char.IsLetter).Any(c => c is 'b' or 'B');
    }

    private static void Scan(Node node, List<ReturnNode> returns, ref bool hasYield)
    {
        foreach (var child in node.Children())
        {
            switch (child)
            {
                case null:
                    break;
                case FunctionNode or ClassNode or LambdaNode:
                    break;
                case ReturnNode r:
                    returns.Add(r);
                    Scan(r, returns, ref hasYield);
                    break;
                case WrapperNode { Kind: NodeKind.Yield or NodeKind.YieldFrom }:
                    hasYield = true;
                    break;
                default:
                    Scan(child, returns, ref hasYield);
                    break;
            }
        }
    }

    private static bool AlwaysTerminates(IReadOnlyList<Node> statements)
    {
        foreach (var s in statements)
        {
            switch (s)
            {
                case ReturnNode or RaiseNode:
                    return true;
                case IfNode { ElseSuite: { } e } i when AlwaysTerminates(i.IfSuite.Statements) && ElseTerminates(e):
                    return true;
                case WhileNode { Test: ConstantNode { Text: "True" } }:
                    return true;
                case WithNode w when AlwaysTerminates(w.Suite.Statements):
                    return true;
                case TryNode t when t.FinallySuite is not null && AlwaysTerminates(t.FinallySuite.Statements):
                    return true;
                case TryNode t2 when AlwaysTerminates(t2.TrySuite.Statements) && t2.Excepts.All(e => AlwaysTerminates(e.Suite.Statements)):
                    return true;
            }
        }

        return false;
    }

    private static bool ElseTerminates(Node e) => e switch
    {
        SuiteNode s => AlwaysTerminates(s.Statements),
        IfNode { ElseSuite: { } e2 } i => AlwaysTerminates(i.IfSuite.Statements) && ElseTerminates(e2),
        _ => false,
    };
}
