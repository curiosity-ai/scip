// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_associations_list.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLAssociationsList : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return Ver(Release.v751, Seq(
          Tok<Tokens.WParenLeftW>(),
          new SQLAssociationEntry(),
          Star(Seq(",", new SQLAssociationEntry())),
          Tok<Tokens.WParenRightW>()));
    }
}
