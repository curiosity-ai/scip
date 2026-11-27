// Ported from abaplint packages/core/src/abap/2_statements/statements/authority_check.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class AuthorityCheck : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var field = Seq("FIELD", Expr<Source>());

        var id = Seq("ID",
                       Expr<Source>(),
                       Alt(field, "DUMMY"));

        var ret = Seq("AUTHORITY-CHECK OBJECT",
                        Expr<Source>(),
                        Opt(Seq("FOR USER", Expr<Source>())),
                        Plus(id));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
