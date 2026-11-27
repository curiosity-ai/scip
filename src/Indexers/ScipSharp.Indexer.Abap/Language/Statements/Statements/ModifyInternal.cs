// Ported from abaplint packages/core/src/abap/2_statements/statements/modify_internal.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ModifyInternal : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var index = Seq("INDEX", Expr<Source>());
        var from = Seq("FROM", Expr<Source>());
        var transporting = Seq("TRANSPORTING", Plus(Alt(Expr<ComponentChainSimple>(), Expr<Dynamic>())));

        var where = Seq("WHERE", Expr<ComponentCond>());
        var assigning = Seq("ASSIGNING", Expr<FSTarget>());
        var @using = Seq("USING KEY", Expr<SimpleName>());
        var additions = Per(where, assigning, @using);

        var target = Alt(Expr<Target>(), Expr<Dynamic>());

        var options = Alt(
          Per(index, transporting),
          Seq(from, Opt(Per(index, transporting, @using))),
          Seq(Per(index, transporting), from, Opt(Per(index, transporting))));

        var @long = Seq("MODIFY", Opt("TABLE"), target, Opt(options), Opt(additions));

        var simple = Seq("MODIFY TABLE", target, Per(from, @using));

        return Alt(@long, simple);
    }
}
