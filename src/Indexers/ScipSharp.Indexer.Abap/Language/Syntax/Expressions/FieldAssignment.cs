// Ported from abaplint packages/core/src/abap/5_syntax/expressions/field_assignment.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class FieldAssignment
{

    public static void RunSyntax(INode node, SyntaxInput input, AbstractType? targetType)
    {

        var fieldSub = node.FindDirectExpression<FieldSub>();
        if (fieldSub == null)
        {
            var message = "FieldAssignment, FieldSub node not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var s = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        if (s == null)
        {
            var message = "FieldAssignment, Source node not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        AbstractType? type = null;
        if (targetType is StructureType)
        {
            AbstractType? context = ((StructureType)targetType);
            foreach (var c in fieldSub.GetChildren())
            {
                var text = c.ConcatTokens();
                if (text != "-" && context is StructureType)
                {
                    context = ((StructureType)context).GetComponentByName(text);
                    if (context == null)
                    {
                        var message = $"field {text} does not exist in structure";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                        return;
                    }
                }
            }
            type = context;
        }
        else if (targetType is VoidType)
        {
            type = ((VoidType)targetType);
        }

        Source.RunSyntax(s, input, type);
    }
}
