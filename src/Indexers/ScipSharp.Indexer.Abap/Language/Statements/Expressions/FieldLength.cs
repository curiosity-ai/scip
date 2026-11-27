// Ported from abaplint packages/core/src/abap/2_statements/expressions/field_length.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FieldLength : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var normal = Seq(OptPrio(Tok<Tokens.Plus>()),
                           AltPrio(Regex(@"^\d+$"), Expr<SimpleFieldChain2>()));

        var length = Seq(Tok<Tokens.ParenLeft>(),
                           OptPrio(AltPrio(normal, "*")),
                           AltPrio(Tok<Tokens.ParenRightW>(), Tok<Tokens.ParenRight>()));

        return length;
    }
}
