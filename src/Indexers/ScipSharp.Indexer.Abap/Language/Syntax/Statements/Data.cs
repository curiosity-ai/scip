// Ported from abaplint packages/core/src/abap/5_syntax/statements/data.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Data
{
    public TypedIdentifier? RunSyntax(StatementNode node, SyntaxInput input)
    {

        var name = node.FindFirstExpression<DefinitionName>();
        var dd = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.DataDefinition>();
        if (dd != null)
        {
            var id = global::ScipSharp.Indexer.Abap.Syntax.Expressions.DataDefinition.RunSyntax(dd, input);
            if (id != null && this.IsOnlyDigits(id.GetName()) && this.AllowOnlyDigitsName(node, input) == false)
            {
                var message = "not possible to have a name with only digits";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, id.GetToken(), message));
                return new TypedIdentifier(id.GetToken(), input.Filename, VoidType.Get(SyntaxInput.CheckSyntaxKey));
            }
            if (id != null && this.IsNameTooLong(id.GetName()))
            {
                var message = "DATA name too long, " + id.GetName();
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, id.GetToken(), message));
            }
            if (id?.GetType().IsGeneric() == true && id?.GetType().ContainsVoid() == false)
            {
                var message = "DATA definition cannot be generic, " + JsUtils.Str(name?.ConcatTokens());
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return new TypedIdentifier(id.GetToken(), input.Filename, VoidType.Get(SyntaxInput.CheckSyntaxKey));
            }
            return id;
        }

        if (name != null)
        {
            if (this.IsOnlyDigits(name.ConcatTokens()) && this.AllowOnlyDigitsName(node, input) == false)
            {
                var message = "not possible to have a name with only digits";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, name.GetFirstToken(), message));
            }
            if (this.IsNameTooLong(name.ConcatTokens()))
            {
                var message = "DATA name too long, " + name.ConcatTokens();
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, name.GetFirstToken(), message));
            }
            return new TypedIdentifier(name.GetFirstToken(), input.Filename, new UnknownType("data, fallback"));
        }

        return null;
    }

    private bool IsNameTooLong(string name)
    {
        return name.Length > 30;
    }

    private bool IsOnlyDigits(string name)
    {
        return Regex.IsMatch(name, @"^[0-9]+$", RegexOptions.ECMAScript);
    }

    private bool AllowOnlyDigitsName(StatementNode node, SyntaxInput input)
    {
        return input.Scope.IsAnyOO() == false && node.GetColon() != null;
    }
}
