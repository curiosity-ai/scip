// Ported from abaplint packages/core/src/abap/5_syntax/statements/parameter.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Parameter : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var nameExpression = node.FindFirstExpression<FieldSub>();
        if (nameExpression == null)
        {
            return;
        }

        AbstractToken nameToken = nameExpression.GetFirstToken();
        // FieldSub can include dashes and optional length, eg p-tcode or p_table(4).
        if (nameExpression.GetChildren().Count > 1)
        {
            var fullName = new Regex(@"\[\]$", RegexOptions.ECMAScript).Replace(new Regex(@"\(.+$", RegexOptions.ECMAScript).Replace(nameExpression.ConcatTokens(), "", 1), "", 1);
            nameToken = new Identifier(nameToken.GetStart(), fullName);
        }

        if (nameToken != null && nameToken.GetStr().Length > 8)
        {
            var message = "Parameter name too long, " + nameToken.GetStr();
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        if (node.FindDirectTokenByText("RADIOBUTTON") != null && node.FindDirectTokenByText("LENGTH") != null)
        {
            var message = "RADIOBUTTON and LENGTH not possible together";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var radioGroup = node.FindFirstExpression<RadioGroupName>();
        if (radioGroup != null && radioGroup.ConcatTokens().Length > 4)
        {
            var message = "Radio button group name too long, " + radioGroup.ConcatTokens();
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, radioGroup.GetFirstToken(), message));
        }

        if (this.HasUserCommand(node) && this.HasLength(node))
        {
            var message = "USER-COMMAND and LENGTH not possible together";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var bfound = new BasicTypes(input).ParseType(node);
        if (bfound != null)
        {
            input.Scope.AddIdentifier(new TypedIdentifier(nameToken, input.Filename, bfound));
        }
        else
        {
            input.Scope.AddIdentifier(new TypedIdentifier(nameToken, input.Filename, new UnknownType("Parameter, fallback")));
        }

        var magicName = "%_" + nameToken.GetStr() + "_%_app_%";
        var magicToken = new Identifier(nameToken.GetStart(), magicName);
        input.Scope.AddIdentifier(new TypedIdentifier(magicToken, input.Filename, VoidType.Get("PARAMETER-MAGIC")));
    }

    // "USER-COMMAND" is lexed as three tokens
    private bool HasUserCommand(StatementNode node)
    {
        var tokens = node.GetTokens();
        for (var i = 0; i < tokens.Count - 2; i++)
        {
            if (JsUtils.ToUpper(tokens.ElementAtOrDefault(i).GetStr()) == "USER" && tokens.ElementAtOrDefault(i + 1).GetStr() == "-" && JsUtils.ToUpper(tokens.ElementAtOrDefault(i + 2).GetStr()) == "COMMAND")
            {
                return true;
            }
        }
        return false;
    }

    // "VISIBLE LENGTH" is not the same as "LENGTH"
    private bool HasLength(StatementNode node)
    {
        var tokens = node.GetTokens();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (JsUtils.ToUpper(tokens[i].GetStr()) == "LENGTH" && JsUtils.ToUpper(tokens.ElementAtOrDefault(i - 1)?.GetStr()) != "VISIBLE")
            {
                return true;
            }
        }
        return false;
    }
}
