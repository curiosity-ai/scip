// Ported from abaplint packages/core/src/abap/2_statements/statements/function_pool.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FunctionPool : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var message = Seq("MESSAGE-ID", Expr<MessageClass>());
        var line = Seq("LINE-SIZE", Expr<Integer>());
        var no = Str("NO STANDARD PAGE HEADING");

        return VerNotLang(LanguageVersion.KeyUser, Seq("FUNCTION-POOL",
                                                       Expr<IncludeName>(),
                                                       Opt(Per(message, line, no))));
    }
}
