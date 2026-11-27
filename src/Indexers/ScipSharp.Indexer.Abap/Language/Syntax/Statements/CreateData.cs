// Ported from abaplint packages/core/src/abap/5_syntax/statements/create_data.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class CreateData : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        var likeLineOf = JsUtils.ToUpper(node.ConcatTokens()).Contains(" LIKE LINE OF ", StringComparison.Ordinal);
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            var sourceType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
            if (likeLineOf == true && sourceType != null && !((sourceType is TableType)) && !((sourceType is VoidType)) && !((sourceType is UnknownType)) && !((sourceType is AnyType)))
            {
                var message = $"\"{s.ConcatTokens()}\" is not an internal table";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        foreach (var s in node.FindDirectExpressions<SimpleSource3>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(t, input);
        }

        var type = node.FindDirectExpression<TypeName>();
        if (type != null)
        {
            var found = new BasicTypes(input).ResolveTypeName(type);
            if (found is UnknownType)
            {
                if (JsUtils.ToUpper(node.ConcatTokens()).Contains(" REF TO ", StringComparison.Ordinal))
                {
                    var def = input.Scope.FindObjectDefinition(type.ConcatTokens());
                    if (def != null)
                    {
                        input.Scope.AddReference(type.GetFirstToken(), ((Identifier?)def), ReferenceType.TypeReference, input.Filename);
                    }
                    else
                    {
                        var identifier = new TypedIdentifier(type.GetFirstToken(), input.Filename, ((UnknownType)found));
                        input.Scope.AddReference(type.GetFirstToken(), identifier, ReferenceType.TypeReference, input.Filename);
                    }
                }
                else
                {
                    var identifier = new TypedIdentifier(type.GetFirstToken(), input.Filename, ((UnknownType)found));
                    input.Scope.AddReference(type.GetFirstToken(), identifier, ReferenceType.TypeReference, input.Filename);
                }
            }
        }
    }
}
