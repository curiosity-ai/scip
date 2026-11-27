// Ported from abaplint packages/core/src/abap/5_syntax/statements/assign.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Assign : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var assignSource = node.FindDirectExpression<AssignSource>();
        var isComponent = JsUtils.ToUpper(assignSource?.GetFirstChild()?.ConcatTokens()) == "COMPONENT";
        List<ExpressionNode> sources = assignSource?.FindDirectExpressionsMulti(typeof(global::ScipSharp.Indexer.Abap.Statements.Expressions.Source), typeof(SimpleSource3)) ?? new List<ExpressionNode>();
        var theSource = sources.ElementAtOrDefault(sources.Count - 1);

        AbstractType? sourceType = null;
        var firstAssign = assignSource?.GetChildren().ElementAtOrDefault(0);
        var secondAssign = assignSource?.GetChildren().ElementAtOrDefault(1);
        var thirdAssign = assignSource?.GetChildren().ElementAtOrDefault(2);
        if (secondAssign?.ConcatTokens() == "=>" && firstAssign != null && (thirdAssign?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic)
        {
            var name = firstAssign.ConcatTokens();
            object? found = (object?)input.Scope.FindClassDefinition(name) ?? input.Scope.FindVariable(name);
            if (found == null && input.Scope.GetDDIC().InErrorNamespace(name) && name.StartsWith("(", StringComparison.Ordinal) == false)
            {
                var message = name + " not found, dynamic";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            sourceType = VoidType.Get("Dynamic");
        }
        else
        {
            sourceType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(theSource, input, null, false, isComponent == false);
            if (secondAssign?.ConcatTokens() == "->" && (thirdAssign?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic && sourceType?.IsGeneric() == true && !((sourceType is DataReference)) && !((sourceType is ObjectReferenceType)) && !((sourceType is GenericObjectReferenceType)) && !(Versions.ReleaseAtLeast(input.Scope.GetRelease(), Release.v756)) && input.Scope.GetLanguageVersion() != LanguageVersion.Cloud && !input.Scope.GetOpenABAP())
            {
                var message = $"\"{theSource.ConcatTokens()}\" is not a reference variable";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        if (assignSource?.GetChildren().Count == 5 && isComponent)
        {
            var componentSource = sources.ElementAtOrDefault(sources.Count - 2);
            var componentType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(componentSource, input);
            if (new TypeUtils(input.Scope).IsAssignable(componentType, new CharacterType(30)) == false)
            {
                var message = "component name must be charlike";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        if (sourceType == null || (assignSource?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>()) != null)
        {
            sourceType = AnyType.Get();
        }
        foreach (var d in assignSource?.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>() ?? new List<ExpressionNode>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(d, input);
        }

        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FSTarget>();
        if (target != null)
        {
            if (isComponent)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.FSTarget.RunSyntax(target, input, AnyType.Get());
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.FSTarget.RunSyntax(target, input, sourceType);
            }
        }

        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            if (s == theSource)
            {
                continue;
            }
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
    }
}
