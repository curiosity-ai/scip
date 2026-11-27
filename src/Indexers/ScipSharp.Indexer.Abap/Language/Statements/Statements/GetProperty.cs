// Ported from abaplint packages/core/src/abap/2_statements/statements/get_property.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GetProperty : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var ret = Seq("GET PROPERTY OF",
                        Expr<FieldSub>(),
                        Alt(Expr<Source>(), Expr<ConstantString>()),
                        "=",
                        Expr<Source>(),
                        Opt("NO FLUSH"),
                        Opt(Alt("QUEUE-ONLY", "QUEUEONLY")),
                        Opt(Expr<OLEExporting>()));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
