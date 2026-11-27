// Ported from abaplint packages/core/src/abap/2_statements/statements/system_call.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SystemCall : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var anyy = Regex(@"^.+$");

        var objmgr = Seq("OBJMGR CLONE", Expr<Source>(), "TO", Expr<Target>());
        var did = Seq(anyy, "DID", Expr<Source>(), "PARAMETERS", Plus(Expr<Source>()));

        var ret = Seq("SYSTEM-CALL", AltPrio(objmgr, did, Plus(anyy)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
