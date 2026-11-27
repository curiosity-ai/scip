// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_association_entry.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLAssociationEntry : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var alias = Seq("AS", Expr<SQLAsName>());
        var redirected = Seq("REDIRECTED TO", Expr<WithName>(), "VIA", Expr<DatabaseTable>());
        var existing = Seq(new SQLPathForEntity(), OptPrio(alias), OptPrio(redirected));

        return Ver(Release.v751, AltPrio(new SQLDefiningNewAssociation(), existing));
    }
}
