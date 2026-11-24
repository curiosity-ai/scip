using System.Text;
using Scip.Indexer.Python.Syntax;

namespace Scip.Indexer.Python.Emit;

/// <summary>
/// Port of pyright's <c>ParseTreeUtils.printExpression</c> (as used by <c>TypeStubWriter._printExpression</c> with
/// <c>DoNotLimitStringLength</c>): expressions are re-printed from the tree, not copied from the source, so
/// <c>str|None</c> becomes <c>str | None</c> and dictionaries become <c>{ "a": 1 }</c>.
/// </summary>
public static class ExpressionPrinter
{
    public static string Print(Node node, string text, bool forwardDeclarations = false) =>
        new Printer(text, forwardDeclarations).Print(node);

    private sealed class Printer(string text, bool forwardDeclarations)
    {
        private string Source(Node n)
        {
            var start = Math.Clamp(n.Start, 0, text.Length);
            var end = Math.Clamp(n.End, start, text.Length);
            return text[start..end];
        }

        public string Print(Node node)
        {
            switch (node)
            {
                case NameNode n:
                    return n.Value;
                case MemberAccessNode m:
                    return Print(m.Left) + "." + m.Member.Value;
                case CallNode c:
                    return Print(c.Left) + "(" + string.Join(", ", c.Arguments.Select(PrintArgument)) + ")";
                case IndexNode i:
                    return Print(i.Left) + "[" + string.Join(", ", i.Items.Select(PrintArgument)) +
                           "]";
                case UnaryNode u:
                    return UnaryOperator(u) + Print(u.Operand);
                case BinaryNode b:
                {
                    var s = Print(b.Left) + " " + b.Operator + " " + Print(b.Right);
                    return IsParenthesized(b) ? "(" + s + ")" : s;
                }

                case NumberNode:
                    return Source(node);
                case StringListNode sl:
                    if (forwardDeclarations && sl.Strings.Count == 1 && !sl.Strings[0].IsFormat && IsPlainQuoted(sl.Strings[0]))
                    {
                        return sl.Strings[0].Value;
                    }

                    return string.Join(' ', sl.Strings.Select(PrintString));
                case StringNode s2:
                    return PrintString(s2);
                case AssignmentExpressionNode ae:
                    return ae.Name.Value + " := " + Print(ae.Right);
                case TypeAnnotationNode ta:
                    return Print(ta.ValueExpression) + ": " + Print(ta.Annotation);
                case WrapperNode { Kind: NodeKind.Await } w:
                    return "await " + Print(w.Expression!);
                case TernaryNode t:
                    return Print(t.IfExpression) + " if " + Print(t.Test) + " else " + Print(t.ElseExpression);
                case SequenceNode { Kind: NodeKind.List } l:
                    return "[" + string.Join(", ", l.Items.Select(Print)) + "]";
                case WrapperNode { Kind: NodeKind.Unpack } w:
                    return "*" + Print(w.Expression!);
                case SequenceNode { Kind: NodeKind.Tuple } tu:
                {
                    var items = tu.Items.Select(Print).ToList();
                    return items.Count == 1 ? "(" + items[0] + ", )" : "(" + string.Join(", ", items) + ")";
                }

                case WrapperNode { Kind: NodeKind.Yield } y:
                    return y.Expression is null ? "yield" : "yield " + Print(y.Expression);
                case WrapperNode { Kind: NodeKind.YieldFrom } yf:
                    return "yield from " + Print(yf.Expression!);
                case ConstantNode k:
                    return k.Text == "..." ? "..." : k.Text;
                case ComprehensionNode comp:
                {
                    var sb = new StringBuilder(Print(comp.Expression));
                    foreach (var clause in comp.Clauses)
                    {
                        sb.Append(' ');
                        switch (clause)
                        {
                            case ComprehensionForNode f:
                                sb.Append(Source(f).StartsWith("async", StringComparison.Ordinal) ? "async " : "")
                                    .Append("for ").Append(Print(f.Target)).Append(" in ").Append(Print(f.Iterable));
                                break;
                            case ComprehensionIfNode f:
                                sb.Append("if ").Append(Print(f.Test));
                                break;
                        }
                    }

                    var open = comp.Start < text.Length ? text[comp.Start] : '\0';
                    return open switch
                    {
                        '[' => "[" + sb + "]",
                        '(' => "(" + sb + "})",
                        _ => sb.ToString(),
                    };
                }

                case SliceNode sl2:
                {
                    var r = "";
                    if (sl2.StartValue is not null || sl2.EndValue is not null || sl2.Step is not null)
                    {
                        if (sl2.StartValue is not null)
                        {
                            r += Print(sl2.StartValue);
                        }

                        if (sl2.EndValue is not null)
                        {
                            r += ": " + Print(sl2.EndValue);
                        }

                        if (sl2.Step is not null)
                        {
                            r += ": " + Print(sl2.Step);
                        }
                    }
                    else
                    {
                        r = ":";
                    }

                    return r;
                }

                case LambdaNode lam:
                    return "lambda " + string.Join(", ", lam.Parameters.Select(p =>
                    {
                        var ps = p.Category switch { ParamCategory.ArgsList => "*", ParamCategory.KwargsDict => "**", _ => "" };
                        ps += p.Name is not null ? p.Name.Value : p.Category == ParamCategory.Simple ? "/" : "";
                        return p.Default is null ? ps : ps + " = " + Print(p.Default);
                    })) + ": " + Print(lam.Body);
                case DictionaryNode d:
                {
                    var contents = string.Join(",", d.Entries.Select(e => e switch
                    {
                        DictKeyEntryNode ke => Print(ke.Key) + ": " + Print(ke.Value),
                        DictExpandEntryNode ex => "**" + Print(ex.Expression),
                        _ => Print(e),
                    }));
                    return contents.Length > 0 ? "{ " + contents + " }" : "{}";
                }

                case SequenceNode { Kind: NodeKind.Set } set:
                    return string.Join(", ", set.Items.Select(Print));
            }

            return "<Expression>";
        }

        private bool IsParenthesized(BinaryNode b)
        {
            var before = b.Start - 1;
            while (before >= 0 && char.IsWhiteSpace(text[before]))
            {
                before--;
            }

            var after = b.End;
            while (after < text.Length && char.IsWhiteSpace(text[after]))
            {
                after++;
            }

            return before >= 0 && after < text.Length && text[before] == '(' && text[after] == ')' &&
                   b.Parent is not (CallNode or ArgumentNode or DecoratorNode);
        }

        private string PrintArgument(ArgumentNode a)
        {
            var s = a.Category switch { 1 => "*", 2 => "**", _ => "" };
            if (a.Name is not null)
            {
                s += a.Name.Value + "=";
            }

            return s + Print(a.Value);
        }

        private string UnaryOperator(UnaryNode u)
        {
            // pyright prints the operator token with no separator, so `not x` prints as `notx`.
            var op = text[Math.Clamp(u.Start, 0, text.Length)..Math.Clamp(u.Operand.Start, 0, text.Length)].Trim();
            return op;
        }

        private bool IsPlainQuoted(StringNode s)
        {
            var src = Source(s);
            return src.Length > 0 && src[0] is '"' or '\'';
        }

        private string PrintString(StringNode s)
        {
            // The node has no token flags, so recover them from the source prefix (pyright prints them as r, u, b, f).
            var src = Source(s);
            var i = 0;
            while (i < src.Length && char.IsLetter(src[i]))
            {
                i++;
            }

            var prefix = src[..i].ToLowerInvariant();
            var sb = new StringBuilder();
            foreach (var c in "rubf")
            {
                if (prefix.Contains(c))
                {
                    sb.Append(c);
                }
            }

            return sb.Append(src[i..]).ToString();
        }
    }
}
