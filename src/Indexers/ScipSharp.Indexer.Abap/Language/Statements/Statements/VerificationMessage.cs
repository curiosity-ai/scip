// Ported from abaplint packages/core/src/abap/2_statements/statements/verification_message.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class VerificationMessage : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var priority = Seq("PRIORITY", Expr<Source>());

        var ret = Seq("VERIFICATION-MESSAGE", Expr<Source>(), Expr<Source>(), Opt(priority));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
