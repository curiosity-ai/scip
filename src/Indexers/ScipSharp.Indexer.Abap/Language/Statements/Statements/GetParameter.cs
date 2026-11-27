// Ported from abaplint packages/core/src/abap/2_statements/statements/get_parameter.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GetParameter : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("GET PARAMETER ID",
                        Expr<Source>(),
                        "FIELD",
                        Expr<Target>());

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
