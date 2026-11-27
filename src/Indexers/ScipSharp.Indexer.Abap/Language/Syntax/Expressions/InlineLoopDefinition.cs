// Ported from abaplint packages/core/src/abap/5_syntax/expressions/inline_loop_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class InlineLoopDefinition
{
    public static void RunSyntax(ExpressionNode? node, SyntaxInput input)
    {
        if (node == null)
        {
            return;
        }

        ExpressionNode? target = node.FindDirectExpression<TargetField>();
        if (target == null)
        {
            target = node.FindDirectExpression<TargetFieldSymbol>();
        }
        var source = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();

        if (source != null && target != null)
        {
            var sourceType = Source.RunSyntax(source, input);
            AbstractType? rowType = null;
            if (sourceType is TableType)
            {
                rowType = ((TableType)sourceType).GetRowType();
            }
            else if (sourceType is VoidType)
            {
                rowType = ((VoidType)sourceType);
            }
            else if (sourceType is UnknownType)
            {
                var message = "Unknown type, " + ((UnknownType)sourceType).GetError();
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            if (rowType == null && JsUtils.ToUpper(node.ConcatTokens()).Contains(" IN GROUP ", StringComparison.Ordinal) && sourceType != null)
            {
                rowType = sourceType;
            }
            else if (rowType == null)
            {
                var message = "InlineLoopDefinition, not a table type";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            var identifier = new TypedIdentifier(target.GetFirstToken(), input.Filename, rowType, new List<string> { IdentifierMeta.InlineDefinition });
            input.Scope.AddReference(target.GetFirstToken(), identifier, ReferenceType.DataWriteReference, input.Filename);
            input.Scope.AddReference(target.GetFirstToken(), identifier, ReferenceType.DataReadReference, input.Filename);
            input.Scope.AddIdentifier(identifier);
        }

        var index = node.FindExpressionAfterToken("INTO");
        if (index != null && index.Get() is TargetField)
        {
            var identifier = new TypedIdentifier(index.GetFirstToken(), input.Filename, IntegerType.Get(), new List<string> { IdentifierMeta.InlineDefinition });
            input.Scope.AddReference(index.GetFirstToken(), identifier, ReferenceType.DataWriteReference, input.Filename);
            input.Scope.AddIdentifier(identifier);
        }
    }
}
