// Ported from abaplint packages/core/src/abap/2_statements/expressions/corresponding_body_mapping.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CorrespondingBodyMapping : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var mapping = Seq("MAPPING", Plus(Seq(Expr<ComponentName>(), "=", Expr<ComponentChain>())));
        return mapping;
    }
}
