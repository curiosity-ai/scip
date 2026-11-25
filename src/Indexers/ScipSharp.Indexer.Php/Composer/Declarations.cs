using ScipSharp.Indexer.Php.Syntax;

namespace ScipSharp.Indexer.Php.Composer;

/// <summary>Top-level declarations of one file (the equivalent of composer's <c>PhpFileParser::findClasses</c> and friends).</summary>
internal sealed class Declarations
{
    public List<string> Classes { get; } = [];

    public List<string> Functions { get; } = [];

    public List<string> Constants { get; } = [];

    public static Declarations Scan(ParsedFile file)
    {
        var d = new Declarations();
        Walk(file.Stmts, null, d);
        return d;
    }

    private static string Q(string? ns, string name) => ns == null ? name : ns + "\\" + name;

    private static void Walk(IEnumerable<Node?> stmts, string? ns, Declarations d)
    {
        foreach (var s in stmts)
        {
            switch (s)
            {
                case NamespaceStmt n:
                    Walk(n.Stmts, n.Name?.Text, d);
                    break;
                case ClassLike { Name: { } name }:
                    d.Classes.Add(Q(ns, name.Name));
                    break;
                case FunctionStmt f:
                    d.Functions.Add(f.NamespacedName);
                    break;
                case ConstStmt c:
                    d.Constants.AddRange(c.Consts.Select(i => i.NamespacedName ?? i.Name.Name));
                    break;
                case ExprStmt { Expr: FuncCall { Callee: Name { Text: var fn }, Args: [{ Value: StringLit lit }, ..] } }
                    when fn.Equals("define", StringComparison.OrdinalIgnoreCase):
                    d.Constants.Add(lit.Value.TrimStart('\\'));
                    break;
                case GenericStmt g when g.Kind is "block" or "if" or "declare" or "try":
                    Walk(g.Kids, ns, d);
                    break;
            }
        }
    }
}
