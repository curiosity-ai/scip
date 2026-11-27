// Ported from abaplint packages/core/src/abap/2_statements/statements/get_pf_status.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GetPFStatus : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var program = Seq("PROGRAM", Expr<Source>());
        var excl = Seq("EXCLUDING", Expr<Source>());

        var ret = Seq("GET PF-STATUS",
                        Expr<Target>(),
                        Opt(program),
                        Opt(excl));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
