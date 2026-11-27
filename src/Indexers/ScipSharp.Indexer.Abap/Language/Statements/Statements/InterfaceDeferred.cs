// Ported from abaplint packages/core/src/abap/2_statements/statements/interface_deferred.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InterfaceDeferred : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        return VerNotLang(LanguageVersion.KeyUser, Seq("INTERFACE",
                                                       Expr<InterfaceName>(),
                                                       "DEFERRED",
                                                       Opt("PUBLIC")));
    }
}
