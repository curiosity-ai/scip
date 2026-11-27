// Ported from abaplint packages/core/src/abap/5_syntax/expressions/method_call_param.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class MethodCallParam
{
    /** returns the type of the unnamed argument, if any */
    public static AbstractType? RunSyntax(ExpressionNode node, SyntaxInput input, object method)
    {
        if (!((node.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodCallParam)))
        {
            var message = "MethodCallParam, unexpected input";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return null;
        }

        var children = node.GetChildren();
        if (children.Count < 2 || children.Count > 3)
        {
            var message = "MethodCallParam, unexpected child length";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return null;
        }

        var child = children.ElementAtOrDefault(1);

        if (child.Get() is WParenRight || child.Get() is WParenRightW)
        {
            if (!((method is VoidType)))
            {
                var required = ((IMethodDefinition)method).GetParameters().GetRequiredParameters();
                if (required.Count > 0)
                {
                    var message = "Parameter \"" + required.ElementAtOrDefault(0).GetName() + "\" must be supplied";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return null;
                }
            }
        }
        else if (child is ExpressionNode && ((((ExpressionNode)child).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source || (((ExpressionNode)child).Get()) is ConstantString))
        {
            if (!((method is VoidType)))
            {
                if (((IMethodDefinition)method).GetParameters().GetImporting().Count == 0)
                {
                    var message = "Method \"" + ((IMethodDefinition)method).GetName() + "\" has no importing parameters";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return null;
                }
                else if (((IMethodDefinition)method).GetParameters().GetRequiredParameters().Count > 1)
                {
                    var message = "Method \"" + ((IMethodDefinition)method).GetName() + "\" has more than one importing or changing parameter";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return null;
                }
            }
            AbstractType? targetType = null;
            if (!((method is VoidType)))
            {
                var name = ((IMethodDefinition)method).GetParameters().GetDefaultImporting();
                if (name == null)
                {
                    var message = "No default importing parameter";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return null;
                }
                foreach (var i in ((IMethodDefinition)method).GetParameters().GetImporting())
                {
                    if (JsUtils.ToUpper(i.GetName()) == name)
                    {
                        targetType = i.GetType();
                        break;
                    }
                }
            }
            else
            {
                targetType = ((VoidType)method);
            }
            AbstractType? sourceType = null;
            if ((((ExpressionNode)child).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source)
            {
                sourceType = Source.RunSyntax(((ExpressionNode)child), input, targetType);
                if (CheckOffsetLengthHelper.CheckOffsetLength(((ExpressionNode)child), sourceType, method, input))
                {
                    return null;
                }
            }
            else if ((((ExpressionNode)child).Get()) is ConstantString)
            {
                sourceType = Constant.RunSyntax(((ExpressionNode)child));
            }

            if (sourceType == null)
            {
                var message = "No source type determined, method source";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return null;
            }
            else if (CheckTextSymbolHelper.CheckTextSymbol(((ExpressionNode)child), targetType, input))
            {
                return null;
            }
            else if (new TypeUtils(input.Scope).IsAssignableStrict(sourceType, targetType, ((ExpressionNode)child)) == false)
            {
                var message = "Method parameter type not compatible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return null;
            }
            return sourceType;
        }
        else if (child is ExpressionNode && (((ExpressionNode)child).Get()) is ParameterListS)
        {
            new MethodParameters().CheckExporting(((ExpressionNode)child), input, method);
        }
        else if (child.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodParameters)
        {
            new MethodParameters().RunSyntax(child, input, method);
        }
        else
        {
            var message = "MethodCallParam, unexpected child";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return null;
        }
        return null;
    }
}
