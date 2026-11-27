// Ported from abaplint packages/core/src/abap/5_syntax/expressions/sql_in.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public static class SqlIn
{

    internal static readonly List<string> RANGE_COMPONENTS = new List<string> { "SIGN", "OPTION", "LOW", "HIGH" };
}

public class SQLIn
{

    public static void RunSyntax(INode node, SyntaxInput input)
    {

        var setop = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSetOpGroup>();
        if (setop != null)
        {
            SQLSetOpGroup.RunSyntax(setop, input);
            return;
        }

        if (node.GetChildren().Count == 2)
        {
            var insource = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource>();
            if (insource != null)
            {
                var intype = SQLSource.RunSyntax(insource, input);
                if (intype != null && !((intype is VoidType)) && !((intype is UnknownType)) && !((intype is TableType)))
                {
                    var message = "IN, not a table";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
                if (intype is TableType && SQLIn.IsRangeRow(((TableType)intype).GetRowType()) == false)
                {
                    var name = new Regex(@"\[\]$", RegexOptions.ECMAScript).Replace(new Regex(@"^@", RegexOptions.ECMAScript).Replace(insource.ConcatTokens(), "", 1), "", 1);
                    var message = $"row structure of {name} is not correct";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
            return;
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource>())
        {
            SQLSource.RunSyntax(s, input);
        }
        foreach (var s in node.FindDirectExpressions<SQLSourceNoSpace>())
        {
            SQLSource.RunSyntax(s, input);
        }
    }

    // "IN itab" expects a ranges table, ie. the row must have SIGN, OPTION, LOW and HIGH
    private static bool IsRangeRow(AbstractType rowType)
    {
        if (rowType is VoidType || rowType is UnknownType || rowType is AnyType)
        {
            return true;
        }
        else if (!((rowType is StructureType)))
        {
            return false;
        }
        return SqlIn.RANGE_COMPONENTS.All(c => ((StructureType)rowType).GetComponentByName(c) != null);
    }
}
