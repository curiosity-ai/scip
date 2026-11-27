// Ported from abaplint packages/core/src/abap/5_syntax/expressions/method_param.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class MethodParam
{
    public static TypedIdentifier RunSyntax(ExpressionNode node, SyntaxInput input, List<string> meta)
    {
        var name = node.FindDirectExpression<MethodParamName>();
        if (name == null)
        {
            throw new AssertError("MethodParam, todo, handle pass by value and reference");
        }

        var type = node.FindDirectExpression<TypeParam>();
        if (type == null)
        {
            throw new AssertError("MethodParam, unexpected structure");
        }

        var def = type.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Default>();
        if (def != null)
        {
            try
            {
                Default.RunSyntax(def, input);
            }
            catch (Exception e)
            {
                return new TypedIdentifier(name.GetFirstToken(), input.Filename, new UnknownType(e.ToString()), meta);
            }
        }

        var concat = JsUtils.ToUpper(type.ConcatTokens());
        if (concat == "TYPE C" || concat.StartsWith("TYPE C ", StringComparison.Ordinal))
        {
            return new TypedIdentifier(name.GetFirstToken(), input.Filename, CGenericType.Get(), meta);
        }
        else if (concat == "TYPE X" || concat.StartsWith("TYPE X ", StringComparison.Ordinal))
        {
            return new TypedIdentifier(name.GetFirstToken(), input.Filename, XGenericType.Get(), meta);
        }
        else if (concat == "TYPE P" || concat.StartsWith("TYPE P ", StringComparison.Ordinal))
        {
            return new TypedIdentifier(name.GetFirstToken(), input.Filename, PGenericType.Get(), meta);
        }
        else if (concat == "TYPE N" || concat.StartsWith("TYPE N ", StringComparison.Ordinal))
        {
            return new TypedIdentifier(name.GetFirstToken(), input.Filename, NGenericType.Get(), meta);
        }

        var found = new BasicTypes(input).ParseType(type);
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
