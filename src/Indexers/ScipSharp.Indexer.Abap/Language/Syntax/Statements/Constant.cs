// Ported from abaplint packages/core/src/abap/5_syntax/statements/constant.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Constant
{
    public TypedIdentifier RunSyntax(StatementNode node, SyntaxInput input)
    {
        var basic = new BasicTypes(input);
        var found = basic.SimpleType(node);
        if (found != null)
        {
            var val = basic.FindValue(node);
            List<string> meta = new List<string> { IdentifierMeta.ReadOnly, IdentifierMeta.Static };
            if (this.IsOnlyDigits(found.GetName()) && this.AllowOnlyDigitsName(node, input) == false)
            {
                var message = "not possible to have a name with only digits";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, found.GetToken(), message));
                return new TypedIdentifier(found.GetToken(), input.Filename, VoidType.Get(SyntaxInput.CheckSyntaxKey), meta, val);
            }
            if (this.IsNameTooLong(found.GetName()))
            {
                var message = "CONSTANTS name too long, " + found.GetName();
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, found.GetToken(), message));
            }
            return new TypedIdentifier(found.GetToken(), input.Filename, found.GetType(), meta, val);
        }

        var fallback = node.FindFirstExpression<DefinitionName>();
        if (fallback != null)
        {
            if (this.IsOnlyDigits(fallback.ConcatTokens()) && this.AllowOnlyDigitsName(node, input) == false)
            {
                var message = "not possible to have a name with only digits";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, fallback.GetFirstToken(), message));
            }
            if (this.IsNameTooLong(fallback.ConcatTokens()))
            {
                var message = "CONSTANTS name too long, " + fallback.ConcatTokens();
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, fallback.GetFirstToken(), message));
            }
            return new TypedIdentifier(fallback.GetFirstToken(), input.Filename, new UnknownType("constant, fallback"));
        }

        throw new AssertError("Statement Constant: unexpected structure");
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
