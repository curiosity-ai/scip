// Ported from abaplint packages/core/src/abap/2_statements/statements/call_ole.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallOLE : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var rc = Seq("=", Expr<Target>());

        var ret = Seq("CALL METHOD OF",
                        Expr<Source>(),
                        Expr<Source>(),
                        Opt(rc),
                        Opt("NO FLUSH"),
                        Opt(Alt("QUEUE-ONLY", "QUEUEONLY")),
                        Opt(Expr<OLEExporting>()));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
