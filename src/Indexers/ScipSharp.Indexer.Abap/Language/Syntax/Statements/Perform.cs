// Ported from abaplint packages/core/src/abap/5_syntax/statements/perform.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class PerformParameter
{
    public ExpressionNode Node { get; set; }
    public AbstractType? Type { get; set; }
}

public class Perform : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        if (!((node.Get() is global::ScipSharp.Indexer.Abap.Statements.Statements.Perform)))
        {
            throw new AssertError("checkPerform unexpected node type");
        }

        ////////////////////////////
        // check parameters are defined
        List<PerformParameter> changing = new List<PerformParameter>();
        foreach (var c in node.FindDirectExpressions<PerformChanging>())
        {
            foreach (var s in c.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
            {
                changing.Add(new PerformParameter { Node = s, Type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(s, input) });
            }
        }
        List<PerformParameter> tables = new List<PerformParameter>();
        foreach (var t in node.FindDirectExpressions<PerformTables>())
        {
            foreach (var s in t.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
            {
                tables.Add(new PerformParameter { Node = s, Type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input) });
            }
        }
        List<PerformParameter> @using = new List<PerformParameter>();
        foreach (var u in node.FindDirectExpressions<PerformUsing>())
        {
            foreach (var s in u.FindDirectExpressions<SimpleSource3>())
            {
                @using.Add(new PerformParameter { Node = s, Type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input) });
            }
        }

        ////////////////////////////
        // find FORM definition
        if ((node.FindFirstExpression<IncludeName>()) != null)
        {
            return;
            // in external program, not checked, todo
        }

        var dynamic = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>();
        if (dynamic != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(dynamic, input);
            return;
            // todo, maybe some parts can be checked
        }

        var expr = node.FindFirstExpression<FormName>();
        if (expr == null)
        {
            return;
            // it might be a dynamic call
        }

        var name = expr.ConcatTokens();

        var found = input.Scope.FindFormDefinition(name);
        if (found == null)
        {
            var message = "FORM definition \"" + name + "\" not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, expr.GetFirstToken(), message));
            return;
        }

        input.Scope.AddReference(expr.GetFirstToken(), ((Identifier?)found), ReferenceType.FormReference, input.Filename);

        ////////////////////////////
        // check parameters match
        // deferred, the FORM might be defined after the PERFORM, its parameters are only typed
        // once the FORM statement itself has been traversed
        input.Deferred.Add(() =>
        {
            // look up again, the definition is replaced when the FORM statement is traversed
            var def = input.Scope.FindFormDefinition(name) ?? found;
            if (this.CheckParameters(node, tables, def.GetTablesParameters(), "TABLES", input) == false)
            {
                return;
            }
            // USING and CHANGING are interchangeable, the actual parameters form a single positional list
            var formal = def.GetUsingParameters().Concat(def.GetChangingParameters()).ToList();
            this.CheckParameters(node, @using.Concat(changing).ToList(), formal, "USING/CHANGING", input);
        });
    }

    // returns false if an issue was reported
    private bool CheckParameters(StatementNode node, List<PerformParameter> actual, List<TypedIdentifier> formal, string kind, SyntaxInput input)
    {

        if (actual.Count != formal.Count)
        {
            var message = $"PERFORM, expected {formal.Count} {kind} parameters, found {actual.Count}";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return false;
        }

        var typeUtils = new TypeUtils(input.Scope);
        for (var i = 0; i < actual.Count; i++)
        {
            var __d0 = actual[i];
            var source = __d0.Node;
            var type = __d0.Type;
            if (type == null)
            {
                continue;
            }
            var parameter = formal.ElementAtOrDefault(i);
            var structureTyping = parameter.GetMeta().Contains(IdentifierMeta.FormParameterStructure);
            bool assignable = structureTyping ? typeUtils.IsAssignableStructureTyping(type, parameter.GetType(), source) : typeUtils.IsAssignableStrict(type, parameter.GetType(), source);
            if (assignable == false)
            {
                var message = $"PERFORM parameter type not compatible, {parameter.GetName()}";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, source.GetFirstToken(), message));
                return false;
            }
        }

        return true;
    }
}
