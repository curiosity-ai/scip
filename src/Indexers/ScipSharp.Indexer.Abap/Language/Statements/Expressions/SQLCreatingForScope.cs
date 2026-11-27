// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_creating_for_scope.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLCreatingForScope : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var forAllOther = Seq("FOR ALL OTHER",
                                AltPrio("BLOB COLUMNS", "CLOB COLUMNS", "COLUMNS"));
        var forAll = Seq("FOR ALL",
                           AltPrio("BLOB COLUMNS", "CLOB COLUMNS", "COLUMNS"));
        var forColumns = Seq("FOR", Expr<SQLCreatingColumns>());

        return AltPrio(forAllOther, forAll, forColumns);
    }
}
