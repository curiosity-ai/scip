// Ported from abaplint packages/core/src/abap/2_statements/statements/call_kernel.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallKernel : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("CALL",
                        AltPrio(Expr<Expressions.Constant>(), Expr<Expressions.Field>()),
                        StarPrio(Expr<KernelId>()));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
