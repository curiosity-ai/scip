// Ported from abaplint packages/core/src/abap/5_syntax/statements/tables.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Tables : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var nameToken = node.FindFirstExpression<Field>()?.GetFirstToken();
        if (nameToken == null)
        {
            return;
        }

        string name = nameToken.GetStr();
        if (name.StartsWith("*", StringComparison.Ordinal))
        {
            name = JsUtils.Substring(name, 1);
        }

        // lookupTableOrView will also give Unknown and Void
        var found = input.Scope.GetDDIC()?.LookupTableOrView(name);
        if (found != null)
        {
            input.Scope.GetDDICReferences().AddUsing(input.Scope.GetParentObj(), new IObjectAndToken { Object = found.Object, Filename = input.Filename, Token = nameToken });

            if (input.Scope.GetType() == ScopeType.Form || input.Scope.GetType() == ScopeType.FunctionModule)
            {
                // hoist TABLES definitions to global scope
                input.Scope.AddNamedIdentifierToParent(nameToken.GetStr(), new TypedIdentifier(nameToken, input.Filename, found.Type, new List<string> { IdentifierMeta.Tables }));
            }
            else
            {
                input.Scope.AddIdentifier(new TypedIdentifier(nameToken, input.Filename, found.Type, new List<string> { IdentifierMeta.Tables }));
            }
            return;
        }

        // this should never happen,
        input.Scope.AddIdentifier(new TypedIdentifier(nameToken, input.Filename, new UnknownType("Tables, fallback")));
    }
}
