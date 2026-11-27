// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_into_structure.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLIntoStructure : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var intoSimple = Seq(OptPrio("CORRESPONDING FIELDS OF"), Expr<SQLTarget>());
        var indicators = OptPrio(Ver(Release.v915, Expr<SQLIndicators>()));

        return Seq("INTO", intoSimple, indicators);
    }
}
