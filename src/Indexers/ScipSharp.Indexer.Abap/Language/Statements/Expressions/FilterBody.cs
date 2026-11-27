// Ported from abaplint packages/core/src/abap/2_statements/expressions/filter_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FilterBody : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var inn = Seq("IN", Expr<Source>());
        var @using = Seq("USING KEY", Expr<SimpleName>());
        return Seq(
          Expr<Source>(),
          OptPrio("EXCEPT"),
          OptPrio(Per(inn, @using)),
          Seq("WHERE", Expr<ComponentCond>()));
    }
}
