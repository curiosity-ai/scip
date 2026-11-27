// Ported from abaplint packages/core/src/abap/5_syntax/expressions/method_call_chain.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class MethodCallChain
{
    public static AbstractType? RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType? targetType = null)
    {

        var helper = new ObjectOriented(input.Scope);
        var children = node.GetChildren();

        var first = children.ElementAtOrDefault(0);
        if (first == null)
        {
            var message = "MethodCallChain, first child expected";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        int currentIndex = 1;
        AbstractType? context = MethodCallChain.FindTop(first, input, targetType);
        if (first.Get() is MethodCall)
        {
            currentIndex--;
        }

        IExpressionOrTokenNode? previous = null;
        while (currentIndex <= children.Count)
        {
            var current = children.ElementAtOrDefault(currentIndex);
            if (current == null)
            {
                break;
            }
            currentIndex++;

            if (current is ExpressionNode && (((ExpressionNode)current).Get()) is MethodCall)
            {
                // for built-in methods set className to undefined
                string? className = context is ObjectReferenceType ? ((ObjectReferenceType)context).GetIdentifierName() : (string?)null;
                var methodToken = ((ExpressionNode)current).FindDirectExpression<MethodName>()?.GetFirstToken();
                var methodName = methodToken?.GetStr();
                var def = input.Scope.FindObjectDefinition(className);
                var isSuper = ((ExpressionNode)current) == children.ElementAtOrDefault(2) && first.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain && JsUtils.ToUpper(first.ConcatTokens()) == "SUPER";
                string? superMessage = isSuper ? helper.CheckSuperCall(def, methodName) : (string?)null;
                if (superMessage != null)
                {
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, methodToken!, superMessage));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                // eslint-disable-next-line prefer-const
                var __d0 = helper.SearchMethodName(def, methodName);
                var method = __d0.method;
                var foundDef = __d0.def;
                if (method == null && ((ExpressionNode)current) == first)
                {
                    method = BuiltIn.SearchBuiltin(JsUtils.ToUpper(methodName));
                    if (method != null)
                    {
                        input.Scope.AddReference(methodToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)method), ReferenceType.BuiltinMethodReference, input.Filename);
                    }
                }
                else
                {
                    if (previous != null && previous.GetFirstToken().GetStr() == "=>" && method?.IsStatic() == false)
                    {
                        var message = "Method \"" + JsUtils.Str(methodName) + "\" not static";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, methodToken!, message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    if (((ExpressionNode)current) == first && method?.IsStatic() == false && input.Scope.IsInStaticMethod() == true)
                    {
                        var message = "Method \"" + JsUtils.Str(methodName) + "\" not static";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, methodToken!, message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    string? notVisible = method != null ? helper.MemberNotVisible(method.GetVisibility(), def, foundDef, input.Scope.GetEnclosingClassName()) : (string?)null;
                    if (notVisible != null)
                    {
                        var message = $"Method \"{JsUtils.Str(methodName)}\" is {notVisible} and cannot be accessed";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, methodToken!, message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    string? specialMessage = isSuper ? (string?)null : helper.CheckSpecialTestMethodCall(method, foundDef);
                    if (specialMessage != null)
                    {
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, methodToken!, specialMessage));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    string? voidedName = context is VoidType ? ((VoidType)context).GetVoided() : (string?)null;
                    var extra = helper.MethodReferenceExtras(foundDef, JsUtils.Or(className, voidedName));
                    input.Scope.AddReference(methodToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)method), ReferenceType.MethodReference, input.Filename, extra);
                }
                if ((methodName?.Contains("~", StringComparison.Ordinal)) == true)
                {
                    var name = methodName.Split("~").ToList().ElementAtOrDefault(0);
                    var idef = input.Scope.FindInterfaceDefinition(name);
                    if (idef != null)
                    {
                        input.Scope.AddReference(methodToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)idef), ReferenceType.ObjectOrientedReference, input.Filename);
                    }
                }

                if (method == null && JsUtils.ToUpper(methodName) == "CONSTRUCTOR")
                {
                    context = null;
                    // todo, this is a workaround, constructors always exists
                }
                else if (method == null && !((context is VoidType)))
                {
                    var message = "Method \"" + JsUtils.Str(methodName) + "\" not found, methodCallChain";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, methodToken!, message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                else if (method != null)
                {
                    var ret = method.GetParameters().GetReturning()?.GetType();
                    context = ret;
                }

                var param = ((ExpressionNode)current).FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodCallParam>();
                if (param != null && method != null)
                {
                    var argumentType = MethodCallParam.RunSyntax(param, input, method);
                    if (method is BuiltInMethod && BuiltIn.ReturnsArgumentType(methodName) && MethodCallChain.IsNumeric(argumentType))
                    {
                        context = argumentType;
                    }
                }
                else if (param != null && context is VoidType)
                {
                    MethodCallParam.RunSyntax(param, input, ((VoidType)context));
                }
            }
            else if (current is ExpressionNode && (((ExpressionNode)current).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentName)
            {
                context = ComponentName.RunSyntax(context, ((ExpressionNode)current), input);
            }
            else if (current is ExpressionNode && (((ExpressionNode)current).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.AttributeName)
            {
                context = AttributeName.RunSyntax(context, ((ExpressionNode)current), input, ReferenceType.DataReadReference);
            }

            previous = current;
        }

        return context;
    }

    //////////////////////////////////////
    private static bool IsNumeric(AbstractType? type)
    {
        return type is IntegerType || type is Integer8Type || type is PackedType || type is FloatType || type is FloatingPointType || type is DecFloatType || type is DecFloat16Type || type is DecFloat34Type;
    }

    private static AbstractType? FindTop(INode first, SyntaxInput input, AbstractType? targetType)
    {
        if (first.Get() is ClassName)
        {
            var token = first.GetFirstToken();
            var className = token.GetStr();
            var classDefinition = input.Scope.FindObjectDefinition(className);
            if (classDefinition == null && input.Scope.GetDDIC().InErrorNamespace(className) == false)
            {
                IReferenceExtras extra = new IReferenceExtras { OoName = className, OoType = "Void" };
                input.Scope.AddReference(token, null, ReferenceType.ObjectOrientedVoidReference, input.Filename, extra);
                return VoidType.Get(className);
            }
            else if (classDefinition == null)
            {
                var message = "Class " + className + " not found";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, first.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            input.Scope.AddReference(first.GetFirstToken(), ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)classDefinition), ReferenceType.ObjectOrientedReference, input.Filename);
            return new ObjectReferenceType(((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)classDefinition));
        }
        else if (first is ExpressionNode && (((ExpressionNode)first).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain)
        {
            return FieldChain.RunSyntax(((ExpressionNode)first), input, ReferenceType.DataReadReference);
        }
        else if (first is ExpressionNode && (((ExpressionNode)first).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.NewObject)
        {
            return NewObject.RunSyntax(((ExpressionNode)first), input, targetType);
        }
        else if (first is ExpressionNode && (((ExpressionNode)first).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Cast)
        {
            return Cast.RunSyntax(((ExpressionNode)first), input, targetType);
        }
        else
        {
            var meType = input.Scope.FindVariable("me")?.GetType();
            if (meType != null)
            {
                return meType;
            }
        }
        return null;
    }
}
