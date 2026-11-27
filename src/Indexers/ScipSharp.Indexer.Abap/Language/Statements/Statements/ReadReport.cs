// Ported from abaplint packages/core/src/abap/2_statements/statements/read_report.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReadReport : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var state = Seq("STATE", Expr<Source>());
        var into = Seq("INTO", Expr<Target>());
        var maximum = Seq("MAXIMUM WIDTH INTO", Expr<Target>());

        var ret = Seq("READ REPORT",
                        Expr<Source>(),
                        Per(state, into, maximum));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
