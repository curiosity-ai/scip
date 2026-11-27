// Ported from abaplint packages/core/src/abap/5_syntax/statements/interface_deferred.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class InterfaceDeferred : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var name = node.FindFirstExpression<InterfaceName>()?.GetFirstToken();
        input.Scope.AddDeferred(name, "INTF");
    }
}
