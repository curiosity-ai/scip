// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_for_all_entries.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLForAllEntries : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var forAll = Seq("FOR ALL ENTRIES IN", Expr<SQLSource>());
        return forAll;
    }
}
