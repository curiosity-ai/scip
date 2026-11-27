// Ported from abaplint packages/core/src/abap/5_syntax/expressions/value_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class ValueBody
{
    public static AbstractType? RunSyntax(ExpressionNode? node, SyntaxInput input, AbstractType? targetType)
    {

        if (node == null)
        {
            return targetType;
        }

        bool letScoped = false;
        var letNode = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Let>();
        if (letNode != null)
        {
            letScoped = Let.RunSyntax(letNode, input);
        }

        int forScopes = 0;
        foreach (var forNode in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.For>() ?? new List<ExpressionNode>())
        {
            var scoped = For.RunSyntax(forNode, input);
            if (scoped == true)
            {
                forScopes++;
            }
        }

        var fields = new HashSet<string>();
        var hasTableLines = node.FindDirectExpression<ValueBodyLine>() != null;
        foreach (var child in node.GetChildren())
        {
            if (!((child is ExpressionNode)))
            {
                continue;
            }
            else if ((((ExpressionNode)child).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldAssignment)
            {
                var fieldname = JsUtils.ToUpper(((ExpressionNode)child).FindDirectExpression<FieldSub>()?.ConcatTokens());
                if (!string.IsNullOrEmpty(fieldname) && hasTableLines == false && fields.Contains(fieldname))
                {
                    var message = "Duplicate field assignment";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, ((ExpressionNode)child).GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                if (!string.IsNullOrEmpty(fieldname))
                {
                    fields.Add(fieldname);
                }
            }
            else if ((((ExpressionNode)child).Get()) is ValueBodyLine)
            {
                var rowFields = new HashSet<string>(fields);
                foreach (var assignment in ((ExpressionNode)child).FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldAssignment>())
                {
                    var fieldname = JsUtils.ToUpper(assignment.FindDirectExpression<FieldSub>()?.ConcatTokens());
                    if (!string.IsNullOrEmpty(fieldname) && rowFields.Contains(fieldname))
                    {
                        var message = "Duplicate field assignment";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, assignment.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    if (!string.IsNullOrEmpty(fieldname))
                    {
                        rowFields.Add(fieldname);
                    }
                }
            }
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldAssignment>())
        {
            FieldAssignment.RunSyntax(s, input, targetType);
        }

        AbstractType? type = null;
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            type = Source.RunSyntax(s, input, type);
        }
        foreach (var s in node.FindDirectExpression<ValueBase>()?.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>() ?? new List<ExpressionNode>())
        {
            type = Source.RunSyntax(s, input, type);
        }

        foreach (var foo in node.FindDirectExpressions<ValueBodyLine>())
        {
            if (!((targetType is TableType)) && !((targetType is UnknownType)) && !((targetType is AnyType)) && targetType != null && !((targetType is VoidType)))
            {
                var message = "Value, not a table type";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, foo.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            AbstractType? rowType = targetType;
            if (targetType is TableType)
            {
                rowType = ((TableType)targetType).GetRowType();
            }

            foreach (var l in foo.FindDirectExpressions<ValueBodyLines>())
            {
                foreach (var s in l.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
                {
                    // LINES OF ?? todo, pass type,
                    Source.RunSyntax(s, input);
                }
            }
            foreach (var s in foo.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldAssignment>())
            {
                FieldAssignment.RunSyntax(s, input, rowType);
            }
            foreach (var s in foo.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
            {
                var sourceType = Source.RunSyntax(s, input, rowType);
                if (rowType is StringType && sourceType is CharacterType)
                {
                    var message = "VALUE, source type CharacterType not compatible with StringType";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, s.GetFirstToken(), message));
                }
                else if (ValueBody.InsertsUsingTableKey(targetType))
                {
                    // only STANDARD tables append the rows, and thus allow padding of shorter c and x values
                    var row = ValueBody.CharacterOrHexLength(rowType);
                    var source = ValueBody.CharacterOrHexLength(sourceType);
                    if (row != null && source != null && row.Value.abap == source.Value.abap && row.Value.length != source.Value.length)
                    {
                        var message = $"VALUE, source type {source.Value.abap} LENGTH {source.Value.length} not compatible " + $"with row type {row.Value.abap} LENGTH {row.Value.length}";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, s.GetFirstToken(), message));
                    }
                }
            }
        }

        if (letScoped == true)
        {
            input.Scope.Pop(node.GetLastToken().GetEnd());
        }

        for (var i = 0; i < forScopes; i++)
        {
            input.Scope.Pop(node.GetLastToken().GetEnd());
        }

        if ((targetType?.IsGeneric()) == true && type != null)
        {
            return type;
        }
        return targetType != null ? targetType : type;
    }

    /** SORTED and HASHED tables insert the constructed rows using the table key, which
     * requires the rows to be compatible with the row type. STANDARD tables only append
     * the rows, so shorter c and x values are padded on the right instead. */
    private static bool InsertsUsingTableKey(AbstractType? targetType)
    {
        if (!((targetType is TableType)))
        {
            return false;
        }
        var accessType = ((TableType)targetType).GetAccessType();
        return accessType == TableAccessType.sorted || accessType == TableAccessType.hashed;
    }

    private static (string abap, int length)? CharacterOrHexLength(AbstractType? type)
    {
        if (type is CharacterType)
        {
            return ("c", ((CharacterType)type).GetLength());
        }
        else if (type is HexType)
        {
            return ("x", ((HexType)type).GetLength());
        }
        return null;
    }
}
