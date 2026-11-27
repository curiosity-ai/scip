// Ported from abaplint packages/core/src/abap/2_statements/statements/cleanup.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Cleanup : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var into = Seq("INTO", Expr<Target>());

        return Seq("CLEANUP", Opt(into));
    }
}
