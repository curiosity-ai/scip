// Ported from abaplint packages/core/src/abap/2_statements/statements/call.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// method call
public class Call : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var call = VerNotLang(LanguageVersion.KeyUser, Seq("CALL METHOD", Expr<MethodSource>(), Expr<MethodCallBody>()));

        return AltPrio(call, Expr<MethodCallChain>());
    }
}
