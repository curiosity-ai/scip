// Ported from abaplint packages/core/src/abap/5_syntax/expressions/source_field.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class SourceField
{
    public static AbstractType? RunSyntax(ExpressionNode node, SyntaxInput input, object? type = null, bool error = true)
    {
        var token = node.GetFirstToken();
        var name = token.GetStr();

        var found = input.Scope.FindVariable(name);
        if (found == null)
        {
            var message = "\"" + name + "\" not found, findTop";
            if (error == true)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            }
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        if (type != null)
        {
            input.Scope.AddReference(token, found, type, input.Filename);
        }
        if (name.Contains("~", StringComparison.Ordinal))
        {
            var idef = input.Scope.FindInterfaceDefinition(name.Split("~").ToList().ElementAtOrDefault(0));
            if (idef != null)
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)idef), ReferenceType.ObjectOrientedReference, input.Filename);
            }
        }
        return found.GetType();
    }
}
