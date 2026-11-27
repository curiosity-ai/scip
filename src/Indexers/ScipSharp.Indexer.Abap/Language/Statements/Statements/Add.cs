// Ported from abaplint packages/core/src/abap/2_statements/statements/add.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Add : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var to = Seq("TO", Expr<Target>());
        var accordingTo = Seq("ACCORDING TO", Expr<Source>());
        var giving = Seq("GIVING", Expr<Source>());
        var then = Seq("THEN", Expr<Source>(), "UNTIL", Expr<Source>(), Opt(Per(giving, accordingTo)), Opt(to));

        var ret = Seq("ADD", Expr<Source>(), AltPrio(to, then));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
