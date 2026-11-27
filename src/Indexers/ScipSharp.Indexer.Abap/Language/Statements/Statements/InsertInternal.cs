// Ported from abaplint packages/core/src/abap/2_statements/statements/insert_internal.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InsertInternal : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var target = AltPrio(Expr<Source>(), Expr<Dynamic>());
        var assigning = Seq("ASSIGNING", Expr<FSTarget>());
        var @ref = Seq("REFERENCE INTO", Expr<Target>());
        var index = Seq("INDEX", Expr<Source>());
        var initial = Str("INITIAL LINE");
        var into = Seq("INTO", Expr<Target>());
        var intoTable = Seq("INTO TABLE", Expr<Target>(), Opt(Alt(@ref, assigning)));

        var to = Seq("TO", Expr<Source>());
        var from = Seq("FROM", Expr<Source>());
        var fromTo = Opt(Per(from, to));

        var foo = Alt(intoTable,
                        Seq(into, Opt(Per(index, Alt(@ref, assigning)))),
                        Per(index, Alt(@ref, assigning)));

        var lines = Seq("LINES OF",
                          target,
                          Opt(fromTo));

        var src = Alt(Expr<SimpleSource4>(), Ver(Release.v740sp02, Expr<Source>(), also: AlsoIn.OpenABAP));

        var tab = Seq("TABLE", Expr<Source>());

        var ret = Seq("INSERT",
                        AltPrio(tab, Seq(AltPrio(initial, lines, src), foo)));

        return ret;
    }
}
