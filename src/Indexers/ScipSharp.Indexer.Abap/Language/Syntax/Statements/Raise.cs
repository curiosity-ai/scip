// Ported from abaplint packages/core/src/abap/5_syntax/statements/raise.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Raise : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        // todo
        var helper = new ObjectOriented(input.Scope);
        object? method = null;

        var classTok = node.FindDirectExpression<ClassName>()?.GetFirstToken();
        var className = classTok?.GetStr();
        if (classTok != null && !string.IsNullOrEmpty(className))
        {
            var found = input.Scope.ExistsObject(className);
            if ((found?.id) != null)
            {
                input.Scope.AddReference(classTok, found.Value.id, ReferenceType.ObjectOrientedReference, input.Filename);

                var def = input.Scope.FindObjectDefinition(className);
                method = helper.SearchMethodName(def, "CONSTRUCTOR").method;
            }
            else if (input.Scope.GetDDIC().InErrorNamespace(className) == false)
            {
                IReferenceExtras extra = new IReferenceExtras { OoName = className, OoType = "Void" };
                input.Scope.AddReference(classTok, null, ReferenceType.ObjectOrientedVoidReference, input.Filename, extra);
                method = VoidType.Get(className);
            }
            else
            {
                var message = "RAISE, unknown class " + className;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, classTok, message));
                return;
            }

            if (method == null)
            {
                method = VoidType.Get(className);
            }
        }

        var c = node.FindExpressionAfterToken("EXCEPTION");
        if (c is ExpressionNode && (c.Get() is SimpleSource2 || c.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source))
        {
            var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(c, input);
            if (type is VoidType)
            {
                method = ((VoidType)type);
            }
            else if (type is ObjectReferenceType)
            {
                var def = input.Scope.FindObjectDefinition(((ObjectReferenceType)type).GetIdentifierName());
                method = helper.SearchMethodName(def, "CONSTRUCTOR").method;
            }
            else if (type != null)
            {
                var message = "RAISE EXCEPTION, must be object reference, got " + type.GetType().Name;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, c.GetFirstToken(), message));
                return;
            }
        }

        if (method == null)
        {
            method = VoidType.Get("Exception");
        }

        // check parameters vs constructor
        var param = node.FindDirectExpression<ParameterListS>();
        if (param != null)
        {
            new global::ScipSharp.Indexer.Abap.Syntax.Expressions.MethodParameters().CheckExporting(param, input, method, true);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.RaiseWith>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.RaiseWith.RunSyntax(s, input);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
        foreach (var s in node.FindDirectExpressions<SimpleSource2>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.MessageSource>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.MessageSource.RunSyntax(s, input);
        }

        var id = node.FindExpressionAfterToken("ID")?.ConcatTokens();
        string? number = node.FindDirectExpression<MessageNumber>()?.ConcatTokens();
        if (number == null)
        {
            var num = node.FindExpressionAfterToken("NUMBER")?.ConcatTokens();
            if ((num?.StartsWith("'", StringComparison.Ordinal)) == true)
            {
                number = JsUtils.ToUpper(JsUtils.Substring(num, 1, num.Length - 1));
            }
        }
        if ((id?.StartsWith("'", StringComparison.Ordinal)) == true && !string.IsNullOrEmpty(number))
        {
            var messageClass = JsUtils.ToUpper(JsUtils.Substring(id, 1, id.Length - 1));
            input.Scope.GetMSAGReferences().AddUsing(input.Filename, node.GetFirstToken(), messageClass, number);
        }
    }
}
