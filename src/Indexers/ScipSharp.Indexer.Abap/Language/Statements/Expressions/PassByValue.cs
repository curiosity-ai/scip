// Ported from abaplint packages/core/src/abap/2_statements/expressions/pass_by_value.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class PassByValue : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var value = Seq("VALUE",
                          Tok<Tokens.ParenLeft>(),
                          Expr<FormParamName>(),
                          Tok<Tokens.ParenRightW>());

        return value;
    }
}
