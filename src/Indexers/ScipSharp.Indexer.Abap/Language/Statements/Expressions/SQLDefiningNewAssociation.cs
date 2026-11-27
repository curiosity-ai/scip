// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_defining_new_association.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLDefiningNewAssociation : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var joinTag = OptPrio(Regex(@"^#[A-Z][A-Z0-9_]*$", "i"));
        var target = AltPrio(new DatabaseTable(true), Expr<WithName>());
        return Ver(Release.v751, Seq(
          "JOIN",
          joinTag,
          OptPrio(Expr<SQLPathJoinType>()),
          target,
          "AS",
          Expr<SQLAsName>(),
          "ON",
          Expr<SQLCond>()));
    }
}
