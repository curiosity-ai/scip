// Ported from abaplint packages/core/src/abap/2_statements/statements/set_handler.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SetHandler : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var activation = Seq("ACTIVATION", Expr<Source>());

        var fo = Seq("FOR", AltPrio("ALL INSTANCES", Expr<Source>()));

        var ret = Seq("SET HANDLER",
                        Plus(Expr<MethodSource>()),
                        OptPrio(fo),
                        OptPrio(activation));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
