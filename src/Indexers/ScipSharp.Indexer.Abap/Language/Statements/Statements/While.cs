// Ported from abaplint packages/core/src/abap/2_statements/statements/while.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class While : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var vary = Seq("VARY", Expr<Target>(), "FROM", Expr<Source>(), "NEXT", Expr<Source>());

        return Seq("WHILE", Expr<Cond>(), Star(vary));
    }
}
