// Ported from abaplint packages/core/src/abap/2_statements/statements/do.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Do : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var range = Seq("RANGE", Expr<Source>());

        var vary = Seq("VARYING",
                         Expr<Target>(),
                         "FROM",
                         Expr<Source>(),
                         "NEXT",
                         Expr<Source>(),
                         OptPrio(range));

        var times = Seq(Expr<Source>(), "TIMES");

        return Seq("DO", OptPrio(Per(Plus(vary), times)));
    }
}
