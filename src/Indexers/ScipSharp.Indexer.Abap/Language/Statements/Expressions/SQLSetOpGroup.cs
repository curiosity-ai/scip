// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_set_op_group.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLSetOpGroup : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var operand = AltPrio(Expr<SQLSetOpGroup>(), Seq("SELECT", SelectCore.BuildSelectCore(false, false)));
        var chain = Seq(operand, OptPrio(Seq(Ver(Release.v750, PlusPrio(Expr<SQLSetOp>()), also: AlsoIn.OpenABAP), OptPrio(Expr<SQLOrderBy>()))));

        return Ver(Release.v750, Seq("(", chain, ")"), also: AlsoIn.OpenABAP);
    }
}
