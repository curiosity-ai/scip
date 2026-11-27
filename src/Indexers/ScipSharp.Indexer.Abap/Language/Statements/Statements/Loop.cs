// Ported from abaplint packages/core/src/abap/2_statements/statements/loop.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Loop : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var where = Seq("WHERE", Alt(Expr<ComponentCond>(), VerNotLang(LanguageVersion.KeyUser, Expr<Dynamic>())));

        var group = Ver(Release.v740sp08, Seq("GROUP BY", Expr<LoopGroupBy>()), also: AlsoIn.OpenABAP);

        var step = Ver(Release.v757, Seq("STEP", Expr<Source>()), also: AlsoIn.OpenABAP);

        var from = Seq("FROM", Expr<Source>());

        var to = Seq("TO", Expr<Source>());

        var usingKey = Seq("USING KEY", AltPrio(Expr<SimpleName>(), Expr<Dynamic>()));

        var options = Per(Expr<LoopTarget>(), from, to, where, usingKey, group, step);

        var at = Seq(Opt(Seq("SCREEN", FailCombinator())),
                       Opt(Ver(Release.v740sp08, "GROUP", also: AlsoIn.OpenABAP)),
                       Expr<LoopSource>(),
                       Opt(options));

        return Seq("LOOP AT", at);
    }
}
