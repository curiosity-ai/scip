// Ported from abaplint packages/core/src/abap/2_statements/statements/delete_internal.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DeleteInternal : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        // todo, is READ and DELETE similar? something can be reused?
        var index = Seq("INDEX", Expr<Source>());

        var keyName = AltPrio(Expr<SimpleName>(), Expr<Dynamic>());
        var @using = Seq("USING KEY", keyName);

        var from = Seq("FROM", Expr<Source>());

        var fromTo = Seq(OptPrio(from),
                           OptPrio(Seq("TO", Expr<Source>())));

        // WHERE (dynamic) blocked in KeyUser
        var where = Seq("WHERE", Alt(Expr<ComponentCond>(), VerNotLang(LanguageVersion.KeyUser, Expr<Dynamic>())));

        var key = Seq("WITH TABLE KEY",
                        Opt(Seq(keyName, "COMPONENTS")),
                        Plus(Expr<ComponentCompare>()));

        var table = Seq("TABLE",
                          Expr<Target>(),
                          Alt(Per(index, @using), Seq(OptPrio(from), OptPrio(@using)), key));

        var other = Seq(Expr<Target>(),
                          Alt(Per(index, @using), fromTo, key), Opt(where));

        var f = Seq(Expr<SimpleFieldChain2>(), OptPrio(Expr<FieldOffset>()), OptPrio(Expr<FieldLength>()));

        var adjacent = Seq("ADJACENT DUPLICATES FROM",
                             Expr<Target>(),
                             OptPrio(@using),
                             Opt(Seq("COMPARING", AltPrio("ALL FIELDS", Plus(AltPrio(f, Expr<Dynamic>()))))),
                             OptPrio(@using));

        return Seq("DELETE", Alt(table, adjacent, other));
    }
}
