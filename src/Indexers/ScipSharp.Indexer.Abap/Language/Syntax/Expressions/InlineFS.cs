// Ported from abaplint packages/core/src/abap/5_syntax/expressions/inline_fs.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class InlineFS
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType? type)
    {
        var token = node.FindFirstExpression<TargetFieldSymbol>()?.GetFirstToken();

        var fs = node.FindFirstExpression<FieldSymbol>();
        if (fs != null && fs.ConcatTokens().Length > 30)
        {
            var message = "FIELD-SYMBOLS name too long, " + fs.ConcatTokens();
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, fs.GetFirstToken(), message));
        }

        if (token != null && type != null)
        {
            var identifier = new TypedIdentifier(token, input.Filename, type, new List<string> { IdentifierMeta.InlineDefinition });
            input.Scope.AddIdentifier(identifier);
            input.Scope.AddReference(token, identifier, ReferenceType.DataWriteReference, input.Filename);
        }
        else if (token != null)
        {
            var message = "InlineFS, could not determine type for \"" + token.GetStr() + "\"";
            var identifier = new TypedIdentifier(token, input.Filename, new UnknownType(message), new List<string> { IdentifierMeta.InlineDefinition });
            input.Scope.AddIdentifier(identifier);
            input.Scope.AddReference(token, identifier, ReferenceType.DataWriteReference, input.Filename);
        }
    }
}
