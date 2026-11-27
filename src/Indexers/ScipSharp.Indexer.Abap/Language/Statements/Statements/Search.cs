// Ported from abaplint packages/core/src/abap/2_statements/statements/search.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Search : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var starting = Seq("STARTING AT", Expr<Source>());
        var ending = Seq("ENDING AT", Expr<Source>());
        var mark = Str("AND MARK");

        var mode = AltPrio("IN BYTE MODE", "IN CHARACTER MODE");

        var ret = Seq("SEARCH",
                        Expr<Source>(),
                        "FOR",
                        Expr<Source>(),
                        Opt(Per(mode, starting, ending, mark)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
