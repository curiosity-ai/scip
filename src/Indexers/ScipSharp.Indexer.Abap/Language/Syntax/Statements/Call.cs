// Ported from abaplint packages/core/src/abap/5_syntax/statements/call.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Syntax.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Call : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var children = node.GetChildren();

        if (children.Count == 2)
        {
            var first = ((ExpressionNode)children.ElementAtOrDefault(0));
            MethodCallChain.RunSyntax(first, input);
            return;
        }

        var methodSource = ((ExpressionNode)children.ElementAtOrDefault(2));
        if (methodSource == null)
        {
            var message = "Call, child MethodSource not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }
        var methodDef = MethodSource.RunSyntax(methodSource, input);

        var body = children.ElementAtOrDefault(3);
        if (body is ExpressionNode)
        {
            // todo, resolve the method definition above and pass, if possible, in case of dynamic pass void
            MethodCallBody.RunSyntax(((ExpressionNode)body), input, methodDef ?? VoidType.Get("CallTODO"));
        }
    }
}
