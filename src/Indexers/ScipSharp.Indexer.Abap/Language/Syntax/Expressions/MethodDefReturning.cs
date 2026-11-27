// Ported from abaplint packages/core/src/abap/5_syntax/expressions/method_def_returning.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class MethodDefReturning
{
    public static TypedIdentifier RunSyntax(ExpressionNode node, SyntaxInput input, List<string> meta)
    {
        var name = node.FindDirectExpression<MethodParamName>();
        if (name == null)
        {
            throw new AssertError("method_parameter.ts, todo, handle pass by value and reference");
        }

        var type = node.FindDirectExpression<TypeParam>();
        if (type == null)
        {
            throw new AssertError("method_parameter.ts, unexpected structure");
        }

        AbstractType? found = new BasicTypes(input).ParseType(type);

        var message = "RETURNING parameter must be fully specified";
        var typeText = JsUtils.ToUpper(type.ConcatTokens());
        if (found?.IsGeneric() == true || typeText == "TYPE X" || typeText == "TYPE C")
        {
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, type.GetFirstToken(), message));
            found = new UnknownType(message);
        }

        if (found != null)
        {
            return new TypedIdentifier(name.GetFirstToken(), input.Filename, found, meta);
        }
        else
        {
            return new TypedIdentifier(name.GetFirstToken(), input.Filename, new UnknownType("method param, todo"), meta);
        }
    }
}
