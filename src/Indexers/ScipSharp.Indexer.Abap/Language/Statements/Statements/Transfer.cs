// Ported from abaplint packages/core/src/abap/2_statements/statements/transfer.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Transfer : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var length = Seq("LENGTH", Expr<Source>());

        var ret = Seq("TRANSFER",
                        Expr<Source>(),
                        "TO",
                        Expr<Source>(),
                        Opt(length),
                        Opt("NO END OF LINE"));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
