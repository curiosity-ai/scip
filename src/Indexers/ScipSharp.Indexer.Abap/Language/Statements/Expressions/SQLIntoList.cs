// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_into_list.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLIntoList : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var intoList = Seq(AltPrio(Tok<Tokens.WParenLeft>(), Ver(Release.v740sp02, Tok<Tokens.WParenLeftW>(), also: AlsoIn.OpenABAP)),
                             StarPrio(Seq(Expr<SQLTarget>(), ",")),
                             Expr<SQLTarget>(),
                             ")");

        return Seq("INTO", intoList);
    }
}
