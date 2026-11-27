// Ported from abaplint packages/core/src/abap/2_statements/expressions/inline_loop_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InlineLoopDefinition : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var index = Seq("INDEX INTO", Expr<TargetField>());
        var key = Seq("USING KEY", Expr<SimpleName>());
        return Seq(AltPrio(Expr<TargetFieldSymbol>(), Expr<TargetField>()), "IN", Opt("GROUP"), Expr<Source>(), OptPrio(key), OptPrio(index));
    }
}
