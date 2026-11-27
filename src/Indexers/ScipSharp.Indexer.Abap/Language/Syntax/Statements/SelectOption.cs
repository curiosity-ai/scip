// Ported from abaplint packages/core/src/abap/5_syntax/statements/selectoption.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class SelectOption : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var nameExpression = node.FindFirstExpression<FieldSub>();
        if (nameExpression == null)
        {
            return;
        }

        AbstractToken nameToken = nameExpression.GetFirstToken();
        // FieldSub can include dashes and optional length, eg s-matnr or s_name(10).
        if (nameExpression.GetChildren().Count > 1)
        {
            var fullName = new Regex(@"\[\]$", RegexOptions.ECMAScript).Replace(new Regex(@"\(.+$", RegexOptions.ECMAScript).Replace(nameExpression.ConcatTokens(), "", 1), "", 1);
            nameToken = new Identifier(nameToken.GetStart(), fullName);
        }

        if (nameToken != null && nameToken.GetStr().Length > 8)
        {
            var message = "Select-option name too long, " + nameToken.GetStr();
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameToken, message));
            return;
        }

        foreach (var d in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(d, input);
            input.Scope.AddIdentifier(new TypedIdentifier(nameToken, input.Filename, VoidType.Get("DYNAMIC_SELECT_OPTION")));
            return;
        }

        var nameChain = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
        var found = new BasicTypes(input).ResolveLikeName(nameChain);
        if (found != null)
        {
            var stru = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "SIGN", Type = new CharacterType(1) }, new IStructureComponent { Name = "OPTION", Type = new CharacterType(2) }, new IStructureComponent { Name = "LOW", Type = found }, new IStructureComponent { Name = "HIGH", Type = found } });
            input.Scope.AddIdentifier(new TypedIdentifier(nameToken, input.Filename, new TableType(stru, new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default })));
        }
        else
        {
            input.Scope.AddIdentifier(new TypedIdentifier(nameToken, input.Filename, new UnknownType("Select option, fallback")));
        }

        var magicName = "%_" + nameToken.GetStr() + "_%_app_%";
        var magicToken = new Identifier(nameToken.GetStart(), magicName);
        input.Scope.AddIdentifier(new TypedIdentifier(magicToken, input.Filename, VoidType.Get("SELECT-OPTION magic")));
    }
}
