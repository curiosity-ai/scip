// Ported from abaplint packages/core/src/abap/5_syntax/expressions/source_field_symbol.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class SourceFieldSymbol
{
    public static AbstractType RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        var token = node.GetFirstToken();
        var found = input.Scope.FindVariable(token.GetStr());
        if (found == null)
        {
            var message = "\"" + node.GetFirstToken().GetStr() + "\" not found, SourceFieldSymbol";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }
        input.Scope.AddReference(token, found, ReferenceType.DataReadReference, input.Filename);
        return found.GetType();
    }
}
