// Ported from abaplint packages/core/src/abap/2_statements/statements/catch.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Catch : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        return Seq("CATCH",
                   OptPrio("BEFORE UNWIND"),
                   Plus(Expr<ClassName>()),
                   Opt(Seq("INTO", Expr<Target>())));
    }
}
