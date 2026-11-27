// Ported from abaplint packages/core/src/abap/5_syntax/expressions/method_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class MethodSource
{

    public static object? RunSyntax(ExpressionNode node, SyntaxInput input)
    {

        var helper = new ObjectOriented(input.Scope);
        var children = node.GetChildren().ToList();

        var first = JsUtils.Shift(children);
        if (first == null)
        {
            throw new AssertError("MethodSource, first child expected");
        }

        object? context = MethodSource.FindTop(first, input, children.Cast<object>().ToList());
        bool implicitMe = false;
        if (((AbstractType?)context) == null)
        {
            context = input.Scope.FindVariable("me")?.GetType();
            children.Insert(0, first);
            implicitMe = true;
        }

        if (input.Scope.GetLanguageVersion() == LanguageVersion.Cloud && first.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic && first is ExpressionNode && children.ElementAtOrDefault(0)?.ConcatTokens() == "=>")
        {
            var name = JsUtils.RegexReplace(JsUtils.RegexReplace(((ExpressionNode)first).FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Constant>()?.ConcatTokens(), @"'", "", RegexOptions.ECMAScript), @"`", "", RegexOptions.ECMAScript);
            if (name != null && input.Scope.FindClassDefinition(name) == null)
            {
                var message = $"Class \"{name}\" not found/released";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
        }

        if (context is VoidType)
        {
            while (children.Count > 0)
            {
                var current = JsUtils.Shift(children);
                if (current is ExpressionNode && (((ExpressionNode)current).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic)
                {
                    Dynamic.RunSyntax(((ExpressionNode)current), input);
                }
            }

            return ((VoidType)context);
        }

        while (children.Count > 0)
        {
            var current = JsUtils.Shift(children);
            if (current == null)
            {
                break;
            }

            if (current.Get() is Dash)
            {
                if (context is UnknownType)
                {
                    var message = "Not a structure, type unknown, MethodSource";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                else if (!((context is StructureType)))
                {
                    var message = "Not a structure, MethodSource";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
            else if (current.Get() is InstanceArrow || current.Get() is StaticArrow)
            {
                // todo, handling static vs instance
            }
            else if (current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.AttributeName || current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.SourceField)
            {

                if (context is AbstractType)
                {
                    var attr = AttributeName.RunSyntax(((AbstractType)context), current, input, ReferenceType.DataReadReference, false);
                    var isSyntaxError = attr is VoidType && ((VoidType)attr).GetVoided() == SyntaxInput.CheckSyntaxKey;
                    if (isSyntaxError == false)
                    {
                        context = attr;
                        continue;
                    }
                }

                // try looking for method name
                string? className = context is ObjectReferenceType ? ((ObjectReferenceType)context).GetIdentifierName() : (string?)null;
                var methodToken = current.GetFirstToken();
                var methodName = methodToken?.GetStr();
                var def = input.Scope.FindObjectDefinition(className);
                var isSuper = implicitMe == false && first.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.SourceField && JsUtils.ToUpper(first.ConcatTokens()) == "SUPER";
                string? superMessage = isSuper ? helper.CheckSuperCall(def, methodName) : (string?)null;
                if (superMessage != null)
                {
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, methodToken, superMessage));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                // eslint-disable-next-line prefer-const
                var __d0 = helper.SearchMethodName(def, methodName);
                var method = __d0.method;
                var foundDef = __d0.def;

                if (implicitMe && current == first && method?.IsStatic() == false && input.Scope.IsInStaticMethod() == true)
                {
                    var message = "Method \"" + methodName + "\" not static";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, methodToken, message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }

                if (method == null && JsUtils.ToUpper(methodName) == "CONSTRUCTOR")
                {
                    context = VoidType.Get("CONSTRUCTOR");
                    // todo, this is a workaround, constructors always exists
                }
                else if (method == null && !((context is VoidType)))
                {
                    var message = "Method or attribute \"" + methodName + "\" not found, MethodSource";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                else if (method != null)
                {
                    string? specialMessage = isSuper ? (string?)null : helper.CheckSpecialTestMethodCall(method, foundDef);
                    if (specialMessage != null)
                    {
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, methodToken, specialMessage));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    var extra = helper.MethodReferenceExtras(foundDef, className);
                    input.Scope.AddReference(methodToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)method), ReferenceType.MethodReference, input.Filename, extra);

                    context = method;
                }
            }
            else if (current.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentName && context is AbstractType)
            {
                if (context is TableType && ((TableType)context).IsWithHeader())
                {
                    context = ((TableType)context).GetRowType();
                }
                context = ComponentName.RunSyntax(((AbstractType)context), current, input);
            }
            else if (current is ExpressionNode && (((ExpressionNode)current).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic)
            {
                Dynamic.RunSyntax(((ExpressionNode)current), input);
                context = VoidType.Get("Dynamic");
            }
        }

        if (context is AbstractType && !((context is VoidType)))
        {
            var message = "Not a method, MethodSource";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }
        else if (context == null)
        {
            var message = "Not found, MethodSource";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        return context;
    }

    //////////////////////////////////////
    private static AbstractType? FindTop(INode first, SyntaxInput input, List<object> children)
    {
        if (first.Get() is ClassName)
        {
            // todo, refactor this part to new expression handler,
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
        else if (first is ExpressionNode && (((ExpressionNode)first).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.SourceField && children.Count > 0)
        {
            return SourceField.RunSyntax(((ExpressionNode)first), input, ReferenceType.DataReadReference);
        }
        else if (first is ExpressionNode && (((ExpressionNode)first).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.SourceFieldSymbol)
        {
            return SourceFieldSymbol.RunSyntax(((ExpressionNode)first), input);
        }
        else if (first is ExpressionNode && (((ExpressionNode)first).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic)
        {
            Dynamic.RunSyntax(((ExpressionNode)first), input);
            return VoidType.Get("Dynamic");
        }

        return null;
    }
}
