// Ported from abaplint packages/core/src/abap/2_statements/statements/assert.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Assert : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var fields = Seq("FIELDS", Plus(Expr<Source>()));
        var subkey = Seq("SUBKEY", Expr<Source>());
        var id = Seq("ID", Expr<NamespaceSimpleName>());

        return VerNotLang(LanguageVersion.KeyUser, Seq("ASSERT",
                                                       OptPrio(id),
                                                       OptPrio(subkey),
                                                       Opt(fields),
                                                       OptPrio("CONDITION"),
                                                       Expr<Cond>()));
    }
}
