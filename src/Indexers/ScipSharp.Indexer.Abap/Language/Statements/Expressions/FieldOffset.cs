// Ported from abaplint packages/core/src/abap/2_statements/expressions/field_offset.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FieldOffset : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var offset = Seq(Tok<Tokens.Plus>(),
                           OptPrio(AltPrio(Regex(@"^\d+$"), Expr<SimpleFieldChain2>())));

        return offset;
    }
}
