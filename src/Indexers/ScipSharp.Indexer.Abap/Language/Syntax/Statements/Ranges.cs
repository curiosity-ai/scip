// Ported from abaplint packages/core/src/abap/5_syntax/statements/ranges.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Ranges : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        if (input.Scope.IsAnyOO())
        {
            var message = "RANGES is not allowed within classes";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
        }

        var nameToken = node.FindFirstExpression<DefinitionName>()?.GetFirstToken();

        var typeExpression = node.FindFirstExpression<SimpleFieldChain2>();
        if (typeExpression == null)
        {
            throw new AssertError("Ranges, unexpected node");
        }

        var found = new BasicTypes(input).ResolveLikeName(typeExpression);
        if (found != null && nameToken != null)
        {
            var structure = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "sign", Type = new CharacterType(1) }, new IStructureComponent { Name = "option", Type = new CharacterType(2) }, new IStructureComponent { Name = "low", Type = found }, new IStructureComponent { Name = "high", Type = found } });
            var type = new TableType(structure, new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default });
            var id = new TypedIdentifier(nameToken, input.Filename, type);
            input.Scope.AddIdentifier(id);
        }
    }
}
