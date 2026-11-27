// Ported from abaplint packages/core/src/abap/2_statements/statements/call_transaction.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallTransaction : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var options = Seq("OPTIONS FROM", Expr<Source>());
        var messages = Seq("MESSAGES INTO", Expr<Target>());

        var auth = Ver(Release.v740sp02, Seq(AltPrio("WITH", "WITHOUT"), "AUTHORITY-CHECK"), also: AlsoIn.OpenABAP);

        var perm = Per(Seq("UPDATE", Expr<Source>()),
                         "AND SKIP FIRST SCREEN",
                         options,
                         messages,
                         Seq("MODE", Expr<Source>()));

        var ret = Seq("CALL TRANSACTION",
                        Expr<Source>(),
                        OptPrio(auth),
                        OptPrio(Seq("USING", Expr<Source>())),
                        Opt(perm));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
