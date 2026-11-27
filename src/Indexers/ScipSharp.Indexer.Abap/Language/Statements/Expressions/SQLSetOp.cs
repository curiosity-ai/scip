// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_set_op.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLSetOp : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var operand = AltPrio(Expr<SQLSetOpGroup>(), Seq("SELECT", SelectCore.BuildSelectCore(false, false)));

        var union = Seq("UNION", OptPrio(AltPrio("DISTINCT", "ALL")));
        var intersectExcept = AltPrio(Seq("INTERSECT", OptPrio("DISTINCT")),
                                        Seq("EXCEPT", OptPrio("DISTINCT")));

        var op = AltPrio(Ver(Release.v750, union, also: AlsoIn.OpenABAP),
                           Ver(Release.v756, intersectExcept));

        return Seq(op, operand);
    }
}
