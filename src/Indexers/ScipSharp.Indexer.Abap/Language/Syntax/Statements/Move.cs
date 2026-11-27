// Ported from abaplint packages/core/src/abap/5_syntax/statements/move.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Move : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var targets = node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        var firstTarget = targets.ElementAtOrDefault(0);

        var inline = firstTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();

        AbstractType? targetType = null;
        if (inline == null)
        {
            targetType = firstTarget != null ? global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(firstTarget, input) : (AbstractType?)null;
            foreach (var t in targets)
            {
                if (t == firstTarget)
                {
                    continue;
                }
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
            }
        }

        if (inline == null && firstTarget != null)
        {
            // hmm, does this do the scoping correctly? handle constants etc? todo
            var found = input.Scope.FindVariable(firstTarget.ConcatTokens());
            if (found != null && found.GetMeta().Contains(IdentifierMeta.ReadOnly))
            {
                var message = $"\"{firstTarget.ConcatTokens()}\" cannot be modified, it is readonly";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, firstTarget.GetFirstToken(), message));
                return;
            }
        }

        var source = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        AbstractType? sourceType = source != null ? global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(source, input, targetType) : (AbstractType?)null;
        if (sourceType == null)
        {
            var message = "No source type determined";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        if (inline == null && node.FindDirectTokenByText("=") != null && Move.IsRefInferIntoGenericFieldSymbol(targets, source, targetType))
        {
            var message = "REF #( ) cannot infer a type from a generic target, use REF data( ) or a named type";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, source!.GetFirstToken(), message));
            return;
        }

        if ((node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dereference>()) != null)
        {
            sourceType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dereference.RunSyntax(node, sourceType, input);
        }

        if (inline != null)
        {
            if (sourceType is PackedType && (source?.FindDirectExpression<ArithOperator>()) != null)
            {
                sourceType = new PackedType(8, 0);
            }
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, sourceType);
            targetType = sourceType;
        }

        if (node.FindDirectTokenByText("?=") != null)
        {
            if (new TypeUtils(input.Scope).IsCastable(sourceType, targetType) == false)
            {
                var message = "Incompatible types";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }
        else if (new TypeUtils(input.Scope).IsAssignableNew(sourceType, targetType, source) == false)
        {
            var message = "Incompatible types";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }
    }

    /** target = REF #( ... ), with the single target being a field symbol typed "any" or "data",
     *  the type of REF # is inferred from the target, which is generic */
    private static bool IsRefInferIntoGenericFieldSymbol(List<ExpressionNode> targets, ExpressionNode? source, AbstractType? targetType)
    {
        if (targets.Count != 1 || source == null)
        {
            return false;
        }
        else if (!((targetType is AnyType)) && !((targetType is DataType)))
        {
            return false;
        }

        var targetChildren = targets.ElementAtOrDefault(0).GetChildren();
        if (targetChildren.Count != 1 || !((targetChildren.ElementAtOrDefault(0).Get() is TargetFieldSymbol)))
        {
            return false;
        }

        var first = source.GetFirstChild();
        if (!((first is TokenNode)) || JsUtils.ToUpper(((TokenNode)first).GetFirstToken().GetStr()) != "REF")
        {
            return false;
        }

        return source.FindDirectExpression<TypeNameOrInfer>()?.ConcatTokens() == "#";
    }
}
