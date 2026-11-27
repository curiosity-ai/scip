// Ported from abaplint packages/core/src/abap/2_statements/statements/fetch_next_cursor.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FetchNextCursor : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var size = Seq("PACKAGE SIZE", Expr<SQLSourceSimple>());
        var extResult = OptPrio(Ver(Release.v766, Expr<SQLExtendedResult>()));

        var ret = Seq("FETCH NEXT CURSOR",
                        Expr<SQLSourceSimple>(),
                        Alt(Expr<SQLIntoStructure>(), Expr<SQLIntoTable>(), Expr<SQLIntoList>()),
                        extResult,
                        OptPrio(size));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
