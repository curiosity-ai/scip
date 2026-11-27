// Ported from abaplint packages/core/src/abap/5_syntax/statements/sort.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Sort : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(s, input);
        }

        var tnode = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        if (tnode != null)
        {
            var ttype = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(tnode, input);
            if (ttype is TableType)
            {
                if (((TableType)ttype).GetOptions()?.PrimaryKey?.Type == TableAccessType.sorted)
                {
                    var message = "Sorted table, already sorted";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, tnode.GetFirstToken(), message));
                    return;
                }
                var rowType = ((TableType)ttype).GetRowType();
                if (!((rowType is VoidType)) && !((rowType is UnknownType)) && !((rowType is AnyType)))
                {
                    foreach (var component in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentChain>())
                    {
                        global::ScipSharp.Indexer.Abap.Syntax.Expressions.ComponentChain.RunSyntax(rowType, component, input);
                        /*
            if (component.getChildren().length > 1) {
              continue;
            }
            const cname = component.concatTokens().toUpperCase();
            if (cname === "TABLE_LINE") {
              continue;
            } else if (!(rowType instanceof StructureType)) {
              const message = "SORT, table row is not structured";
              input.issues.push(syntaxIssue(input, tnode.getFirstToken(), message));
              return;
            } else if (rowType.getComponentByName(cname) === undefined) {
              const message = `Field ${cname} does not exist in table row structure`;
              input.issues.push(syntaxIssue(input, node.getFirstToken(), message));
              return;
            }
              */
                    }
                }
            }
            else if (ttype != null && !((ttype is VoidType)) && !((ttype is UnknownType)) && !((ttype is AnyType)))
            {
                var message = "SORT, must be a internal table";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, tnode.GetFirstToken(), message));
                return;
            }
        }
    }
}
