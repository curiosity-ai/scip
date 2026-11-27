// Ported from abaplint packages/core/src/abap/5_syntax/statements/raise_event.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class RaiseEvent : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        // todo: only possible in classes
        var f = node.FindDirectExpression<EventName>();
        if ((f?.ConcatTokens().Contains("~", StringComparison.Ordinal)) == true)
        {
            var name = f.ConcatTokens().Split("~").ToList().ElementAtOrDefault(0);
            var idef = input.Scope.FindInterfaceDefinition(name);
            if (idef != null)
            {
                input.Scope.AddReference(f.GetFirstToken(), ((Identifier?)idef), ReferenceType.ObjectOrientedReference, input.Filename);
            }
        }

        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
    }
}
