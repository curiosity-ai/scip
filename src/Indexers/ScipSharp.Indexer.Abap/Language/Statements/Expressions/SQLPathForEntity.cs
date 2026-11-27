// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_path_for_entity.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLPathForEntity : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var prefix = AltPrio(new SQLCTEPathPrefix(), new DatabaseTable());
        return Ver(Release.v740sp05, Seq(OptPrio(prefix), Plus(new SQLPathSegment())));
    }
}
