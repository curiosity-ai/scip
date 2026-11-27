// Ported from abaplint packages/core/src/abap/5_syntax/statements/fieldsymbol.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class FieldSymbol : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var fs = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldSymbol>();
        var fsname = fs?.GetFirstToken();
        if (fs == null || fsname == null)
        {
            return;
        }

        if (fs.ConcatTokens().Length > 30)
        {
            var message = "FIELD-SYMBOLS name too long, " + fs.ConcatTokens();
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, fsname, message));
        }

        if (node.GetChildren().Count == 5)
        {
            // no type specified
            input.Scope.AddIdentifier(new TypedIdentifier(fsname, input.Filename, VoidType.Get("FS-SIMPLE")));
            return;
        }

        var bfound = new BasicTypes(input).ParseType(node);
        if (bfound != null)
        {
            input.Scope.AddIdentifier(new TypedIdentifier(fsname, input.Filename, bfound));
            return;
        }

        input.Scope.AddIdentifier(new TypedIdentifier(fsname, input.Filename, new UnknownType("Fieldsymbol, fallback")));
    }
}
