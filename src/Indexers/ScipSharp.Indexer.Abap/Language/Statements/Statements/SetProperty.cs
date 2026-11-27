// Ported from abaplint packages/core/src/abap/2_statements/statements/set_property.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SetProperty : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("SET PROPERTY OF",
                        Expr<Source>(),
                        Alt(Expr<Expressions.Constant>(), Expr<Expressions.Field>()),
                        "=",
                        Expr<Source>(),
                        OptPrio("NO FLUSH"),
                        Opt(Expr<OLEExporting>()),
                        Opt("QUEUEONLY"));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
