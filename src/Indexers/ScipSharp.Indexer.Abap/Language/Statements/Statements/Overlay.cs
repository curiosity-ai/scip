// Ported from abaplint packages/core/src/abap/2_statements/statements/overlay.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Overlay : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var only = Seq("ONLY", Expr<Source>());

        var ret = Seq("OVERLAY",
                        Expr<Target>(),
                        "WITH",
                        Expr<Source>(),
                        Opt(only));

        return ret;
    }
}
