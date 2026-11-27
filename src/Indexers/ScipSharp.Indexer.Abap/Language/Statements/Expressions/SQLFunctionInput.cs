// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_function_input.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLFunctionInput : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var hostParen = Seq(Tok<Tokens.ParenLeftW>(), Expr<Source>(), Tok<Tokens.WParenRightW>());
        var at = Ver(Release.v740sp05, Seq(Tok<Tokens.WAt>(), AltPrio(Expr<SimpleSource3>(), hostParen)));
        var parenInput = Seq("(", Expr<SQLFunctionInput>(), ")");

        var param = AltPrio(parenInput, Expr<SQLCase>(), Expr<SQLFunction>(), Expr<SQLAggregation>(), Expr<SQLFieldName>(), Expr<SQLAliasField>(), Expr<Constant>(), at);

        var @operator = AltPrio("+", "-", "*", "/", "&&");

        return Seq(param, StarPrio(Seq(@operator, param)));
    }
}
