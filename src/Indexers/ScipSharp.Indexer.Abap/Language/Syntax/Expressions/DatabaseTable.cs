// Ported from abaplint packages/core/src/abap/5_syntax/expressions/database_table.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

// type DatabaseTableSource = Table | DataDefinition | View | undefined; (mapped at use sites)

public class DatabaseTable
{
    public static IParseTypeObject? RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        var token = node.GetFirstToken();
        var rawName = token.GetStr();
        var starPrefixed = rawName.StartsWith("*", StringComparison.Ordinal);
        string name = starPrefixed ? JsUtils.Substring(rawName, 1) : rawName;
        if (name == "(")
        {
            // dynamic
            return null;
        }

        var found = input.Scope.GetDDIC().LookupTableOrView2(name);
        if (found == null && input.Scope.GetDDIC().InErrorNamespace(name) == true)
        {
            var message = "Database table or view \"" + name + "\" not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
        }
        else if (found == null)
        {
            input.Scope.AddReference(token, null, ReferenceType.TableVoidReference, input.Filename);
        }
        else
        {
            input.Scope.AddReference(token, found.GetIdentifier(), ReferenceType.TableReference, input.Filename);
            input.Scope.GetDDICReferences().AddUsing(input.Scope.GetParentObj(), new IObjectAndToken { Object = found, Token = token, Filename = input.Filename });
        }

        return (IParseTypeObject?)found;
    }
}
